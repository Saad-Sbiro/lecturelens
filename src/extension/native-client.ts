export class NativeClient {
  // Offscreen documents can message the worker, but cannot open a native host.
  private port = chrome.runtime.connect({ name: 'lecturelens-native' });
  private pending = new Map<string, { resolve(value: any): void; reject(error: Error): void; timer: ReturnType<typeof setTimeout> }>();
  connected = true;
  onDisconnect = () => {};
  constructor() {
    this.port.onMessage.addListener(message => {
      if (message.transportError) { this.fail(message.transportError); return; }
      const task = this.pending.get(message.requestId);
      if (!task) return;
      clearTimeout(task.timer); this.pending.delete(message.requestId);
      if (message.ok) task.resolve(message.value); else task.reject(new Error(message.error));
    });
    this.port.onDisconnect.addListener(() => {
      const reason = chrome.runtime.lastError?.message ?? 'Native connection closed';
      this.fail(reason);
    });
  }
  private fail(reason: string) {
    if (!this.connected) return;
    this.connected = false;
    for (const task of this.pending.values()) { clearTimeout(task.timer); task.reject(new Error(reason)); }
    this.pending.clear(); this.onDisconnect();
  }
  call<T = any>(op: string, data: Record<string, unknown> = {}): Promise<T> {
    if (!this.connected) return Promise.reject(new Error('Native connection closed'));
    return new Promise((resolve, reject) => {
      const requestId = crypto.randomUUID();
      const timer = setTimeout(() => { this.pending.delete(requestId); reject(new Error('Local request timed out')); }, op === 'finish' ? 120000 : op === 'detectRegion' ? 100000 : 15000);
      this.pending.set(requestId, { resolve, reject, timer });
      this.port.postMessage({ requestId, op, ...data });
    });
  }
  close() { this.onDisconnect = () => {}; this.fail('Connection closed'); this.port.disconnect(); }
}
