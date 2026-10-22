// Browser bridge contract checks with an inert DOM; no files are downloaded or imported.
const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict'),path=require('node:path');
const source=fs.readFileSync(path.join(__dirname,'../Assets/Plugins/WebGL/GazikBridge.jslib'),'utf8');
function setup(){
 const nodes=[],messages=[],blobs=[],revoked=[],reads=[];let timer;
 const library={};const context={LibraryManager:{library},mergeInto:Object.assign,UTF8ToString:x=>x,
  window:{unityInstance:{SendMessage:(target,method,value)=>messages.push({target,method,value})}},
  document:{body:{appendChild:n=>n.appended=true},createElement:type=>{const node={type,click(){this.clicked=true;},remove(){this.removed=true;}};nodes.push(node);return node;}},
  Blob:class{constructor(parts,options){blobs.push({parts,options});}},URL:{createObjectURL:()=> 'blob:test',revokeObjectURL:x=>revoked.push(x)},setTimeout:fn=>timer=fn,
  FileReader:class{readAsText(file,encoding){reads.push({file,encoding});if(file.error)this.onerror();else{this.result=file.contents;this.onload();}}},Date};
 vm.runInNewContext(source,context);return{library,nodes,messages,blobs,revoked,reads,expire:()=>timer()};
}
let count=0;function check(name,work){work();count++;console.log('PASS '+name);}
check('Export retains complete Unicode JSON and revokes its temporary URL',()=>{const s=setup();const json='{"name":"Ёжик 💚","stats":{"hunger":80}}';s.library.Gazik_ExportBackup(json);assert.equal(s.blobs[0].parts[0],json);assert.equal(s.blobs[0].options.type,'application/json;charset=utf-8');assert(s.nodes[0].clicked&&s.nodes[0].removed);assert.match(s.nodes[0].download,/^Gazik-backup-.*\.json$/);s.expire();assert.deepEqual(s.revoked,['blob:test']);});
check('Import picker starts only on an explicit bridge call',()=>{const s=setup();assert.equal(s.nodes.length,0);s.library.Gazik_ImportBackup();assert(s.nodes[0].clicked);assert.equal(s.nodes[0].accept,'.json,application/json');});
check('Cancelled picker changes no progress',()=>{const s=setup();s.library.Gazik_ImportBackup();s.nodes[0].files=[];s.nodes[0].onchange();assert.equal(s.messages.length,0);assert.equal(s.reads.length,0);});
check('Oversized backup is rejected before FileReader runs',()=>{const s=setup();s.library.Gazik_ImportBackup();s.nodes[0].files=[{size:2097153}];s.nodes[0].onchange();assert.equal(s.reads.length,0);assert.equal(s.messages[0].target,'GazikBackup');assert.equal(s.messages[0].method,'OnBackupError');});
check('UTF-8 file reaches the validating C# import callback unchanged',()=>{const s=setup(),contents='\uFEFF{"name":"Газик","stats":{}}';s.library.Gazik_ImportBackup();s.nodes[0].files=[{size:100,contents}];s.nodes[0].onchange();assert.equal(s.reads[0].encoding,'UTF-8');assert.deepEqual(s.messages,[{target:'GazikBackup',method:'OnBackupImported',value:contents}]);});
check('File read failure reports an error instead of importing',()=>{const s=setup();s.library.Gazik_ImportBackup();s.nodes[0].files=[{size:100,error:true}];s.nodes[0].onchange();assert.equal(s.messages[0].method,'OnBackupError');assert.equal(s.messages.length,1);});
console.log('ALL '+count+' BACKUP BRIDGE CHECKS PASSED');
