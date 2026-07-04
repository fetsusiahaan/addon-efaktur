namespace BlazorApp1.Models;

/// <summary>
/// Baris data Pajak Masukan (hasil query OINV) dalam bentuk bertipe,
/// agar RadzenDataGrid bisa sorting/filtering/column-picking secara native.
/// </summary>
public class PajakMasukanRow
{
    public int DocEntry { get; set; }
    public string? CardCode { get; set; }
    public string? CardName { get; set; }
    public string? NumAtCard { get; set; }
    public decimal DocTotalFC { get; set; }
    public string? Address { get; set; }
}
