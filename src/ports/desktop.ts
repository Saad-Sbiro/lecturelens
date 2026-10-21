export interface WindowChoice { id: string; name: string; thumbnail: string }
export interface SessionSummary { id: string; state: string; chunks: number; bytes: number; createdAt: string; problems: string[] }
export interface DesktopBridge {
  listWindows(): Promise<WindowChoice[]>;
  selectSource(id: string, consent: boolean): Promise<void>;
  createSession(mimeType: string): Promise<{ id: string; clockEpoch: number }>;
  append(id: string, sequence: number, atMs: number, bytes: ArrayBuffer): Promise<void>;
  event(id: string, type: string, atMs: number, reason: string): Promise<void>;
  finish(id: string, reason: string): Promise<void>;
  listSessions(): Promise<SessionSummary[]>;
  recover(id: string): Promise<string>;
  deleteSession(id: string): Promise<void>;
  disk(): Promise<{ free: number; estimated: number }>;
  openFolder(): Promise<void>;
  openEmbedded(url: string): Promise<void>;
  onPause(callback: () => void): () => void;
  onSourceChanged(callback: () => void): () => void;
  onEnd(callback: (reason: string) => void): () => void;
}
