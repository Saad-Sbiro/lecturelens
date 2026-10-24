import { initializeAgreement, termsAccepted } from './agreement';
import strings from '../../config/browser-ui.json';
const text = strings as Record<string, string>;
document.querySelectorAll<HTMLElement>('[data-text]').forEach(e => { e.textContent = text[e.dataset.text!]; });
const el = <T extends HTMLElement>(id: string) => document.getElementById(id) as T;
let busy = false, updating = false, previousState = 'idle';
const modes = Array.from(document.querySelectorAll<HTMLInputElement>('[name="lessonMode"]'));
const selectedMode = () => modes.find(input => input.checked)!.value;
function renderMode() {
  const voice = selectedMode() === 'voice';
  el('voiceCard').hidden = !voice; el('slideCard').hidden = voice;
  el('cloudChoice').hidden = voice;
  el('preview').textContent = voice ? text.voicePreview : text.preview;
  document.querySelector<HTMLElement>('[data-text="privacy"]')!.textContent = voice ? text.voicePrivacy : text.privacy;
}
for (const input of modes) input.onchange = () => { el<HTMLInputElement>('cloudConsent').checked = false; renderMode(); };
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
    if (value.state !== previousState && ['idle', 'finished', 'failed'].includes(value.state)) el<HTMLInputElement>('cloudConsent').checked = false;
    previousState = value.state;
    const active = ['loading', 'previewing', 'recording', 'paused', 'stopping'].includes(value.state);
    el<HTMLFieldSetElement>('lessonFormat').disabled = busy || active;
    el<HTMLInputElement>('cloudConsent').disabled = busy || active;
    if (active && value.lessonMode) modes.forEach(input => { input.checked = input.value === value.lessonMode; });
    renderMode();
    document.body.dataset.state = value.state;
    document.body.classList.toggle('active', ['previewing', 'recording', 'paused', 'stopping'].includes(value.state));
    el('tab').textContent = value.sourceTitle ?? tab?.title ?? text.unavailable;
    el('cropStatus').textContent = (text[value.visualStatus] ?? value.visualStatus ?? (value.cropConfirmed ? text.cropReady : text.cropReview)) + ' · ' + text.slidesSaved + ': ' + (value.slides ?? 0);
    el('status').textContent = value.state === 'recording' && value.lessonMode === 'voice' ? text.voiceRecording : text[value.state] ?? value.state;
    if (value.error) el('error').textContent = value.error;
    el<HTMLProgressElement>('meter').value = value.level ?? 0;
    const image = el<HTMLImageElement>('image'); image.hidden = !value.image; if (value.image) image.src = value.image;
    const box = el('cropBox'); box.hidden = !value.image || !value.cropConfirmed;
    if (value.crop) Object.assign(box.style, { left: value.crop.x * 100 + '%', top: value.crop.y * 100 + '%', width: value.crop.width * 100 + '%', height: value.crop.height * 100 + '%' });
    el<HTMLButtonElement>('preview').disabled = busy || !termsAccepted || !['idle', 'finished', 'failed'].includes(value.state);
    el<HTMLButtonElement>('openCrop').disabled = busy || value.state !== 'previewing';
    el<HTMLButtonElement>('autoCrop').disabled = busy || value.state !== 'previewing' || !value.cloudConsent;
    el<HTMLButtonElement>('start').disabled = busy || value.state !== 'previewing' || (value.lessonMode !== 'voice' && !value.cropConfirmed);
    el<HTMLButtonElement>('pause').disabled = busy || !['recording', 'paused'].includes(value.state);
    el('pause').textContent = value.state === 'paused' ? text.resume : text.pauseOnly;
    el<HTMLButtonElement>('stop').disabled = busy || !['previewing', 'recording', 'paused'].includes(value.state);
  } catch (e) { el('error').textContent = String(e); }
  finally { updating = false; }
}
for (const op of ['preview', 'openCrop', 'autoCrop', 'start', 'pause', 'stop']) el(op).onclick = async () => {
  if (busy) return; busy = true; el('error').textContent = '';
  for (const id of ['preview', 'openCrop', 'autoCrop', 'start', 'pause', 'stop']) el<HTMLButtonElement>(id).disabled = true;
  el<HTMLFieldSetElement>('lessonFormat').disabled = true;
  try { await send(op, op === 'preview' ? { tabId: tab?.id, consent: termsAccepted, lessonMode: selectedMode(), cloudConsent: el<HTMLInputElement>('cloudConsent').checked } : {}); }
  catch (e) { el('error').textContent = e instanceof Error ? e.message : String(e); }
  finally { busy = false; await update(); }
};
await initializeAgreement(() => { void update(); });
void update(); setInterval(() => { if (!busy) void update(); }, 1000);
