/* ============================================================================
   Migrasi: tambah kolom Creator & Remark pada IDU_PAJAK_TRANS (SAP_WEB).
   Menyamakan struktur dengan tabel UDO asli di SAP B1:
       Creator  NVARCHAR(25)   -- user pembuat dokumen (username aplikasi)
       Remark   NCLOB          -- di SQL Server -> NVARCHAR(MAX)

   Jalankan SEKALI di database SAP_WEB (SQL Server).
   Aman dijalankan berulang (idempotent): kolom hanya ditambah bila belum ada.
   ============================================================================ */

IF COL_LENGTH('dbo.IDU_PAJAK_TRANS', 'Creator') IS NULL
BEGIN
    ALTER TABLE dbo.IDU_PAJAK_TRANS ADD Creator NVARCHAR(25) NULL;
    PRINT 'Kolom Creator ditambahkan.';
END
ELSE
    PRINT 'Kolom Creator sudah ada - dilewati.';
GO

IF COL_LENGTH('dbo.IDU_PAJAK_TRANS', 'Remark') IS NULL
BEGIN
    ALTER TABLE dbo.IDU_PAJAK_TRANS ADD Remark NVARCHAR(MAX) NULL;
    PRINT 'Kolom Remark ditambahkan.';
END
ELSE
    PRINT 'Kolom Remark sudah ada - dilewati.';
GO

/* Verifikasi */
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'IDU_PAJAK_TRANS' AND COLUMN_NAME IN ('Creator', 'Remark')
ORDER BY ORDINAL_POSITION;
GO
