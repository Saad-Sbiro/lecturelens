import { spawnSync } from 'node:child_process';
import { generateKeyPairSync, createHash } from 'node:crypto';
import { existsSync, readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { resolve, relative, dirname } from 'node:path';
import { build } from 'vite';

// A stable PUBLIC key fixes the unpacked extension ID; the private key is discarded.
const keyFile = 'config/extension-public-key.json';
if (!existsSync(keyFile)) {
  const { publicKey } = generateKeyPairSync('rsa', { modulusLength: 2048 });
  writeFileSync(keyFile, JSON.stringify({ key: publicKey.export({ type: 'spki', format: 'der' }).toString('base64') }, null, 2) + '\n');
}
const { key } = JSON.parse(readFileSync(keyFile, 'utf8'));
export const extensionId = [...createHash('sha256').update(Buffer.from(key, 'base64')).digest('hex').slice(0, 32)].map(c => String.fromCharCode(97 + parseInt(c, 16))).join('');
await build({ configFile: 'vite.extension.config.mjs' });
writeFileSync('dist/extension/manifest.json', JSON.stringify({
  manifest_version: 3, name: 'LectureLens Tab Capture', version: '0.1.0', minimum_chrome_version: '116', key,
  permissions: ['activeTab', 'tabCapture', 'offscreen', 'nativeMessaging', 'storage', 'webNavigation'],
  background: { service_worker: 'background.js', type: 'module' }, action: { default_popup: 'popup.html' },
  content_security_policy: { extension_pages: "script-src 'self'; object-src 'none'; connect-src 'none'" },
}, null, 2));
const local = resolve('.tools/dotnet/dotnet.exe');
const dotnet = existsSync(local) ? local : 'dotnet';
const env = { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: '1' };
const output = resolve('dist/native-host');
const args = ['publish', 'src/native/NativeHost/NativeHost.csproj', '-c', 'Debug', '--self-contained', 'false', '-o', output];
if (existsSync(local)) env.AppHostRelativeDotNet = relative(output, dirname(local)).replaceAll('\\', '/');
const result = spawnSync(dotnet, args, { stdio: 'inherit', env });
if (result.status !== 0) throw new Error('Native host build failed. Stop any recording before rebuilding.');
const origin = `chrome-extension://${extensionId}/`;
writeFileSync(resolve(output, 'allowed-origin.txt'), origin);
mkdirSync('dist/browser-setup', { recursive: true });
const manifestPath = resolve('dist/browser-setup/local.lecturelens.capture.json');
writeFileSync(manifestPath, JSON.stringify({ name: 'local.lecturelens.capture', description: 'LectureLens local recording storage', path: resolve(output, 'LectureLens.NativeHost.exe'), type: 'stdio', allowed_origins: [origin] }, null, 2));
if (!process.argv.includes('--no-register')) {
  const registered = spawnSync('reg.exe', ['add', 'HKCU\\Software\\Google\\Chrome\\NativeMessagingHosts\\local.lecturelens.capture', '/ve', '/t', 'REG_SZ', '/d', manifestPath, '/f'], { stdio: 'inherit' });
  if (registered.status !== 0) throw new Error('Native host registration failed');
}
console.log(`Chrome: chrome://extensions > Developer mode > Load unpacked > ${resolve('dist/extension')}`);
console.log(`Extension ID: ${extensionId}. Reload it after extension source changes. No recording was started.`);
