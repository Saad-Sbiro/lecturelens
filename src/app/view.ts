import strings from '../../config/ui.json';
import { defaultLanguages, languages } from '../../config/languages';

export const text = strings[defaultLanguages.ui as keyof typeof strings];
export function render(): void {
  document.documentElement.lang = defaultLanguages.ui;
  document.documentElement.dir = languages[defaultLanguages.ui].direction;
  document.querySelector<HTMLDivElement>('#app')!.innerHTML = `
    <header><strong>${text.title}</strong><span>${text.local}</span></header>
    <main><p class="eyebrow">${text.eyebrow}</p><h1>${text.heading}</h1><p class="intro">${text.intro}</p>
      <div class="grid"><section class="panel">
        <h2>${text.source}</h2><p class="small">${text.notice}</p>
        <label><input id="consent" type="checkbox" /> ${text.consent}</label>
        <button id="refresh">${text.refresh}</button>
        <label for="source">${text.source}</label><select id="source"></select>
        <div class="actions"><button id="preview">${text.preview}</button><button id="close-preview" disabled>${text.closePreview}</button></div>
        <video id="video" muted autoplay playsinline></video>
        <label for="level">${text.level}</label><meter id="level" min="0" max="1" value="0"></meter>
        <p id="sound" class="small">${text.signal}</p>
      </section><section class="panel">
        <h2>${text.status}</h2><p id="status" role="status">${text.idle}</p>
        <div class="actions"><button id="start" class="primary" disabled>${text.start}</button><button id="pause" disabled>${text.pause}</button><button id="stop" disabled>${text.stop}</button></div>
        <p class="small">${text.shortcut}</p>
        <p id="error" role="alert"></p><p id="disk" class="small"></p>
        <h2>${text.health}</h2><p id="health" class="small">—</p>
        <p class="small">${text.privacy}</p>
        <button id="folder">${text.folder}</button>
      </section></div>
      <section class="panel"><h2>${text.sessions}</h2><div id="sessions"></div></section>
      <details class="panel"><summary>${text.embedded}</summary><p class="small">${text.embeddedNotice}</p>
        <label for="class-url">${text.url}</label><input id="class-url" type="url" placeholder="https://…" />
        <div class="actions"><button id="embedded">${text.openEmbedded}</button></div>
      </details>
    </main>`;
}
export function element<T extends HTMLElement>(id: string): T {
  const value = document.getElementById(id);
  if (!value) throw new Error(`Missing UI element: ${id}`);
  return value as T;
}
