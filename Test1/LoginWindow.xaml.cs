using System.Windows;

namespace Test1
{
    public partial class LoginWindow : Window
    {
        public string? UserRole { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => UsernameTextBox.Focus();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameTextBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(username))
            {
                MessageBox.Show("請輸入帳號和密碼", "錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (AuthConfig.Users.ContainsKey(username) && AuthConfig.Users[username].Password == password)
            {
                UserRole = AuthConfig.Users[username].Role;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("帳號或密碼錯誤", "錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                PasswordBox.Clear();
                PasswordBox.Focus();
            }
        }
    }
}




