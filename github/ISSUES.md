# Add-On E-Faktur — Rencana Issue & Project GitHub

Dokumen ini merangkum backlog aplikasi **Add-On E-Faktur** (Blazor Server .NET 8, Radzen, SAP B1 SQL Server/HANA). Jalankan [`setup-github.ps1`](./setup-github.ps1) untuk membuat repo, label, project board, dan semua issue di bawah secara otomatis.

## Ringkasan status aplikasi

**Sudah ada**
- E-Faktur → Pajak Masukan: grid Radzen (model bertipe), sorting/filter/column-pick, format & lebar kolom, scroll horizontal internal.
- Database Manager: CRUD koneksi, test koneksi (SQL Server & SAP HANA), export/import JSON.
- Query engine dinamis (`SapQueryService`) yang otomatis menyesuaikan dialek SQL Server/HANA.
- Indikator status koneksi realtime (`DbStatus`) + toast notifikasi saat DB terputus/pulih, dengan timeout tegas & anti-pooling.

**Belum ada / perlu dikerjakan** → lihat daftar issue.

---

## Daftar Issue

| # | Judul | Label |
|---|-------|-------|
| 1 | [E-Faktur] Sambungkan filter Pajak Masukan ke query | enhancement, backend |
| 2 | [E-Faktur] Implementasi halaman Pajak Keluaran dengan data nyata | enhancement |
| 3 | [E-Faktur] Fitur Export CSV & XML | enhancement |
| 4 | [E-Faktur] Aksi Add / Cancel dokumen | enhancement |
| 5 | [System] Halaman User Management | enhancement |
| 6 | [System] Halaman Roles & Permissions | enhancement |
| 7 | [System] Halaman Audit Log | enhancement |
| 8 | [Security] Enkripsi connection string tersimpan | security |
| 9 | [Security] Batasi/parameterisasi SQL di SapQueryService | security |
| 10 | [Tech-debt] Jangan serialize computed property DbConnectionEntry | tech-debt |
| 11 | [Infra] Inisialisasi git + CI build .NET 8 (GitHub Actions) | infra |
| 12 | [Docs] README + panduan setup (.env, koneksi, Syncfusion→Radzen) | documentation |

Detail tiap issue ada di dalam `setup-github.ps1` (judul + body + label).
