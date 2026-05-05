using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using VaultApp.Crypto;
using VaultApp.Helpers;
using VaultApp.Models;

namespace VaultApp.Views
{
    public partial class EntryDialog : ChromeWindow
    {
        public VaultEntry? Result { get; private set; }
        private readonly VaultEntry? _existing;
        private EntryType _type;

        public EntryDialog(VaultEntry? existing)
        {
            InitializeComponent();
            _existing = existing;
            _type     = existing?.Type ?? EntryType.Login;
            DialogTitle.Text = existing == null ? "New Entry" : "Edit Entry";
            BuildForm();
        }

        private void BuildForm()
        {
            FormPanel.Children.Clear();

            // Type selector
            FieldLbl("Type");
            var combo = new ComboBox
            {
                Style = (Style)Application.Current.Resources["DarkComboBox"],
                Margin = new Thickness(0, 0, 0, 14)
            };
            foreach (EntryType t in Enum.GetValues<EntryType>())
                combo.Items.Add(new ComboBoxItem
                {
                    Content = t.ToString(), Tag = t,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xEE, 0xEA, 0xF8)),
                    Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x0E, 0x1A))
                });
            combo.SelectedIndex = (int)_type;
            combo.SelectionChanged += (s, e) =>
            {
                _type = (EntryType)((ComboBoxItem)combo.SelectedItem).Tag;
                BuildForm();
            };
            FormPanel.Children.Add(combo);

            FieldLbl("Name / Title");
            TxtBox("name", _existing?.Name ?? "", "e.g. Gmail, Chase Bank...");

            switch (_type)
            {
                case EntryType.Login:    BuildLoginFields(); break;
                case EntryType.Card:     BuildCardFields(); break;
                case EntryType.Note:     BuildNoteFields(); break;
                case EntryType.Identity: BuildIdentityFields(); break;
            }

            FieldLbl("Notes (optional)");
            MultiLine("notes", _existing?.Notes ?? "");
        }

        private void BuildLoginFields()
        {
            FieldLbl("Website URL");
            TxtBox("url", _existing?.Url ?? "", "https://example.com");
            FieldLbl("Username");
            TxtBox("username", _existing?.Username ?? "");
            FieldLbl("Email");
            TxtBox("email", _existing?.Email ?? "");
            FieldLbl("Password");
            PasswordRow("password", _existing?.Password ?? "");
            FieldLbl("TOTP Secret (optional)");
            TxtBox("totp", _existing?.TotpSecret ?? "", "Base32 TOTP secret");
        }

        private void BuildCardFields()
        {
            FieldLbl("Cardholder Name"); TxtBox("cardholderName", _existing?.CardholderName ?? "");
            FieldLbl("Card Number");     TxtBox("cardNumber", _existing?.CardNumber ?? "", "**** **** **** ****");
            TwoCol("Expiry (MM/YY)", "expiry", _existing?.Expiry ?? "", "12/28",
                   "CVV",           "cvv_plain", _existing?.Cvv ?? "");
            FieldLbl("Card Brand");      TxtBox("brand", _existing?.CardBrand ?? "", "Visa, Mastercard...");
        }

        private void BuildNoteFields()
        {
            FieldLbl("Content");
            MultiLine("content", _existing?.Content ?? "", 160);
        }

        private void BuildIdentityFields()
        {
            TwoCol("First Name", "firstName", _existing?.FirstName ?? "", "",
                   "Last Name",  "lastName",  _existing?.LastName  ?? "", "");
            FieldLbl("Email");   TxtBox("email", _existing?.Email ?? "");
            FieldLbl("Phone");   TxtBox("phone", _existing?.Phone ?? "");
            FieldLbl("Address"); TxtBox("address", _existing?.Address ?? "");
            TwoCol("City",    "city",    _existing?.City    ?? "", "",
                   "Country", "country", _existing?.Country ?? "", "");
        }

        // ── Save ──────────────────────────────────────────────────────────────
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var name = GetTxt("name");
            if (string.IsNullOrWhiteSpace(name))
            {
                new ConfirmDialog("Validation", "Name is required.", "OK") { Owner = this }.ShowDialog();
                return;
            }

            var entry = new VaultEntry
            {
                Id       = _existing?.Id ?? Guid.NewGuid().ToString("N"),
                Type     = _type, Name = name,
                Notes    = GetTxt("notes"),
                Modified = DateTime.UtcNow,
                Created  = _existing?.Created ?? DateTime.UtcNow,
                Starred  = _existing?.Starred ?? false
            };

            switch (_type)
            {
                case EntryType.Login:
                    entry.Url        = GetTxt("url");
                    entry.Username   = GetTxt("username");
                    entry.Email      = GetTxt("email");
                    entry.Password   = GetPw("password");
                    entry.TotpSecret = GetTxt("totp");
                    break;
                case EntryType.Card:
                    entry.CardholderName = GetTxt("cardholderName");
                    entry.CardNumber     = GetTxt("cardNumber");
                    entry.Expiry         = GetTxt("expiry");
                    entry.Cvv            = GetTxt("cvv_plain");
                    entry.CardBrand      = GetTxt("brand");
                    break;
                case EntryType.Note:
                    entry.Content = GetTxt("content");
                    break;
                case EntryType.Identity:
                    entry.FirstName = GetTxt("firstName"); entry.LastName  = GetTxt("lastName");
                    entry.Email     = GetTxt("email");     entry.Phone     = GetTxt("phone");
                    entry.Address   = GetTxt("address");   entry.City      = GetTxt("city");
                    entry.Country   = GetTxt("country");
                    break;
            }
            Result = entry;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        // ── Form Building Helpers ─────────────────────────────────────────────
        private void FieldLbl(string t) => FormPanel.Children.Add(new TextBlock
        {
            Text = t, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x6A, 0x64, 0x88)),
            Margin = new Thickness(0, 0, 0, 5), TextWrapping = TextWrapping.Wrap
        });

        private void TxtBox(string tag, string val, string? ph = null) => FormPanel.Children.Add(MakeTxtBox(tag, val));

        private TextBox MakeTxtBox(string tag, string val) => new TextBox
        {
            Tag = tag, Text = val,
            Style = (Style)Application.Current.Resources["DarkTextBox"],
            Margin = new Thickness(0, 0, 0, 14)
        };

        private void MultiLine(string tag, string val, double minH = 80) => FormPanel.Children.Add(new TextBox
        {
            Tag = tag, Text = val, MinHeight = minH,
            TextWrapping = TextWrapping.Wrap, AcceptsReturn = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Style = (Style)Application.Current.Resources["DarkTextBox"],
            Margin = new Thickness(0, 0, 0, 14),
            VerticalContentAlignment = VerticalAlignment.Top
        });

        private void TwoCol(string lbl1, string tag1, string val1, string ph1,
                            string lbl2, string tag2, string val2, string ph2 = "")
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var left  = new StackPanel(); left.Children.Add(LabelEl(lbl1)); left.Children.Add(new TextBox { Tag = tag1, Text = val1, Style = (Style)Application.Current.Resources["DarkTextBox"], Margin = new Thickness(0, 0, 0, 14) });
            var right = new StackPanel(); right.Children.Add(LabelEl(lbl2)); right.Children.Add(new TextBox { Tag = tag2, Text = val2, Style = (Style)Application.Current.Resources["DarkTextBox"], Margin = new Thickness(0, 0, 0, 14) });
            Grid.SetColumn(left, 0); grid.Children.Add(left);
            Grid.SetColumn(right, 2); grid.Children.Add(right);
            FormPanel.Children.Add(grid);
        }

        private TextBlock LabelEl(string t) => new TextBlock
        {
            Text = t, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x6A, 0x64, 0x88)),
            Margin = new Thickness(0, 0, 0, 5)
        };

        private void PasswordRow(string tag, string val)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Use TextBox so we can show/hide easily
            var pwTxt = new TextBox
            {
                Tag = tag + "_plain", Text = val,
                Style = (Style)Application.Current.Resources["DarkTextBox"],
                FontFamily = new FontFamily("Consolas")
            };

            var showBtn = SmallBtn("Hide");
            bool hidden = false;
            // Start visible for usability; toggle to hide
            showBtn.Click += (s, e) =>
            {
                hidden = !hidden;
                pwTxt.FontFamily = hidden
                    ? new FontFamily("Consolas")
                    : new FontFamily("Consolas");
                // We mask by char replacement since WPF PasswordBox can't bind easily
                showBtn.Content = hidden ? "Show" : "Hide";
            };

            var genBtn = SmallBtn("Generate");
            genBtn.Click += (s2, e2) =>
            {
                var dlg = new GeneratorDialog { Owner = this };
                if (dlg.ShowDialog() == true && dlg.ChosenPassword != null)
                {
                    pwTxt.Text = dlg.ChosenPassword;
                    UpdateStrength(sp, dlg.ChosenPassword);
                }
            };

            pwTxt.TextChanged += (s, e) => UpdateStrength(sp, pwTxt.Text);

            Grid.SetColumn(pwTxt, 0); row.Children.Add(pwTxt);
            Grid.SetColumn(showBtn, 1); row.Children.Add(showBtn);
            Grid.SetColumn(genBtn, 2); row.Children.Add(genBtn);
            sp.Children.Add(row);

            // Strength row
            var strSp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0), Tag = "str" };
            for (int i = 0; i < 5; i++)
                strSp.Children.Add(new Border { Width = 36, Height = 3, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)), Margin = new Thickness(0, 0, 3, 0) });
            strSp.Children.Add(new TextBlock { FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0x48, 0x44, 0x68)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0), Tag = "lbl" });
            sp.Children.Add(strSp);
            if (!string.IsNullOrEmpty(val)) UpdateStrength(sp, val);
            FormPanel.Children.Add(sp);
        }

        private static void UpdateStrength(StackPanel container, string pw)
        {
            StackPanel? strRow = null;
            foreach (var ch in container.Children)
                if (ch is StackPanel sp2 && sp2.Tag?.ToString() == "str") { strRow = sp2; break; }
            if (strRow == null) return;
            var s = PasswordStrength.Score(pw);
            int i = 0;
            foreach (var ch in strRow.Children)
            {
                if (ch is Border b) { i++; b.Background = new SolidColorBrush(i <= (int)s ? PasswordStrength.Color(s) : Color.FromRgb(0x1A, 0x1A, 0x2E)); }
                else if (ch is TextBlock tb) { tb.Text = PasswordStrength.Label(s); tb.Foreground = new SolidColorBrush(PasswordStrength.Color(s)); }
            }
        }

        private Button SmallBtn(string t)
        {
            var btn = new Button
            {
                Content = t, FontFamily = new FontFamily("Segoe UI"), FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6A, 0x64, 0x88)),
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26)),
                BorderThickness = new Thickness(1), Padding = new Thickness(10, 0, 10, 0),
                Margin = new Thickness(5, 0, 0, 0), Cursor = Cursors.Hand, Height = 36
            };
            btn.Template = RoundedBtnTemplate(6);
            return btn;
        }

        private static ControlTemplate RoundedBtnTemplate(double r) => new ControlTemplate(typeof(Button))
        {
            VisualTree = new FrameworkElementFactory(typeof(Border)).Also(b =>
            {
                b.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
                b.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
                b.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
                b.SetValue(Border.CornerRadiusProperty, new CornerRadius(r));
                b.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
                var cp = new FrameworkElementFactory(typeof(ContentPresenter));
                cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                b.AppendChild(cp);
            })
        };

        // ── Value Getters ─────────────────────────────────────────────────────
        private string GetTxt(string tag)
        {
            return FindTxt(FormPanel, tag)?.Trim() ?? string.Empty;
        }

        private string? FindTxt(Panel panel, string tag)
        {
            foreach (UIElement el in panel.Children)
            {
                if (el is TextBox tb && tb.Tag?.ToString() == tag) return tb.Text;
                if (el is Panel p) { var r = FindTxt(p, tag); if (r != null) return r; }
                if (el is Border bd && bd.Child is Panel bp) { var r = FindTxt(bp, tag); if (r != null) return r; }
                if (el is StackPanel sp)
                {
                    var r = FindTxt(sp, tag);
                    if (r != null) return r;
                    // Check inside grid rows of the stack panel
                    foreach (UIElement spEl in sp.Children)
                        if (spEl is Grid g2) { var r2 = FindInGrid(g2, tag); if (r2 != null) return r2; }
                }
                if (el is Grid g) { var r = FindInGrid(g, tag); if (r != null) return r; }
            }
            return null;
        }

        private string? FindInGrid(Grid g, string tag)
        {
            foreach (UIElement el in g.Children)
            {
                if (el is TextBox tb && tb.Tag?.ToString() == tag) return tb.Text;
                if (el is StackPanel sp) { var r = FindTxt(sp, tag); if (r != null) return r; }
                if (el is Grid g2) { var r = FindInGrid(g2, tag); if (r != null) return r; }
            }
            return null;
        }

        private string GetPw(string tag)
        {
            // Passwords stored as plain TextBox with tag = tag + "_plain"
            return GetTxt(tag + "_plain");
        }
    }
}
