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

## Requirements

- Windows 10/11
- XAMPP installed at `C:\xampp`
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

Run PowerShell as Administrator, then:

```powershell
cd C:\xampp\XDB-database
.\install-winui.bat
```

The installer script will:

- build the WinUI 3 MSIX package
- create a local development signing certificate if needed
- trust the certificate on the local machine
- sign the MSIX package
- install and launch XDB-database

If you downloaded a GitHub Release zip instead of cloning the repo, extract the zip and run `install-winui.bat` from the extracted folder as Administrator. The script will sign and install the included MSIX package.

## Apache Service Notes

If Apache is installed as the Windows service `Apache2.4`, stopping or starting it requires Administrator permission. XDB-database will trigger a Windows UAC prompt for those actions.

For manual emergency stop:

```powershell
.\stop-apache-admin.bat
```

## Release Notes

See [CHANGELOG.md](CHANGELOG.md).
