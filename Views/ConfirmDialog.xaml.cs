using System.Windows;

namespace VaultApp.Views
{
    public partial class ConfirmDialog : ChromeWindow
    {
        public ConfirmDialog(string title, string body, string confirmLabel, bool isDanger = false)
        {
            InitializeComponent();
            TitleText.Text  = title;
            BodyText.Text   = body;
            ConfirmBtn.Content = confirmLabel;
            if (isDanger)
                ConfirmBtn.Style = (Style)Application.Current.Resources["DangerButton"];
            else
                ConfirmBtn.Style = (Style)Application.Current.Resources["PrimaryButton"];
        }

        private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
        private void Cancel_Click(object sender, RoutedEventArgs e)  => DialogResult = false;
    }
}
