import text from '../../config/browser-ui.json';
import { fullCrop, validCrop, type Crop } from '../domain/slides';
document.querySelectorAll<HTMLElement>('[data-text]').forEach(e => { e.textContent = (text as Record<string, string>)[e.dataset.text!]; });
const el = <T extends HTMLElement>(id: string) => document.getElementById(id) as T;
const stage = el('stage'), frame = el<HTMLImageElement>('frame'), save = el<HTMLButtonElement>('save');
let crop: Crop = { ...fullCrop }, origin: { x: number; y: number } | undefined, ready = false;
async function send(op: string, extra = {}) {
  const reply = await chrome.runtime.sendMessage({ target: 'background', op, ...extra });
  if (!reply?.ok) throw new Error(reply?.error ?? text.connectFailed);
  return reply.value;
}
function draw() {
  const box = el('selection'); box.hidden = !ready;
  Object.assign(box.style, { left: crop.x * 100 + '%', top: crop.y * 100 + '%', width: crop.width * 100 + '%', height: crop.height * 100 + '%' });
  save.disabled = !ready || !validCrop(crop);
}
function fit() {
  if (!frame.naturalWidth) return;
  const area = stage.parentElement!;
  const scale = Math.min((area.clientWidth - 24) / frame.naturalWidth, (area.clientHeight - 24) / frame.naturalHeight);
  stage.style.width = frame.naturalWidth * scale + 'px'; stage.style.height = frame.naturalHeight * scale + 'px';
}
function point(e: PointerEvent) { const r = stage.getBoundingClientRect(); return { x: Math.max(0, Math.min(1, (e.clientX-r.left)/r.width)), y: Math.max(0, Math.min(1, (e.clientY-r.top)/r.height)) }; }
stage.onpointerdown = e => { if (!ready) return; e.preventDefault(); origin = point(e); stage.setPointerCapture(e.pointerId); };
stage.onpointermove = e => { if (!origin) return; const p = point(e); crop = { x: Math.min(origin.x,p.x), y: Math.min(origin.y,p.y), width: Math.abs(origin.x-p.x), height: Math.abs(origin.y-p.y) }; draw(); };
stage.onpointerup = stage.onpointercancel = () => { origin = undefined; };
frame.onload = () => { ready = true; fit(); draw(); };
new ResizeObserver(fit).observe(stage.parentElement!);
async function load() {
  try {
    ready = false; draw(); const value = await send('status');
    if (value.state !== 'previewing' || !value.image) throw new Error(text.cropUnavailable);
    crop = value.crop ?? { ...fullCrop }; frame.src = value.image;
    el('message').textContent = text.cropInstructions;
  } catch (e) { el('message').textContent = String(e); }
}
el('refresh').onclick = () => void load();
el('reset').onclick = () => { crop = { ...fullCrop }; draw(); };
el('cancel').onclick = () => window.close();
document.onkeydown = e => { if (e.key === 'Escape') window.close(); };
save.onclick = async () => {
  if (!ready || !validCrop(crop)) return; save.disabled = true;
  try { await send('crop', { crop }); el('message').textContent = text.cropSaved; window.close(); }
  catch (e) { el('message').textContent = String(e); save.disabled = false; }
};
void load();

