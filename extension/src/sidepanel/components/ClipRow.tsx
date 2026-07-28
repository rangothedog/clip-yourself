import type { ClipItem } from '../../shared/types';
import { relativeTime } from '../util';
import { WaveformPlayer } from './WaveformPlayer';

const ICONS: Record<ClipItem['kind'], string> = {
  text: '📝',
  image: '🖼',
  audio: '🎵',
};

export function ClipRow({
  clip,
  large = false,
  onCopy,
  onDelete,
}: {
  clip: ClipItem;
  large?: boolean;
  onCopy: () => void;
  onDelete: () => void;
}) {
  const imageSrc = clip.dataUrl ?? clip.srcUrl;
  return (
    <div className={`clip-row${large ? ' large' : ''}`} onClick={onCopy} title="Click to copy">
      <span className="clip-icon">{ICONS[clip.kind]}</span>
      <div className="clip-body">
        {clip.kind === 'text' && <div className="clip-text">{clip.text}</div>}
        {clip.kind === 'image' && imageSrc && (
          <img className="clip-img" src={imageSrc} alt="clipped image" />
        )}
        {clip.kind === 'audio' && clip.srcUrl && <WaveformPlayer url={clip.srcUrl} />}
        <div className="clip-meta">{relativeTime(clip.lastCopiedAt)}</div>
      </div>
      <button
        className="clip-delete"
        title="Delete clip"
        onClick={(e) => {
          e.stopPropagation();
          onDelete();
        }}
      >
        ✕
      </button>
    </div>
  );
}
