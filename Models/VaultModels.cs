using System;
using System.Collections.Generic;

namespace VaultApp.Models
{
    public enum EntryType { Login, Card, Note, Identity }

    public class VaultEntry
    {
        public string Id        { get; set; } = Guid.NewGuid().ToString("N");
        public EntryType Type   { get; set; } = EntryType.Login;
        public string Name      { get; set; } = string.Empty;
        public bool Starred     { get; set; }
        public DateTime Created  { get; set; } = DateTime.UtcNow;
        public DateTime Modified { get; set; } = DateTime.UtcNow;
        public string Notes     { get; set; } = string.Empty;

        // Login
        public string Url        { get; set; } = string.Empty;
        public string Username   { get; set; } = string.Empty;
        public string Email      { get; set; } = string.Empty;
        public string Password   { get; set; } = string.Empty;
        public string TotpSecret { get; set; } = string.Empty;

        // Card
        public string CardholderName { get; set; } = string.Empty;
        public string CardNumber     { get; set; } = string.Empty;
        public string Expiry         { get; set; } = string.Empty;
        public string Cvv            { get; set; } = string.Empty;
        public string CardBrand      { get; set; } = string.Empty;

        // Identity
        public string FirstName { get; set; } = string.Empty;
        public string LastName  { get; set; } = string.Empty;
        public string Phone     { get; set; } = string.Empty;
        public string Address   { get; set; } = string.Empty;
        public string City      { get; set; } = string.Empty;
        public string Country   { get; set; } = string.Empty;

        // Note
        public string Content { get; set; } = string.Empty;

        public string DisplayUser => Type switch
        {
            EntryType.Login    => !string.IsNullOrEmpty(Username) ? Username : Email,
            EntryType.Card     => !string.IsNullOrEmpty(CardholderName) ? CardholderName : MaskedCardNumber,
            EntryType.Identity => $"{FirstName} {LastName}".Trim(),
            EntryType.Note     => "Secure note",
            _                  => string.Empty
        };

        public string MaskedCardNumber =>
            CardNumber.Length >= 4 ? "**** " + CardNumber[^4..] : CardNumber;

        public string TypeLabel => Type switch
        {
            EntryType.Login    => "Login",
            EntryType.Card     => "Card",
            EntryType.Note     => "Note",
            EntryType.Identity => "Identity",
            _                  => "Unknown"
        };
    }

    public class VaultMeta
    {
        public int    Version            { get; set; } = 2;
        public string SaltB64            { get; set; } = string.Empty;
        public int    Argon2Memory       { get; set; } = 65_536;
        public int    Argon2Iterations   { get; set; } = 3;
        public int    Argon2Parallelism  { get; set; } = 4;
        public string VaultHmacHex       { get; set; } = string.Empty;
        public string EncryptedSaltB64   { get; set; } = string.Empty;
        public bool   ResetUsed          { get; set; }
        public DateTime CreatedAt        { get; set; } = DateTime.UtcNow;
        public DateTime LastUnlockUtc    { get; set; } = DateTime.UtcNow;
        public int    AutoLockMinutes    { get; set; } = 5;
    }

    public class VaultPayload
    {
        public List<VaultEntry> Entries { get; set; } = new();
    }
}
