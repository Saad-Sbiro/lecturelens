import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { createRequire } from 'node:module';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { build } from 'vite';
const { StorageHost } = createRequire(import.meta.url)('../src/desktop-capture/host.cjs');
const built = await build({ configFile: false, logLevel: 'silent', build: { write: false, minify: false, lib: { entry: 'src/desktop-capture/renderer.ts', name: 'CaptureProbe', formats: ['iife'] } } });
const code = (Array.isArray(built) ? built[0] : built).output.find(item => item.type === 'chunk').code;
const settle = () => new Promise(resolve => setTimeout(resolve, 15));

test('desktop permission boundary: trusted main frame, explicit selection, one-use grant, no monitor fallback', async () => {
  let handler, grant, window, windows = [{ id: 'window:42', name: 'Lesson' }];
  const session = { setPermissionRequestHandler() {}, setPermissionCheckHandler() {}, setDisplayMediaRequestHandler(fn) { grant = fn; } };
  class Window {
    constructor() { window = this; this.webContents = { mainFrame: {}, session, setWindowOpenHandler() {}, on() {} }; }
    getTitle() { return 'LectureLens'; } on() {} async loadFile() {}
  }
  const electron = { app: { setName() {}, requestSingleInstanceLock: () => true, on() {}, whenReady: () => Promise.resolve(), exit() { assert.fail('Unexpected app exit'); } },
    BrowserWindow: Window, desktopCapturer: { async getSources(options) { assert.deepEqual([...options.types], ['window']); return windows; } },
    ipcMain: { handle(name, fn) { handler = fn; } }, dialog: {} };
  vm.runInNewContext(await readFile('src/desktop-capture/main.cjs', 'utf8'), {
    require: name => name === 'electron' ? electron : name === './host.cjs' ? { StorageHost: class {} } : createRequire(import.meta.url)(name),
    __dirname: resolve('src/desktop-capture'), process: { argv: [], env: {} }, console,
  });
  await settle();
  const event = { sender: window.webContents, senderFrame: window.webContents.mainFrame };
  await assert.rejects(handler({ sender: {} }, 'list'), /Untrusted/);
  await assert.rejects(handler(event, 'create', { consent: true }), /Preview/);
  await assert.rejects(handler(event, 'select', { id: 'window:42', consent: false }), /consent/);
  await assert.rejects(handler(event, 'select', { id: 'screen:0', consent: true }), /available/);
  const request = { frame: event.senderFrame, userGesture: true };
  let result;
  await grant(request, value => { result = value; }); assert.equal(result, null);
  await handler(event, 'select', { id: 'window:42', consent: true });
  await grant({ ...request, frame: {} }, value => { result = value; }); assert.equal(result, null);
  await handler(event, 'select', { id: 'window:42', consent: true });
  windows = []; await grant(request, value => { result = value; }); assert.equal(result, null, 'closed window never falls back to another source');
  windows = [{ id: 'window:42', name: 'Lesson' }];
  await handler(event, 'select', { id: 'window:42', consent: true });
  await grant(request, value => { result = value; }); assert.equal(result.video.id, 'window:42'); assert.equal(result.audio, 'loopback');
  await grant(request, value => { result = value; }); assert.equal(result, null, 'grant is one-use');
  await assert.rejects(handler(event, 'select', { id: 'window:42', consent: true }), /Stop/);
});

function harness({ audio = true, storageFails = false } = {}) {
  const elements = new Map(), calls = [], intervals = new Set(); let signal = 0, time = 1000, source = 'same', stopRequested = false;
  function element(id) {
    if (!elements.has(id)) elements.set(id, { id, checked: id === 'full', value: '', disabled: false, style: {}, textContent: '',
      replaceChildren(...items) { this.value = items[0]?.value ?? ''; }, setPointerCapture() {}, getBoundingClientRect: () => ({ left: 0, top: 0, width: 320, height: 180 }),
      getContext: () => ({ strokeRect() {}, drawImage() {}, getImageData: () => ({ data: Uint8ClampedArray.from({ length: 320 * 180 * 4 }, (_, i) => i % 200 < 100 ? 200 : 40) }) }), toDataURL: () => 'data:image/jpeg;base64,/9j/2Q==' });
    return elements.get(id);
  }
  const tracks = ['video', 'audio'].map(kind => ({ kind, readyState: 'live', addEventListener() {}, removeEventListener() {}, stop() { this.readyState = 'ended'; } }));
  const stream = { getTracks: () => tracks, getAudioTracks: () => audio ? [tracks[1]] : [], getVideoTracks: () => [tracks[0]] };
  const video = Object.assign(element('video'), { readyState: 2, videoWidth: 320, videoHeight: 180, clientWidth: 320, clientHeight: 180, play: async () => {} });
  class Audio {
    async resume() {} async close() {}
    createMediaStreamSource() { return { connect() {} }; }
    createAnalyser() { return { fftSize: 2048, getFloatTimeDomainData(samples) { samples.fill(signal); } }; }
  }
  class Recorder {
    static isTypeSupported() { return true; } state = 'inactive';
    start() { this.state = 'recording'; }
    requestData() { this.ondataavailable?.({ data: new Blob([new Uint8Array(300000).fill(42)]) }); }
    pause() { this.state = 'paused'; } resume() { this.state = 'recording'; }
    stop() { this.requestData(); this.state = 'inactive'; queueMicrotask(() => this.onstop?.()); }
  }
  const bridge = { onStop() {}, async call(op, data) {
    calls.push({ op, ...data });
    if (op === 'list') return [{ id: 'window:42', name: 'Synthetic lesson' }];
    if (op === 'create') return { id: 'synthetic' };
    if (op === 'source') return source;
    if (op === 'heartbeat') return { stopRequested };
    if (op === 'append') { await settle(); if (storageFails) throw new Error('Synthetic disk failure'); }
    return true;
  } };
  vm.runInNewContext(code, { window: { captureBridge: bridge }, document: { getElementById: element, querySelector: selector => selector === 'video' ? video : element(selector), createElement: () => element('sample') },
    ResizeObserver: class { observe() {} }, Option: class { constructor(name, value) { this.value = value; } },
    navigator: { mediaDevices: { getDisplayMedia: async () => stream } }, AudioContext: Audio, MediaRecorder: Recorder,
    MediaStream: class { constructor(tracks) { this.tracks = tracks; } getTracks() { return this.tracks; } getAudioTracks() { return this.tracks; } getVideoTracks() { return []; } },
    Blob, AbortController, Uint8Array, Float32Array, performance: { now: () => time }, setTimeout, clearTimeout, queueMicrotask,
    setInterval: fn => { intervals.add(fn); return fn; }, clearInterval: fn => intervals.delete(fn),
    btoa: value => Buffer.from(value, 'binary').toString('base64'), console,
  });
  return { calls, tracks, element, async click(id) { element(id).onclick(); await settle(); },
    async tick(count = 1) { for (let i = 0; i < count; i++) { time += 100; for (const fn of intervals) fn(); await settle(); } },
    sound(value) { signal = value; }, source(value) { source = value; }, stopRequested() { stopRequested = true; } };
}

test('desktop preview needs consent and sound; pause and stop drain durable audio before finish', async () => {
  const h = harness(); await settle();
  assert.equal(h.element('preview').disabled, true);
  h.element('consent').checked = true; h.element('consent').onchange();
  await h.click('preview');
  assert.equal(h.calls.some(c => c.op === 'create'), false);
  assert.equal(h.element('start').disabled, true);
  await h.click('start'); assert.equal(h.calls.some(c => c.op === 'create'), false);
  h.sound(.05); await h.tick(); assert.equal(h.element('start').disabled, false);
  h.element('voice').checked = true;
  await h.click('start'); await h.click('pause'); await h.click('pause'); await h.click('stop'); await settle(); await settle();
  const packets = h.calls.filter(c => c.op === 'append');
  assert.deepEqual(packets.map(p => p.sequence), [0, 1, 2, 3]);
  assert.ok(h.calls.findIndex(c => c.op === 'finish') > h.calls.findLastIndex(c => c.op === 'append'));
  assert.ok(h.calls.some(c => c.op === 'event' && c.type === 'paused'));
  assert.ok(h.calls.some(c => c.op === 'event' && c.type === 'resumed'));
  assert.ok(h.tracks.every(t => t.readyState === 'ended'));
});
test('desktop preview without an audio track releases capture and cannot start', async () => {
  const h = harness({ audio: false }); await settle(); h.element('consent').checked = true; await h.click('preview');
  assert.match(h.element('error').textContent, /No computer sound/);
  assert.ok(h.tracks.every(t => t.readyState === 'ended'));
  assert.equal(h.calls.some(c => c.op === 'create'), false);
});
test('desktop slide crop is logged, pause stops sampling, and source title changes are diagnostic', async () => {
  const h = harness(); await settle(); h.element('consent').checked = true; await h.click('preview');
  h.element('crop').onpointerdown({ clientX: 32, clientY: 18, pointerId: 1 });
  h.element('crop').onpointerup({ clientX: 288, clientY: 162 });
  h.sound(.05); await h.tick(); await h.click('start'); await h.tick();
  const visual = h.calls.find(c => c.op === 'visualEvent');
  assert.ok(visual); assert.equal(visual.crop.x, .1); assert.equal(visual.crop.width, .8);
  await h.click('pause'); const before = h.calls.filter(c => c.op === 'visualEvent').length;
  await h.tick(35); assert.equal(h.calls.filter(c => c.op === 'visualEvent').length, before);
  h.source('changed'); await h.tick(10);
  assert.ok(h.calls.some(c => c.op === 'event' && c.reason === 'window-title-changed'));
  await h.click('stop'); await settle(); await settle();
  assert.ok(h.calls.some(c => c.op === 'finish'));
});
for (const cause of ['source', 'desktop-stop', 'storage']) test(`desktop ${cause} ends capture and preserves recovery semantics`, async () => {
  const h = harness({ storageFails: cause === 'storage' }); await settle(); h.element('consent').checked = true;
  await h.click('preview'); h.sound(.05); await h.tick(); h.element('voice').checked = true; await h.click('start');
  if (cause === 'source') h.source('ended');
  if (cause === 'desktop-stop') h.stopRequested();
  if (cause === 'storage') await h.click('stop'); else await h.tick(10);
  await settle(); await settle(); await settle();
  assert.ok(h.tracks.every(t => t.readyState === 'ended'));
  if (cause === 'storage') { assert.equal(h.calls.some(c => c.op === 'finish'), false); assert.match(h.element('status').textContent, /Recover/); }
  else assert.equal(h.calls.filter(c => c.op === 'finish').length, 1);
});

test('desktop storage bridge: actual host source metadata, crash recovery, shared lock and packet integrity', async () => {
  const root = await mkdtemp(join(tmpdir(), 'lecturelens-desktop-'));
  const previous = process.env.LECTURELENS_DATA_ROOT;
  process.env.LECTURELENS_DATA_ROOT = root;
  const host = new StorageHost(resolve('dist/native-host/LectureLens.NativeHost.exe'));
  const other = new StorageHost(resolve('dist/native-host/LectureLens.NativeHost.exe'));
  if (previous === undefined) delete process.env.LECTURELENS_DATA_ROOT; else process.env.LECTURELENS_DATA_ROOT = previous;
  let recovery;
  try {
    await assert.rejects(host.call('create', { consent: false, mime: 'audio/webm;codecs=opus' }));
    const { id } = await host.call('create', { consent: true, mime: 'audio/webm;codecs=opus' });
    await assert.rejects(other.call('create', { consent: true, mime: 'audio/webm;codecs=opus' }));
    await host.call('append', { id, sequence: 0, atMs: 10, data: 'AQID' });
    assert.equal(JSON.parse(await readFile(join(root, 'recordings', id, 'manifest.json'))).source, 'desktop-window');
    host.close(); other.close(); await settle();
    recovery = new StorageHost(resolve('dist/native-host/LectureLens.NativeHost.exe'), ['--test-root', join(root, 'recordings')]);
    assert.equal((await recovery.call('list'))[0].state, 'interrupted');
    const file = await recovery.call('recover', { id });
    assert.deepEqual(await readFile(file), Buffer.from([1, 2, 3]));
  } finally { host.close(); other.close(); recovery?.close(); await settle(); await rm(root, { recursive: true, force: true }); }
});
