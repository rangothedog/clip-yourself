import type { Command, CommandResponse } from '../shared/messages';
import type { ClipItem } from '../shared/types';

/** Send a command to the background worker (all mutations happen there). */
export function send(cmd: Command): Promise<CommandResponse | undefined> {
  try {
    return chrome.runtime.sendMessage(cmd).catch(() => undefined);
  } catch {
    return Promise.resolve(undefined);
  }
}

/** Copy a clip back to the OS clipboard. */
export async function copyClip(clip: ClipItem): Promise<void> {
  if (clip.kind === 'text') {
    await navigator.clipboard.writeText(clip.text ?? '');
    return;
  }
  if (clip.kind === 'audio') {
    // Audio can't be placed on the clipboard — copy its URL as text.
    await navigator.clipboard.writeText(clip.srcUrl ?? '');
    return;
  }
  // Image: Chrome's async clipboard only accepts image/png, so re-encode via canvas.
  const source = clip.dataUrl ?? clip.srcUrl;
  if (!source) throw new Error('Image clip has no source');
  const blob = await (await fetch(source)).blob();
  const bitmap = await createImageBitmap(blob);
  const canvas = document.createElement('canvas');
  canvas.width = bitmap.width;
  canvas.height = bitmap.height;
  canvas.getContext('2d')!.drawImage(bitmap, 0, 0);
  const png = await new Promise<Blob>((resolve, reject) =>
    canvas.toBlob((b) => (b ? resolve(b) : reject(new Error('PNG encode failed'))), 'image/png'),
  );
  await navigator.clipboard.write([new ClipboardItem({ 'image/png': png })]);
}

export function relativeTime(timestamp: number): string {
  const seconds = Math.max(0, Math.floor((Date.now() - timestamp) / 1000));
  if (seconds < 60) return 'just now';
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  if (days < 7) return `${days}d ago`;
  return new Date(timestamp).toLocaleDateString();
}

export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
}

export function formatSeconds(seconds: number): string {
  if (!Number.isFinite(seconds)) return '0:00';
  const m = Math.floor(seconds / 60);
  const s = Math.floor(seconds % 60);
  return `${m}:${s.toString().padStart(2, '0')}`;
}
