using Microsoft.Win32;
using OfficeOpenXml;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace TEST2
{
    public partial class MainWindow : Window
    {
        private const string ConnectionString = "Host=localhost;Username=postgres;Password=1234;Database=testdb";
        private readonly DatabaseService _dbService;
        private ObservableCollection<OperationRecord> _records = new ObservableCollection<OperationRecord>();

        public MainWindow()
        {
            InitializeComponent();
            _dbService = new DatabaseService(ConnectionString);
            dataGrid.ItemsSource = _records;
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                DatabaseService.EnsureDatabaseExists(ConnectionString);
                await _dbService.InitializeDatabaseAsync();
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Initialization Error (Check Connection String): {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            UpdateLastOpTime();
        }

        private void UpdateLastOpTime()
        {
            txtLastOpTime.Text = $"最後操作時間: {DateTime.Now:yyyy/MM/dd HH:mm:ss}";
        }

        private async Task LoadDataAsync()
        {
            try
            {
                var data = await _dbService.GetAllAsync();
                _records.Clear();
                foreach (var item in data)
                {
                    _records.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnShowAll_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
            UpdateLastOpTime();
        }

        private async void BtnAdd_Click(object sender, RoutedEventArgs e)
      
        {
            var addWindow = new AddRecordWindow();
            addWindow.Owner = this;
            
            if (addWindow.ShowDialog() == true)
            {
                var newRecord = addWindow.NewRecord;
                try
                {
                    await _dbService.InsertAsync(newRecord);
                    await LoadDataAsync();
                    UpdateLastOpTime();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error adding record: {ex.Message}");
                }
            }
        }

        private async void BtnQuery_Click(object sender, RoutedEventArgs e)
        {
            var queryWindow = new QueryWindow();
            queryWindow.Owner = this;

            if (queryWindow.ShowDialog() == true)
            {
                try
                {
                    var data = await _dbService.GetFilteredAsync(
                        queryWindow.QueryPanelID,
                        queryWindow.QueryLOTID,
                        queryWindow.QueryCarrierID,
                        queryWindow.QueryDate,
                        queryWindow.QueryTime
                    );
                    
                    _records.Clear();
                    foreach (var item in data)
                    {
                        _records.Add(item);
                    }
                    UpdateLastOpTime();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"查詢失敗: {ex.Message}", "Error");
                }
            }
        }

        private async void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Excel Files|*.xlsx;*.xls"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var result = await _dbService.ImportFromExcelAsync(openFileDialog.FileName);
                    await LoadDataAsync();
                    MessageBox.Show($"匯入成功!\n\n{result}", "Success");
                    UpdateLastOpTime();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"匯入失敗: {ex.Message}", "Error");
                }
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel Files|*.xlsx",
                FileName = $"Export_{DateTime.Now:yyyyMMddHHmmss}.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    _dbService.ExportToExcel(_records, saveFileDialog.FileName);
                    MessageBox.Show("匯出成功!", "Success");
                    UpdateLastOpTime();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"匯出失敗: {ex.Message}", "Error");
                }
            }
        }

        private void BtnLog_Click(object sender, RoutedEventArgs e)
        {
            var logWindow = new LogWindow(_dbService);
            logWindow.Owner = this;
            logWindow.ShowDialog();
        }

        private async void BtnBatchDelete_Click(object sender, RoutedEventArgs e)
        {
            var selectedRecords = dataGrid.SelectedItems.Cast<OperationRecord>().ToList();

            if (selectedRecords.Count == 0)
            {
                MessageBox.Show("請先選取要刪除的資料", "提示");
                return;
            }

            if (MessageBox.Show($"確定要刪除選取的 {selectedRecords.Count} 筆資料嗎?", "確認", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    var ids = selectedRecords.Select(r => r.Id);
                    await _dbService.DeleteBatchAsync(ids);
                    await LoadDataAsync();
                    UpdateLastOpTime();
                    MessageBox.Show("批量刪除成功!", "Success");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"批量刪除失敗: {ex.Message}", "Error");
                }
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is OperationRecord record)
            {
                if (MessageBox.Show($"確定要刪除 PanelID: {record.PanelID} 嗎?", "確認", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    try
                    {
                        await _dbService.DeleteAsync(record.Id);
                        await LoadDataAsync();
                        UpdateLastOpTime();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"刪除失敗: {ex.Message}", "Error");
                    }
                }
            }
        }

        private async void BtnModify_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is OperationRecord record)
            {
                var modifyWindow = new ModifyRecordWindow(record);
                modifyWindow.Owner = this;

                if (modifyWindow.ShowDialog() == true)
                {
                    var updatedRecord = modifyWindow.ModifiedRecord;
                    try
                    {
                        await _dbService.UpdateAsync(updatedRecord);
                        await LoadDataAsync();
                        UpdateLastOpTime();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"修改失敗: {ex.Message}", "Error");
                    }
                }
            }
        }
    }
}
