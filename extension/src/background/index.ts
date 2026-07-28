import { sha256 } from '../shared/hash';
import type { Command, CommandResponse } from '../shared/messages';
import { getState, removeStateFrom, saveState } from '../shared/store';
import type { ClipKind, Drawer, Settings, State } from '../shared/types';

const MENU_IMAGE = 'clip-image';
const MENU_AUDIO = 'clip-audio';
const AUDIO_EXTENSIONS = ['.mp3', '.wav', '.m4a', '.aac', '.ogg', '.flac'];
/** Max size of an encoded image data URL kept inline (2 MB). */
const MAX_IMAGE_DATA_URL_BYTES = 2 * 1024 * 1024;

void chrome.sidePanel.setPanelBehavior({ openPanelOnActionClick: true }).catch(() => {});

// ---------------------------------------------------------------------------
// Serialized state mutation queue (all mutations happen in this worker).
// ---------------------------------------------------------------------------
let queue: Promise<unknown> = Promise.resolve();

function mutate<T>(fn: (state: State) => T | Promise<T>): Promise<T> {
  const run = queue.then(async () => {
    const state = await getState();
    const result = await fn(state);
    await saveState(state);
    return result;
  });
  queue = run.catch(() => {});
  return run;
}

// ---------------------------------------------------------------------------
// Drawer helpers
// ---------------------------------------------------------------------------
function makeDrawer(name: string, isSession: boolean, settings: Settings): Drawer {
  return {
    id: crypto.randomUUID(),
    name,
    createdAt: Date.now(),
    isSession,
    maxClips: settings.defaultMaxClips,
    maxSizeMB: settings.defaultMaxSizeMB,
    clips: [],
  };
}

function sessionName(): string {
  return `Session — ${new Date().toLocaleString(undefined, { dateStyle: 'short', timeStyle: 'short' })}`;
}

/** Evict oldest clips (from the end) while over the drawer's count/size limits. */
function enforceLimits(drawer: Drawer): void {
  const maxBytes = drawer.maxSizeMB * 1024 * 1024;
  const totalBytes = () => drawer.clips.reduce((sum, c) => sum + c.sizeBytes, 0);
  while (
    drawer.clips.length > drawer.maxClips ||
    (drawer.clips.length > 0 && totalBytes() > maxBytes)
  ) {
    drawer.clips.pop();
  }
}

/** Capture target: the open drawer if one is open in the panel, else the session drawer. */
function targetDrawer(state: State): Drawer {
  if (state.openDrawerId) {
    const open = state.drawers.find((d) => d.id === state.openDrawerId);
    if (open) return open;
  }
  let session = state.drawers.find((d) => d.id === state.sessionDrawerId);
  if (!session) {
    session = makeDrawer(sessionName(), true, state.settings);
    state.drawers.unshift(session);
    state.sessionDrawerId = session.id;
  }
  return session;
}

interface NewClip {
  kind: ClipKind;
  text?: string;
  dataUrl?: string;
  srcUrl?: string;
}

async function addClip(state: State, partial: NewClip): Promise<void> {
  const hashBasis = partial.text ?? partial.dataUrl ?? partial.srcUrl ?? '';
  const hash = await sha256(hashBasis);
  const drawer = targetDrawer(state);
  const now = Date.now();

  const existingIndex = drawer.clips.findIndex((c) => c.hash === hash);
  if (existingIndex >= 0) {
    // Dedup: move the existing clip to the top instead of adding a duplicate.
    const [existing] = drawer.clips.splice(existingIndex, 1);
    existing.lastCopiedAt = now;
    drawer.clips.unshift(existing);
    return;
  }

  const sizeBytes =
    partial.text !== undefined
      ? new TextEncoder().encode(partial.text).length
      : (partial.dataUrl ?? partial.srcUrl ?? '').length;

  drawer.clips.unshift({
    id: crypto.randomUUID(),
    kind: partial.kind,
    text: partial.text,
    dataUrl: partial.dataUrl,
    srcUrl: partial.srcUrl,
    hash,
    createdAt: now,
    lastCopiedAt: now,
    sizeBytes,
  });
  enforceLimits(drawer);
}

/** New browser session: prune empty non-open drawers and create a fresh session drawer. */
function startNewSession(): Promise<void> {
  return mutate((state) => {
    state.drawers = state.drawers.filter(
      (d) => d.clips.length > 0 || d.id === state.openDrawerId,
    );
    for (const d of state.drawers) d.isSession = false;
    const session = makeDrawer(sessionName(), true, state.settings);
    state.drawers.unshift(session);
    state.sessionDrawerId = session.id;
  });
}

// ---------------------------------------------------------------------------
// Context menus (image + audio capture)
// ---------------------------------------------------------------------------
function setupContextMenus(): void {
  chrome.contextMenus.removeAll(() => {
    chrome.contextMenus.create({
      id: MENU_IMAGE,
      title: 'Clip image to Clip Yourself',
      contexts: ['image'],
    });
    chrome.contextMenus.create({
      id: MENU_AUDIO,
      title: 'Clip audio to Clip Yourself',
      contexts: ['audio', 'link'],
    });
  });
}

function isAudioUrl(url: string): boolean {
  try {
    const pathname = new URL(url).pathname.toLowerCase();
    return AUDIO_EXTENSIONS.some((ext) => pathname.endsWith(ext));
  } catch {
    return false;
  }
}

async function blobToDataUrl(blob: Blob): Promise<string> {
  const bytes = new Uint8Array(await blob.arrayBuffer());
  let binary = '';
  const chunk = 0x8000;
  for (let i = 0; i < bytes.length; i += chunk) {
    binary += String.fromCharCode(...bytes.subarray(i, i + chunk));
  }
  return `data:${blob.type || 'image/png'};base64,${btoa(binary)}`;
}

async function clipImage(srcUrl: string): Promise<void> {
  let dataUrl: string | undefined;
  try {
    const response = await fetch(srcUrl);
    if (response.ok) {
      const encoded = await blobToDataUrl(await response.blob());
      // Cap at 2 MB after encoding; larger images fall back to the source URL.
      if (encoded.length <= MAX_IMAGE_DATA_URL_BYTES) dataUrl = encoded;
    }
  } catch {
    // Fetch failed (CORS/offline) — store the srcUrl only.
  }
  await mutate((state) => addClip(state, { kind: 'image', dataUrl, srcUrl }));
}

async function clipAudio(srcUrl: string): Promise<void> {
  await mutate((state) => addClip(state, { kind: 'audio', srcUrl }));
}

chrome.contextMenus.onClicked.addListener((info) => {
  if (info.menuItemId === MENU_IMAGE && info.srcUrl) {
    void clipImage(info.srcUrl);
  } else if (info.menuItemId === MENU_AUDIO) {
    const url = info.srcUrl ?? info.linkUrl;
    if (url && isAudioUrl(url)) void clipAudio(url);
  }
});

// ---------------------------------------------------------------------------
// Lifecycle
// ---------------------------------------------------------------------------
chrome.runtime.onInstalled.addListener(() => {
  setupContextMenus();
  void startNewSession();
});

chrome.runtime.onStartup.addListener(() => {
  setupContextMenus();
  void startNewSession();
});

// ---------------------------------------------------------------------------
// Command handling (side panel + content script)
// ---------------------------------------------------------------------------
async function handleCommand(msg: Command): Promise<CommandResponse> {
  switch (msg.type) {
    case 'clip-text': {
      if (!msg.text || !msg.text.trim()) return { ok: false };
      await mutate((state) => addClip(state, { kind: 'text', text: msg.text }));
      return { ok: true };
    }
    case 'new-drawer': {
      const id = await mutate((state) => {
        const count = state.drawers.filter((d) => !d.isSession).length + 1;
        const drawer = makeDrawer(msg.name?.trim() || `Drawer ${count}`, false, state.settings);
        state.drawers.push(drawer);
        state.openDrawerId = drawer.id;
        return drawer.id;
      });
      return { ok: true, id };
    }
    case 'rename-drawer': {
      await mutate((state) => {
        const drawer = state.drawers.find((d) => d.id === msg.drawerId);
        if (drawer && msg.name.trim()) drawer.name = msg.name.trim();
      });
      return { ok: true };
    }
    case 'delete-drawer': {
      await mutate((state) => {
        state.drawers = state.drawers.filter((d) => d.id !== msg.drawerId);
        if (state.openDrawerId === msg.drawerId) delete state.openDrawerId;
        if (state.sessionDrawerId === msg.drawerId) {
          const session = makeDrawer(sessionName(), true, state.settings);
          state.drawers.unshift(session);
          state.sessionDrawerId = session.id;
        }
      });
      return { ok: true };
    }
    case 'clear-drawer': {
      await mutate((state) => {
        const drawer = state.drawers.find((d) => d.id === msg.drawerId);
        if (drawer) drawer.clips = [];
      });
      return { ok: true };
    }
    case 'delete-clip': {
      await mutate((state) => {
        const drawer = state.drawers.find((d) => d.id === msg.drawerId);
        if (drawer) drawer.clips = drawer.clips.filter((c) => c.id !== msg.clipId);
      });
      return { ok: true };
    }
    case 'clip-copied': {
      await mutate((state) => {
        const drawer = state.drawers.find((d) => d.id === msg.drawerId);
        if (!drawer) return;
        const index = drawer.clips.findIndex((c) => c.id === msg.clipId);
        if (index < 0) return;
        const [clip] = drawer.clips.splice(index, 1);
        clip.lastCopiedAt = Date.now();
        drawer.clips.unshift(clip);
      });
      return { ok: true };
    }
    case 'set-open-drawer': {
      await mutate((state) => {
        if (msg.drawerId && state.drawers.some((d) => d.id === msg.drawerId)) {
          state.openDrawerId = msg.drawerId;
        } else {
          delete state.openDrawerId;
        }
      });
      return { ok: true };
    }
    case 'set-settings': {
      await mutate(async (state) => {
        const wasPersist = state.settings.persist;
        state.settings = {
          persist: !!msg.settings.persist,
          defaultMaxClips: clampInt(msg.settings.defaultMaxClips, 1, 10000, 200),
          defaultMaxSizeMB: clampInt(msg.settings.defaultMaxSizeMB, 1, 500, 25),
        };
        if (wasPersist !== state.settings.persist) {
          // Migrate: saveState() (after this fn) writes to the new area; clear the old one.
          await removeStateFrom(wasPersist);
        }
      });
      return { ok: true };
    }
    case 'set-drawer-limits': {
      await mutate((state) => {
        const drawer = state.drawers.find((d) => d.id === msg.drawerId);
        if (!drawer) return;
        drawer.maxClips = clampInt(msg.maxClips, 1, 10000, drawer.maxClips);
        drawer.maxSizeMB = clampInt(msg.maxSizeMB, 1, 500, drawer.maxSizeMB);
        enforceLimits(drawer);
      });
      return { ok: true };
    }
  }
}

function clampInt(value: number, min: number, max: number, fallback: number): number {
  const n = Math.floor(Number(value));
  if (!Number.isFinite(n)) return fallback;
  return Math.min(max, Math.max(min, n));
}

chrome.runtime.onMessage.addListener((msg: Command, _sender, sendResponse) => {
  handleCommand(msg)
    .then(sendResponse)
    .catch((err: unknown) => sendResponse({ ok: false, error: String(err) }));
  return true; // async response
});
