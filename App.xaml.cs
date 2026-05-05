using System.Windows;
using VaultApp.Views;

namespace VaultApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += (s, ex) =>
            {
                MessageBox.Show($"Unexpected error:\n\n{ex.Exception.Message}",
                    "Vault Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ex.Handled = true;
            };
            var splash = new SplashWindow();
            MainWindow = splash;
            splash.Show();
        }
    }
}
