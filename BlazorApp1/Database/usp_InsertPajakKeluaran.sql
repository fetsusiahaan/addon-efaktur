/* ============================================================================
   Stored Procedure: simpan transaksi Pajak Keluaran (header + detail) ke SAP_WEB
   Jalankan script ini SEKALI di database SAP_WEB (SQL Server).
   Aplikasi memanggil dbo.usp_InsertPajakKeluaran dengan TVP detail.
   ============================================================================ */

/* ---------- 1. Table type untuk baris detail (dikirim dari aplikasi) ---------- */
IF EXISTS (SELECT 1 FROM sys.types WHERE name = 'PajakKeluaranDetailType' AND is_table_type = 1)
BEGIN
    -- Type tidak bisa di-ALTER; hapus SP dulu baru type bila perlu re-create.
    IF OBJECT_ID('dbo.usp_InsertPajakKeluaran', 'P') IS NOT NULL
        DROP PROCEDURE dbo.usp_InsertPajakKeluaran;
    DROP TYPE dbo.PajakKeluaranDetailType;
END
GO

CREATE TYPE dbo.PajakKeluaranDetailType AS TABLE (
    U_EntryINV       INT,
    U_NumINV         NVARCHAR(20),
    U_DateINV        DATETIME,
    U_BPCode         NVARCHAR(15),
    U_DocType        NVARCHAR(50),
    U_DTypeN         NVARCHAR(100),
    U_NoFP           NVARCHAR(30),
    U_BPName         NVARCHAR(100),
    U_DocTotal       NUMERIC(19,6),
    U_VatSum         NUMERIC(19,6),
    U_Discount       NUMERIC(19,6),
    U_DP             NUMERIC(19,6),
    U_JDP            NVARCHAR(1),
    U_Status         NVARCHAR(1),
    U_IDU_Pengganti  NVARCHAR(2),
    U_IDU_KetTambah  NVARCHAR(5),
    U_IDU_Referensi  NVARCHAR(MAX),
    U_IDU_JenisPajak NVARCHAR(2),
    U_IDU_NamaNPWP   NVARCHAR(254),
    U_IDU_AlmtNPWP   NVARCHAR(254),
    U_IDU_NPWP       NVARCHAR(254),
    U_IDU_NIK        NVARCHAR(30),
    U_IDU_KDP        NVARCHAR(254),
    U_IDU_RatePajak  NVARCHAR(2)
);
GO

/* ---------- 2. Stored procedure ---------- */
CREATE PROCEDURE dbo.usp_InsertPajakKeluaran
    @Search   NVARCHAR(1),
    @Format   NVARCHAR(1)  = NULL,
    @FNum     NVARCHAR(30) = NULL,
    @TNum     NVARCHAR(30) = NULL,
    @FEntry   NVARCHAR(30) = NULL,
    @TEntry   NVARCHAR(30) = NULL,
    @FrCust   NVARCHAR(50) = NULL,
    @ToCust   NVARCHAR(50) = NULL,
    @FrDate   DATETIME     = NULL,
    @ToDate   DATETIME     = NULL,
    @UserSign INT          = NULL,   -- Id user aplikasi yang membuat dokumen
    @Creator  NVARCHAR(25) = NULL,   -- username pembuat dokumen (tampil di "Last updated")
    @KoneksiDb NVARCHAR(100) = NULL, -- nama koneksi SAP asal (Database Manager), penanda company
    @Details  dbo.PajakKeluaranDetailType READONLY,
    @DocEntry INT OUTPUT,
    @Uuid     UNIQUEIDENTIFIER = NULL OUTPUT   -- pengaman URL halaman detail
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRAN;

        -- DocEntry baru = MAX + 1
        SELECT @DocEntry = ISNULL(MAX(DocEntry), 0) + 1 FROM IDU_PAJAK_TRANS;

        -- DocNum = YY + MM + urutan 5 digit (reset per bulan), mis. 260700001
        DECLARE @yy     INT = YEAR(GETDATE()) % 100;          -- 2 digit tahun (26)
        DECLARE @mm     INT = MONTH(GETDATE());               -- bulan (7)
        DECLARE @prefix INT = (@yy * 100 + @mm) * 100000;     -- 260700000
        DECLARE @DocNum INT;

        SELECT @DocNum = ISNULL(MAX(DocNum), @prefix) + 1
        FROM IDU_PAJAK_TRANS
        WHERE DocNum >= @prefix AND DocNum < @prefix + 100000;

        SET @Uuid = NEWID();

        -- Header
        INSERT INTO IDU_PAJAK_TRANS
            (DocEntry, DocNum, Canceled, Status, CreateDate, UserSign, Creator, uuid, KoneksiDb,
             U_Search, U_Format, U_FNum, U_TNum, U_FEntry, U_TEntry,
             U_FrCust, U_ToCust, U_FrDate, U_ToDate)
        VALUES
            (@DocEntry, @DocNum, 'N', 'O', GETDATE(), @UserSign, @Creator, @Uuid, @KoneksiDb,
             @Search, @Format, @FNum, @TNum, @FEntry, @TEntry,
             @FrCust, @ToCust, @FrDate, @ToDate);

        -- Detail (LineId/VisOrder digenerate berurutan mulai 0)
        INSERT INTO IDU_PAJAK_TRANS_DET
            (DocEntry, LineId, VisOrder,
             U_EntryINV, U_NumINV, U_DateINV, U_BPCode, U_DocType, U_DTypeN,
             U_NoFP, U_BPName, U_DocTotal, U_VatSum, U_Discount, U_DP, U_JDP, U_Status,
             U_IDU_Pengganti, U_IDU_KetTambah, U_IDU_Referensi, U_IDU_JenisPajak,
             U_IDU_NamaNPWP, U_IDU_AlmtNPWP, U_IDU_NPWP, U_IDU_NIK, U_IDU_KDP, U_IDU_RatePajak)
        SELECT
            @DocEntry,
            ROW_NUMBER() OVER (ORDER BY (SELECT 1)) - 1,
            ROW_NUMBER() OVER (ORDER BY (SELECT 1)) - 1,
            U_EntryINV, U_NumINV, U_DateINV, U_BPCode, U_DocType, U_DTypeN,
            U_NoFP, U_BPName, U_DocTotal, U_VatSum, U_Discount, U_DP, U_JDP, U_Status,
            U_IDU_Pengganti, U_IDU_KetTambah, U_IDU_Referensi, U_IDU_JenisPajak,
            U_IDU_NamaNPWP, U_IDU_AlmtNPWP, U_IDU_NPWP, U_IDU_NIK, U_IDU_KDP, U_IDU_RatePajak
        FROM @Details;

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRAN;
        THROW;   -- lempar error ke aplikasi
    END CATCH
END
GO
