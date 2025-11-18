using System;
using System.Windows;
using OfficeOpenXml;

namespace Test1
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            ExcelPackage.License.SetNonCommercialPersonal("Test1 Application");
            base.OnStartup(e);
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            try
            {
                var login = new LoginWindow();
                bool? loginResult = login.ShowDialog();
                if (loginResult == true)
                {
                    var main = new MainWindow(login.UserRole);
                    this.MainWindow = main;
                    main.Show();
                }
                else
                {
                    Shutdown();
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("應用程式啟動失敗：" + ex.Message + "\n\n詳細資訊：" + ex,
                    "嚴重錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            MessageBox.Show("發生未處理的異常：" + e.Exception.Message + "\n\n詳細資訊：" + e.Exception,
                "未處理的異常", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                MessageBox.Show("發生嚴重錯誤：" + ex.Message + "\n\n詳細資訊：" + ex,
                    "嚴重錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}


