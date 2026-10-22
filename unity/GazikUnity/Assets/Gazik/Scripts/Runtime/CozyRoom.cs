using System.Collections.Generic;
using Gazik.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gazik
{
    public sealed class CozyRoom : MonoBehaviour
    {
        public Camera Camera; public Light Sun,Lamp;public Transform Bed;
        public HedgehogActor Resident;Transform contactShadow;float shadowSleepBlend;
        string focus;Vector3 cameraTarget;
        Light moonRim;
        public void Focus(string id){focus=id;}
        readonly Dictionary<string,GameObject> decor=new Dictionary<string,GameObject>(),toys=new Dictionary<string,GameObject>();
        Material wood,cream,sage,rose,gold,cocoa,skyMaterial;Transform clockHand,hourHand,nightOrnaments,daySun,owlLeftWing,owlRightWing;Light starLamp;float nightBlend,playAt=-100;
        public static CozyRoom Create(Transform parent)
        {var o=new GameObject("Woodland cottage");o.transform.SetParent(parent,false);var r=o.AddComponent<CozyRoom>();r.Build();return r;}
        void Build()
        {
            wood=Shapes.Mat("Honey oak","#946D48",.24f,true);cream=Shapes.Mat("Warm plaster","#64745A",.12f);sage=Shapes.Mat("Woven sage","#78856B",.13f,true);
            rose=Shapes.Mat("Knitted dusty rose","#BD8D88",.15f,true);gold=Shapes.Mat("Warm brass","#D3AA61",.5f);cocoa=Shapes.Mat("Dark timber","#69452F",.24f);
            var shell=Shapes.Node("Static cottage geometry",transform,Vector3.zero);var rng=new System.Random(24);
            for(int x=0;x<10;x++)for(int z=0;z<5;z++)
            {float jitter=(float)rng.NextDouble();var mat=Shapes.Mat("Oak variation "+(int)(jitter*4),new[]{"#AE7C50","#B98759","#BA8B61","#AA784C"}[(int)(jitter*4)],.28f,true);Shapes.Box("Oak floorboard",shell,new Vector3((x-4.5f)*.59f,-.075f,(z-2)*1.19f),new Vector3(.58f,.15f,1.18f),mat);}
            Shapes.Box("Back wall",shell,new Vector3(0,1.85f,2.45f),new Vector3(6,3.8f,.2f),cream);
            Shapes.Box("Left wall",shell,new Vector3(-3,1.85f,.35f),new Vector3(.18f,3.8f,4.35f),cream);
            Shapes.Box("Right wall",shell,new Vector3(3,1.85f,.35f),new Vector3(.18f,3.8f,4.35f),cream);
            Shapes.Box("Rear skirting",shell,new Vector3(0,.18f,2.3f),new Vector3(5.9f,.29f,.15f),wood);
            Shapes.Box("Left skirting",shell,new Vector3(-2.86f,.18f,.35f),new Vector3(.14f,.29f,4.2f),wood);
            for(int i=0;i<3;i++)Shapes.Box("Wall timber",shell,new Vector3(-2.8f+i*2.8f,1.87f,2.28f),new Vector3(.13f,3.7f,.14f),wood);
            BuildWindow(shell);
            // Small upholstered bed, blanket and star cushion.
            Bed=Shapes.Node("Bed",shell,new Vector3(-1.66f,0,.85f));
            Shapes.Box("Bed frame",Bed,new Vector3(0,.34f,0),new Vector3(1.55f,.24f,1.16f),wood);
            foreach(float x in new[]{-.61f,.61f})foreach(float z in new[]{-.4f,.4f})Shapes.Cylinder("Bed leg",Bed,new Vector3(x,.17f,z),new Vector3(.12f,.17f,.12f),cocoa);
            Shapes.Box("Mattress",Bed,new Vector3(0,.57f,0),new Vector3(1.45f,.29f,1.06f),sage);
            Shapes.Box("Padded back",Bed,new Vector3(0,.95f,.49f),new Vector3(1.5f,.8f,.16f),sage);
            Shapes.Box("Soft blanket",Bed,new Vector3(.15f,.74f,-.04f),new Vector3(.7f,.045f,1.09f),cream);
            for(int i=0;i<9;i++)Shapes.Rod("Blanket woven stripe",Bed,new Vector3(-.16f+i*.075f,.766f,-.54f),new Vector3(-.16f+i*.075f,.766f,.46f),.008f,i%3==0?rose:sage);
            Shapes.Curve("Mattress piping",Bed,cream,.016f,new Vector3(-.68f,.67f,-.48f),new Vector3(.68f,.67f,-.48f),new Vector3(.68f,.67f,.48f),new Vector3(-.68f,.67f,.48f),new Vector3(-.68f,.67f,-.48f));
            for(int i=0;i<12;i++)Shapes.Rod("Blanket fringe",Bed,new Vector3(-.17f+i*.055f,.72f,-.59f),new Vector3(-.17f+i*.055f,.62f,-.6f),.012f,cream);
            var pillow=Shapes.Star("Rose star cushion",Bed,new Vector3(-.37f,1.01f,.29f),.29f,rose);pillow.transform.localRotation=Quaternion.Euler(-10,0,-12);
            // Circular braided rug with visible rings and stitching.
            var rug=Shapes.Node("Braided rug",shell,new Vector3(0,.023f,-.28f));Shapes.Cylinder("Rug foundation",rug,Vector3.zero,new Vector3(2.95f,.016f,2.45f),sage);
            for(int ring=0;ring<13;ring++){float radius=.16f+ring*.106f;var points=new Vector3[81];for(int i=0;i<points.Length;i++){float a=i*Mathf.PI*2/80;points[i]=new Vector3(Mathf.Cos(a)*radius,.02f,Mathf.Sin(a)*radius*.83f);}Shapes.Curve("Braided loop",rug,ring%4==0?rose:ring%3==0?cream:sage,.027f,points);}
            // Food bowl stays available independently of purchased toys.
            var bowl=Shapes.Node("Apple bowl",shell,new Vector3(-1.9f,.1f,-1.25f));Shapes.Ball("Ceramic bowl",bowl,Vector3.zero,new Vector3(.33f,.13f,.33f),cream);
            Shapes.Ball("Food surface",bowl,new Vector3(0,.105f,0),new Vector3(.25f,.014f,.25f),cocoa);
            for(int i=0;i<9;i++)Shapes.Ball("Kibble",bowl,new Vector3(Mathf.Sin(i*2.4f)*.18f,.12f,Mathf.Cos(i*2.4f)*.18f),new Vector3(.045f,.035f,.04f),wood);
            // Low table and glowing fabric lamp.
            var table=Shapes.Node("Side table",shell,new Vector3(1.95f,0,.9f));Shapes.Box("Table top",table,new Vector3(0,.76f,0),new Vector3(1.1f,.12f,.75f),wood);
            foreach(float x in new[]{-.4f,.4f})Shapes.Box("Table foot",table,new Vector3(x,.36f,0),new Vector3(.12f,.72f,.62f),wood);
            Shapes.Cylinder("Lamp base",table,new Vector3(.05f,.87f,0),new Vector3(.26f,.06f,.26f),cocoa);Shapes.Rod("Lamp neck",table,new Vector3(.05f,.9f,0),new Vector3(.05f,1.25f,0),.065f,gold);
            var linen=Shapes.Mat("Glowing linen","#EBD9B5",.15f,true);linen.EnableKeyword("_EMISSION");linen.SetColor("_EmissionColor",Shapes.Hex("#CBA774")*.22f);
            Shapes.Ball("Linen lampshade",table,new Vector3(.05f,1.4f,0),new Vector3(.29f,.29f,.29f),linen);
            Shapes.Box("Bedtime book",table,new Vector3(-.26f,.85f,-.09f),new Vector3(.35f,.07f,.32f),sage);Shapes.Box("Ivory book pages",table,new Vector3(-.26f,.85f,-.10f),new Vector3(.31f,.037f,.3f),cream);
            var lightObject=new GameObject("Warm lamplight");lightObject.transform.SetParent(transform,false);lightObject.transform.localPosition=new Vector3(2,1.4f,.9f);Lamp=lightObject.AddComponent<Light>();Lamp.type=LightType.Point;Lamp.range=6;Lamp.intensity=1.4f;Lamp.color=Shapes.Hex("#FFD1A0");
            Shapes.Box("Wall shelf",shell,new Vector3(-1.55f,2.38f,2.13f),new Vector3(1.9f,.11f,.44f),wood);
            for(int i=0;i<4;i++){var book=Shapes.Node("Story book",shell,new Vector3(-2.1f+i*.18f,2.60f,2.1f));Shapes.Box("Book cover",book,Vector3.zero,new Vector3(.12f,.36f,.28f),i%2==0?sage:rose);for(int line=0;line<3;line++)Shapes.Box("Gold spine band",book,new Vector3(0,-.1f+line*.10f,-.145f),new Vector3(.08f,.008f,.008f),gold);book.localRotation=Quaternion.Euler(0,0,-6+i*3);}
            Shapes.Cylinder("Wall clock",shell,new Vector3(-.25f,2.82f,2.19f),new Vector3(.43f,.055f,.43f),wood).transform.localRotation=Quaternion.Euler(90,0,0);
            clockHand=Shapes.Node("Clock hands",transform,new Vector3(-.25f,2.82f,2.1f));Shapes.Rod("Minute hand",clockHand,Vector3.zero,new Vector3(0,.13f,0),.014f,cocoa);
            Shapes.Ball("Clock ivory face",shell,new Vector3(-.25f,2.82f,2.12f),new Vector3(.185f,.185f,.012f),cream);
            for(int i=0;i<12;i++){float a=i*Mathf.PI/6;Shapes.Rod("Clock tick",shell,new Vector3(-.25f+Mathf.Sin(a)*.155f,2.82f+Mathf.Cos(a)*.155f,2.095f),new Vector3(-.25f+Mathf.Sin(a)*.172f,2.82f+Mathf.Cos(a)*.172f,2.095f),.008f,cocoa);}
            hourHand=Shapes.Node("Hour hand",transform,new Vector3(-.25f,2.82f,2.085f));Shapes.Rod("Hour",hourHand,Vector3.zero,new Vector3(0,.085f,0),.018f,cocoa);Shapes.Ball("Clock pin",hourHand,Vector3.zero,Vector3.one*.017f,gold);
            BuildPurchases();Shapes.CombineStatic(shell);
            gameObject.AddComponent<ForestInterior>().Build();
            var sunObject=new GameObject("Soft key light");Sun=sunObject.AddComponent<Light>();Sun.type=LightType.Directional;Sun.color=Shapes.Hex("#FFE2BD");Sun.intensity=1.15f;Sun.shadows=LightShadows.Soft;sunObject.transform.rotation=Quaternion.Euler(43,-30,0);
            sunObject.transform.SetParent(transform,true);RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=Shapes.Hex("#B3B3C3");RenderSettings.ambientEquatorColor=Shapes.Hex("#8A776B");RenderSettings.ambientGroundColor=Shapes.Hex("#6C594B");RenderSettings.ambientIntensity=1;
            var rim=new GameObject("Cool moon rim");rim.transform.SetParent(transform,false);rim.transform.localRotation=Quaternion.Euler(30,155,0);moonRim=rim.AddComponent<Light>();moonRim.type=LightType.Directional;moonRim.color=Shapes.Hex("#A4C5D5");moonRim.intensity=.22f;moonRim.shadows=LightShadows.None;QualitySettings.pixelLightCount=2;
            var cameraObject=new GameObject("Main Camera");cameraObject.tag="MainCamera";cameraObject.transform.SetParent(transform,false);Camera=cameraObject.AddComponent<Camera>();Camera.clearFlags=CameraClearFlags.SolidColor;Camera.backgroundColor=Shapes.Hex("#D8C4AD");Camera.fieldOfView=42;Camera.nearClipPlane=.1f;Camera.farClipPlane=35;
            cameraObject.transform.localPosition=new Vector3(.15f,2.23f,-5.15f);cameraObject.transform.LookAt(new Vector3(0,1.1f,.22f));
            cameraObject.AddComponent<AudioListener>();QualitySettings.shadowDistance=10;QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowCascades=0;QualitySettings.shadowResolution=ShadowResolution.Medium;QualitySettings.antiAliasing=4;
            nightBlend=System.DateTime.Now.Hour>=20||System.DateTime.Now.Hour<6?1:0;ApplyTime();
            // Contact shadow, cheap on mobile and independent of cascaded shadow resolution.
            var shadow=GameObject.CreatePrimitive(PrimitiveType.Quad);shadow.name="Soft contact shadow";shadow.transform.SetParent(transform,false);shadow.transform.localPosition=new Vector3(0,.067f,0);shadow.transform.localRotation=Quaternion.Euler(90,0,0);shadow.transform.localScale=new Vector3(1.6f,1.2f,1);Destroy(shadow.GetComponent<Collider>());shadow.GetComponent<Renderer>().sharedMaterial=Shapes.Glow("Contact shadow","#211810",.43f);contactShadow=shadow.transform;
        }
        void BuildWindow(Transform shell)
        {
            var w=Shapes.Node("Arched moonlit window",shell,new Vector3(.98f,1.84f,2.25f));var sky=Shapes.Mat("Night glass","#263C60",.25f);skyMaterial=sky;sky.EnableKeyword("_EMISSION");sky.SetColor("_EmissionColor",Shapes.Hex("#142944")*.5f);
            Shapes.MeshObject("Night sky",w,Shapes.ArchedPane(.77f,-.90f,.42f,.63f),sky,new Vector3(0,0,.015f),Vector3.one);
            for(int s=-1;s<=1;s+=2)Shapes.Rod("Window upright",w,new Vector3(s*.81f,-.91f,-.07f),new Vector3(s*.81f,.42f,-.07f),.105f,wood);
            var arch=new Vector3[25];for(int i=0;i<arch.Length;i++){float a=i*Mathf.PI/24;arch[i]=new Vector3(Mathf.Cos(a)*.81f,.42f+Mathf.Sin(a)*.65f,-.07f);}Shapes.Curve("Arched timber",w,wood,.12f,arch);
            Shapes.Box("Window sill",w,new Vector3(0,-.95f,-.10f),new Vector3(1.85f,.14f,.4f),wood);
            Shapes.Rod("Window cross",w,new Vector3(-.78f,-.13f,-.12f),new Vector3(.78f,-.13f,-.12f),.055f,wood);Shapes.Rod("Window vertical",w,new Vector3(0,-.85f,-.12f),new Vector3(0,1.04f,-.12f),.055f,wood);
            var moon=Shapes.Mat("Moon ivory","#F8EBC5",.2f);moon.EnableKeyword("_EMISSION");moon.SetColor("_EmissionColor",Shapes.Hex("#C5C4AD")*.45f);
            nightOrnaments=Shapes.Node("Night ornaments",transform,new Vector3(.98f,1.84f,2.25f));
            Shapes.Ball("Moon",nightOrnaments,new Vector3(.42f,.45f,-.05f),new Vector3(.17f,.17f,.025f),moon);Shapes.Ball("Moon cutout",nightOrnaments,new Vector3(.47f,.50f,-.085f),new Vector3(.15f,.15f,.027f),sky);
            for(int i=0;i<23;i++){float x=Mathf.Sin(i*17.17f)*.68f,y=Mathf.Repeat(i*.173f,1)*1.45f-.75f;Shapes.Star("Distant star",nightOrnaments,new Vector3(x,y,-.05f),.011f+i%3*.004f,moon);}
            daySun=Shapes.Node("Day sun",transform,new Vector3(1.4f,2.29f,2.20f));Shapes.Ball("Sun disc",daySun,Vector3.zero,new Vector3(.16f,.16f,.024f),moon);
            for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Shapes.Rod("Sun ray",daySun,new Vector3(Mathf.Cos(a)*.20f,Mathf.Sin(a)*.20f,0),new Vector3(Mathf.Cos(a)*.25f,Mathf.Sin(a)*.25f,0),.014f,moon);}
            for(int s=-1;s<=1;s+=2){Shapes.MeshObject("Linen curtain",w,Shapes.DrapedLinen(),cream,new Vector3(s*1.04f,.02f,-.02f),Vector3.one);Shapes.Rod("Curtain rail",w,new Vector3(s*.84f,1.05f,-.05f),new Vector3(s*1.24f,1.05f,-.05f),.035f,wood);}
        }
        void BuildPurchases()
        {
            foreach(string id in new[]{"lamp","plant","rug","pillow","photos","fern","moonwall","mushroomlamp"})decor[id]=Shapes.Node("Purchased "+id,transform,Vector3.zero).gameObject;
            Shapes.Star("Star lamp",decor["lamp"].transform,new Vector3(-2.35f,1.6f,.75f),.24f,gold);Shapes.Rod("Lamp stand",decor["lamp"].transform,new Vector3(-2.35f,0,.75f),new Vector3(-2.35f,1.48f,.75f),.05f,wood);
            var glow=new GameObject("Purchased star light");glow.transform.SetParent(decor["lamp"].transform,false);glow.transform.localPosition=new Vector3(-2.35f,1.6f,.65f);starLamp=glow.AddComponent<Light>();starLamp.type=LightType.Point;starLamp.range=3;starLamp.intensity=.6f;starLamp.color=Shapes.Hex("#FFD8A6");
            var plant=decor["plant"].transform;Shapes.Cylinder("Terracotta pot",plant,new Vector3(2.25f,.26f,-.6f),new Vector3(.36f,.25f,.36f),rose);for(int i=0;i<8;i++){var leaf=Shapes.Ball("Leaf",plant,new Vector3(2.25f+Mathf.Sin(i*2.1f)*.16f,.62f+i*.035f,-.6f+Mathf.Cos(i*2.1f)*.1f),new Vector3(.07f,.26f,.055f),sage);leaf.transform.localRotation=Quaternion.Euler(i*11,0,Mathf.Sin(i)*35);}
            for(int i=0;i<5;i++)Shapes.Ball("Cloud rug",decor["rug"].transform,new Vector3(Mathf.Sin(i*2.1f)*.45f,.052f,-.25f+Mathf.Cos(i*2.1f)*.32f),new Vector3(.8f,.04f,.65f),cream);
            Shapes.Ball("Moon pillow",decor["pillow"].transform,new Vector3(-1.15f,.82f,1.13f),new Vector3(.3f,.25f,.14f),gold);
            Shapes.Curve("Memory garland cord",decor["photos"].transform,cocoa,.012f,new Vector3(-2.48f,2.12f,2.08f),new Vector3(-1.7f,2.04f,2.08f),new Vector3(-.95f,2.12f,2.08f));
            for(int i=0;i<4;i++){Shapes.Box("Memory frame",decor["photos"].transform,new Vector3(-2.28f+i*.38f,1.82f,2.08f),new Vector3(.29f,.33f,.035f),cream);Shapes.Heart("Memory heart",decor["photos"].transform,new Vector3(-2.28f+i*.38f,1.83f,2.049f),.09f,rose);Shapes.Box("Little peg",decor["photos"].transform,new Vector3(-2.28f+i*.38f,2.04f,2.07f),new Vector3(.04f,.12f,.04f),wood);}
            var fern=decor["fern"].transform;Shapes.Cylinder("Fern pot",fern,new Vector3(-2.55f,.23f,-.15f),new Vector3(.35f,.23f,.35f),wood);for(int i=0;i<12;i++){float a=i*2.4f;var frond=Shapes.Ball("Fern frond",fern,new Vector3(-2.55f+Mathf.Sin(a)*.19f,.60f+i%3*.04f,-.15f+Mathf.Cos(a)*.19f),new Vector3(.065f,.33f,.035f),sage);frond.transform.localRotation=Quaternion.Euler(Mathf.Cos(a)*30,0,-Mathf.Sin(a)*35);}
            var crescent=new Vector3[41];for(int i=0;i<crescent.Length;i++){float a=(.25f+i/40f*1.5f)*Mathf.PI;crescent[i]=new Vector3(-1.95f+Mathf.Sin(a)*.25f,1.88f+Mathf.Cos(a)*.25f,2.06f);}Shapes.Curve("Moon wall ornament",decor["moonwall"].transform,gold,.055f,crescent);Shapes.Star("Moon companion",decor["moonwall"].transform,new Vector3(-1.88f,1.9f,2.06f),.07f,cream);
            var mushroom=decor["mushroomlamp"].transform;Shapes.Rod("Mushroom lamp stem",mushroom,new Vector3(-2.45f,0,-1.45f),new Vector3(-2.45f,.38f,-1.45f),.07f,cream);Shapes.Ball("Mushroom lamp cap",mushroom,new Vector3(-2.45f,.40f,-1.45f),new Vector3(.28f,.13f,.28f),gold);Shapes.Ball("Mushroom warm glow",mushroom,new Vector3(-2.45f,.35f,-1.45f),new Vector3(.25f,.04f,.25f),Shapes.Glow("Mushroom lamp glow","#FFDCA1",.5f));
            foreach(string id in new[]{"ball","pig","owl","car","glowtoy"})toys[id]=Shapes.Node("Toy "+id,transform,new Vector3(0,.18f,-1.9f)).gameObject;
            var glowtoy=toys["glowtoy"].transform;Shapes.Ball("Glowing toy body",glowtoy,new Vector3(0,.04f,0),new Vector3(.15f,.21f,.13f),sage);Shapes.Ball("Toy lantern belly",glowtoy,new Vector3(0,-.04f,-.08f),new Vector3(.12f,.12f,.07f),Shapes.Glow("Toy warm glow","#DFF4AC",.8f));for(int side=-1;side<=1;side+=2){Shapes.Ball("Toy wing",glowtoy,new Vector3(side*.15f,.13f,.03f),new Vector3(.10f,.15f,.04f),cream);Shapes.Ball("Toy eye",glowtoy,new Vector3(side*.055f,.17f,-.10f),Vector3.one*.02f,cocoa);}
            Shapes.Ball("Fabric ball",toys["ball"].transform,Vector3.zero,Vector3.one*.23f,rose);Shapes.Ball("Ball stripe",toys["ball"].transform,Vector3.zero,new Vector3(.236f,.08f,.236f),sage);
            var pig=toys["pig"].transform;Shapes.Ball("Plush pig",pig,Vector3.zero,new Vector3(.29f,.20f,.20f),rose);Shapes.Ball("Snout",pig,new Vector3(0,0,-.2f),new Vector3(.12f,.08f,.05f),cream);for(int s=-1;s<=1;s+=2){Shapes.Ball("Pig ear",pig,new Vector3(s*.16f,.16f,-.04f),new Vector3(.07f,.1f,.05f),rose);Shapes.Ball("Pig eye",pig,new Vector3(s*.11f,.06f,-.18f),Vector3.one*.019f,cocoa);}
            var owl=toys["owl"].transform;Shapes.Ball("Owl plush",owl,new Vector3(0,.07f,0),new Vector3(.18f,.27f,.16f),cocoa);for(int s=-1;s<=1;s+=2){Shapes.Ball("Owl eye patch",owl,new Vector3(s*.075f,.14f,-.14f),new Vector3(.085f,.09f,.025f),cream);Shapes.Ball("Owl eye",owl,new Vector3(s*.075f,.14f,-.17f),Vector3.one*.027f,cocoa);}
            owlLeftWing=Shapes.Node("Owl left wing",owl,new Vector3(.15f,.14f,0));owlRightWing=Shapes.Node("Owl right wing",owl,new Vector3(-.15f,.14f,0));Shapes.Ball("Wing",owlLeftWing,new Vector3(.04f,-.09f,0),new Vector3(.055f,.14f,.09f),sage);Shapes.Ball("Wing",owlRightWing,new Vector3(-.04f,-.09f,0),new Vector3(.055f,.14f,.09f),sage);
            var car=toys["car"].transform;Shapes.Box("Toy car body",car,Vector3.zero,new Vector3(.5f,.16f,.27f),sage);Shapes.Box("Toy car cabin",car,new Vector3(.05f,.12f,0),new Vector3(.23f,.14f,.22f),cream);foreach(float x in new[]{-.17f,.17f})foreach(float z in new[]{-.15f,.15f})Shapes.Ball("Wheel",car,new Vector3(x,-.08f,z),new Vector3(.075f,.075f,.032f),cocoa);
            foreach(var group in decor.Values)Shapes.CombineStatic(group.transform);foreach(var group in toys.Values)Shapes.CombineRigid(group.transform);Shapes.CombineRigid(owlLeftWing);Shapes.CombineRigid(owlRightWing);
        }
        public void ReadState(PetState s)
        {foreach(var p in decor)p.Value.SetActive(s.Owns(p.Key)&&!s.List("hiddenDecor").Contains(p.Key));foreach(var p in toys)p.Value.SetActive(s.Equipped("toy")==p.Key);}
        public void React(string action){if(action=="play")playAt=Time.time;}
        void ApplyTime()
        {
            float rest=Resident?Resident.SleepPose:0,darkness=Mathf.Max(nightBlend,rest*.92f);
            if(skyMaterial){skyMaterial.color=Color.Lerp(Shapes.Hex("#BBD6CB"),Shapes.Hex("#263C60"),darkness);skyMaterial.SetColor("_EmissionColor",Color.Lerp(Shapes.Hex("#63847C")*.28f,Shapes.Hex("#142944")*.5f,darkness));}
            if(nightOrnaments)nightOrnaments.gameObject.SetActive(darkness>.45f);if(daySun)daySun.gameObject.SetActive(darkness<=.45f);
            Sun.intensity=Mathf.Lerp(1.3f,.8f,darkness)*Mathf.Lerp(1,.38f,rest);Sun.color=Color.Lerp(Shapes.Hex("#FFE6C4"),Shapes.Hex("#D1D9F0"),darkness);Lamp.intensity=Mathf.Lerp(.8f,1.4f,darkness)*Mathf.Lerp(1,.65f,rest);if(starLamp)starLamp.intensity=Mathf.Lerp(.35f,.75f,darkness);if(moonRim)moonRim.intensity=Mathf.Lerp(.16f,.3f,darkness);
        }
        void Update()
        {
            if(!Camera)return;bool portrait=(float)Screen.width/Screen.height<.8f;Vector3 position=portrait?new Vector3(.12f,2.15f,-6.6f):new Vector3(.15f,2.23f,-5.15f);
            Vector3 lookAt=new Vector3(0,portrait?.82f:1.1f,.22f);float fov=portrait?43:42;
            string shot=Resident&&Resident.IsSleeping?"sleep":focus;
            if(Resident&&!string.IsNullOrEmpty(shot))
            {Vector3 p=transform.InverseTransformPoint(Resident.transform.position);lookAt=p+new Vector3(0,shot=="bath"?.72f:shot=="feed"?.85f:1,0);
                position=p+new Vector3(shot=="brush"?.35f:.05f,shot=="sleep"?3f:1.65f,shot=="play"?-4.5f:shot=="sleep"?-4.8f:portrait?-3.15f:-2.6f);if(shot=="sleep")lookAt=new Vector3(-1.6f,1,.70f);fov=shot=="play"||shot=="sleep"?48:42;}
            float blend=1-Mathf.Exp(-Time.deltaTime*4);Camera.transform.localPosition=Vector3.Lerp(Camera.transform.localPosition,position,blend);cameraTarget=Vector3.Lerp(cameraTarget,lookAt,blend);Camera.transform.LookAt(transform.TransformPoint(cameraTarget));Camera.fieldOfView=Mathf.Lerp(Camera.fieldOfView,fov,blend);
            float target=System.DateTime.Now.Hour>=20||System.DateTime.Now.Hour<6?1:0;nightBlend=Mathf.MoveTowards(nightBlend,target,Time.deltaTime*.2f);ApplyTime();
            float elapsed=Time.time-playAt,envelope=Mathf.Clamp01(1-elapsed/4.2f);
            foreach(var p in toys){var toy=p.Value.transform;Vector3 offset=new Vector3(0,.18f,-1.9f);float bounce=p.Key=="ball"?.30f:p.Key=="owl"?.16f:.055f;offset.y+=Mathf.Abs(Mathf.Sin(elapsed*10))*bounce*envelope;offset.x+=Mathf.Sin(elapsed*3)*(p.Key=="car"?.75f:.22f)*envelope;toy.localPosition=offset;toy.localRotation=Quaternion.Euler(0,p.Key=="ball"?elapsed*110*envelope:0,Mathf.Sin(elapsed*8)*(p.Key=="pig"?12:5)*envelope);}
            owlLeftWing.localRotation=Quaternion.Euler(0,0,Mathf.Sin(elapsed*16)*42*envelope);owlRightWing.localRotation=Quaternion.Euler(0,0,-Mathf.Sin(elapsed*16)*42*envelope);
            if(clockHand)clockHand.localRotation=Quaternion.Euler(0,0,-System.DateTime.Now.Minute*6);
            if(hourHand)hourHand.localRotation=Quaternion.Euler(0,0,-(System.DateTime.Now.Hour%12+System.DateTime.Now.Minute/60f)*30);
        }
        void LateUpdate()
        {
            if(!Resident||!contactShadow)return;shadowSleepBlend=Resident.SleepPose;
            Vector3 position=transform.InverseTransformPoint(Resident.transform.position);position.y=Mathf.Lerp(.067f,.79f,shadowSleepBlend);contactShadow.localPosition=position;
            contactShadow.localScale=new Vector3(Mathf.Lerp(1.6f,1.3f,shadowSleepBlend),Mathf.Lerp(1.2f,.8f,shadowSleepBlend),1);
        }
    }
}
