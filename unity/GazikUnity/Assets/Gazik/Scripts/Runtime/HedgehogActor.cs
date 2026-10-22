using System;
using System.Collections.Generic;
using Gazik.Core;
using UnityEngine;

namespace Gazik
{
    public sealed class HedgehogActor : MonoBehaviour
    {
        public Transform Body, Head, LeftArm, RightArm, LeftEar, RightEar, LeftFoot, RightFoot;
        Transform leftEye,rightEye,leftPupil,rightPupil,quills,apple,brush,openMouth;Vector3 leftArmPos,rightArmPos;float blinkAt,blinkUntil,nextIdle;
        readonly Dictionary<string,GameObject> outfits=new Dictionary<string,GameObject>();
        readonly List<Transform> bubbles=new List<Transform>();
        readonly List<Transform> sparks=new List<Transform>();
        readonly List<Transform> hearts=new List<Transform>(),dreams=new List<Transform>();
        LineRenderer smile,leftBrow,rightBrow;Vector3[] mouthPoints=new Vector3[5],browLeft=new Vector3[3],browRight=new Vector3[3];
        public string Action { get; private set; }="hello";
        public bool IsSleeping=>sleeping;
        public float SleepPose=>sleepBlend;
        public bool ManagedMotion,Walking,Interactive;
        public Vector3 MotionPosition;public float Facing;
        public float MouthOpenness;
        public Vector3 MouthWorld=>Head.TransformPoint(new Vector3(0,-.28f,-.57f));
        float actionAt,actionLength=3.5f,stageScale=.8f,energy=90,mood=90,sleepBlend,sleepAt;
        bool sleeping;int growthStage; Vector3 look;Material fur,quillMat,blushMaterial; Color furColor;
        const float HeartSize=.055f,SparkSize=.035f;
        Transform leftCheek,rightCheek,nose;EyeLid lidLeft,lidRight;float blinkDuration=.17f;bool doubleBlink;
        public string Emotion{get;private set;}="calm";
        string followup;
        sealed class EyeLid
        {
            readonly Mesh mesh;readonly Vector3[] vertices=new Vector3[9*17];
            public EyeLid(Transform head,Vector3 point,Material material)
            {var root=Shapes.Node("Soft moving eyelid",head,point);var triangles=new List<int>();for(int y=0;y<8;y++)for(int x=0;x<16;x++){int a=y*17+x,b=a+17;triangles.AddRange(new[]{a,b,a+1,a+1,b,b+1});}mesh=Shapes.Make("Eyelid shutter",new List<Vector3>(vertices),triangles);Shapes.MeshObject("Velvet eyelid",root,mesh,material,Vector3.zero,Vector3.one);Set(1);}
            public void Set(float openness)
            {float close=1-Mathf.Clamp01(openness);for(int y=0;y<=8;y++)for(int x=0;x<=16;x++){float py=Mathf.Lerp(.17f,.17f-close*.34f,y/8f),span=Mathf.Sqrt(Mathf.Max(0,1-py*py/(.17f*.17f)))*.153f,px=(x/16f-.5f)*2*span;vertices[y*17+x]=new Vector3(px,py,-.096f-Mathf.Sqrt(Mathf.Max(0,1-px*px/(.16f*.16f)-py*py/(.18f*.18f)))*.018f);}mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();}
        }
        public static HedgehogActor Create(Transform parent)
        {var o=new GameObject("Газик — animated 3D hedgehog");o.transform.SetParent(parent,false);var a=o.AddComponent<HedgehogActor>();a.Build();return a;}
        void Build()
        {
            fur=Shapes.Mat("Biscuit velvet","#E8C391",.2f,true);furColor=fur.color;quillMat=Shapes.Mat("Warm quills","#C1A281",.24f,true);
            Material cocoa=Shapes.Mat("Cocoa fur","#79533C",.2f,true),cream=Shapes.Mat("Muzzle cream","#F1D5AE",.22f,true),pink=Shapes.Mat("Soft rose","#CC8E83",.27f,true);
            blushMaterial=Shapes.Mat("Emotional blush","#CC8E83",.16f,true);
            Material dark=Shapes.Mat("Nose and smile","#34201D",.85f),iris=Shapes.Mat("Amber eyes","#55311F",.96f),pupil=Shapes.Mat("Glass pupils","#100D0D",1);
            var highlight=Shapes.Mat("Eye catchlight","#FFF8DB",.9f);var pawRose=Shapes.Mat("Paw pads","#C58F87",.22f,true);
            Body=Shapes.Node("Body / breathing",transform,new Vector3(0,.78f,.06f));
            Shapes.Ball("Rounded belly",Body,new Vector3(0,-.06f,-.08f),new Vector3(.59f,.67f,.47f),fur);
            Shapes.MeshObject("Belly velvet fibers",Body,Shapes.Fuzz(new Vector3(.592f,.672f,.472f),1100,222,false),fur,new Vector3(0,-.06f,-.08f),Vector3.one);
            Shapes.MeshObject("Back coat",Body,Shapes.Quills(new Vector3(.57f,.64f,.43f),420,31),quillMat,new Vector3(0,.02f,.09f),Vector3.one);
            Head=Shapes.Node("Head / gaze and emotions",Body,new Vector3(0,.62f,-.12f));
            Shapes.Ball("Head",Head,Vector3.zero,new Vector3(.69f,.57f,.48f),fur);
            Shapes.MeshObject("Soft face fibers",Head,Shapes.Fuzz(new Vector3(.692f,.572f,.482f),1450,323,true),fur,Vector3.zero,Vector3.one);
            quills=Shapes.Node("Sculpted head quills",Head,new Vector3(0,.03f,.04f));
            Shapes.MeshObject("Head coat",quills,Shapes.Quills(new Vector3(.68f,.56f,.45f),480,144),quillMat,Vector3.zero,Vector3.one);
            var tuft=Shapes.MeshObject("Distinctive forehead tuft",Head,Shapes.Quills(new Vector3(.12f,.05f,.05f),14,16),quillMat,new Vector3(0,.51f,-.28f),new Vector3(.75f,.8f,.75f));tuft.transform.localRotation=Quaternion.Euler(-32,0,25);
            foreach(int side in new[]{-1,1})
            {
                var ear=Shapes.Node(side<0?"Right ear":"Left ear",Head,new Vector3(side*.58f,.28f,-.01f));
                Shapes.Ball("Ear outer",ear,Vector3.zero,new Vector3(.19f,.225f,.12f),cocoa);
                Shapes.Ball("Ear velvet",ear,new Vector3(0,0,-.058f),new Vector3(.135f,.165f,.07f),pink);
                if(side<0)RightEar=ear;else LeftEar=ear;
                var eye=Shapes.Node(side<0?"Right eyelid pivot":"Left eyelid pivot",Head,new Vector3(side*.26f,.015f,-.424f));
                Shapes.Ball("Amber eye",eye,Vector3.zero,new Vector3(.14f,.158f,.065f),iris);
                var gaze=Shapes.Node("Pupil gaze",eye,new Vector3(-side*.009f,0,-.041f));
                Shapes.Ball("Pupil",gaze,Vector3.zero,new Vector3(.102f,.125f,.038f),pupil);
                Shapes.Ball("Main catchlight",gaze,new Vector3(-.034f,.057f,-.029f),new Vector3(.031f,.035f,.012f),highlight);
                Shapes.Ball("Small catchlight",gaze,new Vector3(.04f,-.032f,-.032f),new Vector3(.012f,.015f,.008f),highlight);
                if(side<0)rightPupil=gaze;else leftPupil=gaze;
                if(side<0)rightEye=eye;else leftEye=eye;
                var lid=new EyeLid(Head,eye.localPosition,fur);if(side<0)lidRight=lid;else lidLeft=lid;
                var cheek=Shapes.Node("Soft cheek",Head,new Vector3(side*.17f,-.185f,-.41f));Shapes.Ball("Muzzle cheek",cheek,Vector3.zero,new Vector3(.245f,.185f,.13f),cream);if(side<0)rightCheek=cheek;else leftCheek=cheek;
                Shapes.Ball("Cheek blush",Head,new Vector3(side*.45f,-.14f,-.367f),new Vector3(.10f,.035f,.012f),blushMaterial);
                var brow=Shapes.Curve("Brow",Head,cocoa,.019f,new Vector3(side*.37f,.225f,-.405f),new Vector3(side*.27f,.26f,-.44f),new Vector3(side*.17f,.24f,-.425f));if(side>0)leftBrow=brow;else rightBrow=brow;
                var arm=Shapes.Node(side<0?"Right shoulder":"Left shoulder",Body,new Vector3(side*.48f,-.05f,-.04f));
                Shapes.Ball("Short arm",arm,new Vector3(0,-.17f,-.025f),new Vector3(.137f,.26f,.15f),fur);
                Shapes.Ball("Paw",arm,new Vector3(0,-.36f,-.035f),new Vector3(.16f,.12f,.15f),cream);
                Shapes.Ball("Heart paw pad",arm,new Vector3(0,-.34f,-.18f),new Vector3(.061f,.055f,.009f),pawRose);
                for(int i=0;i<3;i++)Shapes.Ball("Little toe",arm,new Vector3((i-1)*.065f,-.41f,-.13f),new Vector3(.045f,.055f,.045f),pink);
                if(side<0)RightArm=arm;else LeftArm=arm;
                var foot=Shapes.Node("Foot pivot",Body,new Vector3(side*.31f,-.66f,-.16f));Shapes.Ball("Hind foot",foot,Vector3.zero,new Vector3(.21f,.13f,.265f),fur);
                for(int i=0;i<3;i++)Shapes.Ball("Hind toe",foot,new Vector3((i-1)*.077f,-.03f,-.23f),new Vector3(.055f,.047f,.08f),pink);
                if(side>0)LeftFoot=foot;else RightFoot=foot;
            }
            nose=Shapes.Node("Sniffing nose",Head,new Vector3(0,-.10f,-.571f));Shapes.Ball("Button nose",nose,Vector3.zero,new Vector3(.112f,.078f,.072f),dark);
            smile=Shapes.Curve("Smile",Head,dark,.013f,new Vector3(-.15f,-.26f,-.512f),new Vector3(-.07f,-.30f,-.54f),new Vector3(0,-.307f,-.55f),new Vector3(.07f,-.30f,-.54f),new Vector3(.15f,-.26f,-.512f));
            Shapes.Curve("Nose seam",Head,dark,.009f,new Vector3(0,-.16f,-.555f),new Vector3(0,-.235f,-.555f),new Vector3(0,-.30f,-.55f));
            openMouth=Shapes.Node("Mouth expression",Head,new Vector3(0,-.28f,-.556f));Shapes.Ball("Open mouth",openMouth,Vector3.zero,new Vector3(.065f,.05f,.012f),dark);Shapes.Ball("Tiny tongue",openMouth,new Vector3(0,-.026f,-.01f),new Vector3(.034f,.018f,.005f),pink);openMouth.gameObject.SetActive(false);
            BuildOutfits();
            apple=Shapes.Node("Animated apple",transform,new Vector3(.8f,.5f,-.4f));
            Shapes.Ball("Apple",apple,Vector3.zero,Vector3.one*.13f,Shapes.Mat("Apple red","#C56552",.48f));Shapes.Rod("Stem",apple,new Vector3(0,.1f,0),new Vector3(.01f,.18f,0),.025f,cocoa);
            brush=Shapes.Node("Animated brush",transform,new Vector3(0,1.9f,0));Shapes.Box("Wood brush",brush,Vector3.zero,new Vector3(.35f,.08f,.14f),cocoa);
            for(int i=0;i<7;i++)Shapes.Rod("Bristle",brush,new Vector3(-.12f+i*.04f,-.03f,0),new Vector3(-.12f+i*.04f,-.12f,0),.012f,cream);
            for(int i=0;i<18;i++){var bubble=Shapes.Ball("Bath bubble",transform,Vector3.zero,Vector3.one*.045f,Shapes.Mat("Bubble pastel","#B6DADD",.95f));bubbles.Add(bubble.transform);bubble.SetActive(false);}
            for(int i=0;i<10;i++){var star=Shapes.Star("Care sparkle",transform,Vector3.zero,SparkSize,Shapes.Mat("Care sparkle gold","#E9C879",.55f));sparks.Add(star.transform);star.SetActive(false);}
            for(int i=0;i<5;i++){var heart=Shapes.Heart("Affection",transform,Vector3.zero,HeartSize,Shapes.Mat("Affection rose","#D48D9A",.3f));hearts.Add(heart.transform);heart.SetActive(false);}
            for(int i=0;i<3;i++){var d=Shapes.Node("Sleep dream",transform,Vector3.zero);Shapes.Curve("Quiet Z",d,highlight,.012f,new Vector3(-.045f,.04f,0),new Vector3(.045f,.04f,0),new Vector3(-.045f,-.04f,0),new Vector3(.045f,-.04f,0));dreams.Add(d);d.gameObject.SetActive(false);}
            apple.gameObject.SetActive(false);brush.gameObject.SetActive(false);
            TouchZone("head",Head,new Vector3(0,.22f,-.21f),new Vector3(1.05f,.52f,.68f));
            TouchZone("nose",Head,new Vector3(0,-.1f,-.60f),new Vector3(.29f,.22f,.18f));
            TouchZone("belly",Body,new Vector3(0,-.12f,-.30f),new Vector3(.85f,.83f,.42f));
            TouchZone("quills",Body,new Vector3(0,.12f,.40f),new Vector3(1.05f,1.3f,.40f));
            leftArmPos=LeftArm.localPosition;rightArmPos=RightArm.localPosition;blinkAt=Time.time+2;nextIdle=Time.time+15;
            SoftCharacterMesh.Build(this,fur);
            foreach(var pivot in new[]{Body,Head,LeftArm,RightArm,LeftEar,RightEar,LeftFoot,RightFoot,leftEye,rightEye,leftPupil,rightPupil,apple,brush})Shapes.CombineRigid(pivot);
            foreach(var outfit in outfits.Values)Shapes.CombineRigid(outfit.transform);
        }
        void TouchZone(string id,Transform parent,Vector3 point,Vector3 size)
        {var o=Shapes.Node("Touch "+id,parent,point).gameObject;o.AddComponent<BoxCollider>().size=size;o.AddComponent<PetTouchZone>().Zone=id;}
        public void ClearLook(){look=Vector3.zero;}
        void BuildOutfits()
        {
            Material green=Shapes.Mat("Scarf sage","#6B9A77",.16f,true),rose=Shapes.Mat("Bow rose","#D591A2",.25f,true),gold=Shapes.Mat("Crown gold","#D9AC54",.62f),blue=Shapes.Mat("Pajamas blue","#727FA4",.2f,true);
            var scarf=Shapes.Node("Green scarf",Body,new Vector3(0,.27f,-.19f));Shapes.Ball("Scarf collar",scarf,Vector3.zero,new Vector3(.52f,.10f,.38f),green);Shapes.Box("Scarf tail",scarf,new Vector3(.31f,-.20f,-.31f),new Vector3(.14f,.45f,.07f),green);outfits["scarf"]=scarf.gameObject;
            var bow=Shapes.Node("Pink bow",Head,new Vector3(.43f,.45f,-.24f));Shapes.Ball("Bow lobe",bow,new Vector3(-.10f,0,0),new Vector3(.12f,.08f,.055f),rose);Shapes.Ball("Bow lobe",bow,new Vector3(.1f,0,0),new Vector3(.12f,.08f,.055f),rose);Shapes.Ball("Bow knot",bow,Vector3.zero,Vector3.one*.065f,rose);outfits["bow"]=bow.gameObject;
            var pajamas=Shapes.Node("Star pajamas",Body,new Vector3(0,-.13f,-.13f));Shapes.Ball("Soft pajamas",pajamas,Vector3.zero,new Vector3(.6f,.48f,.43f),blue);for(int i=0;i<7;i++)Shapes.Star("Sewn star",pajamas,new Vector3(Mathf.Sin(i*2.7f)*.4f,Mathf.Cos(i*1.6f)*.3f,-.4f),.035f,gold);outfits["pajamas"]=pajamas.gameObject;
            var crown=Shapes.Node("Tiny crown",Head,new Vector3(0,.66f,0));Shapes.Cylinder("Crown band",crown,Vector3.zero,new Vector3(.38f,.045f,.38f),gold);for(int i=0;i<5;i++){float a=i*Mathf.PI*2/5;Shapes.Star("Crown point",crown,new Vector3(Mathf.Cos(a)*.15f,.07f,Mathf.Sin(a)*.15f),.085f,gold);}outfits["crown"]=crown.gameObject;
            var bandana=Shapes.Node("Mischievous bandana",Head,Vector3.zero);Shapes.Ball("Bandana fold",bandana,new Vector3(0,.32f,.015f),new Vector3(.63f,.065f,.435f),green);Shapes.Ball("Bandana knot",bandana,new Vector3(.59f,.29f,0),Vector3.one*.08f,green);for(int s=-1;s<=1;s+=2){var tail=Shapes.Box("Bandana tail",bandana,new Vector3(.64f,.17f,s*.07f),new Vector3(.09f,.28f,.045f),green);tail.transform.localRotation=Quaternion.Euler(s*15,0,s*16);}outfits["bandana"]=bandana.gameObject;
            var leafhat=Shapes.Node("Leaf hat",Head,new Vector3(0,.61f,-.02f));Shapes.Ball("Hat leaf",leafhat,Vector3.zero,new Vector3(.17f,.025f,.30f),green);Shapes.Curve("Leaf vein",leafhat,gold,.009f,new Vector3(0,.026f,-.25f),new Vector3(0,.033f,0),new Vector3(0,.026f,.27f));outfits["leafhat"]=leafhat.gameObject;
            var spectacles=Shapes.Node("Round spectacles",Head,Vector3.zero);var frame=Shapes.Mat("Spectacle frame","#745C3A",.5f);for(int side=-1;side<=1;side+=2){var ring=new Vector3[41];for(int i=0;i<ring.Length;i++){float a=i*Mathf.PI*2/40;ring[i]=new Vector3(side*.26f+Mathf.Cos(a)*.16f,.015f+Mathf.Sin(a)*.18f,-.55f);}Shapes.Curve("Glasses rim",spectacles,frame,.016f,ring);Shapes.Rod("Glasses arm",spectacles,new Vector3(side*.42f,.03f,-.54f),new Vector3(side*.58f,.08f,-.05f),.012f,frame);}Shapes.Curve("Nose bridge",spectacles,frame,.014f,new Vector3(-.1f,.035f,-.55f),new Vector3(0,.07f,-.59f),new Vector3(.1f,.035f,-.55f));outfits["glasses"]=spectacles.gameObject;
            var cap=Shapes.Node("Soft moss cap",Head,new Vector3(0,.49f,.04f));Shapes.Ball("Cap crown",cap,Vector3.zero,new Vector3(.47f,.22f,.40f),green);Shapes.Ball("Cap brim",cap,new Vector3(0,-.12f,-.24f),new Vector3(.48f,.07f,.35f),green);Shapes.Ball("Cap pompom",cap,new Vector3(.08f,.23f,0),Vector3.one*.09f,creamForHat());outfits["cap"]=cap.gameObject;
            var cape=Shapes.Node("Guardian cape",Body,new Vector3(0,.06f,.77f));Shapes.Ball("Soft cape cloth",cape,new Vector3(0,-.2f,0),new Vector3(.52f,.56f,.055f),blue);Shapes.Curve("Cape hem",cape,gold,.014f,new Vector3(-.46f,-.47f,-.025f),new Vector3(0,-.74f,-.025f),new Vector3(.46f,-.47f,-.025f));Shapes.Star("Cape embroidery",cape,new Vector3(0,-.22f,.06f),.08f,gold);outfits["cape"]=cape.gameObject;
            foreach(var p in outfits)p.Value.SetActive(false);
        }
        Material creamForHat()=>Shapes.Mat("Cap pompom cream","#D9D5B4",.1f,true);
        public void ReadState(PetState s,double now)
        {bool wasSleeping=sleeping;sleeping=s.Sleeping(now);if(!wasSleeping&&sleeping)sleepAt=Time.time;if(wasSleeping&&!sleeping)React("stretch");energy=(float)s.Stat("energy");mood=(float)s.Mood;growthStage=GameRules.Stage(s.Number("growthXp"));stageScale=GameRules.Scale(growthStage);foreach(var p in outfits)p.Value.SetActive(p.Key==s.Equipped("outfit"));}
        public void React(string action)
        {if(string.IsNullOrEmpty(action))return;Action=action;followup=null;Emotion=action=="hello"||action=="happy"?"happy":action=="dance"?"delighted":action=="pet"?"adoring":action=="wink"?"mischievous":action=="smirk"?"smug":action=="grumble"?"annoyed":action=="yawn"||action=="sleep"?"sleepy":action=="catch"?"excited":action=="play"||action=="tongue"?"playful":action=="feed"?"satisfied":action=="sad"?"hurt":action=="surprise"?"surprised":action=="sigh"?"grumpy":action;actionAt=Time.time;nextIdle=Time.time+UnityEngine.Random.Range(12,21);actionLength=action=="catch"?.85f:action=="hello"?3.2f:action=="play"?4.2f:action=="dance"?4.5f:action=="float"?4.5f:3.1f;}
        public void Look(Vector3 world){look=world;}
        void Update()
        {
            if(!Body)return;float time=Time.time,p=Mathf.Clamp01((time-actionAt)/actionLength),wave=Mathf.Sin(p*Mathf.PI),active=p<1?1:0;
            if(p>=1&&followup!=null&&!sleeping){string next=followup;followup=null;React(next);p=0;wave=0;active=1;}
            if(p>=1)Action="idle";if(sleeping){Action="sleep";active=0;}else if(Action=="idle"&&time>nextIdle){React(energy<35?"yawn":UnityEngine.Random.Range(0f,1f)>.5f?"curious":"stretch");p=0;wave=0;}
            if(Action=="idle")Emotion=energy<20?"tired":energy<35?"sleepy":mood<35?"hurt":"calm";if(sleeping)Emotion="sleepy";
            sleepBlend=Mathf.MoveTowards(sleepBlend,sleeping&&time-sleepAt>1?1:0,Time.deltaTime*.8f);
            Vector3 target=sleeping?new Vector3(-1.48f,sleepBlend*.48f,.70f):Vector3.zero;
            if(!sleeping&&Action=="play")target=new Vector3(Mathf.Sin(p*Mathf.PI*2)*.65f,0,-Mathf.Sin(p*Mathf.PI)*.18f);
            if(!sleeping&&Action=="feed")target=new Vector3(-wave*.35f,0,-wave*.22f);
            if(ManagedMotion)transform.localPosition=MotionPosition;else transform.localPosition=Vector3.Lerp(transform.localPosition,target,1-Mathf.Exp(-Time.deltaTime*2));
            transform.localRotation=ManagedMotion?Quaternion.Slerp(transform.localRotation,Quaternion.Euler(0,Facing,0),1-Mathf.Exp(-Time.deltaTime*7)):Quaternion.Euler(0,Action=="play"?Mathf.Sin(p*Mathf.PI*2)*12*wave:0,0);
            float stride=Action=="play"?Mathf.Sin(p*32)*16*wave:sleeping&&sleepBlend<.3f?Mathf.Sin(time*12)*14*(1-sleepBlend/.3f):0;
            if(Walking)stride=Mathf.Sin(time*11)*24;
            LeftFoot.localRotation=Quaternion.Euler(stride-sleepBlend*35,0,0);RightFoot.localRotation=Quaternion.Euler(-stride-sleepBlend*35,0,0);
            LeftFoot.localPosition=new Vector3(.31f,-.66f+sleepBlend*.55f,-.16f+sleepBlend*.20f);RightFoot.localPosition=new Vector3(-.31f,-.66f+sleepBlend*.55f,-.16f+sleepBlend*.20f);
            LeftFoot.localScale=RightFoot.localScale=Vector3.one*(1-sleepBlend*.25f);
            transform.localScale=Vector3.one*Mathf.Lerp(transform.localScale.x,stageScale,Time.deltaTime*3);
            float breathe=Mathf.Sin(time*(sleeping?1.3f:2.1f))*.009f;
            Body.localPosition=new Vector3(0,.78f+breathe,0.06f);Body.localScale=new Vector3(1,1+breathe,1);
            Body.localScale=Vector3.Scale(Body.localScale,new Vector3(1-sleepBlend*.08f,1-sleepBlend*.17f,1+sleepBlend*.08f));Head.localScale=Vector3.one*Mathf.Lerp(1.035f,.98f,growthStage/4f);
            Body.localRotation=Quaternion.Euler(0,0,Mathf.Sin(time*.7f)*.7f);
            float tired=(100-energy)/100f;Head.localRotation=Quaternion.Euler(sleepBlend*17+tired*3,Mathf.Sin(time*.31f)*3,Mathf.Sin(time*.46f)*2);
            Head.localPosition=new Vector3(0,.62f-sleepBlend*.22f,-.12f);LeftArm.localPosition=leftArmPos;RightArm.localPosition=rightArmPos;
            LeftArm.localRotation=Quaternion.Euler(0,0,8-sleepBlend*65);RightArm.localRotation=Quaternion.Euler(0,0,-8+sleepBlend*65);
            LeftEar.localRotation=Quaternion.Euler(0,0,-4+tired*6+Mathf.Sin(time*1.3f)*1.7f);RightEar.localRotation=Quaternion.Euler(0,0,4-tired*6-Mathf.Sin(time*1.3f)*1.7f);
            if(time>blinkAt){blinkDuration=energy<35?.30f:.17f;blinkUntil=time+blinkDuration;if(doubleBlink){blinkAt=time+UnityEngine.Random.Range(2.8f,6.5f);doubleBlink=false;}else{doubleBlink=UnityEngine.Random.value<.18f;blinkAt=doubleBlink?time+blinkDuration+.12f:time+UnityEngine.Random.Range(2.8f,6.5f);}}float blink=time<blinkUntil?Mathf.Abs(Mathf.Sin((blinkUntil-time)/blinkDuration*Mathf.PI))*.98f:0;
            float eyes=Mathf.Clamp(1-blink-sleepBlend*.97f-(mood<40?.17f:0),.04f,1);leftEye.localScale=rightEye.localScale=new Vector3(1,eyes,1);
            if(Emotion=="tired"||Emotion=="sleepy")leftEye.localScale=rightEye.localScale=new Vector3(1,Mathf.Min(eyes,Emotion=="tired"?.55f:.78f),1);
            float joy=Mathf.Clamp01((mood-35)/60f);if(Action=="pet"||Action=="happy"||Action=="hello")joy=Mathf.Lerp(joy,1,wave);joy=Mathf.Lerp(joy,.55f,sleepBlend);
            if(Action=="grumble"||Action=="angry"||Action=="sad")joy=Mathf.Lerp(joy,.03f,wave);if(Action=="shy"||Action=="proud"||Action=="relaxed")joy=Mathf.Lerp(joy,.9f,wave);
            mouthPoints[0]=new Vector3(-.15f,Mathf.Lerp(-.32f,-.24f,joy),-.512f);mouthPoints[1]=new Vector3(-.07f,-.29f,-.54f);mouthPoints[2]=new Vector3(0,Mathf.Lerp(-.28f,-.31f,joy),-.55f);mouthPoints[3]=new Vector3(.07f,-.29f,-.54f);mouthPoints[4]=new Vector3(.15f,mouthPoints[0].y,-.512f);smile.SetPositions(mouthPoints);
            for(int j=0;j<3;j++){float x=.37f-j*.10f,y=.225f+(j==1?.035f:.015f)+(1-joy)*(j==2?.06f:-.01f);browLeft[j]=new Vector3(x,y,j==1?-.44f:-.415f);browRight[j]=new Vector3(-x,y,j==1?-.44f:-.415f);}leftBrow.SetPositions(browLeft);rightBrow.SetPositions(browRight);
            for(int j=0;j<3;j++){float change=Emotion=="angry"?(j==2?-.09f:.035f)*wave:Emotion=="surprised"?.07f*wave:0;browLeft[j].y+=change;browRight[j].y+=change;if(Emotion=="curious"||Emotion=="smug")browRight[j].y+=.045f*wave;}leftBrow.SetPositions(browLeft);rightBrow.SetPositions(browRight);
            blushMaterial.color=Color.Lerp(Shapes.Hex("#CC8E83"),Shapes.Hex("#DE8488"),Emotion=="shy"?wave:0);
            apple.gameObject.SetActive(!Interactive&&Action=="feed"&&active>0);brush.gameObject.SetActive(!Interactive&&Action=="brush"&&active>0);
            bool mouthOpen=(Action=="yawn"&&wave>.45f)||(Action=="feed"&&wave>.7f&&Mathf.Sin(p*24)>0);openMouth.gameObject.SetActive(mouthOpen);openMouth.localScale=new Vector3(1,Action=="yawn"?1.4f:.65f,1);smile.enabled=!mouthOpen;
            if(Interactive){mouthOpen=MouthOpenness>.1f;openMouth.gameObject.SetActive(mouthOpen);openMouth.localScale=new Vector3(1,MouthOpenness,1);smile.enabled=!mouthOpen;}
            for(int i=0;i<bubbles.Count;i++){bool show=Action=="bath"&&active>0;bubbles[i].gameObject.SetActive(show);if(show){float f=Mathf.Repeat(p*1.5f+i*.071f,1);bubbles[i].localPosition=new Vector3(Mathf.Sin(i*2.4f+p*5)*.7f,.3f+f*1.8f,Mathf.Cos(i*2.4f)*.5f);bubbles[i].localScale=Vector3.one*(.025f+Mathf.Sin(f*Mathf.PI)*.075f);}}
            for(int i=0;i<sparks.Count;i++){bool show=active>0&&(Action=="brush"||Action=="happy"||Action=="dance"||Action=="float"||Action=="rainbow");sparks[i].gameObject.SetActive(show);if(show){float phase=Mathf.Repeat(p+i*.1f,1);sparks[i].localPosition=new Vector3(Mathf.Sin(i*2.7f+p*4)*.8f,.3f+phase*1.8f,Mathf.Cos(i*2.7f)*.6f);sparks[i].localScale=Vector3.one*(SparkSize*(.4f+Mathf.Sin(phase*Mathf.PI)));sparks[i].localRotation=Quaternion.Euler(0,0,p*180+i*36);}}
            for(int i=0;i<hearts.Count;i++){bool show=Action=="pet"&&active>0;hearts[i].gameObject.SetActive(show);if(show){float f=Mathf.Repeat(p+i*.19f,1);hearts[i].localPosition=new Vector3(Mathf.Sin(i*2.4f+p*2)*.62f,1.15f+f*.85f,-.5f);hearts[i].localScale=Vector3.one*(HeartSize*Mathf.Sin(f*Mathf.PI));hearts[i].localRotation=Quaternion.Euler(0,0,Mathf.Sin(i+time)*12);}}
            for(int i=0;i<dreams.Count;i++){dreams[i].gameObject.SetActive(sleepBlend>.8f);float f=Mathf.Repeat(time*.23f+i*.32f,1);dreams[i].localPosition=new Vector3(-.4f+f*.35f,1.35f+f*.55f,-.12f);dreams[i].localScale=Vector3.one*(.5f+Mathf.Sin(f*Mathf.PI)*.65f);}
            float tilt=0,jump=0,spin=0;
            switch(Action)
            {
                case "climb":Head.localRotation*=Quaternion.Euler(wave*10,0,0);LeftArm.localRotation=Quaternion.Euler(95*wave,0,-25*wave);RightArm.localRotation=Quaternion.Euler(95*wave,0,25*wave);LeftFoot.localRotation=Quaternion.Euler(Mathf.Sin(p*16)*wave*28,0,0);RightFoot.localRotation=Quaternion.Euler(-Mathf.Sin(p*16)*wave*28,0,0);break;
                case "sigh":Body.localScale+=new Vector3(wave*.02f,-wave*.035f,wave*.02f);Head.localRotation*=Quaternion.Euler(wave*9,0,0);break;
                case "tired":Head.localRotation*=Quaternion.Euler(wave*15,0,-wave*4);leftEye.localScale=rightEye.localScale=new Vector3(1,1-wave*.55f,1);break;
                case "shy":Head.localRotation*=Quaternion.Euler(wave*12,-wave*12,wave*7);leftEye.localScale=rightEye.localScale=new Vector3(1,1-wave*.45f,1);break;
                case "proud":Head.localRotation*=Quaternion.Euler(-wave*12,0,0);Body.localScale+=Vector3.up*wave*.03f;break;
                case "surprise":Head.localRotation*=Quaternion.Euler(-wave*7,0,0);openMouth.gameObject.SetActive(wave>.3f);smile.enabled=wave<=.3f;break;
                case "angry":Head.localRotation*=Quaternion.Euler(wave*6,Mathf.Sin(p*20)*wave*3,0);break;
                case "sad":Head.localRotation*=Quaternion.Euler(wave*12,0,-wave*7);break;
                case "relaxed":leftEye.localScale=rightEye.localScale=new Vector3(1,1-wave*.95f,1);tilt=wave*5;break;
                case "sneeze":Head.localRotation*=Quaternion.Euler(Mathf.Sin(p*18)*wave*12,0,0);leftEye.localScale=rightEye.localScale=new Vector3(1,1-wave*.96f,1);followup="grumble";break;
                case "wink":leftEye.localScale=new Vector3(1,1-wave*.97f,1);Head.localRotation*=Quaternion.Euler(0,0,7*wave);break;
                case "tongue":openMouth.gameObject.SetActive(wave>.3f);smile.enabled=wave<=.3f;break;
                case "sniff":Head.localPosition+=new Vector3(0,Mathf.Sin(p*35)*.018f,-wave*.035f);break;
                case "smirk":rightEye.localScale=new Vector3(1,1-wave*.45f,1);Head.localRotation*=Quaternion.Euler(-5*wave,-10*wave,8*wave);break;
                case "grumble":Head.localRotation*=Quaternion.Euler(8*wave,Mathf.Sin(p*22)*5*wave,0);RightEar.localRotation*=Quaternion.Euler(0,0,20*wave);break;
                case "tickle":tilt=Mathf.Sin(p*32)*8*wave;LeftArm.localRotation=Quaternion.Euler(-45*wave,0,45*wave);RightArm.localRotation=Quaternion.Euler(-45*wave,0,-45*wave);leftEye.localScale=rightEye.localScale=new Vector3(1,1-wave*.9f,1);openMouth.gameObject.SetActive(wave>.3f);smile.enabled=wave<=.3f;break;
                case "catch":jump=Mathf.Sin(p*Mathf.PI)*.28f;LeftArm.localRotation=Quaternion.Euler(105*wave,0,-55*wave);RightArm.localRotation=Quaternion.Euler(105*wave,0,55*wave);break;
                case "shake":tilt=Mathf.Sin(p*52)*9*wave;quills.localRotation=Quaternion.Euler(0,Mathf.Sin(p*60)*8*wave,0);break;
                case "hello":LeftArm.localRotation=Quaternion.Euler(-18,0,Mathf.Lerp(8,148,wave)+Mathf.Sin(p*23)*15*wave);tilt=-6*wave;break;
                case "pet":tilt=8*wave;Head.localRotation*=Quaternion.Euler(-9*wave,0,0);leftEye.localScale=rightEye.localScale=new Vector3(1,1-wave*.75f,1);break;
                case "curious":Head.localRotation*=Quaternion.Euler(-5*wave,10*wave,12*wave);LeftEar.localRotation*=Quaternion.Euler(0,0,-12*wave);break;
                case "yawn":Head.localRotation*=Quaternion.Euler(-11*wave,0,0);leftEye.localScale=rightEye.localScale=new Vector3(1,1-wave*.8f,1);RightArm.localRotation=Quaternion.Euler(-85*wave,0,30*wave);break;
                case "stretch":LeftArm.localRotation=Quaternion.Euler(0,0,8+wave*135);RightArm.localRotation=Quaternion.Euler(0,0,-8-wave*135);Body.localScale=new Vector3(1-wave*.025f,1+breathe+wave*.045f,1);Head.localRotation*=Quaternion.Euler(-7*wave,0,0);break;
                case "feed":apple.localPosition=Vector3.Lerp(new Vector3(.7f,.45f,-.3f),new Vector3(.08f,1.14f,-.76f),wave);Head.localRotation*=Quaternion.Euler(Mathf.Sin(p*24)*3*wave,0,0);LeftArm.localRotation=Quaternion.Euler(-35*wave,0,-130*wave);LeftArm.localPosition=leftArmPos+new Vector3(-.07f,.12f,-.30f)*wave;break;
                case "play":jump=Mathf.Abs(Mathf.Sin(p*Mathf.PI*4))*.18f*wave;Body.localRotation*=Quaternion.Euler(0,Mathf.Sin(p*15)*17*wave,Mathf.Sin(p*12)*7*wave);break;
                case "bath":tilt=Mathf.Sin(p*48)*4*wave;break;
                case "brush":brush.localPosition=new Vector3(Mathf.Sin(p*12)*.4f,1.95f,-.02f);tilt=6*wave;break;
                case "happy":jump=Mathf.Sin(p*Mathf.PI)*.08f;tilt=Mathf.Sin(p*15)*4*wave;break;
                case "dance":spin=p*720;tilt=Mathf.Sin(p*29)*8*wave;jump=Mathf.Abs(Mathf.Sin(p*18))*.12f*wave;break;
                case "float":jump=wave*.65f;tilt=Mathf.Sin(p*7)*8*wave;break;
                case "zoom":Body.localPosition+=Vector3.right*Mathf.Sin(p*25)*.45f*wave;spin=Mathf.Sin(p*25)*22;break;
                case "rainbow":fur.color=Color.Lerp(furColor,Color.HSVToRGB(Mathf.Repeat(p*2,1),.22f,1),wave);jump=wave*.12f;break;
            }
            if(Action!="rainbow")fur.color=furColor;
            float chewing=Action=="feed"?Mathf.Sin(p*30)*wave:0;leftCheek.localScale=rightCheek.localScale=new Vector3(1+Mathf.Abs(chewing)*.12f,1+chewing*.035f,1+Mathf.Abs(chewing)*.12f);nose.localScale=Vector3.one*(1+(Action=="sniff"?Mathf.Sin(p*35)*.07f*wave:Mathf.Sin(time*1.7f)*.008f));
            lidLeft.Set(leftEye.localScale.y);lidRight.Set(rightEye.localScale.y);
            if(Action!="shake")quills.localRotation=Quaternion.Euler(0,Mathf.Sin(time*.8f)*.6f,0);
            Body.localPosition+=Vector3.up*jump;Body.localRotation*=Quaternion.Euler(sleepBlend*6,spin,tilt+sleepBlend*68);
            Vector3 eyeGaze=Vector3.zero;
            if(look!=Vector3.zero&&!sleeping){Vector3 relative=Head.InverseTransformPoint(look);Head.localRotation*=Quaternion.Euler(Mathf.Clamp(-relative.y*2,-6,6),Mathf.Clamp(relative.x*3,-8,8),0);eyeGaze=new Vector3(Mathf.Clamp(relative.x*.014f,-.022f,.022f),Mathf.Clamp(relative.y*.012f,-.014f,.014f),0);}
            leftPupil.localPosition=Vector3.Lerp(leftPupil.localPosition,new Vector3(-.009f,0,-.041f)+eyeGaze,1-Mathf.Exp(-Time.deltaTime*8));rightPupil.localPosition=Vector3.Lerp(rightPupil.localPosition,new Vector3(.009f,0,-.041f)+eyeGaze,1-Mathf.Exp(-Time.deltaTime*8));
        }
        void OnDestroy(){if(fur)fur.color=furColor;}
    }
}
