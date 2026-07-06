using System.Security.Cryptography;
using BlazorApp1.Models;
using Microsoft.Data.Sqlite;

namespace BlazorApp1.Services;

/// <summary>
/// CRUD pengguna aplikasi dengan penyimpanan SQLite (file users.db di ContentRoot).
/// Tabel dibuat & di-seed otomatis saat pertama kali dipakai.
/// </summary>
public class UserService
{
    /// <summary>Password default untuk semua user.</summary>
    public const string DefaultPassword = "Password#01";

    private readonly string _connString;
    private bool _initialized;
    private readonly object _lock = new();

    public UserService(IWebHostEnvironment env)
    {
        var dbPath = Path.Combine(env.ContentRootPath, "users.db");
        _connString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ConnectionString;
    }

    // ── Inisialisasi & seed ────────────────────────────────────────────────
    private void EnsureInit()
    {
        if (_initialized) return;
        lock (_lock)
        {
            if (_initialized) return;

            using var conn = new SqliteConnection(_connString);
            conn.Open();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Users (
                        Id         INTEGER PRIMARY KEY AUTOINCREMENT,
                        FullName   TEXT NOT NULL,
                        Email      TEXT,
                        Username   TEXT,
                        Status     TEXT,
                        Role       TEXT,
                        JoinedDate TEXT,
                        LastActive TEXT,
                        KoneksiDb  TEXT,
                        Password   TEXT
                    );";
                cmd.ExecuteNonQuery();
            }

            // Migrasi: tambah kolom KoneksiDb bila DB lama belum punya.
            if (!ColumnExists(conn, "Users", "KoneksiDb"))
            {
                using var alter = conn.CreateCommand();
                alter.CommandText = "ALTER TABLE Users ADD COLUMN KoneksiDb TEXT;";
                alter.ExecuteNonQuery();
            }

            // Migrasi: tambah kolom Password + isi default untuk baris lama.
            if (!ColumnExists(conn, "Users", "Password"))
            {
                using var alter = conn.CreateCommand();
                alter.CommandText = "ALTER TABLE Users ADD COLUMN Password TEXT;";
                alter.ExecuteNonQuery();
            }
            // Migrasi keamanan: pastikan semua password tersimpan dalam bentuk hash.
            // - kosong/null  -> hash dari DefaultPassword
            // - plaintext lama -> di-hash apa adanya (login lama tetap berlaku)
            var toHash = new List<(int Id, string Hash)>();
            using (var scan = conn.CreateCommand())
            {
                scan.CommandText = "SELECT Id, Password FROM Users;";
                using var sr = scan.ExecuteReader();
                while (sr.Read())
                {
                    var id = sr.GetInt32(0);
                    var pwd = sr.IsDBNull(1) ? "" : sr.GetString(1);
                    if (string.IsNullOrEmpty(pwd))
                        toHash.Add((id, HashPassword(DefaultPassword)));
                    else if (!LooksHashed(pwd))
                        toHash.Add((id, HashPassword(pwd)));
                }
            }
            foreach (var (id, hash) in toHash)
            {
                using var upd = conn.CreateCommand();
                upd.CommandText = "UPDATE Users SET Password=$p WHERE Id=$id;";
                upd.Parameters.AddWithValue("$p", hash);
                upd.Parameters.AddWithValue("$id", id);
                upd.ExecuteNonQuery();
            }

            // Jamin keunikan Username (case-insensitive) di level DB.
            using (var ix = conn.CreateCommand())
            {
                ix.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS UX_Users_Username ON Users(Username COLLATE NOCASE);";
                ix.ExecuteNonQuery();
            }

            using (var check = conn.CreateCommand())
            {
                check.CommandText = "SELECT COUNT(*) FROM Users;";
                var count = Convert.ToInt64(check.ExecuteScalar());
                if (count == 0) Seed(conn);
            }

            _initialized = true;
        }
    }

    private static void Seed(SqliteConnection conn)
    {
        var now = DateTime.Now;
        var seed = new (string Full, string Email, string User, string Status, string Role, DateTime Joined, DateTime Last)[]
        {
            ("John Smith",     "john.smith@gmail.com", "jonny77",    "Active",    "Admin",     new(2023,3,12),  now.AddMinutes(-1)),
            ("Olivia Bennett", "ollyben@gmail.com",    "olly659",    "Inactive",  "User",      new(2022,6,27),  now.AddMonths(-1)),
            ("Daniel Warren",  "dwarren3@gmail.com",   "dwarren3",   "Banned",    "User",      new(2024,1,8),   now.AddDays(-4)),
            ("Chloe Hayes",    "chloehhye@gmail.com",  "chloehh",    "Pending",   "Guest",     new(2021,10,5),  now.AddDays(-10)),
            ("Marcus Reed",    "reeds777@gmail.com",   "reeds7",     "Suspended", "User",      new(2023,2,19),  now.AddMonths(-3)),
            ("Isabelle Clark", "belleclark@gmail.com", "bellecl",    "Active",    "Moderator", new(2022,8,30),  now.AddDays(-7)),
            ("Lucas Mitchell", "lucamich@gmail.com",   "lucamich",   "Active",    "Guest",     new(2024,4,23),  now.AddHours(-4)),
            ("Mark Wilburg",   "markwill32@gmail.com", "markwill32", "Banned",    "User",      new(2020,11,14), now.AddMonths(-2)),
            ("Nicholas Agenn", "nicolass009@gmail.com","nicolass009","Suspended", "User",      new(2023,7,6),   now.AddHours(-3)),
            ("Mia Nadinn",     "mianaddiin@gmail.com", "mianaddiin", "Inactive",  "Guest",     new(2021,12,31), now.AddMonths(-4)),
            ("Noemi Villan",   "noemivill99@gmail.com","noemi",      "Active",    "Admin",     new(2024,8,10),  now.AddMinutes(-15)),
            ("Ethan Brooks",   "ethanb@gmail.com",     "ethanb",     "Active",    "User",      new(2023,5,2),   now.AddHours(-8)),
            ("Sophia Turner",  "sophiat@gmail.com",    "sophiat",    "Pending",   "Guest",     new(2022,3,17),  now.AddDays(-2)),
            ("James Carter",   "jamesc@gmail.com",     "jamesc",     "Active",    "Moderator", new(2021,9,9),   now.AddDays(-1)),
            ("Ava Robinson",   "avar@gmail.com",       "avar",       "Suspended", "User",      new(2024,2,28),  now.AddMonths(-5)),
        };

        foreach (var s in seed)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO Users (FullName,Email,Username,Status,Role,JoinedDate,LastActive,Password)
                                VALUES ($f,$e,$u,$s,$r,$j,$l,$p);";
            cmd.Parameters.AddWithValue("$f", s.Full);
            cmd.Parameters.AddWithValue("$e", s.Email);
            cmd.Parameters.AddWithValue("$u", s.User);
            cmd.Parameters.AddWithValue("$s", s.Status);
            cmd.Parameters.AddWithValue("$r", s.Role);
            cmd.Parameters.AddWithValue("$j", s.Joined.ToString("o"));
            cmd.Parameters.AddWithValue("$l", s.Last.ToString("o"));
            cmd.Parameters.AddWithValue("$p", HashPassword(DefaultPassword));
            cmd.ExecuteNonQuery();
        }
    }

    // ── READ ───────────────────────────────────────────────────────────────
    public List<AppUser> GetAll()
    {
        EnsureInit();
        var list = new List<AppUser>();
        using var conn = new SqliteConnection(_connString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        // Password (hash) sengaja TIDAK dimuat ke model UI.
        cmd.CommandText = "SELECT Id,FullName,Email,Username,Status,Role,JoinedDate,LastActive,KoneksiDb FROM Users ORDER BY Id;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new AppUser
            {
                Id         = r.GetInt32(0),
                FullName   = r.IsDBNull(1) ? "" : r.GetString(1),
                Email      = r.IsDBNull(2) ? "" : r.GetString(2),
                Username   = r.IsDBNull(3) ? "" : r.GetString(3),
                Status     = r.IsDBNull(4) ? "Active" : r.GetString(4),
                Role       = r.IsDBNull(5) ? "User" : r.GetString(5),
                JoinedDate = ParseDate(r, 6),
                LastActive = ParseDate(r, 7),
                KoneksiDb  = r.IsDBNull(8) ? null : r.GetString(8),
            });
        }
        return list;
    }

    private static bool ColumnExists(SqliteConnection conn, string table, string column)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table});";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            if (string.Equals(r.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static DateTime ParseDate(SqliteDataReader r, int i) =>
        r.IsDBNull(i) ? DateTime.MinValue :
        DateTime.TryParse(r.GetString(i), null, System.Globalization.DateTimeStyles.RoundtripKind, out var d) ? d : DateTime.MinValue;

    // ── WRITE ──────────────────────────────────────────────────────────────
    public void Add(AppUser u)
    {
        EnsureInit();
        using var conn = new SqliteConnection(_connString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        // User baru selalu memakai password default (di-hash).
        cmd.CommandText = @"INSERT INTO Users (FullName,Email,Username,Status,Role,JoinedDate,LastActive,KoneksiDb,Password)
                            VALUES ($f,$e,$u,$s,$r,$j,$l,$k,$p);";
        Bind(cmd, u);
        cmd.Parameters.AddWithValue("$p", HashPassword(DefaultPassword));
        cmd.ExecuteNonQuery();
    }

    public void Update(AppUser u)
    {
        EnsureInit();
        using var conn = new SqliteConnection(_connString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        // Username & Password sengaja TIDAK di-update di sini
        // (username tidak boleh diubah; password dikelola terpisah).
        cmd.CommandText = @"UPDATE Users SET FullName=$f,Email=$e,Status=$s,Role=$r,
                            JoinedDate=$j,LastActive=$l,KoneksiDb=$k WHERE Id=$id;";
        Bind(cmd, u);
        cmd.Parameters.AddWithValue("$id", u.Id);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        EnsureInit();
        using var conn = new SqliteConnection(_connString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Users WHERE Id=$id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    private static void Bind(SqliteCommand cmd, AppUser u)
    {
        cmd.Parameters.AddWithValue("$f", u.FullName);
        cmd.Parameters.AddWithValue("$e", u.Email);
        cmd.Parameters.AddWithValue("$u", u.Username);
        cmd.Parameters.AddWithValue("$s", u.Status);
        cmd.Parameters.AddWithValue("$r", u.Role);
        cmd.Parameters.AddWithValue("$j", u.JoinedDate.ToString("o"));
        cmd.Parameters.AddWithValue("$l", u.LastActive.ToString("o"));
        cmd.Parameters.AddWithValue("$k", (object?)u.KoneksiDb ?? DBNull.Value);
    }

    /// <summary>Ambil nilai Koneksi DB terkini milik user (real-time dari tabel), null bila tak ada.</summary>
    public string? GetKoneksiDb(int userId)
    {
        EnsureInit();
        using var conn = new SqliteConnection(_connString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT KoneksiDb FROM Users WHERE Id=$id LIMIT 1;";
        cmd.Parameters.AddWithValue("$id", userId);
        var v = cmd.ExecuteScalar();
        return v is null or DBNull ? null : v.ToString();
    }

    /// <summary>True bila username sudah dipakai (case-insensitive), abaikan baris excludeId.</summary>
    public bool UsernameExists(string username, int excludeId = 0)
    {
        EnsureInit();
        if (string.IsNullOrWhiteSpace(username)) return false;
        using var conn = new SqliteConnection(_connString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Users WHERE Username=$u COLLATE NOCASE AND Id<>$id;";
        cmd.Parameters.AddWithValue("$u", username.Trim());
        cmd.Parameters.AddWithValue("$id", excludeId);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    // ── Autentikasi ─────────────────────────────────────────────────────────

    /// <summary>
    /// Verifikasi username+password. Mengembalikan user bila valid, null bila tidak.
    /// Tahan timing attack (selalu jalankan verifikasi hash walau user tak ada).
    /// </summary>
    public AppUser? ValidateCredentials(string username, string password)
    {
        EnsureInit();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            VerifyPassword(DummyHash, password ?? "");   // samakan waktu proses
            return null;
        }

        using var conn = new SqliteConnection(_connString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT Id,FullName,Email,Username,Status,Role,JoinedDate,LastActive,KoneksiDb,Password
                            FROM Users WHERE Username=$u COLLATE NOCASE LIMIT 1;";
        cmd.Parameters.AddWithValue("$u", username.Trim());
        using var r = cmd.ExecuteReader();

        if (!r.Read())
        {
            VerifyPassword(DummyHash, password);         // hindari user enumeration via timing
            return null;
        }

        var storedHash = r.IsDBNull(9) ? "" : r.GetString(9);
        if (!VerifyPassword(storedHash, password)) return null;

        return new AppUser
        {
            Id         = r.GetInt32(0),
            FullName   = r.IsDBNull(1) ? "" : r.GetString(1),
            Email      = r.IsDBNull(2) ? "" : r.GetString(2),
            Username   = r.IsDBNull(3) ? "" : r.GetString(3),
            Status     = r.IsDBNull(4) ? "Active" : r.GetString(4),
            Role       = r.IsDBNull(5) ? "User" : r.GetString(5),
            JoinedDate = ParseDate(r, 6),
            LastActive = ParseDate(r, 7),
            KoneksiDb  = r.IsDBNull(8) ? null : r.GetString(8),
        };
    }

    // ── Hashing password (PBKDF2-SHA256) ────────────────────────────────────
    private const int Pbkdf2Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    // Hash "dummy" valid (untuk verifikasi konstan saat user tidak ditemukan).
    private static readonly string DummyHash = HashPassword("::dummy::");

    /// <summary>Hasilkan hash "iterasi.salt.hash" (semua base64).</summary>
    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"{Pbkdf2Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    /// <summary>Verifikasi password terhadap hash tersimpan (fixed-time compare).</summary>
    public static bool VerifyPassword(string stored, string password)
    {
        if (!LooksHashed(stored)) return false;
        var parts = stored.Split('.');
        if (!int.TryParse(parts[0], out var iter)) return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expected = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException) { return false; }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iter, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>True bila string berbentuk hash kita ("iter.salt.hash").</summary>
    private static bool LooksHashed(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        var parts = value.Split('.');
        return parts.Length == 3 && int.TryParse(parts[0], out _);
    }
}
