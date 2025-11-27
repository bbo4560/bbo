using System.Collections.ObjectModel;
using System.Windows;

namespace TEST2
{
    public partial class LogWindow : Window
    {
        private readonly DatabaseService _dbService;
        private ObservableCollection<SystemLog> _logs = new ObservableCollection<SystemLog>();

        public LogWindow(DatabaseService dbService)
        {
            InitializeComponent();
            _dbService = dbService;
            logGrid.ItemsSource = _logs;
            Loaded += LogWindow_Loaded;
        }

        private async void LogWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var logs = await _dbService.GetLogsAsync();
                _logs.Clear();
                foreach (var log in logs)
                {
                    _logs.Add(log);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"載入紀錄失敗: {ex.Message}", "錯誤");
            }
        }
    }
}

