using System;
using System.Collections.Generic;
using System.Globalization;
using Mineros.Core;

namespace CoreTests
{
    /// <summary>Pruebas adicionales del port: formato de numeros, JSON, guardado, determinismo, offline, diario y herramientas.</summary>
    public static class ExtraSuite
    {
        static void Check(string n, bool c, string extra = "") { T.Check("extra: " + n, c, extra); }

        public static void Run()
        {
            TestBigNum();
            TestJson();
            TestFullRoundtrip();
            TestGodotFloatSave();
            TestDeterminism();
            TestOffline();
            TestDaily();
            TestTools();
            TestShopAndTick();
            TestEconomyValues();
            TestGeneratedGoals();
        }

        // ---------------------------------------------------------------- BigNum
        // Esperados calculados siguiendo a mano el codigo de num.gd (puerto independiente en Python con la misma logica).
        static readonly object[][] FmtCases =
        {
            new object[] { 0.0, "0" }, new object[] { 0.04, "0" }, new object[] { 0.5, "0.5" }, new object[] { 1.0, "1" },
            new object[] { 1.04, "1" }, new object[] { 1.06, "1.1" }, new object[] { 9.97, "9" }, new object[] { 9.5, "9.5" },
            new object[] { 10.0, "10" }, new object[] { 42.0, "42" }, new object[] { 999.0, "999" }, new object[] { 999.9, "999" },
            new object[] { 1000.0, "1.00K" }, new object[] { 1234.0, "1.23K" }, new object[] { 9995.0, "9.99K" },
            new object[] { 12345.0, "12.3K" }, new object[] { 99999.0, "100.0K" }, new object[] { 100000.0, "100K" },
            new object[] { 123456.0, "123K" }, new object[] { 999994.0, "999K" }, new object[] { 999995.0, "1.00M" },
            new object[] { 1000000.0, "1.00M" }, new object[] { 2500000.0, "2.50M" }, new object[] { 45600000.0, "45.6M" },
            new object[] { 7890000000.0, "7.89B" }, new object[] { 1e12, "1.00T" }, new object[] { 1.5e15, "1.50aa" },
            new object[] { 2.5e18, "2.50ab" }, new object[] { 1e33, "1.00ag" }, new object[] { 1e100, "10.0bc" },
            new object[] { -1234.0, "-1.23K" }, new object[] { -0.5, "-0.5" }, new object[] { 123456789.0, "123M" },
            new object[] { 1e9, "1.00B" }, new object[] { 500000.0, "500K" },
        };

        static void TestBigNum()
        {
            int bad = 0;
            foreach (object[] row in FmtCases)
            {
                double v = (double)row[0];
                string exp = (string)row[1];
                string got = BigNum.Fmt(v);
                bool ok = got == exp;
                if (!ok) bad++;
                Check("BigNum.Fmt(" + v.ToString("R", CultureInfo.InvariantCulture) + ") = " + exp, ok, "obtuvo " + got);
            }
            Check("BigNum.Fmt: " + FmtCases.Length + " valores (>= 20)", bad == 0 && FmtCases.Length >= 20);
            Check("BigNum.Fmt: inf/nan no revientan", BigNum.Fmt(double.PositiveInfinity) == "inf" && BigNum.Fmt(double.NaN) == "nan");
            Check("BigNum.TimeHms 0", BigNum.TimeHms(0) == "0s");
            Check("BigNum.TimeHms 59", BigNum.TimeHms(59) == "59s");
            Check("BigNum.TimeHms 60", BigNum.TimeHms(60) == "1m 00s");
            Check("BigNum.TimeHms 3599", BigNum.TimeHms(3599) == "59m 59s");
            Check("BigNum.TimeHms 3600", BigNum.TimeHms(3600) == "1h 00m");
            Check("BigNum.TimeHms 7384", BigNum.TimeHms(7384) == "2h 03m");
            Check("BigNum.TimeHms negativo", BigNum.TimeHms(-5) == "0s");
        }

        // ---------------------------------------------------------------- JSON
        static void TestJson()
        {
            object o;
            Check("json: parsea objeto anidado", Json.TryParse(" {\"a\": [1, 2.5, -3e2, true, false, null, \"x\"], \"b\": {}} ", out o));
            Dictionary<string, object> d = o as Dictionary<string, object>;
            List<object> a = d != null ? d["a"] as List<object> : null;
            Check("json: enteros -> long, decimales -> double", a != null && a[0] is long && (long)a[0] == 1 && a[1] is double && (double)a[1] == 2.5 && (double)a[2] == -300.0);
            Check("json: bool/null/string", a != null && (bool)a[3] && !(bool)a[4] && a[5] == null && (string)a[6] == "x");
            Check("json: escapes y unicode", Json.TryParse("\"a\\n\\\"b\\\\ \\u00e1\"", out o) && (string)o == "a\n\"b\\ \u00e1");
            Check("json: texto invalido -> false", !Json.TryParse("esto no es json", out o) && o == null);
            Check("json: vacio -> false", !Json.TryParse("", out o));
            Check("json: basura al final -> false", !Json.TryParse("{} x", out o));
            Check("json: objeto sin cerrar -> false", !Json.TryParse("{\"a\":1", out o));
            Check("json: numero invalido -> false", !Json.TryParse("[1.]", out o) && !Json.TryParse("[-]", out o));
            Check("json: long enorme cae a double", Json.TryParse("[12345678901234567890]", out o) && ((List<object>)o)[0] is double);
            Dictionary<string, object> w = new Dictionary<string, object>();
            w["s"] = "he\"llo\n";
            w["d"] = 12345.0;
            w["i"] = 7;
            w["l"] = 9000000000L;
            w["n"] = null;
            w["list"] = new List<object> { 1, 2.5, true, null };
            w["f"] = 1e30;
            string txt = Json.Stringify(w);
            Check("json: doubles enteros llevan .0", txt.Contains("\"d\":12345.0") && txt.Contains("\"i\":7") && txt.Contains("\"l\":9000000000"), txt);
            Check("json: ida y vuelta", Json.TryParse(txt, out o) && Json.Stringify(o) == txt.Replace("\"i\":7", "\"i\":7"), txt);
            Check("json: cultura invariante", WithCulture("es-AR", () => Json.Stringify(1234.5)) == "1234.5");
            Check("json: Fmt no depende de la cultura", WithCulture("de-DE", () => BigNum.Fmt(1234.0)) == "1.23K");
        }

        static string WithCulture(string name, Func<string> f)
        {
            CultureInfo old = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo(name);
                return f();
            }
            catch (CultureNotFoundException)
            {
                return f();
            }
            finally { CultureInfo.CurrentCulture = old; }
        }

        // ---------------------------------------------------------------- guardado completo
        static void Populate(Ctx c)
        {
            GameState g = c.G;
            c.Fresh();
            c.BuyTo("power", 63);
            c.BuyTo("speed", 41);
            c.BuyTo("money", 30);
            g.Gold = 123456.789;
            g.Gems = 987;
            g.World = 4;
            g.Sub = 3;
            g.Progress = 0.375;
            g.MaxStage = 17;
            g.RunMax = 17;
            g.Inv[3] = new Tool(1, 4);
            g.Inv[15] = new Tool(0, 2);
            g.Equip[2] = new Tool(1, 1);
            g.Collection.Add("1_4");
            g.Daily["rocks"] = 55;
            g.DailyDate = c.Clock.Date;
            g.Claimed.Add("rocks100");
            g.LoginDay = 3;
            g.LoginDate = c.Clock.Date;
            g.PassXp = 250;
            g.SkinsOwned.Add(2);
            g.Skin = 2;
            g.Rebirths = 2;
            g.Essence = 31;
            g.EssenceLv["power"] = 4;
            g.EssenceLv["luck"] = 2;
            g.Stats["crits"] = 12345;
            g.TotalGold = 7.5e15;
            g.AutoUp = true;
            g.Eco = true;
            g.Sound = false;
            g.Piggy = 17;
            g.GoalIdx = 12;
            g.GoalBase = 5.0;
            g.AchClaimed.Add("rocks_100");
            g.CheckUnlocks();
        }

        static void TestFullRoundtrip()
        {
            Ctx a = new Ctx();
            Populate(a);
            string json1 = a.G.ToJson();
            Ctx b = new Ctx();
            b.Clock.Time = a.Clock.Time;
            b.Store.Text = json1;
            b.G.Start();
            string json2 = b.G.ToJson();
            // apply_dict siempre fija stats["rocks"] (migracion de total_rocks), asi que el JSON converge tras una vuelta
            Ctx b2 = new Ctx();
            b2.Store.Text = json2;
            b2.G.Start();
            Check("guardar->cargar: el JSON converge (ida y vuelta estable)", b2.G.ToJson() == json2 && json2 == json1.Replace("\"crits\":12345}", "\"crits\":12345,\"rocks\":0}"), json2);
            Check("guardar->cargar: valores clave", b.G.Gold == 123456.789 && b.G.Lv["power"] == 63 && b.G.Inv[3].Equals(new Tool(1, 4)) && b.G.Equip[2].Equals(new Tool(1, 1)));
            Check("guardar->cargar: oro total grande exacto", b.G.TotalGold == 7.5e15 && b.G.EssenceLevel("luck") == 2 && b.G.Skin == 2 && !b.G.Sound && b.G.Eco);
            Check("guardar->cargar: diario y pase", b.G.Daily["rocks"] == 55 && b.G.Claimed.Contains("rocks100") && b.G.PassXp == 250 && b.G.LoginDay == 3);
            Check("guardar->cargar: sin ganancias offline si no paso tiempo", b.G.PendingOffline == 0.0);
            // el autoguardado del Tick usa el store
            int before = a.Store.SaveCount;
            a.G.Tick(6.0);
            Check("tick: autoguardado cada 5 s", a.Store.SaveCount == before + 1);
            a.G.OnAppPausedOrClosing();
            Check("OnAppPausedOrClosing guarda", a.Store.SaveCount == before + 2);
            // claves de guardado iguales a las de Godot
            object o;
            Json.TryParse(json1, out o);
            Dictionary<string, object> d = (Dictionary<string, object>)o;
            string[] keys = { "gold", "gems", "lv", "world", "sub", "progress", "need_boss", "max_stage", "inv", "equip", "daily", "daily_date", "claimed", "login_day",
                "login_date", "pass_xp", "boost_until", "boost_cd_until", "auto_up", "eco", "sound", "music", "skin", "skins_owned", "rebirths", "turbo", "free_chest_at",
                "piggy", "total_rocks", "total_gold", "last_seen", "collection", "stats", "features_unlocked", "features_seen", "essence", "essence_lv", "ach_claimed",
                "run_max", "lv_best", "goal_idx", "goal_base", "goal_gen_target", "goal_gen_kind" };
            bool all = d.Count == keys.Length;
            foreach (string k in keys) if (!d.ContainsKey(k)) all = false;
            Check("guardado: las 44 claves de to_dict de Godot", all, d.Count.ToString());
            Check("guardado: inv tiene 16 huecos y equip 3", ((List<object>)d["inv"]).Count == 16 && ((List<object>)d["equip"]).Count == 3);
        }

        // Partida tal como la escribe Godot: JSON.stringify guarda los enteros como "5.0"? (en Godot 4 todo numero se lee como float)
        static void TestGodotFloatSave()
        {
            string json = "{\"gold\": 1500.5, \"gems\": 40.0, \"lv\": {\"power\": 12.0, \"speed\": 3.0, \"money\": 8.0}, \"world\": 2.0, \"sub\": 4.0, \"progress\": 0.5,"
                + " \"need_boss\": false, \"max_stage\": 8.0, \"inv\": [null, {\"k\": 1.0, \"r\": 2.0}], \"equip\": [{\"k\": 0.0, \"r\": 0.0}, null, null],"
                + " \"stats\": {\"rocks\": 321.0, \"crits\": 9.0}, \"essence\": 5.0, \"essence_lv\": {\"gold\": 2.0}, \"features_unlocked\": [\"speed\", \"money\"],"
                + " \"features_seen\": [\"speed\"], \"goal_idx\": 4.0, \"goal_base\": 0.0, \"run_max\": 8.0, \"last_seen\": 1799999990.5, \"total_gold\": 1.0e6}";
            Ctx c = new Ctx();
            c.Store.Text = json;
            c.G.Start();
            GameState g = c.G;
            Check("partida Godot (numeros float): niveles y etapa", g.Lv["power"] == 12 && g.World == 2 && g.Sub == 4 && g.StageIdx() == 8);
            Check("partida Godot: inv/equip/stats/esencia", g.Inv[1].Equals(new Tool(1, 2)) && g.Inv[0] == null && g.EquippedCount() == 1 && g.Stat("rocks") == 321 && g.EssenceLevel("gold") == 2 && g.Essence == 5);
            Check("partida Godot: desbloqueos y metas leidos tal cual", g.FeaturesUnlocked[0] == "speed" && g.FeaturesUnlocked[1] == "money" && g.FeaturesSeen.Count == 1 && g.GoalIdx == 4 && g.SkinsOwned.Count == 1 && g.SkinsOwned[0] == 0);
            Check("partida Godot: lv_best deriva de lv", g.LvBest["power"] == 12 && g.LvBest["speed"] == 3);
            Check("partida Godot: tiempo ausente < 120 s => sin offline", g.PendingOffline == 0.0);
        }

        // ---------------------------------------------------------------- determinismo
        static string Simulate(int seed)
        {
            Ctx c = new Ctx(seed);
            GameState g = c.G;
            List<string> log = new List<string>();
            g.EventStarted += (k, d) => log.Add("S:" + k);
            g.EventEnded += k => log.Add("E:" + k);
            g.EvNext = 5.0;
            g.Gems = 5000;
            for (int i = 0; i < 20; i++)
            {
                Tool t = g.Gacha();
                log.Add("G:" + (t == null ? "-" : t.Key));
            }
            for (int i = 0; i < 4000; i++) g.Tick(0.5);
            return string.Join(",", log) + "|" + g.Stat("events") + "|" + g.EvNext.ToString("R", CultureInfo.InvariantCulture);
        }

        static void TestDeterminism()
        {
            string a = Simulate(777), b = Simulate(777), x = Simulate(778);
            Check("determinismo: misma semilla => misma secuencia de eventos y gachas", a == b && a.Length > 50, a.Substring(0, Math.Min(60, a.Length)));
            Check("determinismo: otra semilla => otra secuencia", a != x);
        }

        // ---------------------------------------------------------------- offline
        static void TestOffline()
        {
            Ctx c = new Ctx();
            Populate(c);
            c.G.Essence = 0;
            string json = c.G.ToJson();   // last_seen = Time
            double expectBase;
            {
                Ctx r = new Ctx();
                r.Clock.Time = c.Clock.Time + 3 * 3600;
                r.Store.Text = json;
                r.G.Start();
                // inc se calcula despues de cargar: mismo estado => misma formula
                expectBase = r.G.IncomePerSec() * (3 * 3600) * 0.5;
                Check("offline: 3 h al 50%", T.Approx(r.G.PendingOffline, expectBase, expectBase * 1e-12) && r.G.PendingOfflineTime == 3 * 3600);
                Check("offline: alcancia suma 1/min con tope 60", r.G.Piggy == 60);
                double gold0 = r.G.Gold;
                r.G.CollectOffline(2.0);
                Check("offline: collect_offline con multiplicador", T.Approx(r.G.Gold - gold0, expectBase * 2.0, expectBase * 1e-12) && r.G.PendingOffline == 0.0);
            }
            {
                Ctx r = new Ctx();
                r.Clock.Time = c.Clock.Time + 20 * 3600;
                r.Store.Text = json;
                r.G.Start();
                double exp = r.G.IncomePerSec() * (8 * 3600) * 0.5;
                Check("offline: tope de 8 h", T.Approx(r.G.PendingOffline, exp, exp * 1e-12) && r.G.PendingOfflineTime == 20 * 3600);
            }
            {
                Ctx r = new Ctx();
                r.Clock.Time = c.Clock.Time + 20 * 3600;
                r.Store.Text = json.Replace("\"essence_lv\":{\"power\":4,\"luck\":2}", "\"essence_lv\":{\"power\":4,\"night\":2}");
                r.G.Start();
                double exp = r.G.IncomePerSec() * (10 * 3600) * 0.7;
                Check("offline: Minero nocturno 2 => tope 10 h y 70%", T.Approx(r.G.PendingOffline, exp, exp * 1e-12), r.G.PendingOffline + " vs " + exp);
            }
            {
                Ctx r = new Ctx();
                r.Clock.Time = c.Clock.Time + 100;
                r.Store.Text = json;
                r.G.Start();
                Check("offline: menos de 120 s no cuenta", r.G.PendingOffline == 0.0);
            }
        }

        // ---------------------------------------------------------------- diario / pase / misiones
        static void TestDaily()
        {
            Ctx c = new Ctx();
            GameState g = c.G;
            c.Fresh();
            c.Clock.Date = "2026-03-01";
            g.AddDaily("rocks", 100);
            Mission m100 = Content.Missions[0];
            Check("diario: mision lista con 100 rocas", g.MissionReady(m100) && g.MissionsReadyCount() == 1 && g.MissionProgress(m100) == 100);
            int gems0 = g.Gems;
            g.ClaimMission(m100);
            Check("diario: claim da gemas, pase y estadistica", g.Gems == gems0 + 20 && g.PassXp == 25 && g.Stat("missions") == 1 && !g.MissionReady(m100));
            g.ClaimMission(m100);
            Check("diario: no se reclama dos veces", g.Gems == gems0 + 20);
            List<string> toasts = new List<string>();
            g.Toast += t => toasts.Add(t);
            g.AddPassXp(80);
            Check("pase: nivel 2 da 40+2*20 gemas y avisa", g.PassLevel() == 2 && g.Gems == gems0 + 20 + 80 && toasts.Count == 1 && toasts[0] == "Pase nivel 2: +80 gemas", T.Str(toasts));
            g.AddPassXp(300);
            Check("pase: varios niveles de una vez", g.PassLevel() == 5 && g.Gems == gems0 + 20 + 80 + 100 + 120 + 140);
            c.Clock.Date = "2026-03-02";
            g.AddDaily("rocks", 1);
            Check("diario: cambia el dia => se reinician contadores y reclamadas", g.Daily["rocks"] == 1 && g.Claimed.Count == 0 && g.DailyDate == "2026-03-02");
            // premio diario
            Check("login: disponible", g.LoginAvailable());
            int[] expect = { 50, 100, 200, 300, 500, 800, 1200, 50 };
            bool ok = true;
            for (int i = 0; i < expect.Length; i++)
            {
                c.Clock.Date = "2026-04-" + (10 + i);
                int got = g.ClaimLogin();
                if (got != expect[i]) ok = false;
                if (g.ClaimLogin() != 0) ok = false;   // una vez por dia
            }
            Check("login: ciclo de 7 dias y una vez por dia", ok && g.LoginDay == 8);
        }

        // ---------------------------------------------------------------- herramientas
        static void TestTools()
        {
            Ctx c = new Ctx();
            GameState g = c.G;
            c.Fresh();
            Check("tools: equipo inicial Pico+Mazo", g.Equip[0].Equals(new Tool(0, 0)) && g.Equip[1].Equals(new Tool(1, 0)) && g.Equip[2] == null && g.EquippedCount() == 2);
            Check("tools: coleccion inicial", T.Eq(g.Collection, "0_0", "1_0"));
            Check("tools: nombres", g.ToolName(g.Equip[0]) == "Pico" && g.ToolName(g.Equip[1]) == "Mazo" && g.ToolName(null) == "");
            Check("tools: poder y intervalo base", g.ToolPower(g.Equip[0]) == 100.0 && g.ToolPower(g.Equip[1]) == 80.0 && T.Approx(g.ToolPower(new Tool(0, 2)), 100.0 * 2.2 * 2.2, 1e-9) && g.ToolBaseInterval(g.Equip[1]) == 1.0);
            // intercambio
            g.Inv[0] = new Tool(0, 3);
            MoveResult r = g.MoveTool(new SlotRef(SlotKind.Inv, 0), new SlotRef(SlotKind.Equip, 2));
            Check("tools: mover a hueco vacio de equipo", r == MoveResult.Swap && g.Equip[2].Equals(new Tool(0, 3)) && g.Inv[0] == null && g.EquippedCount() == 3);
            Check("tools: mismo hueco no hace nada", g.MoveTool(new SlotRef(SlotKind.Inv, 4), new SlotRef(SlotKind.Inv, 4)) == MoveResult.None);
            Check("tools: origen vacio no hace nada", g.MoveTool(new SlotRef(SlotKind.Inv, 9), new SlotRef(SlotKind.Inv, 4)) == MoveResult.None);
            Check("tools: indice fuera de rango no revienta", g.MoveTool(new SlotRef(SlotKind.Inv, 99), new SlotRef(SlotKind.Inv, 4)) == MoveResult.None);
            // fusion en equipo
            g.Inv[1] = new Tool(1, 0);
            r = g.MoveTool(new SlotRef(SlotKind.Inv, 1), new SlotRef(SlotKind.Equip, 1));
            Check("tools: fusion Mazo+Mazo => rango 1 en el destino", r == MoveResult.Merge && g.Equip[1].Equals(new Tool(1, 1)) && g.Inv[1] == null && g.Collection.Contains("1_1"));
            // no fusionar rango maximo
            g.Inv[2] = new Tool(0, 5);
            g.Inv[3] = new Tool(0, 5);
            r = g.MoveTool(new SlotRef(SlotKind.Inv, 2), new SlotRef(SlotKind.Inv, 3));
            Check("tools: rango SS no fusiona (intercambia)", r == MoveResult.Swap && g.Inv[2].R == 5 && g.Inv[3].R == 5);
            // proteccion de ultimo minero
            c.Fresh();
            g.Equip[1] = null;
            List<string> toasts = new List<string>();
            g.Toast += t => toasts.Add(t);
            r = g.MoveTool(new SlotRef(SlotKind.Equip, 0), new SlotRef(SlotKind.Inv, 0));
            Check("tools: no se deja el equipo vacio (mover)", r == MoveResult.None && g.Equip[0] != null && toasts.Count == 1 && toasts[0] == "Necesitas al menos un minero");
            g.Unequip(0);
            Check("tools: no se deja el equipo vacio (unequip)", g.Equip[0] != null && toasts.Count == 2);
            g.Equip[1] = new Tool(1, 0);
            g.Unequip(1);
            Check("tools: unequip manda al inventario", g.Equip[1] == null && g.Inv[0] != null && g.Inv[0].Equals(new Tool(1, 0)));
            for (int i = 0; i < 16; i++) g.Inv[i] = new Tool(0, 0);
            g.Equip[1] = new Tool(1, 0);
            g.Unequip(1);
            Check("tools: unequip con inventario lleno avisa", g.Equip[1] != null && toasts[toasts.Count - 1] == "Inventario lleno");
            // gacha
            c.Fresh();
            g.Gems = 99;
            Check("gacha: faltan gemas", g.Gacha() == null && toasts[toasts.Count - 1] == "Faltan gemas" && g.Gems == 99);
            for (int i = 0; i < 16; i++) g.Inv[i] = new Tool(0, 0);
            g.Gems = 500;
            Check("gacha: inventario lleno", g.Gacha() == null && toasts[toasts.Count - 1] == "Inventario lleno" && g.Gems == 500);
            c.Fresh();
            g.Gems = 100 * 3000;
            int[] byRank = new int[6];
            int[] byKind = new int[2];
            for (int i = 0; i < 3000; i++)
            {
                for (int s = 0; s < 16; s++) g.Inv[s] = null;
                Tool t = g.Gacha();
                byRank[t.R]++;
                byKind[t.K]++;
            }
            Check("gacha: cuesta 100 gemas", g.Gems == 0);
            Check("gacha: probabilidades de rango 70/25/5", T.Approx(byRank[0] / 3000.0, 0.70, 0.03) && T.Approx(byRank[1] / 3000.0, 0.25, 0.03) && T.Approx(byRank[2] / 3000.0, 0.05, 0.02) && byRank[3] + byRank[4] + byRank[5] == 0,
                byRank[0] + "/" + byRank[1] + "/" + byRank[2]);
            Check("gacha: Pico y Mazo 50/50", T.Approx(byKind[0] / 3000.0, 0.5, 0.04));
        }

        // ---------------------------------------------------------------- tienda / tick
        static void TestShopAndTick()
        {
            Ctx c = new Ctx();
            GameState g = c.G;
            c.Fresh();
            Check("boost: se activa", g.ActivateBoost() && g.BoostActive() && g.GoldMultNow() == 3.0);
            Check("boost: enfriamiento 240 s", !g.ActivateBoost());
            c.Clock.Time += 61;
            Check("boost: expira a los 60 s", !g.BoostActive());
            c.Clock.Time += 200;
            Check("boost: reutilizable tras el enfriamiento", g.ActivateBoost());
            Check("cofre gratis: disponible, +30 gemas y espera 4 h", g.FreeChestReady() && Claim(g) == 30 && !g.FreeChestReady());
            c.Clock.Time += 4 * 3600;
            Check("cofre gratis: vuelve a las 4 h", g.FreeChestReady());
            g.Piggy = 12;
            int gm = g.Gems;
            Check("alcancia: recoger", g.CollectPiggy() == 12 && g.Gems == gm + 12 && g.Piggy == 0);
            g.Tick(60.0);
            Check("alcancia: +1 por minuto de Tick", g.Piggy == 1);
            g.Piggy = 60;
            g.Tick(60.0);
            Check("alcancia: tope 60", g.Piggy == 60);
            // cascos
            g.Gems = 99;
            Check("skin: no alcanzan gemas", !g.BuySkin(1) && g.Skin == 0 && g.Gems == 99);
            g.Gems = 150;
            Check("skin: compra Rubi (100)", g.BuySkin(1) && g.Skin == 1 && g.Gems == 50 && g.SkinsOwned.Contains(1));
            Check("skin: volver a uno ya comprado es gratis", g.BuySkin(0) && g.Skin == 0 && g.Gems == 50);
            Check("skin: indice invalido", !g.BuySkin(99));
            g.Gems = 2000;
            g.BuySkin(5);
            c.Fresh();
            g.Skin = 5;
            double m5 = g.MoneyMult();
            g.Skin = 0;
            Check("skin: Oro puro da +10% de oro", T.Approx(m5 / g.MoneyMult(), 1.1, 1e-12));
            g.Gems = 10;
            Check("spend_gems", !g.SpendGems(11) && g.SpendGems(4) && g.Gems == 6);
            // auto-compra desde Tick
            c.Fresh();
            g.AutoUp = true;
            g.Gold = 1000.0;
            double c0 = g.Cost("power");
            g.Tick(0.1);
            Check("auto: compra la mas barata en Tick", g.Lv["power"] == 2 && g.Gold == 1000.0 - c0);
            // tope de velocidad
            c.Fresh();
            g.Gold = 1e300;
            g.Lv["speed"] = 200;
            Check("velocidad: tope 200 (no compra)", !g.CanBuy("speed") && !g.Buy("speed") && g.Lv["speed"] == 200);
            // senales de compra
            c.Fresh();
            List<string> ups = new List<string>();
            g.Upgraded += k => ups.Add(k);
            g.Gold = 100.0;
            g.Buy("power");
            Check("buy: senal upgraded y stat upgrades", T.Eq(ups, "power") && g.Stat("upgrades") == 1 && g.Daily["upgrades"] == 1);
            g.Gold = 1.0;
            Check("buy: sin oro no compra", !g.Buy("money") && g.Lv["money"] == 1 && g.Gold == 1.0);
            // eventos de etapa y jefe
            c.Fresh();
            List<string> st = new List<string>();
            g.StageCleared += (w, s) => st.Add(w + "-" + s);
            bool boss = false;
            g.BossNeeded += () => boss = true;
            int bossGems = 0;
            g.BossDefeated += n => bossGems = n;
            for (int i = 0; i < 4; i++) c.ClearStage();
            Check("etapas: 1-1..1-4 emiten stage_cleared", T.Eq(st, "1-1", "1-2", "1-3", "1-4"), T.Str(st));
            for (int i = 0; i < 40; i++) g.OnRockBroken(false);
            Check("etapas: 1-5 pide jefe", boss && g.NeedBoss && g.Progress >= 1.0);
            g.OnRockBroken(false);
            Check("etapas: con jefe pendiente las rocas no avanzan", g.StageIdx() == 4);
            g.OnRockBroken(true);
            Check("etapas: jefe vencido => mundo 2, 5 gemas", g.World == 2 && g.Sub == 1 && bossGems == 5 && T.Eq(st, "1-1", "1-2", "1-3", "1-4", "1-5"));
            Check("etapas: rocas por etapa 40 + 7 por mundo", g.RocksNeeded() == 47);
            Check("etapas: nombres y bioma", g.StageName() == "Etapa 2-1" && g.StageLabel(12) == "3-3" && g.Biome() == 1);
        }

        static int Claim(GameState g)
        {
            int before = g.Gems;
            g.ClaimFreeChest();
            return g.Gems - before;
        }

        // ---------------------------------------------------------------- valores de la economia (calculados a mano de las constantes)
        static void TestEconomyValues()
        {
            Ctx c = new Ctx();
            GameState g = c.G;
            c.Fresh();
            Check("economia: costos nivel 1", T.Approx(g.Cost("power"), 3.65, 1e-12) && T.Approx(g.Cost("speed"), 25.0, 1e-12) && T.Approx(g.Cost("money"), 37.33, 1e-12));
            g.Lv["power"] = 11;
            Check("economia: costo Fuerza nivel 11 = 3.65 * 1.282^10", T.Approx(g.Cost("power"), 3.65 * Math.Pow(1.2820, 10), 1e-9));
            c.Fresh();
            Check("economia: vida y oro de la etapa 0", g.RockHp(0) == 40.37 && g.RockBaseGold(0) == 3.76 && g.RockHp() == 40.37);
            Check("economia: vida etapa 1 = 40.37 * min(2.7153, 1.2643 * 1.2326^0)", T.Approx(g.RockHp(1), 40.37 * 1.2643, 1e-9));
            Check("economia: poder y velocidad nivel 1", g.PowerValue() == 10.0 && g.SpeedValue() == 60 && g.SpeedMultTotal() == 1.0 && g.MoneyMult() == 1.0);
            Check("economia: critico base", T.Approx(g.CritChance(), 0.10, 1e-12) && T.Approx(g.CritMult(), 2.5, 1e-12));
            g.RunMax = 10;
            Check("economia: Esencia 3-1 = floor(0.125 * 10^1.6) = 4", g.EssenceGainPreview() == 4);
            g.RunMax = 45;
            Check("economia: Esencia etapa 45", g.EssenceGainPreview() == (int)Math.Floor(0.125 * Math.Pow(45.0, 1.6) + 1e-6));
            Check("economia: costos de nodos", CostsOk());
            Check("economia: hitos 25..300 y cada 50", g.MilestoneLevel(1) == 25 && g.MilestoneLevel(4) == 100 && g.MilestoneLevel(5) == 150 && g.MilestoneLevel(8) == 300
                && g.MilestoneCount(24) == 0 && g.MilestoneCount(99) == 3 && g.MilestoneCount(149) == 4 && g.MilestoneCount(150) == 5 && g.IsMilestoneLevel(150) && !g.IsMilestoneLevel(151) && g.IsMilestoneLevel(25));
            Check("economia: ingreso por segundo del inicio es finito y positivo", g.IncomePerSec() > 0 && !double.IsInfinity(g.IncomePerSec()));
            Check("economia: tablas de contenido (6 cascos, 10 nodos, 29 logros, 40 metas, 11 misiones)",
                Content.Skins.Length == 6 && Content.EssenceNodes.Length == 10 && Content.Achievements.Length == 29 && Content.Goals.Length == 40 && Content.Missions.Length == 11);
        }

        static bool CostsOk()
        {
            // costo = ceil(base * mult^nivel - 1e-9)
            Ctx c = new Ctx();
            c.Fresh();
            c.G.EssenceLv["start"] = 1;
            c.G.EssenceLv["night"] = 2;
            return c.G.EssenceCost("start") == (int)Math.Ceiling(3 * 1.7 - 1e-9) && c.G.EssenceCost("night") == (int)Math.Ceiling(4 * 1.8 * 1.8 - 1e-9) && c.G.EssenceCost("boss") == 3;
        }

        // ---------------------------------------------------------------- metas generadas largas
        static void TestGeneratedGoals()
        {
            Ctx c = new Ctx();
            GameState g = c.G;
            c.Fresh();
            g.MaxStage = 20;
            g.LvBest = new Dictionary<string, int> { { "power", 100 }, { "speed", 198 }, { "money", 5 } };
            g.GoalIdx = 40 + 3;   // n = 3: impar, kinds[(3/2)%3] = speed => pasa a power porque speed >= 195
            g.ActivateGoal();
            Goal a = g.CurrentGoal();
            Check("metas: velocidad casi al tope se reemplaza por Fuerza", a.Text == "Mejora Fuerza a nivel 110" && a.Target == 110 && a.Icon == "power", a.Text);
            g.GoalIdx = 40 + 5;   // n = 5 => kinds[2] = money
            g.ActivateGoal();
            a = g.CurrentGoal();
            Check("metas: Oro generada = mejor nivel + 10", a.Text == "Mejora Oro a nivel 15" && a.Target == 15 && a.Icon == "money", a.Text);
            g.GoalIdx = 40 + 16;  // n = 16: par => etapa max + 2 + 16/8
            g.ActivateGoal();
            a = g.CurrentGoal();
            Check("metas: etapa generada crece con n", a.Target == 24 && a.Text == "Llega a la Etapa 5-5" && a.Gems == 72, a.Text + " " + a.Gems);
            g.GoalIdx = 40 + 100;
            g.ActivateGoal();
            Check("metas: gemas generadas topan en 100", g.CurrentGoal().Gems == 100);
            g.GoalIdx = 40;
            g.ActivateGoal();
            Check("metas: la meta generada se reclama y avanza", Advance(g));
        }

        static bool Advance(GameState g)
        {
            g.MaxStage = g.CurrentGoal().Target > 0 ? (int)g.CurrentGoal().Target : g.MaxStage;
            int gems = g.Gems;
            int got = g.ClaimGoal();
            return got == 40 && g.Gems == gems + 40 && g.GoalIdx == 41;
        }
    }
}
