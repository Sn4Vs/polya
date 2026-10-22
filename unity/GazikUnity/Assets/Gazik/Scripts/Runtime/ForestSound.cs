using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gazik
{
    public sealed class ForestSound:MonoBehaviour
    {
        AudioSource effects,music;readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();readonly Dictionary<string,float> next=new Dictionary<string,float>();
        public bool Enabled{get;private set;}=true;GazikGame game;float stepAt;
        public void Init(GazikGame value)
        {
            game=value;effects=gameObject.AddComponent<AudioSource>();effects.playOnAwake=false;effects.volume=.22f;
            music=gameObject.AddComponent<AudioSource>();music.playOnAwake=false;music.loop=true;music.volume=.045f;
            string[] ids={"step","jump","eat","chew","swallow","water","sponge","bubble","brush","sleep","yawn","firefly","reward","shop","button"};
            for(int n=0;n<ids.Length;n++)clips[ids[n]]=Make(ids[n],n);
            music.clip=Music();SetEnabled(PlayerPrefs.GetInt("Gazik.Sound",1)==1);if(Enabled)music.Play();
        }
        public void SetEnabled(bool value){Enabled=value;effects.mute=music.mute=!value;if(value&&!music.isPlaying)music.Play();}
        public void Play(string id,float volume=1)
        {if(!Enabled||!clips.ContainsKey(id)||(next.TryGetValue(id,out float at)&&Time.unscaledTime<at))return;next[id]=Time.unscaledTime+(id=="chew"?.22f:.12f);effects.PlayOneShot(clips[id],volume);}
        void Update(){if(game&&game.Actor.Walking&&Time.time>stepAt){stepAt=Time.time+.32f;Play("step",.3f);}if(music)music.pitch=Mathf.Lerp(music.pitch,game&&game.Actor.IsSleeping?.86f:1,Time.deltaTime*.2f);}
        static AudioClip Make(string id,int seed)
        {
            const int rate=22050;float length=id=="sleep"||id=="yawn"?.85f:id=="reward"?.6f:.22f;var samples=new float[(int)(length*rate)];var rng=new System.Random(45+seed);
            for(int i=0;i<samples.Length;i++){float t=i/(float)rate,f=t/length,noise=(float)rng.NextDouble()*2-1,envelope=Mathf.Sin(f*Mathf.PI)*Mathf.Exp(-f*3);
                bool friction=id=="sponge"||id=="brush"||id=="water"||id=="step";float frequency=id=="firefly"?1046:id=="reward"?659+Mathf.Floor(f*3)*164:id=="chew"?180:id=="swallow"?280-f*140:id=="sleep"||id=="yawn"?190+Mathf.Sin(f*Mathf.PI)*50:440+seed*13;
                samples[i]=(friction?noise*.24f+Mathf.Sin(t*180)*.08f:Mathf.Sin(t*2*Mathf.PI*frequency)*.22f+Mathf.Sin(t*2*Mathf.PI*frequency*2)*.04f)*envelope;}
            var clip=AudioClip.Create("Gazik "+id,samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        static AudioClip Music()
        {
            const int rate=22050;const float length=24;var data=new float[(int)(rate*length)];int[] notes={0,7,12,4,9,7,4,2,0,12,9,7,4,2,7,0};
            for(int note=0;note<notes.Length;note++){float frequency=220*Mathf.Pow(2,notes[note]/12f);int start=(int)(note*1.5f*rate);for(int i=0;i<rate*3&&start+i<data.Length;i++){float t=i/(float)rate,envelope=Mathf.Min(1,t*8)*Mathf.Exp(-t*1.8f);data[start+i]+=(Mathf.Sin(t*2*Mathf.PI*frequency)+.2f*Mathf.Sin(t*2*Mathf.PI*frequency*2))*.14f*envelope;}}
            var clip=AudioClip.Create("Quiet woodland lullaby",data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        void OnDestroy(){foreach(var clip in clips.Values)if(clip)Destroy(clip);if(music&&music.clip)Destroy(music.clip);}
    }
}
