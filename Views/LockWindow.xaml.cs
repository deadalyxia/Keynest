using System;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using VaultApp.Crypto;
using VaultApp.Helpers;

namespace VaultApp.Views
{
    public partial class LockWindow : ChromeWindow
    {
        private string _pin1 = string.Empty;

        public LockWindow()
        {
            InitializeComponent();
            // Hook inactivity lock event
            Session.InactivityLocked += OnInactivityLock;
            Closed += (s, e) => Session.InactivityLocked -= OnInactivityLock;
            Render();
        }

        private void OnInactivityLock()
        {
            // Already on lock screen — nothing to do
        }

        private void Render()
        {
            if (!VaultStorage.IsInitialised) RenderSetup();
            else                             RenderUnlock();
        }

        // ── SETUP ─────────────────────────────────────────────────────────────
        private void RenderSetup()
        {
            Clear(); SubtitleText.Text = "INITIAL SETUP";
            Add(FieldLabel("Master Password  (minimum 4 characters)"));
            Add(PwBox("pin1"));
            Add(Gap(10));
            Add(FieldLabel("Confirm PIN"));
            Add(PwBox("pin2"));
            Add(Gap(8));
            Add(Hint("Use letters, numbers and symbols for maximum strength."));
            Add(Gap(20));
            var btn = PrimaryBtn("Create Vault");
            btn.Click += (s, e) =>
            {
                var v1 = GetPw("pin1"); var v2 = GetPw("pin2");
                if (v1.Length < 4) { SetStatus("PIN must be at least 4 characters."); return; }
                if (v1 != v2)      { SetStatus("PINs do not match."); return; }
                _pin1 = v1;
                RenderShowRecoveryKey();
            };
            Add(btn);
            FocusFirst("pin1");
        }

        // ── SHOW RECOVERY KEY (once, never stored) ────────────────────────────
        private void RenderShowRecoveryKey()
        {
            Clear(); SubtitleText.Text = "RECOVERY KEY";

            // Create vault — get back the recovery key
            byte[] recoveryKeyBytes;
            try   { recoveryKeyBytes = VaultStorage.Initialise(_pin1); }
            catch (Exception ex) { SetStatus($"Error creating vault: {ex.Message}"); RenderSetup(); return; }

            var encoded = CryptoEngine.EncodeRecoveryKey(recoveryKeyBytes);
            CryptoEngine.SecureClear(recoveryKeyBytes);

            Add(new TextBlock
            {
                Text = "Your recovery key has been generated. Write it down and store it somewhere safe. It will NEVER be shown again.",
                FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0x80, 0xD8)),
                TextWrapping = TextWrapping.Wrap, LineHeight = 18, Margin = new Thickness(0, 0, 0, 16),
                TextAlignment = TextAlignment.Center
            });

            var keyBox = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0A)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x7B, 0x5C, 0xF0)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(0, 0, 0, 16)
            };
            keyBox.Child = new TextBlock
            {
                Text = encoded, FontFamily = new FontFamily("Consolas"), FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(0x7B, 0x5C, 0xF0)),
                TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center
            };
            Add(keyBox);

            var copyBtn = SecBtn("Copy to Clipboard");
            copyBtn.Click += (s, e) =>
            {
                Clipboard.SetText(encoded);
                copyBtn.Content = "Copied";
            };
            Add(copyBtn);
            Add(Gap(12));

            var continueBtn = PrimaryBtn("I have saved my recovery key");
            continueBtn.Click += (s, e) =>
            {
                // Clear key from clipboard if it was copied
                try { if (Clipboard.ContainsText() && Clipboard.GetText() == encoded) Clipboard.Clear(); } catch { }

                var key = VaultStorage.Unlock(_pin1);
                Session.Open(key);
                OpenMain();
            };
            Add(continueBtn);
        }

        // ── UNLOCK ────────────────────────────────────────────────────────────
        private void RenderUnlock()
        {
            Clear(); SubtitleText.Text = "PASSWORD MANAGER";
            Add(FieldLabel("Master Password"));
            var pw = PwBox("unlockPin");
            Add(pw);
            pw.KeyDown += (s, e) => { if (e.Key == Key.Enter) DoUnlock(); };
            Add(Gap(20));
            var btn = PrimaryBtn("Unlock");
            btn.Click += (s, e) => DoUnlock();
            Add(btn);
            Add(Gap(16));

            // Divider
            var sep = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            sep.ColumnDefinitions.Add(new ColumnDefinition());
            sep.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            sep.ColumnDefinitions.Add(new ColumnDefinition());
            var l1 = new Rectangle { Height = 1, Fill = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x32)), VerticalAlignment = VerticalAlignment.Center };
            var lt = new TextBlock { Text = "or", Foreground = new SolidColorBrush(Color.FromRgb(0x3A, 0x34, 0x58)), FontSize = 11, Margin = new Thickness(12, 0, 12, 0) };
            var l2 = new Rectangle { Height = 1, Fill = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x32)), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(l1, 0); sep.Children.Add(l1);
            Grid.SetColumn(lt, 1); sep.Children.Add(lt);
            Grid.SetColumn(l2, 2); sep.Children.Add(l2);
            Add(sep);

            if (!VaultStorage.ResetUsed)
            {
                var reset = SecBtn("Use recovery key");
                reset.Click += (s, e) => RenderRecovery();
                Add(reset);
            }
            else
            {
                Add(new TextBlock
                {
                    Text = "Recovery reset has been used.",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x3A, 0x34, 0x58)),
                    FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center
                });
            }

            FocusFirst("unlockPin");
        }

        private void DoUnlock()
        {
            SetStatus("Unlocking...");
            try
            {
                var key = VaultStorage.Unlock(GetPw("unlockPin"));
                Session.Open(key);
                OpenMain();
            }
            catch (CryptographicException) { SetStatus("Incorrect PIN. Please try again."); }
            catch (Exception ex)           { SetStatus($"Error: {ex.Message}"); }
        }

        // ── RECOVERY RESET ────────────────────────────────────────────────────
        private void RenderRecovery()
        {
            Clear(); SubtitleText.Text = "RECOVERY RESET";

            Add(new TextBlock
            {
                Text = "Enter your recovery key to reset your PIN. All vault entries will be wiped — they cannot be recovered without the original PIN.",
                Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0x80, 0xD8)),
                FontSize = 12, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 18), TextAlignment = TextAlignment.Center, LineHeight = 18
            });

            Add(FieldLabel("Recovery Key"));
            Add(TxtBox("recoveryKey", "", "xxxx-xxxxxx-xxxxxx-..."));
            Add(Gap(10));
            Add(FieldLabel("New Master Password"));
            Add(PwBox("newPin"));
            Add(Gap(8));
            Add(FieldLabel("Confirm New PIN"));
            Add(PwBox("newPin2"));
            Add(Gap(16));

            var btn = new Button
            {
                Content = "Reset and Wipe Vault",
                Style   = (Style)Application.Current.Resources["DangerButton"],
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(0, 12, 0, 12), Margin = new Thickness(0, 0, 0, 8)
            };
            btn.Click += (s, e) =>
            {
                var rkText = GetTxt("recoveryKey").Trim();
                var np     = GetPw("newPin");
                var np2    = GetPw("newPin2");

                if (rkText.Length < 10)  { SetStatus("Enter your recovery key."); return; }
                if (np.Length < 4)       { SetStatus("New PIN must be at least 4 characters."); return; }
                if (np != np2)           { SetStatus("New PINs do not match."); return; }

                var c1 = new ConfirmDialog(
                    "Wipe Vault and Reset?",
                    "All saved entries will be permanently deleted. This cannot be undone.",
                    "Yes, wipe and reset", isDanger: true) { Owner = this };
                if (c1.ShowDialog() != true) return;

                try
                {
                    var rkBytes = CryptoEngine.DecodeRecoveryKey(rkText);
                    VaultStorage.PerformRecoveryReset(np, rkBytes);
                    CryptoEngine.SecureClear(rkBytes);
                    SetStatus("Vault reset. Unlock with your new PIN.");
                    RenderUnlock();
                }
                catch (Exception ex) { SetStatus($"Error: {ex.Message}"); }
            };
            Add(btn);
            var back = SecBtn("Back to Unlock");
            back.Click += (s, e) => RenderUnlock();
            Add(back);
        }

        private void OpenMain()
        {
            var main = new MainWindow();
            Application.Current.MainWindow = main;
            main.Show();
            Close();
        }

        // ── UI Helpers ────────────────────────────────────────────────────────
        private void Add(UIElement el) => ContentPanel.Children.Add(el);
        private void Clear()           { ContentPanel.Children.Clear(); SetStatus(""); }
        private void SetStatus(string msg) => StatusText.Text = msg;

        private TextBlock FieldLabel(string t) => new TextBlock
        {
            Text = t, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x80, 0xA8)),
            Margin = new Thickness(0, 0, 0, 6), TextWrapping = TextWrapping.Wrap
        };

        private TextBlock Hint(string t) => new TextBlock
        {
            Text = t, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x44, 0x68)),
            TextWrapping = TextWrapping.Wrap, LineHeight = 17, Margin = new Thickness(0, 0, 0, 0)
        };

        private PasswordBox PwBox(string tag) => new PasswordBox
        {
            Tag = tag, Style = (Style)Application.Current.Resources["DarkPasswordBox"]
        };

        private TextBox TxtBox(string tag, string val, string? ph = null) => new TextBox
        {
            Tag = tag, Text = val, Style = (Style)Application.Current.Resources["DarkTextBox"]
        };

        private Button PrimaryBtn(string t) => new Button
        {
            Content = t, Style = (Style)Application.Current.Resources["PrimaryButton"],
            HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(0, 12, 0, 12)
        };

        private Button SecBtn(string t) => new Button
        {
            Content = t, Style = (Style)Application.Current.Resources["SecondaryButton"],
            HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(0, 11, 0, 11)
        };

        private Rectangle Gap(double h) => new Rectangle { Height = h };

        private string GetPw(string tag)
        {
            foreach (UIElement el in ContentPanel.Children)
                if (el is PasswordBox pb && pb.Tag?.ToString() == tag) return pb.Password;
            return string.Empty;
        }

        private string GetTxt(string tag)
        {
            foreach (UIElement el in ContentPanel.Children)
                if (el is TextBox tb && tb.Tag?.ToString() == tag) return tb.Text;
            return string.Empty;
        }

        private void FocusFirst(string tag) =>
            Loaded += (s, e) =>
            {
                foreach (UIElement el in ContentPanel.Children)
                    if (el is PasswordBox pb && pb.Tag?.ToString() == tag) { pb.Focus(); break; }
            };
    }
}
