import { spawn } from 'node:child_process';
import { readFile, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
export const metadata={capability:'SpeechToText',runsOn:'device',costTier:'free',mode:'batch',timestampGranularity:'segment',dataUse:'private',needsKey:false,status:'experimental',maxDurationSeconds:180};
export function normalizeTranscript(result,durationSeconds){
 if(!Array.isArray(result.transcription))throw new Error('BadInput: missing transcript');
 return result.transcription.map(s=>{
  const start=s.offsets?.from/1000,end=s.offsets?.to/1000;
  if(!Number.isFinite(start)||!Number.isFinite(end)||start<0||end<start||end>durationSeconds+1||typeof s.text!=='string')throw new Error('BadInput: invalid transcript segment');
  const text=s.text.trim();
  return {start,end,text,language:result.result?.language??null,confidence:null,kept:!!text,reason:text?'transcribed-unreviewed':'empty-segment'};
 });
}
export function runProcess(exe,args,{signal,timeoutMs=900000,stdout,stderr}={}){
 return new Promise((resolve,reject)=>{
  const child=spawn(exe,args,{windowsHide:true,signal,timeout:timeoutMs,stdio:['ignore',stdout??'ignore',stderr??'ignore']});
  child.once('error',reject);child.once('exit',(code,sig)=>code===0?resolve():reject(new Error('Speech process failed: '+(sig??code))));
 });
}
export async function transcribe(wav,{model='small',language,output,durationSeconds,signal,timeoutMs=900000,run=runProcess}){
 if(!['small','base'].includes(model)||!/^[a-z]{2,3}$/.test(language)||!(durationSeconds>0&&durationSeconds<=180))throw new Error('BadInput: unsupported speech options');
 const root=resolve('.tools/speech'), began=performance.now();
 const args=['-m',resolve(root,model+'-q5.bin'),'-f',resolve(wav),'-l',language,'-t','4','-ng','--vad','-vm',resolve(root,'silero.bin'),'-vsd','500','-vp','200','-oj','-of',resolve(output,'engine')];
 const {open}=await import('node:fs/promises');
 const log=await open(resolve(output,'engine.log'),'wx');
 try{await run(resolve(root,'engine/Release/whisper-cli.exe'),args,{signal,timeoutMs,stdout:log.fd,stderr:log.fd});}finally{await log.close();}
 const segments=normalizeTranscript(JSON.parse(await readFile(resolve(output,'engine.json'),'utf8')),durationSeconds);
 const report={model,language,audioSeconds:durationSeconds,processingSeconds:(performance.now()-began)/1000,timestampBasis:'decoded-audio; not aligned to recording pauses yet',quality:'unscored; owner review required',segments};
 await writeFile(resolve(output,'transcript.json'),JSON.stringify(report,null,2)+'\n',{flag:'wx'});
 await writeFile(resolve(output,'transcript.txt'),segments.filter(s=>s.kept).map(s=>'['+s.start.toFixed(2)+' – '+s.end.toFixed(2)+'] '+s.text).join('\n'),{flag:'wx'});
 return report;
}
