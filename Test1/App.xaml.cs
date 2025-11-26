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
                    var main = new MainWindow();
                    this.MainWindow = main;
                    main.Show();
                }
                else
                {
                    Shutdown();
                    return;
                }
            }
            catch
            {
                Shutdown();
                return;
            }
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
        }
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
        }
    }
}


