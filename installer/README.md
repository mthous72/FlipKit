# FlipKit Installers

Per-platform installer assets, driven by `build-installers.ps1` at the repo root.

| Platform | Source |
|---|---|
| Windows | [Windows/FlipKit.iss](Windows/FlipKit.iss) — Inno Setup script |
| Linux | [Linux/build-packages.sh](Linux/build-packages.sh) + [postinst.sh](Linux/postinst.sh) — .deb / .rpm packaging |
| macOS | [Mac/create-dmg.sh](Mac/create-dmg.sh) + [README-INSTALL.txt](Mac/README-INSTALL.txt) — .dmg packaging |

Build all platforms via `build-installers.ps1` from the repo root. Output lands in `releases/`.
