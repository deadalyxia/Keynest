using System;
using System.Windows;
using System.Windows.Threading;

namespace VaultApp.Views
{
    public partial class SplashWindow : ChromeWindow
    {
        private readonly DispatcherTimer _timer = new();

        public SplashWindow()
        {
            InitializeComponent();
            // After animation completes (~1.6s) open the lock screen
            _timer.Interval = TimeSpan.FromMilliseconds(1700);
            _timer.Tick += (s, e) =>
            {
                _timer.Stop();
                var lock_ = new LockWindow();
                Application.Current.MainWindow = lock_;
                lock_.Show();
                Close();
            };
            Loaded += (s, e) => _timer.Start();
        }
    }
}
