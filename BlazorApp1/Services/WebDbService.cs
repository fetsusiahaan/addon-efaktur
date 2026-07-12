using System.Data;
using System.Text.Json;
using BlazorApp1.Models;
using Microsoft.Data.SqlClient;

namespace BlazorApp1.Services;

/// <summary>
/// Akses database aplikasi web (SAP_WEB, SQL Server). Menyimpan transaksi
/// Pajak Keluaran dengan memanggil Stored Procedure dbo.usp_InsertPajakKeluaran
/// (header + detail via Table-Valued Parameter). Connection string dibaca
/// dinamis dari WebDbConfig.json.
/// </summary>
public class WebDbService
{
    private readonly string _configPath;

    public WebDbService(IWebHostEnvironment env)
    {
        _configPath = Path.Combine(env.ContentRootPath, "WebDbConfig.json");
    }

    /// <summary>
    /// Jalankan SELECT apa pun ke SAP_WEB dan kembalikan kolom+baris (dinamis).
    /// Dipakai mis. halaman Daftar Pajak Keluaran.
    /// </summary>
    public async Task<QueryResult> RunQueryAsync(string sql, CancellationToken ct = default)
    {
        var result = new QueryResult();

        var cs = GetConnectionString();
        if (string.IsNullOrWhiteSpace(cs))
        {
            result.Error = "Konfigurasi koneksi (WebDbConfig.json) belum diisi.";
            return result;
        }

        try
        {
            await using var conn = new SqlConnection(cs);
            try { await conn.OpenAsync(ct); }
            catch { result.Error = "SAP_WEB - 500 (Internal Server Error)"; return result; }

            await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            await using var reader = await cmd.ExecuteReaderAsync(ct);

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

    /// <summary>Baca connection string dari WebDbConfig.json (dinamis).</summary>
    private string? GetConnectionString()
    {
        try
        {
            if (!File.Exists(_configPath)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(_configPath));
            return doc.RootElement.TryGetProperty("ConnectionString", out var cs) ? cs.GetString() : null;
        }
        catch { return null; }
    }

    /// <summary>
    /// Simpan transaksi Pajak Keluaran lewat SP: header + N detail dikirim
    /// sekaligus sebagai TVP. Seluruh proses (generate DocEntry, insert header,
    /// insert detail, transaksi) berada di dalam Stored Procedure.
    /// </summary>
    public async Task<(bool Ok, string? Error, int DocEntry)> SavePajakKeluaranAsync(
        PajakKeluaranHeader header, IReadOnlyList<FakturKeluaranRow> details, CancellationToken ct = default)
    {
        var cs = GetConnectionString();
        if (string.IsNullOrWhiteSpace(cs))
            return (false, "Konfigurasi koneksi (WebDbConfig.json) belum diisi.", 0);
        if (details.Count == 0)
            return (false, "Tidak ada baris untuk disimpan.", 0);

        try
        {
            var tvp = BuildDetailTable(details);

            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync(ct);

            await using var cmd = new SqlCommand("dbo.usp_InsertPajakKeluaran", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@Search", (object?)header.Search ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Format", (object?)header.Format ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FNum", (object?)header.FromNum ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TNum", (object?)header.ToNum ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FEntry", (object?)header.FromEntry ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TEntry", (object?)header.ToEntry ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FrCust", (object?)header.FromCust ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ToCust", (object?)header.ToCust ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FrDate", (object?)header.FromDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ToDate", (object?)header.ToDate ?? DBNull.Value);

            // Table-Valued Parameter untuk baris detail.
            var pDetails = cmd.Parameters.AddWithValue("@Details", tvp);
            pDetails.SqlDbType = SqlDbType.Structured;
            pDetails.TypeName = "dbo.PajakKeluaranDetailType";

            var pDocEntry = new SqlParameter("@DocEntry", SqlDbType.Int) { Direction = ParameterDirection.Output };
            cmd.Parameters.Add(pDocEntry);

            await cmd.ExecuteNonQueryAsync(ct);

            var docEntry = pDocEntry.Value is int de ? de : 0;
            return (true, null, docEntry);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, 0);
        }
    }

    /// <summary>
    /// Ambil kunci invoice yang SUDAH difaktur (ada di IDU_PAJAK_TRANS_DET dengan
    /// U_Status='Y'), sebagai pasangan (U_EntryINV, U_DocType). Dipakai untuk
    /// mengecualikan invoice tsb dari hasil Load Data.
    /// Bila SAP_WEB tak terjangkau -> kembalikan set kosong (tanpa filter).
    /// </summary>
    public async Task<HashSet<(int EntryINV, string DocType)>> GetProcessedInvoiceKeysAsync(CancellationToken ct = default)
    {
        var set = new HashSet<(int, string)>();
        var cs = GetConnectionString();
        if (string.IsNullOrWhiteSpace(cs)) return set;

        try
        {
            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync(ct);
            // Hanya invoice yang difaktur (U_Status='Y') pada transaksi yang
            // header-nya TIDAK dibatalkan. Bila header Canceled='Y', invoice
            // tidak dikecualikan (tetap muncul di grid).
            await using var cmd = new SqlCommand(@"
                SELECT d.U_EntryINV, d.U_DocType
                FROM IDU_PAJAK_TRANS_DET d
                JOIN IDU_PAJAK_TRANS h ON d.DocEntry = h.DocEntry
                WHERE d.U_Status = 'Y' AND ISNULL(h.Canceled, 'N') <> 'Y'", conn);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                if (r.IsDBNull(0)) continue;
                var entry = r.GetInt32(0);
                var docType = r.IsDBNull(1) ? "" : r.GetValue(1)?.ToString() ?? "";
                set.Add((entry, docType));
            }
        }
        catch { /* SAP_WEB tak terjangkau -> tanpa filter */ }

        return set;
    }

    // Bangun DataTable sesuai urutan kolom dbo.PajakKeluaranDetailType.
    private static DataTable BuildDetailTable(IReadOnlyList<FakturKeluaranRow> details)
    {
        var dt = new DataTable();
        dt.Columns.Add("U_EntryINV", typeof(int));
        dt.Columns.Add("U_NumINV", typeof(string));
        dt.Columns.Add("U_DateINV", typeof(DateTime));
        dt.Columns.Add("U_BPCode", typeof(string));
        dt.Columns.Add("U_DocType", typeof(string));
        dt.Columns.Add("U_DTypeN", typeof(string));
        dt.Columns.Add("U_NoFP", typeof(string));
        dt.Columns.Add("U_BPName", typeof(string));
        dt.Columns.Add("U_DocTotal", typeof(decimal));
        dt.Columns.Add("U_VatSum", typeof(decimal));
        dt.Columns.Add("U_Discount", typeof(decimal));
        dt.Columns.Add("U_DP", typeof(decimal));
        dt.Columns.Add("U_JDP", typeof(string));
        dt.Columns.Add("U_Status", typeof(string));
        dt.Columns.Add("U_IDU_Pengganti", typeof(string));
        dt.Columns.Add("U_IDU_KetTambah", typeof(string));
        dt.Columns.Add("U_IDU_Referensi", typeof(string));
        dt.Columns.Add("U_IDU_JenisPajak", typeof(string));
        dt.Columns.Add("U_IDU_NamaNPWP", typeof(string));
        dt.Columns.Add("U_IDU_AlmtNPWP", typeof(string));
        dt.Columns.Add("U_IDU_NPWP", typeof(string));
        dt.Columns.Add("U_IDU_NIK", typeof(string));
        dt.Columns.Add("U_IDU_KDP", typeof(string));
        dt.Columns.Add("U_IDU_RatePajak", typeof(string));

        foreach (var r in details)
        {
            dt.Rows.Add(
                r.DocEntry,
                r.DocNum.ToString(),
                (object?)r.DocDate ?? DBNull.Value,
                (object?)r.CardCode ?? DBNull.Value,
                r.ObjType.ToString(),
                (object?)r.ObjName ?? DBNull.Value,
                (object?)r.NoFP ?? DBNull.Value,
                (object?)r.CardName ?? DBNull.Value,
                r.DocTotal,
                r.VatSum,
                r.Discount,
                r.DP,
                (object?)r.JDP ?? DBNull.Value,
                (object?)r.Status ?? DBNull.Value,
                (object?)r.Pengganti ?? DBNull.Value,
                (object?)r.KetTambah ?? DBNull.Value,
                (object?)r.Referensi ?? DBNull.Value,
                (object?)r.JenisPajak ?? DBNull.Value,
                (object?)r.NamaNPWP ?? DBNull.Value,
                (object?)r.AlamatNPWP ?? DBNull.Value,
                (object?)r.NPWP ?? DBNull.Value,
                (object?)r.NIK ?? DBNull.Value,
                (object?)r.KDP ?? DBNull.Value,
                (object?)r.RatePajak ?? DBNull.Value);
        }
        return dt;
    }
}
