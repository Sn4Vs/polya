using System;
using System.Linq;
using System.IO;
using Gazik.Core;
using UnityEngine;

namespace Gazik.Editor
{
    // Drives the real scene over frames; no live Firebase or PlayerPrefs are used.
    internal sealed class NativeCareChecks
    {
        readonly GazikGame game;readonly Action<bool,string> require;
        readonly string[] actions={"feed","bath","brush","play","sleep"};
        int index=-1,phase,stroke;float started,next;double count;string snapshot;
        void Capture(string label){if(Application.isBatchMode)return;Directory.CreateDirectory("TestResults");ScreenCapture.CaptureScreenshot("TestResults/Unity-"+label+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")+".png");}
        public NativeCareChecks(GazikGame value,Action<bool,string> check){game=value;require=check;}
        public bool Tick()
        {
            if(phase==0)
            {
                index++;if(index>=actions.Length){game.DoCare("sleep");phase=5;started=Time.time;return false;}
                count=game.Rules.State.Number("careCount");snapshot=game.Rules.State.ToJson();started=Time.time;stroke=0;
                game.DoCare(actions[index]);require(game.Care.Busy,"Care enters approach: "+actions[index]);require(snapshot==game.Rules.State.ToJson(),"Approach grants no reward: "+actions[index]);phase=1;return false;
            }
            if(Time.time-started>45)throw new Exception("Care timeout: "+actions[Mathf.Clamp(index,0,4)]+" phase "+phase+" progress "+game.Care.Progress);
            if(phase==1)
            {
                if(actions[index]=="sleep"){if(game.Care.Busy)return false;require(game.Rules.State.Sleeping(PetState.Now),"Sleep starts after climbing into bed");Vector3 p=game.Actor.MotionPosition;require(p.x>-2.3f&&p.x<-.95f&&p.z>.3f&&p.z<1.35f&&p.y>=.45f&&p.y<=.75f,"Sleep position is inside the bed");phase=7;next=Time.time+2.5f;return false;}
                if(!game.Care.Ready)return false;
                require(snapshot==game.Rules.State.ToJson(),"Ready interaction still grants no reward: "+actions[index]);
                require(Mathf.Abs(Mathf.DeltaAngle(game.Actor.transform.eulerAngles.y,actions[index]=="brush"?150:0))<12,"Resident faces action view: "+actions[index]);
                phase=6;next=Time.time+1.3f;return false;
            }
            if(phase==6&&Time.time>=next){Capture(actions[index]);phase=2;next=Time.time+.1f;}
            if(phase==7&&Time.time>=next){Capture("sleep");phase=8;next=Time.time+.8f;}
            if(phase==8&&Time.time>=next)phase=4;
            if(phase==2&&Time.time>=next)
            {
                next=Time.time+.075f;
                if(actions[index]=="feed"||actions[index]=="play")
                {var target=game.GetComponentsInChildren<CareTarget>().FirstOrDefault(t=>t.GetComponent<Collider>().enabled);if(target){game.Care.Pointer(game.Room.Camera.WorldToScreenPoint(target.transform.position),true,false);stroke++;}if(!game.Care.Ready)phase=3;}
                else if(actions[index]=="bath")
                {int n=stroke++%80;float x=-.43f+n%10*.095f,y=-.39f+n/10*.1f;float z=-.08f-.47f*Mathf.Sqrt(Mathf.Max(.12f,1-x*x/(.59f*.59f)-(y+.06f)*(y+.06f)/(.67f*.67f)))-.05f;game.Care.Pointer(game.Room.Camera.WorldToScreenPoint(game.Actor.Body.TransformPoint(new Vector3(x,y,z))),false,true);if(!game.Care.Ready)phase=3;}
                else
                {var target=game.Actor.Body.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name.StartsWith("Quill debris "));if(target)game.Care.Pointer(game.Room.Camera.WorldToScreenPoint(target.position),false,true);if(!game.Care.Ready)phase=3;}
            }
            if(phase==3){if(game.Care.Busy)return false;phase=4;}
            if(phase==4)
            {require(game.Rules.State.Number("careCount")==count+1,"One completed interaction grants exactly one care: "+actions[index]);require(game.Rules.State.List("recent").Count>0,"Completed care is recorded: "+actions[index]);if(actions[index]=="feed")require(game.Rules.State.Stat("hunger")>83,"Eating restores hunger after chewing");if(actions[index]=="sleep"){require((bool)game.Rules.State.Map("daily")["bonusClaimed"],"Five completed 3D care actions grant daily bonus");require(game.Rules.State.Number("affection")>=10,"Regular care increases affection");}phase=0;}
            if(phase==5&&Time.time-started>3)
            {require(!game.Rules.State.Sleeping(PetState.Now),"Wake action ends sleep without a second care reward");require(game.Actor.SleepPose<.1f,"Wake animation unfolds resident");game.Life.Stop();game.Actor.MotionPosition=Vector3.zero;game.Life.Face(0);game.Actor.React("hello");return true;}
            return false;
        }
    }
}
