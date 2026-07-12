namespace BlazorApp1.Models;

/// <summary>Satu baris daftar Customer (OCRD) untuk pop-up pemilihan.</summary>
public class CustomerListRow
{
    public int DocEntry { get; set; }
    public string? CardCode { get; set; }
    public string? CardName { get; set; }
    public string? OwnerCode { get; set; }
    public string? OwnerName { get; set; }
}
