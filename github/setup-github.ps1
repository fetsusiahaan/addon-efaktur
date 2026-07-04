<#
    setup-github.ps1
    Membuat repo (opsional), label, Project board, dan issue untuk
    aplikasi "Add-On E-Faktur" via GitHub CLI (gh).

    PRASYARAT
      1. Install GitHub CLI:  https://cli.github.com/
      2. Login:               gh auth login
      3. Scope untuk Projects: gh auth refresh -s project,read:project

    CARA PAKAI
      1. Sesuaikan variabel di bawah.
      2. Jalankan: powershell -ExecutionPolicy Bypass -File .\setup-github.ps1
#>

# --------------------------- KONFIGURASI ---------------------------
$Owner       = "fetsusiahaan"
$Repo        = "addon-efaktur"
$Visibility  = "public"              # private | public | internal
$CreateRepo  = $false                # $false jika repo sudah ada
$ProjectName = "Add-On E-Faktur Roadmap"
# -------------------------------------------------------------------

$ErrorActionPreference = "Stop"
$RepoFull = "$Owner/$Repo"

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI (gh) belum terpasang / belum di PATH."
}

# --------------------------- REPO ---------------------------
if ($CreateRepo) {
    Write-Host "Membuat repo $RepoFull ..." -ForegroundColor Cyan
    gh repo create $RepoFull --$Visibility --description "Add-On E-Faktur untuk SAP Business One (Blazor Server .NET 8)"
} else {
    Write-Host "Melewati pembuatan repo (memakai $RepoFull yang sudah ada)." -ForegroundColor Yellow
}

# --------------------------- LABEL ---------------------------
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

# --------------------------- ISSUES ---------------------------
$issues = @(
@{ title = "[E-Faktur] Sambungkan filter Pajak Masukan ke query"; labels = "enhancement,backend"; body = @'
Panel filter Pajak Masukan (Search by, Document Number, From/To, Canceled, Document Type) saat ini hanya UI dan belum memengaruhi query.

### Tugas
- [ ] Bangun WHERE dinamis dari nilai filter (pakai parameter, aman injeksi).
- [ ] Terapkan ke SapQueryService untuk SQL Server & HANA (perhatikan quoting HANA).
- [ ] Filter Canceled (No/Yes/All) dan Document Type.
'@ }
@{ title = "[E-Faktur] Implementasi halaman Pajak Keluaran dengan data nyata"; labels = "enhancement"; body = @'
PajakKeluaran.razor masih memakai data contoh dan merujuk komponen TaxInvoiceGrid yang tidak ada.

### Tugas
- [ ] Ganti dengan RadzenDataGrid + model bertipe (pola Pajak Masukan).
- [ ] Query sesuai kebutuhan Pajak Keluaran, reuse SapQueryService (koneksi aktif).
'@ }
@{ title = "[E-Faktur] Fitur Export CSV & XML"; labels = "enhancement"; body = @'
Tombol Export CSV dan Export XML belum berfungsi.

### Tugas
- [ ] Export CSV dari data grid (hormati filter/kolom).
- [ ] Export XML sesuai format e-Faktur.
- [ ] Trigger download di browser.
'@ }
@{ title = "[E-Faktur] Aksi Add / Cancel dokumen"; labels = "enhancement"; body = @'
Tombol Add dan Cancel di action bar belum ada handler.

### Tugas
- [ ] Alur tambah dokumen (form + validasi).
- [ ] Alur cancel/batalkan dokumen terpilih.
'@ }
@{ title = "[System] Halaman User Management"; labels = "enhancement"; body = @'
Menu /sys/users ada di NavMenu tapi halamannya belum dibuat.

### Tugas
- [ ] CRUD user, aktif/nonaktif.
- [ ] Kaitkan dengan Roles.
'@ }
@{ title = "[System] Halaman Roles & Permissions"; labels = "enhancement"; body = @'
Menu /sys/roles belum ada halamannya.

### Tugas
- [ ] Definisi role & permission.
- [ ] Assign role ke user; proteksi akses halaman.
'@ }
@{ title = "[System] Halaman Audit Log"; labels = "enhancement"; body = @'
Menu /sys/audit-log belum ada halamannya.

### Tugas
- [ ] Catat aksi penting (login, ubah koneksi, load/export data).
- [ ] Tampilkan log dengan filter tanggal/user/aksi.
'@ }
@{ title = "[Security] Enkripsi connection string tersimpan"; labels = "security"; body = @'
ConnectDBAccess.json menyimpan connection string (kredensial DB) dalam plaintext.

### Tugas
- [ ] Enkripsi field ConnString saat disimpan (DPAPI/AES, kunci dari env).
- [ ] Dekripsi saat dipakai; jangan tampilkan password apa adanya.
'@ }
@{ title = "[Security] Batasi / parameterisasi SQL di SapQueryService"; labels = "security,backend"; body = @'
SapQueryService.RunQueryAsync menjalankan SQL mentah. Bila input pengguna masuk ke query, ada risiko SQL injection.

### Tugas
- [ ] Gunakan parameter untuk semua nilai dari input.
- [ ] Pertimbangkan whitelist / hanya SELECT.
'@ }
@{ title = "[Tech-debt] Jangan serialize computed property DbConnectionEntry"; labels = "tech-debt"; body = @'
ProviderName, FormatHint, IconClass adalah computed property tetapi ikut ter-serialize ke ConnectDBAccess.json.

### Tugas
- [ ] Tandai dengan [JsonIgnore] agar tidak tersimpan.
'@ }
@{ title = "[Infra] Inisialisasi git + CI build .NET 8 (GitHub Actions)"; labels = "infra"; body = @'
Belum ada CI.

### Tugas
- [ ] .gitignore .NET + push (sudah).
- [ ] Workflow GitHub Actions: dotnet build (+ test) pada push/PR.
'@ }
@{ title = "[Docs] README + panduan setup"; labels = "documentation"; body = @'
Belum ada README.

### Tugas
- [ ] Cara build & run (.NET 8), konfigurasi .env.
- [ ] Cara menambah koneksi DB (SQL Server/HANA) + Set Aktif.
- [ ] Catatan: UI grid memakai Radzen Blazor (Syncfusion sudah dihapus).
'@ }
)

$created = @()
foreach ($i in $issues) {
    Write-Host "Issue: $($i.title)" -ForegroundColor Cyan
    $tmp = New-TemporaryFile
    Set-Content -Path $tmp -Value $i.body -Encoding UTF8
    $url = gh issue create --repo $RepoFull --title $i.title --body-file $tmp --label $i.labels
    Remove-Item $tmp -ErrorAction SilentlyContinue
    Write-Host "  -> $url" -ForegroundColor Green
    $created += $url
}

# --------------------------- PROJECT (v2) ---------------------------
Write-Host "Membuat project '$ProjectName' ..." -ForegroundColor Cyan
try {
    gh project create --owner $Owner --title $ProjectName
    $num = ((gh project list --owner $Owner --format json | ConvertFrom-Json).projects `
            | Where-Object { $_.title -eq $ProjectName } | Select-Object -First 1).number
    foreach ($url in $created) {
        if ($url) { gh project item-add $num --owner $Owner --url $url }
    }
    Write-Host "Semua issue ditautkan ke project." -ForegroundColor Green
}
catch {
    Write-Warning "Project gagal dibuat/ditautkan. Jalankan: gh auth refresh -s project,read:project"
    Write-Warning $_.Exception.Message
}

Write-Host ""
Write-Host "Selesai. $($created.Count) issue dibuat di $RepoFull." -ForegroundColor Green
