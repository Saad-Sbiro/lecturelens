import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { webcrypto } from 'node:crypto';
import { build } from 'vite';

const built = await build({ configFile: false, logLevel: 'silent', build: { write: false, minify: false, lib: { entry: 'src/extension/offscreen.ts', name: 'RecordingProbe', formats: ['iife'] } } });
const code = (Array.isArray(built) ? built[0] : built).output.find(item => item.type === 'chunk').code;

test('offscreen orchestration: preview does not persist; recording drains packets before finish and releases tracks', { timeout: 10000 }, async () => {
  const calls = [], constraints = [], draws = []; let receiver, connectionCount = 0;
  const tracks = ['video', 'audio'].map(kind => ({ kind, readyState: 'live', addEventListener() {}, removeEventListener() {}, stop() { this.readyState = 'ended'; } }));
  const stream = { getTracks: () => tracks, getVideoTracks: () => [tracks[0]], getAudioTracks: () => [tracks[1]] };
  const video = { readyState: 2, videoWidth: 320, videoHeight: 180, srcObject: null, play: async () => {} };
  function makeCanvas() {
    const canvas = { width: 320, height: 180, toDataURL: () => 'data:image/jpeg;base64,/9j/2Q==', getContext: () => ({ drawImage(...args) { draws.push(args); }, getImageData: () => ({ data: Uint8ClampedArray.from({ length: canvas.width * canvas.height * 4 }, (_, i) => Math.floor(i / 4) % 20 < 10 ? 200 : 40) }) }) };
    return canvas;
  }
  class Audio {
    async resume() {} async close() {}
    createMediaStreamSource() { return { connect() {} }; }
    createAnalyser() { return { fftSize: 2048, getFloatTimeDomainData(samples) { samples.fill(0.05); } }; }
  }
  class Recorder {
    static isTypeSupported() { return true; }
    state = 'inactive';
    start() { this.state = 'recording'; }
    requestData() { this.ondataavailable?.({ data: new Blob([new Uint8Array(300000).fill(42)]) }); }
    pause() { this.state = 'paused'; }
    resume() { this.state = 'recording'; }
    stop() { this.requestData(); this.state = 'inactive'; queueMicrotask(() => this.onstop?.()); }
  }
  const chrome = { runtime: {
    id: 'test', onMessage: { addListener(fn) { receiver = fn; } }, sendMessage: async () => {},
    // Deliberately no connectNative: Chrome does not expose it offscreen.
    connect({ name }) { assert.equal(name, 'lecturelens-native'); let nativeReply; const connection = ++connectionCount; return {
      onMessage: { addListener(fn) { nativeReply = fn; } }, onDisconnect: { addListener() {} }, disconnect() {},
      postMessage(request) {
        calls.push({ ...request, connection });
        if (request.op === 'detectRegion') return; // Cloud can hang without blocking storage.
        const value = request.op === 'create' ? { id: 'synthetic' } : request.op === 'heartbeat' ? { stopRequested: false } : true;
        // A delayed ACK makes finish ordering observable.
        setTimeout(() => nativeReply({ requestId: request.requestId, ok: true, value }), 5);
      },
    }; },
  } };
  vm.runInNewContext(code, {
    chrome, document: { querySelector: () => video, createElement: makeCanvas },
    navigator: { mediaDevices: { getUserMedia: async value => {
      for (const track of [value.audio, value.video]) {
        if ('mandatory' in track || 'optional' in track) {
          assert.ok(Object.keys(track).every(key => key === 'mandatory' || key === 'optional'),
            'Chrome rejects mixing legacy mandatory/optional with standard constraints');
        }
      }
      constraints.push(value); return stream;
    } } },
    AudioContext: Audio, MediaRecorder: Recorder, Blob, Uint8Array, Float32Array, AbortController,
    MediaStream: class { constructor(tracks) { this.tracks = tracks; } getTracks() { return this.tracks; } getAudioTracks() { return this.tracks; } getVideoTracks() { return []; } },
    setTimeout, clearTimeout, setInterval, clearInterval, queueMicrotask, performance, crypto: webcrypto,
    btoa: value => Buffer.from(value, 'binary').toString('base64'), console,
  });
  const send = (op, extra = {}) => new Promise(resolve => receiver({ target: 'offscreen', op, ...extra }, { id: 'test' }, resolve));
  try {
    assert.equal((await send('preview', { streamId: 'one-tab-only' })).ok, true);
    assert.equal(calls.some(call => call.op === 'create'), false);
    assert.equal(constraints.length, 1);
    assert.equal(constraints[0].audio.mandatory.chromeMediaSourceId, 'one-tab-only');
    assert.equal(constraints[0].video.mandatory.chromeMediaSourceId, 'one-tab-only');
    assert.equal(constraints[0].video.mandatory.maxFrameRate, 1);
    const preview = (await send('status')).value;
    assert.equal(preview.cropMode, 'manual');
    assert.equal(preview.cropConfirmed, true, 'full-frame safety fallback permits recording while vision is pending');
    video.videoWidth = 1600; video.videoHeight = 1200;
    await send('status', { largePreview: true });
    assert.deepEqual(draws.at(-1).slice(1), [0, 0, 1600, 1200], 'editor preserves source aspect and resolution');
    await send('status');
    assert.deepEqual(draws.at(-1).slice(1), [0, 0, 480, 360], 'compact preview also preserves aspect');
    video.videoWidth = 320; video.videoHeight = 180;
    await new Promise(resolve => setTimeout(resolve, 3200));
    assert.equal((await send('crop', { crop: { x: 0.2, y: 0.2, width: 0.5, height: 0.5 } })).ok, true);
    await new Promise(resolve => setTimeout(resolve, 150));
    assert.equal((await send('start')).ok, true);
    assert.notEqual(calls.find(call => call.op === 'detectRegion').connection, calls.find(call => call.op === 'create').connection, 'cloud requests cannot block the storage native host');
    await send('pause'); await send('pause'); await send('stop');
    assert.equal((await send('status')).value.state, 'finished');
    const packets = calls.filter(call => call.op === 'append');
    assert.ok(packets.length >= 4);
    packets.forEach((packet, index) => { assert.equal(packet.sequence, index); assert.ok(Buffer.from(packet.data, 'base64').length <= 262144); });
    assert.equal(calls.at(-1).op, 'finish');
    assert.equal(calls.find(call => call.op === 'create').mime, 'audio/webm;codecs=opus');
    assert.ok(calls.some(call => call.op === 'slide'));
    assert.ok(draws.some(args => args.length === 9 && args[1] === 64 && args[2] === 36 && args[3] === 160 && args[4] === 90), 'comparison and saved images use the selected crop');
    assert.deepEqual(calls.filter(call => call.op === 'event').map(call => call.type), ['started', 'paused', 'resumed', 'ended']);
    assert.ok(tracks.every(track => track.readyState === 'ended'));
  } finally { await send('stop'); }
});
