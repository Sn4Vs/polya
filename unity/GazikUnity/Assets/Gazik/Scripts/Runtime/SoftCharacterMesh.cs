using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gazik
{
    // A single blended surface, skinned to the same bones as the face and quills.
    public static class SoftCharacterMesh
    {
        static readonly Vector3[] Corners={new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(1,1,0),new Vector3(0,1,0),new Vector3(0,0,1),new Vector3(1,0,1),new Vector3(1,1,1),new Vector3(0,1,1)};
        static readonly int[,] Tetra={{0,5,1,6},{0,1,2,6},{0,2,3,6},{0,3,7,6},{0,7,4,6},{0,4,5,6}};
        static float Ellipsoid(Vector3 p,Vector3 center,Vector3 radii)
        {p-=center;float a=new Vector3(p.x/radii.x,p.y/radii.y,p.z/radii.z).magnitude,b=new Vector3(p.x/(radii.x*radii.x),p.y/(radii.y*radii.y),p.z/(radii.z*radii.z)).magnitude;return b<.00001f?-Mathf.Min(radii.x,Mathf.Min(radii.y,radii.z)):a*(a-1)/b;}
        static float Union(float a,float b,float k){float h=Mathf.Clamp01(.5f+.5f*(b-a)/k);return Mathf.Lerp(b,a,h)-k*h*(1-h);}
        static float Field(Vector3 p)
        {
            float d=Ellipsoid(p,new Vector3(0,.72f,-.02f),new Vector3(.59f,.67f,.47f));d=Union(d,Ellipsoid(p,new Vector3(0,1.4f,-.06f),new Vector3(.69f,.57f,.48f)),.14f);
            for(int side=-1;side<=1;side+=2)
            {d=Union(d,Ellipsoid(p,new Vector3(side*.5f,.53f,.035f),new Vector3(.145f,.3f,.16f)),.12f);d=Union(d,Ellipsoid(p,new Vector3(side*.49f,.37f,-.015f),new Vector3(.165f,.13f,.16f)),.075f);
                d=Union(d,Ellipsoid(p,new Vector3(side*.31f,.24f,0),new Vector3(.20f,.27f,.20f)),.10f);d=Union(d,Ellipsoid(p,new Vector3(side*.31f,.12f,-.10f),new Vector3(.21f,.13f,.265f)),.10f);}
            return d;
        }
        static Vector3 Normal(Vector3 p){const float e=.002f;return new Vector3(Field(p+Vector3.right*e)-Field(p-Vector3.right*e),Field(p+Vector3.up*e)-Field(p-Vector3.up*e),Field(p+Vector3.forward*e)-Field(p-Vector3.forward*e)).normalized;}
        public static void Build(HedgehogActor actor,Material material)
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var triangles=new List<int>();var colors=new List<Color>();var weights=new List<BoneWeight>();
            var indices=new Dictionary<Vector3,int>();const float cell=.065f;Vector3 origin=new Vector3(-.845f,-.075f,-.65f);const int nx=26,ny=34,nz=24;
            var samples=new float[(nx+1)*(ny+1)*(nz+1)];int Offset(int x,int y,int z)=>(z*(ny+1)+y)*(nx+1)+x;
            for(int z=0;z<=nz;z++)for(int y=0;y<=ny;y++)for(int x=0;x<=nx;x++)samples[Offset(x,y,z)]=Field(origin+new Vector3(x,y,z)*cell);
            var cube=new Vector3[8];var values=new float[8];var inside=new int[4];var outside=new int[4];
            int Vertex(Vector3 a,Vector3 b,float fa,float fb)
            {
                Vector3 p=Vector3.Lerp(a,b,fa/(fa-fb));Vector3 key=new Vector3(Mathf.Round(p.x*100000),Mathf.Round(p.y*100000),Mathf.Round(p.z*100000));if(indices.TryGetValue(key,out int existing))return existing;
                int index=vertices.Count;indices[key]=index;vertices.Add(p);normals.Add(Normal(p));float belly=Mathf.Clamp01((-p.z-.18f)*5)*Mathf.Clamp01((1.05f-p.y)*5);colors.Add(Color.Lerp(Color.white,Shapes.Hex("#FFF1CF"),belly*.45f));
                float head=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.98f,1.30f,p.y));float foot=(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.17f,.39f,p.y)))*Mathf.Clamp01((Mathf.Abs(p.x)-.1f)*6);
                float arm=Mathf.Clamp01((Mathf.Abs(p.x)-.38f)*6)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,.9f,p.y)))*(1-foot)*(1-head);
                float body=Mathf.Max(0,1-head-foot-arm),sum=body+head+foot+arm;weights.Add(new BoneWeight{boneIndex0=0,weight0=body/sum,boneIndex1=1,weight1=head/sum,boneIndex2=p.x>0?2:3,weight2=arm/sum,boneIndex3=p.x>0?4:5,weight3=foot/sum});return index;
            }
            void Triangle(int a,int b,int c){Vector3 cross=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);if(Vector3.Dot(cross,normals[a]+normals[b]+normals[c])<0){int swap=b;b=c;c=swap;}triangles.Add(a);triangles.Add(b);triangles.Add(c);}
            for(int z=0;z<nz;z++)for(int y=0;y<ny;y++)for(int x=0;x<nx;x++)
            {for(int i=0;i<8;i++){cube[i]=origin+(new Vector3(x,y,z)+Corners[i])*cell;values[i]=samples[Offset(x+(int)Corners[i].x,y+(int)Corners[i].y,z+(int)Corners[i].z)];}
                for(int t=0;t<6;t++){int ni=0,no=0;for(int i=0;i<4;i++){int c=Tetra[t,i];if(values[c]<0)inside[ni++]=c;else outside[no++]=c;}if(ni==0||ni==4)continue;
                    int Edge(int a,int b)=>Vertex(cube[a],cube[b],values[a],values[b]);
                    if(ni==1)Triangle(Edge(inside[0],outside[0]),Edge(inside[0],outside[1]),Edge(inside[0],outside[2]));
                    else if(ni==3)Triangle(Edge(outside[0],inside[0]),Edge(outside[0],inside[1]),Edge(outside[0],inside[2]));
                    else{int a=Edge(inside[0],outside[0]),b=Edge(inside[0],outside[1]),c=Edge(inside[1],outside[0]),d=Edge(inside[1],outside[1]);Triangle(a,b,c);Triangle(b,d,c);}}
            }
            Mesh mesh=Shapes.Make("Unified soft skinned hedgehog",vertices,triangles,normals,null,colors);Transform[] bones={actor.Body,actor.Head,actor.LeftArm,actor.RightArm,actor.LeftFoot,actor.RightFoot};var bind=new Matrix4x4[bones.Length];for(int i=0;i<bones.Length;i++)bind[i]=bones[i].worldToLocalMatrix*actor.transform.localToWorldMatrix;mesh.bindposes=bind;mesh.boneWeights=weights.ToArray();
            var o=Shapes.Node("Continuous body skin",actor.transform,Vector3.zero).gameObject;var skin=o.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.sharedMaterial=material;skin.bones=bones;skin.rootBone=actor.Body;skin.localBounds=new Bounds(new Vector3(0,1,0),new Vector3(3,4,3));skin.shadowCastingMode=ShadowCastingMode.On;
            foreach(var f in actor.GetComponentsInChildren<MeshFilter>())if(f.name=="Rounded belly"||f.name=="Head"||f.name=="Short arm"||f.name=="Paw"||f.name=="Hind foot"){f.GetComponent<MeshRenderer>().enabled=false;Object.Destroy(f.gameObject);}
        }
    }
}
