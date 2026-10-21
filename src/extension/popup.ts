import strings from '../../config/browser-ui.json';
const text = strings as Record<string, string>;
document.querySelectorAll<HTMLElement>('[data-text]').forEach(e => { e.textContent = text[e.dataset.text!]; });
const el = <T extends HTMLElement>(id: string) => document.getElementById(id) as T;
let busy = false;
const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
el('tab').textContent = tab?.title ?? text.unavailable;
async function send(op: string, extra = {}) {
  const reply = await chrome.runtime.sendMessage({ target: 'background', op, ...extra });
  if (!reply?.ok) throw new Error(reply?.error ?? text.connectFailed);
  return reply.value;
}
async function update() {
  try {
    const value = await send('status');
    el('tab').textContent = value.sourceTitle ? `${text.selected}: ${value.sourceTitle}` : (tab?.title ?? text.unavailable);
    el('status').textContent = `${text[value.state] ?? value.state} · ${text.savedChunks}: ${value.packets ?? 0}`;
    if (value.error) el('error').textContent = value.error;
    el<HTMLProgressElement>('meter').value = value.level ?? 0;
    const image = el<HTMLImageElement>('image'); image.hidden = !value.image; if (value.image) image.src = value.image;
    el<HTMLButtonElement>('preview').disabled = busy || !['idle', 'finished', 'failed'].includes(value.state);
    el<HTMLButtonElement>('start').disabled = busy || value.state !== 'previewing';
    el<HTMLButtonElement>('pause').disabled = busy || !['recording', 'paused'].includes(value.state);
    el<HTMLButtonElement>('stop').disabled = busy || !['previewing', 'recording', 'paused'].includes(value.state);
  } catch (e) { el('error').textContent = String(e); }
}
for (const op of ['preview', 'start', 'pause', 'stop']) el(op).onclick = async () => {
  if (busy) return; busy = true; el('error').textContent = '';
  try { await send(op, op === 'preview' ? { tabId: tab?.id, consent: el<HTMLInputElement>('consent').checked } : {}); }
  catch (e) { el('error').textContent = e instanceof Error ? e.message : String(e); }
  finally { busy = false; await update(); }
};
void update(); setInterval(() => { if (!busy) void update(); }, 1000);
