using System.Security.Cryptography;
using System.Text;

namespace TolllgaFinale.Services;

/// <summary>
/// MUST be placed in:  Services/SecurityService.cs
/// Namespace:          TolllgaFinale.Services
///
/// Provides: PBKDF2 password hashing, AES-256 encryption, SecureStorage key management.
/// </summary>
public static class SecurityService
{
    // ── PBKDF2 constants ──────────────────────────────────────────────────────
    private const int PbkdfIterations = 100_000;
    private const int SaltBytes       = 16;
    private const int HashBytes       = 32;

    // ── SecureStorage keys ────────────────────────────────────────────────────
    private const string DbKeyStorageKey   = "tolllga_db_key";
    private const string JsonKeyStorageKey = "tolllga_json_key";

    // ══ PASSWORD HASHING ══════════════════════════════════════════════════════

    /// <summary>Hash with PBKDF2-SHA256 + random salt. Returns "base64salt:base64hash".</summary>
    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, PbkdfIterations, HashAlgorithmName.SHA256, HashBytes);
        return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Verify a password against a stored hash.
    /// Accepts both PBKDF2 ("salt:hash") and legacy SHA-256 (plain hex).
    /// </summary>
    public static bool VerifyPassword(string password, string stored)
    {
        if (string.IsNullOrEmpty(stored)) return false;

        // Legacy SHA-256 hex (64 hex chars, no colon)
        if (!stored.Contains(':'))
        {
            var legacy = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLower();
            return legacy == stored;
        }

        var parts = stored.Split(':', 2);
        if (parts.Length != 2) return false;

        try
        {
            var salt         = Convert.FromBase64String(parts[0]);
            var expectedHash = Convert.FromBase64String(parts[1]);
            var actualHash   = Rfc2898DeriveBytes.Pbkdf2(
                password, salt, PbkdfIterations, HashAlgorithmName.SHA256, HashBytes);
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch { return false; }
    }

    // ══ AES-256 ENCRYPTION ════════════════════════════════════════════════════

    /// <summary>AES-256-CBC encrypt. Output: Base64(IV[16] + ciphertext).</summary>
    public static string Encrypt(string plainText, byte[] key)
    {
        using var aes   = Aes.Create();
        aes.Key         = key;
        aes.Mode        = CipherMode.CBC;
        aes.Padding     = PaddingMode.PKCS7;
        aes.GenerateIV();
        using var enc   = aes.CreateEncryptor();
        var plain       = Encoding.UTF8.GetBytes(plainText);
        var cipher      = enc.TransformFinalBlock(plain, 0, plain.Length);
        return Convert.ToBase64String(aes.IV.Concat(cipher).ToArray());
    }

    /// <summary>AES-256-CBC decrypt. Input must come from <see cref="Encrypt"/>.</summary>
    public static string Decrypt(string cipherB64, byte[] key)
    {
        var data = Convert.FromBase64String(cipherB64);
        if (data.Length < 17) throw new CryptographicException("Invalid cipher data.");
        using var aes   = Aes.Create();
        aes.Key         = key;
        aes.IV          = data[..16];
        aes.Mode        = CipherMode.CBC;
        aes.Padding     = PaddingMode.PKCS7;
        using var dec   = aes.CreateDecryptor();
        var plain       = dec.TransformFinalBlock(data, 16, data.Length - 16);
        return Encoding.UTF8.GetString(plain);
    }

    // ══ KEY MANAGEMENT ════════════════════════════════════════════════════════

    /// <summary>Get or generate the 256-bit JSON encryption key from SecureStorage.</summary>
    public static async Task<byte[]> GetOrCreateJsonKeyAsync()
        => await GetOrCreateKeyAsync(JsonKeyStorageKey);

    /// <summary>Get or generate the 256-bit database key from SecureStorage.</summary>
    public static async Task<byte[]> GetOrCreateDbKeyAsync()
        => await GetOrCreateKeyAsync(DbKeyStorageKey);

    private static async Task<byte[]> GetOrCreateKeyAsync(string storageKey)
    {
        try
        {
            var stored = await SecureStorage.GetAsync(storageKey);
            if (!string.IsNullOrEmpty(stored))
                return Convert.FromBase64String(stored);
        }
        catch { /* SecureStorage unavailable — generate key without persisting */ }

        var key = RandomNumberGenerator.GetBytes(32);
        try { await SecureStorage.SetAsync(storageKey, Convert.ToBase64String(key)); }
        catch { /* Persist failed — key regenerated next run */ }
        return key;
    }
}
