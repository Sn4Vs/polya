using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gazik.Editor
{
    public static class BuildCommands
    {
        const string ScenePath="Assets/Gazik/Scenes/GazikHome.unity";
        [InitializeOnLoadMethod]
        static void FirstOpen()
        {
            EditorApplication.playModeStateChanged+=mode=>{if(mode==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=PrepareSearchIndex;};
            EditorApplication.delayCall+=()=>
            {
                PrepareSearchIndex();
                if(EditorApplication.isCompiling||EditorApplication.isPlayingOrWillChangePlaymode||File.Exists(ScenePath))return;
                // Do not discard a scene that the user is already editing.
                if(SceneManager.GetActiveScene().isDirty)return;
                Configure();CreateScene();
                Debug.Log("Газик: проект подготовлен. Нажми Play. Команды сборки: меню Gazik.");
            };
        }
        internal static void PrepareSearchIndex()
        {
            // Unity's default search database cannot be created once Play is starting.
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||File.Exists("UserSettings/Search.index"))return;
            UnityEditor.Search.SearchService.EnumerateDatabases().ToArray();
        }
        [MenuItem("Gazik/1. Open home scene")]
        public static void OpenHome()
        {
            TryOpenHome();
        }
        public static bool TryOpenHome()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return false;
            Configure();if(!File.Exists(ScenePath))CreateScene();else EditorSceneManager.OpenScene(ScenePath);return true;
        }
        static void CreateScene()
        {
            Directory.CreateDirectory("Assets/Gazik/Scenes");var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Газик — press Play").AddComponent<GazikGame>();EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};AssetDatabase.SaveAssets();
        }
        static void Configure()
        {
            PlayerSettings.companyName="ForZvezdochka";PlayerSettings.productName="Газик — уютный домик";PlayerSettings.bundleVersion="0.4.0";
            PlayerSettings.defaultScreenWidth=1080;PlayerSettings.defaultScreenHeight=1920;PlayerSettings.colorSpace=ColorSpace.Linear;
            PlayerSettings.defaultWebScreenWidth=1080;PlayerSettings.defaultWebScreenHeight=1920;
            PlayerSettings.WebGL.template="PROJECT:GazikMobile";PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback=false;PlayerSettings.runInBackground=false;
            PlayerSettings.WebGL.initialMemorySize=64;PlayerSettings.WebGL.maximumMemorySize=512;
            // This self-contained project intentionally uses the built-in render pipeline.
            UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline=null;
            QualitySettings.renderPipeline=null;
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
        }
        [MenuItem("Gazik/2. Build WebGL")]
        public static void BuildWebGL()
        {
            Configure();if(!File.Exists(ScenePath))CreateScene();
            if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL,BuildTarget.WebGL))throw new InvalidOperationException("Установи Web Build Support для этой версии Unity через Unity Hub.");
            Directory.CreateDirectory("Builds/WebGL");BuildReport report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/WebGL",target=BuildTarget.WebGL,options=BuildOptions.None});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("WebGL build failed: "+report.summary.result);
            File.WriteAllText("Builds/last-successful-build.txt",DateTime.UtcNow.ToString("O")+"\n"+report.summary.totalSize+" bytes\n");
            Debug.Log("Газик: WebGL-сборка готова в Builds/WebGL. Она не опубликована автоматически.");
        }
        [MenuItem("Gazik/3. Capture game screenshot")]
        public static void Screenshot()
        {
            if(!EditorApplication.isPlaying){Debug.LogWarning("Сначала нажми Play и дождись появления Газика.");return;}
            Directory.CreateDirectory("Screenshots");ScreenCapture.CaptureScreenshot("Screenshots/Gazik-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".png",2);
        }
        [MenuItem("Gazik/4. Export local save backup")]
        public static void ExportSave()
        {
            string path=EditorUtility.SaveFilePanel("Сохранить резервную копию","","Gazik-backup.json","json");if(string.IsNullOrEmpty(path))return;
            File.WriteAllText(path,PlayerPrefs.GetString(GazikGame.CacheKey,"{}"));
        }
        [MenuItem("Gazik/5. Import local save backup")]
        public static void ImportSave()
        {
            if(EditorApplication.isPlaying){Debug.LogWarning("Останови Play перед импортом.");return;}
            string path=EditorUtility.OpenFilePanel("Выбрать резервную копию","","json");if(string.IsNullOrEmpty(path))return;
            var journal=new Core.SaveJournal(GazikGame.CacheKey,k=>PlayerPrefs.GetString(k,""),(k,v)=>PlayerPrefs.SetString(k,v),()=>PlayerPrefs.Save());
            var current=journal.Load(Core.PetState.Now,out _);journal.Import(File.ReadAllText(path),current,Core.PetState.Now);
        }
    }
}
