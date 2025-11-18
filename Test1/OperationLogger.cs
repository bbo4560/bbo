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
                string sql = @"CREATE TABLE IF NOT EXISTS operation_logs (
                               id SERIAL PRIMARY KEY,
                               timestamp TIMESTAMP NOT NULL,
                               operation_type VARCHAR(100) NOT NULL,
                               target TEXT NOT NULL,
                               detail TEXT
                            );";
                conn.Execute(sql);
            }
            catch
            {
                // 如果資料庫連接失敗，忽略錯誤，將使用本地檔案備份
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
            
            // 優先寫入資料庫
            try
            {
                EnsureOperationLogsTableExists();
                using var conn = new NpgsqlConnection(DbConnStr);
                conn.Open();
                conn.Execute(
                    "INSERT INTO operation_logs (timestamp, operation_type, target, detail) VALUES (@Timestamp, @OperationType, @Target, @Detail)",
                    new { entry.Timestamp, OperationType = entry.OperationType, Target = entry.Target, Detail = entry.Detail });
                dbSuccess = true;
            }
            catch (Exception ex)
            {
                // 資料庫寫入失敗，記錄錯誤
                System.Diagnostics.Debug.WriteLine($"資料庫寫入操作紀錄失敗: {ex.Message}");
            }
            
            // 如果資料庫寫入失敗，寫入本地檔案作為備份
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
            
            // 從資料庫讀取
            try
            {
                EnsureOperationLogsTableExists();
                using var conn = new NpgsqlConnection(DbConnStr);
                conn.Open();
                
                // 使用 SQL 別名確保正確映射到 C# 屬性（Dapper 預設區分大小寫）
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
                // 資料庫讀取失敗，記錄但不中斷
                System.Diagnostics.Debug.WriteLine($"資料庫讀取操作紀錄失敗: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"堆疊追蹤: {ex.StackTrace}");
            }
            
            // 從本地檔案讀取（作為備份或補充）
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
            
            // 合併資料庫和本地檔案的紀錄，去除重複（根據時間戳和操作類型）
            var allEntries = new Dictionary<string, OperationLogEntry>();
            
            // 先添加資料庫的紀錄
            foreach (var entry in dbEntries)
            {
                var key = $"{entry.Timestamp:yyyyMMddHHmmss}_{entry.OperationType}_{entry.Target}";
                if (!allEntries.ContainsKey(key))
                {
                    allEntries[key] = entry;
                }
            }
            
            // 再添加本地檔案的紀錄（如果資料庫中沒有）
            foreach (var entry in fileEntries)
            {
                var key = $"{entry.Timestamp:yyyyMMddHHmmss}_{entry.OperationType}_{entry.Target}";
                if (!allEntries.ContainsKey(key))
                {
                    allEntries[key] = entry;
                    // 嘗試將本地檔案的紀錄遷移到資料庫
                    try
                    {
                        EnsureOperationLogsTableExists();
                        using var conn = new NpgsqlConnection(DbConnStr);
                        conn.Open();
                        conn.Execute(
                            "INSERT INTO operation_logs (timestamp, operation_type, target, detail) VALUES (@Timestamp, @OperationType, @Target, @Detail)",
                            new { entry.Timestamp, OperationType = entry.OperationType, Target = entry.Target, Detail = entry.Detail });
                    }
                    catch
                    {
                        // 遷移失敗，忽略
                    }
                }
            }
            
            entries = allEntries.Values.OrderBy(e => e.Timestamp).ToList();
            
            System.Diagnostics.Debug.WriteLine($"合併後總共 {entries.Count} 筆操作紀錄（資料庫: {dbEntries.Count}, 本地檔案: {fileEntries.Count}）");
            
            return entries;
        }
        
        /// <summary>
        /// 測試資料庫連接並返回資料庫中的操作紀錄數量
        /// </summary>
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
                return -1; // 表示連接失敗
            }
        }

        public static void Clear()
        {
            // 優先清除資料庫中的紀錄
            try
            {
                using var conn = new NpgsqlConnection(DbConnStr);
                conn.Open();
                conn.Execute("DELETE FROM operation_logs");
            }
            catch
            {
                // 如果資料庫清除失敗，清除本地檔案
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
