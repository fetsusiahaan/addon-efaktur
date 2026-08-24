using BlazorApp1.Models;
using Radzen.Documents.Markdown;
using System.Globalization;
using System.Text;
using System.Xml;

namespace BlazorApp1.Services;

/// <summary>
/// Export CSV e-Faktur 100% sesuai versi VB.NET, TANPA membuat SP baru:
/// item per baris diambil langsung dari DB SAP B1 (INV1/OINV) dan data
/// perusahaan dari OADM/ADM1 via query, lalu digabung dengan field header
/// dari grid (SAP_WEB). Perhitungan DPP/PPN & format FK/FAPR/OF meniru SP
/// __IDUFAKTURPAJAK_EFAKTUR + Button__TP__Export.
/// </summary>
public class EFakturExportService
{
    private readonly SapQueryService _sap;
    private readonly WebDbService _web;
    public EFakturExportService(SapQueryService sap, WebDbService web)
    {
        _sap = sap;
        _web = web;
    }

    private sealed record Line(int DocEntry, string? ItemCode, string? Desc, decimal Price, decimal Qty, decimal LineTotal, decimal TotalPPN, decimal Vatsum);

    public async Task<(string? Csv, string? Error)> BuildCsvAsync(
        IReadOnlyList<FakturKeluaranRow> rows, CancellationToken ct = default)
    {
        if (rows.Count == 0) return (null, "Tidak ada data.");

        var inv = CultureInfo.InvariantCulture;

        // ── 1. Item per baris dari SAP B1 (hanya invoice OINV / ObjType 13) ──
        var invEntries = rows.Where(r => r.ObjType == 13).Select(r => r.DocEntry).Distinct().ToList();
        var lineMap = new Dictionary<int, List<Line>>();

        if (invEntries.Count > 0)
        {
            var ids = string.Join(",", invEntries);
            // var lineSql =
            //     "SELECT T1.\"DocEntry\" \"DocEntry\", T1.\"VisOrder\" \"VisOrder\", T1.\"ItemCode\" \"ItemCode\", " +
            //     "REPLACE(REPLACE(REPLACE(REPLACE(T1.\"Dscription\",'\"',''),'''',''),',','.'),';','.') \"Dscription\", " +
            //     "CASE WHEN T1.\"Rate\" > 0 THEN ROUND(CAST(T1.\"PriceBefDi\" AS DECIMAL(19,2)) * T1.\"Rate\", 0) " +
            //     "ELSE CAST(T1.\"PriceBefDi\" AS DECIMAL(19,2)) END \"PriceBefDi\", " +
            //     "CASE WHEN T9.\"DocType\" = 'S' THEN 1 ELSE T1.\"OpenQty\" END \"Quantity\", " +
            //     "T1.\"LineTotal\" \"LineTotal\" " +
            //     "FROM \"INV1\" T1 JOIN \"OINV\" T9 ON T9.\"DocEntry\" = T1.\"DocEntry\" " +
            //     $"WHERE T1.\"DocEntry\" IN ({ids}) AND T1.\"LineTotal\" > 0 " +
            //     "ORDER BY T1.\"DocEntry\", T1.\"VisOrder\"";

            var lineSql =
               "SELECT X.\"DocEntry\", X.\"VisOrder\", X.\"ItemCode\", X.\"Dscription\", " +
               "X.\"PriceBefDi\", X.\"Quantity\", X.\"LineTotal\", " +
               "FLOOR(SUM(\"TotalPPN\") OVER(PARTITION BY \"DocNum\" ORDER BY \"DocNum\")) \"VatSum\"," +

               "ROUND( " +
               "CASE " +
               "WHEN MAX(X.\"Num\") OVER (PARTITION BY X.\"DocNum\") = X.\"Num\" THEN " +
                   "CASE " +
                   "WHEN SUM(X.\"TotalPPN\") OVER (PARTITION BY X.\"DocNum\") <> X.\"VatSum\" THEN " +
                       "FLOOR(X.\"TotalPPN\" + (X.\"VatSum\" - SUM(X.\"TotalPPN\") OVER (PARTITION BY X.\"DocNum\"))) " +
                   "ELSE FLOOR(X.\"TotalPPN\") " +
                   "END " +
               "ELSE FLOOR(X.\"TotalPPN\") " +
               "END, 0) \"TotalPPN\" " +
               "FROM ( " +

                   "SELECT " +
                   "T1.\"DocEntry\" \"DocEntry\", " +
                   "T9.\"DocNum\" \"DocNum\", " +
                   "T1.\"VisOrder\" \"VisOrder\", " +
                   "ROW_NUMBER() OVER(PARTITION BY T1.\"DocEntry\" ORDER BY T1.\"VisOrder\") \"Num\", " +
                   "T1.\"ItemCode\" \"ItemCode\", " +
                   "REPLACE(REPLACE(REPLACE(REPLACE(T1.\"Dscription\",'\"',''),'''',''),',','.'),';','.') \"Dscription\", " +

                   "CASE WHEN T1.\"Rate\" > 0 " +
                   "THEN ROUND(CAST(T1.\"PriceBefDi\" AS DECIMAL(19,2)) * T1.\"Rate\",0) " +
                   "ELSE CAST(T1.\"PriceBefDi\" AS DECIMAL(19,2)) END \"PriceBefDi\", " +

                   "CASE WHEN T9.\"DocType\"='S' THEN 1 ELSE T1.\"OpenQty\" END \"Quantity\", " +
                   "T1.\"LineTotal\" \"LineTotal\", " +
                   "T9.\"VatSum\" \"VatSum\", " +

                   "FLOOR(( " +
                       "FLOOR(IFNULL(T1.\"LineTotal\",0)) - " +
                       "FLOOR(T1.\"DiscPrcnt\" * (T1.\"LineTotal\" / " +
                           "SUM(IFNULL(T1.\"LineTotal\",0)) OVER(PARTITION BY T9.\"DocNum\" ORDER BY T9.\"DocNum\"))) " +
                   ") * T9.\"U_IDU_RatePajak\" / 100) \"TotalPPN\" " +

                   "FROM \"INV1\" T1 " +
                   "JOIN \"OINV\" T9 ON T9.\"DocEntry\" = T1.\"DocEntry\" " +
                   $"WHERE T1.\"DocEntry\" IN ({ids}) " +
                   "AND T1.\"LineTotal\" > 0 " +

               ") X " +
               "ORDER BY X.\"DocEntry\", X.\"VisOrder\"";

            var lr = await _sap.RunRawQueryAsync(lineSql, ct);
            if (lr.Error is not null) return (null, lr.Error);

            var idx = ColIndex(lr);
            foreach (var row in lr.Rows)
            {
                var de = ToInt(Cell(row, idx, "DocEntry"));
                var line = new Line(de,
                    Cell(row, idx, "ItemCode")?.ToString(),
                    Cell(row, idx, "Dscription")?.ToString(),
                    ToDec(Cell(row, idx, "PriceBefDi")),
                    ToDec(Cell(row, idx, "Quantity")),
                    ToDec(Cell(row, idx, "LineTotal")),
                    ToDec(Cell(row, idx, "TotalPPN")),
                    ToDec(Cell(row, idx, "VatSum")));
                if (!lineMap.TryGetValue(de, out var list)) lineMap[de] = list = new();
                list.Add(line);
            }
        }

        // ── 2. Data perusahaan (FAPR) ──
        var (cName, cAddr, cCity, cSigner) = await GetCompanyAsync(ct);

        // ── 3. Bangun CSV ──
        var sb = new StringBuilder();
        sb.AppendLine("FK,KD_JENIS_TRANSAKSI,FG_PENGGANTI,NOMOR_FAKTUR,MASA_PAJAK,TAHUN_PAJAK," +
                      "TANGGAL_FAKTUR,NPWP,NAMA,ALAMAT_LENGKAP,JUMLAH_DPP,JUMLAH_PPN,JUMLAH_PPNBM," +
                      "ID_KETERANGAN_TAMBAHAN,FG_UANG_MUKA,UANG_MUKA_DPP,UANG_MUKA_PPN,UANG_MUKA_PPNBM," +
                      "REFERENSI,KODE_DOKUMEN_PENDUKUNG,NIK");
        sb.AppendLine("LT,NPWP,NAMA,JALAN,BLOK,NOMOR,RT,RW,KECAMATAN,KELURAHAN,KABUPATEN," +
                      "PROPINSI,KODE_POS,NOMOR_TELEPON,,,,,,,");
        sb.AppendLine("OF,KODE_OBJEK,NAMA,HARGA_SATUAN,JUMLAH_BARANG,HARGA_TOTAL,DISKON,DPP,PPN," +
                      "TARIF_PPNBM,PPNBM,,,,,,,,,,");

        foreach (var r in rows)
        {
            var rate = ToDec(r.RatePajak);
            if (rate <= 0) rate = 11m;   // default PPN

            var ofRows = new List<(string Code, string Desc, decimal Price, decimal Qty, decimal Bef, decimal Disc, decimal Dpp, decimal Ppn)>();
            decimal totalDpp = 0, totalPpn = 0;
            int oneLineCount = 1;
            if (lineMap.TryGetValue(r.DocEntry, out var lines) && lines.Count > 0)
            {
                var sumLine = lines.Sum(l => l.LineTotal);
                oneLineCount = lines.Count;
                // Hitung per baris: DPP + PPN "sebenarnya" (belum dibulatkan).
                var tmp = new List<(string Code, string Desc, decimal Price, decimal Qty, decimal Bef, decimal Disc, decimal Dpp, decimal TruePpn)>();
                foreach (var l in lines)
                {
                    var bef = Math.Floor(l.LineTotal);
                    var disc = sumLine == 0 ? 0 : Math.Floor(r.Discount * (l.LineTotal / sumLine));
                    var dpp = bef - disc;


                    totalPpn = l.Vatsum;   // PPN header (FAPR) = Σ PPN baris (belum dibulatkan)
                    var truePpn = l.TotalPPN;   // PPN "sebenarnya" (belum dibulatkan)
                    tmp.Add((l.ItemCode ?? "000000", l.Desc ?? "", l.Price, l.Qty, bef, disc, dpp, truePpn));
                }

                totalDpp = tmp.Sum(t => t.Dpp);

                // ── Penyerapan pembulatan baris terakhir DIMATIKAN (permintaan) ──
                // Tiap baris PPN = FLOOR(PPN sebenarnya), dan PPN header = Σ PPN baris
                // (tetap konsisten: Σ PPN OF = JUMLAH_PPN). Versi lama disimpan bila
                // perlu diaktifkan lagi:
                //
                //   totalPpn = Math.Floor(tmp.Sum(t => t.TruePpn));   // FLOOR(Σ PPN sebenarnya)
                //   decimal accum = 0;
                //   for (var i = 0; i < tmp.Count; i++)
                //   {
                //       var t = tmp[i];
                //       decimal ppn;
                //       if (i < tmp.Count - 1) { ppn = Math.Floor(t.TruePpn); accum += ppn; }
                //       else { ppn = totalPpn - accum; }   // baris terakhir menyerap selisih
                //       ofRows.Add((t.Code, t.Desc, t.Price, t.Qty, t.Bef, t.Disc, t.Dpp, ppn));
                //   }
                // ─────────────────────────────────────────────────────────────────
                //totalPpn = 0;
                foreach (var t in tmp)
                {
                    //var ppn = Math.Floor(t.TruePpn);
                    //totalPpn += t.TruePpn;
                    decimal ppnPerRow = 0;
                    if (oneLineCount == 1)
                    {
                        ppnPerRow = t.TruePpn;
                    }
                    else
                    {
                        ppnPerRow = t.TruePpn;
                    }


                    ofRows.Add((t.Code, t.Desc, t.Price, t.Qty, t.Bef, t.Disc, t.Dpp, ppnPerRow));
                }
            }
            else
            {
                // Fallback (DP / tanpa baris item): satu OF ringkas dari grid.
                var dpp = Math.Floor(r.DocTotal - r.VatSum);
                var ppn = Math.Floor(r.VatSum);
                totalDpp = dpp;
                totalPpn = r.VatSum;
                ofRows.Add(("000000", CommaClean(r.CardName) ?? "Penjualan", dpp, 1m, dpp, Math.Floor(r.Discount), dpp, ppn));
            }

            var dt = r.DocDate;
            var masa = dt is { } d1 ? d1.Month.ToString() : "";
            var tahun = dt is { } d2 ? d2.Year.ToString() : "";
            var tgl = dt is { } d3 ? d3.ToString("dd/MM/yyyy") : "";

            // FK
            sb.Append("FK,")
              .Append(r.JenisPajak).Append(',')
              .Append(string.IsNullOrEmpty(r.Pengganti) ? "0" : r.Pengganti).Append(',')
              .Append(Digits(r.NoFP)).Append(',')
              .Append(masa).Append(',').Append(tahun).Append(',').Append(tgl).Append(',')
              .Append(Q(Digits(r.NPWP))).Append(',')
              // NAMA: nama NPWP untuk customer ber-NPWP, kosong bila cash/tanpa
              // NPWP (mengikuti output SAP / SP __IDUFAKTURPAJAK_EFAKTU).
              .Append(NamaForFk(r)).Append(',')
              .Append(Q(CommaClean(r.AlamatNPWP))).Append(',')
              .Append(Int(totalDpp)).Append(',').Append(Int(totalPpn)).Append(",0,")
              .Append(Q(CommaClean(r.KetTambah))).Append(",0,0,0,0,")
              .Append(OneLine(r.Referensi)).Append(',')
              .Append(r.KDP).Append(',').Append(r.NIK);
            sb.AppendLine();

            // FAPR — prefix DIKOSONGKAN agar sama dengan output VB.NET
            // (VB memakai setting FAPR=empty; dulu prefix di sini "FAPR,").
            sb.Append(',').Append(cName).Append(',').Append(Q(cAddr)).Append(',')
              .Append(cSigner).Append(',').Append(cCity).Append(",,,,,,,,,,,,,,,,");
            sb.AppendLine();

            // OF
            foreach (var of in ofRows)
            {
                sb.Append("OF,").Append(of.Code).Append(',').Append(Q(of.Desc)).Append(',')
                  .Append(Dec(of.Price)).Append(',').Append(Dec(of.Qty)).Append(',')
                  .Append(Int(of.Bef)).Append(',').Append(Int(of.Disc)).Append(',')
                  .Append(Int(of.Dpp)).Append(',')
                  .Append(Int(of.Ppn)).Append(",0,0,,,,,,,,,,");
                sb.AppendLine();
            }
        }

        return (sb.ToString(), null);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  EXPORT XML — meniru struktur "TaxInvoiceBulk" (Coretax) versi VB.NET
    //  (Button__TP__XML + SP __IDUFAKTURPAJAK_EFAKTUR). Karena data UDO ada di
    //  SAP_WEB (bukan DB SAP B1 tempat SP membaca), field dihitung di aplikasi:
    //  item baris + data buyer/seller diambil dari SAP B1, digabung field grid.
    //  Cakupan: faktur normal (ObjType 13). DP (07/08) & delivery-BM disederhanakan.
    // ════════════════════════════════════════════════════════════════════════
    // Tarif PPN yang ditulis di elemen <VATRate> dan dipakai rumus VAT jenis '01'
    // (meniru output VB.NET: ROUND(11/12 × DPP × 11/100)). Ubah bila tarif berubah.
    private const int VatRatePct = 11;

    private sealed record XmlLine(
        int DocEntry, string? ItemCode, string? Desc, string? Unit, string DocType,
        decimal Price, decimal Qty, decimal LineTotal, decimal VatPrcnt);

    private sealed record BuyerInfo(
        string JenisCust, string JenisCash,
        string NamaNPWP, string AlmtNPWP, string Nik, string Idtku,
        string Email, string CardName, string CpName, string CpAddr, string CpEmail,
        string Country);

    public async Task<(string? Xml, string? Error)> BuildXmlAsync(
        IReadOnlyList<FakturKeluaranRow> rows, CancellationToken ct = default)
    {
        if (rows.Count == 0) return (null, "Tidak ada data.");

        // ── 1. Item per baris dari SAP B1 (invoice / ObjType 13) ──
        var invEntries = rows.Where(r => r.ObjType == 13).Select(r => r.DocEntry).Distinct().ToList();
        var lineMap = new Dictionary<int, List<XmlLine>>();

        if (invEntries.Count > 0)
        {
            var ids = string.Join(",", invEntries);
            var lineSql =
                "SELECT T1.\"DocEntry\" \"DocEntry\", T1.\"VisOrder\" \"VisOrder\", T1.\"ItemCode\" \"ItemCode\", " +
                "REPLACE(REPLACE(REPLACE(REPLACE(T1.\"Dscription\",'\"',''),'''',''),',','.'),';','.') \"Dscription\", " +
                "CASE WHEN T1.\"Rate\" > 0 THEN ROUND(CAST(T1.\"PriceBefDi\" AS DECIMAL(19,2)) * T1.\"Rate\", 0) " +
                "ELSE CAST(T1.\"PriceBefDi\" AS DECIMAL(19,2)) END \"PriceBefDi\", " +
                "CASE WHEN T9.\"DocType\" = 'S' THEN 1 ELSE T1.\"OpenQty\" END \"Quantity\", " +
                "T1.\"LineTotal\" \"LineTotal\", T1.\"VatPrcnt\" \"VatPrcnt\", T9.\"DocType\" \"DocType\", " +
                "CASE WHEN T9.\"DocType\" = 'S' THEN 'UM.0018' ELSE COALESCE(T19.\"U_IDU_UMCode\",'') END \"UMCode\" " +
                "FROM \"INV1\" T1 JOIN \"OINV\" T9 ON T9.\"DocEntry\" = T1.\"DocEntry\" " +
                "LEFT JOIN \"OUOM\" T19 ON T19.\"UomCode\" = T1.\"UomCode\" " +
                $"WHERE T1.\"DocEntry\" IN ({ids}) AND T1.\"LineTotal\" > 0 " +
                "ORDER BY T1.\"DocEntry\", T1.\"VisOrder\"";

            var lr = await _sap.RunRawQueryAsync(lineSql, ct);
            if (lr.Error is not null) return (null, lr.Error);

            var idx = ColIndex(lr);
            foreach (var row in lr.Rows)
            {
                var de = ToInt(Cell(row, idx, "DocEntry"));
                var line = new XmlLine(de,
                    Cell(row, idx, "ItemCode")?.ToString(),
                    Cell(row, idx, "Dscription")?.ToString(),
                    Cell(row, idx, "UMCode")?.ToString(),
                    Cell(row, idx, "DocType")?.ToString() ?? "I",
                    ToDec(Cell(row, idx, "PriceBefDi")),
                    ToDec(Cell(row, idx, "Quantity")),
                    ToDec(Cell(row, idx, "LineTotal")),
                    ToDec(Cell(row, idx, "VatPrcnt")));
                if (!lineMap.TryGetValue(de, out var list)) lineMap[de] = list = new();
                list.Add(line);
            }
        }

        // ── 2. Data buyer + referensi per invoice (OINV + OCRD) ──
        var buyerMap = new Dictionary<int, BuyerInfo>();
        if (invEntries.Count > 0)
        {
            var ids = string.Join(",", invEntries);
            var buyerSql =
                "SELECT T9.\"DocEntry\" \"DocEntry\", " +
                "COALESCE(T7.\"U_IDU_JenisCustomer\",'') \"JenisCust\", COALESCE(T7.\"U_IDU_JenisCash\",'') \"JenisCash\", " +
                "COALESCE(T7.\"U_IDU_NamaNPWP\",'') \"NamaNPWP\", COALESCE(T7.\"U_IDU_AlmtNPWP\",'') \"AlmtNPWP\", " +
                "COALESCE(T7.\"U_IDU_NIK\",'') \"Nik\", " +
                "COALESCE(T7.\"U_IDU_IDTKU\",'') \"IDTKU\", COALESCE(T7.\"E_Mail\",'') \"Email\", " +
                "COALESCE(T9.\"CardName\",'') \"CardName\", " +
                "COALESCE(T8.\"Name\",'') \"CpName\", COALESCE(T8.\"Address\",'') \"CpAddr\", COALESCE(T8.\"E_MailL\",'') \"CpEmail\", " +
                "COALESCE(T7.\"Country\",'') \"Country\" " +
                "FROM \"OINV\" T9 JOIN \"OCRD\" T7 ON T7.\"CardCode\" = T9.\"CardCode\" " +
                "LEFT JOIN \"OCPR\" T8 ON T8.\"CardCode\" = T9.\"CardCode\" AND T8.\"CntctCode\" = T9.\"CntctCode\" " +
                $"WHERE T9.\"DocEntry\" IN ({ids})";
            var br = await _sap.RunRawQueryAsync(buyerSql, ct);
            if (br.Error is not null) return (null, br.Error);
            {
                var idx = ColIndex(br);
                foreach (var row in br.Rows)
                {
                    var de = ToInt(Cell(row, idx, "DocEntry"));
                    buyerMap[de] = new BuyerInfo(
                        Cell(row, idx, "JenisCust")?.ToString() ?? "",
                        Cell(row, idx, "JenisCash")?.ToString() ?? "",
                        Cell(row, idx, "NamaNPWP")?.ToString() ?? "",
                        Cell(row, idx, "AlmtNPWP")?.ToString() ?? "",
                        Cell(row, idx, "Nik")?.ToString() ?? "",
                        Cell(row, idx, "IDTKU")?.ToString() ?? "",
                        Cell(row, idx, "Email")?.ToString() ?? "",
                        Cell(row, idx, "CardName")?.ToString() ?? "",
                        Cell(row, idx, "CpName")?.ToString() ?? "",
                        Cell(row, idx, "CpAddr")?.ToString() ?? "",
                        Cell(row, idx, "CpEmail")?.ToString() ?? "",
                        Cell(row, idx, "Country")?.ToString() ?? "");
                }
            }
        }

        // ── 3. Peta kode negara ISO-3 (tabel ISO3CountryCode di SAP_WEB) ──
        //   OCRD.Country (SAP B1) di-map ke ISO3Code lewat tabel di SAP_WEB.
        //   Karena beda database (HANA vs SQL Server), join dilakukan di aplikasi.
        var iso3 = await GetIso3MapAsync();

        // ── 4. Seller (TIN dari OADM) ──
        var tin = await GetCompanyTinAsync(ct);
        var sellerIdtku = string.IsNullOrEmpty(tin) ? "" : tin + "000000";

        // ── 4. Tulis XML "TaxInvoiceBulk" ──
        var settings = new XmlWriterSettings
        {
            Indent = true,
            // encoderShouldEmitUTF8Identifier: true -> tulis BOM UTF-8 (EF BB BF)
            // di awal stream, sama seperti output VB.NET.
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            ConformanceLevel = ConformanceLevel.Auto,
        };

        using var ms = new MemoryStream();
        using (var w = XmlWriter.Create(ms, settings))
        {
            w.WriteStartDocument();
            w.WriteStartElement("TaxInvoiceBulk");
            w.WriteAttributeString("xmlns", "xsd", null, "http://www.w3.org/2001/XMLSchema");
            w.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");

            // TIN penjual (sekali), lalu daftar faktur
            w.WriteElementString("TIN", tin);
            w.WriteStartElement("ListOfTaxInvoice");

            foreach (var r in rows.OrderBy(x => x.DocNum))
            {
                var jenis = (r.JenisPajak ?? "").Trim();
                buyerMap.TryGetValue(r.DocEntry, out var buyer);
                var invNum = r.DocNum > 0 ? r.DocNum.ToString(CultureInfo.InvariantCulture) : "";

                w.WriteStartElement("TaxInvoice");

                // Header faktur — CustomDoc & RefDesc = nomor invoice (seperti VB).
                w.WriteElementString("TaxInvoiceDate", r.DocDate is { } d ? d.ToString("yyyy-MM-dd") : "");
                w.WriteElementString("TaxInvoiceOpt", "Normal");
                WriteEl(w, "TrxCode", jenis);
                WriteEl(w, "AddInfo", "");
                WriteEl(w, "CustomDoc", invNum);
                WriteEl(w, "CustomDocMonthYear", "");
                WriteEl(w, "RefDesc", invNum);
                WriteEl(w, "FacilityStamp", "");
                WriteEl(w, "SellerIDTKU", sellerIdtku);

                // Buyer — data dari SAP B1 (OCRD/OCPR), meniru CASE di SP EFAKTUR:
                //   JenisCustomer='01' (ber-NPWP) → pakai NPWP/nama/alamat OCRD
                //   selain itu (NIK) → cash pakai OCPR, else OCRD; fallback CardName.
                var isNpwp = (buyer?.JenisCust ?? "").Trim() == "01";
                var isCash = (buyer?.JenisCash ?? "").Trim() == "01";
                var npwp = Digits(r.NPWP);   // NPWP dari detail (t0), seperti SP

                string bName, bAddr, bEmail;
                if (isNpwp)
                {
                    bName = buyer!.NamaNPWP;
                    bAddr = buyer.AlmtNPWP;
                    bEmail = buyer.Email;
                }
                else if (isCash)
                {
                    bName = buyer!.CpName;
                    bAddr = buyer.CpAddr;
                    bEmail = buyer.CpEmail;
                }
                else
                {
                    bName = FirstNonEmpty(buyer?.NamaNPWP, buyer?.CpName);
                    bAddr = FirstNonEmpty(buyer?.AlmtNPWP, buyer?.CpAddr);
                    bEmail = FirstNonEmpty(buyer?.Email, buyer?.CpEmail);
                }
                // Fallback terakhir bila semua kosong: nama/alamat dari invoice.
                //bName = FirstNonEmpty(bName, buyer?.CardName, r.CardName);

                WriteEl(w, "BuyerTin", isNpwp ? npwp : null);
                WriteEl(w, "BuyerDocument", isNpwp ? "TIN" : "National ID");
                var country = (buyer?.Country ?? "").Trim();
                WriteEl(w, "BuyerCountry",
                    country.Length > 0 && iso3.TryGetValue(country, out var iso) ? iso : "IDN");
                WriteEl(w, "BuyerDocumentNumber", isNpwp ? "-" : Digits(buyer?.Nik is { Length: > 0 } nk ? nk : r.NIK));
                WriteEl(w, "BuyerName", CommaClean(bName));
                WriteEl(w, "BuyerAdress", bAddr);
                WriteEl(w, "BuyerEmail", bEmail);
                WriteEl(w, "BuyerIDTKU", isNpwp ? Digits(buyer?.Idtku) : "000000");

                // Daftar barang/jasa
                w.WriteStartElement("ListOfGoodService");
                foreach (var g in BuildGoodServices(r, jenis, lineMap))
                {
                    w.WriteStartElement("GoodService");
                    WriteEl(w, "Opt", g.Opt);
                    WriteEl(w, "Code", "000000");
                    WriteEl(w, "Name", g.Name);
                    WriteEl(w, "Unit", g.Unit);
                    w.WriteElementString("Price", Dec(g.Price));
                    w.WriteElementString("Qty", Dec(g.Qty));
                    w.WriteElementString("TotalDiscount", Int(g.Discount));
                    w.WriteElementString("TaxBase", Int(g.TaxBase));
                    w.WriteElementString("OtherTaxBase", Int(g.OtherTaxBase));
                    w.WriteElementString("VATRate", VatRatePct.ToString(CultureInfo.InvariantCulture));
                    w.WriteElementString("VAT", Int(g.Vat));
                    w.WriteElementString("STLGRate", "0");
                    w.WriteElementString("STLG", "0");
                    w.WriteEndElement(); // GoodService
                }
                w.WriteEndElement(); // ListOfGoodService

                w.WriteEndElement(); // TaxInvoice
            }

            w.WriteEndElement(); // ListOfTaxInvoice
            w.WriteEndElement(); // TaxInvoiceBulk
            w.WriteEndDocument();
        }

        // GetString TIDAK membuang BOM: byte EF BB BF di awal stream terbaca jadi
        // karakter U+FEFF, lalu Blob di sisi JS meng-encode-nya kembali menjadi
        // EF BB BF -> file .xml yang terunduh tetap ber-BOM UTF-8.
        return (new UTF8Encoding(false).GetString(ms.ToArray()), null);
    }

    private sealed record GoodRow(string Opt, string Name, string Unit,
        decimal Price, decimal Qty, decimal Discount, decimal TaxBase, decimal OtherTaxBase, decimal Vat);

    /// <summary>Bangun baris GoodService untuk satu faktur (meniru rumus SP EFAKTUR).</summary>
    private static List<GoodRow> BuildGoodServices(
        FakturKeluaranRow r, string jenis, Dictionary<int, List<XmlLine>> lineMap)
    {
        var result = new List<GoodRow>();

        if (lineMap.TryGetValue(r.DocEntry, out var lines) && lines.Count > 0)
        {
            var sumLine = lines.Sum(l => l.LineTotal);

            // ── NONAKTIF ────────────────────────────────────────────────────────
            // Versi lama: baris TERAKHIR menyerap selisih pembulatan agar
            // Σ Total DPP = DPP header (U_DocTotal - U_VatSum + U_Discount dari
            // SAP_WEB), meniru SP __IDUFAKTURPAJAK_EFAKTUR. Diganti dengan
            // FLOOR(INV1.LineTotal) apa adanya. Disimpan bila perlu diaktifkan lagi.
            //
            //   var headerDpp = Math.Floor(r.DocTotal - r.VatSum + r.Discount);
            //
            //   var tmp = new List<(XmlLine L, decimal Bef, decimal Disc)>();
            //   foreach (var l in lines)
            //   {
            //       var bef = Math.Floor(l.LineTotal);
            //       var disc = sumLine == 0 ? 0 : Math.Floor(r.Discount * (l.LineTotal / sumLine));
            //       if (disc < 0) disc = 0;
            //       tmp.Add((l, bef, disc));
            //   }
            //
            //   var sumBef = tmp.Sum(t => t.Bef);
            //   for (var i = 0; i < tmp.Count; i++)
            //   {
            //       var (l, bef, disc) = tmp[i];
            //       var totalDpp = bef;
            //       if (i == tmp.Count - 1 && sumBef != headerDpp && jenis != "07" && jenis != "08")
            //           totalDpp = bef + (headerDpp - sumBef);
            //
            //       var aftdisc = bef - disc;
            //       var (taxBase, otherTaxBase, vat) = TaxValues(jenis, totalDpp, aftdisc, l.LineTotal, l.VatPrcnt);
            //       result.Add(new GoodRow(
            //           l.DocType == "S" ? "B" : "A",
            //           l.Desc ?? "", l.Unit ?? "",
            //           l.Price, l.Qty, disc, taxBase, otherTaxBase, vat));
            //   }
            // ────────────────────────────────────────────────────────────────────

            foreach (var l in lines)
            {
                // Total DPP baris = FLOOR(INV1.LineTotal) apa adanya, tanpa
                // penyesuaian pembulatan di baris terakhir.
                var totalDpp = Math.Floor(l.LineTotal);
                var disc = sumLine == 0 ? 0 : Math.Floor(r.Discount * (l.LineTotal / sumLine));
                if (disc < 0) disc = 0;
                var aftdisc = totalDpp - disc;

                var (taxBase, otherTaxBase, vat) = TaxValues(jenis, totalDpp, aftdisc, l.LineTotal, l.VatPrcnt);
                result.Add(new GoodRow(
                    l.DocType == "S" ? "B" : "A",
                    l.Desc ?? "", l.Unit ?? "",
                    l.Price, l.Qty, disc, taxBase, otherTaxBase, vat));
            }
        }
        else
        {
            // Fallback (DP / tanpa baris item): satu GoodService ringkas dari grid.
            var dpp = Math.Floor(r.DocTotal - r.VatSum);
            var (taxBase, otherTaxBase, vat) = TaxValues(jenis, dpp, dpp, dpp, 11m);
            result.Add(new GoodRow("A", r.CardName ?? "Penjualan", "UM.0018",
                dpp, 1m, Math.Floor(r.Discount), taxBase, otherTaxBase, vat));
        }

        return result;
    }

    /// <summary>TaxBase / OtherTaxBase / VAT per baris sesuai jenis pajak (SP EFAKTUR).</summary>
    private static (decimal TaxBase, decimal Other, decimal Vat) TaxValues(
        string jenis, decimal totalDpp, decimal aftdisc, decimal lineTotal, decimal vatPrcnt)
    {
        // 11/12 dipakai untuk DPP Nilai Lain (selain 01 & 05).
        const decimal ratio = 11m / 12m;
        if (jenis == "01")
        {
            // VB.NET: ROUND(11/12 × DPP × VATRate/100). VATRate = 11.
            var vat = Math.Round(ratio * totalDpp * VatRatePct / 100m, MidpointRounding.AwayFromZero);
            return (totalDpp, totalDpp, vat);
        }
        if (jenis == "05")
        {
            var vat = Math.Round(aftdisc * 1.1m / 100m, MidpointRounding.AwayFromZero);
            return (aftdisc, aftdisc, vat);
        }
        // Default (04 dll): OtherTaxBase = ROUND(DPP*11/12); VAT = floor per baris.
        var other = Math.Round(totalDpp * ratio, MidpointRounding.AwayFromZero);
        var vatDef = vatPrcnt > 0 ? Math.Floor(lineTotal * vatPrcnt / 100m) : Math.Floor(lineTotal * 11m / 100m);
        return (totalDpp, other, vatDef);
    }

    /// <summary>Peta kode negara 2-huruf → ISO-3 dari tabel ISO3CountryCode (SAP_WEB).</summary>
    private async Task<Dictionary<string, string>> GetIso3MapAsync()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var r = await _web.RunQueryAsync("SELECT Code, ISO3Code FROM ISO3CountryCode");
            if (r.Error is null)
            {
                var idx = ColIndex(r);
                foreach (var row in r.Rows)
                {
                    var code = Cell(row, idx, "Code")?.ToString()?.Trim();
                    var iso = Cell(row, idx, "ISO3Code")?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(iso))
                        map[code] = iso;
                }
            }
        }
        catch { /* abaikan -> fallback IDN */ }
        return map;
    }

    private async Task<string> GetCompanyTinAsync(CancellationToken ct)
    {
        try
        {
            var sql = "SELECT REPLACE(REPLACE(REPLACE(T5.\"TaxIdNum\",'.',''),'-',''),' ','') \"TIN\" FROM \"OADM\" T5";
            var r = await _sap.RunRawQueryAsync(sql, ct);
            if (r.Error is null && r.Rows.Count > 0)
                return Cell(r.Rows[0], ColIndex(r), "TIN")?.ToString() ?? "";
        }
        catch { /* abaikan */ }
        return "";
    }

    private static string FirstNonEmpty(params string?[] vals)
        => vals.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";

    // Tulis elemen teks: value hanya ditulis bila tidak kosong (meniru VB.NET).
    private static void WriteEl(XmlWriter w, string name, string? value)
    {
        w.WriteStartElement(name);
        if (!string.IsNullOrEmpty(value)) w.WriteString(value);
        w.WriteEndElement();
    }

    private async Task<(string Name, string Addr, string City, string Signer)> GetCompanyAsync(CancellationToken ct)
    {
        try
        {
            var sql =
                "SELECT REPLACE(T5.\"CompnyName\",',',' ') \"CompnyName\", " +
                "REPLACE(T5.\"CompnyAddr\",',',' ') \"CompnyAddr\", " +
                "REPLACE(IFNULL(T6.\"City\",''),',',' ') \"City\", " +
                "IFNULL(T5.\"U_IDU_Signer\",'') \"Signer\" " +
                "FROM \"OADM\" T5 CROSS JOIN \"ADM1\" T6";
            var r = await _sap.RunRawQueryAsync(sql, ct);
            if (r.Error is null && r.Rows.Count > 0)
            {
                var idx = ColIndex(r);
                var row = r.Rows[0];
                return (Cell(row, idx, "CompnyName")?.ToString() ?? "",
                        Cell(row, idx, "CompnyAddr")?.ToString() ?? "",
                        Cell(row, idx, "City")?.ToString() ?? "",
                        Cell(row, idx, "Signer")?.ToString() ?? "");
            }
        }
        catch { /* abaikan -> perusahaan kosong */ }
        return ("", "", "", "");
    }

    // ── Helper ────────────────────────────────────────────────────────────────
    private static Dictionary<string, int> ColIndex(QueryResult r)
    {
        var idx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < r.Columns.Count; i++) idx[r.Columns[i]] = i;
        return idx;
    }
    private static object? Cell(object?[] row, Dictionary<string, int> idx, string col)
        => idx.TryGetValue(col, out var i) && i < row.Length ? row[i] : null;

    private static int ToInt(object? v) => v is null ? 0 : int.TryParse(v.ToString(), out var i) ? i : 0;
    private static decimal ToDec(object? v) => v is null ? 0m
        : decimal.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;

    private static string Q(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
    private static string Int(decimal d) => Math.Round(d, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
    private static string Dec(decimal d) => d.ToString("0.##", CultureInfo.InvariantCulture);
    private static string OneLine(string? s) => (s ?? "").Replace("\r", " ").Replace("\n", " ").Replace(",", " ");
    private static string? CommaClean(string? s) => s?.Replace(",", " ");
    private static string Digits(string? s) => new string((s ?? "").Where(c => char.IsDigit(c)).ToArray());

    // NAMA (baris FK): nama NPWP bila customer ber-NPWP, kosong bila cash/tanpa
    // NPWP — mengikuti output SAP. NPWP dianggap "ada" bila memuat digit selain 0.
    private static string NamaForFk(FakturKeluaranRow r)
    {
        var npwp = Digits(r.NPWP);
        var hasNpwp = npwp.Length > 0 && npwp.Any(c => c != '0');
        return hasNpwp ? (CommaClean(r.NamaNPWP) ?? "") : "";
    }
}
