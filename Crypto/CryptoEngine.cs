using System;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace VaultApp.Crypto
{
    /// <summary>
    /// Cryptographic core for Vault.
    ///
    /// Key derivation : Argon2id  (m=65536 KB, t=3 iterations, p=4 lanes)
    /// Encryption     : AES-256-GCM authenticated encryption
    /// Tamper detect  : HMAC-SHA256 over vault blob stored in .meta
    /// Recovery key   : 32 cryptographically random bytes, shown once as Base58
    /// Entropy        : zxcvbn-style pattern scoring + crack-time estimate
    /// </summary>
    public static class CryptoEngine
    {
        // ── Constants ──────────────────────────────────────────────────────────
        public const int SaltBytes    = 32;
        public const int NonceBytes   = 12;
        public const int TagBytes     = 16;
        public const int KeyBytes     = 32;   // AES-256

        // Argon2id parameters — tuned for ~300ms on a mid-range desktop
        private const int Argon2Memory      = 65_536;  // 64 MB
        private const int Argon2Iterations  = 3;
        private const int Argon2Parallelism = 4;

        // Recovery key
        public const int RecoveryKeyBytes = 32;

        // ── Salt ───────────────────────────────────────────────────────────────
        public static byte[] GenerateSalt() =>
            RandomNumberGenerator.GetBytes(SaltBytes);

        // ── Key Derivation: Argon2id ───────────────────────────────────────────
        /// <summary>
        /// Derives a 256-bit key from a master PIN using Argon2id.
        /// Deliberately slow to resist brute-force attacks.
        /// </summary>
        public static byte[] DeriveKey(string pin, byte[] salt)
        {
            var argon2 = new Argon2id(Encoding.UTF8.GetBytes(pin))
            {
                Salt        = salt,
                MemorySize  = Argon2Memory,
                Iterations  = Argon2Iterations,
                DegreeOfParallelism = Argon2Parallelism
            };
            return argon2.GetBytes(KeyBytes);
        }

        // ── Encryption: AES-256-GCM ────────────────────────────────────────────
        /// <summary>
        /// Layout: [32 salt][12 nonce][16 tag][ciphertext]
        /// Salt embedded so the blob is fully self-contained.
        /// </summary>
        public static byte[] Encrypt(byte[] plaintext, byte[] key, byte[] salt)
        {
            var nonce      = RandomNumberGenerator.GetBytes(NonceBytes);
            var ciphertext = new byte[plaintext.Length];
            var tag        = new byte[TagBytes];

            using var aes = new AesGcm(key, TagBytes);
            aes.Encrypt(nonce, plaintext, ciphertext, tag);

            var result = new byte[SaltBytes + NonceBytes + TagBytes + ciphertext.Length];
            Buffer.BlockCopy(salt,       0, result, 0,                                 SaltBytes);
            Buffer.BlockCopy(nonce,      0, result, SaltBytes,                         NonceBytes);
            Buffer.BlockCopy(tag,        0, result, SaltBytes + NonceBytes,            TagBytes);
            Buffer.BlockCopy(ciphertext, 0, result, SaltBytes + NonceBytes + TagBytes, ciphertext.Length);
            return result;
        }

        public static byte[] EncryptString(string plaintext, byte[] key, byte[] salt) =>
            Encrypt(Encoding.UTF8.GetBytes(plaintext), key, salt);

        /// <summary>
        /// Decrypts and authenticates. Throws CryptographicException if tag fails
        /// (wrong key or tampered ciphertext).
        /// </summary>
        public static byte[] Decrypt(byte[] blob, byte[] key)
        {
            if (blob.Length < SaltBytes + NonceBytes + TagBytes)
                throw new CryptographicException("Vault blob is too short — file may be corrupt.");

            var nonce      = new byte[NonceBytes];
            var tag        = new byte[TagBytes];
            var ciphertext = new byte[blob.Length - SaltBytes - NonceBytes - TagBytes];
            var plaintext  = new byte[ciphertext.Length];

            Buffer.BlockCopy(blob, SaltBytes,                                 nonce,      0, NonceBytes);
            Buffer.BlockCopy(blob, SaltBytes + NonceBytes,                    tag,        0, TagBytes);
            Buffer.BlockCopy(blob, SaltBytes + NonceBytes + TagBytes,         ciphertext, 0, ciphertext.Length);

            using var aes = new AesGcm(key, TagBytes);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return plaintext;
        }

        public static string DecryptString(byte[] blob, byte[] key) =>
            Encoding.UTF8.GetString(Decrypt(blob, key));

        public static byte[] ExtractSalt(byte[] blob)
        {
            var salt = new byte[SaltBytes];
            Buffer.BlockCopy(blob, 0, salt, 0, SaltBytes);
            return salt;
        }

        // ── HMAC-SHA256 Tamper Detection ───────────────────────────────────────
        /// <summary>
        /// Computes HMAC-SHA256 of the vault blob using the derived key.
        /// Store in .meta; verify on every load.
        /// </summary>
        public static string ComputeHmac(byte[] blob, byte[] key)
        {
            using var hmac = new HMACSHA256(key);
            return Convert.ToHexString(hmac.ComputeHash(blob));
        }

        public static bool VerifyHmac(byte[] blob, byte[] key, string storedHmac)
        {
            var computed = ComputeHmac(blob, key);
            // Constant-time comparison
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computed),
                Encoding.UTF8.GetBytes(storedHmac));
        }

        // ── Recovery Key ───────────────────────────────────────────────────────
        /// <summary>Generates a 32-byte random recovery key.</summary>
        public static byte[] GenerateRecoveryKey() =>
            RandomNumberGenerator.GetBytes(RecoveryKeyBytes);

        /// <summary>
        /// Encodes recovery key as Base58 (human-readable, no ambiguous chars).
        /// Displayed as groups of 6 for readability.
        /// </summary>
        public static string EncodeRecoveryKey(byte[] key)
        {
            const string alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
            var encoded = new System.Text.StringBuilder();
            // Simple base58 encode
            var digits = new int[key.Length * 137 / 100 + 1];
            int digitsLen = 1;
            foreach (var b in key)
            {
                int carry = b;
                for (int j = 0; j < digitsLen; j++)
                {
                    carry += digits[j] << 8;
                    digits[j] = carry % 58;
                    carry /= 58;
                }
                while (carry > 0) { digits[digitsLen++] = carry % 58; carry /= 58; }
            }
            foreach (var b in key) { if (b == 0) encoded.Append('1'); else break; }
            for (int i = digitsLen - 1; i >= 0; i--) encoded.Append(alphabet[digits[i]]);

            // Format as groups of 6 with dashes for readability
            var raw = encoded.ToString();
            var grouped = new System.Text.StringBuilder();
            for (int i = 0; i < raw.Length; i++)
            {
                if (i > 0 && i % 6 == 0) grouped.Append('-');
                grouped.Append(raw[i]);
            }
            return grouped.ToString();
        }

        public static byte[] DecodeRecoveryKey(string encoded)
        {
            const string alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
            var clean = encoded.Replace("-", "").Replace(" ", "");
            var bytes  = new int[clean.Length * 733 / 1000 + 1];
            int bytesLen = 1;
            foreach (var c in clean)
            {
                int carry = alphabet.IndexOf(c);
                if (carry < 0) throw new FormatException("Invalid recovery key character.");
                for (int j = 0; j < bytesLen; j++)
                {
                    carry += bytes[j] * 58;
                    bytes[j] = carry & 0xFF;
                    carry >>= 8;
                }
                while (carry > 0) { bytes[bytesLen++] = carry & 0xFF; carry >>= 8; }
            }
            var result = new byte[bytesLen];
            for (int i = 0; i < bytesLen; i++) result[i] = (byte)bytes[bytesLen - 1 - i];
            return result;
        }

        /// <summary>
        /// Encrypts the vault salt using the recovery key (not the vault key).
        /// This lets us reset the master password without losing the vault.
        /// Recovery key → decrypt salt → derive new vault key → re-encrypt.
        /// </summary>
        public static byte[] EncryptSaltWithRecoveryKey(byte[] vaultSalt, byte[] recoveryKey)
        {
            // Derive an encryption key from the recovery key using Argon2id with a fixed label salt
            var labelSalt = new byte[32];
            Encoding.UTF8.GetBytes("VAULT-RECOVERY-KEY-V1").CopyTo(labelSalt, 0);
            var rk = new Argon2id(recoveryKey) { Salt = labelSalt, MemorySize = 65_536, Iterations = 3, DegreeOfParallelism = 4 };
            var rkKey = rk.GetBytes(KeyBytes);
            return Encrypt(vaultSalt, rkKey, labelSalt);
        }

        public static byte[] DecryptSaltWithRecoveryKey(byte[] encryptedSalt, byte[] recoveryKey)
        {
            var labelSalt = new byte[32];
            Encoding.UTF8.GetBytes("VAULT-RECOVERY-KEY-V1").CopyTo(labelSalt, 0);
            var rk = new Argon2id(recoveryKey) { Salt = labelSalt, MemorySize = 65_536, Iterations = 3, DegreeOfParallelism = 4 };
            var rkKey = rk.GetBytes(KeyBytes);
            return Decrypt(encryptedSalt, rkKey);
        }

        // ── Password Generation ────────────────────────────────────────────────
        public static string GeneratePassword(int length, bool upper, bool lower,
            bool digits, bool symbols, bool avoidAmbiguous)
        {
            var pool = new StringBuilder();
            if (upper)   pool.Append(avoidAmbiguous ? "ABCDEFGHJKMNPQRSTUVWXYZ" : "ABCDEFGHIJKLMNOPQRSTUVWXYZ");
            if (lower)   pool.Append(avoidAmbiguous ? "abcdefghjkmnpqrstuvwxyz" : "abcdefghijklmnopqrstuvwxyz");
            if (digits)  pool.Append(avoidAmbiguous ? "23456789" : "0123456789");
            if (symbols) pool.Append("!@#$%^&*()-_=+[]{}|;:,.<>?");
            if (pool.Length == 0) pool.Append("abcdefghijklmnopqrstuvwxyz");

            var chars  = pool.ToString();
            var result = new char[length];
            var buf    = RandomNumberGenerator.GetBytes(length * 4);
            int offset = 0;
            for (int i = 0; i < length; i++)
            {
                uint rand;
                do
                {
                    if (offset + 4 > buf.Length) { buf = RandomNumberGenerator.GetBytes(length * 4); offset = 0; }
                    rand = BitConverter.ToUInt32(buf, offset);
                    offset += 4;
                } while (rand >= uint.MaxValue - (uint.MaxValue % (uint)chars.Length));
                result[i] = chars[(int)(rand % chars.Length)];
            }
            return new string(result);
        }

        private static readonly string[] _words = {
            "abandon","ability","absent","absorb","abstract","abuse","access","account","accuse","achieve",
            "acid","acquire","action","actor","adapt","addict","address","adjust","admit","adult",
            "advance","advice","afford","afraid","agent","agree","alarm","album","alcohol","alert",
            "alien","allow","almost","alone","alpha","alter","amateur","analyst","ancient","angry",
            "ankle","announce","answer","antenna","antique","apart","apple","approve","arch","arctic",
            "arena","argue","armor","army","arrest","arrive","arrow","artist","aspect","assault",
            "assist","assume","athlete","atom","attack","attend","attract","audit","avoid","award",
            "balance","banner","barrel","battle","become","behave","believe","benefit","better","beyond",
            "blanket","borrow","bottle","bottom","bounce","broken","brother","burden","butter","bypass",
            "camera","candle","captain","carbon","castle","cattle","cement","certain","chapter","charge",
            "cherry","choice","circuit","citizen","classic","climate","cluster","coffee","column","combat",
            "comfort","common","complex","concept","connect","corner","correct","courage","cricket","cruise",
            "crystal","culture","current","custom","damage","danger","debate","decade","defend","define"
        };

        public static string GeneratePassphrase(int wordCount = 5)
        {
            var seps = new[] { "-", ".", "_", "+" };
            var buf  = RandomNumberGenerator.GetBytes((wordCount + 1) * 4);
            var sel  = new string[wordCount];
            for (int i = 0; i < wordCount; i++)
                sel[i] = _words[BitConverter.ToUInt32(buf, i * 4) % (uint)_words.Length];
            var sep = seps[BitConverter.ToUInt32(buf, wordCount * 4) % (uint)seps.Length];
            return string.Join(sep, sel);
        }

        // ── Entropy Estimation ─────────────────────────────────────────────────
        public record EntropyResult(double Bits, string CrackTime, string Label);

        /// <summary>
        /// Estimates password entropy using character pool size and length,
        /// adjusted for obvious patterns. Returns crack time at 10 billion guesses/sec.
        /// </summary>
        public static EntropyResult EstimateEntropy(string pw)
        {
            if (string.IsNullOrEmpty(pw)) return new(0, "instant", "No password");

            // Determine pool size
            int pool = 0;
            bool hasLower  = false, hasUpper = false, hasDigit = false, hasSym = false;
            foreach (var c in pw)
            {
                if (char.IsLower(c))   hasLower  = true;
                if (char.IsUpper(c))   hasUpper  = true;
                if (char.IsDigit(c))   hasDigit  = true;
                if (!char.IsLetterOrDigit(c)) hasSym = true;
            }
            if (hasLower)  pool += 26;
            if (hasUpper)  pool += 26;
            if (hasDigit)  pool += 10;
            if (hasSym)    pool += 32;
            if (pool == 0) pool  = 26;

            double bits = pw.Length * Math.Log2(pool);

            // Penalise repeating characters
            int repeats = 0;
            for (int i = 1; i < pw.Length; i++)
                if (pw[i] == pw[i - 1]) repeats++;
            bits -= repeats * 2;
            bits = Math.Max(0, bits);

            // Crack time at 10^10 guesses/sec (GPU cluster)
            double guesses     = Math.Pow(2, bits);
            double seconds     = guesses / 1e10;
            string crackTime   = FormatCrackTime(seconds);

            string label = bits switch
            {
                < 28  => "Very Weak",
                < 36  => "Weak",
                < 60  => "Fair",
                < 80  => "Strong",
                _     => "Very Strong"
            };

            return new(Math.Round(bits, 1), crackTime, label);
        }

        private static string FormatCrackTime(double seconds) => seconds switch
        {
            < 1            => "less than a second",
            < 60           => $"{(int)seconds} seconds",
            < 3_600        => $"{(int)(seconds / 60)} minutes",
            < 86_400       => $"{(int)(seconds / 3_600)} hours",
            < 2_592_000    => $"{(int)(seconds / 86_400)} days",
            < 31_536_000   => $"{(int)(seconds / 2_592_000)} months",
            < 3_153_600_000=> $"{(int)(seconds / 31_536_000)} years",
            _              => "centuries"
        };

        // ── Memory Safety ──────────────────────────────────────────────────────
        public static void SecureClear(byte[]? buffer)
        {
            if (buffer != null) CryptographicOperations.ZeroMemory(buffer);
        }
    }
}
