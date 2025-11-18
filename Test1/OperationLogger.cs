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

        private static readonly string DbConnStr = "Host=192.168.43.93;Username=postgres;Password=1234;Database=panellogdb";

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

                    if (!hasTimestamp)
                    {
                        conn.Execute("ALTER TABLE operation_logs ADD COLUMN timestamp TIMESTAMP");
                        conn.Execute("UPDATE operation_logs SET timestamp = NOW() WHERE timestamp IS NULL");
                        conn.Execute("ALTER TABLE operation_logs ALTER COLUMN timestamp SET NOT NULL");
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"確保操作紀錄表存在失敗: {ex.Message}");
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

            // 補值確保 timestamp 一定合法
            if (entry.Timestamp == default(DateTime) || entry.Timestamp == DateTime.MinValue)
            {
                entry.Timestamp = DateTime.Now;
            }

            bool dbSuccess = false;

            try
            {
                EnsureOperationLogsTableExists();
                using var conn = new NpgsqlConnection(DbConnStr);
                conn.Open();

                conn.Execute(
                    "INSERT INTO operation_logs (timestamp, operation_type, target, detail) VALUES (@Timestamp, @OperationType, @Target, @Detail)",
                    new { Timestamp = entry.Timestamp, OperationType = entry.OperationType, Target = entry.Target, Detail = entry.Detail });
                dbSuccess = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"資料庫寫入操作紀錄失敗: {ex.Message}");
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

                var sql = @"SELECT 
                    id AS Id,
                    timestamp AS Timestamp,
                    operation_type AS OperationType,
                    target AS Target,
                    detail AS Detail
                    FROM operation_logs 
                    ORDER BY timestamp ASC";

                dbEntries = conn.Query<OperationLogEntry>(sql).ToList();

                System.Diagnostics.Debug.WriteLine($"從資料庫讀取到 {dbEntries.Count} 筆操作紀錄");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"資料庫讀取操作紀錄失敗: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"堆疊追蹤: {ex.StackTrace}");
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
                                    if (entry.Timestamp == default(DateTime) || entry.Timestamp == DateTime.MinValue)
                                        entry.Timestamp = DateTime.Now;
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

                    foreach (var entry in entriesToMigrate)
                    {
                        try
                        {
                            if (entry.Timestamp == default(DateTime) || entry.Timestamp == DateTime.MinValue)
                                entry.Timestamp = DateTime.Now;

                            conn.Execute(
                                "INSERT INTO operation_logs (timestamp, operation_type, target, detail) VALUES (@Timestamp, @OperationType, @Target, @Detail)",
                                new { Timestamp = entry.Timestamp, OperationType = entry.OperationType, Target = entry.Target, Detail = entry.Detail });
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"遷移操作紀錄失敗: {ex.Message}");
                            System.Diagnostics.Debug.WriteLine($"操作類型: {entry.OperationType}, 目標: {entry.Target}, 時間: {entry.Timestamp}");
                        }
                    }
                    System.Diagnostics.Debug.WriteLine($"成功遷移 {entriesToMigrate.Count} 筆本地檔案紀錄到資料庫");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"批量遷移操作紀錄失敗: {ex.Message}");
                }
            }

            entries = allEntries.Values.OrderBy(e => e.Timestamp).ToList();

            System.Diagnostics.Debug.WriteLine($"合併後總共 {entries.Count} 筆操作紀錄（資料庫: {dbEntries.Count}, 本地檔案: {fileEntries.Count}）");

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
                                if (entry.Timestamp == default(DateTime) || entry.Timestamp == DateTime.MinValue)
                                    entry.Timestamp = DateTime.Now;
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

                    int successCount = 0;
                    foreach (var entry in fileEntries)
                    {
                        try
                        {
                            if (entry.Timestamp == default(DateTime) || entry.Timestamp == DateTime.MinValue)
                                entry.Timestamp = DateTime.Now;

                            var existing = conn.QueryFirstOrDefault<int?>(
                                "SELECT id FROM operation_logs WHERE timestamp = @Timestamp AND operation_type = @OperationType AND target = @Target",
                                new { Timestamp = entry.Timestamp, OperationType = entry.OperationType, Target = entry.Target });

                            if (existing == null)
                            {
                                conn.Execute(
                                    "INSERT INTO operation_logs (timestamp, operation_type, target, detail) VALUES (@Timestamp, @OperationType, @Target, @Detail)",
                                    new { Timestamp = entry.Timestamp, OperationType = entry.OperationType, Target = entry.Target, Detail = entry.Detail });
                                successCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"遷移單筆操作紀錄失敗: {ex.Message}");
                            System.Diagnostics.Debug.WriteLine($"操作類型: {entry.OperationType}, 目標: {entry.Target}, 時間: {entry.Timestamp}");
                        }
                    }

                    if (successCount > 0)
                    {
                        File.Delete(InternalLogFilePath);
                        System.Diagnostics.Debug.WriteLine($"成功遷移 {successCount} 筆本地檔案紀錄到資料庫，並刪除本地檔案");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"遷移本地檔案到資料庫失敗: {ex.Message}");
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

