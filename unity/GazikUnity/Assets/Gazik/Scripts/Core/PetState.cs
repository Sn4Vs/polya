using System;
using System.Collections.Generic;
using System.Globalization;

namespace Gazik.Core
{
    public sealed class PetState
    {
        public readonly Dictionary<string, object> Data;
        public PetState(Dictionary<string, object> raw) { Data = raw ?? new Dictionary<string, object>(); }
        public static double Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public static string Day(double now) => DateTimeOffset.FromUnixTimeMilliseconds((long)now).ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        public static double Clamp(double n) => Math.Max(0, Math.Min(100, n));
        public double Number(string key, double fallback = 0) => N(Data, key, fallback);
        public string Text(string key, string fallback = "") => Data.TryGetValue(key, out object o) && o is string s ? s : fallback;
        public void Set(string key, object value) { Data[key] = value; }
        public static double N(Dictionary<string, object> d, string key, double fallback = 0)
        { try { if (!d.TryGetValue(key, out object v) || v == null || v is bool || v is string) return fallback; double n = Convert.ToDouble(v, CultureInfo.InvariantCulture); return double.IsNaN(n) || double.IsInfinity(n) ? fallback : n; } catch { return fallback; } }
        public Dictionary<string, object> Map(string key)
        { if (!Data.TryGetValue(key, out object v) || !(v is Dictionary<string, object>)) Data[key] = new Dictionary<string, object>(); return (Dictionary<string, object>)Data[key]; }
        public List<object> List(string key)
        { if (!Data.TryGetValue(key, out object v) || !(v is List<object>)) Data[key] = new List<object>(); return (List<object>)Data[key]; }
        public bool Owns(string id) => List("owned").Contains(id);
        public bool HasLetter(int index)
        {foreach(object value in List("letters")){if(value==null||value is string||value is bool)continue;try{if(Convert.ToDouble(value,CultureInfo.InvariantCulture)==index)return true;}catch{}}return false;}
        public double Stat(string key) => N(Map("stats"), key, 90);
        public void Stat(string key, double value) { Map("stats")[key] = Clamp(value); }
        public string Name => Text("name", "Газик");
        public double Mood => (Stat("hunger") + Stat("fun") + Stat("clean") + Stat("energy")) / 4;
        public bool Sleeping(double now) => Number("sleepUntil") > now;
        public string Equipped(string key) => Map("equipped").TryGetValue(key, out object v) ? v as string ?? "" : "";
        public PetState Clone() => FromJson(ToJson());
        public string ToJson() => Json.Stringify(Data);
        public static PetState FromJson(string json)
        { if (!(Json.Parse(json) is Dictionary<string, object> raw)) throw new FormatException("Save must be an object"); return new PetState(raw); }
        public static PetState Fresh(double now)
        {
            var s = new PetState(new Dictionary<string, object>());
            s.Set("version", 1); s.Set("name", "Газик"); s.Set("bornAt", now); s.Set("lastUpdated", now);
            s.Set("stars", 4); s.Set("growthXp", 0); s.Set("careCount", 0); s.Set("sleepUntil", 0);
            s.Set("affection",0);s.Set("lastSeenAt",now);s.Set("lastBondAt",0);s.List("hiddenDecor");
            foreach (string k in new[] { "hunger", "fun", "clean", "energy" }) s.Stat(k, k == "clean" ? 92 : k == "fun" ? 88 : 90);
            foreach (string k in new[] { "lastActions", "consumables", "gameRewards", "equipped" }) s.Map(k);
            foreach (string k in new[] { "owned", "letters", "recent", "rareSeen" }) s.List(k);
            s.Map("equipped")["outfit"] = ""; s.Map("equipped")["toy"] = "";
            s.Set("daily", new Dictionary<string, object> { { "key", Day(now) }, { "actions", new List<object>() }, { "bonusClaimed", false } });
            return s;
        }
        public void Normalize(double now)
        {
            // Merge defaults without discarding fields belonging to older/newer builds.
            PetState fresh = Fresh(now);
            foreach (var p in fresh.Data) if (!Data.ContainsKey(p.Key)) Data[p.Key] = p.Value;
            foreach (string key in new[] { "stats", "equipped", "lastActions", "gameRewards" })
            { var target = Map(key); foreach (var p in fresh.Map(key)) if (!target.ContainsKey(p.Key)) target[p.Key] = p.Value; }
            foreach (string k in new[] { "hunger", "fun", "clean", "energy" }) Stat(k, Stat(k));
            Set("stars", Math.Max(0, Number("stars"))); Set("growthXp", Math.Max(0, Number("growthXp")));
            Set("affection",Clamp(Number("affection")));List("hiddenDecor");
            foreach (string k in new[] { "owned", "letters", "recent", "rareSeen" }) List(k);
            foreach (string k in new[] { "lastActions", "consumables", "gameRewards", "equipped", "daily" }) Map(k);
        }
    }
    public sealed class ShopItem
    {
        public string Id, Category, Icon, Name, Description; public int Cost,MinStage;
    }
    public sealed class Catalog
    {
        public readonly List<ShopItem> Shop = new List<ShopItem>();
        public readonly List<string> Letters = new List<string>();
        public readonly string[] Stages = { "колючий малыш", "любопытный ёжик", "смелый исследователь", "уютный взрослый ёж", "звёздный хранитель" };
        public static Catalog Load(string json)
        {
            var data = (Dictionary<string, object>)Json.Parse(json); var c = new Catalog();
            foreach (var entry in (List<object>)data["shop"]) { var d = (Dictionary<string, object>)entry; c.Shop.Add(new ShopItem { Id = (string)d["id"], Category = (string)d["cat"], Icon = (string)d["icon"], Name = (string)d["name"], Description = (string)d["desc"], Cost = (int)PetState.N(d, "cost"),MinStage=(int)PetState.N(d,"minStage") }); }
            foreach (var line in (List<object>)data["letters"]) c.Letters.Add((string)line); return c;
        }
    }
}
