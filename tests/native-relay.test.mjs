import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { build } from 'vite';

const built = await build({ configFile: false, logLevel: 'silent', build: { write: false, minify: false, lib: { entry: 'src/extension/native-relay.ts', name: 'Relay', formats: ['iife'] } } });
const code = (Array.isArray(built) ? built[0] : built).output.find(item => item.type === 'chunk').code;
function port(sender) {
  const messages = [], disconnects = [];
  return {
    name: 'lecturelens-native', sender, sent: [], closed: false,
    onMessage: { addListener(fn) { messages.push(fn); } },
    onDisconnect: { addListener(fn) { disconnects.push(fn); } },
    postMessage(message) { this.sent.push(message); },
    disconnect() { if (this.closed) return; this.closed = true; disconnects.forEach(fn => fn()); },
    receive(message) { messages.forEach(fn => fn(message)); },
  };
}
function setup() {
  let connect; const host = port(); let opened = 0;
  const chrome = { runtime: { id: 'test', getURL: name => `chrome-extension://test/${name}`,
    onConnect: { addListener(fn) { connect = fn; } },
    connectNative(name) { assert.equal(name, 'local.lecturelens.capture'); opened++; return host; },
  } };
  const context = vm.createContext({ chrome }); vm.runInContext(code, context); context.Relay.registerNativeRelay();
  return { host, chrome, connect: client => connect(client), opened: () => opened };
}
test('worker relays original requests/ACKs and releases native host on offscreen disconnect', () => {
  const h = setup(); const client = port({ id: 'test', url: 'chrome-extension://test/offscreen.html' });
  h.connect(client); assert.equal(h.opened(), 1);
  const request = { requestId: 'packet-1', op: 'append', data: 'AQID' };
  client.receive(request); assert.equal(h.host.sent[0], request);
  const ack = { requestId: 'packet-1', ok: true, value: { sequence: 0 } };
  h.host.receive(ack); assert.equal(client.sent[0], ack);
  client.disconnect(); assert.equal(h.host.closed, true);
  client.receive(request); assert.equal(h.host.sent.length, 1);
});
test('worker rejects other pages and propagates host errors without reconnecting', () => {
  const h = setup(); const invalid = port({ id: 'test', url: 'chrome-extension://test/popup.html' });
  h.connect(invalid); assert.equal(invalid.closed, true); assert.equal(h.opened(), 0);
  const client = port({ id: 'test', url: 'chrome-extension://test/offscreen.html' }); h.connect(client);
  h.chrome.runtime.lastError = { message: 'Host unavailable' }; h.host.disconnect();
  assert.equal(client.sent[0].transportError, 'Host unavailable'); assert.equal(client.closed, true);
  assert.equal(h.opened(), 1);
});
