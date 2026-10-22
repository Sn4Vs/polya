using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Gazik.Editor
{
    [InitializeOnLoad]
    public static class EditorChecks
    {
        [Serializable]sealed class Report{public bool passed;public string utc,unity,error,screenshot;public bool graphicsAvailable;public List<string> checks=new List<string>();}
        const string Flag="Gazik.Smoke";static double started;static bool running,captured,captureIssued,scheduled;static Report report;
        static NativeCareChecks careChecks;
        static double exitAt;static bool exitPassed;
        static EditorChecks(){EditorApplication.playModeStateChanged+=ModeChanged;if(SessionState.GetBool(Flag+".Requested",false)){scheduled=true;EditorApplication.update+=BeginWhenReady;}}
        [MenuItem("Gazik/6. Run game smoke check")]
        public static void Run()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Останови Play перед проверкой.");
            if(scheduled)return;scheduled=true;SessionState.SetBool(Flag+".Requested",true);EditorApplication.update+=BeginWhenReady;Debug.Log("Gazik QA: waiting for editor readiness.");
        }
        static void BeginWhenReady()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            EditorApplication.update-=BeginWhenReady;scheduled=false;SessionState.SetBool(Flag+".Requested",false);BuildCommands.PrepareSearchIndex();Debug.Log("Gazik QA: opening native Play Mode checks.");
            if(!BuildCommands.TryOpenHome())return;
            if(File.Exists("TestResults/Unity-smoke.json"))File.Copy("TestResults/Unity-smoke.json","TestResults/Unity-smoke-previous-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")+".json");
            if(!Application.isBatchMode)
            {
                var gameViewType=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                if(gameViewType==null)throw new Exception("Unity Game View type is unavailable.");
                var view=(EditorWindow)ScriptableObject.CreateInstance(gameViewType);view.titleContent=new GUIContent("Gazik QA Game");view.ShowUtility();
                bool portrait=Environment.GetCommandLineArgs().Contains("-gazikPortrait");view.position=new Rect(80,65,portrait?620:1240,portrait?950:850);view.Focus();
                SetCaptureResolution(gameViewType,portrait?1080:1440,portrait?1920:1000);
            }
            report=null;SessionState.SetBool(Flag,true);SessionState.SetBool(Flag+".Done",false);EditorApplication.isPlaying=true;
        }
        static void SetCaptureResolution(Type viewType,int width,int height)
        {
            var asm=typeof(EditorWindow).Assembly;var sizesType=asm.GetType("UnityEditor.GameViewSizes");var groupType=asm.GetType("UnityEditor.GameViewSizeGroupType");var sizeType=asm.GetType("UnityEditor.GameViewSize");var kindType=asm.GetType("UnityEditor.GameViewSizeType");
            const BindingFlags members=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            var sizes=sizesType.GetProperty("instance",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy).GetValue(null);
            var group=sizesType.GetMethod("GetGroup",members).Invoke(sizes,new[]{Enum.Parse(groupType,"Standalone")});
            var size=Activator.CreateInstance(sizeType,members,null,new object[]{Enum.Parse(kindType,"FixedResolution"),width,height,"Gazik QA "+width+"×"+height},null);
            group.GetType().GetMethod("AddCustomSize",members).Invoke(group,new[]{size});int index=(int)group.GetType().GetMethod("GetTotalCount",members).Invoke(group,null)-1;
            foreach(var window in Resources.FindObjectsOfTypeAll(viewType)){viewType.GetProperty("selectedSizeIndex",members).SetValue(window,index);((EditorWindow)window).Repaint();}
        }
        static void ModeChanged(PlayModeStateChange mode)
        {
            if(!SessionState.GetBool(Flag,false))return;
            if(mode==PlayModeStateChange.EnteredPlayMode){started=EditorApplication.timeSinceStartup;running=true;captured=captureIssued=false;EditorApplication.update+=Tick;}
            if(mode==PlayModeStateChange.EnteredEditMode)
            {bool ok=SessionState.GetBool(Flag+".Done",false)&&SessionState.GetBool(Flag+".Passed",false);running=false;report=null;EditorApplication.update-=Tick;SessionState.SetBool(Flag,false);if(Application.isBatchMode||Environment.GetCommandLineArgs().Contains("-gazikQaExit")){exitPassed=ok;exitAt=EditorApplication.timeSinceStartup+2;EditorApplication.update+=ExitWhenClean;}}
        }
        static void ExitWhenClean(){if(EditorApplication.timeSinceStartup<exitAt)return;EditorApplication.update-=ExitWhenClean;EditorApplication.Exit(exitPassed?0:1);}
        static void Require(bool condition,string name){if(!condition)throw new Exception(name);report.checks.Add(name);}
        static void Tick()
        {
            if(!running)return;
            if(careChecks!=null)
            {
                try{if(!careChecks.Tick())return;report.passed=true;}catch(Exception error){report.passed=false;report.error=error.ToString();Debug.LogException(error);}
                careChecks=null;Directory.CreateDirectory("TestResults");File.WriteAllText("TestResults/Unity-smoke.json",JsonUtility.ToJson(report,true));
                if(report.graphicsAvailable&&!Application.isBatchMode&&report.passed){report.screenshot="TestResults/Unity-game-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")+".png";captured=true;started=EditorApplication.timeSinceStartup;return;}
            }
            if(report==null&&EditorApplication.timeSinceStartup-started<1.5)return;
            if(report==null)
            {
                report=new Report{utc=DateTime.UtcNow.ToString("O"),unity=Application.unityVersion,graphicsAvailable=SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null};
                try
                {
                    var game=UnityEngine.Object.FindAnyObjectByType<GazikGame>();Require(game&&game.Rules!=null&&game.Hud&&game.Actor&&game.Room,"Game, rules, room, actor and HUD boot");
                    Require(game.Cloud&&!game.Cloud.Active,"Native QA stays local without touching Firebase");
                    Require(Resources.Load<Font>("GazikUI")!=null,"Embedded UI font imported");
                    var glyphFont=Resources.Load<Font>("GazikUI");string originalEmoji="Газик 💚 💌",displayEmoji=EmojiGlyphs.Render(originalEmoji);
                    Require(originalEmoji=="Газик 💚 💌"&&displayEmoji.Length==originalEmoji.Length-2,"Emoji display aliases preserve original Unicode strings");
                    Require(displayEmoji.Where(c=>c>=0xe000&&c<=0xf8ff).All(c=>glyphFont.HasCharacter(c)),"Supplementary emoji aliases have imported font glyphs");
                    var glyphProbe=game.Hud.Label(game.Hud.Root,"💚 💌",32);Canvas.ForceUpdateCanvases();glyphProbe.cachedTextGenerator.Populate(glyphProbe.text,glyphProbe.GetGenerationSettings(new Vector2(500,100)));
                    Require(glyphProbe.cachedTextGenerator.vertexCount>=8,"Heart and mail symbols generate visible UI geometry");UnityEngine.Object.Destroy(glyphProbe.gameObject);
                    var shader=Resources.Load<Shader>("GazikCoat");Require(shader!=null,"Coat shader imported");if(report.graphicsAvailable)Require(shader.isSupported,"Coat shader supported by current graphics device");
                    var meshes=game.GetComponentsInChildren<MeshFilter>().Where(m=>m.GetComponent<MeshRenderer>()&&m.GetComponent<MeshRenderer>().enabled).ToArray();
                    Require(meshes.Length>20&&meshes.All(m=>m.sharedMesh&&m.sharedMesh.vertexCount>0),"Visible procedural meshes and rigid batches contain geometry");
                    var actorType=typeof(HedgehogActor);var update=actorType.GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance);
                    foreach(string action in new[]{"pet","brush"})
                    {game.Actor.React(action);update.Invoke(game.Actor,null);foreach(string fieldName in new[]{"hearts","sparks"})foreach(var effect in (List<Transform>)actorType.GetField(fieldName,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(game.Actor))if(effect.gameObject.activeSelf)Require(Vector3.Distance(effect.TransformPoint(Vector3.left),effect.TransformPoint(Vector3.right))<Vector3.Distance(game.Actor.Head.TransformPoint(Vector3.left*.69f),game.Actor.Head.TransformPoint(Vector3.right*.69f))*.1f,"Care effect remains below 10% of head width: "+action+" "+effect.name);}
                    var skin=game.Actor.GetComponentInChildren<SkinnedMeshRenderer>();Require(skin&&skin.sharedMesh.boneWeights.Length==skin.sharedMesh.vertexCount&&skin.bones.Length==6,"Unified character surface is skinned to six bones");
                    Require(skin.sharedMesh.boneWeights.All(w=>Mathf.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1)<.0001f),"Every skin vertex has normalized bone weights");
                    Require(skin.sharedMesh.vertices.All(v=>!float.IsNaN(v.x)&&!float.IsNaN(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.x)&&!float.IsInfinity(v.y)&&!float.IsInfinity(v.z)),"Unified surface has no nonfinite vertices");
                    CheckInventory(game);
                    if(Screen.height>Screen.width)
                    {Canvas.ForceUpdateCanvases();var corners=new Vector3[4];foreach(var button in game.Hud.Root.GetComponentsInChildren<Button>()){var rect=button.GetComponent<RectTransform>();rect.GetWorldCorners(corners);float cssHeight=Vector3.Distance(corners[0],corners[1])*360/Screen.width;Require(cssHeight>=43.9f,"Portrait home touch target is at least 44 CSS px at 360px width: "+button.name);}}
                    game.Rules.State.Stat("hunger",50);var feed=game.Hud.Root.GetComponentsInChildren<Button>().First(b=>b.name=="Покормить");string before=game.Rules.State.ToJson();feed.onClick.Invoke();
                    Require(game.Care.Busy&&before==game.Rules.State.ToJson(),"Care button starts a real approach without instant rewards");
                    game.Care.Cancel();Require(before==game.Rules.State.ToJson(),"Aborted care grants no reward and changes no progress");
                    game.Hud.ShowGames();Require(game.Hud.Root.GetComponentsInChildren<Button>().Count(b=>b.name=="Играть")==3,"All three arcade entries are available");
                    game.Hud.Root.GetComponentsInChildren<Button>().First(b=>b.name=="Играть").onClick.Invoke();Require(game.Hud.GetComponent<ArcadeView>()!=null,"Arcade starts from UI");game.Hud.ClosePanel();
                    Require(game.Rules.State.Map("gameRewards").Count==0,"Closing an unfinished game grants no reward");
                    game.Rules.State.Set("name","");game.Hud.PromptNameIfNeeded();var input=game.Hud.Root.GetComponentInChildren<InputField>();Require(input!=null,"Legacy empty-name save opens naming form");input.text="Луна";
                    typeof(GameHud).GetMethod("Rebuild",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(game.Hud,null);
                    Require(game.Hud.Root.GetComponentInChildren<InputField>()==input&&input.text=="Луна","Responsive rebuild retains live input field and typed name");
                    game.Hud.Root.GetComponentsInChildren<Button>().First(b=>b.name=="Сохранить").onClick.Invoke();Require(game.Rules.State.Name=="Луна","Name saves through UI");
                    game.Hud.ShowDaily();Require(game.Hud.Root.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("+8")),"Daily panel shows reward and care checklist");game.Hud.ClosePanel();
                    game.Rename("Газик");careChecks=new NativeCareChecks(game,Require);return;
                }
                catch(Exception error){report.error=error.ToString();report.passed=false;Debug.LogException(error);}
                Directory.CreateDirectory("TestResults");File.WriteAllText("TestResults/Unity-smoke.json",JsonUtility.ToJson(report,true));
                // Batch mode has no rendered Game View even when a graphics device exists.
                if(report.graphicsAvailable&&!Application.isBatchMode&&report.passed){report.screenshot="TestResults/Unity-game-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")+".png";captured=true;started=EditorApplication.timeSinceStartup;return;}
            }
            if(captured&&!captureIssued){if(EditorApplication.timeSinceStartup-started<5.25)return;Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(report.screenshot,1);captureIssued=true;started=EditorApplication.timeSinceStartup;return;}
            if(captured&&EditorApplication.timeSinceStartup-started<1)return;
            if(captured)
            {
                if(!File.Exists(report.screenshot)||new FileInfo(report.screenshot).Length<256)
                {if(EditorApplication.timeSinceStartup-started<10)return;report.passed=false;report.error="Unity did not produce the requested game screenshot.";}
                else report.checks.Add("Native Unity game screenshot written");
                File.WriteAllText("TestResults/Unity-smoke.json",JsonUtility.ToJson(report,true));
            }
            running=false;EditorApplication.update-=Tick;SessionState.SetBool(Flag+".Passed",report.passed);SessionState.SetBool(Flag+".Done",true);report=null;
            EditorApplication.isPlaying=false;
        }
        static void CheckInventory(GazikGame game)
        {
            var previous=game.Rules.State.Clone();game.Rules.State.Set("stars",10000);game.Rules.State.Set("growthXp",280);
            var outfits=(Dictionary<string,GameObject>)typeof(HedgehogActor).GetField("outfits",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(game.Actor);
            var decor=(Dictionary<string,GameObject>)typeof(CozyRoom).GetField("decor",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(game.Room);
            var toys=(Dictionary<string,GameObject>)typeof(CozyRoom).GetField("toys",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(game.Room);
            foreach(var item in game.Rules.Catalog.Shop)
            {
                game.Shop(item.Id);double stars=game.Rules.State.Number("stars");
                if(item.Category=="outfit")Require(outfits.ContainsKey(item.Id)&&outfits[item.Id].activeSelf&&outfits.Count(p=>p.Value.activeSelf)==1,"Purchased outfit has an exclusive visible model: "+item.Id);
                if(item.Category=="toy")Require(toys.ContainsKey(item.Id)&&toys[item.Id].activeSelf&&toys.Count(p=>p.Value.activeSelf)==1,"Selected toy has an exclusive visible model: "+item.Id);
                if(item.Category=="decor"){Require(decor.ContainsKey(item.Id)&&decor[item.Id].activeSelf,"Purchased decor has visible geometry: "+item.Id);game.Shop(item.Id);Require(!decor[item.Id].activeSelf&&stars==game.Rules.State.Number("stars"),"Decor can be stored without paying again: "+item.Id);game.Shop(item.Id);Require(decor[item.Id].activeSelf&&stars==game.Rules.State.Number("stars"),"Stored decor can be placed again: "+item.Id);}
                if(item.Category=="treat"){Require(Core.PetState.N(game.Rules.State.Map("consumables"),item.Id)==1,"Treat purchase is stored: "+item.Id);game.Shop(item.Id);Require(Core.PetState.N(game.Rules.State.Map("consumables"),item.Id)==0&&stars==game.Rules.State.Number("stars"),"Stored treat is used without another charge: "+item.Id);}
            }
            game.Rules.Replace(previous,Core.PetState.Now);game.ApplyVisuals();game.Hud.RefreshLive();
        }
    }
}
