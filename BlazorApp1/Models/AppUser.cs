namespace BlazorApp1.Models;

/// <summary>Satu pengguna aplikasi (disimpan di SQLite).</summary>
public class AppUser
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Username { get; set; } = "";
    public string Status { get; set; } = "Active";   // Active|Inactive|Banned|Pending|Suspended
    public string Role { get; set; } = "User";        // Admin|User|Guest|Moderator
    public DateTime JoinedDate { get; set; } = DateTime.Today;
    public DateTime LastActive { get; set; } = DateTime.Now;

    /// <summary>Inisial untuk avatar (mis. "John Smith" -> "JS").</summary>
    public string Initials
    {
        get
        {
            var parts = FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            if (parts.Length == 1) return parts[0][..1].ToUpperInvariant();
            return (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
        }
    }
}
