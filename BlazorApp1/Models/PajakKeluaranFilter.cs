namespace BlazorApp1.Models;

/// <summary>
/// Kriteria pencarian faktur pajak keluaran (input dari halaman Buat Pajak Keluaran).
/// Nilai tanggal memakai format tampilan "dd.MM.yy" (akan dinormalisasi oleh service).
/// </summary>
public class PajakKeluaranFilter
{
    public string SearchBy { get; set; } = "Vendor";  // "Invoice Number" | "Date" | "Vendor"

    public string InvoiceFrom { get; set; } = "";
    public string InvoiceTo { get; set; } = "";

    public string DateFrom { get; set; } = "";        // "dd.MM.yy"
    public string DateTo { get; set; } = "";

    public string CustomerFrom { get; set; } = "";
    public string CustomerTo { get; set; } = "";

    // Document Type (checked => 1).
    public bool Invoice { get; set; }
    public bool DownPayment { get; set; }
    public bool RefCreditMemo { get; set; }
}
