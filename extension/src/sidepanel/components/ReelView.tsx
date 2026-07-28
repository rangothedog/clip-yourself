import { useEffect, useState } from 'react';
import type { ClipItem, Drawer } from '../../shared/types';
import { formatBytes } from '../util';
import { ClipRow } from './ClipRow';
import { ConfirmButton } from './ConfirmButton';

/** Film-strip overlay shown when a drawer is open. Captures target this drawer. */
export function ReelView({
  drawer,
  onBack,
  onRename,
  onClear,
  onDeleteDrawer,
  onLimits,
  onCopyClip,
  onDeleteClip,
}: {
  drawer: Drawer;
  onBack: () => void;
  onRename: (name: string) => void;
  onClear: () => void;
  onDeleteDrawer: () => void;
  onLimits: (maxClips: number, maxSizeMB: number) => void;
  onCopyClip: (clip: ClipItem) => void;
  onDeleteClip: (clip: ClipItem) => void;
}) {
  const [name, setName] = useState(drawer.name);
  const [maxClips, setMaxClips] = useState(String(drawer.maxClips));
  const [maxSizeMB, setMaxSizeMB] = useState(String(drawer.maxSizeMB));

  useEffect(() => setName(drawer.name), [drawer.name]);
  useEffect(() => setMaxClips(String(drawer.maxClips)), [drawer.maxClips]);
  useEffect(() => setMaxSizeMB(String(drawer.maxSizeMB)), [drawer.maxSizeMB]);

  const totalBytes = drawer.clips.reduce((sum, c) => sum + c.sizeBytes, 0);

  const commitName = () => {
    const trimmed = name.trim();
    if (trimmed && trimmed !== drawer.name) onRename(trimmed);
    else setName(drawer.name);
  };

  const commitLimits = () => {
    const clips = parseInt(maxClips, 10);
    const mb = parseInt(maxSizeMB, 10);
    onLimits(
      Number.isFinite(clips) && clips > 0 ? clips : drawer.maxClips,
      Number.isFinite(mb) && mb > 0 ? mb : drawer.maxSizeMB,
    );
  };

  return (
    <div className="reel">
      <div className="reel-header">
        <div className="reel-header-top">
          <button className="btn icon-btn" onClick={onBack} title="Back">
            ←
          </button>
          <input
            className="reel-name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            onBlur={commitName}
            onKeyDown={(e) => e.key === 'Enter' && (e.target as HTMLInputElement).blur()}
          />
        </div>
        <div className="reel-header-bottom">
          <span className="reel-stats">
            {drawer.clips.length} clip{drawer.clips.length === 1 ? '' : 's'} ·{' '}
            {formatBytes(totalBytes)}
          </span>
          <label className="reel-limit">
            Max clips
            <input
              type="number"
              min={1}
              value={maxClips}
              onChange={(e) => setMaxClips(e.target.value)}
              onBlur={commitLimits}
            />
          </label>
          <label className="reel-limit">
            Max MB
            <input
              type="number"
              min={1}
              value={maxSizeMB}
              onChange={(e) => setMaxSizeMB(e.target.value)}
              onBlur={commitLimits}
            />
          </label>
          <ConfirmButton className="btn small" label="Clear all" onConfirm={onClear} />
          <ConfirmButton className="btn small" label="Delete drawer" onConfirm={onDeleteDrawer} />
        </div>
      </div>
      <div className="reel-body">
        <div className="reel-side" />
        <div className="reel-clips">
          {drawer.clips.length === 0 && (
            <div className="empty">This drawer is empty. Copy something — it lands here while the drawer is open.</div>
          )}
          {drawer.clips.map((clip) => (
            <ClipRow
              key={clip.id}
              clip={clip}
              large
              onCopy={() => onCopyClip(clip)}
              onDelete={() => onDeleteClip(clip)}
            />
          ))}
        </div>
        <div className="reel-side" />
      </div>
    </div>
  );
}
