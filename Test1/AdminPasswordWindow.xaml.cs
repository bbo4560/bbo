using System.Windows;
using System.Windows.Input;

namespace Test1
{
    public partial class AdminPasswordWindow : Window
    {
        public AdminPasswordWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => PasswordBox.Focus();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (PasswordBox.Password == AuthConfig.AdminPassword)
            {
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("密碼錯誤，請重新輸入。", "驗證失敗", MessageBoxButton.OK, MessageBoxImage.Warning);
                PasswordBox.Clear();
                PasswordBox.Focus();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Confirm_Click(sender, e);
            }
        }
    }
}

