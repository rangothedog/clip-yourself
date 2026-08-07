# Clip Yourself — marketing website

Single-page landing site for [clipyourself.com](https://clipyourself.com).
React + TypeScript + Vite, dark-only design matching the desktop app palette.
No runtime network dependencies (system font stack, local SVG placeholders).

## Develop

```powershell
npm install
npm run dev      # Vite dev server, http://localhost:5173
```

Or open `ClipYourself.Website.esproj` in Visual Studio (JavaScript Project
System) — F5 runs `npm run dev`.

## Build

```powershell
npm run build    # tsc type-check + vite build → dist/
npm run preview  # serve the production build locally
```

## Deploy

The output in `dist/` is fully static — host it anywhere (GitHub Pages,
Cloudflare Pages, Netlify, S3 + CloudFront, nginx, IIS). No server-side code,
no environment variables.

Notes:

- `public/downloads/` is where CI drops the Windows installer
  (`ClipYourself-<version>-win-x64.msi`). The hero and Download-section
  buttons link to `/downloads/ClipYourself-1.0.4-win-x64.msi`; update the
  version in `src/components/Hero.tsx` and `src/components/Download.tsx`
  when the installer version bumps.
- The release script also emits a portable zip
  (`ClipYourself-<version>-win-x64-portable.zip`) and `checksums.txt`
  (SHA-256 values for MSI + ZIP). Keep these files in `public/downloads/`.
- `public/screenshots/*.svg` are labeled placeholders — swap in real PNG
  screenshots (keep the same file names or update `ReelShowcase.tsx`).
- Everything under `public/` is copied verbatim into `dist/`.

## Structure

```
src/
  App.tsx                  section layout
  styles.css               design tokens + all styling
  components/
    Nav / Hero / SidebarMockup (CSS recreation of the app UI)
    Features / ReelShowcase / AudioSection / DawTeaser
    Download / Faq / Footer
    Reveal.tsx             IntersectionObserver scroll-in wrapper
```
