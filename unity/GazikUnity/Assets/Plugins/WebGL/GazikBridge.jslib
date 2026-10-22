mergeInto(LibraryManager.library, {
  Gazik_Load: function () {
    if (window.GazikFirebaseBridge) window.GazikFirebaseBridge.load();
    else if (window.unityInstance) window.unityInstance.SendMessage('GazikCloud', 'OnFirebaseError', 'CLOUD_WRAPPER_MISSING');
  },
  Gazik_Save: function (value) {
    var json = UTF8ToString(value);
    if (window.GazikFirebaseBridge) window.GazikFirebaseBridge.save(json);
    else if (window.unityInstance) window.unityInstance.SendMessage('GazikCloud', 'OnFirebaseError', 'CLOUD_WRAPPER_MISSING');
  },
  Gazik_Disconnect: function () {
    if (window.GazikFirebaseBridge && window.GazikFirebaseBridge.disconnect) window.GazikFirebaseBridge.disconnect();
  },
  Gazik_ExportBackup: function (value) {
    var blob = new Blob([UTF8ToString(value)], {type:'application/json;charset=utf-8'});
    var url = URL.createObjectURL(blob), link = document.createElement('a');
    link.href = url; link.download = 'Gazik-backup-' + new Date().toISOString().replace(/[:.]/g,'-') + '.json';
    document.body.appendChild(link);link.click();link.remove();setTimeout(function(){URL.revokeObjectURL(url);},1000);
  },
  Gazik_ImportBackup: function () {
    var input=document.createElement('input');input.type='file';input.accept='.json,application/json';
    input.onchange=function(){var file=input.files&&input.files[0];if(!file)return;
      if(file.size>2097152){window.unityInstance.SendMessage('GazikBackup','OnBackupError','Файл слишком большой. Выбери JSON-копию Газика.');return;}
      var reader=new FileReader();reader.onload=function(){window.unityInstance.SendMessage('GazikBackup','OnBackupImported',String(reader.result));};
      reader.onerror=function(){window.unityInstance.SendMessage('GazikBackup','OnBackupError','Не удалось прочитать файл.');};reader.readAsText(file,'UTF-8');};
    input.click();
  }
});
