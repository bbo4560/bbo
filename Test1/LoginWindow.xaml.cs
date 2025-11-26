using System;
using System.Windows;

namespace Test1
{
    public partial class LoginWindow : Window
    {
        private string computerName;

        public LoginWindow()
        {
            InitializeComponent();
            computerName = Environment.MachineName;
            Loaded += LoginWindow_Loaded;
        }

        private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ComputerNameTextBlock.Text = $"電腦名稱: {computerName}";
            PasswordBox.Focus();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("請輸入密碼", "錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (password == AuthConfig.AdminPassword)
            {
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("密碼錯誤", "錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                PasswordBox.Clear();
                PasswordBox.Focus();
            }
        }
    }
}




