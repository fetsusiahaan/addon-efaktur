namespace BlazorApp1.Models;

/// <summary>Jenis tampilan kolom (menentukan format & perataan).</summary>
public enum ColKind { Text, Number, Money, Date }

/// <summary>Definisi satu kolom grid: nama properti, judul, tipe, visibilitas.</summary>
public record GridColumn(string Property, string Title, ColKind Kind = ColKind.Text, bool Visible = true);

/// <summary>
/// Satu baris hasil SP faktur pajak keluaran
/// (__IDUFAKTURPAJAK_GETINVOICEFP / _DATE / _CUSTOMER).
/// Nama properti mengikuti nama kolom hasil SP.
/// </summary>
public class FakturKeluaranRow
{
    public int DocEntry { get; set; }
    public int DocNum { get; set; }
    public DateTime? DocDate { get; set; }
    public int CardEntry { get; set; }
    public string? CardCode { get; set; }
    public string? CardName { get; set; }
    public decimal DocTotal { get; set; }
    public decimal VatSum { get; set; }
    public int ObjType { get; set; }
    public string? ObjName { get; set; }
    public string? NoFP { get; set; }
    public string? Status { get; set; }
    public string? Pengganti { get; set; }
    public string? KetTambah { get; set; }
    public string? Referensi { get; set; }
    public string? JenisPajak { get; set; }
    public string? NamaNPWP { get; set; }
    public string? AlamatNPWP { get; set; }
    public string? NPWP { get; set; }
    public decimal Discount { get; set; }
    public decimal DP { get; set; }
    public string? JDP { get; set; }
    public string? NIK { get; set; }
    public string? KDP { get; set; }
    public string? RatePajak { get; set; }

    /// <summary>
    /// Definisi kolom grid — sumber tunggal Title/tipe/urutan/visibilitas.
    /// Grid me-render kolom secara dinamis dari daftar ini.
    /// </summary>
    public static readonly IReadOnlyList<GridColumn> Columns = new[]
    {
        new GridColumn("DocNum",     "No. Dokumen",       ColKind.Number),
        new GridColumn("DocDate",    "Tanggal",           ColKind.Date),
        new GridColumn("ObjName",    "Jenis Dokumen"),
        new GridColumn("CardCode",   "Kode Customer"),
        new GridColumn("CardName",   "Nama Customer"),
        new GridColumn("NoFP",       "No. Faktur Pajak"),
        new GridColumn("NPWP",       "NPWP"),
        new GridColumn("NamaNPWP",   "Nama NPWP"),
        new GridColumn("AlamatNPWP", "Alamat NPWP"),
        new GridColumn("NIK",        "NIK"),
        new GridColumn("DocTotal",   "Total",             ColKind.Money),
        new GridColumn("VatSum",     "PPN",               ColKind.Money),
        new GridColumn("Discount",   "Diskon",            ColKind.Money),
        new GridColumn("DP",         "DP",                ColKind.Money),
        new GridColumn("JenisPajak", "Jenis Pajak"),
        new GridColumn("JDP",        "Jenis DP"),
        new GridColumn("RatePajak",  "Rate Pajak"),
        new GridColumn("Pengganti",  "Pengganti"),
        new GridColumn("Status",     "Status"),
        new GridColumn("Referensi",  "Referensi"),
        new GridColumn("KetTambah",  "Ket. Tambahan"),
        new GridColumn("KDP",        "Kode DP"),
        new GridColumn("DocEntry",   "DocEntry",          ColKind.Number, false),
        new GridColumn("ObjType",    "Tipe Objek",        ColKind.Number, false),
        new GridColumn("CardEntry",  "Card Entry",        ColKind.Number, false),
    };
}
