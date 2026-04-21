<#
.SYNOPSIS
    .env dosyasını PowerShell session'ına yükler (${VAR} placeholder'larını genişleterek).

.DESCRIPTION
    docker-compose.yml tarzı .env dosyasındaki KEY=VALUE çiftlerini process-level
    environment variable olarak set eder. ${OTHER_VAR} placeholder'larını manuel
    genişletir (docker-compose bunu otomatik yapar, PowerShell yapmaz).

    PowerShell 5.1 uyumlu (?? operator kullanmaz).

.EXAMPLE
    . .\env.ps1
    # Not: dot-sourcing (başındaki nokta) gerekli — aksi halde env var'lar
    # sadece script'in alt-scope'unda kalır, parent PowerShell session'ına yansımaz.

.NOTES
    Her YENİ PowerShell oturumunda bir kez çalıştırılmalıdır.
    dotnet run / dotnet ef / docker komutları bu variable'ları okur.
#>

$envFile = Join-Path $PSScriptRoot ".env"
if (-not (Test-Path $envFile)) {
    Write-Host "UYARI: .env dosyası bulunamadı ($envFile). .env.example'dan kopyalayın." -ForegroundColor Yellow
    return
}

$loaded = 0
Get-Content $envFile | ForEach-Object {
    if ($_ -match '^\s*([^#=][^=]*?)\s*=\s*(.*?)\s*$') {
        $key = $matches[1].Trim()
        $val = $matches[2].Trim()

        # ${VAR} placeholder'ları genişlet (docker-compose tarzı)
        $val = [regex]::Replace($val, '\$\{([^}]+)\}', {
            param($m)
            $envName = $m.Groups[1].Value
            $envVal = [Environment]::GetEnvironmentVariable($envName, 'Process')
            if ($envVal) { $envVal } else { '' }
        })

        [Environment]::SetEnvironmentVariable($key, $val, 'Process')
        $loaded++
    }
}

Write-Host ".env yüklendi: $loaded değişken." -ForegroundColor Green
