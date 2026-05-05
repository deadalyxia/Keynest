using System.Windows;
using System.Windows.Input;

namespace VaultApp.Views
{
    /// <summary>
    /// Base class for all windows. Provides custom chrome:
    /// borderless, draggable title bar, min/max/close buttons, resize grip.
    /// </summary>
    public class ChromeWindow : Window
    {
        public ChromeWindow()
        {
            WindowStyle          = WindowStyle.None;
            AllowsTransparency   = false;
            ResizeMode           = ResizeMode.CanResizeWithGrip;
            Background           = System.Windows.Media.Brushes.Transparent;

            // Enable native snap/aero by keeping AllowsTransparency=false
        }

        /// <summary>Call from title bar MouseLeftButtonDown.</summary>
        protected void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
                return;
            }
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        protected void MinimizeButton_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        protected void MaximizeButton_Click(object sender, RoutedEventArgs e)
            => ToggleMaximize();

        protected void CloseButton_Click(object sender, RoutedEventArgs e)
            => Close();

        private void ToggleMaximize()
            => WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
    }
}
