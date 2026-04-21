@echo off
REM ═══════════════════════════════════════════════════════════════════════════
REM  SiteHub dev-infra wrapper — Windows icin kolay cagirici
REM
REM  Ne yapar?
REM  - Once PowerShell 7 (pwsh) bulmaya calisir (tercih edilen)
REM  - Yoksa Windows PowerShell 5.1 (powershell) kullanir (script uyumlu)
REM  - Execution policy bypass ile calistirir (MOTW/signed sorunu olmaz)
REM
REM  Kullanim:
REM    scripts\dev-infra.cmd setup
REM    scripts\dev-infra.cmd up
REM    scripts\dev-infra.cmd migrate
REM ═══════════════════════════════════════════════════════════════════════════

setlocal

REM 1. Once pwsh (PowerShell 7) dene — tercih edilen
where pwsh >nul 2>&1
if not errorlevel 1 (
    pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0dev-infra.ps1" %*
    exit /b %ERRORLEVEL%
)

REM 2. Yoksa Windows PowerShell 5.1'e geri don (her Windows'ta vardir)
where powershell >nul 2>&1
if not errorlevel 1 (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0dev-infra.ps1" %*
    exit /b %ERRORLEVEL%
)

echo.
echo ================================================================
echo  HATA: PowerShell bulunamadi.
echo ================================================================
echo  Bu sisteminizde olmasi imkansiz bir durum. Sistem yoneticinize basvurun.
echo.
exit /b 1
