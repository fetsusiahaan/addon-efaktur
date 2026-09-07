/* ============================================================================
   Migrasi: tambah kolom KoneksiDb ke IDU_PAJAK_TRANS (SAP_WEB, SQL Server).
   Jalankan SEKALI, sebelum re-deploy usp_InsertPajakKeluaran.sql yang baru.
   Kolom menyimpan nama koneksi SAP (DbConnectionEntry.Nama) asal transaksi,
   untuk mendukung banyak company SAP. Baris lama otomatis NULL (data lama,
   diperlakukan sebagai "cocok koneksi manapun" oleh aplikasi).
   ============================================================================ */

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.IDU_PAJAK_TRANS') AND name = 'KoneksiDb'
)
BEGIN
    ALTER TABLE dbo.IDU_PAJAK_TRANS ADD KoneksiDb NVARCHAR(100) NULL;
END
GO
