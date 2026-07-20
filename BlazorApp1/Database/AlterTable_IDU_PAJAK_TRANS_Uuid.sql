/* ============================================================================
   Migrasi: tambah kolom uuid pada IDU_PAJAK_TRANS (SAP_WEB).
   Dipakai sebagai pengaman URL halaman detail:
       /efaktur/show-pajak-keluaran?doc={DocEntry}&uuid={uuid}
   Halaman hanya terbuka bila DocEntry DAN uuid cocok, sehingga dokumen lain
   tidak bisa dibuka hanya dengan menebak/menaikkan angka DocEntry.

   Jalankan SEKALI di database SAP_WEB (SQL Server).
   Aman dijalankan berulang (idempotent).
   ============================================================================ */

/* ---------- 1. Tambah kolom (nullable dulu, agar baris lama tidak menolak) ---------- */
IF COL_LENGTH('dbo.IDU_PAJAK_TRANS', 'uuid') IS NULL
BEGIN
    ALTER TABLE dbo.IDU_PAJAK_TRANS ADD uuid UNIQUEIDENTIFIER NULL;
    PRINT 'Kolom uuid ditambahkan.';
END
ELSE
    PRINT 'Kolom uuid sudah ada - dilewati.';
GO

/* ---------- 2. Backfill dokumen lama yang belum punya uuid ---------- */
UPDATE dbo.IDU_PAJAK_TRANS
SET uuid = NEWID()
WHERE uuid IS NULL;
PRINT CONCAT(@@ROWCOUNT, ' baris di-backfill uuid.');
GO

/* ---------- 3. Default untuk baris baru + jaga keunikan ---------- */
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints
               WHERE name = 'DF_IDU_PAJAK_TRANS_uuid')
BEGIN
    ALTER TABLE dbo.IDU_PAJAK_TRANS
        ADD CONSTRAINT DF_IDU_PAJAK_TRANS_uuid DEFAULT NEWID() FOR uuid;
    PRINT 'Default NEWID() ditambahkan.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'UX_IDU_PAJAK_TRANS_uuid'
                 AND object_id = OBJECT_ID('dbo.IDU_PAJAK_TRANS'))
BEGIN
    CREATE UNIQUE INDEX UX_IDU_PAJAK_TRANS_uuid
        ON dbo.IDU_PAJAK_TRANS (uuid) WHERE uuid IS NOT NULL;
    PRINT 'Unique index uuid dibuat.';
END
GO

/* ---------- Verifikasi ---------- */
SELECT TOP 10 DocEntry, DocNum, uuid FROM dbo.IDU_PAJAK_TRANS ORDER BY DocEntry DESC;
GO
