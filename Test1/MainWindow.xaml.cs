using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Dapper;
using Npgsql;
using Microsoft.Win32;
using System.IO;
using OfficeOpenXml;

namespace Test1
{
    public partial class MainWindow : Window
    {
        private DateTime lastUpdateTime = DateTime.MinValue;
        private AppConfig appConfig;
        private string? userRole;

        public MainWindow(string? role = null)
        {
            userRole = role;
            appConfig = AppConfig.Load();
            if (appConfig.LastUpdateTime.HasValue)
            {
                lastUpdateTime = appConfig.LastUpdateTime.Value;
            }

            var tempVm = new PanelLogViewModel(appConfig, createEmpty: true);
            try
            {
                InitializeComponent();
                DataContext = tempVm;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"視窗初始化失敗：{ex.Message}\n\n詳細資訊：{ex}\n\n應用程式將關閉。", "嚴重錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.Loaded -= MainWindow_Loaded;
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var vm = new PanelLogViewModel(appConfig);
                    bool dbConnected = vm.Repo != null;
                    Dispatcher.Invoke(() =>
                    {
                        DataContext = vm;
                        vm.UserRole = userRole;
                        vm.DataUpdated += (s, e) => SaveAndUpdateTime();
                        ApplyUserPermissions();
                        if (!dbConnected)
                        {
                            MessageBox.Show($"資料庫連接失敗：無法連接到資料庫。\n\n應用程式將以離線模式運行。\n請確認 PostgreSQL 服務是否正在運行。", "資料庫錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                        UpdateTimeFromDb();
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"資料庫連接失敗：{ex.Message}\n\n應用程式將以離線模式運行。\n請確認 PostgreSQL 服務是否正在運行。", "資料庫錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                        UpdateTimeFromDb();
                    });
                }
            });
        }

        public void UpdateTimeFromDb()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(UpdateTimeFromDb);
                return;
            }
            
            var repo = (DataContext as PanelLogViewModel)?.Repo;
            DateTime? lastDbTime = repo?.GetLastUpdateTimeFromDb();
            txtUpdateTime.Text = lastDbTime.HasValue
                ? "最後更新時間: " + lastDbTime.Value.ToString("yyyy/MM/dd HH:mm:ss")
                : "尚未有資料更新";
        }


        private void SaveAndUpdateTime()
        {
            lastUpdateTime = DateTime.Now;
            appConfig.UpdateLastUpdateTime(lastUpdateTime);
            UpdateTimeFromDb();
        }

        private void ApplyUserPermissions()
        {
            bool isAdmin = userRole == "Admin";
            bool isUser = userRole == "User";

            btnAdd.IsEnabled = isAdmin;
            btnImport.IsEnabled = isAdmin;
            LogsDataGrid.IsReadOnly = isUser;
            if (DataContext is PanelLogViewModel vm)
            {
                vm.UserRole = userRole;
            }
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void OpenSearchWindow(object sender, RoutedEventArgs e)
        {
            var dialog = new Window3 { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                var found = dialog.ResultLog;
                var vm = (PanelLogViewModel)DataContext;
                vm.SelectedLog = found;
            }
        }

        private void OpenAddWindow(object sender, RoutedEventArgs e)
        {
            if (userRole != "Admin")
            {
                MessageBox.Show("您沒有權限執行此操作。", "權限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var dialog = new Window1 { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                var newItem = dialog.NewPanelLog;
                if (newItem != null)
                {
                    var vm = DataContext as PanelLogViewModel;
                    vm?.AddPanelLogFromOther(newItem);
                    vm?.Reload();
                    appConfig.UpdateLastUpdateTime(DateTime.Now);  // 每次新增都更新
                    UpdateTimeFromDb();
                }
            }
        }

        private void ShowOperationLogWindow(object sender, RoutedEventArgs e)
        {
            try
            {
                var logWindow = new OperationLogWindow { Owner = this };
                logWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"無法開啟操作紀錄視窗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (userRole != "Admin")
            {
                MessageBox.Show("您沒有權限執行此操作。", "權限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var ofd = new OpenFileDialog
            {
                Filter = "Excel 檔案 (*.xlsx)|*.xlsx|所有檔案 (*.*)|*.*",
                Title = "選擇要匯入的 Excel 檔案"
            };
            if (ofd.ShowDialog() == true)
            {
                try
                {
                    var vm = DataContext as PanelLogViewModel;
                    if (vm?.Repo == null)
                    {
                        MessageBox.Show("無法取得資料模型或資料庫未連接。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    var fi = new FileInfo(ofd.FileName);
                    using (var package = new ExcelPackage(fi))
                    {
                        var ws = package.Workbook.Worksheets[0];
                        int rowCount = ws.Dimension.Rows;
                        int successCount = 0, deleteCount = 0, duplicateCount = 0, skipCount = 0, updateCount = 0;

                        for (int row = 2; row <= rowCount; row++)
                        {
                            try
                            {
                                string dateStr = ws.Cells[row, 1].Text.Trim();
                                string panelIdStr = ws.Cells[row, 2].Text.Trim();
                                string lotIdStr = ws.Cells[row, 3].Text.Trim();
                                string carrierIdStr = ws.Cells[row, 4].Text.Trim();
                                string delFlag = ws.Cells[row, 5].Text.Trim().ToUpper();

                                if (string.IsNullOrWhiteSpace(panelIdStr) || string.IsNullOrWhiteSpace(lotIdStr) || string.IsNullOrWhiteSpace(carrierIdStr)) { skipCount++; continue; }
                                if (!int.TryParse(panelIdStr, out int panelId) || !int.TryParse(lotIdStr, out int lotId) || !int.TryParse(carrierIdStr, out int carrierId)) { skipCount++; continue; }
                                if (!DateTime.TryParse(dateStr, out DateTime logTime)) { skipCount++; continue; }

                                if (vm.Repo.LogExists(panelId, lotId, carrierId))
                                {
                                    var log = vm.Repo.GetLogByKeys(panelId, lotId, carrierId);
                                    if (log != null && log.Id > 0)
                                    {
                                        if (log.Time != logTime)
                                        {
                                            log.Time = logTime;
                                            vm.Repo.UpdatePanelLog(log);
                                            updateCount++;
                                        }
                                        else
                                            duplicateCount++;
                                    }
                                    else
                                        skipCount++;
                                    continue;
                                }

                                vm.Repo.AddPanelLog(logTime, panelId, lotId, carrierId);
                                successCount++;
                            }
                            catch (Exception ex)
                            {
                                skipCount++;
                                MessageBox.Show($"第{row}行匯入異常：{ex.Message}", "匯入錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }

                        vm.Reload();
                        appConfig.UpdateLastUpdateTime(DateTime.Now); // 匯入/新增後更新
                        UpdateTimeFromDb();
                        OperationLogger.Log("匯入", $"{fi.FullName}", $"新增={successCount}, 更新={updateCount}, 刪除={deleteCount}, 重複={duplicateCount}, 略過={skipCount}");

                        string message = "匯入完成";
                        MessageBox.Show(message, "匯入結果", MessageBoxButton.OK,
                            successCount > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"匯入失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                Filter = "Excel 檔案 (*.xlsx)|*.xlsx|所有檔案 (*.*)|*.*",
                FileName = "PanelLogs.xlsx",
                Title = "儲存 Excel 檔案"
            };
            if (sfd.ShowDialog() != true)
                return;

            var vm = DataContext as PanelLogViewModel;
            if (vm == null)
            {
                MessageBox.Show("資料模型初始化失敗。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            vm.Reload(); // 保證匯出前資料是最新

            if (vm.Logs == null || vm.Logs.Count == 0)
            {
                MessageBox.Show("沒有資料可匯出！", "提醒", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var fi = new FileInfo(sfd.FileName);
                using (var package = new ExcelPackage(fi))
                {
                    var wsName = "PanelLogs";
                    var sheets = package.Workbook.Worksheets;
                    if (sheets[wsName] != null)
                        sheets.Delete(wsName);
                    var ws = sheets.Add(wsName);

                    ws.Cells[1, 1].Value = "Time";
                    ws.Cells[1, 2].Value = "Panel_ID";
                    ws.Cells[1, 3].Value = "LOT_ID";
                    ws.Cells[1, 4].Value = "Carrier_ID";

                    for (int i = 0; i < vm.Logs.Count; i++)
                    {
                        ws.Cells[i + 2, 1].Value = vm.Logs[i].Time;
                        ws.Cells[i + 2, 1].Style.Numberformat.Format = "yyyy/MM/dd HH:mm:ss";
                        ws.Cells[i + 2, 2].Value = vm.Logs[i].Panel_ID;
                        ws.Cells[i + 2, 3].Value = vm.Logs[i].LOT_ID;
                        ws.Cells[i + 2, 4].Value = vm.Logs[i].Carrier_ID;
                    }

                    // 匯出最後更新時間（採用 appConfig 設定）
                    if (appConfig.LastUpdateTime.HasValue)
                    {
                        ws.Cells[vm.Logs.Count + 3, 1].Value = "最後更新時間:";
                        ws.Cells[vm.Logs.Count + 3, 2].Value = appConfig.LastUpdateTime.Value.ToString("yyyy/MM/dd HH:mm:ss");
                    }

                    ws.Cells.AutoFitColumns();
                    package.Save();
                }

                MessageBox.Show("匯出成功");
                OperationLogger.Log("匯出", $"{sfd.FileName}",
                    $"匯出筆數={vm.Logs.Count}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"匯出失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class PanelLog : INotifyPropertyChanged
    {
        public int Id { get; set; }
        private DateTime time = DateTime.Now;
        public DateTime Time
        {
            get => time;
            set { time = value; NotifyChanged(nameof(Time)); NotifyChanged(nameof(TimeFormatted)); }
        }
        public string TimeFormatted
        {
            get => time.ToString("yyyy/MM/dd HH:mm:ss");
            set { if (DateTime.TryParse(value, out var dt)) Time = dt; }
        }
        private int panelId;
        public int Panel_ID
        {
            get => panelId;
            set { panelId = value; NotifyChanged(nameof(Panel_ID)); }
        }
        private int lotId;
        public int LOT_ID
        {
            get => lotId;
            set { lotId = value; NotifyChanged(nameof(LOT_ID)); }
        }
        private int carrierId;
        public int Carrier_ID
        {
            get => carrierId;
            set { carrierId = value; NotifyChanged(nameof(Carrier_ID)); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void NotifyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class PanelLogRepository
    {
        private readonly string serverConnStr = "Host=localhost;Username=postgres;Password=1234;Database=postgres";
        private readonly string dbName = "panellogdb";
        private string DbConnStr => $"Host=localhost;Username=postgres;Password=1234;Database={dbName}";

        public PanelLogRepository()
        {
            EnsureDatabaseExists();
            EnsureTableExists();
            EnsureCarrierIdColumn();
        }

        private void EnsureDatabaseExists()
        {
            using var conn = new NpgsqlConnection(serverConnStr);
            conn.Open();
            using var cmd = new NpgsqlCommand($"SELECT 1 FROM pg_database WHERE datname = '{dbName}'", conn);
            if (cmd.ExecuteScalar() == null)
            {
                using var createDbCmd = new NpgsqlCommand($"CREATE DATABASE \"{dbName}\"", conn);
                createDbCmd.ExecuteNonQuery();
            }
        }

        private void EnsureTableExists()
        {
            using var conn = new NpgsqlConnection(DbConnStr);
            conn.Open();
            string sql = @"CREATE TABLE IF NOT EXISTS panel_logs (
                           id SERIAL PRIMARY KEY,
                           time TIMESTAMP NOT NULL,
                           panel_id INTEGER NOT NULL,
                           lot_id INTEGER NOT NULL
                        );";
            conn.Execute(sql);
        }

        private void EnsureCarrierIdColumn()
        {
            using var conn = new NpgsqlConnection(DbConnStr);
            conn.Open();
            var cmd = new NpgsqlCommand("SELECT 1 FROM information_schema.columns WHERE table_name='panel_logs' AND column_name='carrier_id'", conn);
            if (cmd.ExecuteScalar() == null)
                conn.Execute("ALTER TABLE panel_logs ADD COLUMN carrier_id INTEGER NOT NULL DEFAULT 0;");
        }

        public bool LogExists(int panelId, int lotId, int carrierId)
        {
            using var conn = new NpgsqlConnection(DbConnStr);
            conn.Open();
            var count = conn.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM panel_logs WHERE panel_id=@Panel_ID AND lot_id=@LOT_ID AND carrier_id=@Carrier_ID",
                new { Panel_ID = panelId, LOT_ID = lotId, Carrier_ID = carrierId });
            return count > 0;
        }

        public PanelLog? GetLogByKeys(int panelId, int lotId, int carrierId)
        {
            using var conn = new NpgsqlConnection(DbConnStr);
            conn.Open();
            return conn.QueryFirstOrDefault<PanelLog>(
                "SELECT id, time, panel_id AS Panel_ID, lot_id AS LOT_ID, carrier_id AS Carrier_ID FROM panel_logs WHERE panel_id=@Panel_ID AND lot_id=@LOT_ID AND carrier_id=@Carrier_ID",
                new { Panel_ID = panelId, LOT_ID = lotId, Carrier_ID = carrierId });
        }

        public void AddPanelLog(DateTime time, int panelId, int lotId, int carrierId)
        {
            using var conn = new NpgsqlConnection(DbConnStr);
            conn.Open();
            conn.Execute("INSERT INTO panel_logs (time, panel_id, lot_id, carrier_id) VALUES (@Time, @Panel_ID, @LOT_ID, @Carrier_ID)",
                new { Time = time, Panel_ID = panelId, LOT_ID = lotId, Carrier_ID = carrierId });
        }

        public void UpdatePanelLog(PanelLog log)
        {
            using var conn = new NpgsqlConnection(DbConnStr);
            conn.Open();
            conn.Execute("UPDATE panel_logs SET time=@Time, panel_id=@Panel_ID, lot_id=@LOT_ID, carrier_id=@Carrier_ID WHERE id=@Id",
                new { log.Time, log.Panel_ID, log.LOT_ID, log.Carrier_ID, log.Id });
        }

        public void RemovePanelLog(int id)
        {
            using var conn = new NpgsqlConnection(DbConnStr);
            conn.Open();
            conn.Execute("DELETE FROM panel_logs WHERE id=@Id", new { Id = id });
        }

        public ObservableCollection<PanelLog> GetAllLogs()
        {
            using var conn = new NpgsqlConnection(DbConnStr);
            conn.Open();
            var raw = conn.Query<PanelLog>("SELECT id, time AS Time, panel_id AS Panel_ID, lot_id AS LOT_ID, carrier_id AS Carrier_ID FROM panel_logs");
            var sorted = raw.OrderBy(x => x.Panel_ID).ThenBy(x => x.LOT_ID).ThenBy(x => x.Carrier_ID).ToList();
            return new ObservableCollection<PanelLog>(sorted);
        }

        public DateTime? GetLastUpdateTimeFromDb()
        {
            using var conn = new NpgsqlConnection(DbConnStr);
            conn.Open();
            return conn.ExecuteScalar<DateTime?>("SELECT MAX(time) FROM panel_logs");
        }
    }

    public class PanelLogViewModel : INotifyPropertyChanged
    {
        private PanelLogRepository? repo;
        public PanelLogRepository? Repo => repo;

        private AppConfig appConfig;
        public ObservableCollection<PanelLog> Logs { get; private set; }
        private PanelLog? _selectedLog;
        public PanelLog? SelectedLog
        {
            get => _selectedLog;
            set { _selectedLog = value; OnPropertyChanged(nameof(SelectedLog)); }
        }

        private string? _userRole;
        public string? UserRole
        {
            get => _userRole;
            set { _userRole = value; OnPropertyChanged(nameof(UserRole)); OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanDelete)); }
        }

        public bool CanEdit => _userRole == "Admin";
        public bool CanDelete => _userRole == "Admin";

        public ICommand AddLogCommand { get; }
        public ICommand DeleteLogCommand { get; }
        public ICommand UpdateLogCommand { get; }
        public ICommand DeleteMultipleLogsCommand { get; }

        public event EventHandler? DataUpdated;

        public void NotifyDataUpdated()
        {
            DataUpdated?.Invoke(this, EventArgs.Empty);
        }

        private string newPanelId = "";
        public string NewPanelID { get => newPanelId; set { newPanelId = value; OnPropertyChanged(nameof(NewPanelID)); } }
        private string newLotId = "";
        public string NewLotID { get => newLotId; set { newLotId = value; OnPropertyChanged(nameof(NewLotID)); } }
        private string newCarrierId = "";
        public string NewCarrierID { get => newCarrierId; set { newCarrierId = value; OnPropertyChanged(nameof(NewCarrierID)); } }

        public PanelLogViewModel(AppConfig? config = null, bool createEmpty = false)
        {
            Logs = new ObservableCollection<PanelLog>();
            appConfig = config ?? new AppConfig();

            if (!createEmpty)
            {
                try
                {
                    repo = new PanelLogRepository();
                    Logs = repo.GetAllLogs();
                }
                catch
                {
                    Logs = new ObservableCollection<PanelLog>();
                    repo = null;
                }
            }

            AddLogCommand = new RelayCommand(_ =>
            {
                if (_userRole != "Admin") { MessageBox.Show("您沒有權限執行此操作。", "權限不足", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                if (repo == null) { MessageBox.Show("資料庫未連接，無法新增資料。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error); return; }
                if (int.TryParse(NewPanelID, out var pid) && int.TryParse(NewLotID, out var lid) && int.TryParse(NewCarrierID, out var cid))
                {
                    try
                    {
                        var now = DateTime.Now;
                        repo.AddPanelLog(now, pid, lid, cid);
                        Reload();
                        appConfig.UpdateLastUpdateTime(DateTime.Now);
                        OperationLogger.Log("新增", $"PanelID={pid}",
                            $"Time={now:yyyy/MM/dd HH:mm:ss}, PanelID={pid},LotID={lid},CarrierID={cid}");
                        NewPanelID = ""; NewLotID = ""; NewCarrierID = "";
                        NotifyDataUpdated();
                    }
                    catch (Exception ex) { MessageBox.Show($"新增資料失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error); }
                }
            });

            DeleteMultipleLogsCommand = new RelayCommand(param =>
            {
                if (_userRole != "Admin") { MessageBox.Show("您沒有權限執行此操作。", "權限不足", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                if (repo == null)
                {
                    MessageBox.Show("資料庫未連接，無法刪除資料。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (param is System.Collections.IList items && items.Count > 0)
                {
                    var pwdWindow = new AdminPasswordWindow { Owner = Application.Current.MainWindow };
                    if (pwdWindow.ShowDialog() != true) return;

                    if (MessageBox.Show($"確定要刪除這 {items.Count} 筆資料嗎？", "刪除確認", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        try
                        {
                            var panelIds = items.Cast<PanelLog>().Select(x => x.Panel_ID).ToList();

                            var details = string.Join(Environment.NewLine,
                                items.Cast<PanelLog>().Select(x =>
                                    $"Time={x.Time:yyyy/MM/dd HH:mm:ss}  |  PanelID={x.Panel_ID}  |  LotID={(x.LOT_ID > 0 ? x.LOT_ID.ToString() : "?")}  |  CarrierID={(x.Carrier_ID > 0 ? x.Carrier_ID.ToString() : "?")}"
                                ));

                            foreach (var item in items.Cast<PanelLog>())
                            {
                                repo.RemovePanelLog(item.Id);
                            }
                            Reload();
                            appConfig.UpdateLastUpdateTime(DateTime.Now);
                            OperationLogger.Log("批次刪除", $"PanelID={string.Join(", ", panelIds)}", details);
                            NotifyDataUpdated();
                            MessageBox.Show("刪除成功", "訊息", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"刪除資料失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            });

            DeleteLogCommand = new RelayCommand(param =>
            {
                if (_userRole != "Admin") { MessageBox.Show("您沒有權限執行此操作。", "權限不足", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                if (repo == null)
                {
                    MessageBox.Show("資料庫未連接，無法刪除資料。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (param is PanelLog log)
                {
                    var pwdWindow = new AdminPasswordWindow { Owner = Application.Current.MainWindow };
                    if (pwdWindow.ShowDialog() != true) return;

                    if (MessageBox.Show("確定要刪除嗎？", "刪除確認", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        try
                        {
                            var originalTime = log.Time.ToString("yyyy/MM/dd HH:mm:ss");
                            var panelId = log.Panel_ID;
                            var lotId = log.LOT_ID;
                            var carrierId = log.Carrier_ID;
                            var detail = $"Time={originalTime}\nPanelID={panelId}\nLotID={lotId}\nCarrierID={carrierId}";

                            repo.RemovePanelLog(log.Id);
                            Reload();
                            appConfig.UpdateLastUpdateTime(DateTime.Now);
                            OperationLogger.Log("刪除", $"PanelID={panelId}", detail);
                            NotifyDataUpdated();
                            MessageBox.Show("刪除成功");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"刪除資料失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            });

            UpdateLogCommand = new RelayCommand(param =>
            {
                if (_userRole != "Admin") { MessageBox.Show("您沒有權限執行此操作。", "權限不足", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                if (repo == null) { MessageBox.Show("資料庫未連接，無法修改資料。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error); return; }
                if (param is PanelLog log)
                {
                    var originalSnapshot = new PanelLog
                    {
                        Id = log.Id,
                        Panel_ID = log.Panel_ID,
                        LOT_ID = log.LOT_ID,
                        Carrier_ID = log.Carrier_ID,
                        Time = log.Time
                    };

                    var wnd = new Window2(log) { Owner = Application.Current.MainWindow };
                    if (wnd.ShowDialog() == true)
                    {
                        var edited = wnd.EditedPanelLog;
                        if (edited != null)
                        {
                            try
                            {
                                repo.UpdatePanelLog(edited);
                                Reload();
                                appConfig.UpdateLastUpdateTime(DateTime.Now);
                                var detail = OperationLogger.DescribeChanges(originalSnapshot, edited);
                                OperationLogger.Log("修改", $"PanelID={edited.Panel_ID}", detail);
                                NotifyDataUpdated();
                            }
                            catch (Exception ex) { MessageBox.Show($"修改資料失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error); }
                        }
                    }
                }
            });
        }

        public void AddPanelLogFromOther(PanelLog item)
        {
            if (_userRole != "Admin") { MessageBox.Show("您沒有權限執行此操作。", "權限不足", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (repo == null) { MessageBox.Show("資料庫未連接，無法新增資料。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            try
            {
                repo.AddPanelLog(item.Time, item.Panel_ID, item.LOT_ID, item.Carrier_ID);
                Reload();
                appConfig.UpdateLastUpdateTime(DateTime.Now);
                OperationLogger.Log("新增", $"PanelID={item.Panel_ID}",
                            $"Time={item.Time:yyyy/MM/dd HH:mm:ss} \nPanelID={item.Panel_ID} \nLotID={item.LOT_ID} \nCarrierID={item.Carrier_ID}");
                NotifyDataUpdated();
            }
            catch (Exception ex) { MessageBox.Show($"新增資料失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        public void Reload()
        {
            if (repo == null) return;
            try
            {
                Logs = repo.GetAllLogs();
                OnPropertyChanged(nameof(Logs));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"重新載入資料失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            (Application.Current.MainWindow as MainWindow)?.UpdateTimeFromDb();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> execute;
        private readonly Func<object?, bool>? canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => canExecute == null || canExecute(parameter);
        public void Execute(object? parameter) => execute(parameter);
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
