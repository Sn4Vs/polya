using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Gazik
{
    public sealed class ArcadeView:MonoBehaviour
    {
        GameHud hud;RectTransform root,arena;Text status,meta;Button retry;bool stopped,finished,locked;string id;
        public void Begin(GameHud value,RectTransform parent,string game)
        {
            hud=value;root=parent;id=game;
            status=hud.Label(GameHud.Rect("Instructions",root,new Vector2(0,.89f),Vector2.one,Vector2.zero,Vector2.zero),"",30,TextAnchor.MiddleCenter);
            meta=hud.Label(GameHud.Rect("Score",root,new Vector2(0,.81f),new Vector2(1,.89f),Vector2.zero,Vector2.zero),"",30,TextAnchor.MiddleCenter,GameHud.Accent);
            arena=GameHud.Rect("Arena",root,new Vector2(0,.16f),new Vector2(1,.80f),Vector2.zero,Vector2.zero);hud.Card(arena,"Play area",new Color(0,0,0,.16f));
            retry=hud.Button(GameHud.Rect("Retry",root,Vector2.zero,new Vector2(1,.12f),new Vector2(8,4),new Vector2(-8,-4)),"Ещё раз",Restart);retry.gameObject.SetActive(false);Restart();
        }
        public void Stop(){stopped=true;StopAllCoroutines();}
        void Restart()
        {
            StopAllCoroutines();stopped=false;finished=false;locked=false;retry.gameObject.SetActive(false);
            for(int i=arena.childCount-1;i>=0;i--){var child=arena.GetChild(i);child.gameObject.SetActive(false);Destroy(child.gameObject);}
            hud.Card(arena,"Play area",new Color(0,0,0,.16f));
            if(id=="catch")StartCoroutine(Catch());else if(id=="toss")StartCoroutine(Toss());else StartCoroutine(Memory());
        }
        void Complete(int score,string description)
        {
            if(stopped||finished)return;finished=true;locked=true;status.text=description;
            hud.Game.Reward(id,score);retry.gameObject.SetActive(true);
        }
        IEnumerator Catch()
        {
            int score=0;float end=Time.unscaledTime+15;Button target=null;
            status.text="Лови светящиеся звёзды, пока не закончится время.";
            Action spawn=null;spawn=()=>{
                if(stopped||finished||Time.unscaledTime>=end)return;
                if(target){target.interactable=false;Destroy(target.gameObject);}
                float x=UnityEngine.Random.Range(.13f,.87f),y=UnityEngine.Random.Range(.14f,.86f);
                var cell=GameHud.Rect("Star",arena,new Vector2(x,y),new Vector2(x,y),new Vector2(-58,-58),new Vector2(58,58));bool caught=false;
                target=hud.Button(cell,"✦",()=>{if(caught||finished||Time.unscaledTime>=end)return;caught=true;score++;spawn();},new Color(.67f,.52f,.28f,.9f),58);
            };
            spawn();
            while(!stopped&&Time.unscaledTime<end){meta.text=Mathf.CeilToInt(end-Time.unscaledTime)+" сек · поймано "+score;yield return null;}
            if(stopped)yield break;if(target){target.interactable=false;Destroy(target.gameObject);}Complete(score,"Поймано: "+score+" ✦");
        }
        IEnumerator Toss()
        {
            int throws=0,hits=0;float position=0,startedAt=Time.unscaledTime;status.text="Нажми, когда указатель окажется в зелёной середине";
            var meter=GameHud.Rect("Timing meter",arena,new Vector2(.08f,.45f),new Vector2(.92f,.58f),Vector2.zero,Vector2.zero);hud.Card(meter,"Meter",new Color(.64f,.37f,.29f));hud.Card(GameHud.Rect("Target",meter,new Vector2(.38f,0),new Vector2(.62f,1),Vector2.zero,Vector2.zero),"Green target",new Color(.45f,.69f,.47f));
            var marker=GameHud.Rect("Pointer",meter,Vector2.zero,new Vector2(0,1),new Vector2(-8,-12),new Vector2(8,12));hud.Card(marker,"Pointer",GameHud.Ink);
            Button button=null;button=hud.Button(GameHud.Rect("Throw",arena,new Vector2(.17f,.10f),new Vector2(.83f,.30f),Vector2.zero,Vector2.zero),"бросить яблоко 🍎",()=>{
                if(finished||throws>=5)return;throws++;bool hit=Mathf.Abs(position-.5f)<=.12f;if(hit)hits++;meta.text=(hit?"Идеально! ":"Почти! ")+hits+" / "+throws;hud.Game.Actor.React("play");
                if(throws==5){button.interactable=false;Complete(hits,"Результат: "+hits+" / 5 попаданий");}
            });
            while(!stopped&&!finished){position=Mathf.PingPong((Time.unscaledTime-startedAt)*1.75f,.98f);marker.anchorMin=new Vector2(position,0);marker.anchorMax=new Vector2(position,1);yield return null;}
        }
        IEnumerator Memory()
        {
            int round=0,index=0;bool wrong=false,ready=false;var sequence=new List<int>();var buttons=new List<Button>();
            Color[] colors={new Color(.44f,.66f,.49f),new Color(.70f,.44f,.53f),new Color(.46f,.49f,.70f),new Color(.75f,.62f,.33f)};
            for(int i=0;i<4;i++)
            {
                int n=i,x=i%2,y=i/2;var cell=GameHud.Rect("Memory pad",arena,new Vector2(.1f+x*.43f,.13f+y*.42f),new Vector2(.47f+x*.43f,.49f+y*.42f),new Vector2(5,5),new Vector2(-5,-5));
                buttons.Add(hud.Button(cell,"✦",()=>{if(locked||finished||!ready)return;buttons[n].GetComponent<Image>().color=Color.Lerp(colors[n],Color.white,.5f);StartCoroutine(Restore(buttons[n],colors[n]));if(sequence[index]!=n){wrong=true;ready=false;locked=true;}else{index++;if(index==sequence.Count){ready=false;locked=true;}}},colors[i],65));
            }
            while(!stopped&&!finished)
            {
                round++;sequence.Add(UnityEngine.Random.Range(0,4));index=0;locked=true;ready=false;status.text="Смотри внимательно…";meta.text="Раунд "+round+" из 4";yield return new WaitForSecondsRealtime(.5f);
                foreach(int pad in sequence){if(stopped)yield break;buttons[pad].GetComponent<Image>().color=Color.Lerp(colors[pad],Color.white,.65f);yield return new WaitForSecondsRealtime(.42f);buttons[pad].GetComponent<Image>().color=colors[pad];yield return new WaitForSecondsRealtime(.16f);}
                locked=false;ready=true;status.text="Теперь повтори ✦";
                while(!stopped&&ready)yield return null;if(stopped)yield break;
                if(wrong){status.text="Упс, последовательность сбилась. Попробуй ещё раз.";retry.gameObject.SetActive(true);yield break;}
                if(round==4){Complete(4,"Все 4 раунда пройдены ✦");yield break;}
                status.text="Идеально! Следующий раунд";yield return new WaitForSecondsRealtime(.65f);
            }
        }
        IEnumerator Restore(Button b,Color c){yield return new WaitForSecondsRealtime(.18f);if(b)b.GetComponent<Image>().color=c;}
        void OnDisable(){Stop();}
    }
}
