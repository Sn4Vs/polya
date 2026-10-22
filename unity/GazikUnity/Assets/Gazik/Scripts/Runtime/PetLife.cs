using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gazik
{
    // Navigation owns the root; the character rig owns breathing and gestures.
    [DefaultExecutionOrder(-30)]
    public sealed class PetLife:MonoBehaviour
    {
        GazikGame game;readonly Queue<Vector3> route=new Queue<Vector3>();Action arrived;
        float nextWalk;bool hadSleep;public bool Moving{get;private set;}
        static readonly Vector3[] Stops={new Vector3(-.8f,0,-.65f),new Vector3(.1f,0,-1.1f),new Vector3(0,0,.65f),new Vector3(-.6f,0,-.45f),new Vector3(.2f,0,.05f)};
        public void Init(GazikGame value){game=value;game.Actor.ManagedMotion=true;nextWalk=Time.time+5;}
        public void Go(Vector3 point,Action done)
        {
            route.Clear();arrived=done;Vector3 from=game.Actor.MotionPosition;
            // Furniture occupies both rear corners. Route across the open clearing first.
            if(from.x<-1&&from.z>.1f)route.Enqueue(new Vector3(-.75f,0,-.75f));
            if(from.x>1&&from.z>-.9f)route.Enqueue(new Vector3(.5f,0,-1.35f));
            if(point.x>1)route.Enqueue(new Vector3(.5f,0,-1.35f));
            if(point.x<-1)route.Enqueue(new Vector3(-.75f,0,-.75f));
            route.Enqueue(point);Moving=true;game.Actor.Walking=true;game.Actor.ClearLook();
        }
        public void Stop(){route.Clear();arrived=null;Moving=false;if(game)game.Actor.Walking=false;nextWalk=Time.time+UnityEngine.Random.Range(7,13);}
        public void Face(float yaw){game.Actor.Facing=yaw;}
        void Update()
        {
            if(!game||!game.Actor)return;var actor=game.Actor;
            if(actor.IsSleeping){if(!hadSleep)Stop();actor.MotionPosition=new Vector3(-1.48f,Mathf.Lerp(.70f,.48f,actor.SleepPose),.70f);hadSleep=true;Face(0);return;}
            if(hadSleep){hadSleep=false;actor.MotionPosition=new Vector3(-1.2f,.48f,.65f);Go(new Vector3(-.75f,0,-.4f),()=>actor.React("stretch"));}
            if(Moving&&route.Count>0)
            {
                Vector3 destination=route.Peek(),delta=destination-actor.MotionPosition;
                actor.MotionPosition=Vector3.MoveTowards(actor.MotionPosition,destination,Time.deltaTime*.67f);
                if(new Vector2(delta.x,delta.z).sqrMagnitude>.002f)Face(Mathf.Atan2(-delta.x,-delta.z)*Mathf.Rad2Deg);
                if(delta.magnitude<.045f){actor.MotionPosition=destination;route.Dequeue();if(route.Count==0){Moving=false;actor.Walking=false;Face(0);nextWalk=Time.time+UnityEngine.Random.Range(7,14);var callback=arrived;arrived=null;if(callback!=null)callback();else{actor.React(new[]{"wink","curious","sniff","smirk","hello","tongue","grumble","shy","sigh","proud","relaxed","surprise"}[UnityEngine.Random.Range(0,12)]);game.TrySurprise();}}}
            }
            else if(!game.Care.Busy&&!game.Hud.HasPanel&&Time.time>nextWalk)Go(Stops[UnityEngine.Random.Range(0,Stops.Length)],null);
        }
    }
    public sealed class PetTouchZone:MonoBehaviour{public string Zone;}
    public sealed class CareTarget:MonoBehaviour{public int Index;}
}
