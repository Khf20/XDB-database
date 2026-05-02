$ErrorActionPreference = 'Stop'

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from PowerShell as Administrator.'
}

$service = Get-Service Apache2.4 -ErrorAction SilentlyContinue
if ($service) {
    if ($service.Status -ne 'Stopped') {
        Stop-Service Apache2.4 -Force
        $service.WaitForStatus('Stopped', '00:00:30')
    }
}

Get-Process httpd -ErrorAction SilentlyContinue | Stop-Process -Force

Get-Service Apache2.4 -ErrorAction SilentlyContinue | Select-Object Name,Status,StartType
Get-Process httpd -ErrorAction SilentlyContinue | Select-Object Id,ProcessName,Path
