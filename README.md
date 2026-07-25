# 711-zip

A modern **Windows 11–style** front end for the [7-Zip](https://www.7-zip.org/) archiver.

711zip keeps the battle-tested 7-Zip engine (`7z.dll`) and wraps it in a fresh
**WinUI 3 / Fluent** interface with Mica material, rounded corners, Segoe UI
Variable typography, and full **light / dark theme** support — built to be
compact and fast.

> This is a fork/companion project. The archiving engine is unmodified 7-Zip;
> the user interface is a ground-up rewrite.

## Status

Stable — **v1.0.0**. Included:

- [x] WinUI 3 shell with Mica backdrop and custom title bar
- [x] Light / dark theme toggle (with readable caption buttons in both themes)
- [x] Bundled 7-Zip engine (`7z.dll`/`7z.exe`, all formats incl. RAR read)
- [x] Single-pane file browser: drives, folders, path box with live folder
      autocomplete, typed-path navigation
- [x] Back / Forward navigation (toolbar buttons, mouse side buttons, Alt+←/→),
      restores scroll position on Back
- [x] Reopens the last folder on launch (like the 7-Zip file manager)
- [x] First-run guide with the main features and keyboard shortcuts
- [x] Open / browse archives (incl. nested archives) and extract
- [x] Create / update archives (7z/zip, selectable level, password, destination folder)
- [x] Full right-click context menus with shortcut hints (background + item):
      open, extract, view, compress, cut/copy/paste, rename, delete, properties
- [x] Favorites — a pinned virtual folder in "This PC", shows the full path
- [x] Search within the current folder (Ctrl+F)
- [x] Keyboard shortcuts (Ctrl+F, Ctrl+R/F5, Ctrl+L, F2, Del, Ctrl+C/X/V, Space, Alt+←/→)
- [x] Drag & drop, in and out (including extracting straight out of an archive)
- [x] File-type icons (image / video / audio / code / docs / …)
- [x] In-app preview for images, video, audio and text/code (Space / Esc)
- [x] Resizable, aligned columns (Name / Size / Modified)
- [x] Settings (theme, default format/level, hidden files, delete confirmation, about, guide)
- [x] Explorer integration: one grouped "711-zip" right-click submenu & "Open with" registration
- [x] Inno Setup installer (self-contained, clean install root)

## Install

Download the latest `711zip-<version>-setup.exe` from
[Releases](https://github.com/metawka/711zip/releases) and run it. Program files
install under `C:\Program Files\711zip\app\`, keeping the install root clean
(just the launcher shortcut and the uninstaller). The installer registers
711-zip in the Windows **Open with** dialog and adds a single grouped Explorer
right-click submenu (*Open in 711-zip*, *Extract here*, *Extract to folder*,
*Add to archive*).

## Build

Requirements:

- .NET SDK 10+
- Windows 10 1809+ / Windows 11 (x64)

```bash
dotnet build src/App/711zip.csproj -c Release
```

The 7-Zip engine ships in `vendor/engine-x64/`. To re-fetch it from the
official upstream release, run `tools/fetch-engine.ps1`.

## Engine / upstream

Built on **7-Zip 26.02** by Igor Pavlov (<https://github.com/ip7z/7zip>).

## License

- The 711zip user-interface code in this repository is licensed under the
  **GNU LGPL v2.1** — see [`LICENSE`](LICENSE).
- The bundled 7-Zip engine is licensed under LGPL with the unRAR restriction
  and some BSD-licensed portions — see [`LICENSE-7zip.txt`](LICENSE-7zip.txt).
  The RAR code may **not** be used to develop a RAR-compatible archiver.

711zip is not affiliated with or endorsed by the 7-Zip project.
