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
$package = Get-ChildItem -Path $packageRoot -Recurse -Include '*.msixbundle','*.msix' |
    Where-Object { $_.FullName -notmatch '\\Dependencies\\' -and ($_.Name -like '*.msixbundle' -or $_.Name -like '*_x64.msix') } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($package) {
    Copy-Item -LiteralPath $package.FullName -Destination $outDir
    $dependencies = Join-Path $package.DirectoryName 'Dependencies\x64'
    if (Test-Path -LiteralPath $dependencies) {
        $dependencyOut = Join-Path $outDir 'Dependencies\x64'
        New-Item -ItemType Directory -Force -Path $dependencyOut | Out-Null
        Copy-Item -Path (Join-Path $dependencies '*') -Destination $dependencyOut
    }
}

Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $outDir
Copy-Item -LiteralPath (Join-Path $root 'CHANGELOG.md') -Destination $outDir
Copy-Item -LiteralPath (Join-Path $root 'Install-XDB-database.bat') -Destination $outDir
Copy-Item -LiteralPath (Join-Path $root 'install-winui.bat') -Destination $outDir
Copy-Item -LiteralPath (Join-Path $root 'install-winui.ps1') -Destination $outDir

foreach ($folder in @('config', 'stack', 'www', 'data')) {
    $source = Join-Path $root $folder
    if (Test-Path -LiteralPath $source) {
        Copy-Item -LiteralPath $source -Destination (Join-Path $outDir $folder) -Recurse -Force
    }
}

$zip = Join-Path $releaseDir "XDB-database-$version.zip"
Remove-Item -LiteralPath $zip -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $outDir '*') -DestinationPath $zip

Write-Host "Release assets created:"
Get-ChildItem -LiteralPath $outDir
Get-Item -LiteralPath $zip
