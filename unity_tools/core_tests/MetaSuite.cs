using System;
using System.Collections.Generic;
using Mineros.Core;

namespace CoreTests
{
    /// <summary>Port 1:1 de miner_idle/tools/test_meta.gd (144 comprobaciones, mismos nombres).</summary>
    public static class MetaSuite
    {
        static readonly string[] Icons = { "coin", "gem", "power", "speed", "money", "pick", "hammer", "chest", "trophy", "star", "mission", "rebirth", "boss", "clock", "shop", "skin", "piggy" };

        static void Check(string n, bool c, string extra = "") { T.Check(n, c, extra); }

        public static void Run(Ctx c)
        {
            TestUnlocks(c);
            TestMilestones(c);
            TestGoals(c);
            TestAchievements(c);
            TestRebirth(c);
            TestEvents(c);
            TestStatsMisc(c);
            TestSaveRoundtrip(c);
            TestOldSave(c);
            c.G.ResetAll();
        }

        static Tool Tl(int k, int r) { return new Tool(k, r); }

        // ------------------------------------------------------------ desbloqueos
        static void TestUnlocks(Ctx c)
        {
            GameState g = c.G;
            c.Fresh();
            List<string> order = new List<string>();
            g.FeatureUnlocked += f => order.Add(f);
            Check("unlock: partida nueva sin funciones", g.FeaturesUnlocked.Count == 0 && g.NewlyUnlocked().Count == 0);
            Check("unlock: nucleo siempre activo", g.IsUnlocked("power") && !g.IsUnlocked("speed"));
            c.BuyTo("power", 2);
            Check("unlock: speed no antes de Fuerza 3", !g.IsUnlocked("speed"));
            c.BuyTo("power", 3);
            Check("unlock: speed con Fuerza 3", g.IsUnlocked("speed") && T.Eq(order, "speed"), T.Str(order));
            Check("unlock: newly_unlocked tiene speed", T.Eq(g.NewlyUnlocked(), "speed"));
            g.MarkFeatureSeen("speed");
            Check("unlock: mark_feature_seen", g.NewlyUnlocked().Count == 0);
            c.ClearStage();
            Check("unlock: money tras 1-1", g.IsUnlocked("money") && !g.IsUnlocked("boost"));
            c.ClearStage();
            Check("unlock: boost tras 1-2", g.IsUnlocked("boost") && !g.IsUnlocked("tools"));
            c.ClearStage();
            Check("unlock: tools tras 1-3", g.IsUnlocked("tools") && !g.IsUnlocked("missions"));
            c.ClearStage();
            Check("unlock: missions tras 1-4", g.IsUnlocked("missions") && !g.IsUnlocked("play"));
            c.ClearStage();   // 1-5 con jefe
            Check("unlock: play tras el jefe", g.IsUnlocked("play") && !g.IsUnlocked("shop"), "stage=" + g.StageIdx());
            c.ClearStage();
            Check("unlock: shop/skin/piggy en 2-2", g.IsUnlocked("shop") && g.IsUnlocked("skin") && g.IsUnlocked("piggy"));
            Check("unlock: auto aun no", !g.IsUnlocked("auto") && !g.IsUnlocked("rebirth"));
            c.BuyTo("power", 10);
            c.BuyTo("speed", 10);
            c.BuyTo("money", 10);
            Check("unlock: auto con 10/10/10", g.IsUnlocked("auto"));
            for (int i = 0; i < 4; i++) c.ClearStage();
            Check("unlock: rebirth en 3-1", g.IsUnlocked("rebirth"), "stage=" + g.StageIdx() + " max=" + g.MaxStage);
            Check("unlock: achievements con un logro listo", g.IsUnlocked("achievements"), g.AchievementsReadyCount().ToString());
            string[] expect = { "speed", "money", "boost", "tools", "missions", "play", "shop", "skin", "piggy", "auto", "rebirth", "achievements" };
            List<string> sortedOrder = new List<string>(order);
            sortedOrder.Sort(StringComparer.Ordinal);
            List<string> sortedExpect = new List<string>(expect);
            sortedExpect.Sort(StringComparer.Ordinal);
            Check("unlock: las 12 funciones, una sola vez cada una", sortedOrder.Count == sortedExpect.Count && T.Eq(sortedOrder, sortedExpect.ToArray()) && order.Count == 12, T.Str(order));
            Check("unlock: orden de la tabla (logros aparece al primer logro)",
                order.IndexOf("speed") < order.IndexOf("money") && order.IndexOf("money") < order.IndexOf("boost")
                && order.IndexOf("boost") < order.IndexOf("tools") && order.IndexOf("tools") < order.IndexOf("missions")
                && order.IndexOf("play") < order.IndexOf("shop") && order.IndexOf("shop") < order.IndexOf("auto")
                && order.IndexOf("auto") < order.IndexOf("rebirth"), T.Str(order));
        }

        // ------------------------------------------------------------ hitos
        static void TestMilestones(Ctx c)
        {
            GameState g = c.G;
            c.Fresh();
            List<string> got = new List<string>();
            Action<string, int> cb = (kind, level) => got.Add(kind + ":" + level);
            g.MilestoneReached += cb;
            c.BuyTo("power", 24);
            double p24 = g.PowerValue();
            Check("hito: sin hito antes de 25", got.Count == 0 && g.MilestoneMult("power") == 1.0);
            Check("hito: next_milestone = 25", g.NextMilestone("power") == 25);
            c.BuyTo("power", 25);
            double p25 = g.PowerValue();
            Check("hito: senal en nivel 25", T.Eq(got, "power:25"), T.Str(got));
            Check("hito: power x2 al comprar hasta 25", T.Approx(p25 / p24, 2.0 * Balance.PowerGrow, 1e-6), (p25 / p24).ToString());
            Check("hito: next_milestone = 50", g.NextMilestone("power") == 50);
            c.BuyTo("money", 25);
            Check("hito: money x2", g.MilestoneMult("money") == 2.0);
            double iv0 = g.ToolInterval(Tl(0, 0));
            c.BuyTo("speed", 24);
            double iv24 = g.ToolInterval(Tl(0, 0));
            c.BuyTo("speed", 25);
            double iv25 = g.ToolInterval(Tl(0, 0));
            Check("hito: speed x1.25 en el intervalo", T.Approx(iv24 / iv25, 1.25 * (60 + 3 * 24) / (60.0 + 3 * 23), 1e-6), (iv24 / iv25).ToString());
            Check("hito: speed acelera", iv25 < iv0);
            c.BuyTo("power", 100);
            Check("hito: x16 en nivel 100 (4 hitos)", g.MilestoneMult("power") == 16.0 && g.NextMilestone("power") == 150);
            g.Lv["power"] = 300;
            Check("hito: 300 = 8 hitos y sigue cada 50", g.MilestoneCount(300) == 8 && g.NextMilestone("power") == 350);
            g.Lv["speed"] = 200;
            Check("hito: speed tope 200", g.NextMilestone("speed") == 0);
            bool multi = got.Contains("power:25") && got.Contains("power:50") && got.Contains("power:75") && got.Contains("power:100");
            Check("hito: una senal por hito", multi, T.Str(got));
            g.MilestoneReached -= cb;
        }

        // ------------------------------------------------------------ metas
        static void TestGoals(Ctx c)
        {
            GameState g = c.G;
            c.Fresh();
            HashSet<string> ids = new HashSet<string>();
            bool okLen = true, okIcon = true, okGems = true;
            foreach (GoalDef gd in Content.Goals)
            {
                ids.Add(gd.Id);
                if (gd.Text.Length > 32)
                {
                    okLen = false;
                    Console.WriteLine("   texto largo: " + gd.Text);
                }
                if (Array.IndexOf(Icons, gd.Icon) < 0) okIcon = false;
                if (gd.Gems < 5 || gd.Gems > 40) okGems = false;
            }
            Check("metas: 40 curadas con ids unicos", Content.Goals.Length == 40 && ids.Count == 40);
            Check("metas: texto <= 32 caracteres", okLen);
            Check("metas: iconos validos", okIcon);
            Check("metas: gemas entre 5 y 40", okGems);
            List<string> doneSignals = new List<string>();
            Action<Goal> cb = goal => doneSignals.Add(goal.Id);
            g.GoalCompleted += cb;
            Goal cg = g.CurrentGoal();
            Check("metas: primera meta", cg.Id == "g01" && cg.Progress == 1 && cg.Target == 3 && !cg.Done, cg.Id);
            g.PollGoal();
            Check("metas: sin senal si no esta lista", doneSignals.Count == 0);
            c.BuyTo("power", 3);
            g.PollGoal();
            g.PollGoal();
            Check("metas: senal una sola vez", T.Eq(doneSignals, "g01"), T.Str(doneSignals));
            Check("metas: done y progress", g.CurrentGoal().Done && g.CurrentGoal().Progress == 3);
            int gems0 = g.Gems;
            int r1 = g.ClaimGoal();
            Check("metas: claim 1 da gemas", r1 == 5 && g.Gems == gems0 + 5, "r=" + r1);
            Check("metas: avanza a la 2", g.CurrentGoal().Id == "g02");
            Check("metas: no reclamable si no esta lista", g.ClaimGoal() == 0);
            // meta 2: romper 12 rocas contadas desde que empieza
            g.Stats["rocks"] = 1000;
            g.GoalBase = 1000.0;
            g.Stats["rocks"] = 1000;
            for (int i = 0; i < 11; i++) g.OnRockBroken(false);
            Check("metas: contador relativo 11/12", g.CurrentGoal().Progress == 11 && !g.CurrentGoal().Done, g.CurrentGoal().Progress.ToString());
            g.OnRockBroken(false);
            g.PollGoal();
            Check("metas: rocas 12/12 y senal", g.CurrentGoal().Done && doneSignals.Contains("g02"));
            Check("metas: claim 2", g.ClaimGoal() == 5);
            // meta 3: velocidad 3
            c.BuyTo("speed", 3);
            g.PollGoal();
            Check("metas: velocidad 3 hecha", g.CurrentGoal().Id == "g03" && g.CurrentGoal().Done);
            Check("metas: claim 3", g.ClaimGoal() == 6 && g.CurrentGoal().Id == "g04");
            Check("metas: stat goals = 3", g.Stat("goals") == 3);
            // generadas
            g.GoalIdx = 40;
            g.MaxStage = 12;
            g.LvBest = new Dictionary<string, int> { { "power", 40 }, { "speed", 30 }, { "money", 35 } };
            g.ActivateGoal();
            Goal a = g.CurrentGoal();
            Check("metas: generada de etapa", a.Text.StartsWith("Llega a la Etapa ", StringComparison.Ordinal) && a.Target == 14, a.Text + " " + a.Target);
            g.GoalIdx = 41;
            g.ActivateGoal();
            Goal b = g.CurrentGoal();
            Check("metas: generada de nivel", b.Text.StartsWith("Mejora ", StringComparison.Ordinal) && b.Target == 50, b.Text + " " + b.Target);
            g.GoalIdx = 42;
            g.ActivateGoal();
            Check("metas: alterna etapa", g.CurrentGoal().Text.StartsWith("Llega a la Etapa ", StringComparison.Ordinal));
            g.GoalCompleted -= cb;
        }

        // ------------------------------------------------------------ logros
        static void TestAchievements(Ctx c)
        {
            GameState g = c.G;
            c.Fresh();
            Check("logros: >= 24 definidos", Content.Achievements.Length >= 24, Content.Achievements.Length.ToString());
            Check("logros: ninguno listo al inicio", g.AchievementsReadyCount() == 0);
            g.Stats["rocks"] = 150;
            Achievement a100 = null;
            foreach (Achievement a in Content.Achievements) if (a.Id == "rocks_100") a100 = a;
            Check("logros: progreso topado", g.AchievementProgress(a100) == 100 && g.AchievementReady(a100));
            Check("logros: contador", g.AchievementsReadyCount() == 1);
            int gm = g.Gems;
            Check("logros: claim da gemas", g.ClaimAchievement("rocks_100") == 10 && g.Gems == gm + 10);
            Check("logros: no se reclama dos veces", g.ClaimAchievement("rocks_100") == 0 && g.AchievementsReadyCount() == 0);
            g.TotalGold = 2.0e9;
            Check("logros: oro total (1K, 1M, 1B)", g.AchievementsReadyCount() == 3, g.AchievementsReadyCount().ToString());
            Check("logros: id inexistente", g.ClaimAchievement("nada") == 0);
            bool gemsOk = true;
            foreach (Achievement a in Content.Achievements) if (a.Gems < 10 || a.Gems > 300) gemsOk = false;
            Check("logros: gemas 10-300", gemsOk);
        }

        // ------------------------------------------------------------ renacer
        static void TestRebirth(Ctx c)
        {
            GameState g = c.G;
            c.Fresh();
            Check("renacer: no disponible al inicio", !g.CanRebirth() && !g.IsUnlocked("rebirth"));
            g.DoRebirth();
            Check("renacer: do_rebirth sin condicion no hace nada", g.Rebirths == 0 && g.Essence == 0);
            g.RunMax = 9;
            Check("renacer: 2-5 aun no", !g.CanRebirth());
            g.RunMax = 10;
            g.MaxStage = 10;
            g.World = 3;
            g.Sub = 1;
            Check("renacer: disponible en 3-1", g.CanRebirth());
            int prev = g.EssenceGainPreview();
            Check("renacer: esencia en 3-1 entre 3 y 5", prev >= 3 && prev <= 5, prev.ToString());
            int pOld = 0;
            bool mono = true;
            for (int s = 10; s < 60; s++)
            {
                g.RunMax = s;
                int v = g.EssenceGainPreview();
                if (v < pOld) mono = false;
                pOld = v;
            }
            Check("renacer: esencia creciente con la etapa", mono && pOld > 40, pOld.ToString());
            g.RunMax = 10;
            g.Gold = 5000.0;
            g.Gems = 77;
            g.Lv = new Dictionary<string, int> { { "power", 30 }, { "speed", 20 }, { "money", 25 } };
            g.Inv[0] = Tl(1, 2);
            g.Equip[2] = Tl(0, 1);
            g.Collection.Add("1_2");
            g.AchClaimed.Add("rocks_100");
            List<string> uf = new List<string>(g.FeaturesUnlocked);
            List<int> rbSig = new List<int>();
            g.Rebirthed += x => rbSig.Add(x);
            g.DoRebirth();
            Check("renacer: suma esencia", g.Essence == prev && rbSig.Count == 1 && rbSig[0] == prev, "e=" + g.Essence);
            Check("renacer: resetea oro/niveles/etapa", g.Gold == 0.0 && g.Lv["power"] == 1 && g.Lv["speed"] == 1 && g.Lv["money"] == 1 && g.World == 1 && g.Sub == 1 && !g.NeedBoss);
            Check("renacer: conserva gemas/herramientas/logros", g.Gems == 77 && g.Inv[0] != null && g.Equip[2] != null && g.AchClaimed.Contains("rocks_100") && g.Collection.Contains("1_2"));
            Check("renacer: conserva desbloqueos y etapa max", g.FeaturesUnlocked.Count >= uf.Count && g.MaxStage == 10);
            Check("renacer: contadores", g.Rebirths == 1 && g.Stat("rebirths") == 1);
            Check("renacer: no se puede de inmediato", !g.CanRebirth() && g.EssenceGainPreview() == 0);
            Check("renacer: compat rebirth_mult/rebirth_gain", g.RebirthMult() >= 1.0 && g.RebirthGain() == 0.0);
            // nodos
            Check("esencia: nodos 8-10", Content.EssenceNodes.Length >= 8 && Content.EssenceNodes.Length <= 10);
            double basePw = g.PowerValue();
            g.Essence = 1000;
            int c1 = g.EssenceCost("power");
            Check("esencia: costo base 1", c1 == 1, c1.ToString());
            Check("esencia: buy_essence ok", g.BuyEssence("power") && g.EssenceLevel("power") == 1 && g.Essence == 1000 - c1);
            Check("esencia: fuerza ancestral +25%", T.Approx(g.PowerValue() / basePw, 1.25, 1e-9));
            Check("esencia: costo sube", g.EssenceCost("power") >= c1);
            double baseGold = g.MoneyMult();
            g.BuyEssence("gold");
            Check("esencia: oro ancestral +25%", T.Approx(g.MoneyMult() / baseGold, 1.25, 1e-9));
            double iv = g.ToolInterval(Tl(0, 0));
            g.BuyEssence("speed");
            Check("esencia: manos rapidas +5%", T.Approx(iv / g.ToolInterval(Tl(0, 0)), 1.05, 1e-9));
            g.BuyEssence("crit");
            Check("esencia: ojo critico +1.5%", T.Approx(g.CritChance(), 0.115, 1e-9));
            g.BuyEssence("brutal");
            Check("esencia: golpe brutal +25% critico", T.Approx(g.CritMult(), 2.5 * 1.25, 1e-9));
            g.EssenceLv["brutal"] = 0;
            Check("esencia: crit_mult base 2.5", g.CritMult() == 2.5);
            Check("esencia: gemas de jefe", g.BossGems() == 5 && g.BuyEssence("boss") && g.BossGems() == 7);
            int cg0 = g.ChestOpened();
            g.BuyEssence("chest");
            int cg1 = g.ChestOpened();
            Check("esencia: cofre generoso", cg0 == 20 && cg1 == 25, cg0 + " " + cg1);
            Check("esencia: tope offline y eficiencia", g.OfflineCap() == 8.0 * 3600 && g.OfflineEff() == 0.5);
            g.BuyEssence("night");
            Check("esencia: minero nocturno", g.OfflineCap() == 9.0 * 3600 && T.Approx(g.OfflineEff(), 0.6, 1e-9));
            g.BuyEssence("start");
            g.BuyEssence("start");
            Check("esencia: inicio rapido (2 niveles)", g.StartStage() == 2);
            g.RunMax = 10;
            g.MaxStage = 10;
            g.DoRebirth();
            Check("esencia: renacer empieza 2 etapas adelante", g.World == 1 && g.Sub == 3 && g.RunMax == 2 && g.StageIdx() == 2, g.World + "-" + g.Sub);
            g.Essence = 0;
            Check("esencia: sin saldo no compra", !g.BuyEssence("power"));
            g.Essence = 1000;
            for (int i = 0; i < 40; i++) g.BuyEssence("luck");
            Check("esencia: respeta el maximo", g.EssenceLevel("luck") == 5 && g.EssenceMaxed("luck") && g.EssenceCost("luck") == 0 && !g.BuyEssence("luck"));
            Check("esencia: id invalido", !g.BuyEssence("zzz") && g.EssenceLevel("zzz") == 0);
        }

        // ------------------------------------------------------------ eventos
        static void TestEvents(Ctx c)
        {
            GameState g = c.G;
            c.Fresh();
            List<string> started = new List<string>();
            List<string> ended = new List<string>();
            Action<string, double> c1 = (k, d) => started.Add(k + ":" + d.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            Action<string> c2 = k => ended.Add(k);
            g.EventStarted += c1;
            g.EventEnded += c2;
            g.BoostUntil = 0.0;
            Check("evento: ninguno activo", g.ActiveEvent() == "" && g.GoldMultNow() == 1.0 && g.EventSpeedMult() == 1.0);
            double iv0 = g.ToolInterval(Tl(0, 0));
            g.DebugStartEvent("gold_rush");
            Check("evento: gold_rush activo x3 oro", g.ActiveEvent() == "gold_rush" && g.GoldMultNow() == 3.0 && g.EventGoldMult() == 3.0);
            Check("evento: senal con duracion 20", T.Eq(started, "gold_rush:20.0"), T.Str(started));
            Check("evento: tiempo restante", T.Approx(g.EventTimeLeft(), 20.0, 0.01));
            Check("evento: suma stat events", g.Stat("events") == 1);
            g.Tick(25.0);
            Check("evento: termina solo", g.ActiveEvent() == "" && T.Eq(ended, "gold_rush") && g.GoldMultNow() == 1.0, T.Str(ended));
            g.DebugStartEvent("frenzy");
            Check("evento: frenesi x2 velocidad", T.Approx(g.ToolInterval(Tl(0, 0)), iv0 / 2.0, 1e-9) && g.EventSpeedMult() == 2.0);
            g.DebugStartEvent("meteor");
            Check("evento: meteoro 12 s", g.ActiveEvent() == "meteor" && T.Approx(g.EventTimeLeft(), 12.0, 0.01) && g.GoldMultNow() == 1.0);
            g.DebugStartEvent("chest");
            Check("evento: cofre 30 s", g.ActiveEvent() == "chest" && T.Approx(g.EventTimeLeft(), 30.0, 0.01));
            int gm0 = g.Gems;
            int cg = g.ChestOpened();
            Check("evento: chest_opened da gemas y cierra el evento", cg == 20 && g.Gems == gm0 + 20 && g.ActiveEvent() == "" && g.Stat("chests") == 1);
            g.ActivateBoost();
            g.DebugStartEvent("gold_rush");
            Check("evento: se combina con x3 Oro (x9)", g.GoldMultNow() == 9.0);
            g.BoostUntil = 0.0;
            g.EventsEnabled = false;
            Check("evento: events_enabled=false lo cancela", g.ActiveEvent() == "");
            g.EventsEnabled = true;
            // programacion automatica
            g.EvNext = 0.01;
            g.Tick(0.05);
            Check("evento: arranca solo al vencer el temporizador", g.ActiveEvent() != "");
            g.Tick(60.0);
            Check("evento: siguiente en 180-300 s", g.ActiveEvent() == "" && g.EvNext >= 180.0 && g.EvNext <= 300.0, g.EvNext.ToString());
            g.EventsEnabled = false;
            long s0 = g.Stat("events");
            g.EvNext = 0.0;
            g.Tick(1.0);
            Check("evento: deshabilitado no dispara", g.ActiveEvent() == "" && g.Stat("events") == s0);
            g.EventsEnabled = true;
            // pesos
            Dictionary<string, int> counts = new Dictionary<string, int> { { "gold_rush", 0 }, { "frenzy", 0 }, { "meteor", 0 }, { "chest", 0 } };
            for (int i = 0; i < 4000; i++) counts[g.RollEvent()]++;
            Check("evento: pesos 35/30/20/15",
                T.Approx(counts["gold_rush"] / 4000.0, 0.35, 0.04) && T.Approx(counts["frenzy"] / 4000.0, 0.30, 0.04)
                && T.Approx(counts["meteor"] / 4000.0, 0.20, 0.04) && T.Approx(counts["chest"] / 4000.0, 0.15, 0.04),
                counts["gold_rush"] + "/" + counts["frenzy"] + "/" + counts["meteor"] + "/" + counts["chest"]);
            g.EventStarted -= c1;
            g.EventEnded -= c2;
            g.DebugStartEvent("zzz");
            g.EvKind = "";
        }

        // ------------------------------------------------------------ varios
        static void TestStatsMisc(Ctx c)
        {
            GameState g = c.G;
            c.Fresh();
            g.AddStat("crits");
            g.AddStat("crits", 4);
            Check("stats: add_stat/stat", g.Stat("crits") == 5 && g.Stat("nunca") == 0);
            g.TotalRocks += 1;
            Check("stats: total_rocks += 1 (compat)", g.Stat("rocks") == 1 && g.TotalRocks == 1);
            g.OnRockBroken(false);
            Check("stats: on_rock_broken suma rocas", g.Stat("rocks") == 2);
            g.AddGold(1234.0);
            Check("stats: oro total", g.Stat("gold") == 1234);
            g.Sub = 5;
            g.Progress = 0.99;
            g.NeedBoss = true;
            int gm = g.Gems;
            g.OnRockBroken(true);
            Check("stats: jefe cuenta y da 5 gemas", g.Stat("bosses") == 1 && g.Gems == gm + 5);
            long n = g.Stat("plays");
            g.OnPlayFinished(true, 2);
            g.OnPlayFinished(false);
            Check("stats: on_play_finished", g.Stat("plays") == n + 2 && g.Stat("plays_won") == 1 && g.Stat("plays_2star") == 1);
            Check("stats: crit_chance base", T.Approx(g.CritChance(), 0.1, 1e-9));
            Check("stats: income_per_sec > 0", g.IncomePerSec() > 0.0);
            g.Gold = 100.0;
            int[] sig = { 0 };
            g.StatsChanged += () => sig[0]++;
            g.AddStat("x");
            Check("stats: senal stats_changed", sig[0] == 1);
            // equipar / fusionar / cofre
            c.Fresh();
            g.Gems = 500;
            g.Gacha();
            Check("stats: gacha suma", g.Stat("gacha") == 1);
            g.Inv[5] = Tl(0, 0);
            g.Inv[6] = Tl(0, 0);
            g.MoveTool(new SlotRef(SlotKind.Inv, 5), new SlotRef(SlotKind.Inv, 6));
            Check("stats: fusion suma", g.Stat("merges") == 1);
            g.ActivateBoost();
            Check("stats: boost suma", g.Stat("boosts") == 1);
            // offline con minero nocturno
            c.Fresh();
            g.Lv = new Dictionary<string, int> { { "power", 20 }, { "speed", 10 }, { "money", 10 } };
            double inc = g.IncomePerSec();
            Check("offline: ingreso base", inc > 0.0);
        }

        // ------------------------------------------------------------ guardado
        static void TestSaveRoundtrip(Ctx c)
        {
            GameState g = c.G;
            c.Fresh();
            c.BuyTo("power", 27);
            c.BuyTo("speed", 5);
            g.MaxStage = 14;
            g.RunMax = 14;
            g.World = 3;
            g.Sub = 5;
            g.Essence = 12;
            g.EssenceLv = new Dictionary<string, int> { { "power", 3 }, { "start", 1 } };
            g.Stats["crits"] = 321;
            g.Stats["events"] = 7;
            g.AchClaimed = new List<string> { "rocks_100", "gold_1k" };
            g.GoalIdx = 8;
            g.GoalBase = 77.0;
            g.FeaturesUnlocked = new List<string> { "speed", "money" };
            g.FeaturesSeen = new List<string> { "speed" };
            g.TotalGold = 4567.0;
            g.Gold = 99.0;
            g.Gems = 321;
            g.CheckUnlocks();
            Goal snapGoal = g.CurrentGoal();
            Dictionary<string, object> d = g.ToDict();
            object parsedObj;
            Json.TryParse(Json.Stringify(d), out parsedObj);
            Dictionary<string, object> parsed = (Dictionary<string, object>)parsedObj;
            List<string> beforeUnlocked = new List<string>(g.FeaturesUnlocked);
            g.ResetAll();
            Check("guardado: reset limpia", g.Essence == 0 && g.Stats.Count == 0 && g.FeaturesUnlocked.Count == 0);
            g.ApplyDict(parsed);
            Check("guardado: niveles y etapa", g.Lv["power"] == 27 && g.Lv["speed"] == 5 && g.World == 3 && g.Sub == 5 && g.MaxStage == 14 && g.RunMax == 14);
            Check("guardado: esencia", g.Essence == 12 && g.EssenceLevel("power") == 3 && g.EssenceLevel("start") == 1);
            Check("guardado: estadisticas", g.Stat("crits") == 321 && g.Stat("events") == 7);
            Check("guardado: logros", T.Eq(g.AchClaimed, "rocks_100", "gold_1k"));
            Check("guardado: metas", g.GoalIdx == 8 && g.GoalBase == 77.0 && g.CurrentGoal().Id == snapGoal.Id);
            Check("guardado: desbloqueos", T.Eq(g.FeaturesUnlocked, beforeUnlocked.ToArray()) && T.Eq(g.FeaturesSeen, "speed"), T.Str(g.FeaturesUnlocked));
            Check("guardado: oro/gemas", g.Gold == 99.0 && g.Gems == 321 && g.TotalGold == 4567.0);
            Check("guardado: lv_best", g.LvBest["power"] == 27);
            // via "archivo" (ISaveStore en memoria)
            g.SaveGame();
            g.ResetAll();
            g.LoadGame();
            c.Fresh();
            g.Essence = 9;
            g.EssenceLv = new Dictionary<string, int> { { "gold", 2 } };
            g.Stats["bosses"] = 4;
            g.SaveGame();
            g.Essence = 0;
            g.EssenceLv = new Dictionary<string, int>();
            g.Stats = new Dictionary<string, long>();
            g.LoadGame();
            Check("guardado: archivo conserva esencia y stats", g.Essence == 9 && g.EssenceLevel("gold") == 2 && g.Stat("bosses") == 4);
        }

        public static string OldSaveJson(double lastSeen)
        {
            return "{\"gold\":12345.0,\"gems\":250,\"lv\":{\"power\":60,\"speed\":40,\"money\":55},\"world\":4,\"sub\":2,"
                + "\"progress\":0.4,\"need_boss\":false,\"max_stage\":16,"
                + "\"inv\":[{\"k\":0,\"r\":1},null],\"equip\":[{\"k\":0,\"r\":1},{\"k\":1,\"r\":0},null],"
                + "\"daily\":{\"rocks\":10},\"daily_date\":\"2020-01-01\",\"claimed\":[],\"login_day\":2,\"login_date\":\"2020-01-01\","
                + "\"pass_xp\":120,\"boost_until\":0.0,\"boost_cd_until\":0.0,\"auto_up\":true,\"eco\":false,\"sound\":true,"
                + "\"music\":true,\"skin\":1,\"skins_owned\":[0,1],\"rebirths\":1,\"turbo\":false,\"free_chest_at\":0.0,"
                + "\"piggy\":5,\"total_rocks\":5000,\"total_gold\":9.9e9,\"last_seen\":" + lastSeen.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + ",\"collection\":[\"0_0\",\"1_0\",\"0_1\"]}";
        }

        static void TestOldSave(Ctx c)
        {
            GameState g = c.G;
            string oldJson = OldSaveJson(c.Clock.Now() - 30.0);
            c.Fresh();
            g.ApplyJson(oldJson);
            Check("viejo: carga sin error y estado", g.Lv["power"] == 60 && g.World == 4 && g.Gems >= 250);
            Check("viejo: rocas migradas", g.Stat("rocks") == 5000 && g.TotalRocks == 5000);
            Check("viejo: oro total", g.Stat("gold") == 9900000000L);
            string[] allOld = { "speed", "money", "boost", "tools", "missions", "play", "shop", "skin", "piggy", "auto", "rebirth", "achievements" };
            Check("viejo: todo lo alcanzado desbloqueado", T.Eq(g.FeaturesUnlocked, allOld), T.Str(g.FeaturesUnlocked));
            Check("viejo: sin animaciones (todo visto)", g.NewlyUnlocked().Count == 0);
            Check("viejo: sin esencia ni nodos", g.Essence == 0 && g.EssenceLv.Count == 0);
            Check("viejo: run_max = etapa actual", g.RunMax == g.StageIdx() && g.RunMax == 16);
            Check("viejo: lv_best", g.LvBest["power"] == 60);
            Goal cg = g.CurrentGoal();
            Check("viejo: metas ya cumplidas saltadas", g.GoalIdx > 0 && !cg.Done, "idx=" + g.GoalIdx + " " + cg.Id);
            Check("viejo: logros listos", g.AchievementsReadyCount() > 0);
            Check("viejo: puede renacer", g.CanRebirth() && g.EssenceGainPreview() > 3);
            // via "archivo"
            c.Fresh();
            c.Store.Text = oldJson;
            g.LoadGame();
            Check("viejo: load_game desde archivo", g.Lv["money"] == 55 && g.Stat("rocks") == 5000 && g.NewlyUnlocked().Count == 0);
            // json minimo y corrupto
            g.ResetAll();
            c.Store.Text = "{}";
            g.LoadGame();
            Check("viejo: JSON vacio -> valores por defecto", g.Lv["power"] == 1 && g.World == 1 && g.GoalIdx == 0 && g.FeaturesUnlocked.Count == 0);
            c.Store.Text = "esto no es json";
            g.LoadGame();
            Check("viejo: archivo corrupto no rompe", g.Lv["power"] == 1);
        }
    }
}
