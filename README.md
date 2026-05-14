# XDB-database

XDB-database is a Windows desktop control app for a local XAMPP installation.

The current app is built with **C# + WinUI 3** and is designed as a modern desktop dashboard for Apache, MySQL/MariaDB, PHP switching, logs, and common XAMPP tools.

## Features

- Apache start, stop, restart, and status
- MySQL/MariaDB start, stop, restart, and status
- Administrator-aware Apache service control for `Apache2.4`
- PHP version visibility and Apache PHP switching
- XAMPP log viewer
- Shortcuts to `htdocs`, Apache config, MySQL data, phpMyAdmin, and XAMPP dashboard
- Dark desktop UI inspired by the XDB mobile dashboard mockups
- Settings page for XAMPP root, service names, and preferred browser
- App activity log for service actions and errors
- Startup validation for required XAMPP folders/files
- phpMyAdmin access repair helper with config backup and Apache syntax validation
- Service transition states with small progress indicators
- Port conflict detection for ports 80, 443, and 3306
- Dashboard health summary for Apache, MySQL, phpMyAdmin, and PHP version mismatch
- Safer PHP Switcher with preview, validation, and rollback

## Requirements

- Windows 10/11
- XAMPP installed at `C:\xampp` by default, or another folder configured in Settings
- .NET SDK 10 for building from source
- Windows App Runtime 1.8

## Build

```bat
build-winui.bat
```

## Package

```bat
package-winui.bat
```

## Install Locally

Run PowerShell, then:

```powershell
cd "C:\Program Files\XDB-database"
.\install-winui.bat
```

The installer script will:

- build the WinUI 3 MSIX package
- create a local development signing certificate if needed
- trust the certificate for the current Windows user
- sign the MSIX package
- install included Windows App Runtime dependencies when they are bundled with a release zip
- install and launch XDB-database

If you downloaded a GitHub Release zip instead of cloning the repo, extract the zip and run `Install-XDB-database.bat` from the extracted folder. The installer will sign and install the included MSIX bundle.

## Apache Service Notes

If Apache is installed as the Windows service `Apache2.4`, stopping or starting it requires Administrator permission. XDB-database will trigger a Windows UAC prompt for those actions.

For manual emergency stop:

```powershell
.\stop-apache-admin.bat
```

## Release Notes

See [CHANGELOG.md](CHANGELOG.md).
