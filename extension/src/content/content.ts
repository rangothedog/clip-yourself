// Clip Yourself content script — bundled as a single IIFE (no module imports at runtime).
// Listens for copy/cut events and forwards the selected text to the background worker.

function selectedText(): string {
  const el = document.activeElement;
  if (el instanceof HTMLTextAreaElement || el instanceof HTMLInputElement) {
    try {
      const start = el.selectionStart;
      const end = el.selectionEnd;
      if (typeof start === 'number' && typeof end === 'number' && start !== end) {
        return el.value.slice(start, end);
      }
    } catch {
      // Some input types (e.g. number/email) throw on selectionStart access.
    }
  }
  return window.getSelection()?.toString() ?? '';
}

function onCopyOrCut(): void {
  const text = selectedText();
  if (!text || !text.trim()) return; // ignore empty/whitespace-only
  try {
    void chrome.runtime.sendMessage({ type: 'clip-text', text }).catch(() => {});
  } catch {
    // Extension context invalidated (e.g. extension reloaded) — ignore.
  }
}

document.addEventListener('copy', onCopyOrCut, true);
document.addEventListener('cut', onCopyOrCut, true);
