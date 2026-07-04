using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlazorApp1.Models;

/// <summary>
/// Header faktur. Perhitungan DPP, PPN, dan Total diturunkan dari baris item.
/// </summary>
public class Invoice
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Nomor faktur wajib diisi.")]
    [MaxLength(50)]
    public string Nomor { get; set; } = string.Empty;

    public DateTime Tanggal { get; set; } = DateTime.Today;

    [Range(1, int.MaxValue, ErrorMessage = "Pelanggan wajib dipilih.")]
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    /// <summary>Tarif PPN dalam persen. Default 11%.</summary>
    [Range(0, 100)]
    public decimal PpnPersen { get; set; } = 11m;

    [MaxLength(500)]
    public string? Keterangan { get; set; }

    public List<InvoiceLine> Lines { get; set; } = new();

    // ---- Nilai turunan (tidak disimpan ke database) ----

    /// <summary>Dasar Pengenaan Pajak = jumlah seluruh subtotal baris.</summary>
    [NotMapped]
    public decimal Dpp => Lines?.Sum(l => l.Subtotal) ?? 0m;

    /// <summary>Nilai PPN = DPP × tarif.</summary>
    [NotMapped]
    public decimal PpnNilai => Math.Round(Dpp * PpnPersen / 100m, 2, MidpointRounding.AwayFromZero);

    /// <summary>Total tagihan = DPP + PPN.</summary>
    [NotMapped]
    public decimal Total => Dpp + PpnNilai;
}
