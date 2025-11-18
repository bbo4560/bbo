using System.Windows;

namespace Test1
{
    public partial class Window2 : Window
    {
        public PanelLog? EditedPanelLog { get; private set; }
        private int _originId;

        public Window2(PanelLog original)
        {
            InitializeComponent();

            PanelIDTextBox.Text = original.Panel_ID.ToString();
            LotIDTextBox.Text = original.LOT_ID.ToString();
            CarrierIDTextBox.Text = original.Carrier_ID.ToString();

            DatePicker.SelectedDate = original.Time.Date;
            TimeTextBox.Text = original.Time.ToString("HH:mm:ss");

            _originId = original.Id;
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
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

            EditedPanelLog = new PanelLog
            {
                Id = _originId,
                Time = fullDateTime,
                Panel_ID = int.Parse(PanelIDTextBox.Text),
                LOT_ID = int.Parse(LotIDTextBox.Text),
                Carrier_ID = int.Parse(CarrierIDTextBox.Text)
            };

            DialogResult = true;
            Close();
        }
    }
}

