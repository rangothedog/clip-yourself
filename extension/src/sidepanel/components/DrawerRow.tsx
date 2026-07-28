import { useState } from 'react';
import type { Drawer } from '../../shared/types';
import { ConfirmButton } from './ConfirmButton';

export function DrawerRow({
  drawer,
  onOpen,
  onRename,
  onDelete,
}: {
  drawer: Drawer;
  onOpen: () => void;
  onRename: (name: string) => void;
  onDelete: () => void;
}) {
  const [editing, setEditing] = useState(false);
  const [name, setName] = useState(drawer.name);

  const commitRename = () => {
    setEditing(false);
    const trimmed = name.trim();
    if (trimmed && trimmed !== drawer.name) onRename(trimmed);
    else setName(drawer.name);
  };

  return (
    <div className="drawer-row">
      <div className="drawer-info">
        {editing ? (
          <input
            className="drawer-rename-input"
            value={name}
            autoFocus
            onChange={(e) => setName(e.target.value)}
            onBlur={commitRename}
            onKeyDown={(e) => {
              if (e.key === 'Enter') commitRename();
              if (e.key === 'Escape') {
                setName(drawer.name);
                setEditing(false);
              }
            }}
          />
        ) : (
          <div className="drawer-name">{drawer.name}</div>
        )}
        <div className="drawer-meta">
          {drawer.clips.length} clip{drawer.clips.length === 1 ? '' : 's'} ·{' '}
          {new Date(drawer.createdAt).toLocaleDateString()}
        </div>
      </div>
      <div className="drawer-actions">
        <button className="btn" onClick={onOpen}>
          Open
        </button>
        <button className="btn" onClick={() => setEditing(true)}>
          Rename
        </button>
        <ConfirmButton className="btn" label="Delete" onConfirm={onDelete} />
      </div>
    </div>
  );
}
