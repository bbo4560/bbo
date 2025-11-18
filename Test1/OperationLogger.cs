using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Test1
{
    public static class OperationLogger
    {
        public class OperationLogEntry
        {
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

        public static IReadOnlyList<OperationLogEntry> ReadAll()
        {
            var entries = new List<OperationLogEntry>();
            try
            {
                lock (SyncRoot)
                {
                    if (!File.Exists(InternalLogFilePath))
                        return entries;

                    foreach (var line in File.ReadLines(InternalLogFilePath, Encoding.UTF8))
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;
                        try
                        {
                            var entry = JsonSerializer.Deserialize<OperationLogEntry>(line, SerializerOptions);
                            if (entry != null)
                            {
                                entries.Add(entry);
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
            }
            return entries;
        }

        public static void Clear()
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
