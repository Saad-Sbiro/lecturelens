import { BrowserMediaSource, chooseMimeType } from '../adapters/capture/media-recorder';
import { SoundMeter } from '../adapters/capture/sound-meter';
import type { CaptureSession } from '../ports/capture';
import { NativeClient } from './native-client';
import text from '../../config/browser-ui.json';

const video = document.querySelector('video')!;
let state = 'idle', error = '', id = '', sourceTitle = '', packets = 0, level = 0, recentAudio = -Infinity;
let stream: MediaStream | undefined, native: NativeClient | undefined, session: CaptureSession | undefined;
let meter: SoundMeter | undefined, listening: AudioContext | undefined;
let timer: ReturnType<typeof setInterval> | undefined, heartbeatBusy = false;
let ending: Promise<void> | undefined, pump: Promise<void> | undefined;
let started = 0, eventIndex = 0;
const badge = (value: string) => { void chrome.runtime.sendMessage({ target: 'background', op: 'badge', text: value }).catch(() => {}); };
async function release() {
  clearInterval(timer); timer = undefined;
  stream?.getTracks().forEach(track => track.stop()); stream = undefined; video.srcObject = null;
  await meter?.close(); meter = undefined;
  await listening?.close(); listening = undefined;
  native?.close(); native = undefined; session = undefined; badge('');
}
async function flushEvents() {
  while (session && eventIndex < session.events.length) {
    const event = session.events[eventIndex++];
    await native!.call('event', { id, ...event });
  }
}
async function save() {
  try {
    for await (const chunk of session!.media) {
      await flushEvents();
      // Native messages stay bounded; ACK arrives only after the packet is flushed to disk.
      for (let offset = 0; offset < chunk.bytes.length; offset += 262144) {
        const bytes = chunk.bytes.subarray(offset, offset + 262144);
        let binary = '';
        for (let i = 0; i < bytes.length; i += 8192) binary += String.fromCharCode(...bytes.subarray(i, i + 8192));
        await native!.call('append', { id, sequence: packets, atMs: chunk.atMs, data: btoa(binary) }); packets++;
      }
    }
    await flushEvents();
    await native!.call('finish', { id, reason: error ? 'capture-error' : (session?.events.at(-1)?.reason ?? 'user-stop') });
    state = error ? 'failed' : 'finished';
  } catch (e) {
    error = e instanceof Error ? e.message : String(e); state = 'failed';
    // Disconnect releases the native lock; the desktop can recover acknowledged packets.
    void session?.stop('capture-error');
  } finally { await release(); }
}
async function stop(reason = 'user-stop') {
  if (ending) return ending;
  ending = (async () => {
    if (session) { state = 'stopping'; badge('…'); await session.stop(reason); await pump; }
    else { await release(); state = error ? 'failed' : 'idle'; }
  })();
  try { await ending; } finally { ending = undefined; }
}
async function preview(streamId: string) {
  if (!['idle', 'finished', 'failed'].includes(state)) throw new Error(text.sourceLocked);
  state = 'loading'; error = ''; id = ''; packets = 0; recentAudio = -Infinity;
  try {
    native = new NativeClient(); await native.call('hello');
    native.onDisconnect = () => { error = text.connectFailed; void stop('native-disconnected'); };
    const mandatory = { chromeMediaSource: 'tab', chromeMediaSourceId: streamId };
    stream = await navigator.mediaDevices.getUserMedia({
      // Chrome's tab-stream selector uses legacy constraints. Mixing standard
      // audio fields beside `mandatory` causes Chrome to reject the whole request.
      audio: { mandatory },
      video: { mandatory: { ...mandatory, maxWidth: 1280, maxHeight: 720, maxFrameRate: 10 } },
    } as unknown as MediaStreamConstraints);
    video.srcObject = stream; await video.play();
    listening = new AudioContext(); listening.createMediaStreamSource(stream).connect(listening.destination); await listening.resume();
    meter = new SoundMeter(stream); await meter.start();
    stream.getTracks().forEach(track => track.addEventListener('ended', () => { void stop('source-ended'); }, { once: true }));
    state = 'previewing'; badge('VIEW');
    let tick = 0;
    timer = setInterval(() => {
      level = meter?.level() ?? 0;
      if (level > 0.002) recentAudio = performance.now();
      if (++tick % 10 === 0 && !heartbeatBusy && native?.connected && state !== 'stopping') {
        heartbeatBusy = true;
        void native.call('heartbeat').then(reply => { if (reply.stopRequested) void stop('desktop-stop-or-low-disk'); })
          .catch(e => { error = String(e.message); void stop('storage-failed'); }).finally(() => { heartbeatBusy = false; });
      }
    }, 100);
  } catch (e) { await release(); state = 'failed'; error = e instanceof Error ? e.message : String(e); throw e; }
}
async function command(message: { op: string; streamId?: string; reason?: string; title?: string }) {
  if (message.op === 'status') {
    let image: string | undefined;
    if (stream && video.readyState >= 2) {
      const canvas = document.createElement('canvas'); canvas.width = 320; canvas.height = 180;
      canvas.getContext('2d')!.drawImage(video, 0, 0, 320, 180); image = canvas.toDataURL('image/jpeg', 0.5);
    }
    return { state, error, sourceTitle, packets, level, image, elapsedMs: started ? performance.now() - started : 0 };
  }
  if (message.op === 'preview') { sourceTitle = message.title ?? text.selected; return preview(message.streamId!); }
  if (message.op === 'start') {
    if (state !== 'previewing') throw new Error(text.sourceLocked);
    if (performance.now() - recentAudio > 5000) throw new Error(text.noSignal);
    if (video.readyState < 2) throw new Error(text.noVideo);
    const mime = chooseMimeType();
    id = (await native!.call('create', { consent: true, mime })).id;
    started = performance.now(); eventIndex = 0;
    try {
      session = await new BrowserMediaSource(stream!, mime, { now: () => performance.now() - started }).start({ signal: new AbortController().signal, timeoutMs: 15000 });
      state = 'recording'; badge('REC'); pump = save();
    } catch (e) { error = String(e); await release(); state = 'failed'; throw e; }
    return;
  }
  if (message.op === 'pause') {
    if (state === 'recording') { session!.pause(); state = 'paused'; badge('PAUSE'); }
    else if (state === 'paused') { session!.resume(); state = 'recording'; badge('REC'); }
    return;
  }
  if (message.op === 'stop') return stop(message.reason);
  throw new Error('Unsupported operation');
}
// Mutations serialize; status remains responsive while saving. Never obtain a second stream.
let commands = Promise.resolve<unknown>(undefined);
chrome.runtime.onMessage.addListener((message, sender, respond) => {
  if (sender.id !== chrome.runtime.id || message.target !== 'offscreen') return;
  const result = message.op === 'status' ? command(message) : (commands = commands.catch(() => {}).then(() => command(message)));
  void result.then(value => respond({ ok: true, value }), e => respond({ ok: false, error: String(e.message ?? e) }));
  return true;
});
