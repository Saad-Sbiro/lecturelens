const { spawn } = require('node:child_process');

// The same framed, acknowledged storage protocol used by the browser extension.
class StorageHost {
  constructor(executable, args = ['--desktop-capture']) {
    this.child = spawn(executable, args, { windowsHide: true, stdio: ['pipe', 'pipe', 'ignore'] });
    this.pending = new Map(); this.serial = 0; this.buffer = Buffer.alloc(0); this.closed = false;
    this.child.stdout.on('data', bytes => {
      this.buffer = Buffer.concat([this.buffer, bytes]);
      while (this.buffer.length >= 4) {
        const size = this.buffer.readUInt32LE();
        if (size > 1048576) return this.fail(new Error('Invalid storage reply'));
        if (this.buffer.length < size + 4) break;
        try {
          const reply = JSON.parse(this.buffer.subarray(4, size + 4));
          this.buffer = this.buffer.subarray(size + 4);
          const task = this.pending.get(reply.requestId);
          if (task) { clearTimeout(task.timer); this.pending.delete(reply.requestId); reply.ok ? task.resolve(reply.value) : task.reject(new Error(reply.error)); }
        } catch (error) { this.fail(error); }
      }
    });
    this.child.on('error', error => this.fail(error));
    this.child.on('exit', () => this.fail(new Error('Storage disconnected; saved packets can be recovered')));
    this.child.stdin.on('error', error => this.fail(error));
  }
  fail(error) {
    this.closed = true;
    for (const task of this.pending.values()) { clearTimeout(task.timer); task.reject(error); }
    this.pending.clear(); this.child.kill();
  }
  call(op, data = {}) {
    if (this.closed) return Promise.reject(new Error('Storage connection closed'));
    if (this.pending.size >= 32) return Promise.reject(new Error('Storage queue full'));
    return new Promise((resolve, reject) => {
      const requestId = String(++this.serial);
      const payload = Buffer.from(JSON.stringify({ ...data, op, requestId }));
      if (payload.length > 1048576) { reject(new Error('Storage packet too large')); return; }
      const timer = setTimeout(() => this.fail(new Error('Storage timed out; saved packets preserved')), op === 'finish' ? 120000 : 15000);
      this.pending.set(requestId, { resolve, reject, timer });
      const header = Buffer.alloc(4); header.writeUInt32LE(payload.length);
      this.child.stdin.write(Buffer.concat([header, payload]));
    });
  }
  close() { this.fail(new Error('Storage connection closed')); }
}
module.exports = { StorageHost };
