using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VaultApp.Crypto;
using VaultApp.Helpers;

namespace VaultApp.Views
{
    public partial class GeneratorDialog : ChromeWindow
    {
        public string? ChosenPassword { get; private set; }

        public GeneratorDialog()
        {
            InitializeComponent();
            Generate();
        }

        private void Generate()
        {
            bool pp = PassphraseMode.IsChecked == true;
            PwOpts.Visibility = pp ? Visibility.Collapsed : Visibility.Visible;

            string pw = pp
                ? CryptoEngine.GeneratePassphrase()
                : CryptoEngine.GeneratePassword(
                    (int)LenSlider.Value,
                    UseUpper.IsChecked  == true,
                    UseLower.IsChecked  == true,
                    UseDigits.IsChecked == true,
                    UseSymbols.IsChecked== true,
                    NoAmbig.IsChecked   == true);

            OutputText.Text = pw;
            LenVal.Text     = ((int)LenSlider.Value).ToString();

            var s       = PasswordStrength.Score(pw);
            var entropy = CryptoEngine.EstimateEntropy(pw);
            var segs    = new[] { S1, S2, S3, S4, S5 };
            var onColor = PasswordStrength.Color(s);
            var offCol  = Color.FromRgb(0x1A, 0x1A, 0x2E);
            for (int i = 0; i < 5; i++)
                segs[i].Background = new SolidColorBrush(i < (int)s ? onColor : offCol);

            StrLabel.Text       = $"Strength:  {PasswordStrength.Label(s)}     |     {entropy.Bits} bits  —  crack time: {entropy.CrackTime}";
            StrLabel.Foreground = new SolidColorBrush(onColor);
        }

        private void Rebuild(object sender, RoutedEventArgs e)                              => Generate();
        private void Regenerate_Click(object sender, RoutedEventArgs e)                    => Generate();
        private void Slider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)=> Generate();

        private void CopyOutput_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(OutputText.Text))
                Clipboard.SetText(OutputText.Text);
        }

        private void Use_Click(object sender, RoutedEventArgs e)
        {
            ChosenPassword = OutputText.Text;
            DialogResult   = true;
        }
    }
}
