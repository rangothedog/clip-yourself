# Bridging DAW Audio Clipboards — Research Report for "Clip Yourself"

*Researched 2026-07-27. Claims cite sources; items marked "unverified" need empirical testing.*

**TL;DR:** Almost every DAW's Ctrl+C is an internal, in-process object copy that never touches the Windows clipboard — Audacity, REAPER, Ableton Live, Cubase, Pro Tools, GoldWave all confirmed or strongly indicated internal-only. The exceptions are the *wave editors*: Adobe Audition (Waveform view, opt-in "Windows clipboard"), WavePad (explicit "System Clipboard" commands), Sound Forge (evidence of OS clipboard use), and FL Studio's Edison (custom registered clipboard format pointing at a temp WAV). A universal "clipboard bridge" via CF_WAVE is impossible; a practical bridge is a **multi-route capture funnel**: OS clipboard listener + drop target + per-DAW scripting integrations + watched export folders, normalizing everything to a WAV the user can then drag/paste into any DAW (drag-*in* of files is universal). No existing product does this — the prior art is session converters and sync tools, not clipboard bridges.

---

## 1. Audacity

**Clipboard is a pure internal singleton — zero OS clipboard involvement on copy.**

- The source confirms it: [`au3/src/Clipboard.h`](https://github.com/audacity/audacity/blob/master/au3/src/Clipboard.h) is a singleton (`Clipboard::Get()`) holding a `TrackList` plus time range (`mT0`/`mT1`) and a project reference. There is no `wxClipboard`/OS clipboard interaction in it — copied audio lives as in-memory track objects tied to the project's SQLite storage. That's why a CF_WAVE listener never fires.
- Consequence of the project-tied design: since 3.0, closing the source project wipes the paste buffer ([GitHub discussion #4698](https://github.com/audacity/audacity/discussions/4698)); the [manual](https://manual.audacityteam.org/man/edit_menu_copy_paste_and_duplicate.html) describes copy as going to "the Audacity clipboard," one item at a time.
- **One-way OS clipboard support exists — import only.** [PR #6280](https://github.com/audacity/audacity/pull/6280) (Audacity 3.6, merged May 2024) added Ctrl+V import of *file lists* from the system clipboard (i.e., files copied in Explorer); [issue #6376](https://github.com/audacity/audacity/issues/6376) extended it to handle clipboard promises (for Muse Hub). The PR explicitly does **not** add copying *to* the system clipboard. This matters for the paste-into-Audacity direction: put CF_HDROP on the clipboard and Audacity 3.6+ will import it on Ctrl+V.
- **Feature requests for OS-clipboard export exist and are unimplemented**: ["Copy As" Selection](https://forum.audacityteam.org/t/copy-as-selection/50498) (export selection to temp file, put file on clipboard), [Copy selected audio to clipboard (as a file)](https://forum.audacityteam.org/t/copy-selected-audio-to-clipboard-as-a-file/61514), [Advanced clipboard](https://forum.audacityteam.org/t/advanced-clipboard-for-complex-journalistic-projects/64026). This is literally our feature being asked for.
- **Drag-out:** no evidence of dragging audio out of Audacity to Explorer/other apps (drag-and-drop is import-only). *Unverified as an explicit "no" — but no doc, forum thread, or code reference to drag-out was found.*
- **Scripting escape hatch — viable.** [mod-script-pipe](https://manual.audacityteam.org/man/scripting.html) (ships with Audacity, user enables it in Preferences → Modules) exposes named pipes (`\\.\pipe\ToSrvPipe` on Windows) accepting commands including `Export2:`/`Export:` with `Mode=Selection` — i.e., "export the current selection to a WAV at this path" is programmable from our app ([pipeclient.py](https://gist.github.com/SteveDaulton/df9cd15c6a85f478b925d8ce7beab14a), [forum export examples](https://forum.audacityteam.org/t/python-mod-script-pip-render-mp3/55317)). Known flakiness reports exist ([Export2 only works once](https://forum.audacityteam.org/t/python-mod-script-pipe-export2-only-works-once/101491)), so budget QA time. Nyquist (`GetInfo`/plug-ins) can also write files but mod-script-pipe is the cleaner integration.

**Verdict:** Audacity can be bridged today via a "Copy to Clip Yourself" hotkey that drives mod-script-pipe (`Export2` of the selection to a temp WAV, then we place CF_HDROP/CF_WAVE on the clipboard ourselves). No passive capture possible.

## 2. Pro Tools

- **Internal clipboard, single-session model.** Copy/paste of clips is internal; only one session opens at a time, so even PT↔PT transfer goes through Import Session Data ([DUC thread](https://duc.avid.com/showthread.php?t=423492), [Gearspace](https://gearspace.com/board/post-production-forum/187478-copy-paste-between-protools-sessions.html)). Nothing lands on the OS clipboard. Pro Tools "clips" are references into session media plus edit metadata — meaningless outside the `.ptx` session.
- **Export path:** right-click → **Export Clips as Files** (Ctrl/Cmd+Shift+K) renders selected clips from the Clip List to WAV/AIFF anywhere on disk ([Production Expert on PT export options](https://www.production-expert.com/production-expert-1/pro-tools-export-options-aaf-omf-midi-stems-and-clips), [PCAudioLabs Clip List guide](https://pcaudiolabs.com/clips-list-in-pro-tools/)). AAF/OMF export is for whole-timeline interchange with NLEs/other DAWs, not per-clip clipboard use.
- **Drag-out to desktop:** no documentation or forum evidence that clips can be dragged from the Edit window/Clip List to Finder/Explorer; drag-and-drop is import-only (Workspace → timeline). *Flagged unverified as an explicit "no" — no counter-evidence found.*
- **Scripting SDK (2023+):** Pro Tools now has an official language-independent gRPC [Scripting SDK](https://www.avid.com/resource-center/pro-tools-scripting-sdk) with export commands and timeline get/set ([Production Expert](https://www.production-expert.com/production-expert-1/avid-scripting-sdk-deeper-control-of-pro-tools-is-coming), [SDK FAQ](https://kb.avid.com/pkb/articles/en_US/Knowledge/Pro-Tools-Scripting-SDK-FAQ)). A "copy selection out of Pro Tools" companion integration is technically feasible on modern PT versions; whether the exact "export selected clip" RPC granularity fits the UX needs hands-on validation. *Partially verified — SDK exists and exports; per-clip ergonomics unverified.*

**Verdict:** No passive capture. Realistic v1: watch the user's "Export Clips as Files" target folder; v2: Scripting SDK integration.

## 3. Other Windows DAWs/editors

| App | Copy → OS clipboard? | Drag media out to Explorer/apps? |
|---|---|---|
| **REAPER** | No — internal item clipboard (copies item *references*; cross-project paste needs glue or project tabs) ([Cockos forum](https://forum.cockos.com/archive/index.php/t-163917.html)) | Not natively documented; SWS can copy the *source file path* to clipboard ([SWS/BR thread](https://forums.cockos.com/showthread.php?t=192936)) |
| **Ableton Live** | No — internal only; clipboard managers famously don't see Live's clipboard ([Ableton forum](https://forum.ableton.com/viewtopic.php?t=16994)) | Yes on Mac; on Windows historically broken ("file in use", .alc for MIDI — [forum](https://forum.ableton.com/viewtopic.php?t=220219)) but reported working for wav/mp3/aif in Live 11.3.20+/12 ([forum](https://forum.ableton.com/viewtopic.php?t=249239)). Note: it drags the *referenced sample file*, not a render of the clip |
| **FL Studio** | Main app: no. **Edison**: yes — a custom registered format "Audio clipboard file" (version int + path to a temp WAV), designed by Image-Line's developer explicitly because CF_WAVE is RAM-bound with erratic 20–50 MB size limits ([Image-Line dev thread](https://forum.image-line.com/viewtopic.php?t=21830)). *Current-version persistence of this format unverified* | Edison has a dedicated "Drag / copy sample / selection" button that drags selections out as files ([Edison manual](https://www.image-line.com/fl-studio-learning/fl-studio-online-manual/html/plugins/Edison_4.htm); works into VSTs per [JUCE forum](https://forum.juce.com/t/drag-and-drop-to-vst-from-edison-in-fl-studio/49171); Win10 Explorer bugs reported [here](https://forum.image-line.com/viewtopic.php?t=153738)). Playlist audio: no drag-out; use File → Export |
| **Cubase/Nuendo** | No — internal, per-project; no easy cross-project transfer ([Steinberg forum](https://forums.steinberg.net/t/drag-and-drop-tracks-between-projects/110258)); File → Export → Selected Events is the out-path | No drag-out found. *Unverified "no"* |
| **Studio One** | Internal (no OS-clipboard audio found; *unverified*) | **Yes — flagship feature.** Audio parts/events drag out to Explorer/Finder as files; instrument parts as Musicloop/MIDI ([SOS](https://www.soundonsound.com/techniques/studio-one-browser-sound-sets-pool), [SOS Audioloops](https://www.soundonsound.com/techniques/studio-one-audioloops-musicloops), [PreSonus KB](https://support.presonus.com/hc/en-us/articles/210043833-Studio-One-Drag-and-Drop)). Caveat: exact format of dragged audio (wav vs Audioloop/REX2) depends on what you drag |
| **Adobe Audition** | **Yes, opt-in.** Waveform view has 5 internal clipboards **plus the Windows system clipboard**, selected via Edit → Set Current Clipboard ("Choose the Windows clipboard if you want to copy audio data to other Windows applications" — [Adobe helpx](https://helpx.adobe.com/audition/using/copying-cutting-pasting-deleting-audio.html); same feature back to [Audition 1.5](https://www.manualslib.com/manual/1995/Adobe-Audition-1-5.html?page=100) and [CS6](https://www.peachpit.com/articles/article.aspx?p=1867758&seqNum=4)). Works only in Waveform view, not Multitrack ([Adobe community](https://community.adobe.com/questions-544/set-current-clipboard-greyed-out-162076)). Exact clipboard format when "Windows" is selected is **unverified** — almost certainly CF_WAVE, test empirically | No drag-out found (*unverified*) |
| **Sound Forge** | Strong evidence of OS clipboard use: a third-party clipboard manager (Comfort Clipboard Pro) corrupted Sound Forge copy/paste until disabled ([Magix forum](https://www.magix.info/us/forum/sound-forge-audio-studio-15-copy-and-paste-bug--1274584/)). Historically pastes wave data across apps. **CF_WAVE specifically: unverified** — test empirically | Not documented (*unverified*) |
| **GoldWave** | **No — explicitly internal since v5.** GoldWave's own forum states v5's internal clipboard/virtual editing means "you cannot copy and paste audio between programs — save to a file (File \| Copy To) then open it in the other program" ([GoldWave forum](http://goldwave.ca/forums/viewtopic.php?t=1780) — *thread now 404s; claim recovered from search index, treat as partially verified*; [Edit menu manual](https://goldwave.com/help/desktop/EditMenu.html)) | File \| Copy To saves selection directly to a file (good folder-watch target) |
| **ocenaudio** | Internal copy/paste only per docs; no system-clipboard interop found ([features page](https://www.ocenaudio.com/en/features)). ***Unverified either way*** | Not documented (*unverified*) |
| **WavePad (NCH)** | **Yes, explicit.** Separate commands: "Copy to System Clipboard (Ctrl+Shift+C) and Paste from System Clipboard (Ctrl+Shift+V). The System Clipboard can be used to copy and paste audio to and from other applications" ([NCH manual](https://help.nchsoftware.com/help/en/wavepad/win/cutcopypaste.html)). Format undocumented — likely CF_WAVE, verify empirically | Not documented |

**Audition verification: confirmed.** Audition did and still does offer Windows-clipboard audio copy — but it's opt-in (Set Current Clipboard → Windows), Waveform-view-only, and off by default (default is internal clipboard 1). The format is undocumented in Adobe's help; CF_WAVE is the reasonable assumption given lineage from Cool Edit — **flagged unverified**, 30 minutes with the existing CF_WAVE listener will settle it.

## 4. What interchange actually works app-to-app today

- **Files are the lingua franca, not clipboard bits.** Every app above imports WAV via drag-in from Explorer; most import via Ctrl+V of copied files (Audacity 3.6+ [confirmed](https://github.com/audacity/audacity/pull/6280); others vary). So a bridge that materializes captured audio as a temp WAV + CF_HDROP reaches *every* DAW on the paste side.
- **CF_WAVE is a legacy niche.** Image-Line's developer documented its problems first-hand: whole payload must be in RAM, clipboard rejects blocks somewhere in the 20–50 MB range, few editors support it ([Image-Line thread](https://forum.image-line.com/viewtopic.php?t=21830)); Windows' own Sound Recorder stopped putting wave data on the clipboard after XP ([Microsoft Q&A](https://learn.microsoft.com/en-us/answers/questions/fac13db6-9864-444d-9843-1cd59956c099/copying-waveform-data-to-clipboard)). Working CF_WAVE pairs today are roughly {Audition (opt-in), WavePad, Sound Forge (probable)} ↔ each other and ↔ Clip Yourself.
- **Drag-out actually works** from Studio One, Ableton Live (Mac; Windows in recent versions), and FL Edison. **No DAW was found to use CFSTR_FILEDESCRIPTOR/FILECONTENTS virtual files** — they write real temp files and drag CF_HDROP (Edison's design explicitly writes a temp WAV). Still, virtual-file support in the drop target is cheap insurance; it also catches Outlook attachments etc. *(Absence of virtual-file DAWs: negative claim, not exhaustively verifiable.)*
- **AAF/OMF/ARA are irrelevant to a clipboard bridge.** AAF/OMF move whole timelines between PT/NLEs ([Production Expert](https://www.production-expert.com/production-expert-1/pro-tools-export-options-aaf-omf-midi-stems-and-clips)); ARA is an in-process plugin API (Melodyne-style), not an interchange file. Don't build on them for v1; at most, "import audio essence from an AAF the user gives us" is a future differentiator.

## 5. Prior art

**No product bridges DAW internal clipboards.** Closest neighbors:

- **Splice Bridge** — plugin + desktop app; lets you paste/drag Splice sounds *into* DAWs, tempo/key-matched ([splice.com/tools/bridge](https://splice.com/tools/bridge)). One-directional content delivery, not a DAW-to-DAW clipboard.
- **Audio Design Desk "DAW Bridge"** — syncs DAWs to Final Cut Pro via MIDI Timecode ([add.app/daw-bridge](https://add.app/daw-bridge/)). Transport sync, not clipboard.
- **AATranslator** ([aatranslator.com.au](https://www.aatranslator.com.au/)) and **Vordio** — offline *session file* converters between ~40 DAW/NLE formats. Proves demand for cross-DAW interchange; file-based, slow-loop, not clipboard-shaped.
- Generic clipboard managers actively *break* audio apps rather than help (Sound Forge thread above) — a positioning angle: "the clipboard manager that understands audio."

The gap is real, and the reason is structural: internal DAW clipboards are in-process object graphs (Audacity's `TrackList`, PT clip references) with no serialized externalizable form. That's why the winning shape is capture-at-the-edges, not clipboard interception.

## 6. Feasibility verdict & v1 feature matrix

**Route (a) OS clipboard (CF_WAVE + custom formats).** Cheap — already built. Covers Audition (opt-in), WavePad, probably Sound Forge. Add a listener for Image-Line's registered **"Audio clipboard file"** format (int version + WAV path) to catch FL Edison copies — verify it still exists in FL 21/2024 at runtime. Respect CF_WAVE's ~20–50 MB practical ceiling when *writing* to the clipboard; always pair it with CF_HDROP to a temp WAV.

**Route (b) Drop target (CF_HDROP + CFSTR_FILEDESCRIPTOR/FILECONTENTS).** Covers Studio One (best-in-class), Ableton Live on Windows (recent versions; note it drags the source sample file, so unrendered clip edits/warping are NOT baked in — set expectations in UX), FL Edison, plus generic Explorer drags. Medium effort; virtual-file support is insurance, not a known DAW requirement.

**Route (c) Scripting companions ("Copy to Clip Yourself" hotkey).** Highest-value paid feature. Audacity via mod-script-pipe `Export2 Mode=Selection` (user enables module once); REAPER via a bundled ReaScript/action that glues-renders selected items to a temp file and pokes our app ([ReaScript](https://www.reaper.fm/sdk/reascript/reascript.php) + [SWS](https://sws-extension.org/) make this trivially scriptable, and REAPER users expect installing scripts); Pro Tools via the gRPC Scripting SDK (2023+; validate clip-export granularity). Ableton could follow via Max for Live (unresearched depth — flag).

**Route (d) Watched folders.** Universal safety net, trivial to build: watch Desktop + user-designated export dirs; auto-ingest new WAV/AIFF (with debounce for files still being written). This is the *only* route for Cubase (Export Selected Events), GoldWave (File | Copy To), FL playlist, and Pro Tools v1 (Export Clips as Files → watched folder).

**Paste-into-DAW direction (the "between them" half):** always publish captured audio as temp-WAV + CF_HDROP + CF_WAVE, and make every Clip Yourself tile a drag *source* of that file. Drag-in of WAV files works in every app surveyed; Ctrl+V file-paste works at least in Audacity 3.6+, Audition, WavePad, Sound Forge.

### v1 "Works with your DAW" matrix

| App | (a) OS clipboard | (b) Drag-out → Clip Yourself | (c) Scripting hotkey | (d) Watched folder | Realistic v1 story |
|---|---|---|---|---|---|
| Audacity | — (import-only) | — | ✅ mod-script-pipe | ✅ | Hotkey via script-pipe ★ |
| Pro Tools | — | — (unverified no) | ◐ SDK (PT 2023+) | ✅ Export Clips as Files | Watched folder; SDK later |
| REAPER | — | — | ✅ ReaScript/SWS | ✅ render path | Bundled ReaScript ★ |
| Ableton Live | — | ✅ Win 11.3.20+ (source file only) | ◐ Max for Live (unresearched) | ✅ | Drag-out (with caveat) |
| FL Studio | ✅ Edison custom format (verify) | ✅ Edison drag button | — | ✅ render dir | Edison both ways ★ |
| Cubase/Nuendo | — | — (unverified) | — (no export API found) | ✅ Export Selected Events | Watched folder |
| Studio One | — (unverified) | ✅ native | — | ✅ | Drag-out ★ |
| Audition | ✅ opt-in, Waveform view | — (unverified) | — | ✅ | OS clipboard ★ |
| Sound Forge | ✅ probable (verify format) | — (unverified) | — | ✅ | OS clipboard |
| GoldWave | ✗ (internal by design) | — | — | ✅ File \| Copy To | Watched folder |
| ocenaudio | ? unverified | ? | — | ✅ | Watched folder |
| WavePad | ✅ documented | — | — | ✅ | OS clipboard ★ |

★ = strong demo-able story for launch. **Bottom line:** the paid feature is viable, but sell it as "Clip Yourself understands how *your* DAW gets audio out" (four capture routes, per-app onboarding), not as universal Ctrl+C interception — the latter is technically impossible for Audacity, REAPER, Live, Cubase, Pro Tools, and GoldWave. Highest-ROI build order: (d) watched folders → (b) drop target → (a) extra clipboard formats (Edison format + verify Audition/Sound Forge/WavePad empirically) → (c) Audacity + REAPER companions.
