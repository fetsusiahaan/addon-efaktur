CREATE PROCEDURE __IDUFAKTURPAJAK_EFAKTUR
(
	in DocEntry INT
)
AS
BEGIN
	SELECT * FROM (
		SELECT			
			"Num", "DocNum", "U_IDU_JenisPajak", "U_IDU_Pengganti", "U_IDU_No_Pajak", "masa", "tahun", 
			"DocDate", "LicTradNum", "CardName", "Address", "ppnbm", "U_IDU_KetTambah",
			"U_IDU_UANGMUKA", "dp dpp", "dp ppn", "dp ppnbm", "U_IDU_Referensi", "CompnyAddr", "CompnyName", 
			"ZipCode", "TaxIdNum", "City", "U_IDU_Signer", "ItemCode", "Dscription", "PriceBefDi", "OpenQty" "Quantity", 
			"QtyTotal",
			FLOOR(SUM("Total DPP") OVER(PARTITION BY "DocNum" ORDER BY "DocNum")) "dpp",
			FLOOR(SUM("Total PPN") OVER(PARTITION BY "DocNum" ORDER BY "DocNum")) "VatSum",
			/*CASE WHEN max("Num") OVER (PARTITION BY "DocNum") = "Num" THEN
				CASE WHEN sum("linetotal befdisc") OVER(PARTITION BY "DocNum" ORDER BY "DocNum") <> "dpp" THEN
					"linetotal befdisc" + ("dpp" - sum("linetotal befdisc") OVER(PARTITION BY "DocNum" ORDER BY "DocNum"))
				ELSE
					"linetotal befdisc"
				END
			ELSE
				"linetotal befdisc"
			END*/ "linetotal befdisc", 		
			/*CASE WHEN max("Num") OVER (PARTITION BY "DocNum") = "Num" THEN
				CASE WHEN sum("linetotal befdisc" - "linedisc") OVER (PARTITION BY "DocNum" ORDER BY "DocNum") <> "dpp" THEN
					CASE WHEN "U_IDU_JenisPajak"='07' OR "U_IDU_JenisPajak"='08' THEN
						"linedisc"
					ELSE
						"linedisc" + ("dpp" - (sum("linetotal befdisc" - "linedisc") OVER (PARTITION BY "DocNum" ORDER BY "DocNum")))
					END
				ELSE
					"linedisc"
				END
			ELSE
				"linedisc"
			END*/ "linedisc",
			"U_IDU_NIK",
			"U_IDU_KDP",
			/*CASE WHEN max("Num") OVER (PARTITION BY "DocNum") = "Num" THEN
				CASE WHEN sum("Total DPP") OVER (PARTITION BY "DocNum" ORDER BY "DocNum") <> "dpp" THEN
					CASE WHEN "U_IDU_JenisPajak"='07' OR "U_IDU_JenisPajak"='08' THEN
						"Total DPP"
					ELSE
						"Total DPP" + ("dpp" - sum("Total DPP") OVER (PARTITION BY "DocNum" ORDER BY "DocNum"))
					END
				ELSE
					"Total DPP"
				END
			ELSE
				"Total DPP"
			END*/ "Total DPP",

			ROUND(
				CASE WHEN MAX("Num") OVER (PARTITION BY "DocNum") = "Num" THEN
					CASE WHEN SUM("Total PPN") OVER(PARTITION BY "DocNum" ORDER BY "DocNum") <> "VatSum" THEN
						"Total PPN" + ("VatSum" - SUM("Total PPN") OVER(PARTITION BY "DocNum" ORDER BY "DocNum"))	
					ELSE
						"Total PPN"				
					END
				ELSE
					"Total PPN"
				END
			,0) "Total PPN",
			
			"BuyerCountry", "BuyerEmail", "DocType", "U_IDU_UMCode" "UMCode", "VATRate" "U_IDU_RatePajak",
			"BuyerDocument", "BuyerDocumentNumber", "BuyerTin", "BuyerName", "BuyerAdress", "BuyerIDTKU", "AddInfo", "FacilityStamp",
			"CodeBarangJasa", "VATRate", "DocNum" "RefDesc",

			CASE WHEN "U_IDU_JenisPajak" = '01' THEN
				"Total DPP"
			ELSE
				CASE WHEN "U_IDU_JenisPajak" = '05' THEN
					"linetotal aftdisc"
				ELSE
					"Total DPP"
				END
			END "TaxBase",

			CASE WHEN "U_IDU_JenisPajak" = '01' THEN
				"Total DPP"
			ELSE
				CASE WHEN "U_IDU_JenisPajak" = '05' THEN
					"linetotal aftdisc"
				ELSE
					ROUND(((11 / 12) * "Total DPP"),0)
				END
			END "OtherTaxBase",

			CASE WHEN "U_IDU_JenisPajak" = '01' THEN
				ROUND(((11 / 12) * "Total DPP" * "VATRate" / 100),0)
			ELSE
				CASE WHEN "U_IDU_JenisPajak" = '05' THEN
					ROUND(("Total DPP" * 1.1 / 100),0)
				ELSE
					ROUND(((11 / 12) * "Total DPP" * "VATRate" / 100),0)
				END
			END "VAT",
			
			--Rumus lain jika gagal perkalian pecahan:
			--ROUND((CAST(11.0/12.0 AS DECIMAL(21,6)) * "Total DPP"),0) "OtherTaxBase",
			--ROUND((CAST(11.0/12.0 AS DECIMAL(21,6)) * "Total DPP" * "VATRate" / 100),0) "VAT",			
			
			"TaxIdNum" "TIN",
			CONCAT("TaxIdNum",'000000') "SellerIDTKU",
			
			"U_IDU_JenisPajak" "TrxCode",
			--'04' "TrxCode"

			"DocNum" "CustomDoc",
			'' "CustomDocMonthYear"

		FROM
		(
		SELECT 
			row_number() OVER(PARTITION BY t0."U_EntryINV", t0."U_DocType" ORDER BY t0."VisOrder") "Num",	
			/* Baris 1 - FK */
			T0."U_NumINV" "DocNum", t0."U_IDU_JenisPajak", T0."U_IDU_Pengganti",
			replace(replace(t0."U_NoFP",'.',''),'-','') "U_IDU_No_Pajak",
			month(t0."U_DateINV") "masa", year(t0."U_DateINV") "tahun",
			t0."U_DateINV" "DocDate",
			replace(replace(replace(t0."U_IDU_NPWP",'.',''),'-',''),' ','') "LicTradNum", 
			
			CASE WHEN replace(replace(replace(t0."U_IDU_NPWP",'.',''),'-',''),' ','')='' OR replace(replace(replace(t0."U_IDU_NPWP",'.',''),'-',''),' ','')='0' OR replace(replace(replace(t0."U_IDU_NPWP",'.',''),'-',''),' ','')='000000000000000' OR replace(replace(replace(t0."U_IDU_NPWP",'.',''),'-',''),' ','')=null THEN
				CONCAT(CONCAT(t7."U_IDU_NIK",'#NIK#NAMA#'),replace(t0."U_IDU_NamaNPWP",',', ' '))
			ELSE
				replace(t0."U_IDU_NamaNPWP",',', ' ')
			END "CardName",

			--replace(t0."U_IDU_NamaNPWP",',', ' ') "CardName", 
			replace(t0."U_IDU_AlmtNPWP", ',', ' ') "Address", 
			/*
			CASE WHEN t0."U_IDU_JenisPajak"='07' OR t0."U_IDU_JenisPajak"='08' THEN
				FLOOR(t0."U_DocTotal"+t0."U_Discount")
			ELSE
				FLOOR(t0."U_DocTotal"-t0."U_VatSum"+t0."U_Discount")
			END "dpp",
			*/
			CASE WHEN t0."U_IDU_JenisPajak"='07' OR t0."U_IDU_JenisPajak"='08' THEN
				FLOOR((t0."U_DocTotal"+t0."U_Discount") * ifnull(CASE WHEN t0."U_IDU_RatePajak"='' THEN 11 ELSE t0."U_IDU_RatePajak" END,11)/100)
			ELSE
				FLOOR((t0."U_DocTotal"-t0."U_VatSum"+t0."U_Discount")*(t1."VatPrcnt"/100))
			END "VatSum",
			
			0 "ppnbm",

			SUBSTR(T0."U_IDU_KetTambah",LOCATE(T0."U_IDU_KetTambah",'.')+1) "U_IDU_KetTambah",

			t0."U_JDP" "U_IDU_UANGMUKA",

			CASE WHEN t0."U_IDU_JenisPajak"='07' OR t0."U_IDU_JenisPajak"='08' THEN
				CASE WHEN ifnull(t0."U_DP",0) = 0 THEN
					0
				ELSE
					FLOOR(ifnull((t1."LineTotal"),0) - ifnull(t0."U_DP",0))
				END
			ELSE
				ifnull(FLOOR((t0."U_DocTotal"-t0."U_VatSum"+t0."U_DP") - t3."DrawnSum") ,0)
			END "dp dpp",

			CASE WHEN t0."U_IDU_JenisPajak"='07' OR t0."U_IDU_JenisPajak"='08' THEN
				--ifnull(t0.U_DP * (ifnull(t0."U_IDU_RatePajak",11)/100),0)

				CASE WHEN ifnull(t0."U_DP",0) = 0 THEN
					0
				ELSE
					FLOOR((t0."U_DocTotal"+t0."U_Discount")*ifnull(CASE WHEN t0."U_IDU_RatePajak"='' THEN 11 ELSE t0."U_IDU_RatePajak" END,11)/100)
				END
			ELSE
				ifnull(FLOOR(((t0."U_DocTotal"-t0."U_VatSum"+t0."U_DP")*(t1."VatPrcnt"/100)) - t3."Vat"),0)
			END "dp ppn",

			0 "dp ppnbm",
			cast(t0."U_IDU_Referensi" as nvarchar(254)) "U_IDU_Referensi",
			t0."U_IDU_KDP",
	
			/* Baris 2 - FAPR */
			replace(t5."CompnyName",',', ' ') AS "CompnyName", replace(t5."CompnyAddr",',', ' ') AS "CompnyAddr", t6."ZipCode", 
			replace(replace(replace(t5."TaxIdNum",'.',''),'-',''),' ','') "TaxIdNum", replace(t6."City",',', ' ') AS "City", t5."U_IDU_Signer",
			
			/* Baris 3 - OF */
			t1."ItemCode",
			
			REPLACE(
				REPLACE(
					REPLACE(
						REPLACE(
							t1."Dscription", CHAR(34), ''
						), CHAR(39), ''
					), CHAR(44), '.'
				), CHAR(59), '.'
			) "Dscription",

			--replace(replace(t1."Dscription",char(34),''),char(39),'') "Dscription",
			
			CASE WHEN T1."Rate" > 0 THEN
				ROUND(CAST(T1."PriceBefDi" AS DECIMAL(19,2)) * T1."Rate",0)
			ELSE
				CAST(T1."PriceBefDi" AS DECIMAL(19,2))
			END "PriceBefDi", 

			CASE WHEN T9."DocType" = 'S' THEN
				1
			ELSE
				T1."OpenQty"
			END "OpenQty",

			sum(t1."OpenQty") OVER (PARTITION BY "U_NumINV" ORDER BY "U_NumINV") "QtyTotal",
			FLOOR(ifnull(((t1."LineTotal")-(t0."U_Discount"/sum(ifnull(t1."LineTotal",0)) OVER (PARTITION BY "U_NumINV" ORDER BY "U_NumINV")*t1."LineTotal")),0)) "linetotal aftdisc",
			FLOOR(ifnull((t1."LineTotal"),0)) "linetotal befdisc",
			--0 "linedisc",
			
			FLOOR(t0."U_Discount"*(t1."LineTotal"/sum(ifnull(t1."LineTotal",0)) OVER (PARTITION BY "U_NumINV" ORDER BY "U_NumINV"))) "linedisc",

			--FLOOR(ifnull((t1."LineTotal"),0)) "Total DPP",

			FLOOR(ifnull((t1."LineTotal"),0))-FLOOR(t0."U_Discount"*(t1."LineTotal"/sum(ifnull(t1."LineTotal",0)) OVER (PARTITION BY "U_NumINV" ORDER BY "U_NumINV"))) "Total DPP",
			FLOOR((FLOOR(ifnull((t1."LineTotal"),0))-FLOOR(t0."U_Discount"*(t1."LineTotal"/sum(ifnull(t1."LineTotal",0)) OVER (PARTITION BY "U_NumINV" ORDER BY "U_NumINV"))))*t0."U_IDU_RatePajak"/100) "Total PPN",

			t7."U_IDU_NIK",

			IFNULL(t18."ISO3Code",'IDN') "BuyerCountry", t9."DocType", 

			CASE WHEN T9."DocType"='S' THEN
				'UM.0018'
			ELSE
				T19."U_IDU_UMCode"
			END "U_IDU_UMCode",
			
			'000000' as "CodeBarangJasa", CASE WHEN t0."U_IDU_RatePajak"='' THEN 12 ELSE IFNULL(t0."U_IDU_RatePajak",12) END as "VATRate",

			CASE WHEN T7."U_IDU_JenisCustomer"='01' THEN
				CASE WHEN T7."U_IDU_JenisKantor"='01' THEN
					'TIN'
				ELSE
					'TIN'
				END
			ELSE
				'National ID'
			END "BuyerDocument",

			CASE WHEN T7."U_IDU_JenisCustomer"='01' THEN
				CASE WHEN T7."U_IDU_JenisKantor"='01' THEN
					replace(replace(replace(replace(t0."U_IDU_NPWP",'.',''),'-',''),' ',''),',',':')
				ELSE
					replace(replace(replace(replace(t0."U_IDU_NPWP",'.',''),'-',''),' ',''),',',':')
				END
			ELSE
				'0000000000000000'
			END "BuyerTin",

			CASE WHEN T7."U_IDU_JenisCustomer"='01' THEN
				CASE WHEN T7."U_IDU_JenisKantor"='01' THEN
					'-'
				ELSE
					'-'
				END
			ELSE
				CASE WHEN T7."U_IDU_JenisCash"='01' THEN
					replace(replace(replace(replace(t8."U_IDU_NIK",'.',''),'-',''),' ',''),',',':')
				ELSE
					replace(replace(replace(replace(t7."U_IDU_NIK",'.',''),'-',''),' ',''),',',':')
				END
			END "BuyerDocumentNumber",

			CASE WHEN T7."U_IDU_JenisCustomer"='01' THEN
				CASE WHEN T7."U_IDU_JenisKantor"='01' THEN
					replace(replace(replace(replace(t7."U_IDU_IDTKU",'.',''),'-',''),' ',''),',',':')
				ELSE
					replace(replace(replace(replace(t7."U_IDU_IDTKU",'.',''),'-',''),' ',''),',',':')
				END
			ELSE
				'000000'
			END "BuyerIDTKU",

			CASE WHEN T7."U_IDU_JenisCustomer"='01' THEN
				CASE WHEN T7."U_IDU_JenisKantor"='01' THEN
					T7."U_IDU_NamaNPWP"
				ELSE
					T7."U_IDU_NamaNPWP"
				END
			ELSE
				CASE WHEN T7."U_IDU_JenisCash"='01' THEN
					T8."Name"
				ELSE
					IFNULL(T7."U_IDU_NamaNPWP",T8."Name")
				END
			END "BuyerName",

			CASE WHEN T7."U_IDU_JenisCustomer"='01' THEN
				CASE WHEN T7."U_IDU_JenisKantor"='01' THEN
					T7."U_IDU_AlmtNPWP"
				ELSE
					T7."U_IDU_AlmtNPWP"
				END
			ELSE
				CASE WHEN T7."U_IDU_JenisCash"='01' THEN
					T8."Address"
				ELSE
					IFNULL(T7."U_IDU_AlmtNPWP",T8."Address")
				END
			END "BuyerAdress",

			CASE WHEN T7."U_IDU_JenisCustomer"='01' THEN
				CASE WHEN T7."U_IDU_JenisKantor"='01' THEN
					T7."E_Mail"
				ELSE
					T7."E_Mail"
				END
			ELSE
				CASE WHEN T7."U_IDU_JenisCash"='01' THEN
					T8."E_MailL"
				ELSE
					IFNULL(T7."E_Mail",T8."E_MailL")
				END
			END "BuyerEmail",

			CASE WHEN t0."U_IDU_JenisPajak"='07' THEN
				T9."U_IDU_AddInfo_07"
			ELSE
				CASE WHEN t0."U_IDU_JenisPajak"='08' THEN
					T9."U_IDU_AddInfo_08"
				ELSE
					''
				END
			END "AddInfo",

			CASE WHEN t0."U_IDU_JenisPajak"='07' THEN
				T9."U_IDU_FacilityStamp_07"
			ELSE
				CASE WHEN t0."U_IDU_JenisPajak"='08' THEN
					T9."U_IDU_FacilityStamp_08"
				ELSE
					''
				END
			END "FacilityStamp"

		FROM "@IDU_PAJAK_TRANS_DET" t0
		JOIN oinv t9 ON t9."DocEntry"=t0."U_EntryINV"
		JOIN ocrd t7 on t7."CardCode"=t0."U_BPCode"
		JOIN "ISO3CountryCode" t18 on t18."Code"=t7."Country"
		JOIN inv1 t1 on t0."U_EntryINV" = t1."DocEntry" and t0."U_DocType" = T1."ObjType"
		LEFT JOIN ouom t19 on t19."UomCode"=t1."UomCode"
		LEFT JOIN ocpr t8 ON t8."CardCode" = t0."U_BPCode" AND t8."CntctCode" = t9."CntctCode"
		LEFT JOIN oitm t2 on t1."ItemCode" = t2."ItemCode"
		LEFT JOIN inv9 t3 on t0."U_EntryINV" = t3."DocEntry"
		LEFT JOIN odpi t4 on t3."BaseAbs" = t4."DocEntry"
		cross JOIN oadm t5
		cross JOIN adm1 t6
		WHERE t0."DocEntry" = :docentry and t1."LineTotal" > 0
		) A
	UNION
		SELECT
			"Num", "DocNum", "U_IDU_JenisPajak", "U_IDU_Pengganti", "U_IDU_No_Pajak", "masa", "tahun", 
			"DocDate", "LicTradNum", "CardName", "Address", "ppnbm", "U_IDU_KetTambah",
			"U_IDU_UANGMUKA", "dp dpp", "dp ppn", "dp ppnbm", "U_IDU_Referensi", "CompnyAddr", "CompnyName", 
			"ZipCode", "TaxIdNum", "City", "U_IDU_Signer", "ItemCode", "Dscription", "PriceBefDi", "OpenQty" "Quantity", 
			"QtyTotal",
			FLOOR(sum("Total DPP") OVER(PARTITION BY "DocNum" ORDER BY "DocNum")) "dpp",
			FLOOR(sum("Total PPN") OVER(PARTITION BY "DocNum" ORDER BY "DocNum")) "VatSum",
			/*CASE WHEN max("Num") OVER (PARTITION BY "DocNum") = "Num" THEN
				CASE WHEN sum("linetotal befdisc") OVER(PARTITION BY "DocNum" ORDER BY "DocNum") <> "dpp" THEN
					"linetotal befdisc" + ("dpp" - sum("linetotal befdisc") OVER(PARTITION BY "DocNum" ORDER BY "DocNum"))
				ELSE
					"linetotal befdisc"
				END
			ELSE
				"linetotal befdisc"
			END*/ "linetotal befdisc",
			"linedisc",
			"U_IDU_NIK",
			"U_IDU_KDP",
			/*CASE WHEN max("Num") OVER (PARTITION BY "DocNum") = "Num" THEN
				CASE WHEN sum("Total DPP") OVER (PARTITION BY "DocNum" ORDER BY "DocNum") <> "dpp" THEN
					CASE WHEN "U_IDU_JenisPajak"='07' OR "U_IDU_JenisPajak"='08' THEN
						"Total DPP"
					ELSE
						"Total DPP" + ("dpp" - sum("Total DPP") OVER (PARTITION BY "DocNum" ORDER BY "DocNum"))
					END
				ELSE
					"Total DPP"
				END
			ELSE
				"Total DPP"
			END*/ "Total DPP",
			/*CASE WHEN max("Num") OVER (PARTITION BY "DocNum") = "Num" THEN
				CASE WHEN sum("Total PPN") OVER(PARTITION BY "DocNum" ORDER BY "DocNum") <> "VatSum" THEN
					"Total PPN" + ("VatSum" - sum("Total PPN") OVER(PARTITION BY "DocNum" ORDER BY "DocNum"))	
				ELSE
					"Total PPN"				
				END
			ELSE
				"Total PPN"
			END */"Total PPN",
			
			"BuyerCountry", "BuyerEmail", "DocType", "U_IDU_UMCode" "UMCode", "VATRate" "U_IDU_RatePajak",
			"BuyerDocument", "BuyerDocumentNumber", "BuyerTin", "BuyerName", "BuyerAdress", "BuyerIDTKU", "AddInfo", "FacilityStamp",
			"CodeBarangJasa", "VATRate", "DocNum" "RefDesc",

			CASE WHEN "U_IDU_JenisPajak" = '01' THEN
				"Total DPP"
			ELSE
				CASE WHEN "U_IDU_JenisPajak" = '05' THEN
					"linetotal aftdisc"
				ELSE
					"Total DPP"
				END
			END "TaxBase",

			CASE WHEN "U_IDU_JenisPajak" = '01' THEN
				"Total DPP"
			ELSE
				CASE WHEN "U_IDU_JenisPajak" = '05' THEN
					"linetotal aftdisc"
				ELSE
					ROUND(((11 / 12) * "Total DPP"),0)
				END
			END "OtherTaxBase",

			CASE WHEN "U_IDU_JenisPajak" = '01' THEN
				ROUND(((11 / 12) * "Total DPP" * "VATRate" / 100),0)
			ELSE
				CASE WHEN "U_IDU_JenisPajak" = '05' THEN
					ROUND(("Total DPP" * 1.1 / 100),0)
				ELSE
					ROUND(((11 / 12) * "Total DPP" * "VATRate" / 100),0)
				END
			END "VAT",
			
			--Rumus lain jika gagal perkalian pecahan:
			--ROUND((CAST(11.0/12.0 AS DECIMAL(21,6)) * "Total DPP"),0) "OtherTaxBase",
			--ROUND((CAST(11.0/12.0 AS DECIMAL(21,6)) * "Total DPP" * "VATRate" / 100),0) "VAT",
			
			"TaxIdNum" "TIN",
			CONCAT("TaxIdNum",'000000') "SellerIDTKU",
			
			"U_IDU_JenisPajak" "TrxCode",
			--'04' "TrxCode"

			"DocNum" "CustomDoc",
			'' "CustomDocMonthYear"

		FROM
		(	
		SELECT 
			row_number() OVER(PARTITION BY t0."U_EntryINV", t0."U_DocType" ORDER BY t0."VisOrder") "Num",	
			/* Baris 1 - FK */
			T0."U_NumINV" "DocNum", t0."U_IDU_JenisPajak", T0."U_IDU_Pengganti",
			replace(replace(t0."U_NoFP",'.',''),'-','') "U_IDU_No_Pajak",
			month(t0."U_DateINV") "masa", year(t0."U_DateINV") "tahun",
			t0."U_DateINV" "DocDate",
			replace(replace(replace(t0."U_IDU_NPWP",'.',''),'-',''),' ','') "LicTradNum", 
			
			CASE WHEN replace(replace(replace(t0."U_IDU_NPWP",'.',''),'-',''),' ','')='' OR replace(replace(replace(t0."U_IDU_NPWP",'.',''),'-',''),' ','')='0' OR replace(replace(replace(t0."U_IDU_NPWP",'.',''),'-',''),' ','')='000000000000000' OR replace(replace(replace(t0."U_IDU_NPWP",'.',''),'-',''),' ','')=null THEN
				CONCAT(CONCAT(t7."U_IDU_NIK",'#NIK#NAMA#'),replace(t0."U_IDU_NamaNPWP",',', ' '))
			ELSE
				replace(t0."U_IDU_NamaNPWP",',', ' ')
			END "CardName",
			
			--replace(t0."U_IDU_NamaNPWP",',', ' ') "CardName", 
			replace(t0."U_IDU_AlmtNPWP", ',', ' ') "Address", 

			CASE WHEN t0."U_IDU_JenisPajak"='07' OR t0."U_IDU_JenisPajak"='08' THEN
				FLOOR(t0."U_DocTotal"+t0."U_Discount")
			ELSE
				FLOOR(t0."U_DocTotal"-t0."U_VatSum"+t0."U_Discount")
			END "dpp",
			
			CASE WHEN t0."U_IDU_JenisPajak"='07' OR t0."U_IDU_JenisPajak"='08' THEN
				FLOOR((t0."U_DocTotal" * ifnull(CASE WHEN t0."U_IDU_RatePajak"='' THEN 11 ELSE t0."U_IDU_RatePajak" END,11)/100))
			ELSE
				FLOOR((t0."U_DocTotal"-t0."U_VatSum"+t0."U_DP")*(t1."VatPrcnt"/100))
			END "VatSum",
			
			0 "ppnbm",
			
			SUBSTR(T0."U_IDU_KetTambah",LOCATE(T0."U_IDU_KetTambah",'.')+1) "U_IDU_KetTambah",

			t0."U_JDP" "U_IDU_UANGMUKA",

			CASE WHEN t0."U_IDU_JenisPajak"='07' OR t0."U_IDU_JenisPajak"='08' THEN
				CASE WHEN ifnull(t0."U_DP",0) = 0 THEN
					0
				ELSE
					FLOOR(ifnull(t0."U_DocTotal",0))
				END
			ELSE
				ifnull(FLOOR(t0."U_DocTotal"-t0."U_VatSum"+t0."U_DP"),0)
			END "dp dpp",

			CASE WHEN t0."U_IDU_JenisPajak"='07' OR t0."U_IDU_JenisPajak"='08' THEN
				CASE WHEN ifnull(t0."U_DP",0) = 0 THEN
					0
				ELSE
					FLOOR((t0."U_DocTotal")*ifnull(CASE WHEN t0."U_IDU_RatePajak"='' THEN 11 ELSE t0."U_IDU_RatePajak" END,11)/100)
				END
			ELSE
				ifnull(FLOOR((t0."U_DocTotal"-t0."U_VatSum"+t0."U_DP")*(t1."VatPrcnt"/100)),0)
			END "dp ppn",
			
			0 "dp ppnbm",
			cast(t0."U_IDU_Referensi" as nvarchar(254)) "U_IDU_Referensi",
			t0."U_IDU_KDP",

			/* Baris 2 - FAPR */
			replace(t5."CompnyName",',', ' ') AS "CompnyName", replace(t5."CompnyAddr",',', ' ') AS "CompnyAddr", t6."ZipCode", 
			replace(replace(replace(t5."TaxIdNum",'.',''),'-',''),' ','') "TaxIdNum", replace(t6."City",',', ' ') AS "City", t5."U_IDU_Signer",
			
			/* Baris 3 - OF */
			t1."ItemCode",

			REPLACE(
				REPLACE(
					REPLACE(
						REPLACE(
							t1."Dscription", CHAR(34), ''
						), CHAR(39), ''
					), CHAR(44), '.'
				), CHAR(59), '.'
			) "Dscription",
			
			--t2."ItemCode", replace(replace(t1."Dscription",char(34),''),char(39),'') "Dscription", 
			
			CASE WHEN T1."Rate" > 0 THEN
				ROUND(CAST(T1."PriceBefDi" AS DECIMAL(19,2)) * T1."Rate",0)
			ELSE
				CAST(T1."PriceBefDi" AS DECIMAL(19,2))
			END "PriceBefDi", 
			
			CASE WHEN T9."DocType" = 'S' THEN
				1
			ELSE
				T1."OpenQty"
			END "OpenQty",
			
			sum(t1."OpenQty") OVER (PARTITION BY "U_NumINV" ORDER BY "U_NumINV") "QtyTotal",
			FLOOR(ifnull(((t1."LineTotal")-(t0."U_Discount"/sum(ifnull(t1."LineTotal",0)) OVER (PARTITION BY "U_NumINV" ORDER BY "U_NumINV")*t1."LineTotal")),0)) "linetotal aftdisc",
			FLOOR(ifnull((t1."Price"),0)) "linetotal befdisc",
			--0 AS "linedisc",
			FLOOR(t0."U_Discount"*(t1."LineTotal"/sum(ifnull(t1."LineTotal",0)) OVER (PARTITION BY "U_NumINV" ORDER BY "U_NumINV"))) AS "linedisc",
			--FLOOR(ifnull((t1."LineTotal"),0)) "Total DPP",

			FLOOR(ifnull((t1."LineTotal"),0))-FLOOR(t0."U_Discount"*(t1."LineTotal"/sum(ifnull(t1."LineTotal",0)) OVER (PARTITION BY "U_NumINV" ORDER BY "U_NumINV"))) "Total DPP",
			FLOOR((FLOOR(ifnull((t1."LineTotal"),0))-FLOOR(t0."U_Discount"*(t1."LineTotal"/sum(ifnull(t1."LineTotal",0)) OVER (PARTITION BY "U_NumINV" ORDER BY "U_NumINV"))))*t0."U_IDU_RatePajak"/100) "Total PPN",
			/*
			CASE WHEN t0."U_IDU_JenisPajak"='07' OR t0."U_IDU_JenisPajak"='08' THEN
				FLOOR((t1."LineTotal")*ifnull(CASE WHEN t0."U_IDU_RatePajak"='' THEN 11 ELSE t0."U_IDU_RatePajak" END,11)/100)
			ELSE
				FLOOR(ifnull((t1."LineTotal")*(t1."VatPrcnt"/100),0))
			END "Total PPN",
			*/
			t7."U_IDU_NIK",

			IFNULL(t18."ISO3Code",'IDN') "BuyerCountry", t9."DocType",
			
			CASE WHEN T9."DocType"='S' THEN
				'UM.0018'
			ELSE
				T19."U_IDU_UMCode"
			END "U_IDU_UMCode",
						
			'000000' as "CodeBarangJasa", CASE WHEN t0."U_IDU_RatePajak"='' THEN 12 ELSE IFNULL(t0."U_IDU_RatePajak",12) END as "VATRate",

			CASE WHEN T7."U_IDU_JenisCustomer"='01' THEN
				CASE WHEN T7."U_IDU_JenisKantor"='01' THEN
					'TIN'
				ELSE
					'TIN'
				END
			ELSE
				'National ID'
			END "BuyerDocument",

			CASE WHEN T7."U_IDU_JenisCustomer"='01' THEN
				CASE WHEN T7."U_IDU_JenisKantor"='01' THEN
					replace(replace(replace(replace(t0."U_IDU_NPWP",'.',''),'-',''),' ',''),',',':')
				ELSE
					replace(replace(replace(replace(t0."U_IDU_NPWP",'.',''),'-',''),' ',''),',',':')
				END
			ELSE
				'0000000000000000'
			END "BuyerTin",

			CASE WHEN T7."U_IDU_JenisCustomer"='01' THEN
				CASE WHEN T7."U_IDU_JenisKantor"='01' THEN
					'-'
				ELSE
					'-'
				END
			ELSE
				CASE WHEN T7."U_IDU_JenisCash"='01' THEN
					replace(replace(replace(replace(t8."U_IDU_NIK",'.',''),'-',''),' ',''),',',':')
				ELSE
					replace(replace(replace(replace(t7."U_IDU_NIK",'.',''),'-',''),' ',''),',',':')
				END
			END "BuyerDocumentNumber",

			CASE WHEN T7."U_IDU_JenisCustomer"='01' THEN
				CASE WHEN T7."U_IDU_JenisKantor"='01' THEN
					replace(replace(replace(replace(t7."U_IDU_IDTKU",'.',''),'-',''),' ',''),',',':')
				ELSE
					replace(replace(replace(replace(t7."U_IDU_IDTKU",'.',''),'-',''),' ',''),',',':')
				END
			ELSE
				'000000'
			END "BuyerIDTKU",

			CASE WHEN T7."U_IDU_JenisCustomer"='01' THEN
				CASE WHEN T7."U_IDU_JenisKantor"='01' THEN
					T7."U_IDU_NamaNPWP"
				ELSE
					T7."U_IDU_NamaNPWP"
				END
			ELSE
				CASE WHEN T7."U_IDU_JenisCash"='01' THEN
					T8."Name"
				ELSE
					IFNULL(T7."U_IDU_NamaNPWP",T8."Name")
				END
			END "BuyerName",

			CASE WHEN T7."U_IDU_JenisCustomer"='01' THEN
				CASE WHEN T7."U_IDU_JenisKantor"='01' THEN
					T7."U_IDU_AlmtNPWP"
				ELSE
					T7."U_IDU_AlmtNPWP"
				END
			ELSE
				CASE WHEN T7."U_IDU_JenisCash"='01' THEN
					T8."Address"
				ELSE
					IFNULL(T7."U_IDU_AlmtNPWP",T8."Address")
				END
			END "BuyerAdress",

			CASE WHEN T7."U_IDU_JenisCustomer"='01' THEN
				CASE WHEN T7."U_IDU_JenisKantor"='01' THEN
					T7."E_Mail"
				ELSE
					T7."E_Mail"
				END
			ELSE
				CASE WHEN T7."U_IDU_JenisCash"='01' THEN
					T8."E_MailL"
				ELSE
					IFNULL(T7."E_Mail",T8."E_MailL")
				END
			END "BuyerEmail",

			CASE WHEN t0."U_IDU_JenisPajak"='07' THEN
				T9."U_IDU_AddInfo_07"
			ELSE
				CASE WHEN t0."U_IDU_JenisPajak"='08' THEN
					T9."U_IDU_AddInfo_08"
				ELSE
					''
				END
			END "AddInfo",

			CASE WHEN t0."U_IDU_JenisPajak"='07' THEN
				T9."U_IDU_FacilityStamp_07"
			ELSE
				CASE WHEN t0."U_IDU_JenisPajak"='08' THEN
					T9."U_IDU_FacilityStamp_08"
				ELSE
					''
				END
			END "FacilityStamp"

		FROM "@IDU_PAJAK_TRANS_DET" t0
		JOIN ocrd t7 on t7."CardCode"=t0."U_BPCode"
		JOIN "ISO3CountryCode" t18 on t18."Code"=t7."Country"
		JOIN dpi1 t1 on t0."U_EntryINV" = t1."DocEntry" and t0."U_DocType" = T1."ObjType"
		JOIN odpi t9 on t9."DocEntry"=t1."DocEntry"
		LEFT JOIN ouom t19 on t19."UomCode"=t1."UomCode"
		LEFT JOIN ocpr t8 ON t8."CardCode" = t0."U_BPCode" AND t8."CntctCode" = t9."CntctCode"
		LEFT JOIN oitm t2 on t1."ItemCode" = t2."ItemCode"
		cross JOIN oadm t5
		cross JOIN adm1 t6
		WHERE t0."DocEntry" = :DocEntry and t1."LineTotal" > 0
		) B	
	) X
	ORDER BY X."DocNum", X."Num";
END;
