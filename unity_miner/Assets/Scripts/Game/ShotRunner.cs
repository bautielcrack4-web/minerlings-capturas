#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Mineros.Core;
using UnityEngine;

namespace Mineros.Game
{
    /// <summary>
    /// Capturas automaticas (solo editor): si la linea de comandos trae -shotScenario, arma una partida nueva
    /// con el estado del escenario (mismos escenarios que miner_idle/tools/shot.gd), saca los PNG y cierra Unity.
    /// Lo lanza ShotTool.Capture (Editor) entrando en Play.
    /// </summary>
    public sealed class ShotRunner : MonoBehaviour
    {
        string outDir, scen;
        GameManager gm;
        GameState G { get { return gm.G; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            string s = Arg("-shotScenario", null);
            if (s == null) return;
            Bootstrap.Manual = true;
            var go = new GameObject("ShotRunner");
            DontDestroyOnLoad(go);
            var r = go.AddComponent<ShotRunner>();
            r.scen = s;
            r.outDir = Arg("-shotOut", "/tmp/unity_shots");
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(outDir);
            var store = new FileSaveStore("shot.json");
            store.Delete();
            StartCoroutine(Watchdog());
            if (scen.StartsWith("isla"))
            {
                new FileSaveStore("shotisla.json").Delete();
                Mineros.IslandView.IslandGame.Build("shotisla.json", ConfigureIsland);
                Portrait();
                yield return null;
                Portrait();
                yield return RunIsland();
                Debug.Log("shotcheck FIN " + scen + " fotos=" + shotCount + " t=" + Time.realtimeSinceStartup.ToString("0") + "s cuadros=" + Time.frameCount);
                UnityEditor.EditorApplication.Exit(0);
                yield break;
            }
            gm = Bootstrap.Build("shot.json", Configure);
            Portrait();
            yield return null;
            Portrait();
            yield return Run();
            UnityEditor.EditorApplication.Exit(0);
        }

        /// <summary>Si algo se rompe (excepcion al armar la escena), no dejar Unity colgado.</summary>
        static IEnumerator Watchdog()
        {
            float lim;
            if (!float.TryParse(Arg("-shotTimeout", "1500"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out lim)) lim = 1500f;
            yield return new WaitForSecondsRealtime(lim);
            // linea propia: el grep de unity_shot.sh la muestra (un escenario cortado nunca puede pasar por terminado)
            Debug.LogError("ShotRunner: tiempo agotado\nshotcheck CORTADO t=" + lim.ToString("0") + "s ultima=" + lastShot + " fotos=" + shotCount);
            UnityEditor.EditorApplication.Exit(2);
        }

        // ------------------------------------------------------------ Isla Minera
        /// <summary>Materiales, constructores y Ayuntamiento de sobra (las obras se terminan con FinishAllWork).</summary>
        static void Rich(Island isl, int th)
        {
            for (int i = 0; i < isl.Stock.Length; i++) isl.Stock[i] = Mathf.Max(isl.Stock[i], 400);
            isl.BonusBuilders = 30;
            isl.Plots[0].Level = Mathf.Max(isl.Plots[0].Level, th);
        }

        void ConfigureIsland(Island isl)
        {
            if (scen == "isla_inicio") return;
            if (scen == "isla_recorrido") return;   // partida nueva de verdad: el recorrido la juega como un jugador
            if (scen != "isla_tutorial") isl.Tut = Island.TutStep.Done;   // las demas capturas no son de una partida nueva
            if (scen == "isla_ciudad") { ConfigureCity(isl); return; }
            if (scen == "isla_cuartel") { ConfigureBarracks(isl); return; }
            if (scen == "isla_kit")
            {
                // el dormitorio de cobre (kit modelado) adelante a la izquierda, a la vista de la camara
                ConfigureBarracks(isl);
                foreach (var md in isl.Modules) if (md.Kind == ModKind.Dorm) { if (md.X == -1 && md.Z == -1) md.Ch = 1; else if (md.Ch == 1) md.Ch = 2; }
                return;
            }
            if (scen == "isla_tutorial") return;
            if (scen == "isla_diario")
            {
                isl.Coins = 1e6; isl.TotalEarned = 5000;
                isl.Upgrade(isl.Find(BKind.House)); isl.Upgrade(isl.Find(BKind.House));
                isl.Coins = 800; isl.Gems = 12;
                isl.LastDay = Mineros.IslandView.IslandGame.Today - 1; isl.Streak = 3; isl.ClaimedDay = isl.LastDay;
                isl.LastSeen = (System.DateTime.UtcNow - new System.DateTime(2024, 1, 1)).TotalSeconds - 3600;
                return;
            }
            if (scen == "isla_offline")
            {
                isl.Coins = 50;
                isl.LastSeen = (System.DateTime.UtcNow - new System.DateTime(2024, 1, 1)).TotalSeconds - 3600;
                return;
            }
            isl.Coins = 1e6;
            isl.TotalEarned = 5000;
            Rich(isl, 4);
            isl.Upgrade(isl.Find(BKind.House)); isl.FinishAllWork();
            isl.Upgrade(isl.Find(BKind.House)); isl.FinishAllWork();
            isl.Build(BKind.Canteen, isl.Plots[2]);
            isl.Build(BKind.Showers, isl.Plots[3]);
            isl.Build(BKind.Smithy, isl.Plots[4]);
            isl.FinishAllWork();
            for (int i = 0; i < 3; i++) { isl.Upgrade(isl.Find(BKind.Depot)); isl.FinishAllWork(); }
            isl.BonusBuilders = 0;
            for (int i = 0; i < isl.Stock.Length; i++) isl.Stock[i] = 0;
            isl.Stock[(int)Res.Stone] = 12; isl.Stock[(int)Res.Wood] = 8;
            isl.Coins = 850;
            for (int i = 0; i < 6; i++) isl.SpawnOre(true);
            if (scen == "isla_show")
            {
                isl.Coins = 1e6;
                Rich(isl, 4);
                while (isl.Find(BKind.House).Level < 5) { isl.Upgrade(isl.Find(BKind.House)); isl.FinishAllWork(); }   // la proxima mejora (6) evoluciona
                isl.Coins = 50000;
                foreach (var p in isl.Plots) p.BuildT = -1f;
                return;
            }
            if (scen == "isla_eventos" || scen == "isla_mundo")
            {
                isl.Coins = 1e6;
                isl.Expand = 1;
                Rich(isl, 4);
                foreach (var k in new[] { BKind.Mine, BKind.Dock, BKind.Lighthouse })
                    foreach (var p in isl.Plots) if (isl.Build(k, p)) break;
                isl.FinishAllWork();
                isl.BonusBuilders = 0;
                isl.Coins = 4200;
                isl.Gems = 12;
                isl.GoalIdx = 9;
            }
            foreach (var p in isl.Plots) p.BuildT = -1f;
        }

        // ------------------------------------------------------------ ciudad (0.9)
        void ConfigureCity(Island isl)
        {
            isl.Coins = 5e6; isl.TotalEarned = 2e5; isl.Gems = 40;
            Rich(isl, 8);
            isl.Expand = 3;
            isl.Upgrade(isl.Find(BKind.House)); isl.FinishAllWork();
            isl.Upgrade(isl.Find(BKind.House)); isl.FinishAllWork();
            // edificios terminados de varias familias
            var done = new[] { BKind.Canteen, BKind.Sawmill, BKind.Barn, BKind.Warehouse, BKind.CoalMine, BKind.Showers, BKind.IronMine,
                BKind.Foundry, BKind.Smithy, BKind.Mine, BKind.Market, BKind.Train, BKind.CopperMine, BKind.SandPit, BKind.GlassKiln,
                BKind.Workshop, BKind.Managers };
            foreach (var k in done) { var f = isl.Plots.Find(p => isl.Allowed(k, p)); if (f != null) isl.Build(k, f); }
            isl.FinishAllWork();
            foreach (var p in isl.Plots) if (p.Building >= 0 && p.Building != (int)BKind.Depot) { for (int i = 0; i < 3; i++) { if (isl.Upgrade(p)) isl.FinishAllWork(); } }
            Rich(isl, 8);
            // obras en curso: una recien empezada, una con lona a la mitad, una casi lista; y una mejora
            var b1 = isl.Plots.Find(p => isl.Allowed(BKind.Bank, p)); if (b1 != null) { isl.Build(BKind.Bank, b1); b1.Work = b1.WorkTotal * 0.55; }
            var b2 = isl.Plots.Find(p => isl.Allowed(BKind.School, p)); if (b2 != null) { isl.Build(BKind.School, b2); b2.Work = b2.WorkTotal * 0.2; }
            var foundry = isl.Find(BKind.Foundry);
            // produccion: listos para cobrar y colas
            foreach (var p in isl.Plots)
            {
                var ex = Island.Extractor(p.Building);
                if (ex != null && p.Level >= 1) { p.ReadyRes = (int)ex.Out; p.Ready = 3 + p.Id % 4; }
            }
            int bar = Island.RecipesOf(BKind.Foundry)[0];
            for (int i = 0; i < 3; i++) isl.QueueRecipe(foundry, bar);
            foundry.ProdT = 25;
            var gk = isl.Find(BKind.GlassKiln); gk.Ready = 2; gk.ReadyRes = (int)Res.Glass;
            isl.Upgrade(isl.Find(BKind.Smithy));
            isl.TrainSoon();
            for (int i = 0; i < isl.Stock.Length; i++) isl.Stock[i] = 30 + (i * 7) % 25;
            isl.BonusBuilders = 1;
            foreach (var p in isl.Plots) p.BuildT = -1f;
            for (int i = 0; i < 6; i++) isl.SpawnOre(true);
        }

        /// <summary>Complejo (0.11): Cuartel nivel 6 con linea Descarga -> Trituradora -> Tesoreria, 4 dormitorios y un piso arriba.</summary>
        void ConfigureBarracks(Island isl)
        {
            isl.Coins = 5e7; isl.TotalEarned = 5e5; isl.Gems = 60;
            Rich(isl, 9);
            isl.Expand = 3;
            isl.Upgrade(isl.Find(BKind.House)); isl.FinishAllWork();
            var bp = isl.Plots.Find(p => isl.Allowed(BKind.Barracks, p));
            isl.Build(BKind.Barracks, bp);
            isl.FinishAllWork();
            // lugar despejado y nivel 6
            for (float r = 6f; r < isl.Radius - 4f; r += 0.5f)
            {
                bool ok = false;
                for (int k = 0; k < 24 && !ok; k++)
                {
                    float a = k * Mathf.PI / 12f;
                    bp.Level = 7;
                    if (isl.CanPlace(bp, bp.Building, Island.Snap(Mathf.Cos(a) * r), Island.Snap(Mathf.Sin(a) * r))) ok = isl.MovePlot(bp, Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                }
                if (ok) break;
            }
            bp.Level = 6;
            isl.Tick(0.02f);
            for (int i = 0; i < isl.Stock.Length; i++) isl.Stock[i] = 5000;
            isl.Coins = 5e7;
            isl.BuyModule(ModKind.Unload, 1, -1, 0, -1);
            isl.BuyModule(ModKind.Crusher, 1, 0, 0, -1);
            isl.BuyModule(ModKind.Treasury, 1, 1, 0, -1);
            isl.BuyModule(ModKind.Dorm, -1, 0, 0, -1, 1);
            isl.BuyModule(ModKind.Dorm, -1, -1, 0, -1, 2);
            isl.BuyModule(ModKind.Dorm, 0, 1, 0, -1, 4);
            isl.BuyModule(ModKind.Dorm, -1, 1, 0, -1, 3);
            isl.BuyModule(ModKind.Lab, 0, 1, 1, -1);
            isl.BuyModule(ModKind.Tools, -1, 0, 1, -1);
            var cr = isl.FirstMod(ModKind.Crusher); if (cr != null) cr.Stage = 2;
            var tr = isl.FirstMod(ModKind.Treasury); if (tr != null) { tr.Stage = 3; tr.Deco = 1; }
            for (int i = 0; i < isl.Stock.Length; i++) isl.Stock[i] = 5000;
            isl.Coins = 5e7;
            foreach (var p in isl.Plots) p.BuildT = -1f;
        }

        IEnumerator RunBarracks(Mineros.IslandView.IslandGame g)
        {
            var isl = g.Isl;
            var bp = isl.Find(BKind.Barracks);
            Vector3 c = new Vector3(bp.X, 0f, bp.Z);
            yield return Wait(2f);
            g.DebugLook(c, 9.5f);
            yield return Wait(2f);
            Snap("cuartel_isla");
            g.EnterComplex();
            yield return Wait(1.2f);
            Snap("cuartel_modo");
            // iman: el Comedor se arrastra y se engancha al frente
            g.BeginModPlace(ModKind.Mess);
            yield return Wait(0.4f);
            g.DebugModAt(0, -2);
            yield return Wait(0.6f);
            Snap("cuartel_arrastre");
            g.DebugModAt(0, -1);
            yield return Wait(0.7f);
            Snap("cuartel_iman");
            g.ConfirmPlace();
            yield return Wait(0.1f);
            Snap("cuartel_cae");
            yield return Wait(1.6f);
            Snap("cuartel_listo");
            // cadena: vagonetas por la linea
            var u = isl.FirstMod(ModKind.Unload);
            for (int i = 0; i < 3; i++) { isl.SpawnCart(u, Island.OreIron, 4, 400); yield return Wait(0.45f); }
            yield return Wait(0.3f);
            Snap("cuartel_cadena");
            yield return Wait(1.2f);
            // seleccion y acciones
            g.SelectedMod = isl.FirstMod(ModKind.Treasury);
            g.Ui.ModSelected(g.SelectedMod);
            yield return Wait(0.6f);
            Snap("cuartel_acciones");
            g.SelectedMod = null; g.Ui.ModSelected(null);
            // piso de arriba
            g.SetViewFloor(1);
            yield return Wait(0.8f);
            Snap("cuartel_piso2");
            g.SetViewFloor(0);
            // evento: averia
            isl.StartCxEvent(CxEventKind.Breakdown);
            yield return Wait(0.8f);
            Snap("cuartel_evento");
            // contratos y libro
            isl.ContractNewDay(5);
            for (int i = 0; i < 40; i++) { isl.Tick(0.1f); if (isl.Offers.Count >= 2) break; }
            g.Ui.OpenContracts();
            yield return Wait(1f);
            Snap("cuartel_contratos");
            g.Ui.CloseSheet();
            isl.CombosFound[4] = true; isl.CombosFound[1] = true;
            g.Ui.OpenBook();
            yield return Wait(1.4f);
            Snap("cuartel_libro");
            g.Ui.CloseSheet();
            // sala secreta
            isl.AddPlans(1);
            var sec = isl.BuySecret(1, 0, 1, -1);
            if (sec != null) sec.Work = 0.05;
            yield return Wait(1.6f);
            Snap("cuartel_secreta");
            g.Ui.CloseSheet();
            yield return Wait(0.3f);
            g.ExitComplex();
            // los especialistas en sus 3 etapas
            Vector3 right = new Vector3(Mathf.Cos(35f * Mathf.Deg2Rad), 0f, -Mathf.Sin(35f * Mathf.Deg2Rad));
            Vector3 front = new Vector3(-Mathf.Sin(35f * Mathf.Deg2Rad), 0f, -Mathf.Cos(35f * Mathf.Deg2Rad));
            Vector3 row = c + front * 7f;
            int n = 0;
            foreach (var m in isl.Miners)
            {
                m.Level = n % 3 == 0 ? 1 : n % 3 == 1 ? 5 : 9;
                Vector3 at = row + right * ((n - (isl.Miners.Count - 1) * 0.5f) * 1.1f);
                m.X = at.x; m.Z = at.z; m.Y = 0f; m.InComplex = false; m.Path = null; m.State = MState.Idle;
                n++;
            }
            g.DebugRefreshMiners();
            g.DebugLook(row, 4.2f);
            for (int f = 0; f < 3; f++) yield return null;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(0.5f);
            Snap("cuartel_mineros");
            Time.timeScale = 1f;
            // puerta a la calle: un minero en el umbral la abre; las demas quedan cerradas
            Vector3 door;
            if (g.DebugDoor(0, out door) && isl.Miners.Count > 0)
            {
                var dm = isl.Miners[0];
                dm.X = door.x; dm.Z = door.z; dm.State = MState.Idle;
                g.Ui.CloseSheet();
                dm.X = door.x + 4f;
                for (int f = 0; f < 40; f++) { dm.X = door.x + 4f; dm.Z = door.z; g.DebugLook(door, 3.2f); yield return null; }
                Snap("cuartel_puerta_cerrada");
                for (int f = 0; f < 50; f++) { dm.X = door.x; dm.Z = door.z; g.DebugLook(door, 3.2f); yield return null; }
                Snap("cuartel_puerta");
                Debug.Log("puertacheck puertas=" + g.DebugDoorCount);
            }
            Debug.Log("cuartelcheck modulos=" + isl.Modules.Count + " mineros=" + isl.Miners.Count + " vagonetas=" + isl.Stat("carts") + " valido=" + (isl.ValidateComplex() ?? "ok"));
        }

        IEnumerator RunCity(Mineros.IslandView.IslandGame g)
        {
            var isl = g.Isl;
            for (int f = 0; f < 120; f++) yield return null;
            // experimentos de rendimiento: sin interfaz / sin sombras
            if (System.Environment.GetEnvironmentVariable("PERF_NOUI") == "1")
                foreach (var cv in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) cv.enabled = false;
            if (System.Environment.GetEnvironmentVariable("PERF_NOSHADOW") == "1") QualitySettings.shadows = ShadowQuality.Disable;
            for (int f = 0; f < 5; f++) yield return null;
            Snap("ciudad_vista");
            // Plan Pueblo: edificios de kit (Fundicion) de cerca, techo puesto y abierto
            var fnd = isl.Find(BKind.Foundry);
            if (fnd != null)
            {
                Vector3 fw = g.PlotWorld(fnd);
                yield return RoofSettle(g, fw, 11f); Snap("ciudad_kit_z11");
                yield return RoofSettle(g, fw, 6f); Snap("ciudad_kit_z6");
                g.DebugLook(Vector3.zero, 15.2f);
                yield return Wait(0.5f);
            }
            // cada nivel se ve: la Casa y la Fundicion del 1 al 10, y un cuadro de la ceremonia de nivel
            foreach (var kk in new[] { BKind.House, BKind.Foundry })
            {
                var lp = isl.Find(kk);
                if (lp == null) continue;
                int keep = lp.Level;
                Vector3 lw = g.PlotWorld(lp);
                for (int lv = 1; lv <= 10; lv++)
                {
                    g.DebugSetLevel(lp, lv);
                    g.DebugLook(lw, 6.8f);
                    for (int f = 0; f < 6; f++) yield return null;
                    Snap("nivel_" + kk.ToString().ToLower() + "_" + lv.ToString("00"));
                }
                g.DebugSetLevel(lp, 4);
                for (int f = 0; f < 4; f++) yield return null;
                g.DebugLevelUp(lp, 5);
                yield return Wait(0.62f); Snap("nivel_" + kk.ToString().ToLower() + "_ceremonia_a");
                yield return Wait(0.5f); Snap("nivel_" + kk.ToString().ToLower() + "_ceremonia_b");
                yield return Wait(1.2f);
                g.DebugSetLevel(lp, keep);
                g.DebugLook(Vector3.zero, 15.2f);
                yield return Wait(0.3f);
            }
            g.Ui.OpenMenu();
            for (int f = 0; f < 30; f++) yield return null;
            Snap("ciudad_menu");
            g.Ui.CloseSheet();
            for (int f = 0; f < 20; f++) yield return null;
            if (isl.Orders.Count > 1) { isl.Orders[0].Wait = 0f; isl.Orders[0].Delivered = isl.Orders[0].Count; isl.Orders[1].Wait = 0f; isl.Orders[1].Delivered = isl.Orders[1].Count * 8 / 10; }
            g.Ui.OpenBoard();
            for (int f = 0; f < 30; f++) yield return null;
            Snap("ciudad_tablon");
            g.Ui.CloseSheet();
            for (int f = 0; f < 20; f++) yield return null;
            // mover un edificio como en Clash of Clans
            var mp = isl.Find(BKind.Sawmill);
            if (mp != null && g.BeginMove(mp))
            {
                g.DebugLook(g.PlotWorld(mp), 11f);
                yield return Wait(0.6f);
                var other = isl.Find(BKind.Foundry) ?? isl.Plots[0];
                g.DebugPlaceAt(other.X + 1f, other.Z);
                g.DebugLook(new Vector3(other.X, 0f, other.Z), 11f);
                yield return Wait(0.5f);
                Snap("mover_rojo");
                var spot = g.DebugFreeSpot(mp, mp.Building);
                if (spot != null)
                {
                    g.DebugPlaceAt(spot.Value.x, spot.Value.z);
                    g.DebugLook(spot.Value, 11f);
                    yield return Wait(0.5f);
                    Snap("mover_verde");
                    g.ConfirmPlace();
                    yield return Wait(0.6f);
                    Snap("mover_camino");
                    yield return Wait(1.6f);
                    Snap("mover_listo");
                    Debug.Log("movercheck x=" + mp.X + " z=" + mp.Z + " ok=" + (mp.X == spot.Value.x && mp.Z == spot.Value.z));
                }
                else { g.CancelPlace(); Debug.Log("movercheck sin lugar"); }
            }
            // colocar uno nuevo
            var np = isl.Plots.Find(p => isl.Offered(p));
            if (np != null)
            {
                BKind nk = BKind.House;
                foreach (var d in Island.Defs) if (isl.CanBuild(d.Kind, np)) { nk = d.Kind; break; }
                g.BeginPlace(np, nk, () => isl.Build(nk, np));
                g.DebugLook(g.PlacePos, 11f);
                yield return Wait(0.6f);
                Snap("colocar_nuevo");
                g.CancelPlace();
            }
            g.DebugLook(Vector3.zero, 15.2f);
            yield return Wait(0.3f);
            g.Ui.OpenPlot(isl.Find(BKind.Foundry));
            for (int f = 0; f < 40; f++) yield return null;
            Snap("ciudad_fundicion");
            g.Ui.CloseSheet();
            for (int f = 0; f < 20; f++) yield return null;
            g.Ui.OpenPlot(isl.Plots.Find(p => isl.Offered(p)));
            for (int f = 0; f < 40; f++) yield return null;
            Snap("ciudad_catalogo");
            g.Ui.CloseSheet();
            for (int f = 0; f < 20; f++) yield return null;
            g.Ui.OpenPlot(isl.Find(BKind.Market));
            for (int f = 0; f < 40; f++) yield return null;
            Snap("ciudad_mercado");
            g.Ui.CloseSheet();
            for (int f = 0; f < 20; f++) yield return null;
            g.Ui.CloseSheet();
            g.FocusOn(g.PlotWorld(isl.Find(BKind.Train)));
            for (int f = 0; f < 40; f++) yield return null;
            Snap("ciudad_tren_llega");
            g.Ui.OpenPlot(isl.Find(BKind.Train));
            for (int f = 0; f < 40; f++) yield return null;
            Snap("ciudad_tren");
            g.Ui.CloseSheet();
            for (int f = 0; f < 20; f++) yield return null;
            g.Ui.OpenPlot(isl.Find(BKind.Depot));
            for (int f = 0; f < 40; f++) yield return null;
            Snap("ciudad_ayuntamiento");
            g.Ui.CloseSheet();
            for (int f = 0; f < 20; f++) yield return null;
            g.Ui.OpenPlot(isl.Find(BKind.Bank));
            for (int f = 0; f < 40; f++) yield return null;
            Snap("ciudad_obra");
            g.Ui.CloseSheet();
            for (int f = 0; f < 20; f++) yield return null;
            isl.FeedPiggy(150);
            g.Ui.OpenShop();
            for (int f = 0; f < 40; f++) yield return null;
            Snap("tienda");
            g.Ui.CloseSheet();
            for (int f = 0; f < 30; f++) yield return null;
            Snap("cofre_anuncio");
            yield return FarShots(g);
            // obra nueva en vivo: colocacion, y despues terminarla para ver la revelacion
            isl.Plots[0].Level = 9;
            isl.BonusBuilders = 8;
            var free = isl.Plots.Find(p => isl.Allowed(BKind.Hospital, p));
            if (free != null)
            {
                isl.Coins = 1e9; for (int i = 0; i < isl.Stock.Length; i++) isl.Stock[i] = Mathf.Max(isl.Stock[i], 100);
                isl.BonusBuilders = 5;
                isl.Build(BKind.Hospital, free);
                for (int f = 0; f < 30; f++) yield return null;
                Snap("ciudad_colocar");
                free.Work = 0.01;
                for (int f = 0; f < 14; f++) yield return null;
                Snap("ciudad_revelar");
                for (int f = 0; f < 60; f++) yield return null;
                Snap("ciudad_revelado");
            }
            // costo de dibujo por grupo (para el presupuesto de A.4)
            var groups = new Dictionary<string, int>();
            var mats = new Dictionary<string, int>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                var t = r.transform; string key = r.name;
                while (t.parent != null && t.parent.name != "Mundo" && t.parent.parent != null) { t = t.parent; }
                key = t.name.Length > 8 ? t.name.Substring(0, 8) : t.name;
                groups[key] = (groups.ContainsKey(key) ? groups[key] : 0) + r.sharedMaterials.Length;
                foreach (var m in r.sharedMaterials) if (m != null) { string mk = m.shader.name + (m.enableInstancing ? "+inst" : ""); mats[mk] = (mats.ContainsKey(mk) ? mats[mk] : 0) + 1; }
            }
            var byName = new Dictionary<string, int>();
            var shadowBy = new Dictionary<string, int>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                string n = r.name.Length > 14 ? r.name.Substring(0, 14) : r.name;
                byName[n] = (byName.ContainsKey(n) ? byName[n] : 0) + 1;
                if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) shadowBy[n] = (shadowBy.ContainsKey(n) ? shadowBy[n] : 0) + 1;
            }
            var lst = new List<KeyValuePair<string, int>>(byName);
            lst.Sort((x, y) => y.Value.CompareTo(x.Value));
            var sn = new System.Text.StringBuilder("renderers:");
            for (int i = 0; i < Mathf.Min(30, lst.Count); i++) sn.Append(" ").Append(lst[i].Key).Append("=").Append(lst[i].Value).Append(shadowBy.ContainsKey(lst[i].Key) ? "(s" + shadowBy[lst[i].Key] + ")" : "");
            Debug.Log(sn.ToString());
            var sb = new System.Text.StringBuilder("dibujo:");
            foreach (var kv in groups) if (kv.Value >= 4) sb.Append(" ").Append(kv.Key).Append("=").Append(kv.Value);
            sb.Append(" | shaders:");
            foreach (var kv in mats) sb.Append(" ").Append(kv.Key).Append("=").Append(kv.Value);
            Debug.Log(sb.ToString());
            Debug.Log("ciudadcheck th=" + isl.Th + " edificios=" + isl.Plots.FindAll(p => p.Building >= 0).Count + " obras=" + isl.BusyBuilders() + " tren=" + isl.TrainHere);
        }

        IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        IEnumerator RunTutorial(Mineros.IslandView.IslandGame g)
        {
            var isl = g.Isl;
            // presentacion de Old Gus: escribiendo, frase completa, segunda frase y la isla despues de cerrarla
            UnityEngine.PlayerPrefs.DeleteKey("intro_gus_v1");
            yield return Frames(110);
            Snap("tut_0a_gus_escribe");
            yield return Frames(90);
            Snap("tut_0b_gus_frase");
            for (int i = 0; i < 2; i++) { g.Ui.DebugIntroTap(); yield return Frames(4); }
            yield return Frames(150);
            Snap("tut_0c_gus_segunda");
            for (int i = 0; i < 8; i++) { g.Ui.DebugIntroTap(); yield return Frames(6); }
            yield return Frames(40);
            Debug.Log("tutcheck presentacion abierta=" + g.Ui.IntroOpen);
            Snap("tut_1_inicio");
            Debug.Log("tutcheck inicio paso=" + isl.Tut + " mineros=" + isl.Miners.Count + " ofrecidas=" + isl.Plots.FindAll(p => isl.Offered(p)).Count);
            // el jugador rompe una roca tocandola
            Ore o = null; foreach (var x in isl.OreList) if (!x.Dead && x.Kind == 0) { o = x; break; }
            for (int i = 0; i < 20 && o != null && !o.Dead; i++) { isl.TapOre(o, i); yield return Frames(2); }
            yield return Frames(60);
            Snap("tut_2_minero");
            isl.Coins = 30; isl.Stock[(int)Res.Stone] = 6; isl.AddStat("rocks", 5);
            yield return Frames(40);
            Snap("tut_3_mejorar");
            isl.Upgrade(isl.Find(BKind.House));
            yield return Frames(40);
            Snap("tut_4_terminar");
            isl.SpeedUp(isl.Find(BKind.House));
            yield return Frames(200);
            Snap("tut_5_cartas");
            g.Ui.DebugFlip(0); yield return Frames(80);
            g.Ui.DebugPick(0);
            yield return Frames(30);
            Snap("tut_6a_carta_mano");
            // como un jugador: arrastra la carta a la isla (antes quedaba en la mano todo el tutorial)
            Vector3 tdrop = g.FreeSummonPoint(g.ScreenToGround(new Vector2(g.Cam.pixelWidth * 0.5f, g.Cam.pixelHeight * 0.5f)));
            g.Ui.DebugHold(tdrop); yield return Frames(6);
            g.Ui.DebugDrop(tdrop);
            yield return Frames(22);
            Snap("tut_6_chispa");
            yield return Frames(60);
            Snap("tut_7_brota");
            yield return Frames(120);
            Snap("tut_8_aserradero");
            isl.Coins = 100;
            var free = isl.Plots.Find(p => isl.Offered(p));
            if (free != null) isl.Build(BKind.Sawmill, free);
            yield return Frames(30);
            if (free != null) isl.SpeedUp(free);
            yield return Frames(400);
            Snap("tut_9_cobrar");
            if (free != null) isl.Collect(free);
            yield return Frames(120);
            Snap("tut_10_listo");
            Debug.Log("tutcheck fin paso=" + isl.Tut + " mineros=" + isl.Miners.Count + " meta=" + isl.GoalIdx);
        }

        /// <summary>
        /// Recorrido completo para la auditoria (despues del tutorial de la partida nueva): lo que ve el jugador al
        /// terminarlo, primera mejora con barco y cartas, la invocacion, el Cuartel (construirlo, Modo Cuartel, poner una
        /// habitacion), los 3 zooms, expandir, menu/tienda/diario/album, noche y un evento. Cada paso deja "reccheck".
        /// </summary>
        IEnumerator RunTour(Mineros.IslandView.IslandGame g)
        {
            var isl = g.Isl;
            float t0 = Time.realtimeSinceStartup;
            System.Action<string> Log = k => Debug.Log("reccheck " + k + " monedas=" + isl.Coins.ToString("0") + " gemas=" + isl.Gems + " mineros=" + isl.Miners.Count
                + " th=" + isl.Level(BKind.Depot) + " meta=" + isl.GoalIdx + " tut=" + isl.Tut + " t=" + (Time.realtimeSinceStartup - t0).ToString("0") + "s");
            // iconos de todos los edificios (catalogo) y las 4 etapas de algunos (cada evolucion suma una sala): hojas para revisarlos
            System.Action<BKind[], int[], int, string> Sheet = (kinds, tiers, cols, file) =>
            {
                int sz = 256, rows = (kinds.Length + cols - 1) / cols;
                var sheetTex = new Texture2D(cols * sz, rows * sz, TextureFormat.RGBA32, false);
                var clear = new Color32[cols * sz * rows * sz]; for (int i = 0; i < clear.Length; i++) clear[i] = new Color32(235, 225, 205, 255);
                sheetTex.SetPixels32(clear);
                for (int i = 0; i < kinds.Length; i++)
                {
                    var spr = Mineros.IslandView.IslandStage.I.BuildingIcon(kinds[i], tiers[i]);
                    var tmp = RenderTexture.GetTemporary(sz, sz, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    var prevRt = RenderTexture.active;
                    Graphics.Blit(spr.texture, tmp);
                    RenderTexture.active = tmp;
                    var rd = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
                    rd.ReadPixels(new Rect(0, 0, sz, sz), 0, 0); rd.Apply();
                    RenderTexture.active = prevRt; RenderTexture.ReleaseTemporary(tmp);
                    var px = rd.GetPixels32();
                    int ox = (i % cols) * sz, oy = (rows - 1 - i / cols) * sz;
                    var dst = sheetTex.GetPixels32();
                    for (int y = 0; y < sz; y++) for (int x = 0; x < sz; x++) { var c = px[y * sz + x]; if (c.a > 20) dst[(oy + y) * cols * sz + ox + x] = c; }
                    sheetTex.SetPixels32(dst);
                    Destroy(rd);
                }
                sheetTex.Apply();
                File.WriteAllBytes(Path.Combine(outDir, file), sheetTex.EncodeToPNG());
            };
            {
                var defs = Island.Defs;
                var ks = new BKind[defs.Length]; var ts = new int[defs.Length];
                for (int i = 0; i < defs.Length; i++) { ks[i] = defs[i].Kind; ts[i] = 1; }
                Sheet(ks, ts, 6, "iconos_edificios.png");
                var ek = new[] { BKind.Foundry, BKind.CoalMine, BKind.Bank, BKind.School };
                var ks2 = new BKind[16]; var ts2 = new int[16];
                for (int i = 0; i < 16; i++) { ks2[i] = ek[i / 4]; ts2[i] = 1 + i % 4; }
                Sheet(ks2, ts2, 4, "iconos_etapas.png");
                Debug.Log("iconocheck " + defs.Length + " iconos");
            }
            // 1) lo que queda en pantalla al terminar el tutorial, sin tocar nada (se entiende que hacer?)
            g.Ui.CloseSheet();
            for (int f = 0; f < 60; f++) yield return null;
            Snap("rec_01_despues_tutorial");
            Log("despues_tutorial");
            // fin de semana dorado (forzado: hoy puede no ser sabado)
            Mineros.IslandView.IslandGame.DebugWeekend = true;
            for (int f = 0; f < 40; f++) yield return null;
            Snap("rec_01b_fin_de_semana");
            Mineros.IslandView.IslandGame.DebugWeekend = false;
            // 2) juego libre (los mineros pican solos): cuanto junta un jugador que solo mira
            for (int f = 0; f < 300; f++) yield return null;
            Snap("rec_02_un_rato");
            Log("un_rato");
            // 3) primera mejora de la casa -> barco con cartas
            isl.Coins = System.Math.Max(isl.Coins, isl.UpgradeCost(isl.Find(BKind.House)) + 50);
            for (int i = 0; i < isl.Stock.Length; i++) isl.Stock[i] = System.Math.Max(isl.Stock[i], 40);
            g.Ui.OpenPlot(isl.Find(BKind.House));
            for (int f = 0; f < 25; f++) yield return null;
            Snap("rec_03_hoja_casa");
            g.Ui.DebugPress();
            for (int f = 0; f < 4; f++) yield return null;
            Snap("rec_03b_mejora_se_ve");   // la hoja se va y la camara muestra la obra
            for (int f = 0; f < 6; f++) yield return null;
            g.Ui.CloseSheet();
            isl.FinishAllWork();
            for (int k = 0; k < 60 && !g.Ui.DebugFlip(0); k++) yield return null;
            for (int f = 0; f < 20; f++) yield return null;
            Snap("rec_04_barco_cartas");
            g.Ui.DebugFlip(1); g.Ui.DebugFlip(2);
            for (int f = 0; f < 40; f++) yield return null;
            Snap("rec_05_cartas_vueltas");
            g.Ui.DebugPick(0);
            for (int f = 0; f < 40; f++) yield return null;
            Snap("rec_06_carta_mano");
            Vector3 drop = g.FreeSummonPoint(g.ScreenToGround(new Vector2(g.Cam.pixelWidth * 0.5f, g.Cam.pixelHeight * 0.5f)));
            g.Ui.DebugHold(drop);
            for (int f = 0; f < 8; f++) yield return null;
            g.Ui.DebugDrop(drop);
            for (int f = 0; f < 6; f++) yield return null;
            Snap("rec_07_carta_clavada");
            for (int f = 0; f < 14; f++) yield return null;
            Snap("rec_08_minero_sale");
            for (int f = 0; f < 40; f++) yield return null;
            Snap("rec_09_minero_llego");
            Log("invocacion");
            // carnet del minero (sin desenfoque): tres mineros distintos, cada uno con su cara y su nombre
            for (int k = 0; k < Mathf.Min(3, isl.Miners.Count); k++)
            {
                var cm = isl.Miners[isl.Miners.Count - 1 - k];
                g.DebugLook(g.MinerWorld(cm), 7f);
                g.Ui.ShowMiner(cm);
                for (int f = 0; f < 30; f++) yield return null;
                Snap("rec_09c_carnet_" + k);
                Debug.Log("carnet " + k + " " + Island.FullName(cm) + " cara " + Island.FaceOf(cm) + " " + Island.Hometown(cm));
            }
            g.Ui.CloseMinerCard();
            // 4) el Cuartel: catalogo de una parcela libre, obra, revelacion
            isl.Coins = System.Math.Max(isl.Coins, 5000);
            for (int i = 0; i < isl.Stock.Length; i++) isl.Stock[i] = System.Math.Max(isl.Stock[i], 200);
            if (isl.Level(BKind.Depot) < 2) { isl.Coins += isl.UpgradeCost(isl.Plots[0]); isl.Upgrade(isl.Plots[0]); isl.FinishAllWork(); }
            for (int f = 0; f < 20; f++) yield return null;
            var bpl = isl.Plots.Find(p => isl.Allowed(BKind.Barracks, p));
            if (bpl != null)
            {
                g.Ui.OpenPlot(bpl);
                for (int f = 0; f < 30; f++) yield return null;
                Snap("rec_10_catalogo");
                g.Ui.DebugCatalogTab(1);
                for (int f = 0; f < 20; f++) yield return null;
                Snap("rec_10b_catalogo_pestana");
                g.Ui.CloseSheet();
                isl.Coins += isl.BuildCost(BKind.Barracks);
                isl.Build(BKind.Barracks, bpl);
                g.DebugLook(g.PlotWorld(bpl), 11f);
                for (int f = 0; f < 20; f++) yield return null;
                Snap("rec_11_obra_cuartel");
                isl.FinishAllWork();
                for (int f = 0; f < 30; f++) { g.DebugLook(g.PlotWorld(bpl), 11f); yield return null; }
                Snap("rec_12_cuartel_listo");
            }
            Log("cuartel cuartel=" + (isl.Find(BKind.Barracks) != null));
            var bp = isl.Find(BKind.Barracks);
            if (bp != null)
            {
                Vector3 c = new Vector3(bp.X, 0f, bp.Z);
                g.EnterComplex();
                for (int f = 0; f < 30; f++) yield return null;
                Snap("rec_13_modo_cuartel");
                isl.Coins += 5000;
                if (g.BeginModPlace(ModKind.Dorm, 1))
                {
                    for (int f = 0; f < 8; f++) yield return null;
                    g.DebugModAt(0, -2);
                    for (int f = 0; f < 10; f++) yield return null;
                    Snap("rec_14_habitacion_arrastre");
                    g.DebugModAt(0, -1);
                    for (int f = 0; f < 10; f++) yield return null;
                    // construir con momento: paso fijo de 1/30 s para que cada foto caiga en su instante
                    Time.captureDeltaTime = 1f / 30f;
                    g.ConfirmPlace();
                    int fr = 0;
                    foreach (var kv in new[] { new { F = 9, N = "rec_15a_arma_piso" }, new { F = 17, N = "rec_15b_arma_paredes" }, new { F = 30, N = "rec_15c_arma_techo_cae" }, new { F = 38, N = "rec_15d_arma_techo_golpe" }, new { F = 75, N = "rec_16_habitacion_lista" } })
                    {
                        for (; fr < kv.F; fr++) { g.DebugLook(c, 8.4f); yield return null; }
                        Snap(kv.N);
                    }
                    Time.captureDeltaTime = 0f;
                }
                else Debug.Log("reccheck habitacion NO se pudo empezar a colocar");
                g.ExitComplex();
                for (int f = 0; f < 10; f++) yield return null;
                foreach (var z in new[] { 15f, 9f, 6f })
                {
                    yield return RoofSettle(g, c, z);
                    Snap("rec_17_cuartel_z" + z.ToString("0"));
                }
            }
            Log("habitacion modulos=" + isl.Modules.Count);
            // 5) expandir la isla
            g.DebugLook(Vector3.zero, 15f);
            for (int i = 0; i < 6 && isl.Level(BKind.Depot) < Island.ExpandTh[0]; i++) { isl.Coins += isl.UpgradeCost(isl.Plots[0]); if (!isl.Upgrade(isl.Plots[0])) break; isl.FinishAllWork(); }
            isl.Coins += Island.ExpandCost[0] * 2;
            for (int f = 0; f < 20; f++) yield return null;
            Snap("rec_18_antes_expandir");
            bool ex = isl.DoExpand();
            for (int f = 0; f < 50; f++) yield return null;
            Snap("rec_19_expandida");
            Log("expandir ok=" + ex);
            // el hallazgo de la tierra nueva (Island.Discovered, ~3.5 s despues)
            for (int f = 0; f < 60 && isl.Giant == null; f++) yield return null;
            var hz = isl.Giant;
            if (hz != null) for (int f = 0; f < 20; f++) { g.DebugLook(new Vector3(hz.X, 0f, hz.Z), 10f); yield return null; }
            Snap("rec_19b_hallazgo");
            Log("hallazgo legendario=" + (hz != null && hz.Legendary));
            // 6) menu, tienda, diario, album
            g.Ui.OpenMenu(); for (int f = 0; f < 30; f++) yield return null; Snap("rec_20_menu"); g.Ui.CloseSheet();
            for (int f = 0; f < 10; f++) yield return null;
            g.Ui.OpenShop(); for (int f = 0; f < 30; f++) yield return null; Snap("rec_21_tienda"); g.Ui.CloseSheet();
            for (int f = 0; f < 10; f++) yield return null;
            g.Ui.OpenDaily(); for (int f = 0; f < 30; f++) yield return null; Snap("rec_22_diario"); g.Ui.CloseSheet();
            for (int f = 0; f < 10; f++) yield return null;
            g.Ui.OpenAlbum(); for (int f = 0; f < 30; f++) yield return null; Snap("rec_23_album"); g.Ui.CloseSheet();
            for (int f = 0; f < 10; f++) yield return null;
            if (isl.DecorSlots.Count > 0) { g.Ui.OpenDecorPicker(0); for (int f = 0; f < 30; f++) yield return null; Snap("rec_23b_decorar_oferta"); g.Ui.CloseSheet(); }
            Debug.Log("reccheck oferta adorno=" + isl.DecorDeal);
            for (int f = 0; f < 10; f++) yield return null;
            // 7) noche y un evento (veta gigante)
            isl.DayClock = Island.DayLength * 0.78f;
            g.DebugLook(Vector3.zero, 12f);
            for (int f = 0; f < 40; f++) yield return null;
            Snap("rec_24_noche");
            isl.DayClock = Island.DayLength * 0.25f;
            isl.SpawnGiant();
            for (int f = 0; f < 60; f++) yield return null;
            Snap("rec_25_evento_gigante");
            // jackpot: Yacimiento legendario (anuncio, y romperlo)
            var gi = isl.Giant; if (gi != null) { gi.Hp = 0.5; isl.TapOre(gi, 0); }   // la de antes se rompe (no queda colgada en la vista)
            for (int f = 0; f < 20; f++) yield return null;
            var leg = isl.SpawnGiant(false, false, true);
            Vector3 lp = leg != null ? new Vector3(leg.X, 0f, leg.Z) : Vector3.zero;
            for (int f = 0; f < 12; f++) { g.DebugLook(lp, 9f); yield return null; }
            Snap("rec_26_legendario");
            if (leg != null)
            {
                for (int f = 0; f < 40; f++) { g.DebugLook(lp, 9f); yield return null; }
                Snap("rec_26b_legendario_cerca");
                leg.Hp = 0.5;
                isl.TapOre(leg, 0);
                for (int f = 0; f < 6; f++) { g.DebugLook(lp, 9f); yield return null; }
                Snap("rec_27_legendario_rompe");
                for (int f = 0; f < 30; f++) yield return null;
                Snap("rec_28_legendario_premio");
            }
            Log("legendario stat=" + isl.Stat("legendary"));
            // avisos locales que se programarian al salir ahora (con una obra larga en curso)
            var hp = isl.Find(BKind.House);
            if (hp != null) { isl.Coins += isl.UpgradeCost(hp); for (int i = 0; i < isl.Stock.Length; i++) isl.Stock[i] = System.Math.Max(isl.Stock[i], 300); isl.Upgrade(hp); }
            double ws, cs; string wt, ct;
            Mineros.IslandView.IslandNotify.Plan(isl, out ws, out wt, out cs, out ct);
            Debug.Log("avisocheck obra=" + ws.ToString("0") + "s \"" + wt + "\" cofre=" + cs.ToString("0") + "s \"" + ct + "\"");
            Log("fin");
        }

        IEnumerator RunIsland()
        {
            var g = Mineros.IslandView.IslandGame.I;
            if (scen == "isla_ciudad") { yield return RunCity(g); yield break; }
            if (scen == "isla_tutorial") { yield return RunTutorial(g); yield break; }
            if (scen == "isla_recorrido") { yield return RunTutorial(g); yield return RunTour(g); yield break; }
            if (scen == "isla_cuartel") { yield return RunBarracks(g); yield break; }
            if (scen == "isla_kit")
            {
                var bpk = g.Isl.Find(BKind.Barracks);
                Vector3 ck = new Vector3(bpk.X, 0f, bpk.Z);
                yield return Wait(2f);
                yield return RoofSettle(g, ck, 15f); Snap("kit_isla");
                // techos que se desvanecen: a medio camino (tramado + brillo) y del todo (se ve adentro). En la nube cada
                // cuadro tarda y el dt del juego esta topeado: se espera a que el techo termine de moverse.
                var dorm = g.Isl.Modules.Find(x => x.Kind == ModKind.Dorm && x.Ch == 1);
                Vector3 cd = dorm != null ? g.ModWorld(dorm) : ck;
                yield return RoofSettle(g, cd, 9f); Snap("kit_z9");
                yield return RoofSettle(g, cd, 8f); Snap("kit_z8");
                Debug.Log("kitcheck techo_z8=" + g.RoofCutAmount.ToString("0.00"));
                yield return RoofSettle(g, cd, 6f); Snap("kit_cerca");
                Debug.Log("kitcheck techo_ido=" + g.RoofCutAmount.ToString("0.00"));
                // tocar a traves del techo abierto: elige el dormitorio (Modo Cuartel con la habitacion marcada)
                if (dorm != null)
                {
                    g.DebugTap(g.Cam.WorldToScreenPoint(g.ModWorld(dorm) + Vector3.up * 0.6f));
                    yield return Wait(1.2f); Snap("kit_toque");
                    Debug.Log("kitcheck toque modo=" + g.ComplexMode + " elegido=" + (g.SelectedMod != null ? g.SelectedMod.Kind + "/" + g.SelectedMod.Ch : "nada"));
                    g.ExitComplex(); yield return Wait(0.5f);
                }
                yield return RoofSettle(g, cd, 12f); Snap("kit_techo_vuelve");
                Debug.Log("kitcheck techo_vuelve=" + g.RoofCutAmount.ToString("0.00"));
                // los otros temas del kit: oro (Tesoreria) y piedra (Trituradora), techados a 9 y abiertos a 6
                foreach (var kv in new[] { new { K = ModKind.Treasury, N = "oro" }, new { K = ModKind.Crusher, N = "piedra" }, new { K = ModKind.Lab, N = "cristal" }, new { K = ModKind.Tools, N = "hierro" } })
                {
                    var md = g.Isl.FirstMod(kv.K);
                    if (md == null) continue;
                    Vector3 cm = g.ModWorld(md);
                    yield return RoofSettle(g, cm, 9f); Snap("kit_" + kv.N + "_z9");
                    yield return RoofSettle(g, cm, 6f); Snap("kit_" + kv.N + "_z6");
                    Debug.Log("kitcheck " + kv.N + " remate=" + Mineros.IslandView.RoomKit.Piece(md, "remate") + " pared=" + Mineros.IslandView.RoomKit.Piece(md, "pared"));
                }
                yield return RoofSettle(g, ck, 12f);
                g.EnterComplex(); yield return Wait(1.2f); Snap("kit_modo");
                Debug.Log("kitcheck remate=" + Mineros.IslandView.RoomKit.Piece(g.Isl.Modules.Find(x => x.Kind == ModKind.Dorm && x.Ch == 1), "remate") + " comun=" + Mineros.IslandView.RoomKit.Common("techo_0"));
                // construir con momento visto desde afuera: dormitorio nuevo en la celda libre del frente, techo cayendo
                bool began = false;
                foreach (int ch in new[] { 5, 6, 7, 8 }) if (!began && g.Isl.RoomState(ch) == 1) began = g.BeginModPlace(ModKind.Dorm, ch);
                if (began)
                {
                    yield return Wait(0.4f);
                    g.DebugModAt(0, -1);
                    yield return Wait(0.6f);
                    Time.captureDeltaTime = 1f / 30f;
                    g.ConfirmPlace();
                    g.ExitComplex();
                    Vector3 cn = g.CellWorld(0, -1, 0);
                    int fr = 0;
                    foreach (var kv in new[] { new { F = 9, N = "kit_arma_a_piso" }, new { F = 15, N = "kit_arma_b_paredes" }, new { F = 21, N = "kit_arma_c_paredes" }, new { F = 30, N = "kit_arma_d_techo_cae" }, new { F = 37, N = "kit_arma_e_golpe" }, new { F = 70, N = "kit_arma_f_listo" } })
                    {
                        for (; fr < kv.F; fr++) { g.DebugLook(cn, 9f); yield return null; }
                        Snap(kv.N);
                    }
                    Time.captureDeltaTime = 0f;
                    Debug.Log("kitcheck armado modulos=" + g.Isl.Modules.Count);
                }
                else Debug.Log("kitcheck armado SIN dormitorio disponible");
                // mejorar con escena: la Trituradora sube de etapa (andamio + martillazos), y termina (ta-da)
                var crm = g.Isl.FirstMod(ModKind.Crusher);
                if (crm != null && crm.Stage < 3 && g.Isl.Evolve(crm))
                {
                    Vector3 cc = g.ModWorld(crm);
                    g.ExitComplex();
                    for (int f = 0; f < 30; f++) { g.DebugLook(cc, 10f); yield return null; }
                    Snap("kit_obra_andamio");
                    crm.Work = 0.01;
                    for (int f = 0; f < 8; f++)
                    {
                        float tf = Time.realtimeSinceStartup;
                        g.DebugLook(cc, 10f); yield return null;
                        Debug.Log("kitcheck cuadro " + f + " " + ((Time.realtimeSinceStartup - tf) * 1000f).ToString("0") + "ms work=" + crm.Work.ToString("0.000") + " etapa=" + crm.Stage);
                    }
                    Snap("kit_obra_lista");
                    Debug.Log("kitcheck etapa trituradora=" + crm.Stage);
                }
                // Plan Pueblo: Duchas como habitacion, con cola en la puerta
                Module bath = null;
                // en planta baja (la cola se arma en el suelo, frente a su puerta)
                for (int pass = 0; pass < 2 && bath == null; pass++)
                    foreach (var cell in g.Isl.FreeCells(ModKind.Bath)) { if (pass == 0 && cell[2] != 0) continue; bath = g.Isl.BuyModule(ModKind.Bath, cell[0], cell[1], cell[2], -1); if (bath != null) break; }
                if (bath != null)
                {
                    bath.Work = 0;
                    int dirty = 0;
                    foreach (var mm in g.Isl.Miners) if (!mm.InComplex && dirty < 4) { mm.Clean = 5f; mm.State = MState.Idle; dirty++; }
                    Vector3 bw = g.ModWorld(bath);
                    for (int f = 0; f < 120; f++) { g.DebugLook(bw, 8f); yield return null; }
                    // encuadre: entre la habitacion y la fila (no el techo)
                    Vector3 qc = bw; int qn = 1;
                    foreach (var mm in g.Isl.Miners) if (mm.State == MState.Queued || mm.State == MState.InRoom || mm.State == MState.ToRoom) { qc += g.MinerWorld(mm); qn++; }
                    qc /= qn;
                    for (int f = 0; f < 20; f++) { g.DebugLook(qc, 6.5f); yield return null; }
                    Snap("kit_duchas_cola");
                    int q = 0, inn = 0; foreach (var mm in g.Isl.Miners) { if (mm.State == MState.Queued) q++; if (mm.State == MState.InRoom || mm.State == MState.ToRoom) inn++; }
                    Debug.Log("kitcheck duchas cola=" + q + " adentro=" + inn + " piso=" + bath.F);
                }
                else Debug.Log("kitcheck duchas SIN lugar");
                yield break;
            }
            if (scen == "isla_rig") { yield return RunRig(g); yield break; }
            yield return Wait(4f);
            Snap(scen + "_a");
            yield return Wait(5f);
            Snap(scen + "_b");
            if (scen == "isla_media")
            {
                g.Ui.OpenPlot(g.Isl.Plots[5]);
                yield return Wait(0.8f);
                Snap("isla_construir");
                g.Ui.CloseSheet();
                g.Ui.OpenPlot(g.Isl.Find(BKind.House));
                yield return Wait(0.8f);
                Snap("isla_mejorar");
                g.Ui.CloseSheet();
                g.Isl.Coins = 1e6;
                Rich(g.Isl, 4);
                g.Isl.Upgrade(g.Isl.Find(BKind.House));
                yield return Wait(0.5f);
                Snap("isla_obra");
                g.Isl.FinishAllWork();
                yield return Wait(2f);
                Snap("isla_nuevo_minero");
            }
            if (scen == "isla_eventos")
            {
                g.Isl.SpawnGiant();
                g.Isl.ShipSoon();
                yield return Wait(8f);
                Snap("isla_eventos_gigante");
                // arrastre: el punto del suelo tocado debe quedar bajo el dedo (antes estaba invertido)
                var c = new Vector2(g.Cam.pixelWidth * 0.5f, g.Cam.pixelHeight * 0.5f);
                Vector3 before = g.ScreenToGround(c);
                float err = g.DebugDrag(c, c + new Vector2(0f, -g.Cam.pixelHeight * 0.2f));
                Vector3 after = g.ScreenToGround(c);
                Debug.Log("dragcheck err=" + err.ToString("0.000") + " groundMoved=" + (after - before));
                yield return Wait(0.5f);
                Snap("isla_drag");
                g.DebugDrag(c + new Vector2(0f, -g.Cam.pixelHeight * 0.2f), c);
                g.Ui.ShowMiner(g.Isl.Miners[0]);
                yield return Wait(0.8f);
                Snap("isla_minero");
                g.Ui.CloseSheet();
                g.Ui.OpenSettings();
                yield return Wait(0.8f);
                Snap("isla_ajustes");
                g.Ui.CloseSheet();
                g.Isl.Gems = 12;
                g.Isl.BuyTurbo();
                yield return Wait(1.5f);
                Snap("isla_turbo");
                Debug.Log("islacheck giant=" + (g.Isl.Giant != null) + " ship=" + (g.Isl.CurShip != null) + " gems=" + g.Isl.Gems);
            }
            if (scen == "isla_show")
            {
                var isl = g.Isl;
                foreach (var bk in new[] { BKind.House, BKind.Mine, BKind.Lighthouse })
                {
                    var spr = Mineros.IslandView.IslandStage.I.BuildingIcon(bk, 1);
                    var tmp = RenderTexture.GetTemporary(spr.texture.width, spr.texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    Graphics.Blit(spr.texture, tmp);
                    var prevRt = RenderTexture.active; RenderTexture.active = tmp;
                    var rd = new Texture2D(tmp.width, tmp.height, TextureFormat.RGBA32, false);
                    rd.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0); rd.Apply();
                    RenderTexture.active = prevRt; RenderTexture.ReleaseTemporary(tmp);
                    File.WriteAllBytes(Path.Combine(outDir, "icono_" + bk + ".png"), rd.EncodeToPNG());
                }
                Mineros.Core.Plot free = null;
                foreach (var p in isl.Plots) if (isl.Offered(p)) { free = p; break; }
                Rich(isl, 4);
                isl.Build(BKind.Mine, free);
                yield return Wait(0.7f); Snap("show_construir_a");
                if (free != null) free.Work = 0.01;   // la obra termina: revelacion
                yield return Wait(1.5f); Snap("show_construir_b");
                yield return Wait(0.6f); Snap("show_construir_c");
                yield return Wait(2.5f);
                isl.Upgrade(isl.Find(BKind.House));
                yield return Wait(1.1f); Snap("show_evolucion_a");
                yield return Wait(0.6f); Snap("show_evolucion_b");
                yield return Wait(1.4f); Snap("show_evolucion_c");
                yield return Wait(3f);
                isl.BalloonSoon();
                yield return Wait(6.5f); Snap("show_globo");
                isl.TapBalloon();
                g.Ui.OpenWheel();
                yield return Wait(0.8f); Snap("show_ruleta_a");
                g.Ui.DebugPress();
                yield return Wait(1.2f); Snap("show_ruleta_b");
                yield return Wait(4.2f); Snap("show_ruleta_c");
                g.Ui.CloseSheet();
                isl.GiveChest(2);
                yield return Wait(0.5f);
                g.Ui.OpenChestUi();
                yield return Wait(1.8f); Snap("show_cofre_a");
                g.Ui.DebugPress();
                yield return Wait(1.6f); Snap("show_cofre_b");
                yield return Wait(5f); Snap("show_cofre_c");
                g.Ui.CloseSheet();
                isl.Spins = 1; isl.PendingPrize = 5; isl.ClaimSpin();
                yield return Wait(2.5f); Snap("show_cielo_a");
                yield return Wait(5f); Snap("show_cielo_b");
                Debug.Log("showcheck tier=" + Island.Tier(isl.Find(BKind.House).Level) + " chests=" + isl.ChestCount + " giant=" + (isl.Giant != null));
            }
            if (scen == "isla_mineros")
            {
                var isl = g.Isl;
                isl.Coins = 1e6;
                Rich(isl, 4);
                isl.Upgrade(isl.Find(BKind.House));
                isl.FinishAllWork();
                // forzar un dorado entre los candidatos para ver su escena
                yield return Wait(0.5f);
                if (isl.Recruits.Count == 3) { isl.Recruits[2] = 6; isl.RecruitGolden[2] = true; isl.Recruits[1] = 4; }
                yield return Wait(2.5f);
                Snap("mineros_barco");
                yield return Wait(3f);
                Snap("mineros_cartas");
                // la hoja del barco puede tardar (cola de avisos): esperar a que este para dar vuelta las cartas
                for (int k = 0; k < 40 && !g.Ui.DebugFlip(0); k++) yield return Wait(0.5f);
                yield return Wait(0.6f);
                Snap("mineros_cartas_frente");
                g.Ui.DebugFlip(1); yield return Wait(1.3f);
                Snap("mineros_cartas_rara");
                g.Ui.DebugFlip(2); yield return Wait(1.0f);
                Snap("mineros_dorado_suspenso");
                for (int f = 0; f < 90; f++) yield return null;   // las animaciones de UI avanzan por cuadro (tope 0.1 s)
                yield return Wait(1f);
                Snap("mineros_dorado");
                // carta en grande (tocar la dada vuelta) y de vuelta a su lugar
                g.Ui.DebugBig(2);
                for (int f = 0; f < 20; f++) yield return null;
                yield return Wait(0.5f);
                Snap("mineros_grande");
                g.Ui.DebugBig(2);
                for (int f = 0; f < 20; f++) yield return null;
                // invocacion: elegir la dorada, la carta va a la mano, se arrastra y se suelta sobre la isla
                g.Ui.DebugPick(2);
                for (int f = 0; f < 40; f++) yield return null;
                yield return Wait(0.6f);
                Snap("mineros_mano");
                Vector3 drop = g.FreeSummonPoint(g.ScreenToGround(new Vector2(g.Cam.pixelWidth * 0.55f, g.Cam.pixelHeight * 0.45f)));
                g.Ui.DebugHold(drop);
                for (int f = 0; f < 12; f++) yield return null;
                Snap("mineros_arrastre");
                g.Ui.DebugDrop(drop);
                for (int f = 0; f < 4; f++) yield return null;
                Snap("mineros_cae");
                yield return Wait(0.45f);
                Snap("mineros_golpe");
                yield return Wait(0.5f);
                Snap("mineros_luz");
                yield return Wait(0.35f);
                Snap("mineros_sale");
                yield return Wait(1.6f);
                Snap("mineros_llega");
                yield return Wait(3f);
                Snap("mineros_presentado");
                Miner golden = null;
                foreach (var mm in g.Isl.Miners) if (mm.Golden) golden = mm;
                Debug.Log("cartacheck dorado=" + (golden != null) + " tex=" + (Mineros.IslandView.IslandUi.CardTex(6, true) != null));
                g.Ui.OpenAlbum();
                yield return Wait(4f);
                Snap("mineros_album");
                g.Ui.CloseSheet();
                if (golden != null) { golden.Level = 4; g.Ui.ShowMiner(golden); }
                yield return Wait(0.8f);
                Snap("mineros_tarjeta");
                g.Ui.CloseSheet();
                // estados: uno duerme, otro come, uno cansado y sucio
                var ms = isl.Miners;
                ms[0].Energy = 10f; ms[0].State = MState.Resting;
                if (ms.Count > 1) { ms[1].Clean = 10f; }
                isl.DayClock = Island.DayLength * 0.78f;
                g.DebugLook(new Vector3(ms[0].X, 0f, ms[0].Z), 7.5f);
                yield return Wait(3f);
                Snap("mineros_noche_estados");
                Debug.Log("minerocheck mineros=" + ms.Count + " album=" + isl.FoundCount + " dorados=" + isl.Stat("golden") + " candidatos=" + isl.Recruits.Count);
            }
            if (scen == "isla_progreso")
            {
                var isl = g.Isl;
                isl.Coins = 1e9;
                g.DebugLook(g.WonderWorld, 9f);
                yield return Wait(1.5f);
                Snap("prog_ruinas");
                for (int ph = 1; ph <= 5; ph++)
                {
                    isl.BuildWonder();
                    for (int f = 0; f < 60; f++) yield return null;
                    yield return Wait(ph == 5 ? 2.5f : 0.3f);
                    if (ph == 1 || ph == 3 || ph == 5) Snap("prog_maravilla_" + ph);
                    g.Ui.CloseSheet();
                }
                // decorar
                g.DebugLook(Vector3.zero, 12f);
                g.DecorMode = true;
                yield return Wait(1f);
                Snap("prog_decorar");
                for (int i = 0; i < isl.DecorSlots.Count && i < 6; i++) isl.PlaceDecor(i, i % 6);
                yield return Wait(1.5f);
                g.DecorMode = false;
                Snap("prog_decorado");
                // golem
                isl.BossSoon();
                yield return Wait(0.5f);
                for (int f = 0; f < 50; f++) yield return null;
                Snap("prog_golem");
                var boss = isl.Boss;
                if (boss != null)
                {
                    for (int i = 0; i < 4000 && boss.Hp > boss.MaxHp * 0.6; i++) isl.TapOre(boss, 5);
                    for (int f = 0; f < 15; f++) yield return null;
                    Snap("prog_golem_enojado");
                    for (int i = 0; i < 9000 && !boss.Dead; i++) isl.TapOre(boss, 5);
                    for (int f = 0; f < 12; f++) yield return null;
                    Snap("prog_golem_cae");
                }
                yield return Wait(2f);
                g.Ui.CloseSheet();
                // pase y museo
                isl.AddXp(560);
                g.Ui.OpenPass();
                for (int f = 0; f < 30; f++) yield return null;
                Snap("prog_pase");
                g.Ui.CloseSheet();
                for (int i = 0; i < 7; i++) isl.RollPiece(1.0);
                g.Ui.OpenMuseum();
                for (int f = 0; f < 40; f++) yield return null;
                Snap("prog_museo");
                g.Ui.CloseSheet();
                // zarpar
                g.Ui.OpenSail();
                for (int f = 0; f < 20; f++) yield return null;
                Snap("prog_zarpar");
                g.Ui.DebugPress();
                for (int f = 0; f < 35; f++) yield return null;
                Snap("prog_viaje");
                for (int f = 0; f < 200 && Mineros.IslandView.IslandGame.I == g; f++) yield return null;
                yield return Wait(3f);
                var g2 = Mineros.IslandView.IslandGame.I;
                if (g2 != null) { Snap("prog_isla_nueva"); Debug.Log("progcheck isla=" + g2.Isl.IslandNo + " reliquias=" + g2.Isl.Relics + " biome=" + g2.Isl.Biome + " mineros=" + g2.Isl.Miners.Count + " banco=" + g2.Isl.Bench.Count); }
                else Debug.Log("progcheck sin isla nueva");
            }
            if (scen == "isla_diario")
            {
                var isl = g.Isl;
                yield return null;
                Snap("diario_regreso_cuenta");
                for (int f = 0; f < 40; f++) yield return null;
                Snap("diario_regreso");
                g.Ui.DebugPress();
                for (int f = 0; f < 40; f++) yield return null;
                Snap("diario_hoja");
                g.Ui.DebugPress();
                for (int f = 0; f < 30; f++) yield return null;
                Snap("diario_cobrado");
                g.Ui.CloseSheet();
                // una mision lista y un pedido listo
                var m0 = isl.Missions[0];
                isl.Stats[m0.Stat] = isl.Stat(m0.Stat) + m0.Target;
                g.Ui.OpenDaily();
                for (int f = 0; f < 20; f++) yield return null;
                Snap("diario_mision_lista");
                g.Ui.CloseSheet();
                yield return Wait(1f);
                if (isl.Orders.Count > 0) { isl.Orders[0].Wait = 0f; isl.Orders[0].Delivered = isl.Orders[0].Count; }
                g.DebugLook(g.BoardWorld, 7f);
                yield return Wait(1.5f);
                Snap("diario_tablon");
                g.Ui.OpenBoard();
                for (int f = 0; f < 25; f++) yield return null;
                Snap("diario_tablon_hoja");
                Debug.Log("diariocheck racha=" + isl.Streak + " misiones=" + isl.Missions.Count + " pedidos=" + isl.Orders.Count + " gemas=" + isl.Gems);
            }
            if (scen == "isla_sorpresas")
            {
                var isl = g.Isl;
                var st = Mineros.IslandView.IslandStage.I;
                // cofre de madera que sube hasta legendario
                isl.GiveChest(0);
                yield return Wait(0.3f);
                g.Ui.OpenChestUi();
                for (int f = 0; f < 30; f++) yield return null;
                Snap("sorpresa_cofre_madera");
                isl.OpenTier = 2; st.UpgradeChest(2);
                for (int f = 0; f < 4; f++) yield return null;
                Snap("sorpresa_cofre_sube");
                for (int f = 0; f < 20; f++) yield return null;
                isl.OpenTier = 3; st.UpgradeChest(3);
                for (int f = 0; f < 25; f++) yield return null;
                Snap("sorpresa_cofre_legendario");
                g.Ui.DebugPress();
                for (int f = 0; f < 60; f++) yield return null;
                Snap("sorpresa_cofre_botin");
                g.Ui.CloseSheet();
                // ruleta: frena en el borde del premio grande y cae al lado
                Mineros.IslandView.IslandStage.ForceNear = true;
                isl.Spins = 1; isl.PendingPrize = 4;
                g.Ui.OpenWheel();
                yield return Wait(0.5f);
                g.Ui.DebugPress();
                for (int f = 0; f < 200 && st.Spinning; f++) yield return null;
                yield return null;
                Snap("sorpresa_ruleta_casi");
                for (int f = 0; f < 40; f++) yield return null;
                Snap("sorpresa_ruleta_premio");
                g.Ui.CloseSheet();
                // botella con mapa
                isl.BottleSoon();
                yield return Wait(4f);
                if (isl.CurBottle != null) g.DebugLook(new Vector3(isl.CurBottle.X, 0f, isl.CurBottle.Z) * 0.8f, 9f);
                yield return Wait(1f);
                Snap("sorpresa_botella");
                isl.OpenBottle();
                for (int f = 0; f < 40; f++) yield return null;
                Snap("sorpresa_mapa");
                g.Ui.CloseSheet();
                if (isl.CurBottle != null) g.DebugLook(new Vector3(isl.CurBottle.TX, 0f, isl.CurBottle.TZ), 8f);
                for (int f = 0; f < 200 && isl.CurBottle != null; f++) { yield return Wait(0.1f); if (f == 40) Snap("sorpresa_cava"); }
                yield return Wait(0.4f);
                Snap("sorpresa_tesoro");
                // bichos
                g.DebugLook(Vector3.zero, 12f);
                foreach (var k in new[] { CritterKind.Crab, CritterKind.Gull, CritterKind.Mole, CritterKind.Butterfly })
                {
                    var c = isl.SpawnCritter(k);
                    g.DebugLook(new Vector3(c.X, 0f, c.Z), 7f);
                    yield return Wait(k == CritterKind.Gull ? 1.6f : 1.2f);
                    Snap("sorpresa_bicho_" + k);
                    isl.CatchCritter();
                    yield return Wait(0.3f);
                }
                Debug.Log("sorpresacheck cofres=" + isl.ChestCount + " tesoros=" + isl.Stat("treasures") + " bichos=" + isl.Stat("critters"));
            }
            if (scen == "isla_mundo")
            {
                var isl = g.Isl;
                isl.DayClock = Island.DayLength * 0.665f;
                yield return Wait(2.5f);
                Snap("mundo_atardecer");
                isl.DayClock = Island.DayLength * 0.74f;
                yield return Wait(2f);
                Snap("mundo_noche_a");
                isl.SpawnCrystal(); isl.SpawnCrystal();
                yield return Wait(5f);
                Snap("mundo_noche_b");
                g.DebugLook(Vector3.zero, Mineros.IslandView.IslandGame.ZoomMin + 1f);
                yield return Wait(1.5f);
                Snap("mundo_noche_cerca");
                g.DebugLook(Vector3.zero, 14f);
                isl.DayClock = Island.DayLength * 0.2f;
                isl.StartWeather(Weather.Rain);
                yield return Wait(9f);
                Snap("mundo_lluvia");
                isl.StartWeather(Weather.Storm);
                yield return Wait(4.2f);
                Snap("mundo_tormenta");
                yield return Wait(0.6f);
                isl.Strike();
                yield return Wait(0.08f);
                Snap("mundo_rayo");
                isl.WeatherLeft = 0.1f;
                yield return Wait(4f);
                Snap("mundo_arcoiris");
                isl.StartWeather(Weather.Meteors);
                yield return Wait(4f);
                Snap("mundo_meteoritos");
                Debug.Log("mundocheck crystals=" + isl.Stat("crystals") + " rayos=" + isl.Stat("lightning") + " arcoiris=" + isl.RainbowT.ToString("0") + " clima=" + isl.Sky);
            }
            if (scen == "isla_picar")
            {
                var isl = g.Isl;
                // la veta mas cercana al centro, con mucha vida para que aguante el combo
                Ore pick = null; float bd = 1e9f;
                foreach (var o in isl.OreList) { if (o.Dead || o.Giant) continue; float d = o.X * o.X + o.Z * o.Z; if (d < bd) { bd = d; pick = o; } }
                g.DebugLook(new Vector3(pick.X, 0f, pick.Z), 8f);
                pick.Hp = pick.MaxHp = 1e6;
                for (int i = 0; i < 4; i++) { g.DebugTapOre(pick); yield return Wait(0.12f); }
                Snap("picar_toque");
                for (int i = 0; i < 6; i++) { g.DebugTapOre(pick); yield return Wait(0.1f); }
                yield return Wait(0.15f);
                Snap("picar_combo10");
                for (int i = 0; i < 20; i++) { g.DebugTapOre(pick); yield return Wait(0.08f); }
                yield return Wait(0.35f);
                Snap("picar_frenesi");
                yield return Wait(1.2f);
                Snap("picar_frenesi_b");
                pick.Hp = 0.5;
                g.DebugTapOre(pick);
                yield return Wait(0.12f);
                Snap("picar_rompe_a");
                yield return Wait(0.35f);
                Snap("picar_rompe_b");
                yield return Wait(3f);
                Snap("picar_brote");
                var fresh = isl.SpawnOre(false, 3);
                if (fresh != null) g.DebugLook(new Vector3(fresh.X, 0f, fresh.Z), 7f);
                yield return Wait(0.18f);
                Snap("picar_nace_a");
                yield return Wait(0.3f);
                Snap("picar_nace_b");
                yield return Wait(0.6f);
                Snap("picar_nace_c");
                Debug.Log("picarcheck crits=" + isl.Stat("crits") + " frenesi=" + isl.Stat("frenzies") + " taps=" + isl.Stat("taps"));
            }
            if (scen == "isla_audio")
            {
                // capas de sonido: lejos del mar / zoom a la orilla
                var A = typeof(Mineros.Audio.Sfx);
                string[] names = { "pick", "coin", "goal", "waves", "gull", "wheel", "chest", "meteor", "horn", "upgrade", "combo" };
                var sb = new System.Text.StringBuilder("audiocheck banks=" + Mineros.Audio.Sfx.BankCount);
                foreach (var n in names) sb.Append(" " + n + "=" + Mineros.Audio.Sfx.HasRecorded(n));
                Debug.Log(sb.ToString());
                string Lv() { return "far=" + Mineros.Audio.Sfx.LoopLevel("amb_sea_far").ToString("0.00") + " near=" + Mineros.Audio.Sfx.LoopLevel("amb_sea_near").ToString("0.00") + " birds=" + Mineros.Audio.Sfx.LoopLevel("amb_birds").ToString("0.00") + " wind=" + Mineros.Audio.Sfx.LoopLevel("amb_wind").ToString("0.00") + " music=" + Mineros.Audio.Sfx.Track; }
                Debug.Log("audiocheck centro " + Lv());
                g.DebugLook(new Vector3(0f, 0f, -(g.Isl.Radius + 1f)), Mineros.IslandView.IslandGame.ZoomMin);
                yield return Wait(4f);
                Debug.Log("audiocheck orilla_cerca " + Lv());
                Snap("audio_orilla");
                g.DebugLook(Vector3.zero, Mineros.IslandView.IslandGame.ZoomMax);
                yield return Wait(4f);
                Debug.Log("audiocheck lejos " + Lv());
                g.Sound.Night = 1f;
                yield return Wait(6f);
                Debug.Log("audiocheck noche " + Lv() + " grillos=" + Mineros.Audio.Sfx.LoopLevel("amb_crickets").ToString("0.00"));
            }
            if (scen == "isla_offline") Snap("isla_bienvenida");
            if (scen == "isla_inicio")
            {
                yield return Wait(30f);
                Snap("isla_inicio_30s");
                Debug.Log("islacheck coins=" + g.Isl.Coins + " earned=" + g.Isl.TotalEarned + " mineros=" + g.Isl.Miners.Count);
            }
        }

        // ------------------------------------------------------------ estado inicial por escenario
        void Configure(GameState g)
        {
            g.ResetAll();
            g.LoginDate = g.Today();
            g.PendingOffline = 0;
            switch (scen)
            {
                case "rich":
                    g.Gold = 5.0e6; g.Gems = 1500;
                    Lv(g, 40, 40, 39);
                    g.World = 1; g.Sub = 4; g.Progress = 0.3;
                    Equip(g, 0, 1, 1, 0, 0, 0);
                    g.Inv[0] = new Tool(0, 0); g.Inv[1] = new Tool(0, 0);
                    break;
                case "b0": case "b1": case "b2": case "b3":
                    g.Gold = 50; g.World = scen[1] - '0' + 1;
                    Equip(g, 0, 1, 1, 0, 0, 0);
                    Lv(g, 30 + 12 * (g.World - 1), 20, 20);
                    break;
                case "ev_gold_rush": case "ev_frenzy": case "ev_meteor": case "ev_chest":
                case "milestone": case "goal": case "unlocked": case "panels": case "drag":
                    Meta(g, 2, 3);
                    if (scen != "unlocked") g.UnlockAll();
                    if (scen == "goal") g.Lv["power"] = 2;
                    if (scen == "drag") { g.Inv[0] = new Tool(1, 0); g.Inv[1] = new Tool(1, 0); }
                    break;
                case "play":
                    Meta(g, 1, 3);
                    g.UnlockAll();
                    break;
                case "boss": case "clear":
                    Meta(g, 2, 3);
                    g.UnlockAll();
                    break;
                default:
                    if (scen.StartsWith("fx_"))
                    {
                        Meta(g, scen == "fx_hard" ? 3 : (scen == "fx_gold0" ? 1 : 2), 2);
                        g.UnlockAll();
                    }
                    break;
            }
        }

        static void Meta(GameState g, int world, int sub)
        {
            g.Gold = 2.0e5; g.Gems = 300;
            g.World = world; g.Sub = sub;
            g.MaxStage = 7; g.RunMax = 7;
            Lv(g, 24, 12, 12);
            Equip(g, 0, 0, 1, 0, 0, 0);
        }

        static void Lv(GameState g, int power, int speed, int money)
        {
            g.Lv["power"] = power; g.Lv["speed"] = speed; g.Lv["money"] = money;
            foreach (var k in new List<string>(g.Lv.Keys)) g.LvBest[k] = Mathf.Max(g.LvBest.ContainsKey(k) ? g.LvBest[k] : 0, g.Lv[k]);
        }

        static void Equip(GameState g, params int[] kr)
        {
            for (int i = 0; i < g.Equip.Length; i++) g.Equip[i] = null;
            for (int i = 0; i * 2 + 1 < kr.Length && i < g.Equip.Length; i++) g.Equip[i] = new Tool(kr[i * 2], kr[i * 2 + 1]);
        }

        // ------------------------------------------------------------ guion de capturas
        IEnumerator Run()
        {
            yield return Wait(4f);
            Snap(scen + "_a");
            yield return Wait(3f);
            Snap(scen + "_b");
            if (scen == "idle" || scen == "cost") DumpRenderCost();
            var w = gm.World;
            switch (scen)
            {
                case "panels":
                    foreach (var p in new[] { "tools", "shop", "missions", "daily", "skin", "rebirth", "stats", "achievements", "piggy", "settings" })
                    {
                        var pn = gm.Hud.OpenPanel(p);
                        yield return Wait(0.6f);
                        Snap("panel_" + p);
                        if (pn != null) Destroy(pn.gameObject);
                        yield return Wait(0.1f);
                    }
                    G.PendingOffline = 123456.0;
                    G.PendingOfflineTime = 7200.0;
                    gm.Hud.OpenPanel("welcome");
                    yield return Wait(0.6f);
                    Snap("panel_welcome");
                    break;
                case "boss":
                case "fx_boss":
                    yield return ToBoss();
                    Snap(scen == "boss" ? "boss_c" : "fx_boss_p0");
                    if (scen == "boss") break;
                    w.DebugSetSpecialHp("boss", 0.64);
                    for (int i = 0; i < 4; i++) { yield return Wait(0.07f); Snap("fx_boss_ph1_" + i); }
                    yield return Wait(1f);
                    w.DebugSetSpecialHp("boss", 0.3);
                    yield return Wait(0.12f);
                    Snap("fx_boss_ph2_0");
                    yield return Wait(1.2f);
                    Snap("fx_boss_p2");
                    w.DebugSetSpecialHp("boss", 0);
                    for (int i = 0; i < 6; i++) { yield return Wait(0.1f); Snap("fx_boss_break_" + i); }
                    break;
                case "play":
                    gm.Hud.StartPlay();
                    yield return Wait(1.6f);
                    Snap("play_count");
                    yield return Wait(3.6f);
                    w.Joy = new Vector2(0.7f, -0.5f);
                    yield return Wait(2f);
                    Snap("play_a");
                    w.Joy = new Vector2(-0.6f, 0.3f);
                    yield return Wait(4f);
                    Snap("play_b");
                    w.Joy = Vector2.zero;
                    w.SetPlay(1f, w.PlayGold);
                    yield return Wait(2f);
                    Snap("play_victory");
                    yield return Wait(2.5f);
                    Snap("play_result");
                    break;
                case "sparks":
                {
                    // chispas en el centro de la pantalla, para revisar que el eje largo siga a la velocidad
                    var cam = Camera.main;
                    var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                    float d = ray.direction.y < -0.01f ? -ray.origin.y / ray.direction.y : 20f;
                    Vector3 p = ray.origin + ray.direction * d + Vector3.up * 0.5f;
                    for (int i = 0; i < 4; i++)
                    {
                        Mineros.Fx.Fx.Play("hit_spark", p, default(Color), 2.5f);
                        Mineros.Fx.Fx.Play("crit", p + Vector3.right * 3f, new Color(1f, 0.88f, 0.3f), 2.5f);
                        yield return Wait(0.03f);
                    }
                    yield return Wait(0.05f);
                    Snap("sparks_0");
                    yield return Wait(0.06f);
                    Snap("sparks_1");
                    break;
                }
                case "minerzoom":
                {
                    // camara propia hacia el primer minero (frente y 3/4), sin interfaz
                    var mm = FindObjectsByType<Mineros.Miners.MinerModel>(FindObjectsSortMode.None);
                    if (mm.Length == 0) { Debug.LogError("minerzoom: no hay mineros"); break; }
                    foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) if (c.isRootCanvas) c.enabled = false;
                    var go = new GameObject("ShotCam");
                    var zc = go.AddComponent<Camera>();
                    zc.fieldOfView = 26f;
                    zc.clearFlags = CameraClearFlags.SolidColor;
                    zc.backgroundColor = new Color(0.93f, 0.93f, 0.93f);
                    zc.targetTexture = screen;
                    zc.enabled = false;
                    for (int i = 0; i < 3; i++)
                    {
                        Transform t = mm[0].transform;
                        Vector3 center = t.position + Vector3.up * 0.8f;
                        float yaw = i == 0 ? 0f : (i == 1 ? 35f : 180f);
                        Vector3 fwd = mm[0].transform.Find("Model") != null ? mm[0].transform.Find("Model").forward : t.forward;
                        Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * fwd;
                        zc.transform.position = center + dir * 4.2f + Vector3.up * 0.5f;
                        zc.transform.LookAt(center);
                        zc.Render();
                        SaveFrom(zc, Path.Combine(outDir, "minerzoom_" + i + ".png"));
                        yield return Wait(0.5f);
                    }
                    break;
                }
                case "flashcheck":
                {
                    // rocas que quedaron con el destello puesto (emision alta en el bloque de propiedades)
                    var mpb = new MaterialPropertyBlock();
                    int eid = Shader.PropertyToID("_EmissionColor");
                    var jt = System.Type.GetType("Mineros.Fx.JuiceCore");
                    var fl = jt != null ? jt.GetField("flashList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static) : null;
                    for (int k = 0; k < 6; k++)
                    {
                        int white = 0, total = 0;
                        foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                        {
                            if (!r.gameObject.activeInHierarchy || !r.HasPropertyBlock()) continue;
                            total++;
                            r.GetPropertyBlock(mpb);
                            if (mpb.HasColor(eid) && mpb.GetColor(eid).maxColorComponent > 0.5f)
                            {
                                white++;
                                if (k == 5) Debug.Log("flashcheck blanco: " + r.transform.parent?.name + "/" + r.name);
                            }
                        }
                        var list = fl != null ? fl.GetValue(null) as System.Collections.ICollection : null;
                        Debug.Log("flashcheck t=" + Time.unscaledTime.ToString("0.0") + " conBloque=" + total + " blancos=" + white
                            + " flashList=" + (list != null ? list.Count : -1));
                        yield return Wait(1f);
                    }
                    Snap("flashcheck_c");
                    break;
                }
                case "drag":
                    yield return DragMerge();
                    break;
                case "cost":
                    // mide cuanto aporta cada capa: sin interfaz, y ademas sin sombras
                    var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                    foreach (var c in canvases) if (c.isRootCanvas) c.enabled = false;
                    yield return Wait(0.3f);
                    Snap("cost_noui");
                    var sun = RenderSettings.sun;
                    if (sun != null) sun.shadows = LightShadows.None;
                    yield return Wait(0.3f);
                    Snap("cost_noui_noshadow");
                    break;
                case "clear":
                    G.Progress = 0.98;
                    yield return Wait(3f);
                    Snap("clear_c");
                    break;
                case "milestone":
                    G.Gold = 1.0e9;
                    G.Buy("power");
                    yield return Wait(1f);
                    Snap("milestone_c");
                    break;
                case "goal":
                    G.Lv["power"] = 3;
                    yield return Wait(1.5f);
                    Snap("goal_c");
                    G.ClaimGoal();
                    yield return Wait(1.5f);
                    Snap("goal_d");
                    break;
                case "unlocked":
                    yield return Wait(1f);
                    Snap("unlocked_c");
                    break;
                case "fx_meteor":
                    G.DebugStartEvent("meteor");
                    for (int i = 0; i < 14; i++) { yield return Wait(0.11f); Snap("fx_meteor_" + i.ToString("00")); }
                    break;
                case "fx_chest":
                    G.DebugStartEvent("chest");
                    yield return Wait(0.35f);
                    Snap("fx_chest_a");
                    yield return Wait(1.5f);
                    Snap("fx_chest_b");
                    w.DebugSetSpecialHp("chest", 0);
                    for (int i = 0; i < 5; i++) { yield return Wait(0.09f); Snap("fx_chest_break_" + i); }
                    break;
                case "fx_gold":
                case "fx_gold0":
                    G.DebugStartEvent("gold_rush");
                    yield return Wait(0.3f);
                    Snap(scen + "_0");
                    yield return Wait(2f);
                    Snap(scen + "_1");
                    break;
                case "fx_frenzy":
                    G.DebugStartEvent("frenzy");
                    yield return Wait(0.8f);
                    Snap("fx_frenzy_0");
                    yield return Wait(0.6f);
                    Snap("fx_frenzy_1");
                    break;
                case "fx_ms":
                    G.Gold = 1.0e9;
                    G.Buy("power");
                    yield return Wait(0.25f); Snap("fx_ms_0");
                    yield return Wait(0.35f); Snap("fx_ms_1");
                    yield return Wait(0.5f); Snap("fx_ms_2");
                    break;
                case "fx_hard":
                    yield return Wait(1f);
                    Snap("fx_hard_0");
                    break;
            }
            if (scen.StartsWith("ev_"))
            {
                G.DebugStartEvent(scen.Substring(3));
                yield return Wait(1.5f);
                Snap(scen + "_c");
                yield return Wait(3f);
                Snap(scen + "_d");
            }
        }

        /// <summary>Arrastre real (eventos de puntero) de un Mazo D del inventario sobre otro igual: deben fusionarse en un Mazo C.</summary>
        IEnumerator DragMerge()
        {
            var panel = gm.Hud.OpenPanel("tools");
            yield return Wait(0.8f);
            Mineros.UI.ToolCell a = null, b = null;
            foreach (var c in panel.GetComponentsInChildren<Mineros.UI.ToolCell>())
            {
                if (c.Src.C != SlotKind.Inv || c.Preview) continue;
                if (c.Src.I == 0) a = c;
                if (c.Src.I == 1) b = c;
            }
            if (a == null || b == null) { Debug.LogError("dragcheck: no encontre las celdas"); yield break; }
            var cam = Camera.main;
            Vector2 pa = RectTransformUtility.WorldToScreenPoint(cam, a.transform.position);
            Vector2 pb = RectTransformUtility.WorldToScreenPoint(cam, b.transform.position);
            var es = UnityEngine.EventSystems.EventSystem.current;
            var pe = new UnityEngine.EventSystems.PointerEventData(es) { position = pa, pressPosition = pa, pointerDrag = a.gameObject };
            UnityEngine.EventSystems.ExecuteEvents.Execute(a.gameObject, pe, UnityEngine.EventSystems.ExecuteEvents.beginDragHandler);
            for (int i = 1; i <= 10; i++)
            {
                pe.position = Vector2.Lerp(pa, pb, i / 10f * 0.6f);
                UnityEngine.EventSystems.ExecuteEvents.Execute(a.gameObject, pe, UnityEngine.EventSystems.ExecuteEvents.dragHandler);
                yield return null;
            }
            Snap("drag_mid");
            pe.position = pb;
            UnityEngine.EventSystems.ExecuteEvents.Execute(a.gameObject, pe, UnityEngine.EventSystems.ExecuteEvents.dragHandler);
            UnityEngine.EventSystems.ExecuteEvents.Execute(b.gameObject, pe, UnityEngine.EventSystems.ExecuteEvents.dropHandler);
            UnityEngine.EventSystems.ExecuteEvents.Execute(a.gameObject, pe, UnityEngine.EventSystems.ExecuteEvents.endDragHandler);
            yield return Wait(0.4f);
            Snap("drag_merged");
            string inv = "";
            for (int i = 0; i < 4; i++) inv += " inv" + i + "=" + (G.Inv[i] == null ? "-" : G.Inv[i].ToString());
            Debug.Log("dragcheck" + inv);
            yield return Wait(1.2f);
            Snap("drag_after");
        }

        /// <summary>Desglose de lo que se dibuja: renderers, materiales, triangulos y sombras por grupo (hijo de "World").</summary>
        static void DumpRenderCost()
        {
            var groups = new Dictionary<string, int[]>();
            var mats = new Dictionary<string, HashSet<Material>>();
            var allMats = new HashSet<Material>();
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                Transform t = r.transform;
                string key = t.name;
                while (t.parent != null && t.parent.name != "WorldRoot" && t.parent.name != "World" && t.parent.name != "Mineros") { t = t.parent; key = t.name; }
                if (t.parent == null) key = "(raiz) " + key;
                int tris = 0;
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) tris = mf.sharedMesh.triangles.Length / 3;
                int[] g;
                if (!groups.TryGetValue(key, out g)) { g = new int[3]; groups[key] = g; mats[key] = new HashSet<Material>(); }
                g[0]++; g[1] += tris; if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) g[2]++;
                foreach (var m in r.sharedMaterials) if (m != null) { mats[key].Add(m); allMats.Add(m); }
            }
            var keys = new List<string>(groups.Keys);
            keys.Sort((a, b) => groups[b][1].CompareTo(groups[a][1]));
            var sb = new System.Text.StringBuilder("cost total_materials=" + allMats.Count + "\n");
            foreach (var k in keys)
                sb.Append("cost ").Append(k).Append(" renderers=").Append(groups[k][0]).Append(" tris=").Append(groups[k][1])
                  .Append(" shadow=").Append(groups[k][2]).Append(" mats=").Append(mats[k].Count).Append('\n');
            Debug.Log(sb.ToString());
        }

        /// <summary>Lleva la etapa al final del subnivel 5 para que aparezca el jefe.</summary>
        IEnumerator ToBoss()
        {
            G.Sub = 5;
            G.Progress = 0.99;
            for (int i = 0; i < 3; i++) G.OnRockBroken(false);
            yield return Wait(2f);
        }

        static IEnumerator Wait(float s) { return new WaitForSecondsRealtime(s); }

        /// <summary>Adelanta la simulacion `secs` segundos de juego de golpe (los cuadros en la nube son lentos).</summary>
        static void Advance(Island isl, float secs) { for (float t = 0f; t < secs; t += 0.1f) isl.Tick(0.1f); }

        /// <summary>
        /// Isla lejana: niebla con candado (sin Muelle), "?" (con Muelle), descubrimiento, la balsa a las 6:00, el cruce,
        /// la cuadrilla picando con barras de vida, la roca fijada y la balsa grande (8 asientos).
        /// </summary>
        IEnumerator FarShots(Mineros.IslandView.IslandGame g)
        {
            var isl = g.Isl;
            Vector3 far = g.FarCenter;
            Vector3 dir = far.normalized;
            g.Ui.CloseSheet();
            g.DebugLook(dir * (Island.FarDist - 16f), 15f);
            for (int f = 0; f < 20; f++) yield return null;
            Snap("lejana_niebla_candado");
            var dp = isl.Find(BKind.Dock);
            if (dp == null)
            {
                isl.Coins = 1e9; for (int i = 0; i < isl.Stock.Length; i++) isl.Stock[i] = Mathf.Max(isl.Stock[i], 300);
                isl.BonusBuilders = 6;
                var fp = isl.Plots.Find(p => isl.Allowed(BKind.Dock, p));
                if (fp != null) { isl.Build(BKind.Dock, fp); isl.FinishAllWork(); }
                dp = isl.Find(BKind.Dock);
            }
            if (dp == null) { Debug.Log("lejana: SIN MUELLE"); yield break; }
            isl.Plots[0].Level = Mathf.Max(isl.Plots[0].Level, 9);
            while (dp.Level < 4 && isl.Upgrade(dp)) isl.FinishAllWork();
            for (int f = 0; f < 30; f++) yield return null;
            Snap("lejana_niebla_pregunta");
            bool ok = isl.TryDiscoverFar();
            yield return Wait(2.6f);
            g.DebugLook(far, 11f);
            for (int f = 0; f < 10; f++) yield return null;
            Snap("lejana_descubierta");
            Debug.Log("lejana descubierta=" + ok + " rocas=" + isl.FarRockCount + " asientos=" + isl.RaftSeats);
            // 5:55 -> sale la balsa
            isl.DayClock = Island.DayLength * 0.928f;
            Advance(isl, 3f);
            Vector3 home = new Vector3(isl.RaftX, 0f, isl.RaftZ);
            g.DebugLook(home, 8f);
            Advance(isl, 6f);
            for (int f = 0; f < 10; f++) yield return null;
            Snap("lejana_embarque");
            Advance(isl, 40f);
            Debug.Log("lejana balsa=" + isl.Raft + " cuadrilla=" + isl.Crew.Count);
            Advance(isl, Island.RaftTrip * 0.5f);
            g.DebugLook(new Vector3(isl.RaftX, 0f, isl.RaftZ), 9f);
            for (int f = 0; f < 10; f++) yield return null;
            Snap("lejana_cruce");
            Advance(isl, Island.RaftTrip * 0.5f + 25f);
            g.DebugLook(far, 8.5f);
            for (int f = 0; f < 20; f++) yield return null;
            Snap("lejana_picando");
            Ore pick = isl.OreList.Find(o => o.Far && !o.Dead);
            if (pick != null) { for (int i = 0; i < 25; i++) g.DebugTapOre(pick); }
            Advance(isl, 8f);
            for (int f = 0; f < 20; f++) yield return null;
            Snap("lejana_fijada");
            Debug.Log("lejana foco=" + isl.FarFocus + " balsa=" + isl.Raft + " hora=" + isl.Hour.ToString("0.0"));
            // balsa de 8
            while (dp.Level < 8 && isl.Upgrade(dp)) isl.FinishAllWork();
            isl.DayClock = Island.DayLength * 0.66f;   // 19:00 -> vuelven
            Advance(isl, 90f);
            g.DebugLook(new Vector3(isl.RaftX, 0f, isl.RaftZ), 7f);
            for (int f = 0; f < 20; f++) yield return null;
            Snap("lejana_balsa8");
            Debug.Log("lejana vuelta balsa=" + isl.Raft + " asientos=" + isl.RaftSeats + " viajes=" + isl.Stat("raft_trips"));
            g.DebugLook(Vector3.zero, 15.2f);
            yield return Wait(0.3f);
        }

        /// <summary>Espera a que el corte de techos deje de moverse (con cuadros lentos el dt topeado lo frena), maximo 15 s.</summary>
        static IEnumerator RoofSettle(Mineros.IslandView.IslandGame g) { return RoofSettle(g, null, 0f); }

        /// <summary>Igual, pero vuelve a apuntar la camara en cada vuelta (el tutorial la lleva a la casa si se espera).</summary>
        static IEnumerator RoofSettle(Mineros.IslandView.IslandGame g, Vector3? at, float size)
        {
            if (at.HasValue) g.DebugLook(at.Value, size);
            yield return Wait(0.6f);
            float last = -1f, t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 15f)
            {
                if (at.HasValue) g.DebugLook(at.Value, size);
                float a = g.RoofCutAmount;
                if (Mathf.Abs(a - last) < 0.002f) break;
                last = a;
                yield return Wait(0.4f);
            }
            if (at.HasValue) g.DebugLook(at.Value, size);
            yield return Wait(0.3f);
            if (at.HasValue) g.DebugLook(at.Value, size);
        }

        /// <summary>
        /// Fila de control de los mineros con esqueleto (Resources/MinerRig): los 9 especialistas en cada etapa, cada uno
        /// en una pose (quieto, caminando, golpe arriba, impacto, festejo) y un primer plano. Los que no tienen modelo
        /// salen con el de siempre, asi se ve cuales faltan.
        /// </summary>
        IEnumerator RunRig(Mineros.IslandView.IslandGame g)
        {
            var rootGo = new GameObject("FilaRig");
            var home = g.Isl.Find(BKind.House) ?? g.Isl.Plots[0];
            Vector3 c = new Vector3(home.X, 0f, home.Z - 3.5f);
            var ms = new List<Mineros.Miners.MinerModel>();
            for (int tier = 1; tier <= 3; tier++)
            {
                foreach (var m in ms) Destroy(m.gameObject);
                ms.Clear();
                int rigged = 0;
                for (int ch = 0; ch < 9; ch++)
                {
                    string rig = Mineros.Miners.RiggedMiner.NameFor(ch, tier);
                    var mm = Mineros.Miners.MinerModel.Create(rootGo.transform, ch, rig);
                    mm.transform.position = c + new Vector3((ch - 4) * 1.15f, 0f, (ch % 2) * 0.6f);
                    mm.transform.localScale = Vector3.one * Mineros.IslandView.IslandGame.MinerScale;
                    mm.SetLook(new Color32(0xf5, 0xa8, 0x23, 255), new Tool(ch == 0 ? 1 : 0, tier)); mm.SetSpecialist(Mineros.IslandView.IslandArt.SpecColor(ch));
                    if (mm.IsRigged) rigged++;
                    ms.Add(mm);
                }
                for (int f = 0; f < 40; f++) { g.DebugLook(c, 6.5f); PoseRow(ms, f); yield return null; }
                Snap("rig_etapa" + tier);
                Debug.Log("rigcheck etapa=" + tier + " con_esqueleto=" + rigged + "/9");
            }
            // primer plano del primero con esqueleto (o del de piedra): las 5 poses lado a lado
            int pick = 0;
            for (int ch = 0; ch < 9; ch++) if (Mineros.Miners.RiggedMiner.Get(Mineros.Miners.RiggedMiner.NameFor(ch, 1)) != null) { pick = ch; break; }
            foreach (var m in ms) Destroy(m.gameObject);
            ms.Clear();
            for (int i = 0; i < 5; i++)
            {
                var mm = Mineros.Miners.MinerModel.Create(rootGo.transform, pick, Mineros.Miners.RiggedMiner.NameFor(pick, 1));
                mm.transform.position = c + new Vector3((i - 2) * 1.3f, 0f, 0f);
                mm.transform.localScale = Vector3.one * Mineros.IslandView.IslandGame.MinerScale;
                mm.SetLook(new Color32(0xf5, 0xa8, 0x23, 255), new Tool(pick == 0 ? 1 : 0, 2)); mm.SetSpecialist(Mineros.IslandView.IslandArt.SpecColor(pick));
                ms.Add(mm);
            }
            for (int f = 0; f < 40; f++) { g.DebugLook(c + new Vector3(0f, 0f, 0.6f), 3.4f); PoseRow(ms, f); yield return null; }
            Snap("rig_poses");
        }

        static void PoseRow(List<Mineros.Miners.MinerModel> ms, int frame)
        {
            for (int i = 0; i < ms.Count; i++)
            {
                int pose = i % 5;
                Vector3 dir = new Vector3(0.35f, 0f, -1f);   // de frente a la camara, apenas girado
                bool walk = pose == 1;
                float swing = pose == 2 ? 0.45f : pose == 3 ? 0.62f : -1f;
                ms[i].SetCelebrate(pose == 4 ? 1f : 0f);
                ms[i].SetPose(dir, walk ? 1.2f : 0f, walk, swing, false, frame == 0 ? 0f : 0.05f);
            }
        }

        static string lastShot = "-";
        static int shotCount;

        void Snap(string name)
        {
            lastShot = name; shotCount++;
            try { Save(Path.Combine(outDir, name + ".png")); }
            catch (System.Exception e) { Debug.LogException(e); }
        }

        // -shotScale 2 = 1440x3088 (pantalla de telefono de gama alta) para revisar nitidez
        static readonly int ShotW = (int)(720 * ShotScale()), ShotH = (int)(1544 * ShotScale());
        static float ShotScale()
        {
            float f;
            return float.TryParse(Arg("-shotScale", "1"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f) ? f : 1f;
        }
        static RenderTexture screen, low;

        /// <summary>
        /// Los cuadros entre fotos se dibujan a 1/3 (en la nube cada cuadro 2x con MSAA por software tarda ~3 s);
        /// la foto se dibuja a resolucion completa. -shotFast 0 lo apaga.
        /// </summary>
        static readonly bool Fast = Arg("-shotFast", "1") != "0";
        static RenderTexture Frame()
        {
            if (!Fast) return screen;
            if (low == null) low = new RenderTexture(Mathf.Max(64, ShotW / 3), Mathf.Max(64, ShotH / 3), 24);
            return low;
        }

        static void Retarget(RenderTexture rt)
        {
            var cam = Camera.main;
            if (cam != null) cam.targetTexture = rt;
            if (uiCam != null) uiCam.targetTexture = rt;
            // el CanvasScaler recalcula la escala al habilitarse: misma maqueta en unidades de canvas
            foreach (var sc in FindObjectsByType<UnityEngine.UI.CanvasScaler>(FindObjectsSortMode.None)) { if (!sc.enabled) continue; sc.enabled = false; sc.enabled = true; }
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>
        /// La pantalla de Xvfb es apaisada: la camara dibuja siempre en una textura vertical 720x1544 y los Canvas
        /// pasan a ScreenSpaceCamera, asi la interfaz se acomoda como en un telefono (paneles, area segura, escalado).
        /// </summary>
        static void Portrait()
        {
            var cam = Camera.main;
            if (cam == null) return;
            if (screen == null) screen = new RenderTexture(ShotW, ShotH, 24) { antiAliasing = 4 };   // MSAA 4x como en el telefono
            cam.targetTexture = Frame();
            // con posprocesado la interfaz va en una segunda camara (como el Overlay del telefono, que no se procesa)
            Camera target = cam;
            if (cam.GetComponent<Mineros.IslandView.IslandPost>() != null)
            {
                if (uiCam == null)
                {
                    uiCam = new GameObject("UiShotCam").AddComponent<Camera>();
                    DontDestroyOnLoad(uiCam.gameObject);
                    uiCam.clearFlags = CameraClearFlags.Depth;
                    uiCam.cullingMask = 1 << UiLayer;
                    uiCam.orthographic = true;
                    uiCam.nearClipPlane = 0.1f; uiCam.farClipPlane = 10f;
                }
                uiCam.depth = cam.depth + 1;
                uiCam.targetTexture = Frame();
                cam.cullingMask &= ~(1 << UiLayer);
                target = uiCam;
            }
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!c.isRootCanvas) continue;
                if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = target;
                    c.planeDistance = target.nearClipPlane + 0.05f;
                }
                if (target == uiCam) SetLayer(c.transform, UiLayer);
            }
        }

        const int UiLayer = 5;
        static Camera uiCam;

        static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer);
        }

        /// <summary>Lee lo que una camara ya dibujo en la textura de captura y lo guarda.</summary>
        static void SaveFrom(Camera c, string path)
        {
            RenderTexture.active = screen;
            var tex = new Texture2D(ShotW, ShotH, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, ShotW, ShotH), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
            Debug.Log("shot " + path);
        }

        /// <summary>Renderiza la camara principal (con la UI) a un PNG 720x1544.</summary>
        public static void Save(string path)
        {
            Portrait();
            Retarget(screen);
            var cam = Camera.main;
            cam.Render();
            if (uiCam != null && uiCam.targetTexture == screen) uiCam.Render();
            RenderTexture.active = screen;
            var tex = new Texture2D(ShotW, ShotH, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, ShotW, ShotH), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
            if (Fast) Retarget(Frame());
            // estadisticas del ultimo cuadro dibujado (para vigilar draw calls en movil)
            Debug.Log("shot " + path + "\nstats " + Path.GetFileNameWithoutExtension(path) + " draws=" + UnityEditor.UnityStats.drawCalls
                + " batches=" + UnityEditor.UnityStats.batches + " setpass=" + UnityEditor.UnityStats.setPassCalls
                + " tris=" + UnityEditor.UnityStats.triangles + " shadowcasters=" + UnityEditor.UnityStats.shadowCasters
                + " t=" + Time.realtimeSinceStartup.ToString("0") + "s cuadro=" + Time.frameCount);
        }

        static string Arg(string name, string def)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return def;
        }
    }
}
#endif
