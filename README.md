# XDB-database

XDB-database is a Windows desktop app for controlling a portable local database and PHP development stack.

The app is built with **C# + WinUI 3** and is moving toward a self-contained stack layout: Apache, MariaDB, PHP, web files, and database data live beside the app instead of depending on a system-wide XAMPP install.

## Portable Layout

```text
XDB-database/
├── XDB-database.exe
├── config/
│   ├── httpd.template.conf
│   └── my.template.ini
├── stack/
│   ├── apache/
│   ├── mariadb/
│   └── php/
├── www/
└── data/
```

Place portable binaries in:

- `stack/apache` from Apache Lounge, containing `bin/httpd.exe`
- `stack/mariadb` from MariaDB portable/ZIP, containing `bin/mysqld.exe`
- `stack/php` or `stack/php8.2`, containing `php.exe`, `php.ini`, `php8ts.dll`, and `php8apache2_4.dll`

## Features

- Apache start, stop, restart, and status by direct child process control
- MariaDB start, stop, restart, and status by direct child process control
- No Windows Service or `sc.exe` requirement for portable stack actions
- Dynamic config rendering from `config/*.template.*`
- PHP switcher for isolated `stack/php*` folders
- Open Terminal with temporary PATH injection for PHP and MariaDB
- Optional `scripts/install-stack.ps1` helper for downloading ZIP binaries when URLs are provided
- MariaDB data initialization when `mariadb-install-db.exe` is present
- Automatic fallback to Apache port 8080 and MariaDB port 3307 when default ports are busy
- Log viewer, activity log, port conflict detection, and health summary
- phpMyAdmin access repair helper
- WinUI 3 dashboard with service transition states and non-blocking InfoBar feedback

## Build

```bat
build-winui.bat
```

## Package

```bat
package-winui.bat
```

## Make Release Folder

```bat
make-release.bat
```

The release folder includes the MSIX bundle plus `config`, `stack`, `www`, and `data` folders so the portable layout stays together.

## Install Stack Binaries

XDB does not commit Apache/PHP/MariaDB binaries into Git. Provide ZIP URLs manually:

```powershell
.\scripts\install-stack.ps1 `
  -ApacheZipUrl "https://example.com/apache.zip" `
  -PhpZipUrl "https://example.com/php.zip" `
  -MariaDbZipUrl "https://example.com/mariadb.zip"
```

phpMyAdmin can be extracted to `apps/phpmyadmin`; Apache will expose it as `/phpmyadmin` when that folder exists.

## Install Locally

```powershell
cd "C:\Program Files\XDB-database"
.\install-winui.bat
```

If you downloaded a GitHub Release zip, extract it and run `Install-XDB-database.bat`.

## Release Notes

See [CHANGELOG.md](CHANGELOG.md).
