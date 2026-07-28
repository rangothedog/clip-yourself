import { useCallback, useEffect, useRef, useState } from 'react';
import { getState } from '../shared/store';
import type { ClipItem, State } from '../shared/types';
import { ClipRow } from './components/ClipRow';
import { DrawerRow } from './components/DrawerRow';
import { ReelView } from './components/ReelView';
import { SettingsView } from './components/SettingsView';
import { copyClip, send } from './util';

export function App() {
  const [state, setState] = useState<State | null>(null);
  const [view, setView] = useState<'main' | 'settings'>('main');
  const [toast, setToast] = useState<string | null>(null);
  const toastTimer = useRef<number | undefined>(undefined);

  const refresh = useCallback(async () => {
    setState(await getState());
  }, []);

  useEffect(() => {
    void refresh();
    const listener = () => void refresh();
    chrome.storage.onChanged.addListener(listener);
    return () => chrome.storage.onChanged.removeListener(listener);
  }, [refresh]);

  const showToast = useCallback((message: string) => {
    setToast(message);
    window.clearTimeout(toastTimer.current);
    toastTimer.current = window.setTimeout(() => setToast(null), 1500);
  }, []);

  const handleCopy = useCallback(
    async (drawerId: string, clip: ClipItem) => {
      try {
        await copyClip(clip);
        void send({ type: 'clip-copied', drawerId, clipId: clip.id });
        showToast('Copied ✓');
      } catch {
        showToast('Copy failed');
      }
    },
    [showToast],
  );

  if (!state) return <div className="loading">Loading…</div>;

  const sessionDrawer = state.drawers.find((d) => d.id === state.sessionDrawerId);
  const otherDrawers = state.drawers.filter((d) => d.id !== state.sessionDrawerId);
  const openDrawer = state.openDrawerId
    ? state.drawers.find((d) => d.id === state.openDrawerId)
    : undefined;

  return (
    <div className="app">
      <header className="header">
        <h1>Clip Yourself</h1>
        <div className="header-actions">
          <button className="btn accent" onClick={() => void send({ type: 'new-drawer' })}>
            ➕ New Drawer
          </button>
          <button className="btn icon-btn" title="Settings" onClick={() => setView('settings')}>
            ⚙
          </button>
        </div>
      </header>

      <main className="main">
        <section className="clip-list">
          {sessionDrawer && sessionDrawer.clips.length > 0 ? (
            sessionDrawer.clips.map((clip) => (
              <ClipRow
                key={clip.id}
                clip={clip}
                onCopy={() => void handleCopy(sessionDrawer.id, clip)}
                onDelete={() =>
                  void send({ type: 'delete-clip', drawerId: sessionDrawer.id, clipId: clip.id })
                }
              />
            ))
          ) : (
            <div className="empty">
              Nothing clipped yet. Copy text on any page, or right-click an image or audio link.
            </div>
          )}
        </section>

        <section className="drawers">
          <h2>Drawers</h2>
          {otherDrawers.length === 0 && <div className="empty">No drawers yet.</div>}
          {otherDrawers.map((drawer) => (
            <DrawerRow
              key={drawer.id}
              drawer={drawer}
              onOpen={() => void send({ type: 'set-open-drawer', drawerId: drawer.id })}
              onRename={(name) => void send({ type: 'rename-drawer', drawerId: drawer.id, name })}
              onDelete={() => void send({ type: 'delete-drawer', drawerId: drawer.id })}
            />
          ))}
        </section>
      </main>

      {openDrawer && (
        <ReelView
          drawer={openDrawer}
          onBack={() => void send({ type: 'set-open-drawer' })}
          onRename={(name) =>
            void send({ type: 'rename-drawer', drawerId: openDrawer.id, name })
          }
          onClear={() => void send({ type: 'clear-drawer', drawerId: openDrawer.id })}
          onDeleteDrawer={() => void send({ type: 'delete-drawer', drawerId: openDrawer.id })}
          onLimits={(maxClips, maxSizeMB) =>
            void send({ type: 'set-drawer-limits', drawerId: openDrawer.id, maxClips, maxSizeMB })
          }
          onCopyClip={(clip) => void handleCopy(openDrawer.id, clip)}
          onDeleteClip={(clip) =>
            void send({ type: 'delete-clip', drawerId: openDrawer.id, clipId: clip.id })
          }
        />
      )}

      {view === 'settings' && (
        <SettingsView
          settings={state.settings}
          onChange={(settings) => void send({ type: 'set-settings', settings })}
          onBack={() => setView('main')}
        />
      )}

      {toast && <div className="toast">{toast}</div>}
    </div>
  );
}
