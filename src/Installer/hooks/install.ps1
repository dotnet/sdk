<#
.SYNOPSIS
    Installs the dotnetup pre-commit hook into .git/hooks (Windows).
.DESCRIPTION
    Run from anywhere inside the repo:
        powershell -File src/Installer/hooks/install.ps1
#>

$ErrorActionPreference = 'Stop'

$repoRoot = git rev-parse --show-toplevel
$hookSrc = Join-Path $repoRoot 'src/Installer/hooks/pre-commit'
$hookDst = Join-Path $repoRoot '.git/hooks/pre-commit'

if (Test-Path $hookDst) {
    Write-Host "Pre-commit hook already exists at $hookDst"
    $alt = Join-Path $repoRoot '.git/hooks/pre-commit-dotnetup'
    Copy-Item $hookSrc $alt -Force
    Write-Host "Copied dotnetup hook to $alt - call it from your existing hook."
}
else {
    Copy-Item $hookSrc $hookDst -Force
    Write-Host "Pre-commit hook installed at $hookDst"
}
