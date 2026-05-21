using SQLite;

namespace TolllgaFinale.Models;

public class AppUser
{
    [PrimaryKey, AutoIncrement]
    public int    Id           { get; set; }
    public string Username     { get; set; } = "";
    public string PasswordHash { get; set; } = "";   // SHA-256 hex
    public string Role         { get; set; } = "operateur"; // "admin" | "operateur"
    public bool   IsActive     { get; set; } = true;
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
}
