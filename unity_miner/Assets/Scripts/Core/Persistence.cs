using System;
using System.Collections.Generic;

namespace Mineros.Core
{
    /// <summary>Guardado y carga (JSON con las mismas claves que el juego Godot, para migrar partidas).</summary>
    public sealed partial class GameState
    {
        public Dictionary<string, object> ToDict()
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["gold"] = Gold;
            d["gems"] = Gems;
            d["lv"] = CopyDict(Lv);
            d["world"] = World;
            d["sub"] = Sub;
            d["progress"] = Progress;
            d["need_boss"] = NeedBoss;
            d["max_stage"] = MaxStage;
            d["inv"] = ToolsToList(Inv);
            d["equip"] = ToolsToList(Equip);
            d["daily"] = CopyDict(Daily);
            d["daily_date"] = DailyDate;
            d["claimed"] = new List<string>(Claimed);
            d["login_day"] = LoginDay;
            d["login_date"] = LoginDate;
            d["pass_xp"] = PassXp;
            d["boost_until"] = BoostUntil;
            d["boost_cd_until"] = BoostCdUntil;
            d["auto_up"] = AutoUp;
            d["eco"] = Eco;
            d["sound"] = Sound;
            d["music"] = Music;
            d["skin"] = Skin;
            d["skins_owned"] = new List<int>(SkinsOwned);
            d["rebirths"] = Rebirths;
            d["turbo"] = Turbo;
            d["free_chest_at"] = FreeChestAt;
            d["piggy"] = Piggy;
            d["total_rocks"] = TotalRocks;
            d["total_gold"] = TotalGold;
            d["last_seen"] = Now();
            d["collection"] = new List<string>(Collection);
            d["stats"] = CopyDict(Stats);
            d["features_unlocked"] = new List<string>(FeaturesUnlocked);
            d["features_seen"] = new List<string>(FeaturesSeen);
            d["essence"] = Essence;
            d["essence_lv"] = CopyDict(EssenceLv);
            d["ach_claimed"] = new List<string>(AchClaimed);
            d["run_max"] = RunMax;
            d["lv_best"] = CopyDict(LvBest);
            d["goal_idx"] = GoalIdx;
            d["goal_base"] = GoalBase;
            d["goal_gen_target"] = GoalGenTarget;
            d["goal_gen_kind"] = GoalGenKind;
            return d;
        }

        public string ToJson() { return Json.Stringify(ToDict()); }

        public void SaveGame() { store.Save(ToJson()); }

        static Dictionary<string, T> CopyDict<T>(Dictionary<string, T> src) { return new Dictionary<string, T>(src); }

        static List<object> ToolsToList(Tool[] tools)
        {
            List<object> l = new List<object>(tools.Length);
            foreach (Tool t in tools)
            {
                if (t == null)
                {
                    l.Add(null);
                    continue;
                }
                Dictionary<string, object> td = new Dictionary<string, object>();
                td["k"] = t.K;
                td["r"] = t.R;
                l.Add(td);
            }
            return l;
        }

        // Herramienta desde JSON; k y r se acotan a rangos validos para que un guardado corrupto no rompa el juego.
        static Tool ToolFrom(object v)
        {
            Dictionary<string, object> d = v as Dictionary<string, object>;
            if (d == null) return null;
            int k = Math.Max(0, Math.Min(Content.ToolNames.Length - 1, JsonRead.Int(d, "k", 0)));
            int r = Math.Max(0, Math.Min(Content.Ranks.Length - 1, JsonRead.Int(d, "r", 0)));
            return new Tool(k, r);
        }

        static List<string> StrList(object v)
        {
            List<string> o = new List<string>();
            List<object> l = v as List<object>;
            if (l != null)
                foreach (object x in l) o.Add(JsonRead.ToStr(x, ""));
            return o;
        }

        /// <summary>Carga la partida desde el ISaveStore (equivale a load_game). Calcula las ganancias offline pendientes.</summary>
        public void LoadGame()
        {
            string text = store.Load();
            if (text == null)
            {
                LastSeen = Now();
                return;
            }
            object parsed;
            Dictionary<string, object> d;
            if (!Json.TryParse(text, out parsed) || (d = parsed as Dictionary<string, object>) == null) return;
            ApplyDict(d);
            double away = Now() - LastSeen;
            if (away > 120.0)
            {
                double t = Math.Min(away, OfflineCap());
                PendingOfflineTime = away;
                PendingOffline = IncomePerSec() * t * OfflineEff();
                Piggy = Math.Min(Balance.PiggyCap, Piggy + (int)(t / 60.0));
            }
        }

        /// <summary>Carga desde un texto JSON; false si no es un JSON de objeto valido (no cambia nada).</summary>
        public bool ApplyJson(string json)
        {
            object parsed;
            Dictionary<string, object> d;
            if (!Json.TryParse(json, out parsed) || (d = parsed as Dictionary<string, object>) == null) return false;
            ApplyDict(d);
            return true;
        }

        /// <summary>Carga un diccionario de partida (guardada o vieja). Campos ausentes -> valores por defecto.</summary>
        public void ApplyDict(Dictionary<string, object> d)
        {
            Gold = JsonRead.Dbl(d, "gold", 0.0);
            Gems = JsonRead.Int(d, "gems", 0);
            Dictionary<string, object> l = JsonRead.Dict(d, "lv");
            Lv = NewLevels();
            foreach (string k in Content.UpgradeKinds)
                Lv[k] = Math.Max(1, JsonRead.Int(l, k, 1));
            World = JsonRead.Int(d, "world", 1);
            Sub = JsonRead.Int(d, "sub", 1);
            Progress = JsonRead.Dbl(d, "progress", 0.0);
            NeedBoss = JsonRead.Bool(d, "need_boss", false);
            MaxStage = JsonRead.Int(d, "max_stage", 0);
            List<object> di = JsonRead.List(d, "inv");
            for (int i = 0; i < Balance.InvSize; i++)
                Inv[i] = di != null && i < di.Count ? ToolFrom(di[i]) : null;
            List<object> de = JsonRead.List(d, "equip");
            for (int i = 0; i < Balance.EquipSlots; i++)
                Equip[i] = de != null && i < de.Count ? ToolFrom(de[i]) : null;
            if (EquippedCount() == 0) Equip[0] = new Tool(0, 0);
            Daily = new Dictionary<string, int>();
            Dictionary<string, object> dd = JsonRead.Dict(d, "daily");
            if (dd != null)
                foreach (KeyValuePair<string, object> kv in dd) Daily[kv.Key] = JsonRead.ToInt(kv.Value, 0);
            DailyDate = JsonRead.Str(d, "daily_date", "");
            Claimed = StrList(JsonRead.Get(d, "claimed"));
            LoginDay = JsonRead.Int(d, "login_day", 0);
            LoginDate = JsonRead.Str(d, "login_date", "");
            PassXp = JsonRead.Int(d, "pass_xp", 0);
            BoostUntil = JsonRead.Dbl(d, "boost_until", 0.0);
            BoostCdUntil = JsonRead.Dbl(d, "boost_cd_until", 0.0);
            AutoUp = JsonRead.Bool(d, "auto_up", false);
            Eco = JsonRead.Bool(d, "eco", false);
            Sound = JsonRead.Bool(d, "sound", true);
            Music = JsonRead.Bool(d, "music", true);
            Skin = JsonRead.Int(d, "skin", 0);
            SkinsOwned = new List<int>();
            List<object> so = JsonRead.List(d, "skins_owned");
            if (so != null)
                foreach (object s in so) SkinsOwned.Add(JsonRead.ToInt(s, 0));
            else if (!JsonRead.Has(d, "skins_owned"))
                SkinsOwned.Add(0);
            Rebirths = JsonRead.Int(d, "rebirths", 0);
            Turbo = JsonRead.Bool(d, "turbo", false);
            FreeChestAt = JsonRead.Dbl(d, "free_chest_at", 0.0);
            Piggy = JsonRead.Int(d, "piggy", 0);
            TotalGold = JsonRead.Dbl(d, "total_gold", 0.0);
            if (JsonRead.Has(d, "collection")) Collection = StrList(JsonRead.Get(d, "collection"));
            LastSeen = JsonRead.Dbl(d, "last_seen", Now());
            // meta-progresion (valores por defecto seguros para partidas viejas)
            Stats = new Dictionary<string, long>();
            Dictionary<string, object> ds = JsonRead.Dict(d, "stats");
            if (ds != null)
                foreach (KeyValuePair<string, object> kv in ds) Stats[kv.Key] = JsonRead.ToLong(kv.Value, 0);
            Stats["rocks"] = Math.Max(StatRaw("rocks"), JsonRead.Lng(d, "total_rocks", 0));
            Essence = JsonRead.Int(d, "essence", 0);
            EssenceLv = new Dictionary<string, int>();
            Dictionary<string, object> de2 = JsonRead.Dict(d, "essence_lv");
            if (de2 != null)
                foreach (KeyValuePair<string, object> kv in de2) EssenceLv[kv.Key] = JsonRead.ToInt(kv.Value, 0);
            AchClaimed = StrList(JsonRead.Get(d, "ach_claimed"));
            RunMax = JsonRead.Int(d, "run_max", StageIdx());
            MaxStage = Math.Max(MaxStage, RunMax);
            LvBest = new Dictionary<string, int> { { "power", Lv["power"] }, { "speed", Lv["speed"] }, { "money", Lv["money"] } };
            Dictionary<string, object> db = JsonRead.Dict(d, "lv_best");
            if (db != null)
                foreach (string k in Content.UpgradeKinds)
                    LvBest[k] = Math.Max(LvBest[k], JsonRead.Int(db, k, 1));
            GoalGenKind = JsonRead.Str(d, "goal_gen_kind", "power");
            if (Content.KindName(GoalGenKind) == null) GoalGenKind = "power";
            GoalGenTarget = JsonRead.Int(d, "goal_gen_target", 0);
            if (JsonRead.Has(d, "features_unlocked"))
            {
                FeaturesUnlocked = StrList(JsonRead.Get(d, "features_unlocked"));
                FeaturesSeen = StrList(JsonRead.Get(d, "features_seen"));
            }
            else
            {
                // partida vieja: todo lo ya alcanzado queda desbloqueado y visto (sin animaciones)
                FeaturesUnlocked = new List<string>();
                FeaturesSeen = new List<string>();
                CheckUnlocks(true);
            }
            if (JsonRead.Has(d, "goal_idx"))
            {
                GoalIdx = Math.Max(0, JsonRead.Int(d, "goal_idx", 0));
                GoalBase = JsonRead.Dbl(d, "goal_base", 0.0);
                goalNotified = CurrentGoal().Done;
            }
            else
            {
                MigrateGoals();
            }
        }
    }
}
