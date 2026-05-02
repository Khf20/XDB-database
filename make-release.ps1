$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$releaseDir = Join-Path $root 'release'
$version = 'v1.0.0'
$outDir = Join-Path $releaseDir $version

Remove-Item -LiteralPath $outDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

& (Join-Path $root 'package-winui.bat')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$packageRoot = Join-Path $root 'src\XDBDatabase.WinUI\AppPackages'
$msix = Get-ChildItem -Path $packageRoot -Recurse -Filter '*.msix' |
    Where-Object { $_.FullName -notmatch '\\Dependencies\\' -and $_.Name -like '*_x64.msix' } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($msix) {
    Copy-Item -LiteralPath $msix.FullName -Destination $outDir
}

Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $outDir
Copy-Item -LiteralPath (Join-Path $root 'CHANGELOG.md') -Destination $outDir
Copy-Item -LiteralPath (Join-Path $root 'install-winui.bat') -Destination $outDir
Copy-Item -LiteralPath (Join-Path $root 'install-winui.ps1') -Destination $outDir

$zip = Join-Path $releaseDir "XDB-database-$version.zip"
Remove-Item -LiteralPath $zip -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $outDir '*') -DestinationPath $zip

Write-Host "Release assets created:"
Get-ChildItem -LiteralPath $outDir
Get-Item -LiteralPath $zip
