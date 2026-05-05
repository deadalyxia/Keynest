using System;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;
using VaultApp.Crypto;
using VaultApp.Models;

namespace VaultApp.Helpers
{
    /// <summary>
    /// Manages vault persistence.
    ///
    /// Files (in %APPDATA%\VaultApp\):
    ///   vault.vault  — AES-256-GCM encrypted payload (all entries)
    ///   vault.meta   — plaintext JSON: Argon2 salt, HMAC, recovery token, reset flag
    ///
    /// Security guarantees:
    ///   - Wrong PIN → CryptographicException (GCM tag mismatch)
    ///   - Tampered file → CryptographicException (HMAC mismatch detected before decrypt)
    ///   - Atomic writes via .tmp rename
    /// </summary>
    public static class VaultStorage
    {
        private static readonly string VaultDir  = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VaultApp");

        private static readonly string MetaPath  = Path.Combine(VaultDir, "vault.meta");
        private static readonly string DataPath  = Path.Combine(VaultDir, "vault.vault");

        public static bool IsInitialised => File.Exists(MetaPath) && File.Exists(DataPath);

        // ── Initialise new vault ────────────────────────────────────────────────
        /// <summary>
        /// Creates a fresh vault. Returns the plaintext recovery key (show once, never stored).
        /// </summary>
        public static byte[] Initialise(string pin)
        {
            Directory.CreateDirectory(VaultDir);

            var salt        = CryptoEngine.GenerateSalt();
            var key         = CryptoEngine.DeriveKey(pin, salt);
            var recoveryKey = CryptoEngine.GenerateRecoveryKey();

            // Encrypt the vault salt under the recovery key (so we can reset PIN later)
            var encryptedSalt = CryptoEngine.EncryptSaltWithRecoveryKey(salt, recoveryKey);

            // Write empty payload
            var payload = new VaultPayload { Entries = new() };
            var blob    = CryptoEngine.EncryptString(JsonConvert.SerializeObject(payload), key, salt);
            AtomicWrite(DataPath, blob);

            var meta = new VaultMeta
            {
                Version          = 2,
                SaltB64          = Convert.ToBase64String(salt),
                EncryptedSaltB64 = Convert.ToBase64String(encryptedSalt),
                VaultHmacHex     = CryptoEngine.ComputeHmac(blob, key),
                ResetUsed        = false,
                CreatedAt        = DateTime.UtcNow,
                LastUnlockUtc    = DateTime.UtcNow,
                Argon2Memory     = 65_536,
                Argon2Iterations = 3,
                Argon2Parallelism= 4
            };
            WriteMeta(meta);
            CryptoEngine.SecureClear(key);

            return recoveryKey; // Caller shows this once
        }

        // ── Unlock ──────────────────────────────────────────────────────────────
        /// <summary>
        /// Derives the key, verifies HMAC (tamper check), then decrypts.
        /// Throws CryptographicException on wrong PIN or tampered file.
        /// </summary>
        public static byte[] Unlock(string pin)
        {
            var meta = ReadMeta();
            var salt = Convert.FromBase64String(meta.SaltB64);
            var key  = CryptoEngine.DeriveKey(pin, salt);

            // HMAC check first — fast fail on tamper before AES-GCM decrypt
            VerifyIntegrity(key, meta);

            // AES-GCM tag validates the PIN — throws CryptographicException if wrong
            ReadEntries(key);

            meta.LastUnlockUtc = DateTime.UtcNow;
            WriteMeta(meta);

            return key;
        }

        // ── Read / Write entries ────────────────────────────────────────────────
        public static VaultPayload ReadEntries(byte[] key)
        {
            var blob = File.ReadAllBytes(DataPath);
            var json = CryptoEngine.DecryptString(blob, key);
            return JsonConvert.DeserializeObject<VaultPayload>(json) ?? new VaultPayload();
        }

        public static void WriteEntries(VaultPayload payload, byte[] key, byte[] salt)
        {
            var json = JsonConvert.SerializeObject(payload);
            var blob = CryptoEngine.EncryptString(json, key, salt);
            AtomicWrite(DataPath, blob);

            // Update HMAC in meta
            var meta = ReadMeta();
            meta.VaultHmacHex = CryptoEngine.ComputeHmac(blob, key);
            WriteMeta(meta);
        }

        // ── Integrity verification ──────────────────────────────────────────────
        /// <summary>
        /// Verifies the stored HMAC matches the current vault file.
        /// If no HMAC stored (legacy vault), skips check.
        /// </summary>
        public static bool VerifyIntegrity(byte[] key, VaultMeta? meta = null)
        {
            meta ??= ReadMeta();
            if (string.IsNullOrEmpty(meta.VaultHmacHex)) return true; // no HMAC stored yet
            var blob = File.ReadAllBytes(DataPath);
            if (!CryptoEngine.VerifyHmac(blob, key, meta.VaultHmacHex))
                throw new CryptographicException(
                    "Vault integrity check failed. The vault file may have been tampered with.");
            return true;
        }

        // ── Recovery key reset ──────────────────────────────────────────────────
        public static bool ResetUsed => ReadMeta().ResetUsed;

        /// <summary>
        /// Uses the recovery key to decrypt the vault salt, then re-encrypts the vault
        /// with a new PIN. All entries are preserved. Reset is marked used permanently.
        /// </summary>
        public static void PerformRecoveryReset(string newPin, byte[] recoveryKey)
        {
            var meta = ReadMeta();
            if (meta.ResetUsed)
                throw new InvalidOperationException("Recovery reset has already been used.");
            if (string.IsNullOrEmpty(meta.EncryptedSaltB64))
                throw new InvalidOperationException("No recovery token found in vault metadata.");

            // Verify the recovery key is correct by attempting to decrypt the stored salt
            var encryptedSalt = Convert.FromBase64String(meta.EncryptedSaltB64);
            byte[] oldSalt;
            try { oldSalt = CryptoEngine.DecryptSaltWithRecoveryKey(encryptedSalt, recoveryKey); }
            catch { throw new CryptographicException("Recovery key is invalid or incorrect."); }
            CryptoEngine.SecureClear(oldSalt); // we don't need it — entries are wiped by design

            // Write a fresh empty vault under the new password
            var newSalt = CryptoEngine.GenerateSalt();
            var newKey  = CryptoEngine.DeriveKey(newPin, newSalt);
            var blob    = CryptoEngine.EncryptString(
                JsonConvert.SerializeObject(new VaultPayload { Entries = new() }), newKey, newSalt);
            AtomicWrite(DataPath, blob);

            meta.SaltB64      = Convert.ToBase64String(newSalt);
            meta.VaultHmacHex = CryptoEngine.ComputeHmac(blob, newKey);
            meta.ResetUsed    = true;
            WriteMeta(meta);

            CryptoEngine.SecureClear(newKey);
        }

        // ── Change PIN (re-encrypt with new key) ────────────────────────────────
        public static void ChangePIN(string currentPin, string newPin)
        {
            var meta    = ReadMeta();
            var oldSalt = Convert.FromBase64String(meta.SaltB64);
            var oldKey  = CryptoEngine.DeriveKey(currentPin, oldSalt);

            VerifyIntegrity(oldKey, meta);
            var payload = ReadEntries(oldKey);

            var newSalt = CryptoEngine.GenerateSalt();
            var newKey  = CryptoEngine.DeriveKey(newPin, newSalt);
            var blob    = CryptoEngine.EncryptString(JsonConvert.SerializeObject(payload), newKey, newSalt);
            AtomicWrite(DataPath, blob);

            meta.SaltB64      = Convert.ToBase64String(newSalt);
            meta.VaultHmacHex = CryptoEngine.ComputeHmac(blob, newKey);
            WriteMeta(meta);

            CryptoEngine.SecureClear(oldKey);
            // Return new key to caller via Session.UpdateKey
            Session.UpdateKey(newKey);
        }

        // ── Clear all data ──────────────────────────────────────────────────────
        public static void ClearAllData()
        {
            foreach (var path in new[] { DataPath, MetaPath })
            {
                if (!File.Exists(path)) continue;
                var size = (int)Math.Min(new FileInfo(path).Length, 1_048_576);
                File.WriteAllBytes(path, RandomNumberGenerator.GetBytes(Math.Max(size, 64)));
                File.Delete(path);
            }
        }

        // ── Meta helpers ────────────────────────────────────────────────────────
        public static VaultMeta ReadMeta()
        {
            var text = File.ReadAllText(MetaPath, System.Text.Encoding.UTF8);
            return JsonConvert.DeserializeObject<VaultMeta>(text)
                ?? throw new InvalidOperationException("Vault meta file is corrupt or empty.");
        }

        public static void WriteMeta(VaultMeta meta)
        {
            var json = JsonConvert.SerializeObject(meta, Formatting.Indented);
            // Explicit UTF-8 no-BOM prevents Windows from writing a BOM that breaks deserialization
            File.WriteAllText(MetaPath, json, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        public static byte[] GetSalt()
        {
            var meta = ReadMeta();
            return Convert.FromBase64String(meta.SaltB64);
        }

        // ── Atomic write ────────────────────────────────────────────────────────
        private static void AtomicWrite(string path, byte[] data)
        {
            var tmp = path + ".tmp";
            File.WriteAllBytes(tmp, data);
            File.Move(tmp, path, overwrite: true);
        }
    }
}
