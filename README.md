# Add-On E-Faktur

Aplikasi web **Add-On E-Faktur** untuk **SAP Business One** — mengelola dokumen **Pajak Keluaran** (faktur pajak penjualan) berbasis data invoice SAP B1, lalu meng-*export* ke format **e-Faktur / Coretax** (CSV & XML) sesuai perilaku add-on VB.NET aslinya.

Dibangun dengan **Blazor Server (.NET 8)** dan komponen UI **[Radzen Blazor](https://blazor.radzen.com/)**. Antarmuka berbahasa Indonesia.

> Proyek internal **Intidatautama**.

---

## 📑 Daftar Isi

- [Arsitektur singkat](#-arsitektur-singkat)
- [Fitur](#-fitur)
- [Teknologi](#-teknologi)
- [Struktur proyek](#-struktur-proyek)
- [Autentikasi & otorisasi](#-autentikasi--otorisasi)
- [Dua sumber database](#-dua-sumber-database)
- [Alur Pajak Keluaran](#-alur-pajak-keluaran)
- [Export e-Faktur (CSV & XML)](#-export-e-faktur-csv--xml)
- [Menjalankan (development)](#-menjalankan-development)
- [Deploy ke IIS](#-deploy-ke-iis)
- [Konfigurasi](#-konfigurasi)
- [Stored Procedure](#-stored-procedure)
- [Keamanan](#-keamanan)

---

## 🏗️ Arsitektur singkat

Aplikasi menjembatani **dua database berbeda**:

| Peran | Database | Diakses via | Konfigurasi |
|---|---|---|---|
| **Sumber data SAP B1** (invoice, customer, item, perusahaan) | SAP Business One — **SAP HANA** *atau* **SQL Server** | `SapQueryService` | `ConnectDBAccess.json` (koneksi aktif) / per-user `KoneksiDb` |
| **Penyimpanan transaksi faktur** (`IDU_PAJAK_TRANS` + `_DET`) | **SQL Server** (`SAP_WEB`) | `WebDbService` | `WebDbConfig.json` |
| **Akun pengguna** | **SQLite** (`users.db`) | `UserService` | otomatis (auto-seed) |

Karena data UDO faktur berada di `SAP_WEB` (SQL Server) sedangkan item invoice ada di DB SAP B1 (HANA/SQL Server), **query lintas-database digabung di sisi aplikasi** — bukan lewat `JOIN` langsung. Ini penting terutama pada proses Export CSV/XML.

```
                         ┌──────────────────────────┐
   Browser ─(cookie)──▶  │  Blazor Server (.NET 8)  │
                         │  Interactive Server       │
                         └────────────┬─────────────┘
                    ┌─────────────────┼─────────────────┐
                    ▼                 ▼                 ▼
           SapQueryService     WebDbService       UserService
                    │                 │                 │
          ┌─────────▼───────┐ ┌───────▼────────┐ ┌──────▼──────┐
          │  SAP B1 DB      │ │  SAP_WEB        │ │  users.db   │
          │  HANA / SQLSrv  │ │  (SQL Server)   │ │  (SQLite)   │
          │  OINV, INV1,    │ │ IDU_PAJAK_TRANS │ │  Users      │
          │  OCRD, OADM...  │ │ IDU_PAJAK_..DET │ │             │
          └─────────────────┘ └────────────────┘ └─────────────┘
```

---

## ✨ Fitur

### E-Faktur → Pajak Keluaran
- **Daftar Pajak Keluaran** (`/efaktur/pajak-keluaran`) — grid dokumen dari `SAP_WEB` (`SELECT DocEntry, DocNum, CreateDate, U_Format FROM IDU_PAJAK_TRANS WHERE Canceled='N'`). Kolom dinamis dari hasil query, filter per-kolom, sort asc/desc, lebar kolom menyesuaikan isi, tombol **New** dan **Edit** (menuju halaman detail).
- **Buat Pajak Keluaran** (`/efaktur/create-pajak-keluaran`) —
  - **Load Data** invoice dari SAP B1 lewat SP `__IDUFAKTURPAJAK_GETINVOICEFP` (varian *Invoice Number / Date / Customer*), rentang **From/To** wajib, plus filter *Document Type*.
  - **Document Number** di-generate dari SP `__IDUFAKTURPAJAK_GENERATE_NOFAKTUR`.
  - **Picker modal** untuk Invoice Number & Customer.
  - Invoice yang sudah difaktur (`U_Status='Y'`, header tidak *canceled*) **otomatis dikecualikan** dari hasil Load.
  - **Add** — simpan baris tercentang ke `SAP_WEB` lewat SP `usp_InsertPajakKeluaran` (TVP), lalu **pop up sukses** menampilkan `DocNum` + tombol **Show** menuju detail.
- **Detail Pajak Keluaran** (`/efaktur/show-pajak-keluaran?doc={DocEntry}`) — tampilan read-only header (Document Number, Document Date, **Status Document**: `O`=Open / `C`=Closed) + grid detail dari `IDU_PAJAK_TRANS join IDU_PAJAK_TRANS_DET`.
  - **Exclude** baris tercentang (konfirmasi modal) → set `U_Status='N'`; baris ber-`U_Status='N'` disembunyikan dari grid & export, dan invoice-nya tersedia lagi untuk diproses ulang.
  - **Cancel Document**, **Export CSV**, **Export XML**.
  - Halaman menampilkan header saja bila seluruh detail ter-exclude; "Halaman Tidak Ditemukan" hanya bila `DocEntry` benar-benar tak ada.

### Sistem (khusus Admin)
- **Database Manager** — CRUD koneksi SAP B1 (SQL Server & SAP HANA), test koneksi, set koneksi aktif, export/import JSON.
- **User Management** — CRUD pengguna, username unik & read-only saat edit, validasi email, role **Admin/User**, field **KoneksiDb** (koneksi SAP per-user), password default `Password#01`.

### Umum
- **Login** berbasis cookie + role-based access; user yang sudah login membuka `/login` dialihkan ke halaman terakhir.
- **Indikator status koneksi** (`DbStatus`) realtime (polling non-blok, offload ke thread pool).
- **Query engine dinamis** (`SapQueryService`) yang menyesuaikan dialek HANA vs SQL Server (auto-quote identifier untuk HANA).
- Tata letak konsisten: `PageHeader`, sidebar hamburger persisten (lintas halaman & mobile), skala tampilan 90%.

---

## 🧱 Teknologi

| | |
|---|---|
| Framework | Blazor Server, **.NET 8** (Interactive Server, `DetailedErrors`) |
| UI | **Radzen.Blazor 11.1.0**, Bootstrap 5, Bootstrap Icons |
| Autentikasi | `Microsoft.AspNetCore.Authentication.Cookies` + **PBKDF2** (`Rfc2898DeriveBytes`, 100k iterasi) |
| Data SAP B1 | `Microsoft.Data.SqlClient 7.0.2` · `Sap.Data.Hana.Net.v8.0 2.29.23` |
| Data SAP_WEB | `Microsoft.Data.SqlClient` (+ TVP) |
| Akun user | `Microsoft.Data.Sqlite 10.0.9` |
| Kripto | `Microsoft.Bcl.Cryptography 10.0.9` |
| Konfigurasi | `DotNetEnv 3.2.0` (`.env`) |

---

## 📁 Struktur proyek

```
BlazorApp1/
├─ Program.cs                     # DI, cookie auth, middleware last-page, logout
├─ Components/
│  ├─ App.razor, Routes.razor     # root + routing (AuthorizeRouteView)
│  ├─ Layout/
│  │  ├─ MainLayout.razor         # shell + user box + logout + toggle sidebar
│  │  ├─ LoginLayout.razor        # layout khusus halaman login
│  │  ├─ NavMenu.razor            # menu (E-Faktur / System) per-role
│  │  └─ DbStatus.razor           # indikator koneksi realtime
│  ├─ Account/
│  │  ├─ AccessDenied.razor, RedirectToLogin.razor
│  ├─ Shared/ PageHeader.razor
│  └─ Pages/
│     ├─ Login.razor, Home.razor, Denied.razor, NotFoundPage.razor, Error.razor
│     ├─ EFaktur/
│     │  ├─ PajakKeluaran.razor        # Daftar
│     │  ├─ CreatePajakKeluaran.razor  # Buat + Load + Add
│     │  ├─ ShowPajakKeluaran.razor    # Detail + Exclude + Export
│     │  ├─ InvoiceNumberList.razor    # modal picker invoice
│     │  └─ CustomerList.razor         # modal picker customer
│     └─ Sys/
│        ├─ DatabaseManager.razor
│        ├─ UserManagement.razor, UserEditDialog.razor, UserDetailDialog.razor
├─ Services/
│  ├─ SapQueryService.cs          # query SAP B1 (HANA/SQLSrv), resolve koneksi per-role
│  ├─ WebDbService.cs             # SAP_WEB: insert (TVP), exclude, query, get DocNum
│  ├─ PajakKeluaranService.cs     # Load Data & Generate No. Faktur (SP SAP B1)
│  ├─ EFakturExportService.cs     # Export CSV & XML e-Faktur/Coretax
│  ├─ DbManagerService.cs         # CRUD koneksi (file-lock aman)
│  ├─ UserService.cs              # CRUD user (SQLite) + PBKDF2
│  └─ ServerAuthStateProvider.cs  # AuthenticationStateProvider (cookie)
├─ Models/
│  ├─ FakturKeluaranRow.cs        # baris grid (sumber tunggal definisi kolom)
│  ├─ PajakKeluaranFilter.cs, PajakKeluaranHeader.cs
│  ├─ DbConnectionEntry.cs        # + properti Schema (parse dari conn-string)
│  ├─ AppUser.cs, InvoiceListRow.cs, CustomerListRow.cs
├─ Database/
│  └─ usp_InsertPajakKeluaran.sql # TVP + SP insert (DocNum = YYMM + urutan)
├─ ConnectDBAccess.sample.json    # contoh koneksi SAP B1
├─ ConnectDBAccess.json           # koneksi SAP B1 (gitignored)
├─ WebDbConfig.json               # koneksi SAP_WEB (gitignored)
└─ wwwroot/                       # app.css (zoom), JS (toggleSidebar, downloadFile, …)
```

---

## 🔐 Autentikasi & otorisasi

- **Cookie authentication** (`EFaktur.Auth`, `HttpOnly`, `SameSite=Lax`, `SecurePolicy=SameAsRequest` agar jalan di HTTP maupun HTTPS), sliding expiration 8 jam.
- Password disimpan sebagai hash **PBKDF2** (`iter.salt.hash`, 100.000 iterasi) di `users.db` (SQLite). Password default semua user: `Password#01`.
- **Role**:
  - **Admin** → semua halaman (E-Faktur + System).
  - **User** → hanya Home + menu E-Faktur (Pajak Keluaran). Koneksi SAP diambil dari field **KoneksiDb** user; bila kosong → *"No choose DB"*.
- Middleware HTTP melacak "halaman aktif terakhir" (cookie `efaktur_last`) dan mengalihkan user login yang membuka `/login` ke halaman itu — tanpa `NavigationException`.
- **Logout** hanya via `POST /Account/Logout` dengan token antiforgery.

---

## 🗄️ Dua sumber database

### 1. SAP B1 (HANA / SQL Server) — `SapQueryService`
Sumber data master & transaksi SAP: `OINV`, `INV1`, `OCRD`, `OCPR`, `OADM`, `ADM1`, `OUOM`, dll. `SapQueryService`:
- Meresolusi koneksi berdasarkan role (Admin → koneksi aktif; User → `KoneksiDb`).
- `RunQueryAsync` (auto-quote identifier untuk HANA) & `RunRawQueryAsync` (tanpa auto-quote, untuk SQL yang sudah portable).
- `Schema` di-parse dari connection string: `Current Schema` (HANA) / `Database` (SQL Server).

### 2. SAP_WEB (SQL Server) — `WebDbService`
Menyimpan hasil faktur:
- `IDU_PAJAK_TRANS` (header) + `IDU_PAJAK_TRANS_DET` (detail).
- `SavePajakKeluaranAsync` → SP `usp_InsertPajakKeluaran` (TVP `dbo.PajakKeluaranDetailType`), mengembalikan `DocEntry` **dan** `DocNum`.
- `ExcludeDetailAsync` → `UPDATE ... SET U_Status='N'` (berparameter).
- `GetProcessedInvoiceKeysAsync` → daftar invoice yang sudah difaktur (untuk exclude di Load Data).

---

## 🔄 Alur Pajak Keluaran

```
Buat  ──▶ Load Data (SP SAP B1) ──▶ centang baris ──▶ Add ──▶ SP insert (SAP_WEB)
                                                              │
                                                     pop up "Berhasil …{DocNum}"
                                                              │  [Show]
                                                              ▼
Daftar ◀── grid IDU_PAJAK_TRANS      Detail ◀── ?doc={DocEntry}
                                        │
                                        ├─ Exclude (U_Status='N')
                                        ├─ Cancel Document
                                        └─ Export CSV / XML
```

---

## 📤 Export e-Faktur (CSV & XML)

Diimplementasikan di `EFakturExportService`, **meniru 100% output add-on VB.NET tanpa membuat SP baru** — item baris & data buyer/seller diambil langsung dari SAP B1, digabung dengan field header dari grid (`SAP_WEB`).

- **CSV** — format DJP `FK / FAPR / OF` (dari `INV1`/`OINV` + `OADM`/`ADM1`). Pembulatan PPN: PPN header = `FLOOR(Σ PPN)`, baris terakhir menyerap selisih agar `Σ PPN baris = PPN header`.
- **XML** — struktur **Coretax `TaxInvoiceBulk`** (`ListOfTaxInvoice → TaxInvoice → ListOfGoodService → GoodService`).
  - `VATRate = 11`; VAT jenis `01` = `ROUND(11/12 × DPP × 11/100)`.
  - `CustomDoc` & `RefDesc` = nomor invoice.
  - Blok **Buyer** dari `OCRD`/`OCPR` mengikuti CASE `U_IDU_JenisCustomer` (`01` → TIN, selain itu → National ID).
  - **BuyerCountry** dari `OCRD.Country` dipetakan lewat tabel `ISO3CountryCode` (di **SAP_WEB**), default `IDN` (join dilakukan di aplikasi karena beda database).
- Kedua export hanya memproses baris yang tampil di grid (baris `U_Status='N'` otomatis tidak ikut).

> Cakupan yang disederhanakan (perlu verifikasi di DB): kasus **DP (07/08)**, **delivery `U_Kode_Pekerjaan='BM'`**, dan `AddInfo`/`FacilityStamp`.

---

## 🚀 Menjalankan (development)

**Prasyarat:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```bash
cd BlazorApp1
dotnet restore
dotnet run
# atau profil HTTPS eksplisit:
dotnet run --launch-profile https --urls https://localhost:7240
```

Login awal (seed): user **admin** (password `Password#01`). Buat/kelola user lain di **System → User Management**.

---

## 🌐 Deploy ke IIS

Aplikasi diuji berjalan di IIS pada `http://localhost:5343`.

```bash
dotnet publish BlazorApp1 -c Release -o <folder-publish>
```

1. Arahkan Site/Application IIS ke folder publish (butuh **.NET 8 Hosting Bundle**).
2. Pastikan `ConnectDBAccess.json`, `WebDbConfig.json`, dan `users.db` ikut/tersedia di folder aplikasi.
3. Cookie sudah dikonfigurasi `SameAsRequest` sehingga login **tetap jalan di HTTP** (tanpa HTTPS).

---

## ⚙️ Konfigurasi

### Koneksi SAP B1 — `ConnectDBAccess.json` (gitignored)
Kelola lewat **System → Database Manager** (disarankan) atau salin `ConnectDBAccess.sample.json`.

- **SQL Server:** `Server=host;Database=SBO_DB;User Id=sa;Password=***;TrustServerCertificate=True;Encrypt=False;`
- **SAP HANA:** `Server=host:30015;UID=user;PWD=***;Current Schema=PEN_1706_T;encrypt=True;`

> Query membaca koneksi **aktif** (atau `KoneksiDb` user) setiap kali dipakai — ganti koneksi langsung berpengaruh tanpa restart.

### Koneksi SAP_WEB — `WebDbConfig.json` (gitignored)
```json
{ "ConnectionString": "Server=HOST\\INSTANCE;Database=SAP_WEB;User Id=sa;Password=***;TrustServerCertificate=True;Encrypt=False;" }
```

### `.env` (opsional, gitignored)
Dimuat via `DotNetEnv` sebelum `CreateBuilder`.

---

## 🧩 Stored Procedure

- **`Database/usp_InsertPajakKeluaran.sql`** (SAP_WEB) — membuat TVP `dbo.PajakKeluaranDetailType` + SP `usp_InsertPajakKeluaran`. `DocNum` = `(YY×100 + MM) × 100000 + urutan` (mis. `260700001`).
- **SP SAP B1 (dari add-on, sudah terpasang di DB SAP):**
  - `__IDUFAKTURPAJAK_GETINVOICEFP` (+ `_DATE` / `_CUSTOMER`) — Load Data.
  - `__IDUFAKTURPAJAK_GENERATE_NOFAKTUR` — Document Number.
- **`ISO3CountryCode`** (tabel di SAP_WEB) — pemetaan kode negara untuk Export XML; bila belum ada, Export tetap aman (fallback `IDN`).

---

## 🔒 Keamanan

- `ConnectDBAccess.json`, `WebDbConfig.json`, `users.db`, dan `.env` **dikecualikan dari repo** (`.gitignore`).
- Password di-hash **PBKDF2** (bukan plaintext).
- Query tulis (Insert/Exclude) **berparameter** untuk mencegah SQL injection.
- Pesan error ke pengguna **tidak membocorkan nama SP / nama akses DB** (mis. diganti *"Invalid Access Database"*).
- Logout wajib `POST` + token antiforgery (anti-CSRF); cookie `HttpOnly` (anti-XSS).
