using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Dapper;
using Npgsql;

namespace Test1
{
    public static class OperationLogger
    {
        public class OperationLogEntry
        {
            public int Id { get; set; }
            public DateTime Timestamp { get; set; }
            public string OperationType { get; set; } = string.Empty;
            public string Target { get; set; } = string.Empty;
            public string? Detail { get; set; }
        }

        private static readonly object SyncRoot = new object();
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Test1");
        private static readonly string InternalLogFilePath = Path.Combine(LogDirectory, "operations.log");

        public static string LogFilePath => InternalLogFilePath;

        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = false
        };

        private static readonly string DbConnStr = "Host=172.20.10.2;Username=postgres;Password=1234;Database=panellogdb";

        private static void EnsureOperationLogsTableExists()
        {
            try
            {
                using var conn = new NpgsqlConnection(DbConnStr);
                conn.Open();
                
                var tableExists = conn.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'operation_logs'") > 0;
                
                if (!tableExists)
                {
                    string sql = @"CREATE TABLE operation_logs (
                                   id SERIAL PRIMARY KEY,
                                   timestamp TIMESTAMP NOT NULL,
                                   operation_type VARCHAR(100) NOT NULL,
                                   target TEXT NOT NULL,
                                   detail TEXT
                                );";
                    conn.Execute(sql);
                }
                else
                {
                    var hasTimestamp = conn.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'operation_logs' AND column_name = 'timestamp'") > 0;
                    
                    var hasActionTime = conn.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'operation_logs' AND column_name = 'action_time'") > 0;
                    
                    if (!hasTimestamp && hasActionTime)
                    {
                        conn.Execute("ALTER TABLE operation_logs RENAME COLUMN action_time TO timestamp");
                    }
                    else if (!hasTimestamp)
                    {
                        var hasTime = conn.ExecuteScalar<int>(
                            "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'operation_logs' AND column_name = 'time'") > 0;
                        
                        if (hasTime)
                        {
                            conn.Execute("ALTER TABLE operation_logs RENAME COLUMN time TO timestamp");
                        }
                        else
                        {
                            conn.Execute("ALTER TABLE operation_logs ADD COLUMN timestamp TIMESTAMP");
                            conn.Execute("UPDATE operation_logs SET timestamp = NOW() WHERE timestamp IS NULL");
                            conn.Execute("ALTER TABLE operation_logs ALTER COLUMN timestamp SET NOT NULL");
                        }
                    }
                    
                    var hasOperationType = conn.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'operation_logs' AND column_name = 'operation_type'") > 0;
                    
                    if (!hasOperationType)
                    {
                        conn.Execute("ALTER TABLE operation_logs ADD COLUMN operation_type VARCHAR(100)");
                        conn.Execute("UPDATE operation_logs SET operation_type = '' WHERE operation_type IS NULL");
                        conn.Execute("ALTER TABLE operation_logs ALTER COLUMN operation_type SET NOT NULL");
                    }
                    
                    var hasTarget = conn.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'operation_logs' AND column_name = 'target'") > 0;
                    
                    if (!hasTarget)
                    {
                        conn.Execute("ALTER TABLE operation_logs ADD COLUMN target TEXT");
                        conn.Execute("UPDATE operation_logs SET target = '' WHERE target IS NULL");
                        conn.Execute("ALTER TABLE operation_logs ALTER COLUMN target SET NOT NULL");
                    }
                    
                    var hasDetail = conn.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'operation_logs' AND column_name = 'detail'") > 0;
                    
                    if (!hasDetail)
                    {
                        conn.Execute("ALTER TABLE operation_logs ADD COLUMN detail TEXT");
                    }
                }
            }
            catch
            {
            }
        }

        public static string DescribeChanges(PanelLog before, PanelLog after)
        {
            var changes = new List<string>();
            if (before.Panel_ID != after.Panel_ID)
                changes.Add($"PanelID: {before.Panel_ID} -> {after.Panel_ID}");
            if (before.LOT_ID != after.LOT_ID)
                changes.Add($"LOTID: {before.LOT_ID} -> {after.LOT_ID}");
            if (before.Carrier_ID != after.Carrier_ID)
                changes.Add($"CarrierID: {before.Carrier_ID} -> {after.Carrier_ID}");
            if (before.Time != after.Time)
                changes.Add($"Time: {before.Time:yyyy/MM/dd HH:mm:ss} -> {after.Time:yyyy/MM/dd HH:mm:ss}");

            return changes.Count > 0 ? string.Join(", ", changes) : "未變更";
        }

        public static void Log(string operationType, string target, string? detail = null, DateTime? timestamp = null)
        {
            var entry = new OperationLogEntry
            {
                Timestamp = timestamp ?? DateTime.Now,
                OperationType = operationType,
                Target = target,
                Detail = detail
            };

            bool dbSuccess = false;
            
            try
            {
                EnsureOperationLogsTableExists();
                using var conn = new NpgsqlConnection(DbConnStr);
                conn.Open();
                
                var hasActionTime = conn.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'operation_logs' AND column_name = 'action_time'") > 0;
                
                string timeColumnName = hasActionTime ? "action_time" : "timestamp";
                
                conn.Execute(
                    $"INSERT INTO operation_logs ({timeColumnName}, operation_type, target, detail) VALUES (@Timestamp, @OperationType, @Target, @Detail)",
                    new { entry.Timestamp, OperationType = entry.OperationType, Target = entry.Target, Detail = entry.Detail });
                dbSuccess = true;
            }
            catch
            {
            }
            
            if (!dbSuccess)
            {
                var line = JsonSerializer.Serialize(entry, SerializerOptions);
                try
                {
                    lock (SyncRoot)
                    {
                        if (!Directory.Exists(LogDirectory))
                        {
                            Directory.CreateDirectory(LogDirectory);
                        }
                        File.AppendAllText(InternalLogFilePath, line + Environment.NewLine, Encoding.UTF8);
                    }
                }
                catch
                {
                }
            }
        }

        public static IReadOnlyList<OperationLogEntry> ReadAll()
        {
            var entries = new List<OperationLogEntry>();
            var dbEntries = new List<OperationLogEntry>();
            var fileEntries = new List<OperationLogEntry>();
            
            try
            {
                EnsureOperationLogsTableExists();
                using var conn = new NpgsqlConnection(DbConnStr);
                conn.Open();
                
                var hasActionTime = conn.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'operation_logs' AND column_name = 'action_time'") > 0;
                
                string timeColumnName = hasActionTime ? "action_time" : "timestamp";
                
                var sql = $@"SELECT 
                    id AS Id,
                    {timeColumnName} AS Timestamp,
                    operation_type AS OperationType,
                    target AS Target,
                    detail AS Detail
                    FROM operation_logs 
                    ORDER BY {timeColumnName} ASC";
                
                dbEntries = conn.Query<OperationLogEntry>(sql).ToList();
            }
            catch
            {
            }
            
            try
            {
                lock (SyncRoot)
                {
                    if (File.Exists(InternalLogFilePath))
                    {
                        foreach (var line in File.ReadLines(InternalLogFilePath, Encoding.UTF8))
                        {
                            if (string.IsNullOrWhiteSpace(line))
                                continue;
                            try
                            {
                                var entry = JsonSerializer.Deserialize<OperationLogEntry>(line, SerializerOptions);
                                if (entry != null)
                                {
                                    fileEntries.Add(entry);
                                }
                            }
                            catch
                            {
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            
            var allEntries = new Dictionary<string, OperationLogEntry>();
            
            foreach (var entry in dbEntries)
            {
                var key = $"{entry.Timestamp:yyyyMMddHHmmss}_{entry.OperationType}_{entry.Target}";
                if (!allEntries.ContainsKey(key))
                {
                    allEntries[key] = entry;
                }
            }
            
            var entriesToMigrate = new List<OperationLogEntry>();
            foreach (var entry in fileEntries)
            {
                var key = $"{entry.Timestamp:yyyyMMddHHmmss}_{entry.OperationType}_{entry.Target}";
                if (!allEntries.ContainsKey(key))
                {
                    allEntries[key] = entry;
                    entriesToMigrate.Add(entry);
                }
            }
            
            if (entriesToMigrate.Count > 0)
            {
                try
                {
                    EnsureOperationLogsTableExists();
                    using var conn = new NpgsqlConnection(DbConnStr);
                    conn.Open();
                    
                    var hasActionTime = conn.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'operation_logs' AND column_name = 'action_time'") > 0;
                    
                    string timeColumnName = hasActionTime ? "action_time" : "timestamp";
                    
                    foreach (var entry in entriesToMigrate)
                    {
                        try
                        {
                            var timestamp = entry.Timestamp;
                            if (timestamp == DateTime.MinValue || timestamp == default(DateTime))
                            {
                                timestamp = DateTime.Now;
                            }
                            
                            conn.Execute(
                                $"INSERT INTO operation_logs ({timeColumnName}, operation_type, target, detail) VALUES (@Timestamp, @OperationType, @Target, @Detail)",
                                new { Timestamp = timestamp, OperationType = entry.OperationType, Target = entry.Target, Detail = entry.Detail });
                        }
                        catch
                        {
                        }
                    }
                }
                catch
                {
                }
            }
            
            entries = allEntries.Values.OrderBy(e => e.Timestamp).ToList();
            
            return entries;
        }
        
        public static int GetDatabaseRecordCount()
        {
            try
            {
                using var conn = new NpgsqlConnection(DbConnStr);
                conn.Open();
                var count = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM operation_logs");
                return count;
            }
            catch
            {
                return -1;
            }
        }

        public static void MigrateLocalFileToDatabase()
        {
            try
            {
                lock (SyncRoot)
                {
                    if (!File.Exists(InternalLogFilePath))
                        return;
                    
                    var fileEntries = new List<OperationLogEntry>();
                    foreach (var line in File.ReadLines(InternalLogFilePath, Encoding.UTF8))
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;
                        try
                        {
                            var entry = JsonSerializer.Deserialize<OperationLogEntry>(line, SerializerOptions);
                            if (entry != null)
                            {
                                fileEntries.Add(entry);
                            }
                        }
                        catch
                        {
                        }
                    }
                    
                    if (fileEntries.Count == 0)
                        return;
                    
                    EnsureOperationLogsTableExists();
                    using var conn = new NpgsqlConnection(DbConnStr);
                    conn.Open();
                    
                    var hasActionTime = conn.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'operation_logs' AND column_name = 'action_time'") > 0;
                    
                    string timeColumnName = hasActionTime ? "action_time" : "timestamp";
                    
                    int successCount = 0;
                    foreach (var entry in fileEntries)
                    {
                        try
                        {
                            var timestamp = entry.Timestamp;
                            if (timestamp == DateTime.MinValue || timestamp == default(DateTime))
                            {
                                timestamp = DateTime.Now;
                            }
                            
                            var existing = conn.QueryFirstOrDefault<int?>(
                                $"SELECT id FROM operation_logs WHERE {timeColumnName} = @Timestamp AND operation_type = @OperationType AND target = @Target",
                                new { Timestamp = timestamp, OperationType = entry.OperationType, Target = entry.Target });
                            
                            if (existing == null)
                            {
                                conn.Execute(
                                    $"INSERT INTO operation_logs ({timeColumnName}, operation_type, target, detail) VALUES (@Timestamp, @OperationType, @Target, @Detail)",
                                    new { Timestamp = timestamp, OperationType = entry.OperationType, Target = entry.Target, Detail = entry.Detail });
                                successCount++;
                            }
                        }
                        catch
                        {
                        }
                    }
                    
                    if (successCount > 0)
                    {
                        File.Delete(InternalLogFilePath);
                    }
                }
            }
            catch
            {
            }
        }

        public static void Clear()
        {
            try
            {
                using var conn = new NpgsqlConnection(DbConnStr);
                conn.Open();
                conn.Execute("DELETE FROM operation_logs");
            }
            catch
            {
                try
                {
                    lock (SyncRoot)
                    {
                        if (File.Exists(InternalLogFilePath))
                        {
                            File.Delete(InternalLogFilePath);
                        }
                    }
                }
                catch
                {
                }
            }
        }
    }
}
