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

Run:

```bat
build.bat
```

The output will be:

```text
XDB-database.exe
```

## Notes

This app expects XAMPP paths under `C:\xampp`. If your XAMPP is installed somewhere else, update the `root` value in `XdbDatabase.cs`.
