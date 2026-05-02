# XDB-database

XDB-database is a Windows desktop control app for a local XAMPP installation.

It provides a modern dashboard for:

- Apache start, stop, restart, and status
- MySQL/MariaDB start, stop, restart, and status
- PHP version visibility and Apache PHP switching
- XAMPP log viewing
- Shortcuts to phpMyAdmin, htdocs, Apache config, and MySQL data

## Requirements

- Windows
- XAMPP installed at `C:\xampp`
- .NET Framework C# compiler, usually available at `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`

## Build

### WinForms legacy build

Run:

```bat
build.bat
```

The output will be:

```text
XDB-database.exe
```

### WinUI 3 build

The WinUI 3 rewrite lives in:

```text
src\XDBDatabase.WinUI
```

Build it with:

```bat
build-winui.bat
```

Create an MSIX package with:

```bat
package-winui.bat
```

WinUI 3 apps need Windows App Runtime installed and MSIX packages must be signed/trusted before installation.

Install and launch from an Administrator PowerShell with:

```bat
install-winui.bat
```

## Notes

This app expects XAMPP paths under `C:\xampp`. If your XAMPP is installed somewhere else, update the `root` value in `XdbDatabase.cs`.
