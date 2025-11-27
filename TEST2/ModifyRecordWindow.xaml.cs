using System;
using System.Windows;

namespace TEST2
{
    public partial class ModifyRecordWindow : Window
    {
        public OperationRecord ModifiedRecord { get; private set; }
        private int _originalId;

        public ModifyRecordWindow(OperationRecord record)
        {
            InitializeComponent();
            
            if (record == null)
            {
                MessageBox.Show("無效的資料記錄", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            _originalId = record.Id;

            txtPanelID.Text = record.PanelID.ToString();
            txtLOTID.Text = record.LOTID.ToString();
            txtCarrierID.Text = record.CarrierID.ToString();
            
            dpDate.SelectedDate = record.Time.Date;
            txtTime.Text = record.Time.ToString("HH:mm:ss");
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

            ModifiedRecord = new OperationRecord
            {
                Id = _originalId, 
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
