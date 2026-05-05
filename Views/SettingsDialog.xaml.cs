using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Newtonsoft.Json;
using VaultApp.Crypto;
using VaultApp.Helpers;
using VaultApp.Models;

namespace VaultApp.Views
{
    public partial class SettingsDialog : ChromeWindow
    {
        public SettingsDialog()
        {
            InitializeComponent();
            RenderMain();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // MAIN MENU
        // ═══════════════════════════════════════════════════════════════════════
        private void RenderMain()
        {
            BodyPanel.Children.Clear();
            DialogTitleBar.Text = "Settings";

            SectionHeader("Security");
            SettingRow("Change Master Password",         "Re-encrypt the vault with a new PIN.",                         "Change",      RenderChangePIN);
            SettingRow("Auto-Lock Timeout",         "Lock vault after a period of inactivity.",                     "Configure",   RenderAutoLock);

            SectionHeader("Password Health");
            SettingRow("Password Health Report",    "Detect weak, reused, and compromised passwords.",              "View Report", RenderHealthReport);

            SectionHeader("Vault");
            SettingRow("Verify Vault Integrity",    "Check the HMAC to confirm no tampering has occurred.",         "Verify Now",  DoIntegrityCheck);
            SettingRow("Import from CSV",           "Import credentials from a CSV file (Bitwarden format).",       "Import",      DoImport);
            SettingRow("Export to JSON",            "Save an unencrypted copy of all entries to disk.",             "Export",      DoExport);

            SectionHeader("Vault Info");
            try
            {
                var meta = VaultStorage.ReadMeta();
                InfoRow("Created",          meta.CreatedAt.ToLocalTime().ToString("dd MMM yyyy  HH:mm"));
                InfoRow("Last unlock",      meta.LastUnlockUtc.ToLocalTime().ToString("dd MMM yyyy  HH:mm"));
                InfoRow("Recovery reset",   meta.ResetUsed ? "Used — unavailable" : "Available");
                InfoRow("KDF",              $"Argon2id  m={meta.Argon2Memory / 1024}MB  t={meta.Argon2Iterations}  p={meta.Argon2Parallelism}");
                InfoRow("Encryption",       "AES-256-GCM");
                InfoRow("Tamper detection", "HMAC-SHA256");
                InfoRow("Entries",          $"{Session.Entries.Count}");
            }
            catch { InfoRow("Status", "Could not read vault metadata."); }

            SectionHeader("Danger Zone");
            var clearBtn = new Button
            {
                Content = "Clear All Data",
                Style   = (Style)Application.Current.Resources["DangerButton"],
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(0, 12, 0, 12), Margin = new Thickness(0, 0, 0, 8)
            };
            clearBtn.Click += (s, e) => DoClearData();
            BodyPanel.Children.Add(clearBtn);
            BodyPanel.Children.Add(new TextBlock
            {
                Text = "Wipes every entry and forces new vault setup. Requires your current PIN. Cannot be undone.",
                FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x48, 0x44, 0x68)),
                TextWrapping = TextWrapping.Wrap, LineHeight = 17
            });
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CHANGE PIN
        // ═══════════════════════════════════════════════════════════════════════
        private void RenderChangePIN()
        {
            BodyPanel.Children.Clear();
            DialogTitleBar.Text = "Change Master Password";

            Heading("Change Master Password");
            Hint("Verify your current PIN, then set a new one. The vault is re-encrypted immediately with the new key.");
            Gap(18);

            FieldLbl("Current PIN");
            var cur  = PwField("curPin");
            Gap(10);
            FieldLbl("New PIN  (minimum 4 characters)");
            var np   = PwField("newPin");
            Gap(10);
            FieldLbl("Confirm new PIN");
            var conf = PwField("confPin");
            Gap(20);

            var msg = MsgBlock();
            var btn = PrimaryBtn("Update PIN");
            btn.Click += (s, e) =>
            {
                var c = cur.Password; var n = np.Password; var k = conf.Password;
                if (c.Length == 0) { Err(msg, "Enter your current PIN."); return; }
                if (n.Length < 4)  { Err(msg, "New PIN must be at least 4 characters."); return; }
                if (n != k)        { Err(msg, "New PINs do not match."); return; }
                try
                {
                    VaultStorage.ChangePIN(c, n);  // re-encrypts and updates Session key
                    Ok(msg, "PIN updated successfully.");
                    cur.Password = ""; np.Password = ""; conf.Password = "";
                }
                catch (CryptographicException) { Err(msg, "Current PIN is incorrect."); }
                catch (Exception ex)           { Err(msg, $"Error: {ex.Message}"); }
            };
            BodyPanel.Children.Add(msg);
            BodyPanel.Children.Add(btn);
            Gap(8);
            BackBtn();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // AUTO-LOCK
        // ═══════════════════════════════════════════════════════════════════════
        private void RenderAutoLock()
        {
            BodyPanel.Children.Clear();
            DialogTitleBar.Text = "Auto-Lock Timeout";

            Heading("Auto-Lock");
            Hint("The vault will automatically lock after this period of inactivity. Choose 0 to disable.");
            Gap(20);

            var meta = VaultStorage.ReadMeta();
            var options = new[] { ("Never", 0), ("1 minute", 1), ("2 minutes", 2), ("5 minutes", 5), ("10 minutes", 10), ("15 minutes", 15), ("30 minutes", 30) };

            foreach (var (label, minutes) in options)
            {
                var row = new Border
                {
                    Background = meta.AutoLockMinutes == minutes
                        ? new SolidColorBrush(Color.FromArgb(20, 0xC8, 0xA9, 0x6E))
                        : new SolidColorBrush(Color.FromRgb(0x0E, 0x0E, 0x1A)),
                    BorderBrush = meta.AutoLockMinutes == minutes
                        ? new SolidColorBrush(Color.FromArgb(80, 0xC8, 0xA9, 0x6E))
                        : new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)),
                    BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 6),
                    Cursor = Cursors.Hand
                };
                var rowGrid = new Grid();
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var lbl = new TextBlock { Text = label, FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(0xEE, 0xEA, 0xF8)), VerticalAlignment = VerticalAlignment.Center };
                var tick = new TextBlock
                {
                    Text = meta.AutoLockMinutes == minutes ? "✓" : "",
                    FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(0x7B, 0x5C, 0xF0)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(lbl, 0); rowGrid.Children.Add(lbl);
                Grid.SetColumn(tick, 1); rowGrid.Children.Add(tick);
                row.Child = rowGrid;
                var capturedMinutes = minutes;
                row.MouseLeftButtonUp += (s, e) =>
                {
                    meta.AutoLockMinutes = capturedMinutes;
                    VaultStorage.WriteMeta(meta);
                    RenderAutoLock(); // refresh to show new selection
                };
                BodyPanel.Children.Add(row);
            }

            Gap(8);
            BackBtn();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // PASSWORD HEALTH REPORT
        // ═══════════════════════════════════════════════════════════════════════
        private void RenderHealthReport()
        {
            BodyPanel.Children.Clear();
            DialogTitleBar.Text = "Password Health";

            Heading("Password Health Report");
            Gap(12);

            var entries  = Session.Entries.Where(e => e.Type == EntryType.Login && !string.IsNullOrEmpty(e.Password)).ToList();
            var allPws   = entries.Select(e => e.Password).ToList();
            var dupes    = allPws.GroupBy(p => p).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
            var weak     = entries.Where(e => PasswordStrength.Score(e.Password) <= PasswordStrength.Level.Weak).ToList();
            var dupeList = entries.Where(e => dupes.Contains(e.Password)).ToList();
            var old      = entries.Where(e => e.Modified < DateTime.UtcNow.AddDays(-90)).ToList();

            // Summary cards
            var summaryGrid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var weakCard  = StatCard($"{weak.Count}",  "Weak",     weak.Count  > 0 ? Color.FromRgb(0xD9, 0x5F, 0x5F) : Color.FromRgb(0x5B, 0xAD, 0x6F));
            var dupeCard  = StatCard($"{dupeList.Count}", "Reused", dupeList.Count > 0 ? Color.FromRgb(0xD4, 0x93, 0x5A) : Color.FromRgb(0x5B, 0xAD, 0x6F));
            var oldCard   = StatCard($"{old.Count}",   "Outdated", old.Count   > 0 ? Color.FromRgb(0x6A, 0x64, 0x88) : Color.FromRgb(0x5B, 0xAD, 0x6F));

            Grid.SetColumn(weakCard, 0); summaryGrid.Children.Add(weakCard);
            Grid.SetColumn(dupeCard, 2); summaryGrid.Children.Add(dupeCard);
            Grid.SetColumn(oldCard,  4); summaryGrid.Children.Add(oldCard);
            BodyPanel.Children.Add(summaryGrid);

            // Weak passwords
            if (weak.Any())
            {
                SectionLabel("Weak Passwords");
                foreach (var e in weak)
                {
                    var entropy = CryptoEngine.EstimateEntropy(e.Password);
                    HealthRow(e.Name, $"{entropy.Bits} bits  —  {entropy.CrackTime}", Color.FromRgb(0xD9, 0x5F, 0x5F));
                }
            }

            // Reused passwords
            if (dupeList.Any())
            {
                SectionLabel("Reused Passwords");
                foreach (var e in dupeList)
                    HealthRow(e.Name, "Password used on multiple sites", Color.FromRgb(0xD4, 0x93, 0x5A));
            }

            // Old passwords
            if (old.Any())
            {
                SectionLabel("Not Updated in 90+ Days");
                foreach (var e in old)
                    HealthRow(e.Name, $"Last updated  {e.Modified.ToLocalTime():dd MMM yyyy}", Color.FromRgb(0x6A, 0x64, 0x88));
            }

            if (!weak.Any() && !dupeList.Any() && !old.Any())
            {
                BodyPanel.Children.Add(new TextBlock
                {
                    Text = "All passwords look healthy.",
                    FontSize = 14, Foreground = new SolidColorBrush(Color.FromRgb(0x5B, 0xAD, 0x6F)),
                    HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 20, 0, 0)
                });
            }

            Gap(12);
            BackBtn();
        }

        private Border StatCard(string number, string label, Color color) =>
            new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x0E, 0x1A)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, color.R, color.G, color.B)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 14, 12, 14),
                Child = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children =
                    {
                        new TextBlock { Text = number, FontSize = 28, FontWeight = FontWeights.Light, Foreground = new SolidColorBrush(color), HorizontalAlignment = HorizontalAlignment.Center },
                        new TextBlock { Text = label,  FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x44, 0x68)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) }
                    }
                }
            };

        private void SectionLabel(string t) =>
            BodyPanel.Children.Add(new TextBlock
            {
                Text = t.ToUpper(), FontSize = 9, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x44, 0x68)),
                Margin = new Thickness(0, 14, 0, 6)
            });

        private void HealthRow(string name, string detail, Color accent)
        {
            var b = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x0E, 0x1A)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 8, 0, 8)
            };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var dot = new Border { Width = 4, Height = 4, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(accent), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = name,   FontSize = 12, FontWeight = FontWeights.Medium, Foreground = new SolidColorBrush(Color.FromRgb(0xEE, 0xEA, 0xF8)) });
            sp.Children.Add(new TextBlock { Text = detail, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x44, 0x68)), Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetColumn(dot, 0); g.Children.Add(dot);
            Grid.SetColumn(sp, 1);  g.Children.Add(sp);
            b.Child = g;
            BodyPanel.Children.Add(b);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // INTEGRITY CHECK
        // ═══════════════════════════════════════════════════════════════════════
        private void DoIntegrityCheck()
        {
            BodyPanel.Children.Clear();
            DialogTitleBar.Text = "Vault Integrity";
            Heading("Verify Vault Integrity");
            Hint("Checks the HMAC-SHA256 signature of the vault file against the value stored in vault.meta. A mismatch means the file was modified outside of Vault.");
            Gap(16);
            FieldLbl("Current PIN  (required to derive the HMAC key)");
            var pw  = PwField("intPin");
            Gap(16);
            var msg = MsgBlock();
            var btn = PrimaryBtn("Run Integrity Check");
            btn.Click += (s, e) =>
            {
                if (pw.Password.Length == 0) { Err(msg, "Enter your PIN."); return; }
                try
                {
                    var key = VaultStorage.Unlock(pw.Password);
                    VaultStorage.VerifyIntegrity(key);
                    CryptoEngine.SecureClear(key);
                    Ok(msg, "Integrity check passed. Vault has not been tampered with.");
                }
                catch (CryptographicException ex) { Err(msg, $"INTEGRITY FAILURE: {ex.Message}"); }
                catch (Exception ex)              { Err(msg, $"Error: {ex.Message}"); }
            };
            BodyPanel.Children.Add(msg);
            BodyPanel.Children.Add(btn);
            Gap(8);
            BackBtn();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // IMPORT CSV (Bitwarden format)
        // ═══════════════════════════════════════════════════════════════════════
        private void DoImport()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Import Credentials",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            BodyPanel.Children.Clear();
            DialogTitleBar.Text = "Import CSV";
            Heading("Import from CSV");
            Gap(8);

            try
            {
                var lines   = File.ReadAllLines(dlg.FileName);
                var imported = 0;
                var skipped  = 0;

                // Bitwarden CSV header:
                // folder,favorite,type,name,notes,fields,reprompt,login_uri,login_username,login_password,login_totp
                // Generic fallback: name,username,password,url,notes
                bool isBitwarden = lines.Length > 0 && lines[0].Contains("login_username");

                for (int i = 1; i < lines.Length; i++)
                {
                    var cols = SplitCsvLine(lines[i]);
                    if (cols.Length < 3) { skipped++; continue; }

                    try
                    {
                        VaultEntry entry;
                        if (isBitwarden && cols.Length >= 10)
                        {
                            entry = new VaultEntry
                            {
                                Type     = EntryType.Login,
                                Name     = cols[3].Trim('"'),
                                Notes    = cols[4].Trim('"'),
                                Url      = cols.Length > 7  ? cols[7].Trim('"')  : "",
                                Username = cols.Length > 8  ? cols[8].Trim('"')  : "",
                                Password = cols.Length > 9  ? cols[9].Trim('"')  : "",
                                TotpSecret = cols.Length > 10 ? cols[10].Trim('"') : "",
                                Created  = DateTime.UtcNow,
                                Modified = DateTime.UtcNow
                            };
                        }
                        else
                        {
                            // Generic: name, username, password[, url, notes]
                            entry = new VaultEntry
                            {
                                Type     = EntryType.Login,
                                Name     = cols[0].Trim('"'),
                                Username = cols[1].Trim('"'),
                                Password = cols[2].Trim('"'),
                                Url      = cols.Length > 3 ? cols[3].Trim('"') : "",
                                Notes    = cols.Length > 4 ? cols[4].Trim('"') : "",
                                Created  = DateTime.UtcNow,
                                Modified = DateTime.UtcNow
                            };
                        }

                        if (!string.IsNullOrWhiteSpace(entry.Name))
                        {
                            Session.Entries.Add(entry);
                            imported++;
                        }
                        else skipped++;
                    }
                    catch { skipped++; }
                }

                Session.Save();

                var result = new TextBlock
                {
                    Text = $"Import complete.\n\n{imported} entries imported.\n{skipped} rows skipped (empty or malformed).",
                    FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(0x5B, 0xAD, 0x6F)),
                    TextWrapping = TextWrapping.Wrap, LineHeight = 20, Margin = new Thickness(0, 0, 0, 16),
                    TextAlignment = TextAlignment.Center
                };
                BodyPanel.Children.Add(result);
            }
            catch (Exception ex)
            {
                BodyPanel.Children.Add(new TextBlock
                {
                    Text = $"Import failed:\n{ex.Message}",
                    FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0xD9, 0x5F, 0x5F)),
                    TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16)
                });
            }

            BackBtn();
        }

        private static string[] SplitCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current   = new System.Text.StringBuilder();
            foreach (char c in line)
            {
                if (c == '"')      { inQuotes = !inQuotes; }
                else if (c == ',' && !inQuotes) { result.Add(current.ToString()); current.Clear(); }
                else               { current.Append(c); }
            }
            result.Add(current.ToString());
            return result.ToArray();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // EXPORT JSON
        // ═══════════════════════════════════════════════════════════════════════
        private void DoExport()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title    = "Export Vault",
                Filter   = "JSON files (*.json)|*.json",
                FileName = $"vault_export_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                File.WriteAllText(dlg.FileName,
                    JsonConvert.SerializeObject(Session.Entries, Formatting.Indented));
                new ConfirmDialog(
                    "Export Complete",
                    $"Exported {Session.Entries.Count} entries to an unencrypted JSON file.\n\nDelete this file when you are done with it.",
                    "OK") { Owner = this }.ShowDialog();
            }
            catch (Exception ex)
            {
                new ConfirmDialog("Export Failed", ex.Message, "OK") { Owner = this }.ShowDialog();
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CLEAR ALL DATA
        // ═══════════════════════════════════════════════════════════════════════
        private void DoClearData()
        {
            var c1 = new ConfirmDialog(
                "Clear All Vault Data?",
                "Every saved entry will be permanently deleted and the vault reset to factory state. This cannot be undone.",
                "Continue", isDanger: true) { Owner = this };
            if (c1.ShowDialog() != true) return;

            var pinDlg = new PinVerifyDialog { Owner = this };
            if (pinDlg.ShowDialog() != true) return;
            try { VaultStorage.Unlock(pinDlg.EnteredPin); }
            catch (CryptographicException)
            {
                new ConfirmDialog("Incorrect PIN", "PIN is wrong. No data was changed.", "OK") { Owner = this }.ShowDialog();
                return;
            }

            var c2 = new ConfirmDialog(
                "Final Confirmation",
                "You are about to permanently destroy all vault data. There is no recovery from this action.\n\nAre you absolutely certain?",
                "Destroy all data", isDanger: true) { Owner = this }.ShowDialog();
            if (c2 != true) return;

            try
            {
                VaultStorage.ClearAllData();
                Session.Lock();
                var lock_ = new LockWindow();
                Application.Current.MainWindow = lock_;
                lock_.Show();
                // Close settings and main window
                foreach (Window w in Application.Current.Windows)
                    if (w is MainWindow || w is SettingsDialog) w.Close();
            }
            catch (Exception ex)
            {
                new ConfirmDialog("Error", ex.Message, "OK") { Owner = this }.ShowDialog();
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UI HELPERS
        // ═══════════════════════════════════════════════════════════════════════
        private void SectionHeader(string title)
        {
            if (BodyPanel.Children.Count > 0)
                BodyPanel.Children.Add(new Rectangle
                {
                    Height = 1, Fill = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)),
                    Margin = new Thickness(0, 18, 0, 18)
                });
            BodyPanel.Children.Add(new TextBlock
            {
                Text = title.ToUpper(), FontSize = 9, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x7B, 0x5C, 0xF0)),
                Margin = new Thickness(0, 0, 0, 10)
            });
        }

        private void SettingRow(string title, string desc, string btnLabel, Action onClick)
        {
            var outer = new Border
            {
                Background      = new SolidColorBrush(Color.FromRgb(0x0E, 0x0E, 0x1A)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 12, 14, 12), Margin = new Thickness(0, 0, 0, 6)
            };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = title,  FontSize = 13, FontWeight = FontWeights.Medium, Foreground = new SolidColorBrush(Color.FromRgb(0xEE, 0xEA, 0xF8)) });
            sp.Children.Add(new TextBlock { Text = desc,   FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x44, 0x68)), Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap });
            var btn = new Button
            {
                Content = btnLabel, Style = (Style)Application.Current.Resources["SecondaryButton"],
                FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 0, 0), Padding = new Thickness(14, 8, 14, 8)
            };
            btn.Click += (s, e) => onClick();
            Grid.SetColumn(sp, 0); g.Children.Add(sp);
            Grid.SetColumn(btn, 1); g.Children.Add(btn);
            outer.Child = g;
            BodyPanel.Children.Add(outer);
        }

        private void InfoRow(string label, string value)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            sp.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x44, 0x68)), Width = 160 });
            sp.Children.Add(new TextBlock { Text = value, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x78, 0xA8)), FontFamily = new FontFamily("Segoe UI") });
            BodyPanel.Children.Add(sp);
        }

        private void Heading(string t) => BodyPanel.Children.Add(new TextBlock
        {
            Text = t, FontSize = 16, FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xEE, 0xEA, 0xF8)), Margin = new Thickness(0, 0, 0, 8)
        });

        private void Hint(string t) => BodyPanel.Children.Add(new TextBlock
        {
            Text = t, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x44, 0x68)),
            TextWrapping = TextWrapping.Wrap, LineHeight = 18, Margin = new Thickness(0, 0, 0, 4)
        });

        private void FieldLbl(string t) => BodyPanel.Children.Add(new TextBlock
        {
            Text = t, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x6A, 0x64, 0x88)),
            Margin = new Thickness(0, 0, 0, 5), TextWrapping = TextWrapping.Wrap
        });

        private void Gap(double h) => BodyPanel.Children.Add(new Rectangle { Height = h });

        private PasswordBox PwField(string tag)
        {
            var pb = new PasswordBox { Tag = tag, Style = (Style)Application.Current.Resources["DarkPasswordBox"] };
            BodyPanel.Children.Add(pb);
            return pb;
        }

        private TextBlock MsgBlock()
        {
            var tb = new TextBlock
            {
                FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0xD9, 0x5F, 0x5F)),
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10)
            };
            return tb;
        }

        private Button PrimaryBtn(string t)
        {
            var b = new Button
            {
                Content = t, Style = (Style)Application.Current.Resources["PrimaryButton"],
                HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(0, 12, 0, 12)
            };
            BodyPanel.Children.Add(b);
            return b;
        }

        private void BackBtn()
        {
            var b = new Button
            {
                Content = "Back",
                Style   = (Style)Application.Current.Resources["SecondaryButton"],
                HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(0, 11, 0, 11)
            };
            b.Click += (s, e) => RenderMain();
            BodyPanel.Children.Add(b);
        }

        private static void Err(TextBlock tb, string msg)
        {
            tb.Foreground = new SolidColorBrush(Color.FromRgb(0xD9, 0x5F, 0x5F));
            tb.Text = msg;
        }

        private static void Ok(TextBlock tb, string msg)
        {
            tb.Foreground = new SolidColorBrush(Color.FromRgb(0x5B, 0xAD, 0x6F));
            tb.Text = msg;
        }
    }
}
