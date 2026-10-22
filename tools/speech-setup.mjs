import { createReadStream, createWriteStream } from 'node:fs';
import { mkdir, readFile, rename, access } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { pipeline } from 'node:stream/promises';
import { Readable } from 'node:stream';
import { spawnSync } from 'node:child_process';
import { resolve } from 'node:path';
const assets=JSON.parse(await readFile(new URL('../config/speech-assets.json',import.meta.url)));
const root=resolve('.tools/speech'); await mkdir(root,{recursive:true});
async function hash(path){const h=createHash('sha256');for await(const b of createReadStream(path))h.update(b);return h.digest('hex');}
for(const key of ['engine','vad','small',...(process.argv.includes('--compare')?['base']:[])]){
 const a=assets[key], path=resolve(root,a.file);
 try{if(await hash(path)===a.sha256){console.log(key+': verified cache');continue;}}catch{}
 console.log('Downloading '+key+' (public model/tool; no audio is uploaded)…');
 const response=await fetch(a.url,{signal:AbortSignal.timeout(900000)});if(!response.ok)throw new Error('Download HTTP '+response.status);
 await pipeline(Readable.fromWeb(response.body),createWriteStream(path+'.partial'));
 if(await hash(path+'.partial')!==a.sha256)throw new Error('Checksum mismatch: '+key);
 await rename(path+'.partial',path);
}
try{await access(resolve(root,'engine/Release/whisper-cli.exe'));}catch{
 const r=spawnSync('powershell.exe',['-NoProfile','-File',resolve('tools/speech-unpack.ps1'),resolve(root,'engine.zip'),resolve(root,'engine')],{stdio:'inherit'});if(r.status!==0)throw new Error('Engine extraction failed');
}
console.log('Local speech assets ready. No transcription was started.');
