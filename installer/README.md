# FlipKit Installers

Per-platform installer assets, driven by the build scripts at the repo root. Output lands in `releases/`.

| Platform | Source | Build script |
|---|---|---|
| Windows (`.exe`) | [Windows/FlipKit.iss](Windows/FlipKit.iss) — Inno Setup script | [`build-hub-for-installer.ps1`](../build-hub-for-installer.ps1) |
| Linux (`.deb`/`.rpm`) | [Linux/build-packages.sh](Linux/build-packages.sh) + [postinst.sh](Linux/postinst.sh) | [`build-installers.ps1`](../build-installers.ps1) |
| macOS (`.dmg`) | [Mac/create-dmg.sh](Mac/create-dmg.sh) + [README-INSTALL.txt](Mac/README-INSTALL.txt) | run `Mac/create-dmg.sh` on a Mac |

The cross-platform self-contained **Hub `.zip`** packages (no installer, just unzip and run) come from [`build-release.ps1`](../build-release.ps1).

---

## Building the Windows installer (`FlipKit-Setup-v<version>.exe`)

This is the `.exe` users double-click to install. It wraps the self-contained Hub (Desktop single-file exe + `servers/` Web & API + Docs) into an Inno Setup installer that creates Start-menu/desktop shortcuts and an optional auto-start entry.

### Prerequisite: Inno Setup 6

The build needs Inno Setup's command-line compiler, **`ISCC.exe`**. It is **not installed by default** on dev machines and is not bundled in this repo. `build-hub-for-installer.ps1` looks for it, in order, at:

1. `%LocalAppData%\Programs\Inno Setup 6\ISCC.exe`  ← per-user install (no admin)
2. `C:\Program Files (x86)\Inno Setup 6\ISCC.exe`  ← machine-wide install
3. `C:\Program Files\Inno Setup 6\ISCC.exe`
4. `iscc` on `PATH`

Install it one of these ways:

```powershell
# Machine-wide (run in an ELEVATED terminal):
winget install JRSoftware.InnoSetup --accept-package-agreements --accept-source-agreements
#   or
choco install innosetup -y

# Per-user, NO admin (installs to %LocalAppData%\Programs\Inno Setup 6, which the script checks first):
$exe = "$env:TEMP\innosetup-6.exe"
Invoke-WebRequest -Uri 'https://jrsoftware.org/download.php/is.exe' -OutFile $exe -UseBasicParsing
& $exe /VERYSILENT /CURRENTUSER /SUPPRESSMSGBOXES /NORESTART /NOICONS "/DIR=$env:LOCALAPPDATA\Programs\Inno Setup 6"
```

Verify: `Test-Path "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"` (or check the Program Files path).

### Build

```powershell
# From the repo root. Set the version near the top of the script first
# (it is currently hardcoded — see "Known gotchas").
.\build-hub-for-installer.ps1
```

This (1) publishes Desktop + Web + API self-contained into `releases\temp\FlipKit-Hub-Windows-x64-v<version>\`, then (2) runs `ISCC /DVersion=<version> Installer\Windows\FlipKit.iss`, producing **`releases\FlipKit-Setup-v<version>.exe`**.

If the Hub payload is already staged (e.g. `build-release.ps1` or `build-hub-for-installer.ps1` was just run and the `releases\temp\FlipKit-Hub-Windows-x64-v<version>\` folder still exists), you can recompile the installer alone:

```powershell
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" /DVersion=<version> Installer\Windows\FlipKit.iss
```

> Note: `FlipKit.iss` packages **from `releases\temp\FlipKit-Hub-Windows-x64-v<version>\`** (Desktop exe + `servers\*` + Docs/README/LICENSE). That folder must exist before ISCC runs. `build-release.ps1` deletes its temp hub folder after zipping, so prefer `build-hub-for-installer.ps1`, which leaves it in place for ISCC.

---

## Known gotchas

### Windows Defender locks `FlipKit.Api.exe`

On some machines Defender's real-time scan holds/quarantines the freshly-built `FlipKit.Api.exe` (the .NET apphost), which surfaces as:

- `MSB3021: Unable to copy ... 'FlipKit.Api.exe'. Access to the path ... is denied.` during `dotnet build`, and
- `Access is denied` when launching the API server or extracting a Hub zip over an existing copy.

Workarounds:

- **Build / test** without producing the apphost wrapper: append `-p:UseAppHost=false`, e.g.
  `dotnet build FlipKit.sln -p:UseAppHost=false` and `dotnet test FlipKit.sln -p:UseAppHost=false`.
  (`build-release.ps1`'s test gate does **not** pass this, so it can fail on a locked machine — run tests manually with the flag, then build the package.)
- **Release publish**: a *fresh* self-contained publish (to a clean output dir) generally succeeds even when an incremental `bin\Debug` copy is blocked.
- **Durable fix**: add a Windows Defender **exclusion** for the repo folder and `%LocalAppData%\FlipKit*` (Settings → Virus & threat protection → Manage settings → Exclusions). Requires admin.

### `build-hub-for-installer.ps1` hardcodes the version

The script sets `$Version = "<n>"` near the top instead of taking a parameter. Update it to the release version before running (or pass `/DVersion=` to ISCC manually as shown above). Making it accept a `-Version` parameter is a good future cleanup.

---

## Release publish checklist

The full path from merged code to a published GitHub release:

1. **Bump the changelog** — promote `## [Unreleased]` to `## [<version>] - <yyyy-mm>` in [`CHANGELOG.md`](../CHANGELOG.md).
2. **Merge** the feature branch to `master` (squash, matching the `... (#PR)` convention).
3. **Verify on master**: `dotnet test FlipKit.sln -p:UseAppHost=false` (see Defender gotcha).
4. **Build artifacts**:
   - Hub zips (Windows + Linux): `.\build-release.ps1 -Version <version>` → `releases\FlipKit-Hub-*-v<version>.zip`
   - Windows installer: `.\build-hub-for-installer.ps1` → `releases\FlipKit-Setup-v<version>.exe`
5. **Tag + release**:
   ```powershell
   gh release create v<version> --target master --title "FlipKit Hub v<version>" --notes-file <notes.md> `
     "releases\FlipKit-Hub-Windows-x64-v<version>.zip" `
     "releases\FlipKit-Hub-Linux-x64-v<version>.zip" `
     "releases\FlipKit-Setup-v<version>.exe"
   ```
   (macOS `.dmg` assets are built separately on a Mac and uploaded with `gh release upload v<version> <file>`.)
