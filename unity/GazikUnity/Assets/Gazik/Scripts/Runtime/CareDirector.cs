using System.Collections.Generic;
using Gazik.Core;
using UnityEngine;

namespace Gazik
{
    public sealed class CareDirector:MonoBehaviour
    {
        enum Step{None,Walking,Settling,Ready,FoodFlight,Chewing,Finishing}
        GazikGame game;Step step;string action;float since,lastStroke,soundAt;Vector3 previousStroke;
        Transform props,tool,food;Vector3 foodFrom;int foodChoice,caught;float cleaned;
        float lastMiss=-10;bool celebrated;
        readonly List<Transform> targets=new List<Transform>();readonly HashSet<int> cleared=new HashSet<int>();
        readonly List<Transform> soapBubbles=new List<Transform>();
        Mesh foam;readonly Dictionary<int,Vector3> foamCenters=new Dictionary<int,Vector3>();readonly Dictionary<int,List<int>> foamCells=new Dictionary<int,List<int>>();const int FoamCols=16,FoamRows=12;
        public bool Busy=>step!=Step.None;
        public string Current=>action;
        public bool Ready=>step==Step.Ready;
        public float Progress=>action=="play"?caught/5f:action=="brush"?cleared.Count/18f:cleaned;
        public string Instruction=>step==Step.Walking?"Газик идёт к своему месту…":step==Step.FoodFlight||step==Step.Chewing?"Ням-ням…":step==Step.Finishing?"Вот и готово!":action=="feed"?"Коснись еды — Газик её попробует":action=="bath"?"Води пальцем по пене на пузике":action=="brush"?"Проводи щёткой по листикам на иголках":action=="play"?"Лови светлячков касанием · "+caught+" / 5":"Устраиваемся в лежанке…";
        public void Init(GazikGame value){game=value;}
        public void Begin(string id)
        {
            if(Busy){game.Hud.Toast("Сначала закончим эту заботу.");return;}
            var allowed=game.Rules.CanStartCare(id,PetState.Now);if(!allowed.Ok){game.Hud.Toast(allowed.Message);return;}
            game.Hud.ClosePanel();action=id;step=Step.Walking;since=Time.time;cleared.Clear();targets.Clear();caught=0;cleaned=0;celebrated=false;
            game.Actor.Interactive=true;game.Actor.React("curious");
            props=Shapes.Node("Interactive "+id,game.transform,Vector3.zero);
            Vector3 point=id=="feed"?new Vector3(-1.25f,0,-.72f):id=="bath"?new Vector3(1.65f,0,-1.35f):id=="brush"?new Vector3(.25f,0,.65f):id=="sleep"?new Vector3(-1.4f,0,-.48f):new Vector3(0,0,-.5f);
            game.Life.Go(point,Arrived);game.Hud.RefreshLive();
        }
        void Arrived(){if(!Busy)return;step=Step.Settling;since=Time.time;game.Life.Face(action=="brush"?150:0);if(action=="sleep"||action=="bath")game.Actor.React("climb");}
        void Prepare()
        {
            step=Step.Ready;since=Time.time;var actor=game.Actor;
            if(action=="sleep"){Complete();return;}
            game.Room.Focus(action);actor.React(action=="brush"?"grumble":action=="bath"?"curious":"smirk");
            if(action=="feed")
            {
                for(int i=0;i<4;i++)
                {var o=Shapes.Node("Food choice "+i,props,Vector3.zero);o.position=actor.transform.TransformPoint(new Vector3((i-1.5f)*.38f,.32f,-1.05f));
                    var mat=Shapes.Mat("Food choice "+i,new[]{"#CD6052","#6878A0","#D6B774","#9C764C"}[i],.35f);
                    if(i==2)Shapes.Star("Star biscuit",o,Vector3.zero,.16f,mat);else Shapes.Ball(i==0?"Apple":"Forest berry",o,Vector3.zero,Vector3.one*.14f,mat);
                    Shapes.Rod("Fruit stalk",o,new Vector3(0,.12f,0),new Vector3(.025f,.20f,0),.018f,Shapes.Mat("Fruit stalk","#634F30"));
                    o.gameObject.AddComponent<SphereCollider>().radius=.19f;o.gameObject.AddComponent<CareTarget>().Index=i;targets.Add(o);}
            }
            else if(action=="bath")
            {
                game.Sounds.Play("water",.7f);
                for(int i=0;i<6;i++)soapBubbles.Add(Shapes.Ball("Small soap bubble",props,Vector3.zero,Vector3.one*(.018f+i%3*.005f),Shapes.Glow("Soap bubble glow","#D6EEE8",.35f)).transform);
                BuildFoam();tool=Shapes.Node("Drag sponge",props,Vector3.zero);Shapes.Box("Soft sponge",tool,Vector3.zero,new Vector3(.24f,.12f,.16f),Shapes.Mat("Sponge","#D8C786",.1f,true));
                for(int i=0;i<15;i++)Shapes.Ball("Sponge pores",tool,new Vector3(Mathf.Sin(i*2.4f)*.095f,.0605f,Mathf.Cos(i*2.4f)*.058f),new Vector3(.008f,.0015f,.006f),Shapes.Mat("Sponge pores","#A08C48"));tool.position=actor.Body.TransformPoint(new Vector3(.42f,-.12f,-.65f));
            }
            else if(action=="brush")
            {
                tool=Shapes.Node("Drag quill brush",props,Vector3.zero);var timber=Shapes.Mat("Brush handle","#8A6541");
                Shapes.Box("Brush back",tool,Vector3.zero,new Vector3(.30f,.07f,.14f),timber);Shapes.Rod("Handle",tool,new Vector3(.12f,0,0),new Vector3(.33f,0,0),.035f,timber);
                for(int i=0;i<12;i++)Shapes.Rod("Soft bristle",tool,new Vector3(-.12f+(i%6)*.045f,-.02f,(i/6-.5f)*.07f),new Vector3(-.12f+(i%6)*.045f,-.12f,(i/6-.5f)*.07f),.008f,Shapes.Mat("Bristles","#D7C9A0"));
                for(int i=0;i<18;i++){var leaf=Shapes.Node("Quill debris "+i,actor.Body,new Vector3(Mathf.Sin(i*2.4f)*.43f,-.25f+(i/6)*.24f,.39f+Mathf.Cos(i*2.4f)*.07f));var mat=Shapes.Mat("Fallen leaves "+i%3,new[]{"#A0AA55","#AF8044","#7B8E51"}[i%3]);Shapes.Ball("Leaf",leaf,Vector3.zero,new Vector3(.07f,.026f,.10f),mat);Shapes.Rod("Leaf vein",leaf,new Vector3(0,.028f,-.075f),new Vector3(0,.028f,.075f),.006f,timber);leaf.localRotation=Quaternion.Euler(20,i*29,0);targets.Add(leaf);}
                tool.position=actor.Body.TransformPoint(new Vector3(.5f,0,.55f));
            }
            else if(action=="play")
            {
                game.Room.React("play");
                for(int i=0;i<7;i++){var fly=Shapes.Node("Catchable firefly "+i,props,Vector3.zero);Shapes.Ball("Firefly glow",fly,Vector3.zero,Vector3.one*.055f,Shapes.Glow("Living firefly","#D7F5A0",.9f));Shapes.Ball("Firefly core",fly,Vector3.zero,Vector3.one*.025f,Shapes.Mat("Firefly core","#FFF4BF"));for(int side=-1;side<=1;side+=2)Shapes.Ball("Firefly wing",fly,new Vector3(side*.04f,.015f,0),new Vector3(.035f,.012f,.06f),Shapes.Glow("Firefly wing","#A5D9B2",.4f));fly.gameObject.AddComponent<SphereCollider>().radius=.23f;fly.gameObject.AddComponent<CareTarget>().Index=i;var trail=fly.gameObject.AddComponent<TrailRenderer>();trail.sharedMaterial=Shapes.Glow("Firefly trail","#B9E594",.35f);trail.time=.22f;trail.startWidth=.025f;trail.endWidth=0;trail.minVertexDistance=.025f;targets.Add(fly);}
            }
        }
        void Update()
        {
            if(!Busy)return;
            if(!game.Rules.CanWrite){Cancel();return;}
            if(step==Step.Settling)
            {
                float t=Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.time-since)/1.2f));
                if(action=="bath")game.Actor.MotionPosition=Vector3.Lerp(new Vector3(1.65f,0,-1.35f),new Vector3(1.65f,.19f,-.25f),t);
                if(action=="sleep")game.Actor.MotionPosition=Vector3.Lerp(new Vector3(-1.4f,0,-.48f),new Vector3(-1.48f,.70f,.70f),t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*.32f;
                if(t>=1)Prepare();
            }
            if(step==Step.FoodFlight&&food)
            {
                float f=Mathf.Clamp01((Time.time-since)/.85f);Vector3 end=game.Actor.MouthWorld;
                game.Actor.MouthOpenness=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.4f,.85f,f));
                food.position=Vector3.Lerp(foodFrom,end,f)+Vector3.up*Mathf.Sin(f*Mathf.PI)*.38f;food.localScale=Vector3.one*(1-f*.72f);food.Rotate(0,Time.deltaTime*80,0);
                if(f>=1){food.gameObject.SetActive(false);step=Step.Chewing;since=Time.time;game.Actor.MouthOpenness=0;game.Actor.React("feed");game.Sounds.Play("chew");}
            }
            if(step==Step.Chewing&&Time.time-since>2.6f){game.Sounds.Play("swallow");game.Actor.React(foodChoice==1?"surprise":foodChoice==2?"happy":foodChoice==3?"grumble":"pet");step=Step.Finishing;since=Time.time;}
            if(step==Step.Chewing&&Time.time>soundAt){soundAt=Time.time+.42f;game.Sounds.Play("chew",.55f);}
            if(step==Step.Finishing&&Time.time-since>1.3f)Complete();
            if(step==Step.Ready&&action=="bath")
            {if(Time.time>soundAt){soundAt=Time.time+1.8f;game.Sounds.Play("bubble",.15f);}for(int i=0;i<soapBubbles.Count;i++){float f=Mathf.Repeat(Time.time*.3f+i*.16f,1);soapBubbles[i].position=game.Actor.Body.TransformPoint(new Vector3(Mathf.Sin(i*3.2f)*.48f,-.3f+f*.9f,-.52f));}}
            if(step==Step.Finishing&&action=="play"&&Time.time-since>.75f&&!celebrated){celebrated=true;game.Actor.React("happy");}
            if((step==Step.Ready||step==Step.Finishing)&&action=="play")for(int i=0;i<targets.Count;i++)if(targets[i])
            {if(cleared.Contains(i)){targets[i].position=Vector3.Lerp(targets[i].position,game.Actor.Body.TransformPoint(new Vector3(0,.3f,-.55f)),Time.deltaTime*12);targets[i].localScale=Vector3.MoveTowards(targets[i].localScale,Vector3.zero,Time.deltaTime*2);}
                else{float t=Time.time;targets[i].position=new Vector3(Mathf.Sin(t*.75f+i*2.4f)*1.05f,.65f+(Mathf.Sin(t*1.1f+i*1.7f)+1)*.48f,-.7f+Mathf.Cos(t*.55f+i*3)*.43f);targets[i].localRotation=Quaternion.Euler(0,t*35,i*15);}}
        }
        public void Pointer(Vector2 screen,bool began,bool held)
        {
            if(!Ready)return;var ray=game.Room.Camera.ScreenPointToRay(screen);
            if(began&&(action=="feed"||action=="play"))
            {
                if(Physics.Raycast(ray,out var hit,15)){var target=hit.collider.GetComponent<CareTarget>();if(target&&targets.Contains(target.transform))TapTarget(target.Index);}return;
            }
            if(held&&(action=="bath"||action=="brush"))
            {
                Vector3 center=game.Actor.Body.TransformPoint(new Vector3(0,0,action=="bath"?-.55f:.47f));
                var plane=new Plane(game.Room.Camera.transform.forward,center);if(plane.Raycast(ray,out float distance))Stroke(ray.GetPoint(distance));
            }
        }
        public void TapTarget(int index)
        {
            if(!Ready||index<0||index>=targets.Count||cleared.Contains(index))return;
            if(action=="feed"){food=targets[index];foodChoice=index;foodFrom=food.position;foreach(var p in targets)p.GetComponent<Collider>().enabled=false;step=Step.FoodFlight;since=Time.time;game.Sounds.Play("eat");}
            if(action=="play"){if(caught>0&&Time.time-lastMiss>3&&UnityEngine.Random.value<.14f){lastMiss=Time.time;game.Actor.React("surprise");game.Sounds.Play("jump",.3f);return;}cleared.Add(index);caught++;targets[index].GetComponent<Collider>().enabled=false;game.Actor.Look(targets[index].position);game.Actor.React("catch");game.Sounds.Play("jump",.4f);game.Sounds.Play("firefly");if(caught>=5){step=Step.Finishing;since=Time.time;}}
        }
        public void Stroke(Vector3 world)
        {
            if(!Ready||(action!="bath"&&action!="brush"))return;Vector3 local=game.Actor.Body.InverseTransformPoint(world);
            if(Mathf.Abs(local.x)>.7f||local.y<-.65f||local.y>.7f)return;
            if(tool){tool.position=world;tool.rotation=Quaternion.LookRotation(game.Room.Camera.transform.forward);}
            if(Time.time-lastStroke<.035f||Vector3.Distance(previousStroke,world)<.015f)return;lastStroke=Time.time;previousStroke=world;
            if(action=="bath")
            {foreach(var cell in foamCenters)if(Vector2.Distance(new Vector2(local.x,local.y),new Vector2(cell.Value.x,cell.Value.y))<.16f)cleared.Add(cell.Key);RefreshFoam();cleaned=cleared.Count/(float)foamCenters.Count;game.Actor.React(cleaned>.35f?"pet":"curious");game.Sounds.Play("sponge",.25f);}
            else{for(int i=0;i<targets.Count;i++)if(!cleared.Contains(i)&&Vector3.Distance(world,targets[i].position)<.24f){cleared.Add(i);targets[i].SetParent(props,true);targets[i].gameObject.AddComponent<FallingLeaf>();}game.Actor.React("smirk");game.Sounds.Play("brush",.25f);}
            if((action=="bath"&&cleaned>.9f)||(action=="brush"&&cleared.Count==targets.Count)){step=Step.Finishing;since=Time.time;game.Actor.React("shake");}
        }
        void BuildFoam()
        {
            var v=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<Color>();foamCenters.Clear();foamCells.Clear();var random=new System.Random(921);
            for(int y=0;y<FoamRows;y++)for(int x=0;x<FoamCols;x++)
            {
                float px=((x+.5f)/FoamCols-.5f)*.9f,py=-.4f+(y+.5f)/FoamRows*.74f;if(px*px/(.46f*.46f)+(py+.03f)*(py+.03f)/(.39f*.39f)>1)continue;
                float z=-.08f-.47f*Mathf.Sqrt(Mathf.Max(.12f,1-px*px/(.59f*.59f)-(py+.06f)*(py+.06f)/(.67f*.67f)))-.02f;int id=y*FoamCols+x;foamCenters[id]=new Vector3(px,py,z);var triangles=new List<int>();foamCells[id]=triangles;
                for(int puff=0;puff<5;puff++)
                {float radius=puff==0?.041f+(float)random.NextDouble()*.012f:puff==1?.026f+(float)random.NextDouble()*.014f:.008f+(float)random.NextDouble()*.013f;Vector3 center=foamCenters[id]+new Vector3(((float)random.NextDouble()-.5f)*.05f,((float)random.NextDouble()-.5f)*.05f,puff>1?-.025f:0);int first=v.Count;
                    for(int ring=0;ring<=4;ring++)for(int k=0;k<=8;k++){float a=ring*Mathf.PI/8,b=k*Mathf.PI/4;Vector3 n=new Vector3(Mathf.Sin(a)*Mathf.Cos(b),Mathf.Sin(a)*Mathf.Sin(b),-Mathf.Cos(a));v.Add(center+Vector3.Scale(n,new Vector3(1,1,.60f))*radius);normals.Add(new Vector3(n.x,n.y,n.z/.60f).normalized);colors.Add(Color.Lerp(Shapes.Hex("#D6E8DA"),Color.white,.55f+Mathf.Sin(x*3+y*7+puff)*.25f));}
                    for(int ring=0;ring<4;ring++)for(int k=0;k<8;k++){int a=first+ring*9+k,b=a+9;triangles.AddRange(new[]{a,a+1,b,a+1,b+1,b});}}
            }
            foam=Shapes.Make("Continuous microbubble foam",v,new List<int>(),normals,null,colors);Shapes.MeshObject("Foam on belly",game.Actor.Body,foam,Shapes.Mat("Creamy bath foam","#EFF6E7",.1f),Vector3.zero,Vector3.one);RefreshFoam();
        }
        void RefreshFoam()
        {if(!foam)return;var t=new List<int>();foreach(var cell in foamCells)if(!cleared.Contains(cell.Key))t.AddRange(cell.Value);foam.SetTriangles(t,0);}
        void Complete()
        {
            string id=action;Cleanup();var result=game.Rules.Care(id,PetState.Now,new[]{"apple","berry","cookie","nut"}[Mathf.Clamp(foodChoice,0,3)]);if(id!="sleep")result.Animation=id=="bath"||id=="brush"?"shake":"happy";game.Handle(result);if(id=="sleep")game.Room.Focus("sleep");else game.Life.Go(new Vector3(0,0,-.4f),()=>game.Actor.React("happy"));
        }
        public void Cancel(){if(!Busy)return;Cleanup();game.Life.Go(new Vector3(0,0,-.4f),null);game.Room.Focus(null);game.Hud.RefreshLive();}
        void Cleanup()
        {
            foreach(var p in targets)if(p&&p.parent!=props)Destroy(p.gameObject);targets.Clear();
            if(foam){var f=game.Actor.Body.Find("Foam on belly");if(f)Destroy(f.gameObject);Shapes.ReleaseMesh(foam);foam=null;}
            if(props)Destroy(props.gameObject);soapBubbles.Clear();props=null;tool=food=null;step=Step.None;game.Actor.Interactive=false;game.Actor.MouthOpenness=0;game.Room.Focus(null);game.Hud.RefreshLive();
        }
    }
    public sealed class FallingLeaf:MonoBehaviour
    {float age;void Update(){age+=Time.deltaTime;transform.position+=new Vector3(Mathf.Sin(age*7)*.18f,-.55f,.12f)*Time.deltaTime;transform.Rotate(20*Time.deltaTime,70*Time.deltaTime,30*Time.deltaTime);if(age>1.5f)Destroy(gameObject);}}
}
