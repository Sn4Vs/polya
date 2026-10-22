using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gazik
{
    public static class Shapes
    {
        static Mesh sphere, rounded;
        static readonly List<Mesh> generatedMeshes=new List<Mesh>();
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        public static Color Hex(string value) { ColorUtility.TryParseHtmlString(value, out Color c); return c; }
        public static Material Mat(string name, string hex, float gloss = .25f, bool coat = false)
        {
            if (materials.TryGetValue(name, out Material cached) && cached) return cached;
            // Resource reference keeps the shader in WebGL builds with generated scenes.
            Shader shader = Resources.Load<Shader>("GazikCoat");
            if (!shader) throw new InvalidOperationException("GazikCoat shader resource is missing");
            var mat = new Material(shader) { name = name, color = Hex(hex) };
            mat.SetFloat("_Glossiness", gloss);mat.SetFloat("_Fibre",coat?.15f:0);mat.SetFloat("_RimStrength",coat?.035f:0); materials[name] = mat; return mat;
        }
        public static Material Glow(string name, string hex, float alpha)
        {
            if (materials.TryGetValue(name, out Material cached) && cached) return cached;
            var m = new Material(Resources.Load<Shader>("GazikGlow")); Color c = Hex(hex); c.a = alpha; m.SetColor("_Color", c); materials[name] = m; return m;
        }
        public static Transform Node(string name, Transform parent, Vector3 position)
        { var o = new GameObject(name); o.transform.SetParent(parent, false); o.transform.localPosition = position; return o.transform; }
        public static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material material, Vector3 pos, Vector3 scale)
        {
            var o = new GameObject(name); o.transform.SetParent(parent, false); o.transform.localPosition = pos; o.transform.localScale = scale;
            o.AddComponent<MeshFilter>().sharedMesh = mesh; var r = o.AddComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.On; return o;
        }
        public static GameObject Ball(string name, Transform parent, Vector3 position, Vector3 scale, Material mat)
        { if (!sphere) sphere = Sphere(32, 24); return MeshObject(name, parent, sphere, mat, position, scale); }
        public static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        { if (!rounded) rounded = RoundCube(); return MeshObject(name, parent, rounded, mat, pos, scale); }
        public static GameObject Cylinder(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var o = GameObject.CreatePrimitive(PrimitiveType.Cylinder); o.name = name; o.transform.SetParent(parent, false); o.transform.localPosition = pos; o.transform.localScale = scale;
            UnityEngine.Object.Destroy(o.GetComponent<Collider>()); o.GetComponent<Renderer>().sharedMaterial = mat; return o;
        }
        public static GameObject Rod(string name, Transform parent, Vector3 a, Vector3 b, float radius, Material mat)
        { var o = Cylinder(name, parent, (a + b) * .5f, new Vector3(radius, (a-b).magnitude * .5f, radius), mat); o.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a); return o; }
        public static LineRenderer Curve(string name, Transform parent, Material mat, float width, params Vector3[] points)
        {
            var o = new GameObject(name); o.transform.SetParent(parent, false); var line = o.AddComponent<LineRenderer>(); line.sharedMaterial = mat;
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
            line.useWorldSpace = false; line.positionCount = points.Length; line.SetPositions(points); line.startWidth = line.endWidth = width; line.numCapVertices = 4; line.numCornerVertices = 3;return line;
        }
        public static Mesh Sphere(int longitude, int latitude)
        {
            var v = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int y=0;y<=latitude;y++) for(int x=0;x<=longitude;x++)
            { float a=x*2*Mathf.PI/longitude,b=y*Mathf.PI/latitude; Vector3 p=new Vector3(Mathf.Sin(b)*Mathf.Cos(a),Mathf.Cos(b),Mathf.Sin(b)*Mathf.Sin(a));v.Add(p);normals.Add(p);uv.Add(new Vector2((float)x/longitude,(float)y/latitude)); }
            for(int y=0;y<latitude;y++)for(int x=0;x<longitude;x++){int a=y*(longitude+1)+x,b=a+longitude+1;t.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
            return Make("Smooth ellipsoid",v,t,normals,uv);
        }
        public static Mesh Make(string name, List<Vector3> v, List<int> t, List<Vector3> normals=null, List<Vector2> uv=null, List<Color> colors=null)
        {
            var m=new Mesh{name=name,indexFormat=v.Count>65000?IndexFormat.UInt32:IndexFormat.UInt16};m.SetVertices(v);m.SetTriangles(t,0);
            if(normals!=null)m.SetNormals(normals);else m.RecalculateNormals();if(uv!=null)m.SetUVs(0,uv);
            if(colors!=null)m.SetColors(colors);else{var white=new Color[v.Count];for(int i=0;i<white.Length;i++)white[i]=Color.white;m.colors=white;}
            m.RecalculateBounds();generatedMeshes.Add(m);return m;
        }
        static Mesh RoundCube()
        {
            var v=new List<Vector3>();var n=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();
            Vector3[] axis={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            foreach(Vector3 face in axis)
            {
                Vector3 u=Vector3.Cross(face,Mathf.Abs(face.y)>.5f?Vector3.forward:Vector3.up).normalized,w=Vector3.Cross(face,u);int start=v.Count;
                const int steps=8;for(int y=0;y<=steps;y++)for(int x=0;x<=steps;x++)
                {Vector3 p=face*.5f+u*((float)x/steps-.5f)+w*((float)y/steps-.5f);Vector3 core=new Vector3(Mathf.Clamp(p.x,-.41f,.41f),Mathf.Clamp(p.y,-.41f,.41f),Mathf.Clamp(p.z,-.41f,.41f));Vector3 normal=(p-core).normalized;v.Add(core+normal*.09f);n.Add(normal);uv.Add(new Vector2((float)x/steps,(float)y/steps));}
                for(int y=0;y<steps;y++)for(int x=0;x<steps;x++){int a=start+y*(steps+1)+x,b=a+steps+1;t.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
            }return Make("Beveled furniture",v,t,n,uv);
        }
        public static Mesh Quills(Vector3 radii, int count, int seed)
        {
            var random=new System.Random(seed);var v=new List<Vector3>();var n=new List<Vector3>();var t=new List<int>();var colors=new List<Color>();
            for(int i=0;i<count;i++)
            {
                float y=(float)random.NextDouble()*1.65f-.65f,a=(float)random.NextDouble()*Mathf.PI*2;Vector3 normal=new Vector3(Mathf.Sqrt(1-y*y)*Mathf.Cos(a),y,Mathf.Sqrt(1-y*y)*Mathf.Sin(a));
                if(normal.z<-.22f) { i--; continue; } // Keep the face free of needles.
                Vector3 root=Vector3.Scale(normal,radii), side=Vector3.Cross(normal,Vector3.up).normalized;if(side.sqrMagnitude<.1f)side=Vector3.right;
                Vector3 tangent=Vector3.Cross(normal,side).normalized;float length=.09f+(float)random.NextDouble()*.09f,width=.032f+(float)random.NextDouble()*.018f;int start=v.Count;
                Color rootColor=Color.Lerp(Hex("#674435"),Hex("#8B6142"),(float)random.NextDouble()),tip=Hex("#C39A69");
                for(int ring=0;ring<=6;ring++)for(int k=0;k<=7;k++)
                {float f=ring/6f,b=k*Mathf.PI*2/7;float r=width*Mathf.Sin((.1f+.9f*f)*Mathf.PI);Vector3 center=root+normal*(length*f)+Vector3.up*(.035f*f*f)+Vector3.forward*(.06f*f*f);
                    Vector3 radial=side*Mathf.Cos(b)+tangent*Mathf.Sin(b);v.Add(center+radial*r);n.Add((radial+normal*f*.5f).normalized);colors.Add(Color.Lerp(rootColor,tip,f*f));}
                for(int ring=0;ring<6;ring++)for(int k=0;k<7;k++){int a0=start+ring*8+k,b0=a0+8;t.AddRange(new[]{a0,a0+1,b0,a0+1,b0+1,b0});}
            }return Make("Sculpted soft quills",v,t,n,null,colors);
        }
        public static Mesh Fuzz(Vector3 radii,int count,int seed,bool face)
        {
            var random=new System.Random(seed);var v=new List<Vector3>();var t=new List<int>();var n=new List<Vector3>();
            for(int i=0;i<count;i++)
            {
                float y=(float)random.NextDouble()*1.85f-.85f,a=(float)random.NextDouble()*Mathf.PI*2;Vector3 normal=new Vector3(Mathf.Sqrt(1-y*y)*Mathf.Cos(a),y,Mathf.Sqrt(1-y*y)*Mathf.Sin(a));
                if(normal.z>.2f)continue;
                // Do not grow fibers through the pupils, nose or mouth.
                if(face&&normal.z<-.6f&&y<.31f)continue;
                Vector3 root=Vector3.Scale(normal,radii),side=Vector3.Cross(normal,Vector3.up).normalized;
                Vector3 tangent=Vector3.Cross(normal,side).normalized;float length=.014f+(float)random.NextDouble()*.018f,width=.0016f;
                for(int cross=0;cross<2;cross++)
                {Vector3 lateral=cross==0?side:tangent;int k=v.Count;v.Add(root-lateral*width);v.Add(root+lateral*width);v.Add(root+normal*length+Vector3.down*.005f);n.Add(normal);n.Add(normal);n.Add(normal);t.AddRange(new[]{k,k+1,k+2,k+2,k+1,k});}
            }return Make("Velvet silhouette fibers",v,t,n);
        }
        public static Mesh ArchedPane(float width,float bottom,float shoulder,float rise)
        {
            var v=new List<Vector3>{new Vector3(0,0,0),new Vector3(-width,bottom,0),new Vector3(width,bottom,0)};var t=new List<int>();var n=new List<Vector3>();
            for(int i=0;i<=32;i++){float a=i*Mathf.PI/32;v.Add(new Vector3(Mathf.Cos(a)*width,shoulder+Mathf.Sin(a)*rise,0));}
            for(int i=1;i<v.Count;i++){int next=i==v.Count-1?1:i+1;t.AddRange(new[]{0,next,i});}foreach(var p in v)n.Add(Vector3.back);
            return Make("Arched glass silhouette",v,t,n);
        }
        public static Mesh DrapedLinen()
        {
            var v=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();var normals=new List<Vector3>();
            const int cols=24,rows=12;
            for(int y=0;y<=rows;y++)for(int x=0;x<=cols;x++)
            {float u=x/(float)cols,w=y/(float)rows;v.Add(new Vector3((u-.5f)*.35f,.98f-w*1.96f+Mathf.Sin(u*Mathf.PI*4)*.015f*w,-.055f-Mathf.Cos(u*Mathf.PI*8)*.035f));uv.Add(new Vector2(u,w));normals.Add(new Vector3(Mathf.Sin(u*Mathf.PI*8)*.035f*8*Mathf.PI/.35f,0,-1).normalized);}
            for(int y=0;y<rows;y++)for(int x=0;x<cols;x++){int a=y*(cols+1)+x,b=a+cols+1;t.AddRange(new[]{a,b,a+1,a+1,b,b+1,a+1,b,a,b+1,b,a+1});}
            return Make("Pleated linen",v,t,normals,uv);
        }
        public static GameObject Heart(string name,Transform parent,Vector3 pos,float size,Material mat)
        {
            var v=new List<Vector3>{Vector3.zero};var t=new List<int>();
            for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48,s=Mathf.Sin(a);v.Add(new Vector3(s*s*s,(13*Mathf.Cos(a)-5*Mathf.Cos(2*a)-2*Mathf.Cos(3*a)-Mathf.Cos(4*a))/16,0));}
            for(int i=1;i<v.Count;i++){int b=i==v.Count-1?1:i+1;t.AddRange(new[]{0,i,b,0,b,i});}var normals=new List<Vector3>();foreach(var p in v)normals.Add(Vector3.back);return MeshObject(name,parent,Make("Soft heart silhouette",v,t,normals),mat,pos,Vector3.one*size);
        }
        // Batch only direct mesh children: eye, ear and limb pivots keep their animation.
        public static void CombineRigid(Transform pivot)
        {
            var groups=new Dictionary<Material,List<CombineInstance>>();var originals=new List<GameObject>();
            foreach(Transform child in pivot)
            {var f=child.GetComponent<MeshFilter>();var r=child.GetComponent<MeshRenderer>();if(!f||!r||!r.enabled||!f.sharedMesh||!r.sharedMaterial)continue;var m=r.sharedMaterial;if(!groups.ContainsKey(m))groups[m]=new List<CombineInstance>();groups[m].Add(new CombineInstance{mesh=f.sharedMesh,transform=pivot.worldToLocalMatrix*child.localToWorldMatrix});originals.Add(child.gameObject);}
            foreach(var p in groups){var m=new Mesh{name="Rigid "+p.Key.name,indexFormat=IndexFormat.UInt32};m.CombineMeshes(p.Value.ToArray());generatedMeshes.Add(m);MeshObject(m.name,pivot,m,p.Key,Vector3.zero,Vector3.one);}
            foreach(var o in originals){o.GetComponent<MeshRenderer>().enabled=false;UnityEngine.Object.Destroy(o);}
        }
        public static GameObject Star(string name,Transform parent,Vector3 pos,float size,Material mat)
        {
            var v=new List<Vector3>{new Vector3(0,0,-.08f),new Vector3(0,0,.08f)};var t=new List<int>();
            for(int i=0;i<10;i++){float a=Mathf.PI/2+i*Mathf.PI/5,r=i%2==0?1:.46f;v.Add(new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,-.04f));v.Add(new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,.04f));}
            for(int i=0;i<10;i++){int a=2+i*2,b=2+(i+1)%10*2;t.AddRange(new[]{0,b,a,1,a+1,b+1,a,b,b+1,a,b+1,a+1});}
            return MeshObject(name,parent,Make(name,v,t),mat,pos,Vector3.one*size);
        }
        public static void CombineStatic(Transform root)
        {
            var groups=new Dictionary<Material,List<CombineInstance>>();var originals=new List<GameObject>();
            foreach(var f in root.GetComponentsInChildren<MeshFilter>())
            {var r=f.GetComponent<MeshRenderer>();if(!r||!f.sharedMesh||!r.enabled)continue;Material m=r.sharedMaterial;if(!m)continue;if(!groups.ContainsKey(m))groups[m]=new List<CombineInstance>();groups[m].Add(new CombineInstance{mesh=f.sharedMesh,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix});originals.Add(f.gameObject);}
            foreach(var p in groups){var m=new Mesh{name="Batched "+p.Key.name,indexFormat=IndexFormat.UInt32};m.CombineMeshes(p.Value.ToArray());generatedMeshes.Add(m);MeshObject(m.name,root,m,p.Key,Vector3.zero,Vector3.one);}
            foreach(var o in originals){o.GetComponent<MeshRenderer>().enabled=false;UnityEngine.Object.Destroy(o);}
        }
        public static void ReleaseResources()
        {
            foreach(var mesh in generatedMeshes)if(mesh)UnityEngine.Object.Destroy(mesh);generatedMeshes.Clear();sphere=rounded=null;
            foreach(var material in materials.Values)if(material)UnityEngine.Object.Destroy(material);materials.Clear();
        }
        public static void ReleaseMesh(Mesh mesh){if(!mesh)return;generatedMeshes.Remove(mesh);UnityEngine.Object.Destroy(mesh);}
    }
}
