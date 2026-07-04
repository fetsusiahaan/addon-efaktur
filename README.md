# Add-On E-Faktur

Aplikasi **Add-On E-Faktur** untuk **SAP Business One** — antarmuka web untuk menampilkan & mengelola dokumen pajak (Pajak Masukan / Pajak Keluaran) yang datanya bersumber dari database SAP B1 (**SQL Server** atau **SAP HANA**).

Dibangun dengan **Blazor Server (.NET 8)** dan komponen UI **[Radzen Blazor](https://blazor.radzen.com/)**.

---

## ✨ Fitur

- **E-Faktur → Pajak Masukan** — grid data (RadzenDataGrid) dari query SAP B1, dengan sorting, filtering, column-picking, resize kolom, format angka, dan scroll horizontal internal.
- **Database Manager** — CRUD koneksi database (SQL Server & SAP HANA), test koneksi, set koneksi aktif, export/import JSON.
- **Query engine dinamis** (`SapQueryService`) — menjalankan SELECT ke koneksi aktif dan otomatis menyesuaikan dialek: identifier di-kutip untuk HANA (case-sensitive), apa adanya untuk SQL Server.
- **Indikator status koneksi realtime** (`DbStatus`) — badge di semua halaman yang mem-polling koneksi aktif, plus **toast notifikasi** saat DB terputus/pulih. Tahan terhadap connection pooling (probe query) dan hang (timeout tegas).

## 🧱 Teknologi

| | |
|---|---|
| Framework | Blazor Server, .NET 8 (Interactive Server) |
| UI | Radzen.Blazor, Bootstrap 5, Bootstrap Icons |
| Data | Microsoft.Data.SqlClient · Sap.Data.Hana.Net.v8.0 |
| Konfigurasi | DotNetEnv (`.env`) |

## 🚀 Menjalankan

**Prasyarat:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```bash
cd BlazorApp1
dotnet restore
dotnet run
```

Buka URL yang tampil (mis. `https://localhost:7240`).

## ⚙️ Konfigurasi koneksi database

Koneksi disimpan di **`BlazorApp1/ConnectDBAccess.json`** (file ini **tidak** di-commit karena berisi kredensial). Gunakan **`ConnectDBAccess.sample.json`** sebagai contoh, atau kelola lewat UI:

1. Buka **System → Administration → Database Manager**.
2. **Tambah Koneksi** → pilih jenis (SQL Server / SAP HANA) → isi connection string.
   - SQL Server: `Server=host;Database=SBO_DB;User Id=sa;Password=***;TrustServerCertificate=True;`
   - SAP HANA: `Server=host:30015;UID=user;PWD=***;databaseName=TENANT;encrypt=True;`
3. **Test Koneksi** → **Set Aktif**.
4. Buka **E-Faktur → Pajak Masukan** → **Load Data**.

> Query membaca koneksi **aktif** setiap kali Load Data — ganti koneksi aktif langsung berpengaruh tanpa restart.

### Variabel lingkungan (opsional)

Buat file `.env` (juga tidak di-commit) untuk konfigurasi tambahan yang dimuat via DotNetEnv sebelum aplikasi dibangun.

## 📁 Struktur singkat

```
BlazorApp1/
├─ Components/
│  ├─ Layout/         MainLayout, NavMenu, DbStatus (indikator + notif)
│  └─ Pages/
│     ├─ EFaktur/     PajakMasukan, PajakKeluaran
│     └─ Sys/         DatabaseManager
├─ Models/            DbConnectionEntry, PajakMasukanRow, dll.
├─ Services/          SapQueryService, DbManagerService
└─ ConnectDBAccess.sample.json
```

## 🗺️ Roadmap

Backlog dikelola sebagai [GitHub Issues](https://github.com/fetsusiahaan/addon-efaktur/issues) — antara lain: menyambungkan filter Pajak Masukan, halaman Pajak Keluaran, Export CSV/XML, halaman System (Users/Roles/Audit Log), enkripsi connection string, dan parameterisasi SQL.

## 🔒 Keamanan

- `ConnectDBAccess.json` dan `.env` dikecualikan dari repo (lihat `.gitignore`) agar kredensial tidak bocor.
- Enkripsi connection string tersimpan masih dalam backlog ([#8](https://github.com/fetsusiahaan/addon-efaktur/issues/8)).

---

_Proyek internal Intidatautama._
