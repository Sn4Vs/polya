using System;
using System.Collections.Generic;

namespace Gazik.Core
{
    public sealed class Result
    {
        public bool Ok; public string Message, Animation; public bool NewLetter; public int Levels;
        public static Result Fail(string message) => new Result { Message = message, Animation = "" };
    }
    public sealed class GameRules
    {
        public const double Hour = 3600000, SleepDuration = 45 * 60000, GameCooldown = 4 * Hour;
        public static readonly string[] Actions = { "feed", "play", "bath", "brush", "sleep" };
        static readonly double[] Cooldowns = { 1.5 * Hour, 1.5 * Hour, 3 * Hour, 2 * Hour, 4 * Hour };
        static readonly int[] Growth = { 0, 35, 90, 170, 280 };
        public PetState State { get; private set; }
        public Catalog Catalog { get; }
        public bool CanWrite { get; set; }
        readonly Random random; readonly Func<double, string> day;
        public GameRules(PetState state, Catalog catalog, int seed = 0, Func<double, string> dayKey = null)
        { State = state; Catalog = catalog; random = seed == 0 ? new Random() : new Random(seed); day = dayKey ?? PetState.Day; }
        public void Replace(PetState state, double now) { state.Normalize(now); State = state; }
        public static int Stage(double xp) { int i = 0; for (int j = 0; j < Growth.Length; j++) if (xp >= Growth[j]) i = j; return i; }
        public static int NextGrowth(int stage) => stage >= 4 ? 280 : Growth[stage + 1];
        public static float Scale(int stage) => new[] { .72f, .8f, .89f, .98f, 1.07f }[Math.Max(0, Math.Min(4, stage))];
        public static void Decay(PetState s, double now)
        {
            double from = s.Number("lastUpdated", now); if (now <= from) return;
            double end = s.Number("sleepUntil"), cursor = from;
            if (end > cursor)
            {
                double stop = Math.Min(now, end), h = Math.Max(0, (stop - cursor) / Hour);
                s.Stat("hunger", s.Stat("hunger") - 1.15 * h); s.Stat("fun", s.Stat("fun") - .45 * h);
                s.Stat("clean", s.Stat("clean") - .38 * h); s.Stat("energy", s.Stat("energy") + 13.5 * h); cursor = stop;
            }
            double awake = Math.Max(0, (now - cursor) / Hour);
            s.Stat("hunger", s.Stat("hunger") - 3 * awake); s.Stat("fun", s.Stat("fun") - 1.65 * awake);
            s.Stat("clean", s.Stat("clean") - awake); s.Stat("energy", s.Stat("energy") - 2.1 * awake);
            if (end > 0 && now >= end) s.Set("sleepUntil", 0); s.Set("lastUpdated", now);
        }
        public void EnsureDay(double now)
        {
            var d = State.Map("daily"); if (!d.TryGetValue("key", out object key) || key as string != day(now))
                State.Set("daily", new Dictionary<string, object> { { "key", day(now) }, { "actions", new List<object>() }, { "bonusClaimed", false } });
            else { if (!d.ContainsKey("actions") || !(d["actions"] is List<object>)) d["actions"] = new List<object>(); if (!d.ContainsKey("bonusClaimed")) d["bonusClaimed"] = false; }
        }
        public PetState View(double now) { PetState copy = State.Clone(); Decay(copy, now); return copy; }
        public double CooldownLeft(string action, double now)
        { int index = Array.IndexOf(Actions, action); return index < 0 ? double.MaxValue : Math.Max(0, Cooldowns[index] - (now - PetState.N(State.Map("lastActions"), action))); }
        void Recent(string line) { State.List("recent").Insert(0, line); if (State.List("recent").Count > 8) State.List("recent").RemoveRange(8, State.List("recent").Count - 8); }
        bool Letter(bool force)
        {
            var locked = new List<int>(); for (int i = 0; i < Catalog.Letters.Count; i++) if (!State.HasLetter(i)) locked.Add(i);
            if (locked.Count == 0 || (!force && random.NextDouble() > .34)) return false;
            State.List("letters").Add(locked[random.Next(locked.Count)]); return true;
        }
        Result Finish(Result r, int beforeStage, double now)
        {
            int after = Stage(State.Number("growthXp")); r.Levels = Math.Max(0, after - beforeStage);
            if (r.Levels > 0) {State.Set("stars", State.Number("stars") + 3);r.NewLetter=Letter(true)||r.NewLetter;} // Existing currency reward; growth also unlocks a letter.
            State.Set("lastUpdated", now); Recent(r.Message); r.Ok = true; return r;
        }
        public Result CanStartCare(string action, double now)
        {
            if (!CanWrite) return Result.Fail("Ты сейчас в режиме просмотра — ухаживает за ним Звёздочка 💚");
            if (Array.IndexOf(Actions, action) < 0) return Result.Fail("Неизвестное действие.");
            // Validate against a view before mutating: refused play must not grant XP or stars.
            PetState view = View(now);
            if (view.Sleeping(now)) return Result.Fail(action=="sleep"?"Он уже спит. Проснётся примерно через "+Math.Ceiling((view.Number("sleepUntil")-now)/60000)+" мин. 🌙":view.Name+" сейчас сладко спит. Пусть немного отдохнёт 🌙");
            double cooldown=CooldownLeft(action,now);if(cooldown>0)return Result.Fail(cooldown<Hour?"Ёжик совсем недавно это делал. Ещё примерно "+Math.Ceiling(cooldown/60000)+" мин.":"Он пока в порядке. Ещё примерно "+Math.Ceiling(cooldown/Hour)+" ч.");
            if (action == "play" && view.Stat("energy") < 15) return Result.Fail("Он очень устал для активной игры. Лучше сначала дать ему поспать 🌙");
            return new Result { Ok=true };
        }
        public Result Care(string action, double now,string food="apple")
        {
            Result allowed=CanStartCare(action,now);if(!allowed.Ok)return allowed;
            PetState view=View(now);
            State = view; EnsureDay(now); int before = Stage(State.Number("growthXp"));
            State.Map("lastActions")[action] = now; var daily = State.Map("daily"); var performed = (List<object>)daily["actions"];
            if (!performed.Contains(action)) performed.Add(action);
            State.Set("careCount", State.Number("careCount") + 1); State.Set("growthXp", State.Number("growthXp") + 2); State.Set("stars", State.Number("stars") + 1);
            State.Set("affection",PetState.Clamp(State.Number("affection")+2));State.Set("lastSeenAt",now);
            string msg = ""; string name = State.Name;
            switch (action)
            {
                case "feed": State.Stat("hunger", State.Stat("hunger") + 34); State.Stat("fun", State.Stat("fun") + 3); msg = name + (food=="berry"?" удивлённо пробует лесную ягоду ✦":food=="cookie"?" обожает звёздное печенье ✦":food=="nut"?" ворчит на орешек, но всё равно доедает его":" довольно хрустит яблочком 🍎"); break;
                case "play": State.Stat("fun", State.Stat("fun") + 32); State.Stat("energy", State.Stat("energy") - 8); State.Stat("clean", State.Stat("clean") - 3); msg = name + " носится по домику так, будто тренируется на чемпионат 🧶"; break;
                case "bath": State.Stat("clean", State.Stat("clean") + 42); State.Stat("fun", State.Stat("fun") + 3); msg = name + " теперь подозрительно чистый и пахнет уютом 🫧"; break;
                case "brush": State.Stat("clean", State.Stat("clean") + 19); State.Stat("fun", State.Stat("fun") + 12); msg = "Иголки " + name + " теперь лежат почти идеально ✨"; if (random.NextDouble() < .25) { State.Set("stars", State.Number("stars") + 2); msg += " Между иголками случайно нашлись ещё 2 ⭐"; } break;
                case "sleep": State.Set("sleepUntil", now + SleepDuration); State.Stat("fun", State.Stat("fun") + 5); msg = name + " свернулся клубочком и заснул. Тише… 🌙"; break;
            }
            var result = new Result { Animation = action, Message = msg };
            bool complete = true; foreach (string a in Actions) if (!performed.Contains(a)) complete = false;
            if (complete && !(daily["bonusClaimed"] is bool claimed && claimed))
            { daily["bonusClaimed"] = true; State.Set("stars", State.Number("stars") + 8); State.Set("growthXp", State.Number("growthXp") + 8); result.NewLetter = Letter(true); result.Message += " Полный круг заботы выполнен: +8 ⭐"; }
            else if ((int)State.Number("careCount") % 4 == 0) result.NewLetter = Letter(false);
            if (random.NextDouble() < .1) { State.Set("stars", State.Number("stars") + 2); result.Message += " Ёжик нашёл под ковриком ещё 2 ⭐"; }
            return Finish(result, before, now);
        }
        public Result Shop(string id, double now)
        {
            if (!CanWrite) return Result.Fail("Магазином управляет Звёздочка 💚");
            ShopItem item = Catalog.Shop.Find(i => i.Id == id); if (item == null) return Result.Fail("Предмет не найден.");
            var s = View(now);
            if(!s.Owns(id)&&Stage(s.Number("growthXp"))<item.MinStage)return Result.Fail("Этот предмет откроется, когда Газик немного подрастёт 🌿");
            bool owned = s.Owns(id); double count = Math.Max(0, PetState.N(s.Map("consumables"), id));
            if(item.Category=="treat"&&count>0&&s.Sleeping(now))return Result.Fail("Вкусняшка подождёт, пока Газик проснётся 🌙");
            if (!owned && !(item.Category == "treat" && count > 0) && s.Number("stars") < item.Cost) return Result.Fail("Не хватает " + Math.Ceiling(item.Cost - s.Number("stars")) + " ⭐. Их можно заработать заботой.");
            State = s; string message; string anim = "happy";
            if(owned&&item.Category=="decor")
            {var hidden=State.List("hiddenDecor");if(hidden.Contains(id)){hidden.Remove(id);message=item.Name+" снова в домике.";}else{hidden.Add(id);message=item.Name+" убрано в инвентарь.";}}
            else if (owned && (item.Category == "outfit" || item.Category == "toy"))
            { string key = item.Category == "outfit" ? "outfit" : "toy"; bool remove = State.Equipped(key) == id; State.Map("equipped")[key] = remove ? "" : id; message = item.Category=="outfit"?(remove?"Наряд снят.":item.Name+" теперь на ёжике ✦"):(remove?"Игрушка убрана.":item.Name+" теперь лежит рядом 🧸"); }
            else if (item.Category == "treat" && count > 0)
            { State.Map("consumables")[id] = count - 1; State.Stat("hunger", State.Stat("hunger") + 11); State.Stat("fun", State.Stat("fun") + 13); anim = id == "strawberry" ? "feed" : id == "starcookie" ? "dance" : id == "moonberry" ? "float" : new[] { "dance", "float", "zoom", "rainbow" }[random.Next(4)]; if (!State.List("rareSeen").Contains(id)) State.List("rareSeen").Add(id); message = State.Name + " попробовал: " + item.Name + " ✦"; }
            else
            { State.Set("stars", State.Number("stars") - item.Cost); if (item.Category == "treat") State.Map("consumables")[id] = count + 1; else { State.List("owned").Add(id); if (item.Category == "outfit") State.Map("equipped")["outfit"] = id; if (item.Category == "toy") State.Map("equipped")["toy"] = id; } message = item.Name + " куплено ✦"; }
            State.Set("lastUpdated", now); Recent(message); return new Result { Ok = true, Animation = anim, Message = message };
        }
        public Result AwardGame(string id, int score, double now)
        {
            if (!CanWrite) return Result.Fail("Режим просмотра — награды не начисляются.");
            if (id != "catch" && id != "toss" && id != "memory") return Result.Fail("Неизвестная игра.");
            if (GameRewardLeft(id, now) > 0) return Result.Fail("Награда сейчас на перерыве.");
            if (score < 0 || (id != "toss" && score == 0) || (id == "toss" && score > 5) || (id == "memory" && score != 4) || (id == "catch" && score > 100)) return Result.Fail("Результат не даёт награду.");
            State = View(now); int before = Stage(State.Number("growthXp")); int reward = id == "memory" ? 4 : id == "toss" ? (score >= 4 ? 4 : score >= 3 ? 3 : score >= 1 ? 2 : 1) : score >= 11 ? 4 : score >= 7 ? 3 : score >= 4 ? 2 : 1;
            State.Map("gameRewards")[id] = now; State.Set("stars", State.Number("stars") + reward); State.Set("growthXp", State.Number("growthXp") + 1);
            return Finish(new Result { Message = "Награда: " + reward + " ⭐", Animation = "happy" }, before, now);
        }
        public double GameRewardLeft(string id, double now) => Math.Max(0, GameCooldown - (now - PetState.N(State.Map("gameRewards"), id)));
        public bool Bond(double now)
        {
            if(!CanWrite||now-State.Number("lastBondAt")<600000)return false;
            State=View(now);State.Set("affection",PetState.Clamp(State.Number("affection")+.5));State.Set("lastBondAt",now);return true;
        }
        public Result Surprise(double now)
        {
            if(!CanWrite||State.Sleeping(now)||State.Number("careCount")<3||now-State.Number("lastRareAt")<8*Hour||random.NextDouble()>.12)return Result.Fail("");
            State=View(now);State.Set("lastRareAt",now);string message;int choice=random.Next(3);bool letter=false;
            if(choice==0){State.Set("stars",State.Number("stars")+2);message=State.Name+" нашёл в листиках две звёздочки ⭐";}
            else if(choice==1){State.Map("consumables")["moonberry"]=PetState.N(State.Map("consumables"),"moonberry")+1;message=State.Name+" принёс лунную ягоду. Она ждёт в запасе вкусняшек ✦";}
            else{letter=Letter(true);message=letter?State.Name+" нашёл для тебя ещё одно письмо 💌":State.Name+" устроил маленький лесной танец 💚";}
            State.Set("lastUpdated",now);Recent(message);return new Result{Ok=true,Message=message,Animation=choice==2?"dance":"proud",NewLetter=letter};
        }
        public Result Wake(double now)
        {
            if(!CanWrite)return Result.Fail("Режим просмотра.");if(!State.Sleeping(now))return Result.Fail("Газик уже проснулся.");
            State=View(now);State.Set("sleepUntil",0);State.Set("lastUpdated",now);
            return new Result{Ok=true,Animation="stretch",Message=State.Name+" проснулся и потягивается 🌿"};
        }
        public Result Rename(string name, double now)
        {
            if (!CanWrite) return Result.Fail("Режим просмотра.");
            string clean = System.Text.RegularExpressions.Regex.Replace((name ?? "").Trim(),@"\s+"," ");
            if(clean.Length<2)return Result.Fail("Имя должно быть хотя бы из двух символов.");if(clean.Length>18)return Result.Fail("Сделай имя чуть короче.");
            State = View(now); State.Set("name", clean); State.Set("lastUpdated", now);Recent("Сегодня у ёжика появилось имя — "+clean+" ✦");return new Result { Ok = true, Message = clean+" запомнил своё имя 💚", Animation = "hello" };
        }
    }
}
