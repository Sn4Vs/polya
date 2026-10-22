using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Gazik
{
    public sealed class BrowserBackup:MonoBehaviour
    {
        GazikGame game;
        #if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]static extern void Gazik_ExportBackup(string json);
        [DllImport("__Internal")]static extern void Gazik_ImportBackup();
        #endif
        public void Init(GazikGame value){game=value;}
        public void Export()
        {
            #if UNITY_WEBGL && !UNITY_EDITOR
            Gazik_ExportBackup(game.Rules.State.ToJson());
            #else
            game.Hud.Toast("В Unity Editor: Gazik → Export local save backup.");
            #endif
        }
        public void Import()
        {
            if(game.Cloud.Active){game.Hud.Toast("Сначала выбери «Играть локально». Облачный прогресс не изменён.");return;}
            #if UNITY_WEBGL && !UNITY_EDITOR
            Gazik_ImportBackup();
            #else
            game.Hud.Toast("В Unity Editor: останови Play, затем Gazik → Import local save backup.");
            #endif
        }
        [UnityEngine.Scripting.Preserve]
        public void OnBackupImported(string json){try{game.ImportLocal(json);}catch(Exception e){game.Hud.Toast(e.Message);}}
        [UnityEngine.Scripting.Preserve]
        public void OnBackupError(string error){game.Hud.Toast(error);}
    }
}
