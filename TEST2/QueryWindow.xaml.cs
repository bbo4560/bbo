using System;
using System.Windows;

namespace TEST2
{
    public partial class QueryWindow : Window
    {
        public string? QueryPanelID { get; private set; }
        public string? QueryLOTID { get; private set; }
        public string? QueryCarrierID { get; private set; }

        public DateTime? QueryDate { get; private set; }
        public TimeSpan? QueryTime { get; private set; }

        public QueryWindow()
        {
            InitializeComponent();
        }

        private void BtnQuery_Click(object sender, RoutedEventArgs e)
        {           
            if (!string.IsNullOrWhiteSpace(txtPanelID.Text))
            {
                QueryPanelID = txtPanelID.Text.Trim();
            }
            if (!string.IsNullOrWhiteSpace(txtLOTID.Text))
            {
                QueryLOTID = txtLOTID.Text.Trim();
            }
            if (!string.IsNullOrWhiteSpace(txtCarrierID.Text))
            {
                QueryCarrierID = txtCarrierID.Text.Trim();
            }
            if (dpDate.SelectedDate.HasValue)
            {
                QueryDate = dpDate.SelectedDate.Value.Date;
            }
            if (!string.IsNullOrWhiteSpace(txtTime.Text))
            {
                if (TimeSpan.TryParse(txtTime.Text, out TimeSpan ts))
                {
                    QueryTime = ts;
                }
                else
                {
                    MessageBox.Show("時間格式錯誤 (請使用 HH:mm:ss)", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            DialogResult = true;
            Close();
        }
    }
}

