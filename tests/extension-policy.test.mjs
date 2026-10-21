import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { build } from 'vite';

const bundle = await build({ configFile: false, logLevel: 'silent', build: { write: false, minify: false, lib: { entry: 'src/extension/background.ts', name: 'PolicyProbe', formats: ['iife'] } } });
const code = (Array.isArray(bundle) ? bundle[0] : bundle).output.find(item => item.type === 'chunk').code;
function harness() {
  const listeners = {}, captures = [], messages = [];
  let tab = { id: 7, url: 'https://class.example.test', title: 'Synthetic lesson' }, state = 'idle', selectedTab, hasDocument = false;
  const event = name => ({ addListener(fn) { listeners[name] = fn; } });
  const chrome = {
    runtime: { id: 'test', getURL: file => `chrome-extension://test/${file}`, onConnect: event('connect'), onMessage: event('message'), async sendMessage(message) {
      messages.push(message); if (message.op === 'preview') state = 'previewing'; if (message.op === 'stop') state = 'idle';
      return { ok: true, value: { state } };
    } },
    offscreen: { hasDocument: async () => hasDocument, createDocument: async () => { hasDocument = true; }, Reason: { USER_MEDIA: 'USER_MEDIA' } },
    tabs: { query: async () => [tab], onRemoved: event('removed'), onReplaced: event('replaced') },
    tabCapture: { getMediaStreamId: async value => { captures.push(value); return 'fixed-stream'; } },
    storage: { session: { set: async value => { selectedTab = value.selectedTab; }, get: async () => ({ selectedTab }) } },
    webNavigation: { onBeforeNavigate: event('navigation') },
    action: { setBadgeText: async () => {}, setBadgeBackgroundColor: async () => {} },
  };
  vm.runInNewContext(code, { chrome, console });
  return { listeners, captures, messages, setTab(value) { tab = value; }, send(op, extra = {}) {
    return new Promise(resolve => listeners.message({ target: 'background', op, ...extra }, { id: 'test', url: chrome.runtime.getURL('popup.html') }, resolve));
  } };
}
test('tab selection stays fixed when another tab becomes active; only selected main-frame navigation stops', async () => {
  const h = harness();
  assert.equal((await h.send('preview', { consent: true, tabId: 7 })).ok, true);
  assert.equal(h.captures.length, 1); assert.equal(h.captures[0].targetTabId, 7);
  h.setTab({ id: 9, url: 'https://private.example.test' });
  await h.send('status'); assert.equal(h.captures.length, 1);
  h.listeners.navigation({ frameId: 0, tabId: 9 }); h.listeners.navigation({ frameId: 1, tabId: 7 });
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(h.messages.some(m => m.op === 'stop'), false);
  h.listeners.navigation({ frameId: 0, tabId: 7 }); await new Promise(resolve => setImmediate(resolve));
  assert.equal(h.messages.at(-1).op, 'stop');
});
test('consent, explicit tab match, internal sender and single-source lock are enforced', async () => {
  const h = harness();
  assert.equal((await h.send('preview', { consent: false, tabId: 7 })).ok, false);
  assert.equal((await h.send('preview', { consent: true, tabId: 8 })).ok, false);
  assert.equal(h.captures.length, 0);
  let responded = false;
  h.listeners.message({ target: 'background', op: 'preview', consent: true, tabId: 7 }, { id: 'external', url: 'https://other.test' }, () => { responded = true; });
  assert.equal(responded, false); assert.equal(h.captures.length, 0);
  await h.send('preview', { consent: true, tabId: 7 });
  assert.equal((await h.send('preview', { consent: true, tabId: 7 })).ok, false);
  assert.equal(h.captures.length, 1);
});
