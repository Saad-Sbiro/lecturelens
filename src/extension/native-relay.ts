// Only the service worker owns the native port. Disconnects must release the
// host's recording lock rather than silently creating a replacement session.
export function registerNativeRelay() {
  chrome.runtime.onConnect.addListener(client => {
    if (client.name !== 'lecturelens-native' || client.sender?.id !== chrome.runtime.id || client.sender?.url !== chrome.runtime.getURL('offscreen.html')) {
      client.disconnect(); return;
    }
    let host: chrome.runtime.Port;
    try { host = chrome.runtime.connectNative('local.lecturelens.capture'); }
    catch (error) {
      client.postMessage({ transportError: error instanceof Error ? error.message : String(error) });
      client.disconnect(); return;
    }
    let closed = false;
    const close = () => {
      if (closed) return;
      closed = true; host.disconnect(); client.disconnect();
    };
    client.onMessage.addListener(message => {
      if (closed) return;
      try { host.postMessage(message); } catch { close(); }
    });
    host.onMessage.addListener(message => {
      if (closed) return;
      try { client.postMessage(message); } catch { close(); }
    });
    client.onDisconnect.addListener(() => { void chrome.runtime.lastError; close(); });
    host.onDisconnect.addListener(() => {
      const reason = chrome.runtime.lastError?.message ?? 'Native connection closed';
      if (!closed) {
        try { client.postMessage({ transportError: reason }); } catch { /* The client may have closed first. */ }
      }
      close();
    });
  });
}
