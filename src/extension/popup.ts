import strings from '../../config/browser-ui.json';
const text = strings as Record<string, string>;
document.querySelectorAll<HTMLElement>('[data-text]').forEach(e => { e.textContent = text[e.dataset.text!]; });
const el = <T extends HTMLElement>(id: string) => document.getElementById(id) as T;
let busy = false, updating = false;
const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
async function send(op: string, extra = {}) {
  const reply = await chrome.runtime.sendMessage({ target: 'background', op, ...extra });
  if (!reply?.ok) throw new Error(reply?.error ?? text.connectFailed);
  return reply.value;
}
async function update() {
  if (updating) return; updating = true;
  try {
    const value = await send('status');
    document.body.classList.toggle('active', ['previewing', 'recording', 'paused', 'stopping'].includes(value.state));
    el('tab').textContent = value.sourceTitle ?? tab?.title ?? text.unavailable;
    el('cropStatus').textContent = (value.cropConfirmed ? text.cropReady : text.cropReview) + ' · ' + text.slidesSaved + ': ' + (value.slides ?? 0);
    el('status').textContent = text[value.state] ?? value.state;
    if (value.error) el('error').textContent = value.error;
    el<HTMLProgressElement>('meter').value = value.level ?? 0;
    const image = el<HTMLImageElement>('image'); image.hidden = !value.image; if (value.image) image.src = value.image;
    const box = el('cropBox'); box.hidden = !value.image || !value.cropConfirmed;
    if (value.crop) Object.assign(box.style, { left: value.crop.x * 100 + '%', top: value.crop.y * 100 + '%', width: value.crop.width * 100 + '%', height: value.crop.height * 100 + '%' });
    el<HTMLButtonElement>('preview').disabled = busy || !['idle', 'finished', 'failed'].includes(value.state);
    el<HTMLButtonElement>('openCrop').disabled = busy || value.state !== 'previewing';
    el<HTMLButtonElement>('start').disabled = busy || value.state !== 'previewing' || !value.cropConfirmed;
    el<HTMLButtonElement>('pause').disabled = busy || !['recording', 'paused'].includes(value.state);
    el('pause').textContent = value.state === 'paused' ? text.resume : text.pauseOnly;
    el<HTMLButtonElement>('stop').disabled = busy || !['previewing', 'recording', 'paused'].includes(value.state);
  } catch (e) { el('error').textContent = String(e); }
  finally { updating = false; }
}
for (const op of ['preview', 'openCrop', 'start', 'pause', 'stop']) el(op).onclick = async () => {
  if (busy) return; busy = true; el('error').textContent = '';
  try { await send(op, op === 'preview' ? { tabId: tab?.id, consent: el<HTMLInputElement>('consent').checked } : {}); }
  catch (e) { el('error').textContent = e instanceof Error ? e.message : String(e); }
  finally { busy = false; await update(); }
};
void update(); setInterval(() => { if (!busy) void update(); }, 1000);
