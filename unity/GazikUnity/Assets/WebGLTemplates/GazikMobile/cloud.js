/* Same Firebase project, document and account roles as the existing Gazik wrapper.
   Existing authorized sessions resume on startup. No automatic cloud writes. */
(function(){
  'use strict';
  var config = {
    apiKey: "AIzaSyCQ4Mx2l60D0KBt8zS6TzroHaJ4SpIlZ7s",
    authDomain: "zametki-79427.firebaseapp.com",
    projectId: "zametki-79427",
    storageBucket: "zametki-79427.firebasestorage.app",
    messagingSenderId: "630129759584",
    appId: "1:630129759584:web:b339276bd11ff0ac6a3a05",
    measurementId: "G-8C8X4GPM59"
  };
  var owner='BYmhRqqXQQPKPTOnrjzEFTfNL3D3',viewer='IG3jAerjnHZekX8gEn51AQq6Oli1';
  var path='pets/zvezdochka-hedgehog';
  var box=document.getElementById('auth'),info=document.getElementById('auth-state'),button=document.getElementById('login');
  var user=null,known=false,requested=false,loaded=false,revision=0,saving=false,pending=null,generation=0,resumeWanted=false;
  var stopWatch=null,serverFingerprint='',serverDocument={},writingFingerprint='',writingGeneration=-1;
  function send(method,value){if(window.unityInstance)window.unityInstance.SendMessage('GazikCloud',method,String(value||''));}
  if(typeof firebase==='undefined'){window.GazikFirebaseBridge={load:function(){send('OnFirebaseError','Firebase не загрузился. Локальная игра сохранена.');},save:function(){send('OnFirebaseError','CLOUD_UNAVAILABLE');}};return;}
  if(!firebase.apps.length)firebase.initializeApp(config);var auth=firebase.auth(),db=firebase.firestore();
  auth.setPersistence(firebase.auth.Auth.Persistence.LOCAL).catch(function(){});
  function allowed(){return user&&(user.uid===owner||user.uid===viewer);}
  function fingerprint(data){
    function sorted(value){if(Array.isArray(value))return value.map(sorted);if(value&&typeof value==='object'){var out=Object.create(null);Object.keys(value).sort().forEach(function(k){out[k]=sorted(value[k]);});return out;}return value;}
    return JSON.stringify(sorted(JSON.parse(JSON.stringify(data))));
  }
  function merged(base,patch){
    var result=Object.assign(Object.create(null),JSON.parse(JSON.stringify(base)));Object.keys(patch).forEach(function(key){var value=patch[key];
      result[key]=value&&typeof value==='object'&&!Array.isArray(value)&&Object.keys(value).length&&result[key]&&typeof result[key]==='object'&&!Array.isArray(result[key])?merged(result[key],value):JSON.parse(JSON.stringify(value));
    });return result;
  }
  function unwatch(){if(stopWatch){stopWatch();stopWatch=null;}}
  function fail(error){loaded=false;pending=null;generation++;unwatch();send('OnFirebaseError',error);}
  function deliver(data){var copy=JSON.parse(JSON.stringify(data));copy.__gazikRole=user.uid===owner?'owner':'viewer';send('OnFirebaseLoaded',JSON.stringify(copy));}
  function watch(request){
    unwatch();stopWatch=db.doc(path).onSnapshot(function(snap){
      if(request!==generation||!loaded||!allowed()||(snap.metadata&&snap.metadata.hasPendingWrites))return;
      var data=snap.exists?snap.data():{},stamp=fingerprint(data);
      if(stamp===serverFingerprint||(saving&&writingGeneration===generation&&stamp===writingFingerprint))return;
      if((saving&&writingGeneration===generation)||pending){fail('CONFLICT: прогресс изменился на другом устройстве. Нажми «Облако» для загрузки.');return;}
      revision=Number(data.lastUpdated)||0;serverFingerprint=stamp;serverDocument=JSON.parse(JSON.stringify(data));deliver(data);
    },function(error){if(request===generation)fail(error.message||'LIVE_SYNC_FAILED');});
  }
  async function load(){
    requested=true;loaded=false;pending=null;unwatch();var request=++generation;if(!known)return;
    if(!allowed()){box.classList.add('show');send('OnFirebaseError','AUTH_REQUIRED');return;}
    try{var snap=await db.doc(path).get();if(request!==generation||!allowed())return;var data=snap.exists?snap.data():{};revision=Number(data.lastUpdated)||0;serverDocument=JSON.parse(JSON.stringify(data));serverFingerprint=fingerprint(data);loaded=true;deliver(data);watch(request);}
    catch(e){if(request===generation)fail(e.message||'LOAD_FAILED');}
  }
  async function drain(){
    if(saving||!pending)return;saving=true;var json=pending,request=generation,expectedRevision=revision,expectedFingerprint=serverFingerprint,uid=user&&user.uid;pending=null;
    try{
      if(!loaded||!user||user.uid!==owner)throw new Error('READ_ONLY');
      var data=JSON.parse(json);delete data.__gazikRole;
      if(!data.stats||typeof data.stats!=='object'||Array.isArray(data.stats)||!Number.isFinite(data.lastUpdated))throw new Error('INVALID_SAVE');
      var writingDocument=merged(serverDocument,data);writingFingerprint=fingerprint(writingDocument);writingGeneration=request;
      await db.runTransaction(async function(tx){
        var ref=db.doc(path),snap=await tx.get(ref),remote=snap.exists?snap.data():{};
        if(request!==generation||!user||user.uid!==uid||!loaded)throw new Error('SESSION_CHANGED');
        if((Number(remote.lastUpdated)||0)!==expectedRevision||fingerprint(remote)!==expectedFingerprint)throw new Error('CONFLICT: прогресс изменился на другом устройстве. Нажми «Облако» для загрузки.');
        tx.set(ref,data,{merge:true});
      });
      if(request===generation){revision=Number(data.lastUpdated)||0;serverDocument=writingDocument;serverFingerprint=fingerprint(writingDocument);send('OnFirebaseSaved','ok');}
    }catch(e){if(request===generation)fail(e.message||'SAVE_FAILED');}
    finally{saving=false;writingFingerprint='';writingGeneration=-1;if(pending)drain();}
  }
  function save(json){if(!loaded||!user||user.uid!==owner){send('OnFirebaseError','READ_ONLY');return;}pending=json;drain();}
  function disconnect(){generation++;loaded=false;requested=false;resumeWanted=false;pending=null;unwatch();box.classList.remove('show');}
  function resume(){resumeWanted=true;if(known&&allowed()&&!requested){send('OnFirebaseResume','');return load();}}
  window.GazikFirebaseBridge={load:load,save:save,disconnect:disconnect,resume:resume};
  auth.onAuthStateChanged(function(value){generation++;unwatch();known=true;user=value||null;loaded=false;pending=null;if(requested)load();else if(resumeWanted)resume();});
  document.getElementById('auth-close').onclick=function(){disconnect();send('OnFirebaseLocal','local');};
  document.getElementById('auth-form').onsubmit=async function(event){event.preventDefault();button.disabled=true;info.textContent='Входим…';try{var result=await auth.signInWithEmailAndPassword(document.getElementById('email').value.trim(),document.getElementById('password').value);if(result.user.uid!==owner&&result.user.uid!==viewer){info.textContent='У этого аккаунта нет доступа к Газику.';return;}document.getElementById('password').value='';info.textContent='';box.classList.remove('show');}catch(e){info.textContent='Не получилось войти. Проверь e-mail и пароль.';}finally{button.disabled=false;}};
})();
