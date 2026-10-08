using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using Mineros.UI;
using UnityEngine;
using UnityEngine.UI;
using Juice = Mineros.Fx.Juice;

namespace Mineros.IslandView
{
    /// <summary>
    /// Primera partida y HUD progresivo (docs/DETALLES.md, detalle 17): el HUD empieza casi vacio y cada boton aparece
    /// cuando hace falta, con un salto, un brillo y la etiqueta "¡Nuevo!" la primera vez. La mano del tutorial sigue los
    /// pasos del nucleo (Island.Tut) y desaparece apenas el jugador hace la accion.
    /// </summary>
    public sealed partial class IslandUi
    {
        readonly Dictionary<string, bool> shownHud = new Dictionary<string, bool>();
        float onboardT;

        void UpdateOnboarding()
        {
            onboardT += Time.unscaledDeltaTime;
            bool done = Isl.TutDone;
            // los mineros, los almacenes y lo que vive en el ☰ se muestran solos (IslandUi.Clean.cs)
            Gate("gemas", gemPill != null ? gemPill.Root : null, Isl.Gems > 0 || done);
            Gate("meta", goalBox, done);
            Gate("turbo", turboBtn != null ? (RectTransform)turboBtn.transform : null, done, true);
            Gate("ampliar", expandBtn != null ? (RectTransform)expandBtn.transform : null, done, true);
        }

        /// <summary>
        /// Muestra u oculta un elemento del HUD. `andExisting`: solo puede ocultar (otra parte del codigo decide si se
        /// muestra). Al aparecer por primera vez: salto, brillo y "¡Nuevo!".
        /// </summary>
        void Gate(string key, RectTransform rt, bool allowed, bool andExisting = false)
        {
            if (rt == null) return;
            bool visible = andExisting ? (allowed && rt.gameObject.activeSelf) : allowed;
            if (!allowed && rt.gameObject.activeSelf) rt.gameObject.SetActive(false);
            if (!andExisting && allowed && !rt.gameObject.activeSelf) rt.gameObject.SetActive(true);
            bool was;
            shownHud.TryGetValue(key, out was);
            if (visible && !was && onboardT > 1.5f) Reveal(key, rt);
            shownHud[key] = visible;
        }

        void Reveal(string key, RectTransform rt)
        {
            Tw.Pop(rt, 1.35f);
            Sparkle(LayerPos(rt), new Color(1f, 0.95f, 0.6f));
            Sfx.Play("pop", -8f, 1.2f);
            // sin cinta "¡Nuevo!" (0.9.2): alcanza con el salto y el brillo
        }

        // ------------------------------------------------------------ mano del tutorial
        float idleHintT, idleHintShownT = -1f;
        int idleHints, lastIdleHintPlot = -1;

        void TutorialTarget(ref Vector3? target, ref string hint)
        {
            if (sheet != null || WorldQuiet || IntroOpen || introLayer != null) return;   // la presentacion de Gus va primero
            if (summonActive) return;   // con la carta en la mano, la unica orden es "arrastrala" (antes competia con "picá 40 monedas")
            var house = Isl.Find(BKind.House);
            switch (Isl.Tut)
            {
                case Island.TutStep.TapRock:
                    target = NearestOre(); hint = Loc.T("¡Tocá la roca para picar!");
                    break;
                case Island.TutStep.WatchMiner:
                    if (Isl.Miners.Count > 0) { target = game.MinerWorld(Isl.Miners[0]) + Vector3.up * 0.6f; hint = Loc.T("¡Tocá a tu minero!"); }
                    break;
                case Island.TutStep.UpgradeHouse:
                    if (house != null && Isl.CanUpgrade(house)) { target = game.PlotWorld(house) + Vector3.up * (game.PlotHeight(house) * 0.5f); hint = Loc.T("¡Mejorá la casa!"); }
                    else
                    {
                        target = NearestOre();
                        int st = Isl.Stock[(int)Res.Stone];
                        hint = Isl.Coins < 15 ? Loc.T("Picá para juntar 15 monedas") : st < 3 ? Loc.T("Picá para juntar 3 piedras") : Loc.T("¡Picá un poco más!");
                    }
                    break;
                case Island.TutStep.FinishWork:
                    if (house != null && house.Work > 0) { target = game.PlotWorld(house) + Vector3.up * (game.PlotHeight(house) + 1.5f); hint = Loc.T("¡Terminala gratis!"); }
                    break;
                case Island.TutStep.BuildSawmill:
                {
                    Plot free = null;
                    foreach (var p in Isl.Plots) if (Isl.Offered(p)) { free = p; break; }
                    if (free != null && Isl.CanBuild(BKind.Sawmill, free)) { target = game.PlotWorld(free) + Vector3.up * 0.9f; hint = Loc.T("Construí el Aserradero"); }
                    else { target = NearestOre(); hint = Loc.T("Picá para juntar ") + BigNum.Fmt(Isl.BuildCost(BKind.Sawmill)) + Loc.T(" monedas"); }
                    break;
                }
                case Island.TutStep.CollectWood:
                {
                    var saw = Isl.Find(BKind.Sawmill);
                    if (saw != null && saw.Work > 0) { target = game.PlotWorld(saw) + Vector3.up * 2.6f; hint = Loc.T("¡Terminala gratis!"); }
                    else if (saw != null && saw.Ready > 0) { target = game.PlotWorld(saw) + Vector3.up * (game.PlotHeight(saw) + 1.6f); hint = Loc.T("¡Cobrá la madera!"); }
                    break;
                }
                default:
                    // despues del tutorial: la mano solo vuelve si pasan 25 s sin hacer nada y hay algo para mejorar
                    if (summonActive) { idleHintT = 0f; break; }   // la invocacion es la escena: nada encima
                    bool touching = Input.GetMouseButton(0) || Input.touchCount > 0;
                    idleHintT = touching ? 0f : idleHintT + Time.unscaledDeltaTime;
                    // empujon por inactividad, sin insistir: a los 40 s, como mucho 3 veces por sesion y 12 s cada vez,
                    // y nunca dos veces seguidas sobre el mismo edificio (auditoria final: "Upgrade this!" constante)
                    if (idleHintT > 40f && arrowPlots.Count > 0 && idleHints < 3)
                    {
                        int pick = -1;
                        foreach (var id in arrowPlots) { if (id != lastIdleHintPlot || arrowPlots.Count == 1) { pick = id; break; } }
                        if (pick >= 0)
                        {
                            var p = Isl.Plots[pick];
                            target = game.PlotWorld(p) + Vector3.up * (game.PlotHeight(p) * 0.6f); hint = Loc.T("¡Podés mejorar esto!");
                            if (idleHintShownT < 0f) idleHintShownT = Time.unscaledTime;
                            if (Time.unscaledTime - idleHintShownT > 12f) { idleHints++; lastIdleHintPlot = pick; idleHintT = 0f; idleHintShownT = -1f; target = null; }
                        }
                    }
                    break;
            }
        }

        Vector3? NearestOre()
        {
            Ore best = null; float bd = 1e9f;
            foreach (var o in Isl.OreList) { if (o.Dead || o.Age < 0.6f || o.Giant) continue; float d = o.X * o.X + o.Z * o.Z; if (d < bd) { bd = d; best = o; } }
            if (best == null) return null;
            return new Vector3(best.X, 0.6f, best.Z);
        }
    }
}
