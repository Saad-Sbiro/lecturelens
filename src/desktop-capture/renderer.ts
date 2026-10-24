import { BrowserMediaSource, chooseMimeType, requestPreview } from '../adapters/capture/media-recorder';
import { SoundMeter } from '../adapters/capture/sound-meter';
import { SlideSampler } from '../extension/slide-sampler';
import { fullCrop, type Crop } from '../domain/slides';
import type { CaptureSession } from '../ports/capture';
import { captureConfig as config } from '../../config/capture';

declare global {
  interface Window { captureBridge: { call<T = any>(op: string, data?: Record<string, unknown>): Promise<T>; onStop(callback: () => void): void } }
}
const bridge = window.captureBridge;
const button = (id: string) => document.getElementById(id) as HTMLButtonElement;
const input = (id: string) => document.getElementById(id) as HTMLInputElement;
const text = (id: string, value: string) => { document.getElementById(id)!.textContent = value; };
const choices = document.getElementById('windows') as HTMLSelectElement;
const video = document.querySelector('video')!;
const canvas = document.getElementById('crop') as HTMLCanvasElement;
let stream: MediaStream | undefined, meter: SoundMeter | undefined, session: CaptureSession | undefined, sampler: SlideSampler | undefined;
let state: 'idle' | 'preview' | 'recording' | 'paused' | 'stopping' = 'idle';
let id = '', started = 0, sequence = 0, eventIndex = 0, recentAudio = -Infinity;
let timer: ReturnType<typeof setInterval> | undefined, checking = false, mutation = false;
let pump: Promise<void> | undefined, ending: Promise<void> | undefined;
let failure = '', crop: Crop = { ...fullCrop };
const now = () => performance.now() - started;
function update() {
  const active = state === 'recording' || state === 'paused';
  button('preview').disabled = mutation || state !== 'idle' || !choices.value || !input('consent').checked;
  button('refresh').disabled = mutation || state !== 'idle'; choices.disabled = mutation || state !== 'idle';
  input('consent').disabled = state !== 'idle';
  button('start').disabled = mutation || state !== 'preview' || performance.now() - recentAudio > config.signalFreshMs;
  button('reset').disabled = mutation || state !== 'preview';
  button('pause').disabled = mutation || !active; button('stop').disabled = mutation || !active;
  button('pause').textContent = state === 'paused' ? 'Resume' : 'Pause';
  input('full').disabled = mutation || state !== 'preview'; input('voice').disabled = mutation || state !== 'preview';
  canvas.style.pointerEvents = state === 'preview' && !input('voice').checked ? 'auto' : 'none';
  document.querySelector('.pill')!.textContent = `Desktop capture · ${state}`;
}
function drawCrop() {
  if (!video.videoWidth) return;
  const scale = Math.min(video.clientWidth / video.videoWidth, video.clientHeight / video.videoHeight);
  canvas.style.width = `${video.videoWidth * scale}px`; canvas.style.height = `${video.videoHeight * scale}px`;
  canvas.width = video.videoWidth; canvas.height = video.videoHeight;
  const ctx = canvas.getContext('2d')!;
  if (input('voice').checked) return;
  ctx.strokeStyle = '#bfa6ff'; ctx.lineWidth = 3 / scale;
  ctx.strokeRect(crop.x * canvas.width, crop.y * canvas.height, crop.width * canvas.width, crop.height * canvas.height);
}
new ResizeObserver(drawCrop).observe(video);
let anchor: { x: number; y: number } | undefined;
function point(event: PointerEvent) { const r = canvas.getBoundingClientRect(); return { x: Math.max(0, Math.min(1, (event.clientX - r.left) / r.width)), y: Math.max(0, Math.min(1, (event.clientY - r.top) / r.height)) }; }
canvas.onpointerdown = event => { anchor = point(event); canvas.setPointerCapture(event.pointerId); };
canvas.onpointerup = event => {
  if (!anchor) return;
  const end = point(event); const width = Math.abs(end.x - anchor.x), height = Math.abs(end.y - anchor.y);
  if (width >= .05 && height >= .05) { crop = { x: Math.min(anchor.x, end.x), y: Math.min(anchor.y, end.y), width, height }; input('full').checked = false; drawCrop(); }
  anchor = undefined;
};
input('full').onchange = () => { if (input('full').checked) crop = { ...fullCrop }; drawCrop(); };
input('voice').onchange = () => { drawCrop(); update(); };
input('consent').onchange = update;
async function refresh() {
  const items = await bridge.call<{ id: string; name: string }[]>('list');
  choices.replaceChildren(...items.map(item => new Option(item.name, item.id)));
  if (!items.length) text('status', 'Open your lesson app, then Refresh.');
}
async function release() {
  clearInterval(timer); timer = undefined;
  stream?.getTracks().forEach(track => track.stop()); stream = undefined; video.srcObject = null;
  await meter?.close(); meter = undefined;
  await bridge.call('release'); session = undefined; sampler = undefined; state = 'idle';
  canvas.width = 0; recentAudio = -Infinity; update();
}
async function flushEvents() {
  while (session && eventIndex < session.events.length) await bridge.call('event', { id, ...session.events[eventIndex++] });
}
async function save() {
  try {
    for await (const chunk of session!.media) {
      await flushEvents();
      for (let offset = 0; offset < chunk.bytes.length; offset += 262144) {
        const bytes = chunk.bytes.subarray(offset, offset + 262144); let binary = '';
        for (let i = 0; i < bytes.length; i += 8192) binary += String.fromCharCode(...bytes.subarray(i, i + 8192));
        await bridge.call('append', { id, sequence: sequence++, atMs: chunk.atMs, data: btoa(binary) });
      }
    }
    state = 'stopping'; update(); await flushEvents(); await sampler?.finish();
    await bridge.call('finish', { id, reason: failure ? 'capture-error' : session!.events.at(-1)?.reason ?? 'user-stop' });
    text('status', failure ? 'Saved with a capture problem. Review this recording.' : 'Saved. Return to LectureLens to create your notes.');
  } catch (error) {
    failure = String(error); void session?.stop('capture-error');
    text('status', 'Recording interrupted. Recover saved packets in LectureLens.');
  } finally { text('error', failure); await release(); }
}
async function stop(reason = 'user-stop') {
  if (ending) return ending;
  ending = (async () => {
    if (session) { state = 'stopping'; update(); text('status', 'Saving…'); await session.stop(reason); await pump; }
    else await release();
  })();
  try { await ending; } finally { ending = undefined; }
}
function fail(error: unknown) { failure = String(error); text('error', failure); void stop('capture-error'); }
async function preview() {
  failure = ''; text('error', '');
  await bridge.call('select', { id: choices.value, consent: input('consent').checked });
  try {
    stream = await requestPreview(new AbortController().signal, 30000);
    if (!stream.getAudioTracks().length) throw new Error('No computer sound stream. Check Windows output permissions and try again.');
    video.srcObject = stream; await video.play();
    meter = new SoundMeter(stream); await meter.start(); await bridge.call('hello');
    state = 'preview'; crop = { ...fullCrop }; input('full').checked = true;
    recentAudio = -Infinity; text('placeholder', ''); text('status', 'Preview · confirm the slide area'); drawCrop();
    stream.getTracks().forEach(track => track.addEventListener('ended', () => { void stop('source-ended'); }, { once: true }));
    let tick = 0;
    timer = setInterval(() => {
      const level = meter?.level() ?? 0;
      (document.getElementById('sound') as HTMLMeterElement).value = level;
      if (level > config.signalThreshold) recentAudio = performance.now();
      text('audio', performance.now() - recentAudio < config.signalFreshMs ? 'Computer sound detected' : 'No recent sound. Play lesson audio and check the output device.');
      if (state === 'recording') {
        sampler?.tick();
        text('status', `Recording · ${Math.floor(now() / 1000)}s · ${sequence} saved packets`);
        if (performance.now() - Math.max(recentAudio, started) > config.silenceWarningMs) text('audio', 'Warning: no sound detected. Check the lesson audio.');
      }
      update();
      if (++tick % 10 === 0 && !checking && state !== 'stopping') {
        checking = true;
        void (async () => {
          const source = await bridge.call('source');
          if (['idle', 'stopping'].includes(state)) return;
          if (source === 'ended') { await stop('source-ended'); return; }
          if (source === 'changed' && session) {
            text('error', 'The selected window title changed. Confirm it still shows your lesson.');
            await bridge.call('event', { id, type: 'diagnostic', atMs: now(), reason: 'window-title-changed' });
          }
          const reply = await bridge.call('heartbeat');
          if (reply.stopRequested) await stop('desktop-stop-or-low-disk');
        })().catch(error => { if (state !== 'idle' && state !== 'stopping') fail(error); }).finally(() => { checking = false; });
      }
    }, 100);
  } catch (error) { await release(); throw error; }
}
async function start() {
  if (state !== 'preview' || !stream || performance.now() - recentAudio > config.signalFreshMs) throw new Error('Check lesson sound before starting');
  const mime = chooseMimeType(true);
  ({ id } = await bridge.call('create', { consent: input('consent').checked, mime }));
  started = performance.now(); sequence = eventIndex = 0;
  try {
    session = await new BrowserMediaSource(new MediaStream(stream.getAudioTracks()), mime, { now }).start({ signal: new AbortController().signal, timeoutMs: 30000 });
    if (!input('voice').checked) sampler = new SlideSampler(video, crop, bridge, id, now, fail);
    state = 'recording'; pump = save(); update();
  } catch (error) { await release(); throw error; }
}
async function run(action: () => Promise<void>) {
  if (mutation) return; mutation = true; update();
  try { await action(); } catch (error) { text('error', String(error)); }
  finally { mutation = false; update(); }
}
button('refresh').onclick = () => { void run(refresh); };
button('preview').onclick = () => { void run(preview); };
button('reset').onclick = () => { void run(release); };
button('start').onclick = () => { void run(start); };
button('pause').onclick = () => { void run(async () => {
  if (state === 'recording') { session!.pause(); state = 'paused'; await sampler?.pause(); text('status', 'Paused · nothing new is recorded'); }
  else if (state === 'paused') { session!.resume(); state = 'recording'; }
  await flushEvents();
}); };
button('stop').onclick = () => { void run(() => stop()); };
bridge.onStop(() => { void run(() => stop()); });
void run(refresh);
