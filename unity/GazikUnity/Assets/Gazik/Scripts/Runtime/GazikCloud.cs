using System;
using System.Runtime.InteropServices;
using Gazik.Core;
using UnityEngine;

namespace Gazik
{
    public sealed class GazikCloud:MonoBehaviour
    {
        GazikGame game;public bool Active{get;private set;}bool loaded;
        #if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]static extern void Gazik_Load();
        [DllImport("__Internal")]static extern void Gazik_Save(string json);
        [DllImport("__Internal")]static extern void Gazik_Disconnect();
        #endif
        public void Init(GazikGame value){game=value;}
        [UnityEngine.Scripting.Preserve]
        public void OnFirebaseResume(string unused)
        {
            if(Active)return;game.PrepareCloud();Active=true;loaded=false;game.Rules.CanWrite=false;game.Care.Cancel();game.StatusText("Облако · загружаем Газика");
        }
        public void Connect()
        {
            game.Care.Cancel();
            #if UNITY_WEBGL && !UNITY_EDITOR
            if(!Active)game.PrepareCloud();Active=true;loaded=false;game.Rules.CanWrite=false;game.StatusText("Облако · ожидаем вход и загрузку");Gazik_Load();
            #else
            game.Hud.Toast("В редакторе работает локальное сохранение. Облако доступно в WebGL-сборке.");
            #endif
        }
        public void Save(string json)
        {
            if(!Active||!loaded||!game.Rules.CanWrite)return;
            #if UNITY_WEBGL && !UNITY_EDITOR
            Gazik_Save(json);
            #endif
        }
        public void Disconnect()
        {
            if(Active)game.SaveProgress();Active=false;loaded=false;game.ReturnToLocal();game.StatusText("Локальная игра · сохранение на устройстве");
            #if UNITY_WEBGL && !UNITY_EDITOR
            Gazik_Disconnect();
            #endif
        }
        [UnityEngine.Scripting.Preserve]
        public void OnFirebaseLocal(string unused){Disconnect();}
        // Public method names retain the contract of the existing website wrapper.
        [UnityEngine.Scripting.Preserve]
        public void OnFirebaseLoaded(string json)
        {
            if(!Active)return;
            try
            {
                var state=PetState.FromJson(json);string role=state.Text("__gazikRole","viewer");state.Data.Remove("__gazikRole");
                if(state.Data.Count==0)state=PetState.Fresh(PetState.Now);
                else if(!state.Data.ContainsKey("stats")||!(state.Data["stats"] is System.Collections.Generic.Dictionary<string,object>))
                    throw new FormatException("Формат старого сохранения требует ручного переноса. Оно не изменено.");
                state.Normalize(PetState.Now);game.LoadCloud(state,role=="owner",!loaded);loaded=true;
                game.StatusText(role=="owner"?"Облако · прогресс загружен":"Облако · режим просмотра");
            }
            catch(Exception e){loaded=false;game.Rules.CanWrite=false;game.StatusText("Облако · загрузка остановлена");game.Hud.Toast(e.Message);Debug.LogWarning(e.Message);}
        }
        [UnityEngine.Scripting.Preserve]
        public void OnFirebaseSaved(string message){if(Active)game.StatusText(message=="ok"?"Облако · прогресс сохранён":"Облако · режим просмотра");}
        [UnityEngine.Scripting.Preserve]
        public void OnFirebaseError(string error)
        {
            if(!Active)return;loaded=false;game.Rules.CanWrite=false;game.StatusText("Облако · требуется повторная загрузка");Debug.LogWarning("Gazik cloud: "+error);
            string message=error=="AUTH_REQUIRED"?"Войди в аккаунт, чтобы загрузить прогресс.":error.StartsWith("CONFLICT")?"Прогресс изменился на другом устройстве. Нажми «Облако» для загрузки.":error=="READ_ONLY"?"Режим просмотра — прогресс не изменён.":"Не удалось подключиться к облаку. Можно повторить загрузку или выбрать «Играть локально».";
            game.Hud.Toast(message);
        }
    }
}
