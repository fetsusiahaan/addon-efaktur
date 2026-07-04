using System.ComponentModel.DataAnnotations;

namespace BlazorApp1.Models;

/// <summary>
/// Lawan transaksi / pelanggan yang menerima faktur.
/// </summary>
public class Customer
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Nama pelanggan wajib diisi.")]
    [MaxLength(150)]
    public string Nama { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Npwp { get; set; }

    [MaxLength(300)]
    public string? Alamat { get; set; }

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
