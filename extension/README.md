# Clip Yourself — Chrome Extension

The Chrome side-panel clipboard manager for [clipyourself.com](https://clipyourself.com).
It collects "clips" (text, images, audio URLs) as you browse, groups them into
"drawers" (sessions/categories), and lets you copy any clip back with one click.

## Build

```
npm install
npm run build
```

`npm run build` type-checks (`tsc --noEmit`), builds the side panel + background
service worker with Vite, then bundles the content script as a single IIFE.
The finished, directly loadable extension is written to `dist/`.

Other scripts: `npm run dev` (Vite dev server for panel UI work) and
`npm run typecheck`.

## Load unpacked

1. Open `chrome://extensions`
2. Enable **Developer mode** (top right)
3. Click **Load unpacked**
4. Select the `extension/dist` folder

Click the toolbar button to open the Clip Yourself side panel.

## How capture works

- **Text** — a content script on every page listens for `copy` / `cut` events and
  captures the current selection (including selections inside inputs/textareas).
- **Images** — right-click any image → "Clip image to Clip Yourself". The image is
  fetched and stored inline as a data URL (capped at 2 MB after encoding; larger
  images are stored by URL instead).
- **Audio** — right-click an audio element or a link ending in
  `.mp3 .wav .m4a .aac .ogg .flac` → "Clip audio to Clip Yourself". The URL is
  stored and rendered as a compact waveform player.

Clips land in the drawer currently open in the side panel; if none is open they
go to the current session drawer. Duplicates (same content hash) are moved to
the top instead of being re-added. Each drawer enforces max-clip and max-size
limits by evicting its oldest clips.

By default drawers live in session storage and reset when the browser restarts
(a fresh "Session — …" drawer is created each session). Turn on **Save clips
between sessions** in settings to persist drawers across restarts.

## Limitations

- Browsers cannot observe the OS clipboard globally. Capture only happens via
  `copy`/`cut` events on regular web pages plus the right-click context menus —
  copies made in other apps, on `chrome://` pages, or in the Chrome Web Store
  are not seen.
- Image copy-back is converted to PNG (the only image format Chrome's async
  clipboard accepts). Audio clips copy their URL as text.
- Cross-origin audio may refuse to decode for the waveform (CORS); the player
  falls back to a plain `<audio>` element.
