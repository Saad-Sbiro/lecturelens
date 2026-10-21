export class SoundMeter {
  private context: AudioContext;
  private analyser: AnalyserNode;
  private samples: Float32Array<ArrayBuffer>;
  constructor(stream: MediaStream) {
    this.context = new AudioContext();
    this.analyser = this.context.createAnalyser();
    this.analyser.fftSize = 2048;
    this.samples = new Float32Array(this.analyser.fftSize);
    this.context.createMediaStreamSource(stream).connect(this.analyser);
    // No connection to speakers: the preview must never feed loopback audio into itself.
  }
  async start(): Promise<void> { await this.context.resume(); }
  level(): number {
    this.analyser.getFloatTimeDomainData(this.samples);
    let sum = 0;
    for (const sample of this.samples) sum += sample * sample;
    return Math.sqrt(sum / this.samples.length);
  }
  async close(): Promise<void> { await this.context.close(); }
}
