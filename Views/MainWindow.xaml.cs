using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using VaultApp.Helpers;
using VaultApp.Models;

namespace VaultApp.Views
{
    public partial class MainWindow : ChromeWindow
    {
        private string  _filter     = "all";
        private string  _search     = "";
        private string  _sort       = "name";
        private string? _selectedId = null;
        private VaultEntry? _selected;

        // Sidebar filter definitions — no emojis, geometric icons drawn in code
        private readonly (string Key, string Label)[] _filters =
        {
            ("all",      "All Items"),
            ("starred",  "Favourites"),
            ("login",    "Logins"),
            ("card",     "Cards"),
            ("note",     "Notes"),
            ("identity", "Identity"),
        };

        public MainWindow()
        {
            InitializeComponent();

            // Wire auto-lock: when inactivity fires, return to lock screen
            Session.InactivityLocked += () => Dispatcher.Invoke(GoToLockScreen);

            // Reset inactivity timer on any user activity
            PreviewMouseMove += (s, e) => Session.ResetActivity();
            PreviewKeyDown   += (s, e) => Session.ResetActivity();

            BuildSidebar();
            Refresh();
        }

        private void GoToLockScreen()
        {
            var lock_ = new LockWindow();
            Application.Current.MainWindow = lock_;
            lock_.Show();
            Close();
        }

        // ── Sidebar ───────────────────────────────────────────────────────────
        private void BuildSidebar()
        {
            Sidebar.Children.Clear();
            SidebarSection("Library");
            foreach (var (key, label) in _filters)
            {
                int count = key switch
                {
                    "all"     => Session.Entries.Count,
                    "starred" => Session.Entries.Count(e => e.Starred),
                    _         => Session.Entries.Count(e => e.Type.ToString().ToLower() == key)
                };
                SidebarItem(key, label, count, key == _filter);
            }
        }

        private void SidebarSection(string title)
        {
            Sidebar.Children.Add(new TextBlock
            {
                Text = title.ToUpper(), FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(0x40, 0x3C, 0x38)),
                Margin = new Thickness(8, 4, 0, 6), FontWeight = FontWeights.SemiBold
            });
        }

        private void SidebarItem(string key, string label, int count, bool active)
        {
            var accentColor = Color.FromRgb(0x7B, 0x5C, 0xF0);
            var border = new Border
            {
                Background   = active ? new SolidColorBrush(Color.FromArgb(20, 0x7B, 0x5C, 0xF0)) : Brushes.Transparent,
                CornerRadius = new CornerRadius(6),
                Padding      = new Thickness(8, 7, 8, 7),
                Margin       = new Thickness(0, 1, 0, 1),
                Cursor       = Cursors.Hand
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Indicator line left edge
            var indicator = new Border
            {
                Width = 2, Height = 14, CornerRadius = new CornerRadius(1),
                Background = active ? new SolidColorBrush(accentColor) : Brushes.Transparent,
                Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center
            };
            var labelTb = new TextBlock
            {
                Text = label, FontSize = 13,
                Foreground = active
                    ? new SolidColorBrush(Color.FromRgb(0xEE, 0xEA, 0xF8))
                    : new SolidColorBrush(Color.FromRgb(0x70, 0x6C, 0x68)),
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = active ? FontWeights.Medium : FontWeights.Normal
            };
            var badge = new Border
            {
                Background   = new SolidColorBrush(Color.FromArgb(active ? (byte)30 : (byte)0, 0xC8, 0xA9, 0x6E)),
                CornerRadius = new CornerRadius(4), Padding = new Thickness(5, 1, 5, 1),
                Child = new TextBlock
                {
                    Text       = count.ToString(),
                    FontSize   = 10, FontFamily = new FontFamily("Segoe UI"),
                    Foreground = new SolidColorBrush(active ? accentColor : Color.FromRgb(0x40, 0x3C, 0x38))
                }
            };

            Grid.SetColumn(indicator, 0); grid.Children.Add(indicator);
            Grid.SetColumn(labelTb, 1);   grid.Children.Add(labelTb);
            Grid.SetColumn(badge, 2);     grid.Children.Add(badge);
            border.Child = grid;

            border.MouseEnter        += (s, e) => { if (!active) border.Background = new SolidColorBrush(Color.FromArgb(12, 0xFF, 0xFF, 0xFF)); };
            border.MouseLeave        += (s, e) => { if (!active) border.Background = Brushes.Transparent; };
            border.MouseLeftButtonUp += (s, e) =>
            {
                _filter = key; _selectedId = null;
                CloseDetailPanel();
                BuildSidebar();
                RenderEntries();
                ViewTitle.Text = label;
            };
            Sidebar.Children.Add(border);
        }

        // ── Entry List ────────────────────────────────────────────────────────
        private void Refresh()
        {
            BuildSidebar();
            RenderEntries();
            CountLabel.Text = $"{Session.Entries.Count} item{(Session.Entries.Count != 1 ? "s" : "")}";
        }

        private IEnumerable<VaultEntry> FilteredEntries()
        {
            var q = _search.ToLowerInvariant();
            var entries = Session.Entries.AsEnumerable();
            entries = _filter switch
            {
                "starred"  => entries.Where(e => e.Starred),
                "login"    => entries.Where(e => e.Type == EntryType.Login),
                "card"     => entries.Where(e => e.Type == EntryType.Card),
                "note"     => entries.Where(e => e.Type == EntryType.Note),
                "identity" => entries.Where(e => e.Type == EntryType.Identity),
                _          => entries
            };
            if (!string.IsNullOrEmpty(q))
                entries = entries.Where(e =>
                    e.Name.ToLower().Contains(q) || e.Username.ToLower().Contains(q) ||
                    e.Email.ToLower().Contains(q) || e.Url.ToLower().Contains(q));
            return _sort == "date"
                ? entries.OrderByDescending(e => e.Modified)
                : entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase);
        }

        private void RenderEntries()
        {
            EntryList.Children.Clear();
            var list = FilteredEntries().ToList();
            ViewCount.Text = $"{list.Count} item{(list.Count != 1 ? "s" : "")}";
            if (!list.Any()) { EntryList.Children.Add(EmptyState()); return; }
            foreach (var e in list) EntryList.Children.Add(EntryCard(e));
        }

        private UIElement EntryCard(VaultEntry e)
        {
            bool isSel  = e.Id == _selectedId;
            var  color  = AvatarColor(e.Name);
            var  s      = PasswordStrength.Score(e.Password);

            var border = new Border
            {
                Background      = new SolidColorBrush(isSel ? Color.FromRgb(0x0E, 0x0E, 0x1A) : Color.FromRgb(0x08, 0x08, 0x10)),
                BorderBrush     = new SolidColorBrush(isSel ? Color.FromArgb(80, 0x7B, 0x5C, 0xF0) : Color.FromRgb(0x1A, 0x1A, 0x2E)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(8),
                Padding         = new Thickness(12, 10, 12, 10),
                Margin          = new Thickness(0, 0, 0, 4),
                Cursor          = Cursors.Hand
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Avatar
            var avatar = new Border
            {
                Width = 34, Height = 34, CornerRadius = new CornerRadius(7),
                Background      = new SolidColorBrush(Color.FromArgb(20, color.R, color.G, color.B)),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(60, color.R, color.G, color.B)),
                BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 12, 0),
                Child = new TextBlock
                {
                    Text = e.Name.Length > 0 ? e.Name[0].ToString().ToUpper() : "?",
                    FontSize = 14, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(color),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center
                }
            };

            // Info
            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock
            {
                Text = e.Name, FontSize = 13, FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(0xEE, 0xEA, 0xF8)),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            if (!string.IsNullOrEmpty(e.DisplayUser))
                info.Children.Add(new TextBlock
                {
                    Text = e.DisplayUser, FontSize = 11, FontFamily = new FontFamily("Segoe UI"),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x48, 0x44, 0x68)),
                    TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 2, 0, 0)
                });

            // Meta column
            var meta = new StackPanel
            {
                VerticalAlignment   = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10, 0, 0, 0)
            };
            meta.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)),
                CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2),
                HorizontalAlignment = HorizontalAlignment.Right,
                Child = new TextBlock
                {
                    Text = e.TypeLabel, FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x48, 0x44, 0x68))
                }
            });
            if (!string.IsNullOrEmpty(e.Password))
            {
                var bar = StrengthBar(s, 40);
                bar.Margin = new Thickness(0, 5, 0, 0);
                meta.Children.Add(bar);
            }
            if (e.Starred)
                meta.Children.Add(new TextBlock
                {
                    Text = "* fav", FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromArgb(160, 0x7B, 0x5C, 0xF0)),
                    HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 3, 0, 0)
                });

            // Copy button
            var copyBtn = MiniCopyBtn(e.Password, "copy pw", visible: !string.IsNullOrEmpty(e.Password));
            copyBtn.Margin = new Thickness(8, 0, 0, 0);
            copyBtn.VerticalAlignment = VerticalAlignment.Center;

            Grid.SetColumn(avatar, 0);   grid.Children.Add(avatar);
            Grid.SetColumn(info, 1);     grid.Children.Add(info);
            Grid.SetColumn(meta, 2);     grid.Children.Add(meta);
            Grid.SetColumn(copyBtn, 3);  grid.Children.Add(copyBtn);
            border.Child = grid;

            border.MouseEnter        += (s2, e2) => { if (e.Id != _selectedId) border.Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x0E, 0x1A)); };
            border.MouseLeave        += (s2, e2) => { if (e.Id != _selectedId) border.Background = new SolidColorBrush(Color.FromRgb(0x08, 0x08, 0x10)); };
            border.MouseLeftButtonUp += (s2, e2) => SelectEntry(e.Id);
            return border;
        }

        private UIElement EmptyState()
        {
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 70, 0, 0) };
            var rect = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(10), Background = new SolidColorBrush(Color.FromRgb(0x13, 0x13, 0x1F)), BorderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x32)), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 14) };
            var icon = new Path { Data = Geometry.Parse("M4,8 L4,5 Q4,1 10,1 Q16,1 16,5 L16,8 M2,8 L18,8 L18,18 L2,18 Z M10,12 L10,14"), Stroke = new SolidColorBrush(Color.FromRgb(0x3A, 0x34, 0x58)), StrokeThickness = 1.5, Width = 16, Height = 16, Stretch = Stretch.Uniform };
            rect.Child = icon;
            sp.Children.Add(rect);
            sp.Children.Add(new TextBlock { Text = "No entries", FontSize = 14, FontWeight = FontWeights.Medium, Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x44, 0x68)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 6) });
            sp.Children.Add(new TextBlock { Text = "Press + to add your first entry.", FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x40, 0x3C, 0x38)), HorizontalAlignment = HorizontalAlignment.Center });
            return sp;
        }

        // ── Detail Panel ──────────────────────────────────────────────────────
        private void SelectEntry(string id)
        {
            _selectedId = id;
            _selected   = Session.Entries.FirstOrDefault(e => e.Id == id);
            if (_selected == null) return;
            RenderEntries();
            OpenDetailPanel(_selected);
        }

        private void OpenDetailPanel(VaultEntry e)
        {
            DetailPanel.Visibility = Visibility.Visible;
            var col = AvatarColor(e.Name);
            DetailIcon.Text       = e.Name.Length > 0 ? e.Name[0].ToString().ToUpper() : "?";
            DetailIcon.Foreground = new SolidColorBrush(col);
            DetailAvatar.BorderBrush = new SolidColorBrush(Color.FromArgb(60, col.R, col.G, col.B));
            DetailName.Text = e.Name;
            DetailUrl.Text  = e.Url;
            DetailUrl.Visibility = string.IsNullOrEmpty(e.Url) ? Visibility.Collapsed : Visibility.Visible;
            StarBtn.Content = e.Starred ? "Unfavourite" : "Favourite";
            RenderDetailBody(e);
        }

        private void RenderDetailBody(VaultEntry e)
        {
            DetailBody.Children.Clear();
            switch (e.Type)
            {
                case EntryType.Login:
                    if (!string.IsNullOrEmpty(e.Username))   AddDetailField("Username", e.Username);
                    if (!string.IsNullOrEmpty(e.Email))      AddDetailField("Email", e.Email);
                    if (!string.IsNullOrEmpty(e.Password))   AddDetailPasswordField(e.Password);
                    if (!string.IsNullOrEmpty(e.TotpSecret)) AddDetailField("TOTP Secret", e.TotpSecret, masked: true);
                    break;
                case EntryType.Card:
                    if (!string.IsNullOrEmpty(e.CardholderName)) AddDetailField("Cardholder", e.CardholderName);
                    if (!string.IsNullOrEmpty(e.CardNumber))     AddDetailField("Card Number", e.MaskedCardNumber, copyValue: e.CardNumber);
                    if (!string.IsNullOrEmpty(e.Expiry))         AddDetailField("Expiry", e.Expiry);
                    if (!string.IsNullOrEmpty(e.Cvv))            AddDetailField("CVV", "***", copyValue: e.Cvv, masked: true);
                    if (!string.IsNullOrEmpty(e.CardBrand))      AddDetailField("Brand", e.CardBrand);
                    break;
                case EntryType.Note:
                    if (!string.IsNullOrEmpty(e.Content)) AddDetailNote(e.Content);
                    break;
                case EntryType.Identity:
                    if (!string.IsNullOrEmpty(e.FirstName + e.LastName)) AddDetailField("Full Name", $"{e.FirstName} {e.LastName}".Trim());
                    if (!string.IsNullOrEmpty(e.Email))   AddDetailField("Email", e.Email);
                    if (!string.IsNullOrEmpty(e.Phone))   AddDetailField("Phone", e.Phone);
                    if (!string.IsNullOrEmpty(e.Address)) AddDetailField("Address", e.Address);
                    if (!string.IsNullOrEmpty(e.City))    AddDetailField("City", e.City);
                    if (!string.IsNullOrEmpty(e.Country)) AddDetailField("Country", e.Country);
                    break;
            }
            if (!string.IsNullOrEmpty(e.Notes)) AddDetailNote(e.Notes, "Notes");
            DetailBody.Children.Add(new TextBlock
            {
                Text = $"Modified  {e.Modified.ToLocalTime():dd MMM yyyy  HH:mm}",
                FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0x38, 0x34, 0x58)),
                FontFamily = new FontFamily("Segoe UI"), Margin = new Thickness(0, 12, 0, 0)
            });
        }

        private void AddDetailField(string label, string display, string? copyValue = null, bool masked = false)
        {
            var copy = copyValue ?? display;
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            sp.Children.Add(new TextBlock
            {
                Text = label.ToUpper(), FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(0x48, 0x44, 0x68)),
                Margin = new Thickness(0, 0, 0, 4), FontWeight = FontWeights.SemiBold
            });
            var rowBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 7, 8, 7)
            };
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            if (masked) row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var valTb = new TextBlock
            {
                Text = display, FontSize = 12, FontFamily = new FontFamily("Segoe UI"),
                Foreground = new SolidColorBrush(Color.FromRgb(0xD8, 0xD4, 0xCE)),
                VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(valTb, 0); row.Children.Add(valTb);
            var copyBtn = MiniCopyBtn(copy);
            Grid.SetColumn(copyBtn, 1); row.Children.Add(copyBtn);

            if (masked)
            {
                var revBtn = MiniActionBtn("Show");
                bool showing = false;
                revBtn.Click += (s, e) =>
                {
                    showing = !showing;
                    valTb.Text     = showing ? copy : display;
                    revBtn.Content = showing ? "Hide" : "Show";
                };
                Grid.SetColumn(revBtn, 2); row.Children.Add(revBtn);
            }
            rowBorder.Child = row;
            sp.Children.Add(rowBorder);
            DetailBody.Children.Add(sp);
        }

        private void AddDetailPasswordField(string pw)
        {
            var s = PasswordStrength.Score(pw);
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            sp.Children.Add(new TextBlock
            {
                Text = "PASSWORD", FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(0x48, 0x44, 0x68)),
                Margin = new Thickness(0, 0, 0, 4), FontWeight = FontWeights.SemiBold
            });
            var rowBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 7, 8, 7)
            };
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            bool masked = true;
            var pwTb = new TextBlock
            {
                Text = "* * * * * * * * * *", FontSize = 13, FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(0xD8, 0xD4, 0xCE)),
                VerticalAlignment = VerticalAlignment.Center
            };
            var showBtn = MiniActionBtn("Show");
            showBtn.Click += (s2, e2) =>
            {
                masked = !masked;
                pwTb.Text       = masked ? "* * * * * * * * * *" : pw;
                pwTb.FontSize   = masked ? 13 : 12;
                showBtn.Content = masked ? "Show" : "Hide";
            };
            Grid.SetColumn(pwTb, 0); row.Children.Add(pwTb);
            Grid.SetColumn(showBtn, 1); row.Children.Add(showBtn);
            var cpyBtn = MiniCopyBtn(pw); Grid.SetColumn(cpyBtn, 2); row.Children.Add(cpyBtn);
            rowBorder.Child = row;
            sp.Children.Add(rowBorder);

            var strRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
            strRow.Children.Add(StrengthBar(s, 100));
            strRow.Children.Add(new TextBlock
            {
                Text = PasswordStrength.Label(s), FontSize = 10,
                Foreground = new SolidColorBrush(PasswordStrength.Color(s)),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0)
            });
            sp.Children.Add(rowBorder);
            sp.Children.Add(strRow);
            DetailBody.Children.Add(sp);
        }

        private void AddDetailNote(string content, string label = "Content")
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            sp.Children.Add(new TextBlock
            {
                Text = label.ToUpper(), FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(0x48, 0x44, 0x68)),
                Margin = new Thickness(0, 0, 0, 4), FontWeight = FontWeights.SemiBold
            });
            sp.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Child = new TextBlock
                {
                    Text = content, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0xD8, 0xD4, 0xCE)),
                    TextWrapping = TextWrapping.Wrap, LineHeight = 20
                }
            });
            DetailBody.Children.Add(sp);
        }

        private void CloseDetailPanel()
        {
            DetailPanel.Visibility = Visibility.Collapsed;
            _selectedId = null;
            _selected   = null;
        }

        // ── CRUD ──────────────────────────────────────────────────────────────
        private void Add_Click(object sender, RoutedEventArgs e)        => OpenEntryDialog(null);
        private void EditEntry_Click(object sender, RoutedEventArgs e)  => OpenEntryDialog(_selected);
        private void CloseDetail_Click(object sender, RoutedEventArgs e){ CloseDetailPanel(); RenderEntries(); }

        private void StarEntry_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null) return;
            _selected.Starred = !_selected.Starred;
            Session.Save(); Refresh();
            if (_selected != null) OpenDetailPanel(_selected);
        }

        private void DeleteEntry_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null) return;
            var dlg = new ConfirmDialog(
                $"Delete \"{_selected.Name}\"?",
                "This entry will be permanently removed from your vault. This action cannot be undone.",
                "Delete", isDanger: true) { Owner = this };
            if (dlg.ShowDialog() != true) return;
            Session.Entries.Remove(_selected);
            Session.Save();
            CloseDetailPanel();
            Refresh();
        }

        private void OpenEntryDialog(VaultEntry? existing)
        {
            var dlg = new EntryDialog(existing) { Owner = this };
            if (dlg.ShowDialog() != true) return;
            if (existing == null)
                Session.Entries.Add(dlg.Result!);
            else
            {
                var idx = Session.Entries.IndexOf(existing);
                if (idx >= 0) Session.Entries[idx] = dlg.Result!;
            }
            Session.Save();
            _selectedId = dlg.Result!.Id;
            _selected   = dlg.Result;
            Refresh();
            OpenDetailPanel(_selected);
        }

        // ── Toolbar ───────────────────────────────────────────────────────────
        private void Lock_Click(object sender, RoutedEventArgs e)
        {
            Session.Lock();
            var lock_ = new LockWindow();
            Application.Current.MainWindow = lock_;
            lock_.Show();
            Close();
        }

        private void Generator_Click(object sender, RoutedEventArgs e)
            => new GeneratorDialog { Owner = this }.ShowDialog();

        private void Settings_Click(object sender, RoutedEventArgs e)
            => new SettingsDialog { Owner = this }.ShowDialog();

        private void Sort_Click(object sender, RoutedEventArgs e)
        {
            _sort = _sort == "name" ? "date" : "name";
            RenderEntries();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _search = SearchBox.Text;
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(_search) ? Visibility.Visible : Visibility.Collapsed;
            RenderEntries();
        }
        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)  => SearchPlaceholder.Visibility = Visibility.Collapsed;
        private void SearchBox_LostFocus(object sender, RoutedEventArgs e) => SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;

        private void DetailUrl_Click(object sender, MouseButtonEventArgs e)
        {
            if (string.IsNullOrEmpty(_selected?.Url)) return;
            var url = _selected.Url.StartsWith("http") ? _selected.Url : "https://" + _selected.Url;
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.N && (e.KeyboardDevice.Modifiers & ModifierKeys.Control) != 0) { OpenEntryDialog(null); e.Handled = true; }
            if (e.Key == Key.F && (e.KeyboardDevice.Modifiers & ModifierKeys.Control) != 0) { SearchBox.Focus(); e.Handled = true; }
            if (e.Key == Key.L && (e.KeyboardDevice.Modifiers & ModifierKeys.Control) != 0) { Lock_Click(sender, e); e.Handled = true; }
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static readonly Color[] _palette =
        {
            Color.FromRgb(0x7B,0x5C,0xF0), Color.FromRgb(0x9D,0x7F,0xF5),
            Color.FromRgb(0x5A,0x7A,0xD8), Color.FromRgb(0xA0,0x60,0xE0),
            Color.FromRgb(0x60,0x90,0xD0), Color.FromRgb(0xC0,0x6A,0xD8),
            Color.FromRgb(0x6A,0x8A,0xF0), Color.FromRgb(0x88,0x5C,0xC8)
        };

        private static Color AvatarColor(string name)
        {
            int h = name.Aggregate(0, (acc, c) => (acc * 31 + c) & 0x7FFFFFFF);
            return _palette[Math.Abs(h) % _palette.Length];
        }

        private static StackPanel StrengthBar(PasswordStrength.Level s, double width)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Width = width };
            double segW = (width - 8) / 5;
            for (int i = 1; i <= 5; i++)
                sp.Children.Add(new Border
                {
                    Width = segW, Height = 3, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 2, 0),
                    Background = new SolidColorBrush(i <= (int)s ? PasswordStrength.Color(s) : Color.FromRgb(0x1A, 0x1A, 0x2E))
                });
            return sp;
        }

        private static Button MiniCopyBtn(string value, string label = "Copy", bool visible = true)
        {
            var btn = new Button
            {
                Content = label, FontFamily = new FontFamily("Segoe UI"), FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x44, 0x68)),
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)),
                BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x32)),
                Padding = new Thickness(7, 3, 7, 3), Cursor = Cursors.Hand,
                Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                Visibility = visible ? Visibility.Visible : Visibility.Collapsed
            };
            btn.Template = RoundedTemplate(5);
            btn.Click += (s, e) =>
            {
                if (string.IsNullOrEmpty(value)) return;
                Session.CopyToClipboard(value);
                var orig = btn.Content;
                btn.Content = "Copied";
                var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                t.Tick += (_, __) => { btn.Content = orig; t.Stop(); };
                t.Start();
            };
            return btn;
        }

        private static Button MiniActionBtn(string label)
        {
            var btn = new Button
            {
                Content = label, FontFamily = new FontFamily("Segoe UI"), FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x44, 0x68)),
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)),
                BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x32)),
                Padding = new Thickness(7, 3, 7, 3), Cursor = Cursors.Hand,
                Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center
            };
            btn.Template = RoundedTemplate(5);
            return btn;
        }

        private static ControlTemplate RoundedTemplate(double r) => new ControlTemplate(typeof(Button))
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
    }

    internal static class FEFExtensions
    {
        public static T Also<T>(this T self, Action<T> action) { action(self); return self; }
    }
}
