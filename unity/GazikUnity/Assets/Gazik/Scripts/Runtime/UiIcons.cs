using System.Collections.Generic;
using UnityEngine;

namespace Gazik
{
    // Original line icons, rasterized once. No emoji font or remote image dependency.
    public static class UiIcons
    {
        const int Size=96;static readonly Dictionary<string,Sprite> cache=new Dictionary<string,Sprite>();
        public static Sprite Get(string id)
        {
            if(cache.TryGetValue(id,out var cached)&&cached)return cached;
            var alpha=new float[Size*Size];
            void Line(float x1,float y1,float x2,float y2,float width=3.5f)
            {
                var a=new Vector2(x1,y1);var b=new Vector2(x2,y2);Vector2 segment=b-a;float length=segment.sqrMagnitude;
                for(int y=0;y<Size;y++)for(int x=0;x<Size;x++){Vector2 p=new Vector2(x+.5f,y+.5f),near=a+segment*Mathf.Clamp01(Vector2.Dot(p-a,segment)/Mathf.Max(length,.001f));float coverage=Mathf.Clamp01(width*.5f+.6f-(p-near).magnitude);int k=y*Size+x;alpha[k]=Mathf.Max(alpha[k],coverage);}
            }
            void Arc(float cx,float cy,float radius,float start,float end,int steps=48,float width=3.5f)
            {for(int i=0;i<steps;i++){float a=Mathf.Lerp(start,end,i/(float)steps),b=Mathf.Lerp(start,end,(i+1)/(float)steps);Line(cx+Mathf.Cos(a)*radius,cy+Mathf.Sin(a)*radius,cx+Mathf.Cos(b)*radius,cy+Mathf.Sin(b)*radius,width);}}
            void Path(params float[] p){for(int i=0;i<p.Length-2;i+=2)Line(p[i],p[i+1],p[i+2],p[i+3]);}
            if(id=="feed")
            {Path(48,67,38,73,27,70,19,60,18,47,23,32,30,23,39,22,48,25,57,22,66,23,73,32,78,47,77,60,69,70,58,73,48,67);Path(48,69,48,78,53,84);Path(51,76,59,84,70,84,65,77,57,74,51,76);Arc(33,52,8,2.5f,3.9f,14,2.5f);}
            else if(id=="play")
            {Arc(45,50,28,0,Mathf.PI*2);Path(25,70,49,76,65,67,69,51,60,31);Path(18,50,34,66,54,69,71,57);Path(19,39,34,54,56,59,72,55);Path(27,28,42,46,59,49);Path(43,22,55,36,65,38);Path(52,23,64,16,77,18,83,26);}
            else if(id=="bath")
            {Arc(36,52,18,0,Mathf.PI*2);Arc(65,65,12,0,Mathf.PI*2);Arc(65,35,8,0,Mathf.PI*2);Path(14,21,24,24,35,21,46,24,57,21,68,24,81,21);Path(22,77,30,77);Path(26,73,26,81);}
            else if(id=="brush")
            {Path(24,75,64,75,72,67,72,57,65,51,24,51,18,57,18,68,24,75);Path(41,51,41,19,49,15,57,19,57,51);for(int i=0;i<7;i++)Line(25+i*6,52,25+i*6,40);}
            else
            {Arc(49,50,30,1.05f,5.23f);Arc(63,59,29,2.05f,4.68f);Path(64,77,67,82,70,77,76,75,70,72,67,67,64,72,58,75,64,77);}
            var pixels=new Color[Size*Size];for(int i=0;i<pixels.Length;i++)pixels[i]=new Color(1,1,1,alpha[i]);
            var texture=new Texture2D(Size,Size,TextureFormat.RGBA32,false){name="Gazik icon "+id,filterMode=FilterMode.Bilinear};texture.SetPixels(pixels);texture.Apply(false,true);
            var sprite=Sprite.Create(texture,new Rect(0,0,Size,Size),new Vector2(.5f,.5f),Size);cache[id]=sprite;return sprite;
        }
        public static void ReleaseResources(){foreach(var sprite in cache.Values)if(sprite){Object.Destroy(sprite.texture);Object.Destroy(sprite);}cache.Clear();}
    }
}
