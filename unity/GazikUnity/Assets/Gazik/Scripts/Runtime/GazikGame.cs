using System;
using System.Collections;
using Gazik.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Gazik
{
    public sealed class GazikGame : MonoBehaviour
    {
        public const string CacheKey="Gazik.Home.v2.Local";
        public const string CloudCacheKey="Gazik.Home.v2.Cloud";
        public GameRules Rules {get;private set;}public HedgehogActor Actor {get;private set;}public CozyRoom Room {get;private set;}
        public GameHud Hud {get;private set;}public GazikCloud Cloud {get;private set;}
        public BrowserBackup Backups {get;private set;}SaveJournal localJournal,cloudJournal;
        public PetLife Life{get;private set;}public CareDirector Care{get;private set;}public ForestSound Sounds{get;private set;}
        #if UNITY_EDITOR
        readonly System.Collections.Generic.Dictionary<string,string> checkStorage=new System.Collections.Generic.Dictionary<string,string>();
        #endif
        public event Action Changed;float nextTick;float petCooldown;bool worldPointer;
        public string Status {get;private set;}="Локальная игра · сохранение на устройстве";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureGame(){if(!FindAnyObjectByType<GazikGame>())new GameObject("Gazik game").AddComponent<GazikGame>();}
        void Awake()
        {
            Application.targetFrameRate=60;Screen.sleepTimeout=SleepTimeout.SystemSetting;
            #if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput=false; // HTML sign-in fields must receive keyboard input.
            #endif
            localJournal=Journal(CacheKey);cloudJournal=Journal(CloudCacheKey);double now=PetState.Now;PetState state=localJournal.Load(now,out bool recovered);
            var catalog=Catalog.Load(Resources.Load<TextAsset>("GazikCatalog").text);Rules=new GameRules(state,catalog){CanWrite=true};
            Room=CozyRoom.Create(transform);Actor=HedgehogActor.Create(transform);Room.Resident=Actor;Actor.ReadState(state,now);Room.ReadState(state);
            var cloudObject=new GameObject("GazikCloud");cloudObject.transform.SetParent(transform,false);Cloud=cloudObject.AddComponent<GazikCloud>();Cloud.Init(this);
            var backupObject=new GameObject("GazikBackup");backupObject.transform.SetParent(transform,false);Backups=backupObject.AddComponent<BrowserBackup>();Backups.Init(this);
            Sounds=gameObject.AddComponent<ForestSound>();Sounds.Init(this);Care=gameObject.AddComponent<CareDirector>();Care.Init(this);Life=gameObject.AddComponent<PetLife>();Life.Init(this);
            Hud=gameObject.AddComponent<GameHud>();Hud.Init(this);
            RecordVisit();SaveProgress();ApplyVisuals();Actor.React("hello");if(recovered)Hud.Toast("Восстановлена резервная копия прогресса.");Hud.PromptNameIfNeeded();
        }
        void Update()
        {
            if(Rules==null)return;
            if(Time.unscaledTime>nextTick){nextTick=Time.unscaledTime+1;ApplyVisuals();Hud.RefreshLive();}
            if(Input.GetKeyDown(KeyCode.Escape)){if(Care.Busy)Care.Cancel();else Hud.ClosePanel();}
            bool touch=Input.touchCount>0;
            Vector3 pointer=touch?(Vector3)Input.GetTouch(0).position:Input.mousePosition;
            bool began=touch?Input.GetTouch(0).phase==TouchPhase.Began:Input.GetMouseButtonDown(0),held=touch?Input.GetTouch(0).phase!=TouchPhase.Ended&&Input.GetTouch(0).phase!=TouchPhase.Canceled:Input.GetMouseButton(0);
            if(began)worldPointer=!Hud.HasPanel&&(!EventSystem.current||!(touch?EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId):EventSystem.current.IsPointerOverGameObject()));
            if(worldPointer){if(Care.Busy)Care.Pointer(pointer,began,held);else if(began)PetAt(pointer);}if(!held)worldPointer=false;
            if(!Life.Moving&&!Care.Busy&&!Hud.HasPanel){Ray gaze=Room.Camera.ScreenPointToRay(pointer);Actor.Look(gaze.GetPoint(4.8f));}
        }
        void PetAt(Vector2 screen)
        {
            if(Time.unscaledTime<petCooldown||Rules.View(PetState.Now).Sleeping(PetState.Now))return;
            if(Physics.Raycast(Room.Camera.ScreenPointToRay(screen),out RaycastHit hit,15))
            {var zone=hit.collider.GetComponent<PetTouchZone>();if(!zone)return;petCooldown=Time.unscaledTime+.8f;Life.Stop();Life.Face(0);Actor.React(zone.Zone=="nose"?(UnityEngine.Random.value<.2f?"sneeze":"sniff"):zone.Zone=="belly"?"tickle":zone.Zone=="quills"?(Rules.View(PetState.Now).Mood<35?"grumble":"shake"):"pet");if(Rules.Bond(PetState.Now)){SaveProgress();Cloud.Save(Rules.State.ToJson());Hud.RefreshLive();}}
        }
        public void DoCare(string id){if(id=="sleep"&&Rules.State.Sleeping(PetState.Now)){Handle(Rules.Wake(PetState.Now));Room.Focus(null);}else Care.Begin(id);}
        public void Shop(string id){if(Care.Busy)return;Handle(Rules.Shop(id,PetState.Now));Sounds.Play("shop");}
        public bool Rename(string name){Result result=Rules.Rename(name,PetState.Now);Handle(result);return result.Ok;}
        public void Reward(string id,int score){Handle(Rules.AwardGame(id,score,PetState.Now));}
        public void TrySurprise()
        {
            #if UNITY_EDITOR
            if(UnityEditor.SessionState.GetBool("Gazik.Smoke",false))return;
            #endif
            var result=Rules.Surprise(PetState.Now);if(result.Ok)Handle(result);
        }
        public void Handle(Result result)
        {
            Hud.Toast(result.Message);if(!result.Ok)return;
            Actor.React(result.Animation);Sounds.Play(result.Animation=="sleep"?"sleep":"reward");
            SaveProgress();Cloud.Save(Rules.State.ToJson());ApplyVisuals();Room.React(result.Animation);Changed?.Invoke();
            if(result.NewLetter)StartCoroutine(LetterToast());
            if(result.Levels>0)StartCoroutine(LevelToast());
        }
        IEnumerator LetterToast(){yield return new WaitForSecondsRealtime(2.5f);Hud.Toast("Новое письмо от меня появилось в почте 💌");}
        IEnumerator LevelToast(){yield return new WaitForSecondsRealtime(1);Hud.Toast("Ёжик подрос!\nТеперь он — "+Rules.Catalog.Stages[GameRules.Stage(Rules.State.Number("growthXp"))]+". И за это ещё +3 ⭐");Actor.React("happy");}
        SaveJournal Journal(string key)
        {
            #if UNITY_EDITOR
            if(UnityEditor.SessionState.GetBool("Gazik.Smoke",false))return new SaveJournal(key,k=>checkStorage.TryGetValue(k,out var v)?v:"",(k,v)=>checkStorage[k]=v,()=>{});
            #endif
            return new SaveJournal(key,k=>PlayerPrefs.GetString(k,""),(k,v)=>PlayerPrefs.SetString(k,v),()=>PlayerPrefs.Save());
        }
        public void SaveLocal(){localJournal.Save(Rules.State);}
        public void SaveProgress()
        {try{if(Cloud&&Cloud.Active)cloudJournal.Save(Rules.State);else SaveLocal();}catch(Exception e){Debug.LogError("Save failed: "+e.Message);if(Hud)Hud.Toast("Не удалось сохранить прогресс на устройстве. Сделай резервную копию.");}}
        public void PrepareCloud(){SaveLocal();PlayerPrefs.SetString(CacheKey+".before-cloud",Rules.State.ToJson());PlayerPrefs.Save();}
        public void ReturnToLocal()
        {Care.Cancel();var state=localJournal.Load(PetState.Now,out bool recovered);Rules.Replace(state,PetState.Now);Rules.CanWrite=true;ApplyVisuals();Changed?.Invoke();Actor.React("hello");if(recovered)Hud.Toast("Восстановлена резервная копия прогресса.");Hud.PromptNameIfNeeded();}
        public void ImportLocal(string json)
        {
            if(Cloud.Active)throw new InvalidOperationException("Сначала выбери «Играть локально». Облачный прогресс не изменён.");
            var state=SaveJournal.Parse(json,PetState.Now);localJournal.Import(json,Rules.State,PetState.Now);Rules.Replace(state,PetState.Now);ApplyVisuals();Changed?.Invoke();Actor.React("hello");Hud.Toast("Резервная копия загружена.");Hud.PromptNameIfNeeded();
        }
        public void ApplyVisuals(){PetState view=Rules.View(PetState.Now);Actor.ReadState(view,PetState.Now);Room.ReadState(view);}
        void RecordVisit()
        {if(!Rules.CanWrite)return;double away=PetState.Now-Rules.State.Number("lastSeenAt",PetState.Now);Rules.State.Set("lastSeenAt",PetState.Now);if(away>4*GameRules.Hour){Actor.React("happy");Hud.Toast(Rules.State.Name+" соскучился и очень рад тебя видеть 💚");}}
        public void StatusText(string text){Status=text;if(Hud)Hud.RefreshLive();}
        public void LoadCloud(PetState state,bool canWrite,bool greet=true)
        {
            Care.Cancel();
            Rules.Replace(state,PetState.Now);Rules.CanWrite=canWrite;cloudJournal.Save(state);
            if(greet)RecordVisit();
            ApplyVisuals();Changed?.Invoke();if(greet){Actor.React("hello");Hud.PromptNameIfNeeded();}
        }
        public void ToggleSound(){Sounds.SetEnabled(!Sounds.Enabled);PlayerPrefs.SetInt("Gazik.Sound",Sounds.Enabled?1:0);PlayerPrefs.Save();Hud.Toast(Sounds.Enabled?"Звуки включены":"Звуки выключены");}
        void OnApplicationPause(bool pause){if(pause&&Rules!=null)SaveProgress();}
        void OnApplicationQuit(){if(Rules!=null)SaveProgress();}
        void OnDestroy(){Shapes.ReleaseResources();UiIcons.ReleaseResources();}
    }
}
