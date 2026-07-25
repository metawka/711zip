# 711zip

A modern **Windows 11–style** front end for the [7-Zip](https://www.7-zip.org/) archiver.

711zip keeps the battle-tested 7-Zip engine (`7z.dll`) and wraps it in a fresh
**WinUI 3 / Fluent** interface with Mica material, rounded corners, Segoe UI
Variable typography, and full **light / dark theme** support — built to be
compact and fast.

> This is a fork/companion project. The archiving engine is unmodified 7-Zip;
> the user interface is a ground-up rewrite.

## Status

Early development. Working so far:

- [x] WinUI 3 shell with Mica backdrop and custom title bar
- [x] Light / dark theme toggle
- [x] Bundled 7-Zip engine (`7z.dll`, all formats incl. RAR read)
- [ ] File browser (dual pane)
- [ ] Open / extract archives
- [ ] Create / update archives (all codecs)
- [ ] Explorer context-menu integration
- [ ] Installer

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
