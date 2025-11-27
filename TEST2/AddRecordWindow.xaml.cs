using System;
using System.Windows;

namespace TEST2
{
    public partial class AddRecordWindow : Window
    {
        public OperationRecord NewRecord { get; private set; }

        public AddRecordWindow()
        {
            InitializeComponent();
            
            dpDate.SelectedDate = DateTime.Today;
            txtTime.Text = DateTime.Now.ToString("HH:mm:ss");
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtPanelID.Text) || 
                string.IsNullOrWhiteSpace(txtLOTID.Text) || 
                string.IsNullOrWhiteSpace(txtCarrierID.Text))
            {
                MessageBox.Show("請輸入所有欄位 (PanelID, LOTID, CarrierID)", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dpDate.SelectedDate == null)
            {
                MessageBox.Show("請選擇日期", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TimeSpan.TryParse(txtTime.Text, out TimeSpan timeSpan))
            {
                MessageBox.Show("時間格式錯誤 (請使用 HH:mm:ss)", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!long.TryParse(txtPanelID.Text, out long panelId) || 
                !long.TryParse(txtLOTID.Text, out long lotId) || 
                !long.TryParse(txtCarrierID.Text, out long carrierId))
            {
                MessageBox.Show("PanelID, LOTID, CarrierID 必須為數字", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime combinedDateTime = dpDate.SelectedDate.Value.Date + timeSpan;

            NewRecord = new OperationRecord
            {
                PanelID = panelId,
                LOTID = lotId,
                CarrierID = carrierId,
                Time = combinedDateTime
            };

            DialogResult = true;
            Close();
        }
    }
}
