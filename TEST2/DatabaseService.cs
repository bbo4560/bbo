using Dapper;
using Npgsql;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TEST2
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(string connectionString)
        {
            _connectionString = connectionString;
        }

        private IDbConnection CreateConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }

        public static void EnsureDatabaseExists(string connectionString)
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var originalDb = builder.Database;
            
            builder.Database = "postgres"; 
            
            using var connection = new NpgsqlConnection(builder.ToString());
            connection.Open();

            var exists = connection.ExecuteScalar<bool>(
                "SELECT 1 FROM pg_database WHERE datname = @name", 
                new { name = originalDb });

            if (!exists)
            {
                connection.Execute($"CREATE DATABASE \"{originalDb}\"");
            }
        }

        public async Task<IEnumerable<OperationRecord>> GetAllAsync()
        {
            using var connection = CreateConnection();
            string sql = "SELECT * FROM operation_records ORDER BY PanelID ASC, LOTID ASC, CarrierID ASC, Time ASC";
            return await connection.QueryAsync<OperationRecord>(sql);
        }

        public async Task<IEnumerable<OperationRecord>> GetFilteredAsync(
            string? panelId, string? lotId, string? carrierId, 
            DateTime? date, TimeSpan? time)
        {
            using var connection = CreateConnection();
            
            var sql = "SELECT * FROM operation_records WHERE 1=1";

            long? pId = null;
            long? lId = null;
            long? cId = null;

            if (!string.IsNullOrWhiteSpace(panelId) && long.TryParse(panelId, out long p))
            {
                pId = p;
                sql += " AND PanelID = @PanelID";
            }

            if (!string.IsNullOrWhiteSpace(lotId) && long.TryParse(lotId, out long l))
            {
                lId = l;
                sql += " AND LOTID = @LOTID";
            }

            if (!string.IsNullOrWhiteSpace(carrierId) && long.TryParse(carrierId, out long c))
            {
                cId = c;
                sql += " AND CarrierID = @CarrierID";
            }

            if (date.HasValue)
            {
                if (time.HasValue)
                {
                    sql += " AND Time = @ExactTime";
                }
                else
                {
                    sql += " AND Time >= @StartOfDay AND Time <= @EndOfDay";
                }
            }

            sql += " ORDER BY PanelID ASC, LOTID ASC, CarrierID ASC, Time ASC";

            var exactTime = date.HasValue && time.HasValue ? date.Value + time.Value : (DateTime?)null;
            var startOfDay = date.HasValue ? date.Value : (DateTime?)null;
            var endOfDay = date.HasValue ? date.Value.AddDays(1).AddTicks(-1) : (DateTime?)null;

            return await connection.QueryAsync<OperationRecord>(sql, new { 
                PanelID = pId,
                LOTID = lId,
                CarrierID = cId,
                ExactTime = exactTime,
                StartOfDay = startOfDay,
                EndOfDay = endOfDay
            });
        }

        public async Task InsertAsync(OperationRecord record)
        {
            using var connection = CreateConnection();
            string sql = @"INSERT INTO operation_records (Time, PanelID, LOTID, CarrierID) 
                           VALUES (@Time, @PanelID, @LOTID, @CarrierID)";
            await connection.ExecuteAsync(sql, record);
            
            await InsertLogAsync(new SystemLog
            {
                OperationTime = DateTime.Now,
                MachineName = Environment.MachineName,
                OperationType = "新增",
                AffectedData = $"Panel ID : {record.PanelID}",
                DetailDescription = $"LOTID: {record.LOTID}, CarrierID: {record.CarrierID}, Time: {record.Time}"
            });
        }

        public async Task UpdateAsync(OperationRecord record)
        {
            using var connection = CreateConnection();
            string sql = @"UPDATE operation_records 
                           SET Time = @Time, PanelID = @PanelID, LOTID = @LOTID, CarrierID = @CarrierID 
                           WHERE Id = @Id"; 
            await connection.ExecuteAsync(sql, record);

            await InsertLogAsync(new SystemLog
            {
                OperationTime = DateTime.Now,
                MachineName = Environment.MachineName,
                OperationType = "修改",
                AffectedData = $"Panel ID : {record.PanelID}",
                DetailDescription = $"New Values - LOTID: {record.LOTID}, CarrierID: {record.CarrierID}, Time: {record.Time}"
            });
        }

        public async Task DeleteAsync(int id)
        {
            using var connection = CreateConnection();
            
            string selectSql = "SELECT * FROM operation_records WHERE Id = @id";
            var record = await connection.QueryFirstOrDefaultAsync<OperationRecord>(selectSql, new { id });
            
            string sql = "DELETE FROM operation_records WHERE Id = @id";
            await connection.ExecuteAsync(sql, new { id });

            if (record != null)
            {
                await InsertLogAsync(new SystemLog
                {
                    OperationTime = DateTime.Now,
                    MachineName = Environment.MachineName,
                    OperationType = "刪除",
                    AffectedData = $"Panel ID : {record.PanelID}",
                    DetailDescription = "Record deleted"
                });
            }
        }

        public async Task DeleteBatchAsync(IEnumerable<int> ids)
        {
            var idList = ids.ToList();
            if (!idList.Any()) return;

            using var connection = CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try 
            {
                string deleteSql = "DELETE FROM operation_records WHERE Id = ANY(@Ids)";
                int count = await connection.ExecuteAsync(deleteSql, new { Ids = idList }, transaction);

                transaction.Commit();

                await InsertLogAsync(new SystemLog
                {
                    OperationTime = DateTime.Now,
                    MachineName = Environment.MachineName,
                    OperationType = "批量刪除",
                    AffectedData = $"Batch Delete ({count} records)",
                    DetailDescription = $"Deleted IDs count: {count}"
                });
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<IEnumerable<SystemLog>> GetLogsAsync()
        {
            using var connection = CreateConnection();
            string sql = "SELECT * FROM system_logs ORDER BY OperationTime DESC";
            return await connection.QueryAsync<SystemLog>(sql);
        }

        private async Task InsertLogAsync(SystemLog log)
        {
            using var connection = CreateConnection();
            string sql = @"INSERT INTO system_logs (OperationTime, MachineName, OperationType, AffectedData, DetailDescription) 
                           VALUES (@OperationTime, @MachineName, @OperationType, @AffectedData, @DetailDescription)";
            await connection.ExecuteAsync(sql, log);
        }

        public void ExportToExcel(IEnumerable<OperationRecord> data, string filePath)
        {
            ExcelPackage.License.SetNonCommercialPersonal("DemoUser");

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Operations");

            worksheet.Cells[1, 1].Value = "Time";
            worksheet.Cells[1, 2].Value = "PanelID";
            worksheet.Cells[1, 3].Value = "LOTID";
            worksheet.Cells[1, 4].Value = "CarrierID";

            int row = 2;
            foreach (var item in data)
            {
                worksheet.Cells[row, 1].Value = item.Time.ToString("yyyy/MM/dd HH:mm:ss");
                worksheet.Cells[row, 2].Value = item.PanelID;
                worksheet.Cells[row, 3].Value = item.LOTID;
                worksheet.Cells[row, 4].Value = item.CarrierID;
                row++;
            }

            worksheet.Cells.AutoFitColumns();
            File.WriteAllBytes(filePath, package.GetAsByteArray());

            Task.Run(async () => await InsertLogAsync(new SystemLog
            {
                OperationTime = DateTime.Now,
                MachineName = Environment.MachineName,
                OperationType = "匯出",
                AffectedData = "N/A",
                DetailDescription = $"Exported to {Path.GetFileName(filePath)}"
            }));
        }

        public async Task<string> ImportFromExcelAsync(string filePath)
        {
            ExcelPackage.License.SetNonCommercialPersonal("DemoUser");

            using var package = new ExcelPackage(new FileInfo(filePath));
            var worksheet = package.Workbook.Worksheets[0]; 
            int rowCount = worksheet.Dimension.Rows;

            using var connection = CreateConnection();
            string fetchSql = "SELECT Time, PanelID, LOTID, CarrierID FROM operation_records";
            var existingData = await connection.QueryAsync<OperationRecord>(fetchSql);
            
            var existingSet = new HashSet<string>(existingData.Select(r => 
                $"{r.Time:yyyyMMddHHmmss}|{r.PanelID}|{r.LOTID}|{r.CarrierID}"));

            var newRecords = new List<OperationRecord>();
            
            int added = 0;
            int updated = 0;
            int deleted = 0;
            int duplicates = 0;
            int skipped = 0;

            for (int row = 2; row <= rowCount; row++)
            {
                var timeStr = worksheet.Cells[row, 1].Text;
                bool isTimeValid = DateTime.TryParse(timeStr, out DateTime time);
                bool isPanelIdValid = long.TryParse(worksheet.Cells[row, 2].Text, out long panelId);
                bool isLotIdValid = long.TryParse(worksheet.Cells[row, 3].Text, out long lotId);
                bool isCarrierIdValid = long.TryParse(worksheet.Cells[row, 4].Text, out long carrierId);

                if (isTimeValid && isPanelIdValid && isLotIdValid && isCarrierIdValid)
                {
                    string key = $"{time:yyyyMMddHHmmss}|{panelId}|{lotId}|{carrierId}";

                    if (existingSet.Contains(key))
                    {
                        duplicates++;
                        skipped++;
                    }
                    else
                    {
                        added++;
                        existingSet.Add(key); 
                        newRecords.Add(new OperationRecord
                        {
                            Time = time,
                            PanelID = panelId,
                            LOTID = lotId,
                            CarrierID = carrierId
                        });
                    }
                }
                else
                {
                    skipped++;
                }
            }

            if (newRecords.Any())
            {
                string insertSql = @"INSERT INTO operation_records (Time, PanelID, LOTID, CarrierID) 
                                     VALUES (@Time, @PanelID, @LOTID, @CarrierID)";
                await connection.ExecuteAsync(insertSql, newRecords);
            }

            string resultSummary = $"新增={added}, 更新={updated}, 刪除={deleted}, 重複={duplicates}, 略過={skipped}";

            await InsertLogAsync(new SystemLog
            {
                OperationTime = DateTime.Now,
                MachineName = Environment.MachineName,
                OperationType = "匯入",
                AffectedData = "N/A",
                DetailDescription = $"Imported from {Path.GetFileName(filePath)}. Result: {resultSummary}"
            });

            return resultSummary;
        }

        public async Task InitializeDatabaseAsync()
        {
            using var connection = CreateConnection();
            
            string createSql = @"
                CREATE TABLE IF NOT EXISTS operation_records (
                    Id SERIAL PRIMARY KEY,
                    Time TIMESTAMP,
                    PanelID BIGINT,
                    LOTID BIGINT,
                    CarrierID BIGINT
                );
                
                CREATE TABLE IF NOT EXISTS system_logs (
                    Id SERIAL PRIMARY KEY,
                    OperationTime TIMESTAMP,
                    MachineName TEXT,
                    OperationType TEXT,
                    AffectedData TEXT,
                    DetailDescription TEXT
                );";
            await connection.ExecuteAsync(createSql);

            try 
            {
                var checkSql = "SELECT data_type FROM information_schema.columns WHERE table_name = 'operation_records' AND column_name = 'panelid'";
                var dataType = await connection.QueryFirstOrDefaultAsync<string>(checkSql);

                if (dataType != null && (dataType.Equals("text", StringComparison.OrdinalIgnoreCase) || dataType.Contains("character", StringComparison.OrdinalIgnoreCase)))
                {
                    string alterSql = @"
                        ALTER TABLE operation_records 
                        ALTER COLUMN PanelID TYPE BIGINT USING PanelID::bigint,
                        ALTER COLUMN LOTID TYPE BIGINT USING LOTID::bigint,
                        ALTER COLUMN CarrierID TYPE BIGINT USING CarrierID::bigint;";
                    await connection.ExecuteAsync(alterSql);
                }
            }
            catch 
            {
            }
        }
    }
}
