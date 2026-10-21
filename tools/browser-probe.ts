import { BrowserMediaSource, chooseMimeType } from '../src/adapters/capture/media-recorder';

// Only the development smoke imports this module. It never requests screen/microphone access.
export async function runSyntheticMediaProbe() {
  const canvas = document.createElement('canvas'); canvas.width = 320; canvas.height = 180;
  const context = canvas.getContext('2d')!;
  const stream = canvas.captureStream(10);
  const audio = new AudioContext();
  const tone = audio.createOscillator();
  const gain = audio.createGain(); gain.gain.value = 0.05;
  const output = audio.createMediaStreamDestination();
  tone.connect(gain).connect(output); tone.start(); await audio.resume();
  output.stream.getAudioTracks().forEach(track => stream.addTrack(track));
  let frames = 0;
  const paint = setInterval(() => {
    context.fillStyle = frames++ % 2 ? '#e0cc80' : '#307060'; context.fillRect(0, 0, 320, 180);
    context.fillStyle = '#ffffff'; context.fillText(`Synthetic slide ${frames}`, 30, 70);
  }, 100);
  const origin = performance.now();
  const mime = chooseMimeType();
  const session = await new BrowserMediaSource(stream, mime, { now: () => performance.now() - origin }).start({ signal: new AbortController().signal, timeoutMs: 10000 });
  const parts: Uint8Array<ArrayBuffer>[] = [];
  const consume = (async () => { for await (const chunk of session.media) parts.push(chunk.bytes.slice()); })();
  try {
    await new Promise(resolve => setTimeout(resolve, 1300)); session.pause();
    await new Promise(resolve => setTimeout(resolve, 400)); session.resume();
    await new Promise(resolve => setTimeout(resolve, 3300)); await session.stop(); await consume;
    const video = document.createElement('video'); video.muted = true;
    const url = URL.createObjectURL(new Blob(parts, { type: mime }));
    video.src = url;
    try {
      await Promise.race([video.play(), new Promise((_, reject) => setTimeout(() => reject(new Error('Synthetic media decode timeout')), 5000))]);
      const decoded = { width: video.videoWidth, height: video.videoHeight, readyState: video.readyState };
      video.pause();
      return { mime, decoded, framesGenerated: frames, events: session.events, chunks: parts.map(part => Array.from(part)) };
    } finally { video.pause(); video.removeAttribute('src'); video.load(); URL.revokeObjectURL(url); }
  } finally {
    clearInterval(paint); await session.stop(); stream.getTracks().forEach(track => track.stop());
    tone.stop(); await audio.close();
  }
}
