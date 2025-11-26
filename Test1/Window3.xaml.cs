using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace Test1
{
	public partial class Window3 : Window, System.ComponentModel.INotifyPropertyChanged
	{
		public ObservableCollection<PanelLog> SearchResults { get; set; } = new ObservableCollection<PanelLog>();
		public PanelLog? SelectedResult { get; set; }
		public PanelLog? ResultLog { get; private set; }
        public ICommand DeleteCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteMultipleCommand { get; }

        public bool CanEdit => true;
        public bool CanDelete => true;

        public Window3()
		{
            InitializeComponent();
            DataContext = this;
            DeleteCommand = new RelayCommand(DeletePanelLog);
            EditCommand = new RelayCommand(EditPanelLog);
            DeleteMultipleCommand = new RelayCommand(DeleteMultipleLogs); 
            Loaded += Window3_Loaded;
        }

        private void Window3_Loaded(object sender, RoutedEventArgs e)
        {
            PanelIDTextBox.Focus();
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
        private void DeleteMultipleLogs(object? parameter)
        {
            if (parameter is System.Collections.IList selected && selected.Count > 0)
            {
                var pwdWindow = new AdminPasswordWindow
                {
                    Owner = Owner ?? Application.Current.MainWindow
                };
                if (pwdWindow.ShowDialog() != true)
                    return;

                if (MessageBox.Show($"確定要刪除這 {selected.Count} 筆資料嗎？", "批次刪除確認", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;

                var repo = new PanelLogRepository();
                var toRemove = selected.Cast<PanelLog>().ToList();
                PanelLogViewModel? vm = null;
                if (Owner is MainWindow mw && mw.DataContext is PanelLogViewModel viewModel)
                {
                    vm = viewModel;
                }

                
                var details = string.Join(
                    Environment.NewLine,
                    toRemove.Select(x =>
                        $"Time={x.Time:yyyy/MM/dd HH:mm:ss}  |  PanelID={x.Panel_ID}  |  LotID={x.LOT_ID}  |  CarrierID={x.Carrier_ID}"
                    )
                );

                foreach (var log in toRemove)
                {
                    repo.RemovePanelLog(log.Id);
                    SearchResults.Remove(log);
                    if (vm != null)
                    {
                        var target = vm.Logs.FirstOrDefault(x => x.Id == log.Id);
                        if (target != null) vm.Logs.Remove(target);
                    }
                }

                if (vm != null)
                {
                    vm.NotifyDataUpdated();
                }

                OperationLogger.Log(
                    "批次刪除(查詢視窗)",
                    $"PanelID={string.Join(", ", toRemove.Select(x => x.Panel_ID))}",
                    details
                );

                MessageBox.Show("刪除完成");
            }
            else
            {
                MessageBox.Show("請先選取要刪除的資料！", "提示");
            }
        }


        private void Search_Click(object sender, RoutedEventArgs e)
        {
            int? panelId = null, lotId = null, carrierId = null;
            DateTime? searchTime = null;

            if (!string.IsNullOrWhiteSpace(TimeTextBox.Text))
            {
                DateTime dt;
                if (DateTime.TryParse(TimeTextBox.Text, out dt)) 
                    searchTime = dt;
            }
            if (!string.IsNullOrWhiteSpace(PanelIDTextBox.Text))
                panelId = int.TryParse(PanelIDTextBox.Text, out var v1) ? v1 : (int?)null;
            if (!string.IsNullOrWhiteSpace(LotIDTextBox.Text))
                lotId = int.TryParse(LotIDTextBox.Text, out var v2) ? v2 : (int?)null;
            if (!string.IsNullOrWhiteSpace(CarrierIDTextBox.Text))
                carrierId = int.TryParse(CarrierIDTextBox.Text, out var v3) ? v3 : (int?)null;

            var repo = new PanelLogRepository();
            var candidates = repo.GetAllLogs().Where(item =>
                (string.IsNullOrWhiteSpace(TimeTextBox.Text) || item.Time.ToString("yyyy/MM/dd HH:mm:ss").Contains(TimeTextBox.Text)) &&
                (!panelId.HasValue || item.Panel_ID == panelId) &&
                (!lotId.HasValue || item.LOT_ID == lotId) &&
                (!carrierId.HasValue || item.Carrier_ID == carrierId)
                ).ToList();


            SearchResults.Clear();
            foreach (var log in candidates)
                SearchResults.Add(log);

            if (candidates.Count == 0)
                MessageBox.Show("沒有找到資料", "資料查詢結果");

            
        }

        private void DeletePanelLog(object? parameter)
        {
            var log = parameter as PanelLog;
            if (log == null) return;

            var pwdWindow = new AdminPasswordWindow
            {
                Owner = Owner ?? Application.Current.MainWindow
            };
            if (pwdWindow.ShowDialog() != true)
                return;

            var confirm = MessageBox.Show("確定要刪除嗎？", "刪除確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm == MessageBoxResult.Yes)
            {

                var repo = new PanelLogRepository();
                repo.RemovePanelLog(log.Id);

                var panelId = log.Panel_ID;
                var lotId = log.LOT_ID;
                var carrierId = log.Carrier_ID;
                var timeString = log.Time.ToString("yyyy/MM/dd HH:mm:ss");
                var detail = $"Time={timeString}\nPanelID={panelId}\nLotID={lotId}\nCarrierID={carrierId}";

                if (Owner is MainWindow mw && mw.DataContext is PanelLogViewModel vm)
                {
                    var target = vm.Logs.FirstOrDefault(x => x.Id == log.Id);
                    if (target != null) vm.Logs.Remove(target);
                    vm.NotifyDataUpdated();
                }

                SearchResults.Remove(log);
                OperationLogger.Log("刪除(查詢視窗)", $"PanelID={panelId}", detail);
                MessageBox.Show("刪除成功");
            }
        }


        private void EditPanelLog(object? parameter)
		{
			var log = parameter as PanelLog;
			if (log == null) return;
			var wnd = new Window2(log);
			var originalSnapshot = new PanelLog
			{
				Id = log.Id,
				Panel_ID = log.Panel_ID,
				LOT_ID = log.LOT_ID,
				Carrier_ID = log.Carrier_ID,
				Time = log.Time
			};
			wnd.Owner = this;
			if (wnd.ShowDialog() == true && wnd.EditedPanelLog != null)
			{
				var edited = wnd.EditedPanelLog;
				var repo = new PanelLogRepository();
				repo.UpdatePanelLog(edited);

				if (Owner is MainWindow mw && mw.DataContext is PanelLogViewModel vm)
				{
					var target = vm.Logs.FirstOrDefault(x => x.Id == edited.Id);
					if (target != null)
					{
						target.Panel_ID = edited.Panel_ID;
						target.LOT_ID = edited.LOT_ID;
						target.Carrier_ID = edited.Carrier_ID;
					}
					vm.NotifyDataUpdated();
				}

				var local = SearchResults.FirstOrDefault(x => x.Id == edited.Id);
				if (local != null)
				{
					local.Panel_ID = edited.Panel_ID;
					local.LOT_ID = edited.LOT_ID;
					local.Carrier_ID = edited.Carrier_ID;
				}

				var detail = OperationLogger.DescribeChanges(originalSnapshot, edited);
				OperationLogger.Log("修改(查詢視窗)", $"PanelID={edited.Panel_ID}", detail);
			}
		}
	}
}






