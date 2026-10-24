import { createRequire } from 'node:module';
import { createServer } from 'node:http';
import { readFile, mkdir } from 'node:fs/promises';
import { resolve, extname } from 'node:path';
import assert from 'node:assert/strict';
const require = createRequire(import.meta.url);
const dependencies = process.env.LECTURELENS_PREVIEW_NODE_MODULES;
if (!dependencies) throw new Error('Set LECTURELENS_PREVIEW_NODE_MODULES to the bundled runtime node_modules directory.');
const { chromium } = require(resolve(dependencies, 'playwright'));
const root = resolve('dist/extension');
await mkdir('artifacts/extension-preview', {recursive:true});
const server = createServer(async (req, res) => {
  try { const file = resolve(root, '.' + req.url); if (!file.startsWith(root)) throw Error();
    res.setHeader('Content-Type', ({'.js':'text/javascript','.css':'text/css','.html':'text/html','.png':'image/png'})[extname(file)] || 'application/octet-stream'); res.end(await readFile(file));
  } catch { res.writeHead(404).end(); }
});
await new Promise(r => server.listen(0, '127.0.0.1', r));
const browser = await chromium.launch({ executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', headless:true });
try {
 const page = await browser.newPage({viewport:{width:600,height:600},reducedMotion:'reduce'});
 const errors=[]; page.on('pageerror', e=>errors.push(e.message));
 await page.addInitScript(() => {
   window.mockState={state:'idle'}; window.operations=[];
   window.chrome={tabs:{query:async()=>[{id:7,title:'The art of understanding Â· Seminar'}]},runtime:{sendMessage:async m=>{
     window.operations.push(m);
     if(m.op==='termsStatus')return {ok:true,value:{accepted:localStorage.getItem('termsAccepted')==='yes'}};
     if(m.op==='acceptTerms') {if(window.failAgreement)return {ok:false,error:'Could not save agreement'};localStorage.setItem('termsAccepted','yes');return {ok:true,value:{accepted:true}};}

     if(m.op==='preview') {if(!m.consent)return {ok:false,error:'Permission required'};window.mockState={state:'previewing',lessonMode:m.lessonMode,level:.04};}
     if(m.op==='start')window.mockState.state='recording';
     if(m.op==='pause')window.mockState.state=window.mockState.state==='recording'?'paused':'recording';
     if(m.op==='stop')window.mockState.state='finished';
     return {ok:true,value:window.mockState};
   }}};
 });
 await page.goto(`http://127.0.0.1:${server.address().port}/popup.html`);
 await page.waitForFunction(()=>document.querySelector('#status').textContent==='Ready');
 await page.screenshot({path:'artifacts/extension-preview/slides.png',fullPage:true});
 await page.locator('[value="voice"]').check();
 assert.equal(await page.locator('#slideCard').isVisible(),false);
 assert.equal(await page.locator('#voiceCard').isVisible(),true);
 assert.equal(await page.locator('#consent').isChecked(),false);
 assert.equal(await page.locator('#preview').isDisabled(),true);
 await page.locator('#readTerms').click();
 assert.equal(await page.locator('#termsDialog').isVisible(),true);
 assert.equal(await page.locator('#termsContent h3').count(),7);
 await page.screenshot({path:'artifacts/extension-preview/terms.png',fullPage:true});
 await page.locator('#closeTerms').click();

 await page.screenshot({path:'artifacts/extension-preview/voice.png',fullPage:true});
 await page.locator('#consent').check();
 await page.evaluate(()=>{window.failAgreement=true;});
 await page.locator('#acceptTerms').click();
 await page.waitForFunction(()=>document.querySelector('#error').textContent.includes('Could not save'));
 assert.equal(await page.locator('#agreementNotice').isVisible(),true);
 assert.equal(await page.locator('#preview').isDisabled(),true);
 await page.evaluate(()=>{window.failAgreement=false;});
 await page.locator('#acceptTerms').click();
 await page.waitForFunction(()=>document.querySelector('#agreementNotice').hidden);
 await page.reload();
 await page.waitForFunction(()=>document.querySelector('#agreementNotice').hidden && !document.querySelector('#preview').disabled);
 await page.locator('[value="voice"]').check();
 await page.locator('#preview').click();
 await page.waitForFunction(()=>!document.querySelector('#start').disabled);
 assert.equal(await page.locator('[value="slides"]').isDisabled(),true);
 await page.locator('#start').click();
 await page.waitForFunction(()=>document.body.dataset.state==='recording');
 await page.screenshot({path:'artifacts/extension-preview/recording.png',fullPage:true});
 await page.locator('#pause').click();
 await page.waitForFunction(()=>document.body.dataset.state==='paused');
 assert.equal(await page.locator('#pause').textContent(),'Resume');
 await page.reload();
 await page.evaluate(()=>{window.mockState={state:'paused',lessonMode:'voice'};});
 await page.waitForFunction(()=>document.body.dataset.state==='paused');
 assert.equal(await page.locator('[value="voice"]').isChecked(),true);
 await page.locator('#stop').click();
 await page.waitForFunction(()=>document.body.dataset.state==='finished');
 assert.equal(await page.locator('[value="slides"]').isDisabled(),false);
 assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth),600);
 await page.evaluate(()=>{window.mockState={state:'recording',lessonMode:'voice'};});
 await page.waitForFunction(()=>document.body.dataset.state==='recording');
 await page.emulateMedia({reducedMotion:'no-preference'});
 assert.equal(await page.locator('.voice-art i').first().evaluate(el=>getComputedStyle(el).animationName),'breathing');
 await page.emulateMedia({reducedMotion:'reduce'});
 assert.equal(await page.locator('.voice-art i').first().evaluate(el=>getComputedStyle(el).animationName),'none');
 assert.deepEqual(errors,[]);
 console.log('PASS: one-time terms, read/reopen terms, failed-save retry, mode choice, voice start without crop, mode lock, pause/resume label, popup reopen, end/unlock, no horizontal overflow or JS errors.');
} finally {await browser.close();server.close();}
