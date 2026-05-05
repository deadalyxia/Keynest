using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using VaultApp.Crypto;
using VaultApp.Helpers;
using VaultApp.Models;

namespace VaultApp
{
    /// <summary>
    /// In-memory vault session.
    /// - Holds the decryption key (zeroed on lock)
    /// - Auto-locks after configurable inactivity period
    /// - Auto-clears clipboard after 30 seconds
    /// </summary>
    public static class Session
    {
        private static byte[]?           _key;
        private static DispatcherTimer?  _lockTimer;
        private static DispatcherTimer?  _clipTimer;
        private static string?           _clipContent;

        public static bool IsUnlocked => _key != null;
        public static List<VaultEntry> Entries { get; private set; } = new();

        /// <summary>Fired when the session locks due to inactivity.</summary>
        public static event Action? InactivityLocked;

        // ── Open / Close ───────────────────────────────────────────────────────
        public static void Open(byte[] key)
        {
            _key = key;
            Reload();
            RestartLockTimer();
        }

        public static void Reload()
        {
            if (_key == null) return;
            Entries = VaultStorage.ReadEntries(_key).Entries;
        }

        public static void Save()
        {
            if (_key == null) return;
            var salt = VaultStorage.GetSalt();
            VaultStorage.WriteEntries(new VaultPayload { Entries = Entries }, _key, salt);
            ResetActivity();
        }

        public static void Lock()
        {
            _lockTimer?.Stop();
            _clipTimer?.Stop();
            if (_key != null) { CryptoEngine.SecureClear(_key); _key = null; }
            Entries = new();
        }

        public static void UpdateKey(byte[] newKey)
        {
            if (_key != null) CryptoEngine.SecureClear(_key);
            _key = newKey;
        }

        // ── Activity tracking ──────────────────────────────────────────────────
        /// <summary>
        /// Call on any user interaction to reset the inactivity timer.
        /// </summary>
        public static void ResetActivity()
        {
            RestartLockTimer();
        }

        private static void RestartLockTimer()
        {
            _lockTimer?.Stop();

            int minutes = 5;
            try { minutes = VaultStorage.ReadMeta().AutoLockMinutes; } catch { }
            if (minutes <= 0) return; // 0 = never lock

            _lockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(minutes)
            };
            _lockTimer.Tick += (s, e) =>
            {
                _lockTimer.Stop();
                Lock();
                InactivityLocked?.Invoke();
            };
            _lockTimer.Start();
        }

        // ── Clipboard auto-clear ───────────────────────────────────────────────
        /// <summary>
        /// Copies text to the clipboard and schedules automatic clearing after 30 seconds.
        /// </summary>
        public static void CopyToClipboard(string text)
        {
            Clipboard.SetText(text);
            _clipContent = text;

            _clipTimer?.Stop();
            _clipTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _clipTimer.Tick += (s, e) =>
            {
                _clipTimer!.Stop();
                // Only clear if clipboard still has what we put there
                try
                {
                    if (Clipboard.ContainsText() && Clipboard.GetText() == _clipContent)
                        Clipboard.Clear();
                }
                catch { /* clipboard may be locked by another process */ }
                _clipContent = null;
            };
            _clipTimer.Start();
        }

        public static int ClipboardSecondsRemaining()
        {
            if (_clipTimer == null || !_clipTimer.IsEnabled) return 0;
            return 30; // approximate
        }
    }
}
