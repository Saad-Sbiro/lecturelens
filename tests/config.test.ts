import { test } from 'node:test';
import assert from 'node:assert/strict';
import { defaultLanguages, languages } from '../config/languages';
import { captureConfig } from '../config/capture';

test('independent language roles reference the registry', () => {
  for (const role of Object.values(defaultLanguages)) assert.ok(languages[role].enabled);
  const preferences = { ...defaultLanguages, output: defaultLanguages.ui };
  assert.equal(preferences.teacher, defaultLanguages.teacher);
});
test('capture queue holds multiple chunks within a fixed memory bound', () => {
  const chunkEstimate = (captureConfig.videoBitsPerSecond + captureConfig.audioBitsPerSecond) / 8 * captureConfig.chunkMs / 1000;
  assert.ok(chunkEstimate * 3 < captureConfig.maxQueuedBytes);
});
