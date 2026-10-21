import type { AdapterMetadata, MediaChunk } from './capture';
import type { SessionSummary } from './desktop';

export interface Storage {
  metadata: AdapterMetadata;
  create(mimeType: string): Promise<string>;
  append(id: string, chunk: MediaChunk): Promise<void>;
  list(): Promise<SessionSummary[]>;
  recover(id: string): Promise<string>;
  delete(id: string): Promise<void>;
  usage(): Promise<number>;
  requestPersistence(): Promise<boolean>;
}
