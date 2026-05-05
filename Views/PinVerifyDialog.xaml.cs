using System.Windows;
using System.Windows.Input;

namespace VaultApp.Views
{
    public partial class PinVerifyDialog : ChromeWindow
    {
        public string EnteredPin { get; private set; } = string.Empty;

        public PinVerifyDialog()
        {
            InitializeComponent();
            Loaded += (s, e) => PinBox.Focus();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (PinBox.Password.Length == 0) { ErrText.Text = "Please enter your PIN."; return; }
            EnteredPin   = PinBox.Password;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)  => DialogResult = false;
        private void PinBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) Confirm_Click(sender, e);
        }
    }
}
