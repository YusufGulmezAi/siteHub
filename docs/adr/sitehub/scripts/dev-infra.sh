#!/usr/bin/env bash
# ═════════════════════════════════════════════════════════════════════════════
#  SiteHub dev-infra wrapper — Linux / macOS için
#
#  Kullanım:
#    ./scripts/dev-infra.sh setup
#    ./scripts/dev-infra.sh up
#    ./scripts/dev-infra.sh migrate
# ═════════════════════════════════════════════════════════════════════════════

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if ! command -v pwsh &> /dev/null; then
    echo ""
    echo "================================================================"
    echo "  HATA: PowerShell 7 (pwsh) bulunamadı."
    echo "================================================================"
    echo ""
    echo "  Kurulum:"
    echo "    macOS:        brew install --cask powershell"
    echo "    Ubuntu/Debian: sudo apt install -y powershell"
    echo "    Diğer:        https://aka.ms/powershell-release"
    echo ""
    exit 1
fi

pwsh -NoProfile -File "${SCRIPT_DIR}/dev-infra.ps1" "$@"
