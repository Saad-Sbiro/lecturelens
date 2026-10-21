import type { DesktopBridge } from '../ports/desktop';
import type { CaptureSession, Clock } from '../ports/capture';
import { BrowserMediaSource, chooseMimeType, requestPreview } from '../adapters/capture/media-recorder';
import { SoundMeter } from '../adapters/capture/sound-meter';
import { captureConfig } from '../../config/capture';
import { element, text } from './view';

declare global { interface Window { desktop: DesktopBridge } }

export function connectController(): void {
  const bridge = window.desktop;
  const video = element<HTMLVideoElement>('video');
  const source = element<HTMLSelectElement>('source');
  let stream: MediaStream | undefined;
  let meter: SoundMeter | undefined;
  let session: CaptureSession | undefined;
  let sessionId: string | undefined;
  let clock: Clock | undefined;
  let lastSignal = -Infinity;
  let previewAt = 0;
  let paused = false;
  let busy = false;
  let stopping = false;
  let chunks = 0;
  let bytes = 0;
  let emittedEvents = 0;
  let endedAt = 0;
  let pump = Promise.resolve();
  const controller = new AbortController();
  const status = (message: string) => { element('status').textContent = message; element('status').dataset.active = String(Boolean(session)); };
  const showError = (error: unknown) => { element('error').textContent = `${text.error}: ${error instanceof Error ? error.message : String(error)}`; };
  const controls = () => {
    const active = Boolean(session);
    element<HTMLButtonElement>('start').disabled = busy || !stream || active || performance.now() - lastSignal > captureConfig.signalFreshMs || video.readyState < 2;
    element<HTMLButtonElement>('preview').disabled = busy || active || !source.value || !element<HTMLInputElement>('consent').checked;
    element<HTMLButtonElement>('stop').disabled = busy || !active || stopping;
    element<HTMLButtonElement>('pause').disabled = busy || !active || stopping;
    element<HTMLButtonElement>('close-preview').disabled = busy || !stream || active;
    element<HTMLButtonElement>('refresh').disabled = busy || active || Boolean(stream);
    source.disabled = busy || active || Boolean(stream);
    element<HTMLInputElement>('consent').disabled = busy || Boolean(stream);
  };
  async function closePreview(): Promise<void> {
    stream?.getTracks().forEach(track => track.stop());
    stream = undefined;
    await meter?.close(); meter = undefined;
    video.srcObject = null; lastSignal = -Infinity;
    controls();
  }
  async function listSessions(): Promise<void> {
    const sessions = await bridge.listSessions();
    const container = element('sessions'); container.replaceChildren();
    if (!sessions.length) container.textContent = text.empty;
    for (const saved of sessions) {
      const row = document.createElement('div'); row.className = 'session';
      const description = document.createElement('p');
      description.textContent = `${saved.createdAt} · ${saved.state} · ${saved.chunks} · ${(saved.bytes / 1e6).toFixed(1)} MB ${saved.problems.join(', ')}`;
      row.append(description);
      for (const [label, action] of [[text.recover, async () => { await bridge.recover(saved.id); status(text.exported); }], [text.delete, async () => { await bridge.deleteSession(saved.id); await listSessions(); }]] as const) {
        const button = document.createElement('button'); button.textContent = label; button.disabled = saved.id === sessionId;
        button.onclick = () => { void action().catch(showError); }; row.append(button);
      }
      container.append(row);
    }
  }
  async function refresh(): Promise<void> {
    const choices = await bridge.listWindows();
    source.replaceChildren(...choices.map(choice => { const option = document.createElement('option'); option.value = choice.id; option.textContent = choice.name; return option; }));
  }
  async function flushEvents(): Promise<void> {
    if (!session || !sessionId) return;
    while (emittedEvents < session.events.length) {
      const event = session.events[emittedEvents++];
      await bridge.event(sessionId, event.type, event.atMs, event.reason);
    }
  }
  async function preview(): Promise<void> {
    await closePreview();
    await bridge.selectSource(source.value, element<HTMLInputElement>('consent').checked);
    try {
      stream = await requestPreview(controller.signal, 20_000);
      video.srcObject = stream; await video.play();
      if (!stream.getAudioTracks().length) throw new Error(text.silent);
      meter = new SoundMeter(stream); await meter.start();
      previewAt = performance.now();
      stream.getTracks().forEach(track => track.addEventListener('ended', () => { if (session) void stop('source-ended').catch(showError); else void closePreview().then(() => status(text.idle)); }, { once: true }));
      status(text.previewing);
    } catch (error) { await closePreview(); throw error; }
  }
  async function start(): Promise<void> {
    if (!stream || performance.now() - lastSignal > captureConfig.signalFreshMs || video.readyState < 2) throw new Error(text.signal);
    const mime = chooseMimeType();
    const created = await bridge.createSession(mime);
    sessionId = created.id;
    clock = { now: () => Math.max(0, performance.timeOrigin + performance.now() - created.clockEpoch) };
    try { session = await new BrowserMediaSource(stream, mime, clock).start({ signal: controller.signal, timeoutMs: 10_000 }); }
    catch (error) { await bridge.finish(sessionId, 'start-failed'); sessionId = undefined; await closePreview(); status(text.idle); throw error; }
    chunks = 0; bytes = 0; emittedEvents = 0; paused = false; stopping = false;
    status(text.recording);
    const currentSession = session;
    const id = sessionId;
    pump = (async () => {
      let failed = false;
      try {
        await flushEvents();
        for await (const chunk of currentSession.media) {
          await bridge.append(id, chunk.sequence, chunk.atMs, chunk.bytes.slice().buffer);
          chunks++; bytes += chunk.bytes.byteLength;
          await flushEvents();
        }
        await flushEvents();
      } catch (error) { failed = true; showError(error); }
      finally {
        await currentSession.stop();
        try { await bridge.finish(id, failed ? 'capture-or-storage-error' : currentSession.events.at(-1)?.reason ?? 'capture-ended'); }
        catch (error) { failed = true; showError(error); }
        endedAt = clock?.now() ?? 0;
        session = undefined; sessionId = undefined; stopping = false;
        await closePreview();
        status(failed ? text.failed : text.saved);
        await listSessions(); controls();
      }
    })().catch(showError);
  }
  async function stop(reason = 'user-stop'): Promise<void> {
    if (!session || stopping) return;
    stopping = true; status(text.stopping); controls();
    await session.stop(reason); await pump;
  }
  async function pause(): Promise<void> {
    if (!session || stopping) return;
    if (paused) session.resume(); else session.pause();
    paused = !paused; status(paused ? text.paused : text.recording);
    await flushEvents();
  }
  function bind(id: string, action: () => Promise<void>): void {
    element<HTMLButtonElement>(id).onclick = () => {
      if (busy) return;
      busy = true; element('error').textContent = ''; controls();
      void action().catch(showError).finally(() => { busy = false; controls(); });
    };
  }
  bind('refresh', refresh); bind('preview', preview); bind('start', start); bind('stop', stop); bind('pause', pause);
  bind('close-preview', async () => { await closePreview(); status(text.idle); });
  bind('folder', () => bridge.openFolder());
  bind('embedded', async () => { await bridge.openEmbedded(element<HTMLInputElement>('class-url').value); await refresh(); });
  element('consent').onchange = controls; source.onchange = controls;
  bridge.onPause(() => { void pause().catch(showError); });
  bridge.onEnd(reason => { void stop(reason).catch(showError); });
  bridge.onSourceChanged(() => {
    element('error').textContent = text.sourceChanged;
    if (sessionId && clock) void bridge.event(sessionId, 'sourceChanged', clock.now(), 'window-title-changed').catch(showError);
  });
  setInterval(() => {
    const level = meter?.level() ?? 0;
    element<HTMLMeterElement>('level').value = Math.min(1, level * 8);
    if (level > captureConfig.signalThreshold) lastSignal = performance.now();
    element('sound').textContent = stream && performance.now() - Math.max(lastSignal, previewAt) > captureConfig.silenceWarningMs ? text.silent : performance.now() - lastSignal < captureConfig.signalFreshMs ? text.signalDetected : text.signal;
    element('health').textContent = `${text.chunks}: ${chunks} · ${text.bytes}: ${(bytes / 1e6).toFixed(1)} · ${text.duration}: ${((session ? clock?.now() ?? 0 : endedAt) / 1000).toFixed(0)}`;
    controls();
  }, 100);
  void bridge.disk().then(disk => { element('disk').textContent = `${text.disk}: ${(disk.estimated / 1e9).toFixed(2)} / ${(disk.free / 1e9).toFixed(1)} GB`; }).catch(showError);
  void listSessions().catch(showError);
  controls();
}
