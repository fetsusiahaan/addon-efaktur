using System.Data.Common;
using System.Text;
using Microsoft.Data.SqlClient;
using Sap.Data.Hana;

namespace BlazorApp1.Services;

/// <summary>Hasil query: nama kolom + baris, atau pesan error.</summary>
public class QueryResult
{
    public List<string> Columns { get; set; } = new();
    public List<object?[]> Rows { get; set; } = new();
    public string? Error { get; set; }
}

/// <summary>
/// Menjalankan SELECT apa pun ke koneksi database yang sedang AKTIF di
/// Database Manager (SQL Server SAP B1 atau SAP HANA) dan mengembalikan
/// kolom secara dinamis sesuai hasil query.
/// </summary>
public class SapQueryService
{
    private readonly DbManagerService _dbMgr;

    public SapQueryService(DbManagerService dbMgr) => _dbMgr = dbMgr;

    /// <summary>
    /// Jalankan query ke koneksi yang sedang aktif. Service ini yang
    /// mengidentifikasi jenis DB koneksi aktif (SQL Server / SAP HANA) dan
    /// memilih provider yang sesuai. Tulis query dengan identifier
    /// ber-tanda-kutip ganda (mis. SELECT "DocEntry" FROM "OINV") agar valid
    /// di kedua DB — wajib untuk HANA, dan tetap diterima SQL Server.
    /// </summary>
    public async Task<QueryResult> RunQueryAsync(string sql, CancellationToken ct = default)
    {
        var result = new QueryResult();

        if (string.IsNullOrWhiteSpace(sql))
        {
            result.Error = "Query kosong.";
            return result;
        }

        var active = _dbMgr.GetActive();
        if (active is null)
        {
            result.Error = "Belum ada koneksi aktif. Buka Database Manager lalu klik 'Set Aktif' pada salah satu koneksi.";
            return result;
        }

        if (string.IsNullOrWhiteSpace(active.ConnString))
        {
            result.Error = $"Connection string untuk koneksi aktif '{active.Nama}' masih kosong.";
            return result;
        }

        var isHana = active.JenisDb == "SAP HANA Database";

        // HANA case-sensitive: identifier polos harus dikutip dengan huruf
        // persis. SQL Server tidak perlu diubah.
        var effectiveSql = isHana ? QuoteIdentifiersForHana(sql) : sql;

        try
        {
            // Pilih provider sesuai jenis koneksi aktif. Keduanya turunan
            // DbConnection sehingga sisa kodenya sama (provider-agnostic).
            await using DbConnection conn = isHana
                ? new HanaConnection(active.ConnString)
                : (DbConnection)new SqlConnection(active.ConnString);

            try
            {
                await conn.OpenAsync(ct);
            }
            catch
            {
                // Server DB tidak dapat dihubungi (mati / host tak dikenal / timeout).
                result.Error = $"{active.Nama} - 500 (Internal Server Error)";
                return result;
            }

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = effectiveSql;
            cmd.CommandTimeout = 60;

            await using var reader = await cmd.ExecuteReaderAsync(ct);

            // Kolom diambil langsung dari metadata hasil query (dinamis).
            for (int i = 0; i < reader.FieldCount; i++)
                result.Columns.Add(reader.GetName(i));

            while (await reader.ReadAsync(ct))
            {
                var row = new object?[reader.FieldCount];
                for (int i = 0; i < reader.FieldCount; i++)
                    row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                result.Rows.Add(row);
            }
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    // Kata kunci SQL yang TIDAK boleh dikutip sebagai identifier.
    private static readonly HashSet<string> SqlKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "select", "from", "where", "and", "or", "not", "null", "is", "in", "like", "between",
        "order", "by", "group", "having", "join", "inner", "left", "right", "outer", "full",
        "cross", "on", "as", "asc", "desc", "distinct", "top", "union", "all", "case", "when",
        "then", "else", "end", "exists", "into", "values", "limit", "offset", "with", "over",
        "partition", "true", "false", "escape",
    };

    /// <summary>
    /// Membungkus identifier polos dengan tanda kutip ganda agar cocok dengan
    /// aturan case-sensitive SAP HANA (mis. OINV → "OINV", DocEntry → "DocEntry").
    /// Melewati kata kunci SQL, nama fungsi (diikuti '('), literal string '...',
    /// dan identifier yang sudah dikutip "...".
    /// </summary>
    private static string QuoteIdentifiersForHana(string sql)
    {
        var sb = new StringBuilder(sql.Length + 16);
        int i = 0;

        while (i < sql.Length)
        {
            char c = sql[i];

            // Literal string '...': salin apa adanya (termasuk '' escape).
            if (c == '\'')
            {
                int start = i++;
                while (i < sql.Length)
                {
                    if (sql[i] == '\'')
                    {
                        if (i + 1 < sql.Length && sql[i + 1] == '\'') { i += 2; continue; }
                        i++;
                        break;
                    }
                    i++;
                }
                sb.Append(sql, start, i - start);
                continue;
            }

            // Identifier yang sudah dikutip "...": salin apa adanya.
            if (c == '"')
            {
                int start = i++;
                while (i < sql.Length && sql[i] != '"') i++;
                if (i < sql.Length) i++; // kutip penutup
                sb.Append(sql, start, i - start);
                continue;
            }

            // Kata (identifier / keyword / nama fungsi).
            if (char.IsLetter(c) || c == '_')
            {
                int start = i;
                while (i < sql.Length && (char.IsLetterOrDigit(sql[i]) || sql[i] == '_')) i++;
                var word = sql.Substring(start, i - start);

                // Fungsi bila token berikutnya (abaikan spasi) adalah '('.
                int j = i;
                while (j < sql.Length && char.IsWhiteSpace(sql[j])) j++;
                bool isFunction = j < sql.Length && sql[j] == '(';

                if (SqlKeywords.Contains(word) || isFunction)
                    sb.Append(word);
                else
                    sb.Append('"').Append(word).Append('"');
                continue;
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }
}
