param(
    [switch]$SkipPackage
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'src\XDBDatabase.WinUI\XDBDatabase.WinUI.csproj'
$packageRoot = Join-Path $root 'src\XDBDatabase.WinUI\AppPackages'
$certSubject = 'CN=XDBDatabase'
$certName = 'XDB-database Dev Certificate'

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from PowerShell as Administrator.'
}

if ((Test-Path -LiteralPath $project) -and -not $SkipPackage) {
    dotnet publish $project -c Release -p:Platform=x64 -p:GenerateAppxPackageOnBuild=true -p:AppxPackageSigningEnabled=false -p:AppxBundle=Never
}

$searchRoots = @()
if (Test-Path -LiteralPath $packageRoot) { $searchRoots += $packageRoot }
$searchRoots += $root

$msix = $searchRoots |
    ForEach-Object { Get-ChildItem -Path $_ -Recurse -Filter '*.msix' -ErrorAction SilentlyContinue } |
    Where-Object { $_.FullName -notmatch '\\Dependencies\\' -and $_.Name -like '*_x64.msix' } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $msix) {
    throw 'MSIX package was not found. Run package-winui.bat first, or place the release MSIX next to this script.'
}

$signtool = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Recurse -Filter signtool.exe |
    Where-Object { $_.FullName -like '*\x64\signtool.exe' } |
    Sort-Object FullName -Descending |
    Select-Object -First 1

if (-not $signtool) {
    throw 'signtool.exe was not found. Install Windows SDK build tools.'
}

$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $certSubject } | Select-Object -First 1
if (-not $cert) {
    $cert = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $certSubject `
        -KeyUsage DigitalSignature `
        -FriendlyName $certName `
        -CertStoreLocation Cert:\CurrentUser\My `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3')
}

$cer = Join-Path $root 'XDBDatabaseDev.cer'
Export-Certificate -Cert $cert -FilePath $cer -Force | Out-Null
Import-Certificate -FilePath $cer -CertStoreLocation Cert:\LocalMachine\Root | Out-Null
Import-Certificate -FilePath $cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null

& $signtool.FullName sign /fd SHA256 /sha1 $cert.Thumbprint $msix.FullName
if ($LASTEXITCODE -ne 0) { throw 'signtool sign failed.' }

& $signtool.FullName verify /pa $msix.FullName
if ($LASTEXITCODE -ne 0) { throw 'signtool verify failed.' }

Get-AppxPackage *XDBDatabase* | Remove-AppxPackage -ErrorAction SilentlyContinue
Get-AppxPackage |
    Where-Object { $_.Publisher -eq $certSubject -or $_.Name -eq '1B9D8D8D-F4E5-480D-8A36-60A54D175E93' } |
    Remove-AppxPackage -ErrorAction SilentlyContinue
Add-AppxPackage -Path $msix.FullName

$app = Get-StartApps | Where-Object { $_.Name -eq 'XDB-database' -or $_.AppID -like '*XDBDatabase*' } | Select-Object -First 1
if ($app) {
    Start-Process "shell:AppsFolder\$($app.AppID)"
    Write-Host "Installed and launched $($app.Name)"
} else {
    $pkg = Get-AppxPackage | Where-Object { $_.Publisher -eq $certSubject } | Select-Object -First 1
    if ($pkg) {
        Write-Host "Installed $($pkg.Name), but Start Menu app lookup did not return an app yet."
    } else {
        Write-Host 'Package installed, but package lookup did not return an app.'
    }
}
