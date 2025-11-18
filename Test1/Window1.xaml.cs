using System.Windows;
using System.Windows.Input;

namespace Test1
{
    public partial class Window1 : Window
    {
        public PanelLog? NewPanelLog { get; private set; }

        public Window1()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                DatePicker.SelectedDate = DateTime.Now.Date;
                PanelIDTextBox.Focus();
                TimeTextBox.Text = DateTime.Now.ToString("HH:mm:ss");
            };
        }


        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (!DatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("請選擇日期", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!TimeSpan.TryParse(TimeTextBox.Text, out var timePart))
            {
                MessageBox.Show("時間格式錯誤，請輸入 HH:mm:ss 格式", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var fullDateTime = DatePicker.SelectedDate.Value.Date + timePart;

            int panelId, lotId, carrierId;
            try
            {
                panelId = int.Parse(PanelIDTextBox.Text);
                lotId = int.Parse(LotIDTextBox.Text);
                carrierId = int.Parse(CarrierIDTextBox.Text);
            }
            catch (FormatException)
            {
                MessageBox.Show("請確保 PanelID、LOTID、CarrierID 欄位為整數格式", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var vm = this.Owner?.DataContext as PanelLogViewModel;
            if (vm?.Repo != null && vm.Repo.LogExists(panelId, lotId, carrierId))
            {
                MessageBox.Show("已存在該資料，無法新增", "重複資料", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

           
            NewPanelLog = new PanelLog
            {
                Panel_ID = panelId,
                LOT_ID = lotId,
                Carrier_ID = carrierId,
                Time = fullDateTime
            };

            DialogResult = true;
            Close();
        }


        private void CarrierIDTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Add_Click(sender, e);
        }
    }
}

