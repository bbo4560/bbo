using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace Test1
{
    public partial class OperationLogWindow : Window
    {
        public ObservableCollection<OperationLogger.OperationLogEntry> LogEntries { get; } = new();

        public OperationLogWindow()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += OperationLogWindow_Loaded;
        }

        private void OperationLogWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            LoadLog();
        }

        private void LoadLog()
        {
            try
            {
                LogEntries.Clear();
                
                OperationLogger.MigrateLocalFileToDatabase();
                
                int dbCount = OperationLogger.GetDatabaseRecordCount();
                string dbStatus = dbCount >= 0 ? $"資料庫: {dbCount} 筆" : "資料庫: 連接失敗";
                
                var entries = OperationLogger.ReadAll().OrderBy(entry => entry.Timestamp).ToList();
                
                System.Diagnostics.Debug.WriteLine($"讀取到 {entries.Count} 筆操作紀錄");
                
                foreach (var entry in entries)
                {
                    LogEntries.Add(entry);
                }

                StatusText.Text = $"紀錄筆數: {LogEntries.Count} 筆 ({dbStatus})";
                if (LogEntries.Count == 0)
                {
                    StatusText.Text += "（尚無資料）";
                }

                if (LogEntries.Count > 0)
                {
                    LogDataGrid.ScrollIntoView(LogEntries[^1]);
                }
            }
            catch (Exception ex)
            {
                LogEntries.Clear();
                StatusText.Text = "讀取失敗";
                System.Diagnostics.Debug.WriteLine($"讀取操作紀錄時發生錯誤: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"堆疊追蹤: {ex.StackTrace}");
                MessageBox.Show($"讀取操作紀錄時發生錯誤：{ex.Message}\n\n詳細資訊請查看 Debug 輸出。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
       
        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("確定要清除所有操作紀錄嗎？此操作無法復原。",
                "清除確認", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    OperationLogger.Clear();
                    OperationLogger.Log("查看紀錄", "OperationLogWindow", "清除操作紀錄");
                    LoadLog();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"清除操作紀錄時發生錯誤：{ex.Message}", "錯誤",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RefreshLog_Click(object sender, RoutedEventArgs e)
        {
            LoadLog();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
