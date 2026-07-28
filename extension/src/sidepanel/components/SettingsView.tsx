import { useEffect, useState } from 'react';
import type { Settings } from '../../shared/types';

export function SettingsView({
  settings,
  onChange,
  onBack,
}: {
  settings: Settings;
  onChange: (settings: Settings) => void;
  onBack: () => void;
}) {
  const [maxClips, setMaxClips] = useState(String(settings.defaultMaxClips));
  const [maxSizeMB, setMaxSizeMB] = useState(String(settings.defaultMaxSizeMB));

  useEffect(() => setMaxClips(String(settings.defaultMaxClips)), [settings.defaultMaxClips]);
  useEffect(() => setMaxSizeMB(String(settings.defaultMaxSizeMB)), [settings.defaultMaxSizeMB]);

  const commitNumbers = () => {
    const clips = parseInt(maxClips, 10);
    const mb = parseInt(maxSizeMB, 10);
    onChange({
      ...settings,
      defaultMaxClips: Number.isFinite(clips) && clips > 0 ? clips : settings.defaultMaxClips,
      defaultMaxSizeMB: Number.isFinite(mb) && mb > 0 ? mb : settings.defaultMaxSizeMB,
    });
  };

  return (
    <div className="settings">
      <div className="settings-header">
        <button className="btn icon-btn" onClick={onBack} title="Back">
          ←
        </button>
        <h2>Settings</h2>
      </div>
      <label className="settings-row toggle-row">
        <input
          type="checkbox"
          checked={settings.persist}
          onChange={(e) => onChange({ ...settings, persist: e.target.checked })}
        />
        <span>Save clips between sessions</span>
      </label>
      <label className="settings-row">
        <span>Default max clips per drawer</span>
        <input
          type="number"
          min={1}
          value={maxClips}
          onChange={(e) => setMaxClips(e.target.value)}
          onBlur={commitNumbers}
        />
      </label>
      <label className="settings-row">
        <span>Default max MB per drawer</span>
        <input
          type="number"
          min={1}
          value={maxSizeMB}
          onChange={(e) => setMaxSizeMB(e.target.value)}
          onBlur={commitNumbers}
        />
      </label>
      <p className="settings-note">
        Clips are captured when you copy or cut text on a web page, and via the right-click
        context menus "Clip image / Clip audio to Clip Yourself". Browsers can't observe the OS
        clipboard globally, so copies made outside web pages won't be captured.
      </p>
    </div>
  );
}
