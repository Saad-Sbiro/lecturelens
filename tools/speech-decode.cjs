// Replay-only decoder using the already installed development runtime. Not shipped with WPF.
const {app,BrowserWindow}=require('electron');const fs=require('fs');const path=require('path');
const [input,output,secondsText]=process.argv.slice(2);const limit=Number(secondsText);
app.whenReady().then(async()=>{
 try{
  if(!input||!output||!Number.isFinite(limit)||limit<=0||limit>180)throw Error('Expected input, new WAV path, and 1–180 seconds');
  if(fs.statSync(input).size>40*1024*1024)throw Error('Spike accepts files up to 40 MiB');
  const win=new BrowserWindow({show:false,webPreferences:{nodeIntegration:true,contextIsolation:false}});
  win.webContents.session.webRequest.onBeforeRequest({urls:['http://*/*','https://*/*']},(_details,done)=>done({cancel:true}));
  await win.loadURL('about:blank');
  const details=await win.webContents.executeJavaScript('(async()=>{const fs=require("fs");const bytes=fs.readFileSync('+JSON.stringify(path.resolve(input))+');const context=new AudioContext();const source=await context.decodeAudioData(bytes.buffer.slice(bytes.byteOffset,bytes.byteOffset+bytes.byteLength));const n=Math.min(Math.ceil(source.duration*16000),'+Math.floor(limit*16000)+');const offline=new OfflineAudioContext(1,n,16000);const node=offline.createBufferSource();node.buffer=source;node.connect(offline.destination);node.start();const rendered=await offline.startRendering();const pcm=rendered.getChannelData(0);const wav=Buffer.alloc(44+pcm.length*2);wav.write("RIFF",0);wav.writeUInt32LE(wav.length-8,4);wav.write("WAVEfmt ",8);wav.writeUInt32LE(16,16);wav.writeUInt16LE(1,20);wav.writeUInt16LE(1,22);wav.writeUInt32LE(16000,24);wav.writeUInt32LE(32000,28);wav.writeUInt16LE(2,32);wav.writeUInt16LE(16,34);wav.write("data",36);wav.writeUInt32LE(pcm.length*2,40);for(let i=0;i<pcm.length;i++)wav.writeInt16LE(Math.round(Math.max(-1,Math.min(1,pcm[i]))*32767),44+i*2);fs.writeFileSync('+JSON.stringify(path.resolve(output))+',wav,{flag:"wx"});await context.close();return {sourceSeconds:source.duration,audioSeconds:n/16000,sampleRate:16000,channels:1};})()');
  fs.writeFileSync(output+'.json',JSON.stringify(details),{flag:'wx'});app.exit(0);
 }catch(e){console.error(e.message);app.exit(1);}
});
setTimeout(()=>app.exit(1),60000).unref();
