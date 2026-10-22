import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';
import path from 'node:path';

const here = path.dirname(fileURLToPath(import.meta.url));

export async function buildPreview(output = path.resolve(here, '../../artifacts/design-lab')) {
  await mkdir(output, { recursive: true });
  const fonts = [['LL Plex', 'plex-0.ttf'], ['LL Naskh', 'naskh-0.ttf'], ['LL Alexandria', 'alexandria-0.ttf']];
  const fontCss = (await Promise.all(fonts.map(async ([name, file]) => {
    const data = await readFile(path.join(here, 'assets/fonts', file));
    const license = await readFile(path.join(here, 'assets/fonts', `${file.split('-')[0]}-OFL.txt`), 'utf8');
    return `/* ${license.replaceAll('*/', '* /')} */\n@font-face{font-family:'${name}';src:url(data:font/ttf;base64,${data.toString('base64')}) format('truetype');font-weight:400;font-display:block;}`;
  }))).join('\n');
  const source = await readFile(path.join(here, 'fixtures/directions.html'), 'utf8');
  const fragment = source.replace('/* FONT_FACES */', fontCss);
  if (Buffer.byteLength(fragment) >= 1_000_000) throw new Error('Inline preview exceeds 1 MB.');
  const fragmentFile = path.join(output, 'lecturelens-directions.html');
  await writeFile(fragmentFile, fragment);
  const wrapper = await readFile(path.join(here, 'viewer.html'), 'utf8');
  const standalone = path.join(output, 'preview.html');
  await writeFile(standalone, wrapper.replace('<!-- DIRECTIONS -->', fragment));
  return { fragmentFile, standalone, bytes: Buffer.byteLength(fragment) };
}

if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) {
  console.log(JSON.stringify(await buildPreview(process.argv[2] && path.resolve(process.argv[2])), null, 2));
}
