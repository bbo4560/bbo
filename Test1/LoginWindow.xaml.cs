using System.Windows;

namespace Test1
{
    public partial class LoginWindow : Window
    {
        public string? UserRole { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
            UsernameTextBox.Text = Environment.MachineName;   
            UsernameTextBox.IsReadOnly = true;                
            Loaded += (_, _) => PasswordBox.Focus();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string inputUsername = UsernameTextBox.Text.Trim();          
            string password = PasswordBox.Password;
            string machineName = Environment.MachineName;

            
            if (inputUsername != machineName)
            {
                MessageBox.Show($"帳號必須為本機名稱 ({machineName})，請確認輸入。", "帳號錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                UsernameTextBox.Text = machineName;
                UsernameTextBox.Focus();
                return;
            }

            
            if (machineName == "MSI") 
            {
                if (password != AuthConfig.AdminPassword)
                {
                    MessageBox.Show("密碼錯誤", "錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                    PasswordBox.Clear();
                    PasswordBox.Focus();
                    return;
                }
            }
            else if (machineName == "SAAIBUCIM003")
            {
                if (password != AuthConfig.AdminPassword)
                {
                    MessageBox.Show("密碼錯誤", "錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                    PasswordBox.Clear();
                    PasswordBox.Focus();
                    return;
                }
            }

            UserRole = AuthConfig.GetRole(machineName);
            DialogResult = true;
            Close();
        }
    }
}