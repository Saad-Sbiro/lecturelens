import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';
import path from 'node:path';
import { createRequire } from 'node:module';
import { buildPreview } from './build.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const output = path.resolve(process.argv[2] || path.join(here, '../../artifacts/design-lab'));
const dependencies = process.env.LECTURELENS_PREVIEW_NODE_MODULES;
if (!dependencies) throw new Error('Set LECTURELENS_PREVIEW_NODE_MODULES to the bundled runtime node_modules directory.');
const require = createRequire(import.meta.url);
const { chromium } = require(path.join(dependencies, 'playwright'));
const executablePath = process.env.LECTURELENS_PREVIEW_BROWSER || 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';
const screenshots = path.join(here, 'screenshots');
await mkdir(output, { recursive: true });
await mkdir(screenshots, { recursive: true });

const { fragmentFile, standalone, bytes } = await buildPreview(output);

const browser = await chromium.launch({executablePath,headless:true});
const context = await browser.newContext({viewport:{width:1200,height:900},deviceScaleFactor:1,colorScheme:'dark',reducedMotion:'reduce'});
const page = await context.newPage();
const errors=[];
page.on('pageerror',error=>errors.push(error.message));
const network=[];
await page.route(/^https?:\/\//,route=>{network.push(route.request().url());return route.abort();});
await page.goto(pathToFileURL(standalone).href);
await page.evaluate(()=>document.fonts.ready);
const report={screenshots:[],layouts:[],contrast:[],interactions:[],accessibility:[],errors,network};
await page.locator('#lab-direction').selectOption('paper');
await page.locator('#lab-locale').selectOption('fr');
await page.locator('#lab-theme').selectOption('light');
await page.locator('#lab-screen').selectOption('result');
report.viewer=await page.locator('[data-direction="paper"]').evaluate(app=>({
  selected:!app.parentElement.hidden,
  locale:app.lang==='fr'&&app.dir==='ltr',
  theme:app.style.colorScheme==='light',
  screen:app.dataset.screen==='result',
  independentLanguages:app.querySelector('.ll-page').lang==='ar'&&app.querySelector('.ll-slide').lang==='fr'
}));
await page.addStyleTag({content:'body { padding: 0; } .lab-controls { display: none; }'});

async function setState(direction,screen,theme,locale='ar') {
  await page.evaluate(({direction,screen,theme,locale})=>{
    document.querySelectorAll('[data-variant]').forEach(section=>{section.hidden=section.querySelector('.ll-app').dataset.direction!==direction;});
    document.querySelector(`[data-direction="${direction}"]`).llPreview.set({screen,theme,locale,paused:false});
  },{direction,screen,theme,locale});
  await page.evaluate(()=>document.fonts.ready);
}

async function inspect(direction,screen,theme,locale,width) {
  await page.setViewportSize({width,height:900});
  await setState(direction,screen,theme,locale);
  const layout=await page.locator(`[data-direction="${direction}"]`).evaluate(app=>{
    const box=app.getBoundingClientRect();
    const overflows=[...app.querySelectorAll('*')].filter(el=>{
      if(el.closest('svg'))return false;
      const r=el.getBoundingClientRect();
      return r.width>0 && (r.right>box.right+2 || r.left<box.left-2);
    }).map(el=>({tag:el.tagName,class:el.className,text:el.textContent.slice(0,45)}));
    const clipped=[...app.querySelectorAll('h1,h2,h3,button,.ll-caption,.ll-eyebrow,.ll-slide-note,.ll-save-note')].filter(el=>el.clientWidth && el.scrollWidth>el.clientWidth+2).map(el=>({class:el.className,text:el.textContent}));
    return {height:Math.ceil(box.height),overflow:overflows,clipped};
  });
  report.layouts.push({direction,screen,theme,locale,width,...layout});
  return layout;
}

for(const direction of ['orbit','paper','prism']) {
  for(const theme of ['light','dark']) {
    for(const screen of ['recording','result']) {
      for(const locale of ['ar','fr']) {
        for(const width of [1200,1024,680,360,320]) await inspect(direction,screen,theme,locale,width);
      }
      await inspect(direction,screen,theme,'ar',1200);
      const file=`${direction}-${screen}-${theme}.png`;
      await page.locator(`[data-direction="${direction}"]`).screenshot({path:path.join(screenshots,file),animations:'disabled'});
      report.screenshots.push(file);
    }
    // Measure rendered token pairs; status and text must stay legible in either theme.
    const contrast=await page.locator(`[data-direction="${direction}"]`).evaluate(app=>{
      const resolve=value=>{const el=document.createElement('span');el.style.color=value;app.append(el);const result=getComputedStyle(el).color;el.remove();return result;};
      const luminance=color=>{const rgb=color.match(/[\d.]+/g).slice(0,3).map(Number).map(v=>v/255).map(v=>v<=.04045?v/12.92:((v+.055)/1.055)**2.4);return .2126*rgb[0]+.7152*rgb[1]+.0722*rgb[2];};
      return [['ink','bg'],['muted','bg'],['ink','surface'],['muted','surface'],['accent','wash'],['accent-ink','accent'],['paper-ink','paper'],['paper-muted','paper']].map(([fg,bg])=>{const a=luminance(resolve(`var(--${fg})`)),b=luminance(resolve(`var(--${bg})`));return {fg,bg,ratio:Number(((Math.max(a,b)+.05)/(Math.min(a,b)+.05)).toFixed(2))};});
    });
    report.contrast.push({direction,theme,pairs:contrast});
  }
  await setState(direction,'recording','dark');
  const pauseButton = page.locator(`[data-direction="${direction}"] [data-action="pause"]`);
  await pauseButton.focus();
  await page.keyboard.press('Enter');
  const paused=await page.locator(`[data-direction="${direction}"]`).getAttribute('data-paused');
  const focusRetained=await pauseButton.evaluate(el=>el===document.activeElement);
  await page.keyboard.press('Enter');
  const resumed=await page.locator(`[data-direction="${direction}"]`).getAttribute('data-paused')==='false';
  await page.locator(`[data-direction="${direction}"] .ll-button[data-action="result"]`).click();
  await page.locator(`[data-direction="${direction}"] [data-action="save"]`).click();
  const notice=await page.locator(`[data-direction="${direction}"] .ll-notice`).innerText();
  report.interactions.push({direction,pause:paused==='true',focusRetained,resumed,exportIsDemo:notice.includes('توضيحي'),result:await page.locator(`[data-direction="${direction}"]`).getAttribute('data-screen')});
  const accessibility=await page.locator(`[data-direction="${direction}"]`).evaluate(app=>({
    flow:app.dir,
    pageDirection:app.querySelector('.ll-page').dir,
    slideDirection:app.querySelector('.ll-slide').dir,
    unnamedButtons:[...app.querySelectorAll('button')].filter(el=>!el.textContent.trim()&&!el.getAttribute('aria-label')).length,
    reducedMotion:[...app.querySelectorAll('*')].every(el=>getComputedStyle(el).transitionDuration==='0s'),
    animations:app.getAnimations({subtree:true}).length
  }));
  report.accessibility.push({direction,...accessibility});
}
// Device scale is a browser approximation, not a native Windows DPI claim.
for (const scale of [1.25,1.5]) {
  const scaledContext=await browser.newContext({viewport:{width:1024,height:900},deviceScaleFactor:scale,reducedMotion:'reduce'});
  const scaledPage=await scaledContext.newPage();
  await scaledPage.route(/^https?:\/\//,route=>{network.push(route.request().url());return route.abort();});
  await scaledPage.goto(pathToFileURL(standalone).href);
  await scaledPage.evaluate(()=>document.fonts.ready);
  await scaledPage.screenshot({path:path.join(output,`browser-scale-${scale}.png`)});
  await scaledContext.close();
}
// Comparison boards use the actual UI screenshots; no invented screen images.
const boardPage=await context.newPage();
await boardPage.setViewportSize({width:1600,height:740});
const fontData=(await readFile(path.join(here,'assets/fonts/plex-0.ttf'))).toString('base64');
for (const [direction,name,theme] of [['orbit','مدار','dark'],['paper','ورق','light'],['prism','طيف','dark']]) {
  const screens=await Promise.all(['recording','result'].map(async screen=>({
    title:screen==='recording'?'التسجيل':'النتيجة',
    image:(await readFile(path.join(screenshots,`${direction}-${screen}-${theme}.png`))).toString('base64')
  })));
  await boardPage.setContent(`<!doctype html><html lang="ar" dir="rtl"><head><style>
    @font-face{font-family:Lab;src:url(data:font/ttf;base64,${fontData})}
    *{box-sizing:border-box}body{margin:0;padding:24px;background:#eff1ef;color:#1a2923;font-family:Lab,sans-serif}
    h1{font-size:26px;font-weight:400;margin:0 0 14px}main{display:grid;grid-template-columns:1fr 1fr;gap:20px}
    h2{font-size:17px;font-weight:400;margin:0 0 10px}img{width:100%;display:block;border-radius:12px}
  </style></head><body><h1>${name}</h1><main>${screens.map(screen=>`<section><h2>${screen.title}</h2><img alt="${screen.title}" src="data:image/png;base64,${screen.image}"></section>`).join('')}</main></body></html>`);
  await boardPage.evaluate(async()=>{await document.fonts.ready;await Promise.all([...document.images].map(img=>img.decode()));});
  await boardPage.screenshot({path:path.join(screenshots,`${direction}-comparison.png`),fullPage:true});
}
await browser.close();
await writeFile(path.join(here,'verification.json'),JSON.stringify(report,null,2)+'\n');
const failed=report.layouts.filter(v=>v.overflow.length||v.clipped.length);
const contrastFailures=report.contrast.flatMap(c=>c.pairs.filter(p=>p.ratio<4.5).map(p=>({...p,direction:c.direction,theme:c.theme})));
console.log(JSON.stringify({fragmentFile,bytes,screenshots:report.screenshots.length,layouts:report.layouts.length,layoutFailures:failed,contrastFailures,viewer:report.viewer,interactions:report.interactions,accessibility:report.accessibility,errors,network},null,2));
if(failed.length||contrastFailures.length||errors.length||network.length||Object.values(report.viewer).some(passed=>!passed)||report.interactions.some(i=>!i.pause||!i.focusRetained||!i.resumed||!i.exportIsDemo||i.result!=='result')||report.accessibility.some(i=>i.unnamedButtons||!i.reducedMotion||i.animations)) process.exitCode=1;
