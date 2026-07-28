export type ClipKind = 'text' | 'image' | 'audio';

export interface ClipItem {
  id: string;
  kind: ClipKind;
  /** Text content for kind === 'text'. */
  text?: string;
  /** Inline data URL for kind === 'image' (omitted when over the size cap). */
  dataUrl?: string;
  /** Original source URL for images and audio. */
  srcUrl?: string;
  /** SHA-256 hex of text | dataUrl | srcUrl — used for dedup. */
  hash: string;
  createdAt: number;
  lastCopiedAt: number;
  sizeBytes: number;
}

export interface Drawer {
  id: string;
  name: string;
  createdAt: number;
  isSession: boolean;
  /** Max number of clips kept in this drawer (default 200). */
  maxClips: number;
  /** Max total size in MB kept in this drawer (default 25). */
  maxSizeMB: number;
  clips: ClipItem[];
}

export interface Settings {
  /** When true drawers persist across browser sessions (chrome.storage.local). */
  persist: boolean;
  defaultMaxClips: number;
  defaultMaxSizeMB: number;
}

export interface State {
  drawers: Drawer[];
  sessionDrawerId: string;
  /** Drawer currently open in the side panel reel view; capture target when set. */
  openDrawerId?: string;
  settings: Settings;
}

export const DEFAULT_SETTINGS: Settings = {
  persist: false,
  defaultMaxClips: 200,
  defaultMaxSizeMB: 25,
};
