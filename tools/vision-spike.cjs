const { app, nativeImage, BrowserWindow } = require('electron');
const fs = require('node:fs/promises');
const path = require('node:path');
app.whenReady().then(async () => {
  try {
    const args = process.argv.slice(2), input = args[0];
    if (!input || input.startsWith('--')) throw Error('Usage: npm run vision:spike -- screenshot.png [--allow-upload --free-tier] [--mock] [--no-open]');
    const { analyze, pixels, model } = await import('./vision/gemini.mjs');
    const { report } = await import('./vision/report.mjs');
    const source = path.resolve(input), stat = await fs.stat(source);
    if (stat.size > 14_000_000) throw Error('Use a PNG/JPEG screenshot smaller than 14 MB (inline API limit).');
    const bytes = await fs.readFile(source);
    const mime = bytes.subarray(0, 8).equals(Buffer.from([137,80,78,71,13,10,26,10])) ? 'image/png' : bytes[0] === 255 && bytes[1] === 216 ? 'image/jpeg' : null;
    if (!mime) throw Error('Only PNG/JPEG screenshots are supported.');
    const image = nativeImage.createFromBuffer(bytes), { width, height } = image.getSize();
    if (image.isEmpty() || width * height > 40_000_000) throw Error('Invalid image or more than 40 megapixels.');
    const root = path.resolve('artifacts/vision-spike'), mock = args.includes('--mock');
    const result = mock ? { model, detection: { box: [200, 250, 800, 850], type: 'slide', isEducational: true }, usageMetadata: null, requestUsage: null, cacheHit: false, latencyMs: null } :
      await analyze({ bytes, mime, cacheDir: path.join(root, 'cache'), key: process.env.GEMINI_API_KEY, allowUpload: args.includes('--allow-upload'), freeTier: args.includes('--free-tier') });
    const dir = path.join(root, `${mock ? 'mock' : result.id}-${Date.now()}`); await fs.mkdir(dir, { recursive: true });
    const rect = pixels(result.detection.box, width, height), crop = rect ? image.crop(rect) : null;
    if (crop) await fs.writeFile(path.join(dir, 'crop.png'), crop.toPNG());
    await fs.writeFile(path.join(dir, 'detection.json'), JSON.stringify(result.detection, null, 2));
    await fs.writeFile(path.join(dir, 'metrics.json'), JSON.stringify({ ...result, mock, width, height, pixelCrop: rect, margin: 0.015 }, null, 2));
    const html = report({ original: `data:${mime};base64,${bytes.toString('base64')}`, crop: crop?.toDataURL(), rect, width, height, result, mock });
    await fs.writeFile(path.join(dir, 'report.html'), html);
    process.stdout.write(JSON.stringify(result.detection) + '\n'); console.error('Report: ' + path.join(dir, 'report.html'));
    if (args.includes('--no-open')) { app.quit(); return; }
    const window = new BrowserWindow({ width: 1400, height: 920, backgroundColor: '#191b21', webPreferences: { nodeIntegration: false, contextIsolation: true, sandbox: true } });
    window.webContents.setWindowOpenHandler(() => ({ action: 'deny' }));
    await window.loadFile(path.join(dir, 'report.html'));
    if (args.includes('--smoke')) {
      await fs.writeFile(path.join(dir, 'preview.png'), (await window.webContents.capturePage()).toPNG());
      app.quit();
    }
  } catch (error) { console.error(error.message); app.exit(1); }
});
app.on('window-all-closed', () => app.quit());
