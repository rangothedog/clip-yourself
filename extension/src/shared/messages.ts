import type { Settings } from './types';

/** Commands handled by the background service worker (all mutations live there). */
export type Command =
  | { type: 'clip-text'; text: string }
  | { type: 'new-drawer'; name?: string }
  | { type: 'rename-drawer'; drawerId: string; name: string }
  | { type: 'delete-drawer'; drawerId: string }
  | { type: 'clear-drawer'; drawerId: string }
  | { type: 'delete-clip'; drawerId: string; clipId: string }
  | { type: 'clip-copied'; drawerId: string; clipId: string }
  | { type: 'set-open-drawer'; drawerId?: string }
  | { type: 'set-settings'; settings: Settings }
  | { type: 'set-drawer-limits'; drawerId: string; maxClips: number; maxSizeMB: number };

export interface CommandResponse {
  ok: boolean;
  id?: string;
  error?: string;
}
