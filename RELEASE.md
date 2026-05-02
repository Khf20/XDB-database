# Release Checklist

Use this checklist for GitHub releases.

1. Build:

```powershell
.\build-winui.bat
```

2. Package and install locally as Administrator:

```powershell
.\install-winui.bat
```

3. Verify:

- Dashboard opens as `XDB-database`.
- Apache and MySQL/MariaDB statuses refresh.
- phpMyAdmin button opens the browser.
- Tools page opens the expected folders/pages.
- Apache Stop shows UAC when Apache runs as `Apache2.4` service.

4. Commit everything:

```powershell
git status
git add .
git commit -m "Prepare v1.0.0 release"
```

5. Tag:

```powershell
git tag v1.0.0
```

6. Push:

```powershell
git push origin main
git push origin v1.0.0
```

7. Create a GitHub Release from tag `v1.0.0`.
