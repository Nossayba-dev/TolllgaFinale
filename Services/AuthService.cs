using System.Security.Cryptography;
using System.Text;
using SQLite;
using TolllgaFinale.Models;

namespace TolllgaFinale.Services;

/// <summary>
/// Manages users, sessions, and role checks.
/// Roles: "admin" (full access) | "operateur" (pesée only)
/// </summary>
public class AuthService
{
    private SQLiteAsyncConnection? _db;
    private AppUser? _currentUser;

    // ── Public state ──────────────────────────────────────────────────────────
    public AppUser? CurrentUser => _currentUser;
    public bool IsLoggedIn => _currentUser is not null;
    public bool IsAdmin => _currentUser?.Role == "admin";
    public bool IsOperateur => _currentUser?.Role == "operateur";
    public string UserRole => _currentUser?.Role ?? "";
    public string CurrentUserLabel =>
        string.IsNullOrWhiteSpace(_currentUser?.Username) ? "system" : _currentUser.Username;

    // Notify layout/shell when login state changes
    public event Action? OnAuthChanged;

    // ── DB path (shared with DatabaseService) ─────────────────────────────────
    private static string DbPath =>
        Path.Combine(FileSystem.AppDataDirectory, "weightsync.db3");

    // ── Init ──────────────────────────────────────────────────────────────────
    public async Task InitAsync()
    {
        if (_db is not null) return;
        _db = new SQLiteAsyncConnection(DbPath,
              SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
        await _db.CreateTableAsync<AppUser>();
        await SeedDefaultUsersAsync();
    }

    /// <summary>Creates default admin and operateur accounts if no users exist.</summary>
    private async Task SeedDefaultUsersAsync()
    {
        var count = await _db!.Table<AppUser>().CountAsync();
        if (count > 0) return;

        await _db.InsertAsync(new AppUser
        {
            Username = "admin",
            PasswordHash = Hash("admin123"),
            Role = "admin",
            IsActive = true
        });
        await _db.InsertAsync(new AppUser
        {
            Username = "operateur",
            PasswordHash = Hash("oper123"),
            Role = "operateur",
            IsActive = true
        });
    }

    // ── Login / Logout ────────────────────────────────────────────────────────
    public async Task<(bool success, string error)> LoginAsync(string username, string password)
    {
        await InitAsync();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return (false, "Veuillez remplir tous les champs.");

        // sqlite-net-pcl cannot translate .ToLower() to SQL — load all, filter in C#
        var normalised = username.Trim().ToLowerInvariant();
        var allUsers = await _db!.Table<AppUser>().ToListAsync();
        var user = allUsers.FirstOrDefault(
                             u => u.Username.ToLowerInvariant() == normalised);

        if (user is null)
            return (false, "Nom d'utilisateur introuvable.");

        if (!user.IsActive)
            return (false, "Ce compte est désactivé.");

        if (user.PasswordHash != Hash(password))
            return (false, "Mot de passe incorrect.");

        _currentUser = user;
        OnAuthChanged?.Invoke();
        return (true, "");
    }

    public void Logout()
    {
        _currentUser = null;
        OnAuthChanged?.Invoke();
    }

    // ── User management (admin only) ──────────────────────────────────────────
    public async Task<List<AppUser>> GetAllUsersAsync()
    {
        await InitAsync();
        return await _db!.Table<AppUser>().OrderBy(u => u.Username).ToListAsync();
    }

    public async Task<(bool ok, string error)> CreateUserAsync(
        string username, string password, string role)
    {
        await InitAsync();
        if (string.IsNullOrWhiteSpace(username)) return (false, "Nom d'utilisateur requis.");
        if (password.Length < 4) return (false, "Mot de passe trop court (min 4 caractères).");
        if (role != "admin" && role != "operateur") return (false, "Rôle invalide.");

        var exists = await _db!.Table<AppUser>()
            .Where(u => u.Username.ToLower() == username.ToLower().Trim())
            .FirstOrDefaultAsync();
        if (exists is not null) return (false, "Ce nom d'utilisateur existe déjà.");

        await _db.InsertAsync(new AppUser
        {
            Username = username.Trim(),
            PasswordHash = Hash(password),
            Role = role,
            IsActive = true
        });
        return (true, "");
    }

    public async Task ToggleUserActiveAsync(int id, bool isActive)
    {
        await InitAsync();
        var u = await _db!.FindAsync<AppUser>(id);
        if (u is null) return;
        u.IsActive = isActive;
        await _db.UpdateAsync(u);
    }

    public async Task<(bool ok, string error)> ChangePasswordAsync(
        int userId, string newPassword)
    {
        if (newPassword.Length < 4) return (false, "Mot de passe trop court (min 4 caractères).");
        await InitAsync();
        var u = await _db!.FindAsync<AppUser>(userId);
        if (u is null) return (false, "Utilisateur introuvable.");
        u.PasswordHash = Hash(newPassword);
        await _db!.UpdateAsync(u);
        return (true, "");
    }

    public async Task DeleteUserAsync(int id)
    {
        await InitAsync();
        // Prevent deleting the last admin
        var admins = await _db!.Table<AppUser>()
            .Where(u => u.Role == "admin" && u.IsActive).ToListAsync();
        var user = await _db.FindAsync<AppUser>(id);
        if (user?.Role == "admin" && admins.Count <= 1) return; // keep at least 1 admin
        await _db!.DeleteAsync<AppUser>(id);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    public static string Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLower();
    }
}