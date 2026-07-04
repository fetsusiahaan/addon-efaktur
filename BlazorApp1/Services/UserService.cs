using BlazorApp1.Models;
using Microsoft.Data.Sqlite;

namespace BlazorApp1.Services;

/// <summary>
/// CRUD pengguna aplikasi dengan penyimpanan SQLite (file users.db di ContentRoot).
/// Tabel dibuat & di-seed otomatis saat pertama kali dipakai.
/// </summary>
public class UserService
{
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
                        LastActive TEXT
                    );";
                cmd.ExecuteNonQuery();
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
            cmd.CommandText = @"INSERT INTO Users (FullName,Email,Username,Status,Role,JoinedDate,LastActive)
                                VALUES ($f,$e,$u,$s,$r,$j,$l);";
            cmd.Parameters.AddWithValue("$f", s.Full);
            cmd.Parameters.AddWithValue("$e", s.Email);
            cmd.Parameters.AddWithValue("$u", s.User);
            cmd.Parameters.AddWithValue("$s", s.Status);
            cmd.Parameters.AddWithValue("$r", s.Role);
            cmd.Parameters.AddWithValue("$j", s.Joined.ToString("o"));
            cmd.Parameters.AddWithValue("$l", s.Last.ToString("o"));
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
        cmd.CommandText = "SELECT Id,FullName,Email,Username,Status,Role,JoinedDate,LastActive FROM Users ORDER BY Id;";
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
            });
        }
        return list;
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
        cmd.CommandText = @"INSERT INTO Users (FullName,Email,Username,Status,Role,JoinedDate,LastActive)
                            VALUES ($f,$e,$u,$s,$r,$j,$l);";
        Bind(cmd, u);
        cmd.ExecuteNonQuery();
    }

    public void Update(AppUser u)
    {
        EnsureInit();
        using var conn = new SqliteConnection(_connString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"UPDATE Users SET FullName=$f,Email=$e,Username=$u,Status=$s,Role=$r,
                            JoinedDate=$j,LastActive=$l WHERE Id=$id;";
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
    }
}
