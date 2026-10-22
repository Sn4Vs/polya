using System.Collections.Generic;
using UnityEngine;

namespace Gazik
{
    public sealed class ForestInterior:MonoBehaviour
    {
        Mesh water;Vector3[] stillWater;readonly List<Transform> motes=new List<Transform>();
        public void Build()
        {
            var shell=Shapes.Node("Forest furnishings",transform,Vector3.zero);
            Material timber=Shapes.Mat("Forest roots","#5A4932",.15f,true),moss=Shapes.Mat("Living moss","#60734B",.1f,true),leaf=Shapes.Mat("Emerald foliage","#3E7054",.15f),pale=Shapes.Mat("Leaf light","#92A86D",.2f),stone=Shapes.Mat("River stones","#86917C",.16f),cream=Shapes.Mat("Forest ceramic","#D4D9B4",.4f),brass=Shapes.Mat("Bath brass","#AD9A5D",.6f);
            for(int side=-1;side<=1;side+=2)
            {
                Shapes.Curve("Root wall arch",shell,timber,.14f,new Vector3(side*2.85f,0,2.1f),new Vector3(side*2.65f,1.4f,2.08f),new Vector3(side*2.8f,2.8f,2.08f),new Vector3(side*1.65f,3.5f,2.08f),new Vector3(side*.6f,3.6f,2.08f));
                for(int k=0;k<4;k++)
                {float x=side*(1.45f+k*.38f);var pts=new Vector3[16];for(int j=0;j<16;j++)pts[j]=new Vector3(x+Mathf.Sin(j*.6f+k)*.13f,3.55f-j*.13f,2.06f);Shapes.Curve("Hanging vine",shell,moss,.018f,pts);
                    for(int j=1;j<15;j+=2){var l=Shapes.MeshObject("Vine leaf",shell,LeafMesh(),j%3==0?pale:leaf,pts[j],new Vector3(.13f,.22f,.13f));l.transform.localRotation=Quaternion.Euler(10,20,j%4==1?40:-35);}}
            }
            for(int i=0;i<28;i++)
            {
                float a=i*2.4f;Vector3 p=new Vector3(Mathf.Sin(a)*(2.3f+i%3*.12f),.02f,Mathf.Cos(a)*1.9f);
                Shapes.Ball("Moss cushion",shell,p,new Vector3(.22f+i%3*.05f,.04f,.15f),moss);
                if(i%3==0){Shapes.Ball("Smooth stone",shell,p+new Vector3(.16f,.05f,.04f),new Vector3(.10f,.07f,.08f),stone);Mushroom(shell,p+new Vector3(-.12f,.02f,.10f),.12f+i%2*.05f,cream,brass);}
                var fallen=Shapes.MeshObject("Forest floor leaf",shell,LeafMesh(),pale,p+new Vector3(.12f,.025f,-.1f),Vector3.one*.12f);fallen.transform.localRotation=Quaternion.Euler(90,i*31,0);
            }
            var distant=Shapes.Mat("Distant emerald forest","#42685B",.1f);
            for(int i=0;i<5;i++){Vector3 p=new Vector3(.37f+i*.28f,.98f,2.23f);float height=1.1f+Mathf.Sin(i*1.7f)*.17f;Shapes.Rod("Distant forest trunk",shell,p,p+new Vector3(.02f,height,0),.018f,distant);Shapes.Ball("Distant forest canopy",shell,p+Vector3.up*height*.7f,new Vector3(.18f,height*.35f,.009f),distant);}
            // Meal station: carved stump, jars and berries next to the existing bowl.
            var meal=Shapes.Node("Meal nook",shell,new Vector3(-2.2f,0,-.85f));Shapes.Cylinder("Serving stump",meal,new Vector3(0,.28f,0),new Vector3(.65f,.28f,.65f),timber);Shapes.Cylinder("Wood grain top",meal,new Vector3(0,.57f,0),new Vector3(.67f,.025f,.67f),brass);
            Shapes.Ball("Little plate",meal,new Vector3(0,.61f,0),new Vector3(.25f,.035f,.25f),cream);
            for(int i=0;i<3;i++){Shapes.Cylinder("Preserve jar",meal,new Vector3((i-1)*.18f,.72f,.15f),new Vector3(.1f,.11f,.1f),i%2==0?pale:cream);Shapes.Cylinder("Jar lid",meal,new Vector3((i-1)*.18f,.835f,.15f),new Vector3(.11f,.016f,.11f),brass);}
            // An open tub has separate inner and outer surfaces; water remains visible inside.
            var bath=Shapes.Node("Bath nook",shell,new Vector3(1.65f,0,-.25f));Shapes.MeshObject("Hollow wooden tub",bath,TubMesh(),timber,new Vector3(0,.05f,0),Vector3.one);
            Shapes.Curve("Copper tap",bath,brass,.045f,new Vector3(.5f,.1f,.35f),new Vector3(.5f,.7f,.35f),new Vector3(.4f,.79f,.35f),new Vector3(.23f,.79f,.35f),new Vector3(.23f,.66f,.35f));
            Shapes.Box("Folded towel",bath,new Vector3(.62f,.35f,-.12f),new Vector3(.3f,.10f,.5f),cream);Shapes.Box("Soap",bath,new Vector3(-.58f,.38f,.1f),new Vector3(.19f,.08f,.12f),pale);
            Shapes.Box("Bath shelf",bath,new Vector3(1.04f,.85f,.06f),new Vector3(.25f,.08f,.8f),timber);
            for(int i=0;i<3;i++)Shapes.Cylinder("Bath bottle",bath,new Vector3(1.02f,1,.25f-i*.22f),new Vector3(.10f,.12f,.10f),i%2==0?pale:cream);
            var v=new List<Vector3>();var triangles=new List<int>();for(int y=0;y<=12;y++)for(int x=0;x<=16;x++){float px=(x/16f-.5f)*1.06f,pz=(y/12f-.5f)*.76f;v.Add(new Vector3(px,.28f,pz));}
            for(int y=0;y<12;y++)for(int x=0;x<16;x++){int a=y*17+x,b=a+17;triangles.AddRange(new[]{a,b,a+1,a+1,b,b+1});}
            water=Shapes.Make("Bath water ripples",v,triangles);stillWater=v.ToArray();Shapes.MeshObject("Moving bath water",transform,water,Shapes.Mat("Forest bath water","#80AAA0",.7f),new Vector3(1.65f,0,-.25f),Vector3.one);
            // Dedicated grooming shelf, tools and mirror.
            var groom=Shapes.Node("Quill care nook",shell,new Vector3(-.25f,0,1.8f));Shapes.Box("Grooming shelf",groom,new Vector3(0,.68f,0),new Vector3(1.2f,.1f,.45f),timber);
            for(int i=0;i<3;i++){Shapes.Cylinder("Quill balm",groom,new Vector3(-.35f+i*.22f,.81f,.02f),new Vector3(.13f,.10f,.13f),i==1?brass:cream);Shapes.Cylinder("Balm cap",groom,new Vector3(-.35f+i*.22f,.92f,.02f),new Vector3(.14f,.015f,.14f),moss);}
            Shapes.Box("Comb spine",groom,new Vector3(.35f,.77f,-.10f),new Vector3(.32f,.045f,.08f),brass);for(int i=0;i<8;i++)Shapes.Rod("Comb tooth",groom,new Vector3(.22f+i*.035f,.75f,-.10f),new Vector3(.22f+i*.035f,.75f,-.25f),.009f,brass);
            Shapes.Ball("Hand brush",groom,new Vector3(.27f,.79f,.1f),new Vector3(.13f,.05f,.09f),timber);
            // Root bed rail, moss, mushrooms and hanging moon above the bed.
            for(int side=-1;side<=1;side+=2)Shapes.Curve("Bed root rail",shell,timber,.08f,new Vector3(-1.66f+side*.72f,.3f,.25f),new Vector3(-1.66f+side*.75f,.95f,.5f),new Vector3(-1.66f+side*.72f,1.05f,1.3f));
            Shapes.Curve("Bed mobile",shell,brass,.013f,new Vector3(-2.1f,2.1f,1.15f),new Vector3(-1.3f,2.1f,1.15f));for(int i=0;i<3;i++){Shapes.Rod("Mobile thread",shell,new Vector3(-2+i*.3f,2.1f,1.15f),new Vector3(-2+i*.3f,1.8f+i%2*.12f,1.15f),.006f,brass);Shapes.Star("Bed mobile star",shell,new Vector3(-2+i*.3f,1.75f+i%2*.12f,1.15f),.065f,cream);}
            for(int i=0;i<7;i++){float a=i*Mathf.PI*2/7;Shapes.Ball("Play clearing stone",shell,new Vector3(Mathf.Cos(a)*1.25f,.045f,-.45f+Mathf.Sin(a)*.85f),new Vector3(.09f,.05f,.07f),stone);}
            for(int i=0;i<3;i++){var jar=Shapes.Node("Glow jar",shell,new Vector3(-2.5f+i*2.4f,.13f,i%2==0?1.55f:-1.65f));Shapes.Cylinder("Jar body",jar,Vector3.zero,new Vector3(.13f,.13f,.13f),moss);Shapes.Ball("Jar glow",jar,new Vector3(0,.03f,-.065f),Vector3.one*.06f,Shapes.Glow("Jar glimmer","#C2E5A0",.65f));Shapes.Cylinder("Jar cap",jar,new Vector3(0,.15f,0),new Vector3(.15f,.02f,.15f),brass);}
            Shapes.CombineStatic(shell);
            for(int i=0;i<14;i++)motes.Add(Shapes.Ball("Ambient forest firefly",transform,Vector3.zero,Vector3.one*.015f,Shapes.Glow("Forest mote","#D3EAA7",.55f)).transform);
        }
        static void Mushroom(Transform p,Vector3 at,float size,Material stem,Material cap){Shapes.Rod("Mushroom stem",p,at,at+Vector3.up*size,.025f,stem);Shapes.Ball("Mushroom cap",p,at+Vector3.up*size,new Vector3(size*.7f,size*.35f,size*.7f),cap);}
        static Mesh leafMesh;static Mesh LeafMesh(){if(leafMesh)return leafMesh;leafMesh=Shapes.Make("Curved woodland leaf",new List<Vector3>{new Vector3(0,0,0),new Vector3(-.5f,.45f,.07f),new Vector3(0,1,.04f),new Vector3(.5f,.45f,.07f),new Vector3(0,.48f,.16f)},new List<int>{0,1,4,1,2,4,2,3,4,3,0,4,4,1,0,4,2,1,4,3,2,4,0,3},new List<Vector3>{Vector3.back,Vector3.back,Vector3.back,Vector3.back,Vector3.back});return leafMesh;}
        static Mesh TubMesh()
        {var v=new List<Vector3>();var t=new List<int>();for(int ring=0;ring<4;ring++)for(int i=0;i<=48;i++){float a=i*Mathf.PI*2/48,r=ring==0?.94f:ring==1?1:ring==2?.85f:.77f;v.Add(new Vector3(Mathf.Cos(a)*.7f*r,ring==0||ring==3?.02f:.38f,Mathf.Sin(a)*.52f*r));}for(int r=0;r<3;r++)for(int i=0;i<48;i++){int a=r*49+i,b=a+49;t.AddRange(new[]{a,a+1,b,a+1,b+1,b,b,a+1,a,b,b+1,a+1});}return Shapes.Make("Hollow carved tub",v,t);}
        float rippleAt;
        void Update()
        {
            if(water&&Time.time>rippleAt){rippleAt=Time.time+.06f;var v=(Vector3[])stillWater.Clone();for(int i=0;i<v.Length;i++)v[i].y+=Mathf.Sin(Time.time*2.2f+v[i].x*7+v[i].z*5)*.007f;water.vertices=v;water.RecalculateNormals();}
            for(int i=0;i<motes.Count;i++)if(motes[i])motes[i].localPosition=new Vector3(Mathf.Sin(Time.time*.12f+i*2.4f)*2.45f,.4f+(Mathf.Sin(Time.time*.2f+i*3.2f)+1)*1.2f,1.6f+Mathf.Sin(Time.time*.16f+i*2.1f)*.35f);
        }
    }
}
