namespace BlazorApp1.Models;

/// <summary>
/// Status siklus hidup sebuah faktur.
/// </summary>
public enum InvoiceStatus
{
    Draft = 0,   // Masih disusun, belum final
    Terbit = 1,  // Sudah diterbitkan ke pelanggan
    Lunas = 2,   // Sudah dibayar
    Batal = 3    // Dibatalkan
}
