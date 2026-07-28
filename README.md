# Clip Yourself

A clipboard manager that lives in a sidebar — for Windows (WPF) and Chrome (side panel extension). Every copy becomes a "clip" row with a live preview (text, images, audio with a tiny waveform player). Clips are grouped into **drawers**: each session starts a fresh drawer, and you can create, rename, clear, and delete drawers for projects or categories. Click any clip to put it back on your clipboard. Hosted home: clipyourself.com.

## Repository layout

| Path | What it is |
|---|---|
| `ClipYourself.slnx` | Visual Studio solution (open in VS 2022 17.13+) |
| `src/ClipYourself.Core` | .NET 8 class library — models, dedup, drawer limits, JSON + blob persistence |
| `src/ClipYourself.Desktop` | WPF sidebar app (net8.0-windows, NAudio for waveforms) |
| `extension/` | Chrome MV3 extension — React + TypeScript + Vite, side panel UI |

The extension is included in the solution as `ClipYourself.Extension.esproj`; it loads in Visual Studio when the **JavaScript and TypeScript** workload is installed, and is skipped by `dotnet build` on the command line.

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
- Settings → "Save clips between sessions" opts into persistence (JSON + content-addressed blobs under `%LOCALAPPDATA%\ClipYourself`). Turning it off deletes what was saved.

## Chrome extension

```powershell
cd extension
npm install
npm run build
```

Then in Chrome: `chrome://extensions` → enable **Developer mode** → **Load unpacked** → select `extension/dist`. Click the toolbar icon to open the side panel.

- Captures text whenever you copy on a web page (content script); images and audio via right-click → "Clip image/audio to Clip Yourself". Browsers don't allow watching the OS clipboard globally, so capture is page-scoped by design.
- Same drawer model as the desktop app: session drawers, reel view with film-strip styling, dedup-to-top, per-drawer limits, opt-in persistence (`chrome.storage.local` vs session storage).

See [extension/README.md](extension/README.md) for details.

## Status / known limitations

This is a working prototype:

- Desktop and extension keep separate clip stores (no sync yet — clipyourself.com sync is future work).
- Desktop audio clips come from copying audio *files*; raw audio data on the clipboard is rare and not parsed.
- Waveform decoding covers common formats (mp3/wav/etc. via NAudio); unsupported codecs fall back gracefully.
- The global hotkey is fixed at Ctrl+Alt+V for now.
