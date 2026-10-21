import { createHash } from 'node:crypto';
import { CapabilityError, type MediaChunk } from '../../ports/capture';

export function encodePacket(chunk: MediaChunk): Buffer {
  const header = Buffer.from(JSON.stringify({
    sequence: chunk.sequence, atMs: chunk.atMs, reason: chunk.reason,
    length: chunk.bytes.byteLength, sha256: createHash('sha256').update(chunk.bytes).digest('hex'),
  }));
  const size = Buffer.alloc(4);
  size.writeUInt32LE(header.length);
  return Buffer.concat([size, header, chunk.bytes]);
}

export function decodePacket(packet: Buffer): MediaChunk {
  if (packet.length < 4) throw new CapabilityError('BadInput', 'Truncated packet');
  const size = packet.readUInt32LE(0);
  if (size > 4096 || packet.length < size + 4) throw new CapabilityError('BadInput', 'Invalid packet header');
  const header = JSON.parse(packet.subarray(4, size + 4).toString());
  const bytes = packet.subarray(size + 4);
  if (!Number.isSafeInteger(header.sequence) || header.sequence < 0 || !Number.isFinite(header.atMs) || header.atMs < 0 ||
      header.length !== bytes.length || createHash('sha256').update(bytes).digest('hex') !== header.sha256) {
    throw new CapabilityError('BadInput', 'Packet integrity check failed');
  }
  return { sequence: header.sequence, atMs: header.atMs, reason: header.reason, bytes };
}
