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

## Install Locally

```powershell
cd "C:\Program Files\XDB-database"
.\install-winui.bat
```

If you downloaded a GitHub Release zip, extract it and run `Install-XDB-database.bat`.

## Release Notes

See [CHANGELOG.md](CHANGELOG.md).
