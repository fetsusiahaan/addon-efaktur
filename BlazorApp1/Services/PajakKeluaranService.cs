using BlazorApp1.Models;

namespace BlazorApp1.Services;

/// <summary>
/// Proses bisnis "Load Data" Pajak Keluaran: memilih Stored Procedure sesuai
/// jenis Search By, merakit 6 parameter (from, to, 0, invoice, downpayment,
/// refcreditmemo), lalu mengeksekusinya lewat <see cref="SapQueryService"/>.
/// </summary>
public class PajakKeluaranService
{
    private readonly SapQueryService _query;

    public PajakKeluaranService(SapQueryService query) => _query = query;

    /// <summary>
    /// Jalankan pencarian sesuai filter. Schema Stored Procedure diambil dari
    /// koneksi yang terdaftar untuk user (field Koneksi DB di User Management)
    /// — bukan lagi nilai hard-code.
    /// </summary>
    public async Task<QueryResult> LoadAsync(PajakKeluaranFilter filter, CancellationToken ct = default)
    {
        var (entry, connErr) = await _query.ResolveConnectionAsync();
        if (entry is null)
            return new QueryResult { Error = connErr };

        if (string.IsNullOrWhiteSpace(entry.Schema))
            return new QueryResult { Error = "Schema database belum dikonfigurasi." };

        var result = await _query.RunQueryAsync(BuildSql(filter, entry.Schema), ct);

        // Jangan bocorkan detail SP / akses DB SAP pada pesan error.
        if (!string.IsNullOrEmpty(result.Error))
            result.Error = "Invalid Access Database";

        return result;
    }

    /// <summary>
    /// Susun statement CALL sesuai jenis Search By, memakai schema koneksi.
    /// - Invoice Number -> __IDUFAKTURPAJAK_GETINVOICEFP
    /// - Date           -> __IDUFAKTURPAJAK_GETINVOICEFP_DATE (param tanggal yyyymmdd)
    /// - Vendor         -> __IDUFAKTURPAJAK_GETINVOICEFP_CUSTOMER
    /// </summary>
    public string BuildSql(PajakKeluaranFilter f, string schema)
    {
        var (sp, from, to) = f.SearchBy switch
        {
            "Invoice Number" => ("__IDUFAKTURPAJAK_GETINVOICEFP",          f.InvoiceFrom, f.InvoiceTo),
            "Date"           => ("__IDUFAKTURPAJAK_GETINVOICEFP_DATE",     ToYmd(f.DateFrom), ToYmd(f.DateTo)),
            "Customer"       => ("__IDUFAKTURPAJAK_GETINVOICEFP_CUSTOMER", f.CustomerFrom, f.CustomerTo),
            _                => ("__IDUFAKTURPAJAK_GETINVOICEFP",          f.InvoiceFrom, f.InvoiceTo),
        };

        var inv = f.Invoice ? 1 : 0;
        var dp  = f.DownPayment ? 1 : 0;
        var rcm = f.RefCreditMemo ? 1 : 0;

        return $"CALL \"{schema}\".\"{sp}\" ('{Esc(from)}', '{Esc(to)}', 0, {inv}, {dp}, {rcm})";
    }

    // Escape tanda kutip tunggal agar tidak merusak literal SQL.
    private static string Esc(string? s) => (s ?? "").Replace("'", "''");

    // "29.01.26" -> "20260129" (format parameter SP _DATE).
    private static string ToYmd(string? ddmmyy)
    {
        var d = new string((ddmmyy ?? "").Where(char.IsDigit).ToArray());
        return d.Length == 6 ? $"20{d[4..6]}{d[2..4]}{d[..2]}" : "";
    }
}
