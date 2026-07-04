namespace BlazorApp1.Models;

/// <summary>
/// Satu baris dokumen pajak (Pajak Masukan / Pajak Keluaran).
/// Field mengikuti istilah SAP Business One (DocEntry, CardCode, dst).
/// </summary>
public class TaxDoc
{
    public int DocEntry { get; set; }
    public string Docnum { get; set; } = string.Empty;
    public string TipePajak { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string CardCode { get; set; } = string.Empty;
    public string Cardname { get; set; } = string.Empty;
    public string? Nik { get; set; }
    public string? Npwp { get; set; }
    public string? NpwpName { get; set; }
    public string? NpwpAddress { get; set; }
    public decimal DocTotal { get; set; }
    public decimal Discount { get; set; }
    public bool Canceled { get; set; }

    /// <summary>Status centang di grid (tidak ikut diekspor).</summary>
    public bool Selected { get; set; }
}
