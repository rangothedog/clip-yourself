# Clip Yourself

A Windows clipboard manager that lives in a sidebar (WPF). Every copy — from any app, browsers included — becomes a "clip" row with a live preview (text, images, audio with a tiny waveform player). Clips are grouped into **drawers**: each session starts a fresh drawer, and you can create, rename, clear, and delete drawers for projects or categories. Click any clip to put it back on your clipboard. Hosted home: clipyourself.com.

## Repository layout

| Path | What it is |
|---|---|
| `ClipYourself.slnx` | Visual Studio solution (open in VS 2022 17.13+) |
| `src/ClipYourself.Core` | .NET 8 class library — models, dedup, drawer limits, JSON + blob persistence |
| `src/ClipYourself.Desktop` | WPF sidebar app (net8.0-windows, NAudio for waveforms) |
| `website/` | clipyourself.com landing site — React + TypeScript + Vite, static output |
| `installer/` | WiX v5 MSI installer (self-contained x64, no .NET needed on target) |
| `docs/` | Research notes (DAW clipboard interop) |
| `extension/` | Parked Chrome side-panel prototype — kept for reference, not part of the solution or product |

## Desktop app (Windows)

Run from Visual Studio (set **ClipYourself.Desktop** as startup project) or:

```powershell
dotnet run --project src/ClipYourself.Desktop
```

- Docks to the right edge as a dark sidebar; drag the header to move it, 📌 toggles always-on-top.
- **Ctrl+Alt+V** shows/hides the sidebar from anywhere (global hotkey). Closing/hiding keeps it running; quit from Settings.
- Captures clipboard changes automatically: text, images (screenshots or copied image files), copied audio files (with waveform + play/pause + click-to-seek), and generic file lists.
- Duplicate copies aren't shown twice — the existing clip just jumps back to the top.
- ➕ Drawer starts a new drawer and opens its "movie reel" view; while a drawer is open, new copies land in it. Rename by clicking any drawer/session name. Per-drawer max clips / max MB are enforced by evicting the oldest clips.
- 📌 Pin a clip to keep it at the top of its drawer, exempt from auto-eviction.
- 🔍 Search matches text, previews, and file names across every drawer at once; results show which drawer each clip lives in.
- Drag & drop: drop files or text from anywhere onto the sidebar to clip them without copying; drag a clip card onto a drawer row (or the session header) to file it there.
- Settings → "Save clips between sessions" opts into persistence (JSON + content-addressed blobs under `%LOCALAPPDATA%\ClipYourself`). Turning it off deletes what was saved.

## Windows installer (MSI)

```powershell
.\scripts\build-installer.ps1
```

Publishes the desktop app self-contained (win-x64) and builds `installer/bin/Release/ClipYourself-0.1.0-win-x64.msi` via WiX v5 (restored from NuGet — nothing to install). The MSI installs to Program Files with a Start Menu shortcut, supports major upgrades, and is copied into `website/public/downloads/` for the site's download link. The installer project is in the solution but excluded from normal builds; in Visual Studio it loads with the [HeatWave](https://www.firegiant.com/products/heatwave/) extension.

## Website (clipyourself.com)

```powershell
cd website
npm install
npm run dev     # local preview
npm run build   # static site in website/dist — host anywhere
```

## Status / known limitations

This is a working prototype:

- Waveform decoding covers common formats (mp3/wav/etc. via NAudio); unsupported codecs fall back gracefully.
- The global hotkey is fixed at Ctrl+Alt+V for now.
- A Chrome side-panel extension was prototyped and parked: browsers can't observe the OS clipboard, so it could only capture page-scoped copy events — the desktop app already captures browser copies via the Windows clipboard, making the extension redundant. The code stays in `extension/` for reference.
