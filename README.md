# Keynest — Personal Password Manager

Secure, offline-first, open-source password manager for Windows.
**No cloud. No telemetry. No accounts.**

---

## Security Architecture

| Component         | Implementation                                      |
|-------------------|-----------------------------------------------------|
| Encryption        | AES-256-GCM (authenticated — detects tampering)     |
| Key derivation    | **Argon2id**  m=64MB  t=3  p=4 threads              |
| Tamper detection  | HMAC-SHA256 over vault blob, stored in .meta        |
| Nonce             | Fresh 12-byte cryptographic random per save         |
| Memory safety     | Keys zeroed with `CryptographicOperations.ZeroMemory`|
| Clipboard         | Auto-clears after configurable timeout              |
| Auto-lock         | Configurable inactivity timer                       |

### Files (in %APPDATA%\VaultApp\) - Forgot to rename internally
```
vault.vault   — AES-256-GCM encrypted payload (all entries)
vault.meta    — Plaintext JSON: Argon2 salt, HMAC, recovery token, version

```

`vault.meta` contains **no passwords** — only salts, HMAC signatures, and
the encrypted recovery token. It cannot be used to reconstruct vault data.

---

## Building

**Requirements:** Windows 10/11 x64, [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```bat
BUILD.bat
```
Output: `publish\VaultApp.exe` — single standalone executable, no installer needed.

---

## First Run

1. Launch `Keynest.exe`
2. Choose a master password (4–20+ chars, any characters)
3. **Save your recovery key** — shown once, stored nowhere
4. Your encrypted vault is created

---

## Features

### Phase 1 (Core)
- AES-256-GCM encryption, Argon2id key derivation
- Unique salt and IV per vault/save
- Add / Edit / Delete credentials (Login, Card, Note, Identity)
- Password generator — length, charset, avoid-ambiguous, passphrase mode
- Password strength meter + entropy estimation + estimated crack time
- Search across name, username, email, URL
- Copy buttons with clipboard auto-clear
- Auto-lock after inactivity

### Phase 2 (Security)
- Recovery Key replacing security questions (generated once, shown once)
- HMAC-SHA256 tamper detection — "Verify Integrity" button in settings
- Password health panel — duplicates, weak passwords, strength overview
- Entropy estimator with crack-time display
- Argon2id parameters displayed in Settings for auditability

### UI
- Custom borderless chrome on all windows
- Animated splash screen
- Sidebar with category counts
- Dark, minimal design - warm purple accent

### Data
- Import from CSV (Bitwarden and LastPass format auto-detected)
- Export to unencrypted JSON (with warning)
- Clear All Data - triple confirmation + PIN required

---

## Keyboard Shortcuts

| Shortcut  | Action          |
|-----------|-----------------|
| `Ctrl+N`  | New entry       |
| `Ctrl+F`  | Focus search    |
| `Ctrl+L`  | Lock vault      |

---

## Threat Model

**Protected against:** disk theft, other OS users reading files, brute-force
(Argon2id makes each guess cost ~300ms), file tampering (HMAC catches it).

**Not protected against:** keyloggers or malware with your user privileges,
screen capture while unlocked, physical observation of PIN entry,
RAM scraping (keys are in memory while unlocked - unavoidable for any password manager).

---

## Project Structure

```
VaultApp/
├── Crypto/CryptoEngine.cs        Argon2id, AES-256-GCM, HMAC, recovery key, entropy
├── Helpers/VaultStorage.cs       File I/O, atomic writes, integrity verification
├── Models/VaultModels.cs         VaultEntry, VaultMeta, VaultPayload
├── Session.cs                    In-memory state, auto-lock timer, clipboard clear
├── Views/
│   ├── ChromeWindow.cs           Custom borderless chrome base class
│   ├── SplashWindow              Animated loading screen
│   ├── LockWindow                Password setup (with recovery key), unlock, recovery reset
│   ├── MainWindow                Vault UI — sidebar, entry list, detail panel
│   ├── EntryDialog               Add / edit entries
│   ├── GeneratorDialog           Password generator
│   ├── SettingsDialog            PIN change, health check, integrity verify, import/export
│   ├── ConfirmDialog             Reusable confirmation dialog
│   └── PinVerifyDialog           PIN prompt for destructive actions
```
