<#
.SYNOPSIS
    SiteHub — Local development infrastructure manager

.DESCRIPTION
    Docker altyapısını (PostgreSQL, Redis, Seq, MailHog, MinIO) yönetir
    ve EF Core migration'larını çalıştırır. Tüm secret'lar repo kökündeki
    .env dosyasından okunur.

.EXAMPLE
    ./scripts/dev-infra.ps1 setup          # İlk kurulum: up + migrate
    ./scripts/dev-infra.ps1 up             # Servisleri başlat
    ./scripts/dev-infra.ps1 down           # Servisleri durdur (veriyi koru)
    ./scripts/dev-infra.ps1 restart        # Hızlı down + up
    ./scripts/dev-infra.ps1 pull           # İmajları son sürüme güncelle
    ./scripts/dev-infra.ps1 reset          # Veriyi sil + pull + baştan kur (VERİYİ SİLER!)
    ./scripts/dev-infra.ps1 nuke           # Her şeyi sıfırla: container+volume+image+tools
    ./scripts/dev-infra.ps1 status         # Servis durumu
    ./scripts/dev-infra.ps1 logs postgres  # Bir servisin loglarını izle
    ./scripts/dev-infra.ps1 migrate        # Migration'ları uygula
    ./scripts/dev-infra.ps1 add-migration InitialCreate   # Yeni migration ekle
    ./scripts/dev-infra.ps1 psql           # psql shell aç (DB'ye bağlan)

.NOTES
    Gereksinimler:
    - PowerShell 7+ (pwsh) — Windows, macOS, Linux'ta çalışır
    - Docker Desktop
    - .NET 10 SDK
    - dotnet-ef (otomatik yüklenir — .config/dotnet-tools.json)
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0, Mandatory = $true)]
    [ValidateSet('up', 'down', 'restart', 'reset', 'nuke', 'pull',
                 'status', 'logs', 'migrate', 'add-migration',
                 'setup', 'psql', 'help')]
    [string]$Command,

    [Parameter(Position = 1, ValueFromRemainingArguments = $true)]
    [string[]]$Args
)

$ErrorActionPreference = 'Stop'

# ═══════════════════════════════════════════════════════════════════════════════
# Yardımcılar (renkli çıktı)
# ═══════════════════════════════════════════════════════════════════════════════
function Write-Info    { param($msg) Write-Host "  ℹ  $msg" -ForegroundColor Cyan }
function Write-Success { param($msg) Write-Host "  ✓  $msg" -ForegroundColor Green }
function Write-Warn    { param($msg) Write-Host "  ⚠  $msg" -ForegroundColor Yellow }
function Write-Err     { param($msg) Write-Host "  ✗  $msg" -ForegroundColor Red }
function Write-Step    { param($msg) Write-Host "`n▶  $msg" -ForegroundColor Magenta }

# ═══════════════════════════════════════════════════════════════════════════════
# Yol çözümleme
# ═══════════════════════════════════════════════════════════════════════════════
$scriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot   = Split-Path -Parent $scriptDir
$dockerDir  = Join-Path $repoRoot 'docker'
$envFile    = Join-Path $repoRoot '.env'
$envExample = Join-Path $repoRoot '.env.example'

# ═══════════════════════════════════════════════════════════════════════════════
# Bağımlılık kontrolü
# ═══════════════════════════════════════════════════════════════════════════════
function Test-Prerequisites {
    $missing = @()

    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { $missing += 'docker' }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { $missing += 'dotnet' }

    if ($missing.Count -gt 0) {
        Write-Err "Eksik bağımlılıklar: $($missing -join ', ')"
        Write-Info "Docker Desktop: https://www.docker.com/products/docker-desktop"
        Write-Info ".NET 10 SDK:    https://dotnet.microsoft.com/download"
        exit 1
    }

    # Docker çalışıyor mu?
    try {
        docker info 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Docker not running" }
    }
    catch {
        Write-Err "Docker çalışmıyor. Docker Desktop'ı başlatın."
        exit 1
    }
}

# ═══════════════════════════════════════════════════════════════════════════════
# .env dosyasını yükle (process-level environment variable'lara)
# ═══════════════════════════════════════════════════════════════════════════════
function Import-DotEnv {
    if (-not (Test-Path $envFile)) {
        if (Test-Path $envExample) {
            Write-Warn ".env dosyası bulunamadı. .env.example'dan kopyalanıyor..."
            Copy-Item $envExample $envFile
            Write-Warn "ÖNEMLİ: .env dosyasındaki şifreleri üretimden önce değiştirin!"
        }
        else {
            Write-Err ".env ve .env.example dosyaları bulunamadı!"
            exit 1
        }
    }

    $loaded = 0
    Get-Content $envFile | ForEach-Object {
        $line = $_.Trim()
        if ($line -eq '' -or $line.StartsWith('#')) { return }

        if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)\s*$') {
            $name = $matches[1]
            $value = $matches[2].Trim('"').Trim("'")

            # ${VAR} interpolasyonu (aynı dosya içinde)
            $value = [regex]::Replace($value, '\$\{([A-Za-z_][A-Za-z0-9_]*)\}', {
                param($match)
                $varName = $match.Groups[1].Value
                $envValue = [Environment]::GetEnvironmentVariable($varName, 'Process')
                if ($null -eq $envValue) { '' } else { $envValue }
            })

            [Environment]::SetEnvironmentVariable($name, $value, 'Process')
            $loaded++
        }
    }
    Write-Info ".env dosyasından $loaded değişken yüklendi."

    # .env.example'da olup .env'de olmayan değişkenleri tespit et (upgrade uyarısı)
    if (Test-Path $envExample) {
        $exampleKeys = @()
        Get-Content $envExample | ForEach-Object {
            if ($_ -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=') {
                $exampleKeys += $matches[1]
            }
        }
        $envKeys = @()
        Get-Content $envFile | ForEach-Object {
            if ($_ -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=') {
                $envKeys += $matches[1]
            }
        }
        $missing = $exampleKeys | Where-Object { $envKeys -notcontains $_ }
        if ($missing.Count -gt 0) {
            Write-Warn ".env.example'da olup .env'de OLMAYAN değişkenler:"
            $missing | ForEach-Object { Write-Host "      - $_" -ForegroundColor Yellow }
            Write-Warn "Düzeltmek için: Remove-Item .env; tekrar çalıştır (yeniden oluşur)"
            Write-Host ""
        }
    }
}

# ═══════════════════════════════════════════════════════════════════════════════
# PostgreSQL'in hazır olmasını bekle
# ═══════════════════════════════════════════════════════════════════════════════
function Wait-ForPostgres {
    param([int]$TimeoutSeconds = 60)

    Write-Info "PostgreSQL'in hazır olması bekleniyor..."
    $user = $env:POSTGRES_USER
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        $result = docker exec sitehub-postgres pg_isready -U $user 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Success "PostgreSQL hazır."
            return
        }
        Start-Sleep -Milliseconds 500
        Write-Host "." -NoNewline
    }
    Write-Host ""
    Write-Err "PostgreSQL $TimeoutSeconds saniye içinde ayağa kalkmadı."
    exit 1
}

# ═══════════════════════════════════════════════════════════════════════════════
# MOTW (Mark-of-the-Web) temizliği — zip'ten gelen dosyaların bloklarını kaldır
# ═══════════════════════════════════════════════════════════════════════════════
function Clear-Motw {
    # Windows dışında gereksiz
    if (-not $IsWindows -and $PSVersionTable.PSVersion.Major -ge 6) { return }
    if ($PSVersionTable.PSVersion.Major -lt 6 -and $env:OS -ne 'Windows_NT') { return }

    try {
        # Hem görünür hem gizli dosyalar (-Force), kritik uzantılar
        $files = Get-ChildItem -Path $repoRoot -Recurse -File -Force -ErrorAction SilentlyContinue |
            Where-Object { $_.Extension -in '.ps1', '.psm1', '.psd1', '.cmd', '.bat', '.json',
                                            '.cs', '.csproj', '.sln', '.props', '.targets',
                                            '.razor', '.md', '.yml', '.yaml' }

        $unblockCount = 0
        foreach ($file in $files) {
            $motw = Get-Item -Path $file.FullName -Stream Zone.Identifier -ErrorAction SilentlyContinue
            if ($motw) {
                Unblock-File -Path $file.FullName -ErrorAction SilentlyContinue
                $unblockCount++
            }
        }
        if ($unblockCount -gt 0) {
            Write-Info "$unblockCount dosyanın Mark-of-the-Web işareti kaldırıldı."
        }
    }
    catch {
        # Sessizce geç — bu bloklanmış dosyaları script çağrısını durdurmamalı
    }
}


function Test-PortAvailable {
    param([int]$Port)
    try {
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
        $listener.Start()
        $listener.Stop()
        return $true
    }
    catch {
        return $false
    }
}

function Test-RequiredPorts {
    $ports = @(
        @{ Port = [int]($env:POSTGRES_HOST_PORT); Name = 'PostgreSQL'; Var = 'POSTGRES_HOST_PORT' }
        @{ Port = [int]($env:REDIS_HOST_PORT);    Name = 'Redis';      Var = 'REDIS_HOST_PORT' }
        @{ Port = [int]($env:SEQ_HOST_PORT);      Name = 'Seq';        Var = 'SEQ_HOST_PORT' }
        @{ Port = [int]($env:MAILHOG_HOST_PORT_SMTP); Name = 'MailHog SMTP'; Var = 'MAILHOG_HOST_PORT_SMTP' }
        @{ Port = [int]($env:MAILHOG_HOST_PORT_UI);   Name = 'MailHog UI';   Var = 'MAILHOG_HOST_PORT_UI' }
        @{ Port = [int]($env:MINIO_HOST_PORT_API);    Name = 'MinIO API';    Var = 'MINIO_HOST_PORT_API' }
        @{ Port = [int]($env:MINIO_HOST_PORT_CONSOLE);Name = 'MinIO Console';Var = 'MINIO_HOST_PORT_CONSOLE' }
        @{ Port = [int]($env:PGADMIN_HOST_PORT);      Name = 'pgAdmin';      Var = 'PGADMIN_HOST_PORT' }
    )

    $conflicts = @()
    foreach ($p in $ports) {
        if ($p.Port -gt 0 -and -not (Test-PortAvailable $p.Port)) {
            $conflicts += $p
        }
    }

    if ($conflicts.Count -eq 0) { return }

    Write-Err "╔════════════════════════════════════════════════════════════╗"
    Write-Err "║  Port çakışması tespit edildi                              ║"
    Write-Err "╚════════════════════════════════════════════════════════════╝"
    Write-Host ""
    foreach ($c in $conflicts) {
        Write-Host "    • Port $($c.Port) ($($c.Name)) meşgul" -ForegroundColor Yellow

        # Hangi process kullanıyor, söyle
        try {
            $conn = Get-NetTCPConnection -LocalPort $c.Port -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($conn) {
                $proc = Get-Process -Id $conn.OwningProcess -ErrorAction SilentlyContinue
                if ($proc) {
                    Write-Host "      → $($proc.ProcessName) (PID: $($proc.Id))" -ForegroundColor DarkGray
                }
            }
        } catch { }
    }
    Write-Host ""
    Write-Info "Çözüm 1: Çakışan süreci durdur (Stop-Service veya Stop-Process)"
    Write-Info "Çözüm 2: .env dosyasında şu değişkeni değiştir:"
    foreach ($c in $conflicts) {
        Write-Host "           $($c.Var)=1$($c.Port)" -ForegroundColor Cyan
    }
    Write-Host ""
    exit 1
}

function Invoke-Up {
    Test-RequiredPorts
    Write-Step "Docker servisleri başlatılıyor..."
    Push-Location $dockerDir
    try {
        docker compose --env-file $envFile up -d
        if ($LASTEXITCODE -ne 0) { throw "docker compose up başarısız" }
    }
    finally { Pop-Location }
    Write-Success "Servisler başlatıldı."
    Start-Sleep -Seconds 1
    Show-Status
}

function Invoke-Down {
    Write-Step "Docker servisleri durduruluyor..."
    Push-Location $dockerDir
    try {
        docker compose --env-file $envFile down
    }
    finally { Pop-Location }
    Write-Success "Servisler durduruldu. (Veriler korundu.)"
}

function Invoke-Restart {
    Invoke-Down
    Invoke-Up
}

function Invoke-Pull {
    Write-Step "Docker imajları güncelleniyor (son sürüm çekiliyor)..."
    Push-Location $dockerDir
    try {
        docker compose --env-file $envFile pull
        if ($LASTEXITCODE -ne 0) { throw "docker compose pull başarısız" }
    }
    finally { Pop-Location }
    Write-Success "İmajlar güncellendi."
}

function Invoke-Reset {
    Write-Warn "╔════════════════════════════════════════════════════════════╗"
    Write-Warn "║  DİKKAT: Bu işlem TÜM veritabanı verilerini SİLECEK!       ║"
    Write-Warn "║  Postgres, Redis, MinIO, Seq, pgAdmin — hepsi sıfırlanacak.║"
    Write-Warn "╚════════════════════════════════════════════════════════════╝"
    $confirm = Read-Host "Onay için 'RESET' yazın (iptal için Enter)"
    if ($confirm -ne 'RESET') {
        Write-Info "İptal edildi."
        return
    }

    Write-Step "Volume'lar dahil silme..."
    Push-Location $dockerDir
    try {
        docker compose --env-file $envFile down -v --remove-orphans
    }
    finally { Pop-Location }
    Write-Success "Temiz durum."

    # Son güncellemelerle başlatmak için pull
    Invoke-Pull

    Invoke-Up
    Wait-ForPostgres
    Invoke-Migrate
}

function Invoke-Nuke {
    Write-Warn "╔════════════════════════════════════════════════════════════╗"
    Write-Warn "║  ☢️   NUCLEAR OPTION  ☢️                                   ║"
    Write-Warn "║                                                            ║"
    Write-Warn "║  Tüm SiteHub docker kaynaklarını siler:                    ║"
    Write-Warn "║    • Container'lar (durdur + sil)                          ║"
    Write-Warn "║    • Volume'lar (VERİLER GİDER)                            ║"
    Write-Warn "║    • Network                                               ║"
    Write-Warn "║    • İmajlar yeniden çekilir                               ║"
    Write-Warn "║    • dotnet tool cache yenilenir                           ║"
    Write-Warn "║    • Migration'lar sıfırdan uygulanır                      ║"
    Write-Warn "║                                                            ║"
    Write-Warn "║  Projenin sıfırdan, en son haliyle kurulumu için.          ║"
    Write-Warn "╚════════════════════════════════════════════════════════════╝"
    $confirm = Read-Host "Onay için 'NUKE' yazın (iptal için Enter)"
    if ($confirm -ne 'NUKE') {
        Write-Info "İptal edildi."
        return
    }

    Write-Step "1/6 — Mevcut container ve volume'ları siliyorum..."
    Push-Location $dockerDir
    try {
        docker compose --env-file $envFile down -v --remove-orphans --rmi local
    }
    finally { Pop-Location }

    Write-Step "2/6 — Dangling (sahipsiz) docker kaynakları temizleniyor..."
    docker system prune -f 2>&1 | Out-Null

    Write-Step "3/6 — Son imajlar çekiliyor..."
    Push-Location $dockerDir
    try {
        docker compose --env-file $envFile pull
    }
    finally { Pop-Location }

    Write-Step "4/6 — dotnet tool'lar yeniden yükleniyor..."
    Push-Location $repoRoot
    try {
        # Önce clean
        if (Test-Path (Join-Path $repoRoot '.config/dotnet-tools.json')) {
            dotnet tool restore 2>&1 | Out-Null
        }
    }
    finally { Pop-Location }

    Write-Step "5/6 — Servisler başlatılıyor..."
    Invoke-Up
    Wait-ForPostgres

    Write-Step "6/6 — Migration'lar uygulanıyor..."
    Invoke-Migrate

    Write-Host ""
    Write-Success "╔════════════════════════════════════════════════════════════╗"
    Write-Success "║  Nuke tamamlandı — sıfırdan, temiz, son haliyle kurulum.   ║"
    Write-Success "╚════════════════════════════════════════════════════════════╝"
    Show-Status
}

function Show-Status {
    Write-Step "Servis durumu:"
    Push-Location $dockerDir
    try {
        docker compose --env-file $envFile ps
    }
    finally { Pop-Location }

    Write-Host ""
    Write-Info "Web arayüzleri:"
    Write-Host "    pgAdmin (DB yönetim):   http://localhost:$($env:PGADMIN_HOST_PORT)" -ForegroundColor DarkCyan
    Write-Host "    Seq (loglar):            http://localhost:$($env:SEQ_HOST_PORT)" -ForegroundColor DarkCyan
    Write-Host "    MailHog (e-postalar):    http://localhost:$($env:MAILHOG_HOST_PORT_UI)" -ForegroundColor DarkCyan
    Write-Host "    MinIO Console:           http://localhost:$($env:MINIO_HOST_PORT_CONSOLE)" -ForegroundColor DarkCyan
}

function Show-Logs {
    param([string]$Service)
    if (-not $Service) {
        Write-Err "Servis adı gerekli. Örn: ./dev-infra.ps1 logs postgres"
        Write-Info "Servisler: postgres, redis, seq, mailhog, minio"
        exit 1
    }
    Push-Location $dockerDir
    try {
        docker compose --env-file $envFile logs -f $Service
    }
    finally { Pop-Location }
}

function Invoke-Migrate {
    Write-Step "EF Core migration'ları uygulanıyor..."
    Push-Location $repoRoot
    try {
        # Local tool restore (dotnet-ef vs.)
        Write-Info "dotnet tool restore..."
        dotnet tool restore 2>&1 | Out-Null

        Wait-ForPostgres

        # Migration var mı? Yoksa önce initial migration öner
        $hasMigrations = Test-Path (Join-Path $repoRoot 'src/SiteHub.Infrastructure/Persistence/Migrations')
        if (-not $hasMigrations) {
            Write-Warn "Henüz migration yok."
            Write-Info "İlk migration için: ./scripts/dev-infra.ps1 add-migration InitialCreate"
            return
        }

        dotnet ef database update `
            --project src/SiteHub.Infrastructure `
            --startup-project src/SiteHub.ManagementPortal `
            --context SiteHubDbContext

        if ($LASTEXITCODE -ne 0) { throw "Migration uygulaması başarısız." }
    }
    finally { Pop-Location }
    Write-Success "Migration'lar uygulandı."
}

function Invoke-AddMigration {
    param([string]$Name)
    if (-not $Name) {
        Write-Err "Migration adı gerekli. Örn: ./dev-infra.ps1 add-migration InitialCreate"
        exit 1
    }

    Write-Step "Migration ekleniyor: $Name"
    Push-Location $repoRoot
    try {
        Write-Info "dotnet tool restore..."
        dotnet tool restore
        if ($LASTEXITCODE -ne 0) {
            Write-Err "dotnet tool restore başarısız. Yukarıdaki çıktıya bakın."
            exit 1
        }

        dotnet ef migrations add $Name `
            --project src/SiteHub.Infrastructure `
            --startup-project src/SiteHub.ManagementPortal `
            --context SiteHubDbContext `
            --output-dir Persistence/Migrations

        if ($LASTEXITCODE -ne 0) { throw "Migration eklenemedi." }
    }
    finally { Pop-Location }
    Write-Success "Migration '$Name' eklendi. Uygulamak için: ./dev-infra.ps1 migrate"
}

function Invoke-Psql {
    Write-Info "psql shell açılıyor... (çıkmak için \q)"
    docker exec -it sitehub-postgres psql -U $env:POSTGRES_USER -d $env:POSTGRES_DB
}

function Invoke-Setup {
    Write-Step "═══ SiteHub İlk Kurulum ═══"
    Invoke-Up
    Wait-ForPostgres
    Invoke-Migrate

    Write-Host ""
    Write-Success "╔════════════════════════════════════════════════════════════╗"
    Write-Success "║  Kurulum tamamlandı!                                        ║"
    Write-Success "╚════════════════════════════════════════════════════════════╝"
    Write-Info "Şimdi uygulamayı başlat:"
    Write-Host "    dotnet run --project src/SiteHub.ManagementPortal" -ForegroundColor Yellow
    Write-Info "Sonra tarayıcıda aç: https://localhost:5001"
}

function Show-Help {
    Get-Help $MyInvocation.MyCommand.Path -Detailed
}

# ═══════════════════════════════════════════════════════════════════════════════
# Ana akış
# ═══════════════════════════════════════════════════════════════════════════════

Test-Prerequisites
Clear-Motw
Import-DotEnv

switch ($Command) {
    'up'              { Invoke-Up }
    'down'            { Invoke-Down }
    'restart'         { Invoke-Restart }
    'reset'           { Invoke-Reset }
    'nuke'            { Invoke-Nuke }
    'pull'            { Invoke-Pull }
    'status'          { Show-Status }
    'logs'            { Show-Logs $Args[0] }
    'migrate'         { Invoke-Migrate }
    'add-migration'   { Invoke-AddMigration $Args[0] }
    'psql'            { Invoke-Psql }
    'setup'           { Invoke-Setup }
    'help'            { Show-Help }
}
