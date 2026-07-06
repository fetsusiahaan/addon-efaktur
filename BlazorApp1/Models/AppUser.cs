using System.Globalization;

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

    /// <summary>Nama koneksi DB terpilih (dari Database Manager).</summary>
    public string? KoneksiDb { get; set; }

    /// <summary>Password login. Default untuk semua user baru = "Password#01".</summary>
    public string Password { get; set; } = "Password#01";

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

    private static readonly string[] AvatarColors =
        { "#5b9bd5", "#8a6db1", "#57a773", "#d98b41", "#c15b6b", "#3f7cac", "#7b6cc4" };

    /// <summary>Warna avatar konsisten berdasar nama.</summary>
    public string AvatarColor
    {
        get
        {
            int h = 0;
            foreach (var c in FullName) h = h * 31 + c;
            return AvatarColors[Math.Abs(h) % AvatarColors.Length];
        }
    }

    /// <summary>Tanggal bergabung terformat, mis. "March 12, 2023".</summary>
    public string JoinedDateText => JoinedDate.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);

    /// <summary>Waktu aktif terakhir relatif, mis. "5 minutes ago".</summary>
    public string LastActiveRelative
    {
        get
        {
            if (LastActive == DateTime.MinValue) return "-";
            var span = DateTime.Now - LastActive;
            if (span.TotalSeconds < 60) return $"{P((int)span.TotalSeconds, "second")} ago";
            if (span.TotalMinutes < 60) return $"{P((int)span.TotalMinutes, "minute")} ago";
            if (span.TotalHours < 24) return $"{P((int)span.TotalHours, "hour")} ago";
            if (span.TotalDays < 7) return $"{P((int)span.TotalDays, "day")} ago";
            if (span.TotalDays < 30) return $"{P((int)(span.TotalDays / 7), "week")} ago";
            if (span.TotalDays < 365) return $"{P((int)(span.TotalDays / 30), "month")} ago";
            return $"{P((int)(span.TotalDays / 365), "year")} ago";
        }
    }

    private static string P(int n, string unit) => $"{n} {unit}{(n == 1 ? "" : "s")}";
}
