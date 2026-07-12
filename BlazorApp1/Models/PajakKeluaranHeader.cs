namespace BlazorApp1.Models;

/// <summary>Data header transaksi Pajak Keluaran (IDU_PAJAK_TRANS).</summary>
public class PajakKeluaranHeader
{
    public string Search { get; set; } = "N";   // N=Invoice, D=Date, C=Customer
    public string? Format { get; set; } = "0";

    public string? FromNum { get; set; }
    public string? ToNum { get; set; }
    public string? FromEntry { get; set; }
    public string? ToEntry { get; set; }
    public string? FromCust { get; set; }
    public string? ToCust { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
