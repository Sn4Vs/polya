using System;
using System.Collections.Generic;
using Gazik.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gazik
{
    public sealed class GameHud:MonoBehaviour
    {
        public GazikGame Game{get;private set;}public RectTransform Root{get;private set;}
        public static readonly Color Ink=new Color(.97f,.94f,.86f),Muted=new Color(.72f,.76f,.70f),Panel=new Color(.10f,.17f,.15f,.94f),Accent=new Color(.71f,.81f,.62f);
        Font font;Sprite rounded;Canvas canvas;CanvasScaler scaler;Text nameText,starText,statusText,dailyText,toastText,moodText,mailText;RectTransform modal;ArcadeView arcade;
        readonly List<Image> bars=new List<Image>();readonly List<Text> statText=new List<Text>();readonly List<Button> careButtons=new List<Button>();readonly List<Text> careTimes=new List<Text>();
        string tab="",category="outfit";float toastUntil;int oldWidth,oldHeight;
        RectTransform homeBottom,careBar;Text careHint;Image careProgress;
        RectTransform homeTop,careTop;Text careName;GameObject cloudControl;
        RectTransform restBar;Text restHint;Button wakeButton;
        string pendingToast;
        public bool HasPanel=>modal;
        bool Portrait=>Screen.height>Screen.width;
        public void Init(GazikGame game)
        {
            Game=game;Game.Changed+=OnChanged;font=Resources.Load<Font>("GazikUI");if(!font)font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var tex=new Texture2D(64,64,TextureFormat.RGBA32,false);tex.name="Rounded UI mask";tex.filterMode=FilterMode.Bilinear;
            var pixels=new Color[64*64];for(int y=0;y<64;y++)for(int x=0;x<64;x++){float dx=Mathf.Max(0,Mathf.Abs(x-31.5f)-20),dy=Mathf.Max(0,Mathf.Abs(y-31.5f)-20);float a=Mathf.Clamp01(12-Mathf.Sqrt(dx*dx+dy*dy));pixels[y*64+x]=new Color(1,1,1,a);}tex.SetPixels(pixels);tex.Apply();
            rounded=Sprite.Create(tex,new Rect(0,0,64,64),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(16,16,16,16));
            var o=new GameObject("Gazik HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas=o.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=20;
            scaler=o.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.matchWidthOrHeight=0;
            Root=Rect("Safe area",o.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            if(!FindAnyObjectByType<EventSystem>()){var events=new GameObject("EventSystem");events.AddComponent<EventSystem>();events.AddComponent<StandaloneInputModule>();}
            Rebuild();
        }
        public static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax)
        {var o=new GameObject(name,typeof(RectTransform));var r=o.GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.offsetMin=offsetMin;r.offsetMax=offsetMax;return r;}
        public Image Card(Transform parent,string name,Color color)
        {var r=Rect(name,parent,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);var i=r.gameObject.AddComponent<Image>();i.sprite=rounded;i.type=Image.Type.Sliced;i.color=color;return i;}
        public Text Label(Transform parent,string value,int size=30,TextAnchor align=TextAnchor.MiddleLeft,Color? color=null)
        {var r=Rect("Text",parent,Vector2.zero,Vector2.one,new Vector2(18,6),new Vector2(-18,-6));var text=r.gameObject.AddComponent<GazikText>();text.font=font;text.text=value;text.fontSize=size;text.color=color??Ink;text.alignment=align;text.supportRichText=false;text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;return text;}
        public Button Button(Transform parent,string value,Action click,Color? color=null,int size=29)
        {var image=Card(parent,value,color??Accent);var b=image.gameObject.AddComponent<Button>();var colors=b.colors;colors.highlightedColor=new Color(1.08f,1.08f,1.08f,1);colors.pressedColor=new Color(.82f,.87f,.82f,1);colors.disabledColor=new Color(.65f,.65f,.65f,.62f);b.colors=colors;b.targetGraphic=image;b.onClick.AddListener(()=>{if(Game.Sounds)Game.Sounds.Play("button",.3f);click();});image.gameObject.AddComponent<UiMotion>();Label(image.transform,value,size,TextAnchor.MiddleCenter,color.HasValue?Ink:new Color(.12f,.23f,.18f));return b;}
        RectTransform Fixed(Transform parent,string name,float x,float y,float width,float height,Vector2 anchor)
        {var r=Rect(name,parent,anchor,anchor,Vector2.zero,Vector2.zero);r.pivot=anchor;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(width,height);return r;}
        void Rebuild()
        {
            // Keep dialogs and running games alive through resize / mobile keyboard changes.
            if(modal)modal.SetParent(canvas.transform,false);
            foreach(Transform child in Root){child.gameObject.SetActive(false);Destroy(child.gameObject);}bars.Clear();statText.Clear();careButtons.Clear();careTimes.Clear();
            oldWidth=Screen.width;oldHeight=Screen.height;scaler.referenceResolution=Portrait?new Vector2(900,1600):new Vector2(1600,1000);scaler.matchWidthOrHeight=Portrait?0:1;
            Rect safe=Screen.safeArea;Root.anchorMin=new Vector2(safe.x/Screen.width,safe.y/Screen.height);Root.anchorMax=new Vector2(safe.xMax/Screen.width,safe.yMax/Screen.height);Root.offsetMin=Root.offsetMax=Vector2.zero;
            float margin=Portrait?24:32,topHeight=Portrait?350:280;
            var top=Rect("Top",Root,new Vector2(0,1),new Vector2(1,1),new Vector2(margin,-topHeight),new Vector2(-margin,-margin));Card(top,"Frosted header",Panel);
            homeTop=top;
            var nameBox=Rect("Name",top,new Vector2(0,.65f),new Vector2(.50f,1),new Vector2(12,0),new Vector2(-4,0));var nameButton=Button(nameBox,Game.Rules.State.Name,ShowName,new Color(0,0,0,0),Portrait?44:40);nameText=nameButton.GetComponentInChildren<Text>();
            nameText.resizeTextForBestFit=true;nameText.resizeTextMinSize=28;nameText.resizeTextMaxSize=Portrait?44:40;
            starText=Label(Rect("Stars",top,new Vector2(.50f,.70f),new Vector2(.77f,1),Vector2.zero,Vector2.zero),"",32,TextAnchor.MiddleRight,Shapes.Hex("#E6C47E"));
            Button(Rect("Sound",top,new Vector2(.78f,.65f),new Vector2(.98f,.99f),Vector2.zero,Vector2.zero),"Звук",Game.ToggleSound,new Color(1,1,1,.065f),Portrait?30:25);
            string[] stats={"Сытость","Радость","Чистота","Энергия","Близость"};Color[] colors={Shapes.Hex("#C8B27A"),Shapes.Hex("#D8A4AA"),Shapes.Hex("#8CBEC2"),Shapes.Hex("#A7C38A"),Shapes.Hex("#B7A0CC")};
            for(int i=0;i<5;i++)
            {
                int col=i%3,row=i/3;var s=Rect(stats[i],top,new Vector2(col/3f,.41f-row*.24f),new Vector2((col+1)/3f,.65f-row*.24f),new Vector2(14,0),new Vector2(-14,0));
                var statLabel=Label(Rect("Label",s,new Vector2(0,.25f),Vector2.one,Vector2.zero,Vector2.zero),stats[i],Portrait?33:22,TextAnchor.MiddleLeft);statLabel.rectTransform.offsetMin=new Vector2(0,0);statLabel.rectTransform.offsetMax=Vector2.zero;statLabel.resizeTextForBestFit=true;statLabel.resizeTextMinSize=16;statLabel.resizeTextMaxSize=Portrait?33:22;statText.Add(statLabel);
                var track=Rect("Track",s,Vector2.zero,new Vector2(1,.22f),new Vector2(8,4),new Vector2(-8,-3));Card(track,"Track",new Color(1,1,1,.09f));var fill=Card(track,"Fill",colors[i]);bars.Add(fill);
            }
            var dailyButton=Button(Rect("Daily",top,Vector2.zero,new Vector2(1,.35f),Vector2.zero,Vector2.zero),"",ShowDaily,new Color(0,0,0,0),Portrait?24:21);dailyText=dailyButton.GetComponentInChildren<Text>();dailyText.color=Muted;dailyText.rectTransform.anchorMax=new Vector2(1,.49f);dailyText.rectTransform.offsetMin=new Vector2(18,0);dailyText.rectTransform.offsetMax=new Vector2(-18,0);
            var bottom=Rect("Bottom",Root,Vector2.zero,new Vector2(1,0),new Vector2(margin,margin),new Vector2(-margin,Portrait?460:245));homeBottom=bottom;
            var care=Rect("Care",bottom,new Vector2(0,Portrait?.30f:.38f),Vector2.one,Vector2.zero,new Vector2(0,-2));Card(care,"Care tray",Panel);
            string[] labels={"Покормить","Поиграть","Ванна","Иголки","Уложить"};
            for(int i=0;i<5;i++)
            {
                int index=i;int col=i<3?i:i-3;float columns=i<3?3:2;
                var cell=Rect(labels[i],care,Portrait?new Vector2(col/columns,i<3?.5f:0):new Vector2(i*.2f,0),Portrait?new Vector2((col+1)/columns,i<3?1:.5f):new Vector2((i+1)*.2f,1),new Vector2(7,9),new Vector2(-7,-9));
                var b=Button(cell,labels[i],()=>Game.DoCare(GameRules.Actions[index]),new Color(1,1,1,.075f),Portrait?30:25);var text=b.GetComponentInChildren<Text>();text.rectTransform.anchorMin=new Vector2(0,.24f);text.rectTransform.anchorMax=new Vector2(1,.56f);text.rectTransform.offsetMin=new Vector2(4,0);text.rectTransform.offsetMax=new Vector2(-4,0);careButtons.Add(b);
                var icon=Fixed(b.transform,"Care icon",0,Portrait?-10:-5,Portrait?52:40,Portrait?52:40,new Vector2(.5f,1)).gameObject.AddComponent<Image>();icon.sprite=UiIcons.Get(GameRules.Actions[index]);icon.color=Shapes.Hex(new[]{"#D6AD88","#D4A5B0","#A6D0CF","#DCC38B","#C5C99A"}[i]);icon.raycastTarget=false;
                var time=Label(Rect("Time",b.transform,new Vector2(0,.02f),new Vector2(1,.27f),Vector2.zero,Vector2.zero),"",Portrait?22:20,TextAnchor.MiddleCenter,Muted);time.rectTransform.offsetMin=new Vector2(4,0);time.rectTransform.offsetMax=new Vector2(-4,0);careTimes.Add(time);
            }
            string[] tabs={"Игры","Магазин","Письма"};Action[] callbacks={ShowGames,()=>ShowShop(),ShowMail};
            for(int i=0;i<3;i++){var nav=Button(Rect(tabs[i],bottom,new Vector2(i/3f,0),new Vector2((i+1)/3f,Portrait?.27f:.33f),new Vector2(5,0),new Vector2(-5,0)),tabs[i],callbacks[i],Panel,30);if(i==2)mailText=nav.GetComponentInChildren<Text>();}
            statusText=Label(Fixed(Root,"Cloud status",0,Portrait?-360:-290,Portrait?840:1100,52,new Vector2(.5f,1)),"",Portrait?23:22,TextAnchor.MiddleCenter,Ink);
            statusText.raycastTarget=false;
            cloudControl=Button(Fixed(Root,"Cloud button",margin,Portrait?-420:-355,Portrait?240:230,Portrait?112:78,new Vector2(0,1)),"Облако",ShowCloud,Panel,Portrait?30:25).gameObject;
            var mood=Button(Fixed(Root,"Pet message",0,Portrait?480:270,Portrait?820:950,Portrait?112:90,new Vector2(.5f,0)),"",ShowDaily,new Color(.10f,.17f,.15f,.82f),Portrait?26:24);moodText=mood.GetComponentInChildren<Text>();
            var toastBox=Fixed(Root,"Feedback",0,Portrait?480:270,Portrait?820:950,Portrait?100:90,new Vector2(.5f,0));Card(toastBox,"Feedback background",new Color(.08f,.14f,.12f,1));toastText=Label(toastBox,"",Portrait?28:26,TextAnchor.MiddleCenter);toastBox.gameObject.SetActive(false);
            careBar=Fixed(Root,"Care interaction",0,24,Portrait?840:1100,Portrait?170:120,new Vector2(.5f,0));Card(careBar,"Care background",Panel);
            careHint=Label(Rect("Instruction",careBar,new Vector2(0,.23f),new Vector2(.73f,1),Vector2.zero,Vector2.zero),"",Portrait?29:27);
            Button(Rect("Cancel care",careBar,new Vector2(.74f,.20f),new Vector2(.98f,.87f),Vector2.zero,Vector2.zero),"Вернуться",()=>Game.Care.Cancel(),new Color(1,1,1,.1f),26);
            var progress=Rect("Progress",careBar,new Vector2(.03f,.1f),new Vector2(.7f,.16f),Vector2.zero,Vector2.zero);Card(progress,"Track",new Color(1,1,1,.1f));careProgress=Card(progress,"Fill",Accent);
            careTop=Fixed(Root,"Care compact header",0,-24,Portrait?840:1100,140,new Vector2(.5f,1));Card(careTop,"Compact background",Panel);careName=Label(Rect("Name",careTop,Vector2.zero,new Vector2(.72f,1),Vector2.zero,Vector2.zero),"",32,TextAnchor.MiddleCenter);Button(Rect("Cloud",careTop,new Vector2(.74f,.10f),new Vector2(.98f,.90f),Vector2.zero,Vector2.zero),"Облако",ShowCloud,new Color(1,1,1,.08f),Portrait?30:25);
            restBar=Fixed(Root,"Sleep controls",0,24,Portrait?840:1100,160,new Vector2(.5f,0));Card(restBar,"Sleep background",Panel);restHint=Label(Rect("Rest",restBar,Vector2.zero,new Vector2(.70f,1),Vector2.zero,Vector2.zero),"",28);wakeButton=Button(Rect("Wake",restBar,new Vector2(.71f,.15f),new Vector2(.98f,.85f),Vector2.zero,Vector2.zero),"Разбудить",()=>Game.DoCare("sleep"),new Color(1,1,1,.1f),27);
            if(modal){modal.SetParent(Root,false);modal.SetAsLastSibling();}RefreshLive();
        }
        void Update()
        {
            if(Screen.width!=oldWidth||Screen.height!=oldHeight)Rebuild();bool busy=Game.Care&&Game.Care.Busy,resting=Game.Actor.IsSleeping&&!busy,immersive=busy||resting;
            if(homeBottom)homeBottom.gameObject.SetActive(!immersive);if(homeTop)homeTop.gameObject.SetActive(!immersive);if(statusText)statusText.gameObject.SetActive(!immersive);if(cloudControl)cloudControl.SetActive(!immersive);
            if(careTop){careTop.gameObject.SetActive(immersive);careName.text=Game.Rules.State.Name+"   ·   "+(int)Game.Rules.State.Number("stars")+" ⭐";}
            if(careBar){careBar.gameObject.SetActive(busy);if(busy){careHint.text=Game.Care.Instruction;careProgress.rectTransform.anchorMax=new Vector2(Game.Care.Progress,1);careProgress.rectTransform.offsetMin=careProgress.rectTransform.offsetMax=Vector2.zero;}}
            if(restBar){restBar.gameObject.SetActive(resting);if(resting){restHint.text="Тихий час · ещё "+Math.Max(1,Math.Ceiling((Game.Rules.State.Number("sleepUntil")-PetState.Now)/60000))+" мин. 🌙";wakeButton.interactable=Game.Rules.CanWrite;}}
            if(!immersive&&!HasPanel&&pendingToast!=null){string message=pendingToast;pendingToast=null;Toast(message);}
            if(moodText)moodText.transform.parent.gameObject.SetActive(!immersive&&Time.unscaledTime>toastUntil);if(toastText&&(Time.unscaledTime>toastUntil||immersive))toastText.transform.parent.gameObject.SetActive(false);
        }
        public void RefreshLive()
        {
            if(!nameText||Game.Rules==null)return;double now=PetState.Now;PetState s=Game.Rules.View(now);nameText.text=(string.IsNullOrWhiteSpace(s.Name)?"без имени":s.Name)+"  ·";starText.text=Math.Floor(s.Number("stars"))+" ★";statusText.text=Game.Status;
            var summary=HomeSummary.Read(Game.Rules.State,now,Game.Rules.Catalog);if(moodText)moodText.text=summary.MoodMessage;if(mailText)mailText.text="Письма"+(summary.LetterCount>0?" · "+summary.LetterCount:"");
            string[] keys={"hunger","fun","clean","energy","affection"},labels={"Сытость","Радость","Чистота","Энергия","Близость"};
            for(int i=0;i<5;i++){double value=i==4?s.Number("affection"):s.Stat(keys[i]);statText[i].text=labels[i]+" "+Math.Round(value);bars[i].rectTransform.anchorMax=new Vector2((float)value/100,1);bars[i].rectTransform.offsetMin=bars[i].rectTransform.offsetMax=Vector2.zero;
                }
            for(int i=0;i<5;i++){double left=Game.Rules.CooldownLeft(GameRules.Actions[i],now);careButtons[i].interactable=Game.Rules.CanWrite&&!Game.Care.Busy&&(s.Sleeping(now)?i==4:left<=0);
                careTimes[i].text=s.Sleeping(now)?i==4?"разбудить":"спит":left>0?Math.Ceiling(left/60000)+" мин":new[]{"+ сытость","+ настроение","+ чистота","+ блеск","+ энергия"}[i];}
            int stage=summary.Stage;string growth=stage==4?"максимальный рост ✦":"рост "+(int)s.Number("growthXp")+"/"+GameRules.NextGrowth(stage);dailyText.text=Game.Rules.Catalog.Stages[stage]+" · "+summary.AgeDays+" день · забота "+summary.Count+"/5 · "+growth;
        }
        public void Toast(string text){if(!toastText)return;if(!HasPanel&&(Game.Care.Busy||Game.Rules.State.Sleeping(PetState.Now))){pendingToast=text;return;}toastText.text=text??"";toastUntil=Time.unscaledTime+5;if(moodText)moodText.transform.parent.gameObject.SetActive(false);toastText.transform.parent.gameObject.SetActive(true);toastText.transform.parent.SetAsLastSibling();}
        public RectTransform ShowPanel(string title,string id)
        {
            ClosePanel();tab=id;modal=Rect(title,Root,Vector2.zero,Vector2.one,new Vector2(16,16),new Vector2(-16,-16));Card(modal,"Modal background",Panel);modal.gameObject.AddComponent<UiMotion>().Enter();
            Label(Rect("Title",modal,new Vector2(0,1),Vector2.one,new Vector2(18,-130),new Vector2(-235,-14)),title,Portrait?43:38);
            Button(Fixed(modal,"Close",-16,-16,210,112,Vector2.one),"Закрыть",ClosePanel,new Color(1,1,1,.10f),29);
            return Rect("Body",modal,Vector2.zero,Vector2.one,new Vector2(20,20),new Vector2(-20,-145));
        }
        public void ClosePanel(){if(arcade){arcade.Stop();Destroy(arcade);arcade=null;}if(modal){modal.gameObject.SetActive(false);Destroy(modal.gameObject);modal=null;}tab="";}
        RectTransform Scroll(Transform parent)
        {
            var outer=Rect("Scroll view",parent,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);var scroll=outer.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
            var viewport=Rect("Viewport",outer,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);viewport.gameObject.AddComponent<RectMask2D>();var content=Rect("Content",viewport,new Vector2(0,1),Vector2.one,Vector2.zero,Vector2.zero);content.pivot=new Vector2(.5f,1);
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=14;layout.padding=new RectOffset(3,3,3,3);layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.childControlWidth=true;layout.childForceExpandWidth=true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.viewport=viewport;scroll.content=content;return content;
        }
        RectTransform Row(Transform parent,string title,float height)
        {var r=Rect(title,parent,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);r.gameObject.AddComponent<LayoutElement>().preferredHeight=height;Card(r,"Card",new Color(1,1,1,.055f));return r;}
        void Paragraph(Transform parent,string title,string value,int size,Color color,float minimum=100)
        {
            // Let Unity's layout pass measure the full text after the available width is known.
            var row=Row(parent,title,minimum);var element=row.GetComponent<LayoutElement>();element.preferredHeight=-1;element.minHeight=minimum;
            row.GetComponentInChildren<Image>().gameObject.AddComponent<LayoutElement>().ignoreLayout=true;
            var layout=row.gameObject.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(22,22,20,20);layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;
            Label(row,value,size,TextAnchor.MiddleLeft,color);
        }
        public void ShowGames()
        {
            var body=ShowPanel("Маленькие игры для Газика","games");var list=Scroll(body);string[] ids={"catch","toss","memory"},names={"Лови звёзды","Яблочный бросок","Ёжовая память"},descriptions={"Поймай как можно больше звёзд за 15 секунд.","Пять бросков: останови указатель в середине шкалы.","Запомни и повтори четыре раунда последовательностей."};
            for(int i=0;i<3;i++){int n=i;var row=Row(list,names[i],Portrait?240:210);Label(Rect("Name",row,new Vector2(0,.62f),Vector2.one,new Vector2(5,0),new Vector2(-5,-5)),names[i],36);
                Label(Rect("Description",row,new Vector2(0,.28f),new Vector2(.70f,.64f),Vector2.zero,Vector2.zero),descriptions[i],27,TextAnchor.MiddleLeft,Muted);
                double left=Game.Rules.GameRewardLeft(ids[i],PetState.Now);Label(Rect("Cooldown",row,Vector2.zero,new Vector2(.69f,.28f),Vector2.zero,Vector2.zero),left<=0?"награда доступна":"награда через "+Math.Ceiling(left/60000)+" мин",24,TextAnchor.MiddleLeft,Accent);
                Button(Rect("Play",row,new Vector2(.72f,.10f),new Vector2(.98f,.62f),Vector2.zero,Vector2.zero),"Играть",()=>{var stage=ShowPanel(names[n],"arcade");arcade=gameObject.AddComponent<ArcadeView>();arcade.Begin(this,stage,ids[n]);});}
        }
        public void ShowShop(string selected=null)
        {
            if(selected!=null)category=selected;var body=ShowPanel("Магазин · "+(int)Game.Rules.State.Number("stars")+" ★","shop");
            var tabs=Rect("Categories",body,new Vector2(0,1),Vector2.one,new Vector2(0,Portrait?-110:-90),Vector2.zero);string[] ids={"outfit","decor","toy","treat"},names={"Наряды","Домик","Игрушки","Вкусняшки"};
            for(int i=0;i<4;i++){int n=i;Button(Rect(names[i],tabs,new Vector2(i*.25f,0),new Vector2((i+1)*.25f,1),new Vector2(4,4),new Vector2(-4,-4)),names[i],()=>ShowShop(ids[n]),ids[i]==category?new Color(.36f,.47f,.34f):new Color(1,1,1,.05f),Portrait?25:29);}
            var list=Scroll(Rect("Items",body,Vector2.zero,Vector2.one,Vector2.zero,new Vector2(0,Portrait?-126:-106)));
            foreach(var item in Game.Rules.Catalog.Shop)
            {
                if(item.Category!=category)continue;var row=Row(list,item.Name,Portrait?280:250);Label(Rect("Name",row,new Vector2(0,.66f),Vector2.one,Vector2.zero,Vector2.zero),item.Name,Portrait?33:35);
                bool unlocked=GameRules.Stage(Game.Rules.State.Number("growthXp"))>=item.MinStage||Game.Rules.State.Owns(item.Id);
                Label(Rect("Description",row,new Vector2(0,.26f),new Vector2(.72f,.68f),Vector2.zero,Vector2.zero),item.Description+(unlocked?"":"\nОткроется: "+Game.Rules.Catalog.Stages[item.MinStage]),Portrait?27:28,TextAnchor.MiddleLeft,Muted);
                bool owns=Game.Rules.State.Owns(item.Id);double count=PetState.N(Game.Rules.State.Map("consumables"),item.Id);
                string label=item.Category=="treat"&&count>0?"дать ×"+count:owns?item.Category=="outfit"?(Game.Rules.State.Equipped("outfit")==item.Id?"снять":"надеть"):item.Category=="toy"?(Game.Rules.State.Equipped("toy")==item.Id?"убрать":"поставить"):Game.Rules.State.List("hiddenDecor").Contains(item.Id)?"поставить":"убрать":"купить";
                Label(Rect("Price",row,Vector2.zero,new Vector2(.6f,.26f),Vector2.zero,Vector2.zero),item.Category=="treat"&&count>0?"в запасе: "+count:owns?"есть ✓":item.Cost+" ★",29,TextAnchor.MiddleLeft,Accent);
                var button=Button(Rect("Buy",row,new Vector2(.73f,.13f),new Vector2(.98f,.55f),Vector2.zero,Vector2.zero),unlocked?label:"подрастём",()=>Game.Shop(item.Id),null,Portrait?25:29);button.interactable=Game.Rules.CanWrite&&!Game.Care.Busy&&unlocked;
            }
        }
        public void ShowMail()
        {
            var list=Scroll(ShowPanel("Письма для Звёздочки","mail"));
            if(HomeSummary.Read(Game.Rules.State,PetState.Now,Game.Rules.Catalog).LetterCount==0){Paragraph(list,"Empty","Пока писем нет. Они начнут появляться по мере заботы о ёжике 💚",31,Muted,180);return;}
            for(int i=0;i<Game.Rules.Catalog.Letters.Count;i++)
            {bool open=Game.Rules.State.HasLetter(i);Paragraph(list,"Letter",open?"Письмо "+(i+1).ToString("00")+"\n\n"+Game.Rules.Catalog.Letters[i]:"✦ письмо пока закрыто",Portrait?29:31,open?Ink:Muted,open?240:100);}
        }
        void ShowName()
        {
            var body=ShowPanel("Имя питомца","name");var fieldBox=Rect("Name",body,new Vector2(.04f,1),new Vector2(.96f,1),new Vector2(0,-130),new Vector2(0,-30));Card(fieldBox,"Input",new Color(1,1,1,.09f));var field=fieldBox.gameObject.AddComponent<InputField>();var text=Label(fieldBox,Game.Rules.State.Name,38);((GazikText)text).EncodeEmoji=false;text.text=Game.Rules.State.Name;field.textComponent=text;field.text=Game.Rules.State.Name;field.characterLimit=18;field.lineType=InputField.LineType.SingleLine;field.interactable=Game.Rules.CanWrite;
            Button(Rect("Save",body,new Vector2(.04f,1),new Vector2(.96f,1),new Vector2(0,-260),new Vector2(0,-160)),"Сохранить",()=>{if(Game.Rename(field.text))ClosePanel();});
        }
        public void PromptNameIfNeeded(){if(Game.Rules.CanWrite&&string.IsNullOrWhiteSpace(Game.Rules.State.Name))ShowName();}
        public void ShowDaily()
        {
            var list=Scroll(ShowPanel("Полный круг заботы","daily"));var s=HomeSummary.Read(Game.Rules.State,PetState.Now,Game.Rules.Catalog);
            var growth=Row(list,"Growth",190);Label(Rect("Stage",growth,new Vector2(0,.55f),Vector2.one,Vector2.zero,Vector2.zero),Game.Rules.Catalog.Stages[s.Stage]+" · "+s.AgeDays+" день",32);
            Label(Rect("Growth",growth,new Vector2(0,.20f),new Vector2(1,.55f),Vector2.zero,Vector2.zero),s.GrowthMessage,28,TextAnchor.MiddleLeft,Accent);
            var track=Rect("Growth bar",growth,new Vector2(.04f,.10f),new Vector2(.96f,.16f),Vector2.zero,Vector2.zero);Card(track,"Track",new Color(1,1,1,.10f));var fill=Card(track,"Progress",Accent);fill.rectTransform.anchorMax=new Vector2(s.Growth,1);
            string[] names={"Покормить","Поиграть","Ванна","Иголки","Уложить"};
            for(int i=0;i<5;i++)Label(Row(list,names[i],90),(s.Done[i]?"✓  ":"·  ")+names[i],31,TextAnchor.MiddleLeft,s.Done[i]?Accent:Muted);
            Paragraph(list,"Daily bonus",s.DailyMessage,29,Accent,180);
            Label(Row(list,"Recent title",90),"Недавняя забота",32);
            foreach(var entry in Game.Rules.State.List("recent"))if(entry is string line)Paragraph(list,"Recent",line,27,Muted,150);
        }
        void ShowBackups()
        {
            var body=ShowPanel("Резервная копия","backup");
            Label(Rect("Info",body,new Vector2(.04f,.57f),new Vector2(.96f,.97f),Vector2.zero,Vector2.zero),"Сохрани JSON-копию прогресса на устройство. Импорт доступен в локальном режиме; перед ним сохраняется предыдущий прогресс.",30);
            Button(Rect("Export",body,new Vector2(.08f,.32f),new Vector2(.92f,.48f),Vector2.zero,Vector2.zero),"Скачать резервную копию",Game.Backups.Export);
            var import=Button(Rect("Import",body,new Vector2(.08f,.10f),new Vector2(.92f,.26f),Vector2.zero,Vector2.zero),"Загрузить резервную копию",Game.Backups.Import,new Color(1,1,1,.09f));import.interactable=!Game.Cloud.Active;
        }
        void ShowCloud()
        {
            var body=ShowPanel("Облачный прогресс","cloud");
            Label(Rect("Explanation",body,new Vector2(.04f,.58f),new Vector2(.96f,.98f),Vector2.zero,Vector2.zero),Game.Status+"\n\nИспользуется тот же аккаунт, что на сайте. Сначала загрузим сохранение. Локальная игра хранится отдельно.",Portrait?31:32);
            Button(Rect("Connect",body,new Vector2(.08f,.39f),new Vector2(.92f,.53f),Vector2.zero,Vector2.zero),"Загрузить из облака",()=>{ClosePanel();Game.Cloud.Connect();});
            Button(Rect("Local",body,new Vector2(.08f,.21f),new Vector2(.92f,.35f),Vector2.zero,Vector2.zero),"Играть локально",()=>{Game.Cloud.Disconnect();ClosePanel();},new Color(1,1,1,.09f));
            Button(Rect("Backup",body,new Vector2(.08f,.03f),new Vector2(.92f,.17f),Vector2.zero,Vector2.zero),"Резервная копия",ShowBackups,new Color(1,1,1,.09f));
        }
        void OnChanged(){RefreshLive();if(tab=="shop")ShowShop();else if(tab=="mail")ShowMail();else if(tab=="daily")ShowDaily();}
        void OnDestroy(){if(Game!=null)Game.Changed-=OnChanged;if(canvas)Destroy(canvas.gameObject);if(rounded){Destroy(rounded.texture);Destroy(rounded);}}
    }
}
