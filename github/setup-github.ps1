<#
    setup-github.ps1
    Membuat repo (opsional), label, Project board, dan issue untuk
    aplikasi "Add-On E-Faktur" via GitHub CLI (gh).

    PRASYARAT
      1. Install GitHub CLI:  https://cli.github.com/  (atau: winget install GitHub.cli)
      2. Login:               gh auth login
      3. Scope untuk Projects: gh auth refresh -s project,read:project

    CARA PAKAI
      1. Sesuaikan variabel di bawah ($Owner, $Repo, $Visibility, $CreateRepo).
      2. Jalankan:  powershell -ExecutionPolicy Bypass -File .\setup-github.ps1
#>

# ─────────────────────────── KONFIGURASI ───────────────────────────
$Owner       = "fetsusiahaan"        # username / organisasi GitHub Anda
$Repo        = "addon-efaktur"       # nama repository
$Visibility  = "public"              # private | public | internal
$CreateRepo  = $true                 # $false jika repo sudah ada
$ProjectName = "Add-On E-Faktur — Roadmap"
# ────────────────────────────────────────────────────────────────────

$ErrorActionPreference = "Stop"
$RepoFull = "$Owner/$Repo"

function Have-Gh {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        Write-Error "GitHub CLI (gh) belum terpasang. Lihat https://cli.github.com/"
    }
    gh auth status 1>$null 2>$null
    if (-not $?) { Write-Error "Belum login. Jalankan: gh auth login" }
}

Have-Gh

# ─────────────────────────── REPO ───────────────────────────
if ($CreateRepo) {
    Write-Host "Membuat repo $RepoFull ..." -ForegroundColor Cyan
    gh repo create $RepoFull --$Visibility --description "Add-On E-Faktur untuk SAP Business One (Blazor Server .NET 8)"
} else {
    Write-Host "Melewati pembuatan repo (memakai $RepoFull yang sudah ada)." -ForegroundColor Yellow
}

# ─────────────────────────── LABEL ───────────────────────────
$labels = @(
    @{ name = "enhancement";   color = "a2eeef"; desc = "Fitur baru" },
    @{ name = "bug";           color = "d73a4a"; desc = "Cacat / error" },
    @{ name = "security";      color = "b60205"; desc = "Keamanan" },
    @{ name = "backend";       color = "5319e7"; desc = "Service / query" },
    @{ name = "ui";            color = "fbca04"; desc = "Tampilan / UX" },
    @{ name = "tech-debt";     color = "c5def5"; desc = "Utang teknis" },
    @{ name = "infra";         color = "0e8a16"; desc = "Build / CI / DevOps" },
    @{ name = "documentation"; color = "0075ca"; desc = "Dokumentasi" }
)
foreach ($l in $labels) {
    Write-Host "Label: $($l.name)" -ForegroundColor DarkGray
    gh label create $l.name --color $l.color --description $l.desc --repo $RepoFull --force
}

# ─────────────────────────── ISSUES ───────────────────────────
$issues = @(
@{
  title  = "[E-Faktur] Sambungkan filter Pajak Masukan ke query"
  labels = "enhancement,backend"
  body   = @"
Panel filter di halaman **Pajak Masukan** (Search by, Document Number, From/To, Canceled, Document Type) saat ini hanya UI dan belum memengaruhi query.

### Tugas
- [ ] Bangun WHERE dinamis dari nilai filter (aman dari injeksi — pakai parameter).
- [ ] Terapkan ke ``SapQueryService`` untuk SQL Server & HANA (perhatikan quoting identifier HANA).
- [ ] Filter Canceled (No/Yes/All) → kolom pembatalan OINV.
- [ ] Document Type (Invoice/Down Payment/Ref Credit Memo).

### Acceptance
Klik **Load Data** menghormati semua filter yang dipilih.
"@
},
@{
  title  = "[E-Faktur] Implementasi halaman Pajak Keluaran dengan data nyata"
  labels = "enhancement"
  body   = @"
``PajakKeluaran.razor`` masih memakai data contoh dan merujuk komponen ``TaxInvoiceGrid`` yang tidak ada.

### Tugas
- [ ] Ganti dengan RadzenDataGrid + model bertipe (mengikuti pola Pajak Masukan).
- [ ] Query dari OINV/ODLN sesuai kebutuhan Pajak Keluaran.
- [ ] Reuse ``SapQueryService`` (koneksi aktif).
"@
},
@{
  title  = "[E-Faktur] Fitur Export CSV & XML"
  labels = "enhancement"
  body   = @"
Tombol **Export CSV** dan **Export XML** belum berfungsi.

### Tugas
- [ ] Export CSV dari data grid (hormati filter/kolom yang tampil).
- [ ] Export XML sesuai format e-Faktur yang dibutuhkan.
- [ ] Trigger download di browser.
"@
},
@{
  title  = "[E-Faktur] Aksi Add / Cancel dokumen"
  labels = "enhancement"
  body   = @"
Tombol **Add** dan **Cancel** di action bar belum ada handler.

### Tugas
- [ ] Alur tambah dokumen (form + validasi).
- [ ] Alur cancel/batalkan dokumen terpilih.
"@
},
@{
  title  = "[System] Halaman User Management"
  labels = "enhancement"
  body   = @"
Menu **System → Administration → User Management** (``/sys/users``) ada di NavMenu tapi halamannya belum dibuat.

### Tugas
- [ ] CRUD user, aktif/nonaktif.
- [ ] Kaitkan dengan Roles (lihat issue Roles & Permissions).
"@
},
@{
  title  = "[System] Halaman Roles & Permissions"
  labels = "enhancement"
  body   = @"
Menu **Roles & Permissions** (``/sys/roles``) belum ada halamannya.

### Tugas
- [ ] Definisi role & permission.
- [ ] Assign role ke user; proteksi akses halaman.
"@
},
@{
  title  = "[System] Halaman Audit Log"
  labels = "enhancement"
  body   = @"
Menu **Audit Log** (``/sys/audit-log``) belum ada halamannya.

### Tugas
- [ ] Catat aksi penting (login, ubah koneksi, load/export data).
- [ ] Tampilkan log dengan filter tanggal/user/aksi.
"@
},
@{
  title  = "[Security] Enkripsi connection string tersimpan"
  labels = "security"
  body   = @"
``ConnectDBAccess.json`` menyimpan connection string (termasuk kredensial DB) dalam **plaintext**.

### Tugas
- [ ] Enkripsi field ``ConnString`` saat disimpan (mis. DPAPI/AES dengan kunci dari env).
- [ ] Dekripsi saat dipakai; jangan tampilkan password apa adanya di UI.
"@
},
@{
  title  = "[Security] Batasi / parameterisasi SQL di SapQueryService"
  labels = "security,backend"
  body   = @"
``SapQueryService.RunQueryAsync`` menjalankan SQL mentah. Bila nanti input pengguna masuk ke query, ada risiko **SQL injection**.

### Tugas
- [ ] Gunakan parameter untuk semua nilai dari input.
- [ ] Pertimbangkan whitelist query / hanya SELECT.
"@
},
@{
  title  = "[Tech-debt] Jangan serialize computed property DbConnectionEntry"
  labels = "tech-debt"
  body   = @"
``ProviderName``, ``FormatHint``, ``IconClass`` adalah computed property tetapi ikut ter-serialize ke ``ConnectDBAccess.json``.

### Tugas
- [ ] Tandai dengan ``[JsonIgnore]`` agar tidak tersimpan.
"@
},
@{
  title  = "[Infra] Inisialisasi git + CI build .NET 8 (GitHub Actions)"
  labels = "infra"
  body   = @"
Proyek belum berupa git repo dan belum ada CI.

### Tugas
- [ ] ``git init`` + ``.gitignore`` (.NET) + push ke repo ini.
- [ ] Workflow GitHub Actions: ``dotnet build`` (+ test bila ada) pada push/PR.
"@
},
@{
  title  = "[Docs] README + panduan setup"
  labels = "documentation"
  body   = @"
Belum ada README.

### Tugas
- [ ] Cara build & run (.NET 8), konfigurasi ``.env``.
- [ ] Cara menambah koneksi DB (SQL Server/HANA) + Set Aktif.
- [ ] Catatan: UI grid memakai **Radzen Blazor** (Syncfusion sudah dihapus).
"@
}
)

$created = @()
foreach ($i in $issues) {
    Write-Host "Issue: $($i.title)" -ForegroundColor Cyan
    $url = gh issue create --repo $RepoFull --title $i.title --body $i.body --label $i.labels
    $created += $url
}

# ─────────────────────────── PROJECT (v2) ───────────────────────────
Write-Host "Membuat project '$ProjectName' ..." -ForegroundColor Cyan
try {
    gh project create --owner $Owner --title $ProjectName
    # Tautkan tiap issue ke project
    foreach ($url in $created) {
        if ($url) {
            $num = (gh project list --owner $Owner --format json | ConvertFrom-Json).projects `
                   | Where-Object { $_.title -eq $ProjectName } | Select-Object -First 1 -ExpandProperty number
            gh project item-add $num --owner $Owner --url $url
        }
    }
    Write-Host "Semua issue ditautkan ke project." -ForegroundColor Green
}
catch {
    Write-Warning "Gagal membuat/menautkan project. Pastikan scope: gh auth refresh -s project,read:project"
    Write-Warning $_.Exception.Message
}

Write-Host "`nSelesai. $($created.Count) issue dibuat di $RepoFull." -ForegroundColor Green
