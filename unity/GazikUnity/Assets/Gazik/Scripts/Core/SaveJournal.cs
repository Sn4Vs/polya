using System;
using System.Collections.Generic;

namespace Gazik.Core
{
    // Two generations of each save, with separate namespaces for local and cloud.
    public sealed class SaveJournal
    {
        readonly string key;readonly Func<string,string> read;readonly Action<string,string> write;readonly Action flush;
        public SaveJournal(string key,Func<string,string> read,Action<string,string> write,Action flush)
        {this.key=key;this.read=read;this.write=write;this.flush=flush;}
        public static PetState Parse(string json,double now)
        {
            if(string.IsNullOrWhiteSpace(json)||json.Length>2*1024*1024)throw new FormatException("Неверный размер резервной копии.");
            var state=PetState.FromJson(json.TrimStart('\uFEFF'));
            if(!state.Data.TryGetValue("stats",out object stats)||!(stats is Dictionary<string,object>))throw new FormatException("В файле нет показателей питомца. Прогресс не изменён.");
            state.Normalize(now);return state;
        }
        bool Valid(string json,double now,out PetState state)
        {try{state=Parse(json,now);return true;}catch{state=null;return false;}}
        public PetState Load(double now,out bool recovered)
        {
            recovered=false;string current=read(key);
            if(Valid(current,now,out var state))return state;
            if(!string.IsNullOrEmpty(current)){write(key+".corrupt",current);flush();}
            if(Valid(read(key+".previous"),now,out state)){recovered=true;return state;}
            return PetState.Fresh(now);
        }
        public void Save(PetState state)
        {
            string json=state.ToJson();Parse(json,PetState.Now);string current=read(key);
            if(current==json)return;
            if(Valid(current,PetState.Now,out _))write(key+".previous",current);
            write(key,json);flush();
        }
        public void Import(string json,PetState current,double now)
        {
            var imported=Parse(json,now); // Validate the whole input before touching storage.
            write(key+".before-import",current.ToJson());Save(imported);
        }
    }
    public sealed class HomeSummary
    {
        public readonly bool[] Done=new bool[5];public int Count,LetterCount,Stage,AgeDays;public float Growth;
        public string MoodMessage,DailyMessage,GrowthMessage;
        public static HomeSummary Read(PetState state,double now,Catalog catalog)
        {
            var s=state.Clone();GameRules.Decay(s,now);var v=new HomeSummary();var daily=s.Map("daily");
            var actions=daily.TryGetValue("key",out var key)&&key as string==PetState.Day(now)&&daily.TryGetValue("actions",out var a)?a as List<object>:null;
            for(int i=0;i<5;i++){v.Done[i]=actions!=null&&actions.Contains(GameRules.Actions[i]);if(v.Done[i])v.Count++;}
            bool claimed=daily.TryGetValue("key",out key)&&key as string==PetState.Day(now)&&daily.TryGetValue("bonusClaimed",out var b)&&b is bool value&&value;
            v.DailyMessage=claimed?"Сегодняшний полный круг заботы уже выполнен ✦":"За полный круг заботы ёжик дарит +8 ⭐ и может открыть новое письмо.";
            v.Stage=GameRules.Stage(s.Number("growthXp"));double[] bounds={0,35,90,170,280};
            v.Growth=v.Stage==4?1:(float)Math.Max(0,Math.Min(1,(s.Number("growthXp")-bounds[v.Stage])/(bounds[v.Stage+1]-bounds[v.Stage])));
            v.GrowthMessage=v.Stage==4?"максимальный рост ✦":Math.Floor(s.Number("growthXp"))+" / "+GameRules.NextGrowth(v.Stage);
            v.AgeDays=(int)Math.Min(int.MaxValue,Math.Max(1,Math.Floor((now-s.Number("bornAt",now))/86400000)+1));
            for(int i=0;i<catalog.Letters.Count;i++)if(s.HasLetter(i))v.LetterCount++;
            string name=string.IsNullOrWhiteSpace(s.Name)?"Ёжик":s.Name;
            v.MoodMessage=s.Sleeping(now)?name+" сладко спит. Энергия понемногу восстанавливается 🌙":s.Mood<42?name+" явно рад, что ты заглянула. Посмотри, чего ему сейчас не хватает 💚":s.Mood>82?name+" выглядит максимально довольным жизнью ✦":name+" ждёт свою Звёздочку.";
            return v;
        }
    }
}
