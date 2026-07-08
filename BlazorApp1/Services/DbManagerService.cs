using System.Text.Json;
using System.Text.Json.Nodes;
using BlazorApp1.Models;
using Microsoft.Data.SqlClient;
using Sap.Data.Hana;

namespace BlazorApp1.Services;

/// <summary>
/// Mengelola daftar koneksi database yang disimpan di appsettings.json
/// di bawah key "DatabaseConnections".
/// </summary>
public class DbManagerService
{
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = true };
    private const string Section = "DatabaseConnections";

    // Sinkronkan baca/tulis file lintas sirkuit (mis. polling DbStatus vs
    // Simpan/Set Aktif di Database Manager) agar tidak membaca file setengah tertulis.
    private static readonly object _fileLock = new();

    public DbManagerService(IWebHostEnvironment env)
    {
        // appsettings.json ada di root project (ContentRootPath)
        _settingsPath = Path.Combine(env.ContentRootPath, "ConnectDBAccess.json");
    }

    // ── READ ─────────────────────────────────────────────────────────────────

    public List<DbConnectionEntry> GetAll()
    {
        var root = ReadRoot();
        if (root[Section] is not JsonArray arr) return new();

        return arr
            .Select(n => n.Deserialize<DbConnectionEntry>(_jsonOpts))
            .Where(x => x is not null)
            .Cast<DbConnectionEntry>()
            .ToList();
    }

    /// <summary>Koneksi yang sedang ditandai aktif, atau null bila belum ada.</summary>
    public DbConnectionEntry? GetActive() => GetAll().FirstOrDefault(x => x.IsActive);

    // ── WRITE ─────────────────────────────────────────────────────────────────

    public void Save(DbConnectionEntry entry)
    {
        var root = ReadRoot();
        var arr = EnsureArray(root);

        // Update jika Id sudah ada, tambah jika baru
        var existing = arr.FirstOrDefault(n => n?["Id"]?.GetValue<string>() == entry.Id);
        if (existing is not null)
            arr.Remove(existing);

        arr.Add(JsonNode.Parse(JsonSerializer.Serialize(entry, _jsonOpts))!);
        WriteRoot(root);
    }

    public void Delete(string id)
    {
        var root = ReadRoot();
        var arr = EnsureArray(root);
        var node = arr.FirstOrDefault(n => n?["Id"]?.GetValue<string>() == id);
        if (node is not null) arr.Remove(node);
        WriteRoot(root);
    }

    /// <summary>Tandai satu Id sebagai aktif, sisanya false.</summary>
    public void SetActive(string id)
    {
        var root = ReadRoot();
        var arr = EnsureArray(root);

        foreach (var node in arr)
        {
            if (node is JsonObject obj)
                obj["IsActive"] = node["Id"]?.GetValue<string>() == id;
        }
        WriteRoot(root);
    }

    // ── EXPORT / IMPORT ───────────────────────────────────────────────────────

    /// <summary>Kembalikan JSON string dari seluruh koneksi (untuk di-download).</summary>
    public string Export()
    {
        var list = GetAll();
        return JsonSerializer.Serialize(list, _jsonOpts);
    }

    /// <summary>Merge koneksi dari JSON string ke daftar yang ada (skip duplikat Id).</summary>
    public (int Added, int Skipped) Import(string json)
    {
        List<DbConnectionEntry> incoming;
        try
        {
            incoming = JsonSerializer.Deserialize<List<DbConnectionEntry>>(json, _jsonOpts)
                       ?? new();
        }
        catch
        {
            return (0, 0);
        }

        var existing = GetAll();
        var existingIds = existing.Select(x => x.Id).ToHashSet();

        int added = 0, skipped = 0;
        foreach (var entry in incoming)
        {
            if (existingIds.Contains(entry.Id)) { skipped++; continue; }
            entry.IsActive = false; // import tidak otomatis aktif
            Save(entry);
            added++;
        }
        return (added, skipped);
    }

    // ── TEST KONEKSI ──────────────────────────────────────────────────────────

    public async Task<(bool Ok, string? Error)> TestAsync(DbConnectionEntry entry, TimeSpan? timeout = null)
    {
        if (string.IsNullOrWhiteSpace(entry.ConnString))
            return (false, "Connection string kosong.");

        var limit = timeout ?? TimeSpan.FromSeconds(30);
        var isHana = entry.JenisDb == "SAP HANA Database";

        try
        {
            // Pooling dimatikan agar tiap cek benar-benar konek baru ke server
            // (bukan mengambil koneksi lama dari pool yang tak tervalidasi).
            System.Data.Common.DbConnection conn = isHana
                ? new HanaConnection(DisableHanaPooling(entry.ConnString))
                : new SqlConnection(ForHealthCheck(entry.ConnString, limit));

            // Probe query memaksa round-trip nyata ke server; kalau jaringan
            // putus (mis. VPN mati), ini gagal walau OpenAsync sempat lolos.
            var probe = isHana ? "SELECT 1 FROM DUMMY" : "SELECT 1";

            // Task terpisah yang MEMILIKI + membuang koneksi dan tak pernah
            // melempar — aman ditinggalkan bila timeout menang.
            var openTask = OpenAndValidateAsync(conn, probe, limit);

            // Balapkan dengan timeout: jaminan tak menggantung bila driver
            // mengabaikan cancellation — status langsung "terputus".
            if (await Task.WhenAny(openTask, Task.Delay(limit)) != openTask)
                return (false, $"Timeout {limit.TotalSeconds:0} detik: server tidak merespons.");

            return await openTask;
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>Buka koneksi + jalankan probe query, lalu tutup; tak pernah melempar.</summary>
    private static async Task<(bool Ok, string? Error)> OpenAndValidateAsync(
        System.Data.Common.DbConnection conn, string probeSql, TimeSpan limit)
    {
        try
        {
            await using (conn)
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = probeSql;
                cmd.CommandTimeout = Math.Max(1, (int)limit.TotalSeconds);
                await cmd.ExecuteScalarAsync();
                return (true, null);
            }
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>Connect Timeout pendek + pooling mati untuk SQL Server.</summary>
    private static string ForHealthCheck(string cs, TimeSpan limit)
    {
        try
        {
            return new SqlConnectionStringBuilder(cs)
            {
                ConnectTimeout = Math.Max(1, (int)limit.TotalSeconds),
                Pooling = false
            }.ConnectionString;
        }
        catch
        {
            return cs;
        }
    }

    /// <summary>Matikan pooling pada connection string HANA (bila belum diset).</summary>
    private static string DisableHanaPooling(string cs)
    {
        if (cs.Contains("pooling", StringComparison.OrdinalIgnoreCase))
            return cs;
        return cs.TrimEnd(';', ' ') + ";POOLING=FALSE";
    }

    // ── HELPERS ───────────────────────────────────────────────────────────────

    private JsonObject ReadRoot()
    {
        lock (_fileLock)
        {
            try
            {
                if (!File.Exists(_settingsPath))
                    return new JsonObject();
                var text = File.ReadAllText(_settingsPath);
                if (string.IsNullOrWhiteSpace(text))
                    return new JsonObject();
                return JsonNode.Parse(text)?.AsObject() ?? new JsonObject();
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                // File sedang ditulis proses lain / rusak sementara -> jangan
                // sampai melempar (bisa mematikan sirkuit). Anggap kosong.
                return new JsonObject();
            }
        }
    }

    private void WriteRoot(JsonObject root)
    {
        // Lock men-serialkan tulis vs baca dalam proses, jadi cukup tulis biasa
        // (File.Replace bisa melempar bila file sempat dipegang handle lain).
        lock (_fileLock)
        {
            File.WriteAllText(_settingsPath, root.ToJsonString(_jsonOpts));
        }
    }

    private static JsonArray EnsureArray(JsonObject root)
    {
        if (root[Section] is not JsonArray arr)
        {
            arr = new JsonArray();
            root[Section] = arr;
        }
        return arr;
    }
}
