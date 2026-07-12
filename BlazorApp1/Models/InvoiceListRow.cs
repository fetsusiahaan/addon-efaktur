namespace BlazorApp1.Models;

/// <summary>Satu baris daftar A/R Invoice (OINV) untuk pop-up pemilihan.</summary>
public class InvoiceListRow
{
    public int DocEntry { get; set; }
    public string? NumAtCard { get; set; }
    public int DocNum { get; set; }
    public string? CardCode { get; set; }
    public string? CardName { get; set; }
}
