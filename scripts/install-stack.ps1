param(
    [string]$ApacheZipUrl,
    [string]$PhpZipUrl,
    [string]$MariaDbZipUrl
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$downloads = Join-Path $root '.downloads'
$stack = Join-Path $root 'stack'

New-Item -ItemType Directory -Force -Path $downloads, $stack | Out-Null

function Install-Zip {
    param(
        [Parameter(Mandatory=$true)][string]$Url,
        [Parameter(Mandatory=$true)][string]$Name,
        [Parameter(Mandatory=$true)][string]$Destination
    )

    $zip = Join-Path $downloads "$Name.zip"
    Write-Host "Downloading $Name..."
    Invoke-WebRequest -Uri $Url -OutFile $zip

    $temp = Join-Path $downloads "$Name-extract"
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $temp | Out-Null
    Expand-Archive -LiteralPath $zip -DestinationPath $temp -Force

    $candidate = Get-ChildItem -LiteralPath $temp -Directory | Select-Object -First 1
    Remove-Item -LiteralPath $Destination -Recurse -Force -ErrorAction SilentlyContinue
    if ($candidate) {
        Move-Item -LiteralPath $candidate.FullName -Destination $Destination
    } else {
        Move-Item -LiteralPath $temp -Destination $Destination
    }
}

if ($ApacheZipUrl) { Install-Zip -Url $ApacheZipUrl -Name 'apache' -Destination (Join-Path $stack 'apache') }
if ($PhpZipUrl) { Install-Zip -Url $PhpZipUrl -Name 'php' -Destination (Join-Path $stack 'php') }
if ($MariaDbZipUrl) { Install-Zip -Url $MariaDbZipUrl -Name 'mariadb' -Destination (Join-Path $stack 'mariadb') }

New-Item -ItemType Directory -Force -Path (Join-Path $root 'www'), (Join-Path $root 'data') | Out-Null
Write-Host 'Stack install finished. Verify stack/apache/bin/httpd.exe, stack/php/php.exe, and stack/mariadb/bin/mysqld.exe.'
