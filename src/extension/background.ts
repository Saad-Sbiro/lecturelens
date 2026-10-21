import text from '../../config/browser-ui.json';
import { registerNativeRelay } from './native-relay';

registerNativeRelay();

async function offscreen(op: string, data: Record<string, unknown> = {}) {
  if (!await chrome.offscreen.hasDocument()) {
    if (op !== 'preview') return { ok: true, value: { state: 'idle' } };
    await chrome.offscreen.createDocument({ url: 'offscreen.html', reasons: [chrome.offscreen.Reason.USER_MEDIA], justification: 'Capture the explicitly selected lesson tab and preserve its audio playback.' });
  }
  return chrome.runtime.sendMessage({ target: 'offscreen', op, ...data });
}
let choosing = false;
chrome.runtime.onMessage.addListener((message, sender, respond) => {
  if (sender.id !== chrome.runtime.id || message.target !== 'background') return;
  if (message.op === 'badge' && sender.url === chrome.runtime.getURL('offscreen.html')) {
    void chrome.action.setBadgeText({ text: message.text });
    void chrome.action.setBadgeBackgroundColor({ color: '#b91c1c' });
    respond({ ok: true }); return;
  }
  if (sender.url !== chrome.runtime.getURL('popup.html')) return;
  void (async () => {
    if (message.op === 'preview') {
      if (choosing) throw new Error(text.sourceLocked);
      choosing = true;
      try {
        const current = await offscreen('status');
        if (!['idle', 'finished', 'failed'].includes(current.value?.state)) throw new Error(text.sourceLocked);
        if (message.consent !== true) throw new Error(text.consentRequired);
        const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
        if (!tab?.id || tab.id !== message.tabId || !tab.url?.startsWith('https://')) throw new Error(text.unavailable);
        await chrome.storage.session.set({ selectedTab: tab.id });
        // Always pass targetTabId. Tab activation changes never retarget this stream.
        const streamId = await chrome.tabCapture.getMediaStreamId({ targetTabId: tab.id });
        return await offscreen('preview', { streamId, tabId: tab.id, title: tab.title ?? text.selected });
      } finally { choosing = false; }
    }
    if (['status', 'start', 'pause', 'stop'].includes(message.op)) return offscreen(message.op);
    throw new Error('Unsupported operation');
  })().then(respond, error => respond({ ok: false, error: String(error.message ?? error) }));
  return true;
});
async function stopSelected(tabId: number) {
  const { selectedTab } = await chrome.storage.session.get('selectedTab');
  if (selectedTab === tabId) await offscreen('stop', { reason: 'source-navigation-or-close' });
}
chrome.webNavigation.onBeforeNavigate.addListener(e => { if (e.frameId === 0) void stopSelected(e.tabId); });
chrome.tabs.onRemoved.addListener(id => { void stopSelected(id); });
chrome.tabs.onReplaced.addListener((_, removed) => { void stopSelected(removed); });
