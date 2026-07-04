using System.ComponentModel.DataAnnotations;

namespace BlazorApp1.Models;

/// <summary>
/// Satu baris item pada faktur (barang atau jasa).
/// </summary>
public class InvoiceLine
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    [Required(ErrorMessage = "Nama barang/jasa wajib diisi.")]
    [MaxLength(200)]
    public string NamaBarang { get; set; } = string.Empty;

    [Range(0.0001, double.MaxValue, ErrorMessage = "Kuantitas harus lebih dari 0.")]
    public decimal Qty { get; set; } = 1;

    [Range(0, double.MaxValue, ErrorMessage = "Harga tidak boleh negatif.")]
    public decimal HargaSatuan { get; set; }

    /// <summary>Subtotal baris = Qty × Harga Satuan (tidak disimpan, dihitung).</summary>
    public decimal Subtotal => Qty * HargaSatuan;
}
