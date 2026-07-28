import type { Drawer, Settings, State } from './types';
import { DEFAULT_SETTINGS } from './types';

const SETTINGS_KEY = 'cy-settings';
const STATE_KEY = 'cy-state';

/** The drawer portion of State, stored in local or session area depending on settings.persist. */
interface StoredState {
  drawers: Drawer[];
  sessionDrawerId: string;
  openDrawerId?: string;
}

function areaFor(persist: boolean): chrome.storage.StorageArea {
  return persist ? chrome.storage.local : chrome.storage.session;
}

export async function getSettings(): Promise<Settings> {
  const result = await chrome.storage.local.get(SETTINGS_KEY);
  return { ...DEFAULT_SETTINGS, ...(result[SETTINGS_KEY] as Partial<Settings> | undefined) };
}

export async function saveSettings(settings: Settings): Promise<void> {
  await chrome.storage.local.set({ [SETTINGS_KEY]: settings });
}

export async function getState(): Promise<State> {
  const settings = await getSettings();
  const result = await areaFor(settings.persist).get(STATE_KEY);
  const stored = (result[STATE_KEY] as StoredState | undefined) ?? {
    drawers: [],
    sessionDrawerId: '',
  };
  return {
    drawers: stored.drawers,
    sessionDrawerId: stored.sessionDrawerId,
    openDrawerId: stored.openDrawerId,
    settings,
  };
}

export async function saveState(state: State): Promise<void> {
  await saveSettings(state.settings);
  const stored: StoredState = {
    drawers: state.drawers,
    sessionDrawerId: state.sessionDrawerId,
  };
  if (state.openDrawerId !== undefined) stored.openDrawerId = state.openDrawerId;
  await areaFor(state.settings.persist).set({ [STATE_KEY]: stored });
}

/** Remove the drawer state from the given area (used when the persist toggle flips). */
export async function removeStateFrom(persist: boolean): Promise<void> {
  await areaFor(persist).remove(STATE_KEY);
}
