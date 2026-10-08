using System;

namespace Mineros.Core
{
    /// <summary>
    /// Primera partida (docs/DETALLES.md): la isla empieza casi vacia y cada cosa aparece en su paso. Mientras dura el
    /// tutorial no hay parcelas libres, ni metas, ni sorpresas; cada paso avanza solo cuando el jugador hace la accion.
    /// </summary>
    public sealed partial class Island
    {
        public enum TutStep { TapRock = 0, WatchMiner = 1, UpgradeHouse = 2, FinishWork = 3, BuildSawmill = 4, CollectWood = 5, Done = 6 }

        /// <summary>Paso actual. Las partidas guardadas sin este dato (0.9 o anteriores) ya lo terminaron.</summary>
        public TutStep Tut = TutStep.Done;
        public bool TutDone { get { return Tut >= TutStep.Done; } }
        public event Action<TutStep> TutAdvanced;

        /// <summary>Partida nueva: arranca el tutorial (lo llama el juego al crear la isla, no al cargar).</summary>
        public void StartTutorial() { Tut = TutStep.TapRock; }

        void TickTutorial()
        {
            if (TutDone) return;
            var next = Tut;
            switch (Tut)
            {
                case TutStep.TapRock: if (Stat("tap_breaks") >= 1) next = TutStep.WatchMiner; break;
                case TutStep.WatchMiner: if (Stat("rocks") >= 3 || Stat("miner_taps") >= 1) next = TutStep.UpgradeHouse; break;   // tocarlo cierra el paso
                case TutStep.UpgradeHouse: { var h = Find(BKind.House); if (h != null && (h.Work > 0 || h.Level >= 2)) next = TutStep.FinishWork; break; }
                case TutStep.FinishWork: if (Level(BKind.House) >= 2) next = TutStep.BuildSawmill; break;
                case TutStep.BuildSawmill: if (Find(BKind.Sawmill) != null) next = TutStep.CollectWood; break;
                case TutStep.CollectWood: if (Stat("collected") >= 1) next = TutStep.Done; break;
            }
            if (next == Tut) return;
            Tut = next;
            AddStat("tut_" + (int)next, 1);
            TutAdvanced?.Invoke(next);
        }

        /// <summary>Parcelas libres visibles: ninguna hasta el paso de construir.</summary>
        bool TutHidesPlots { get { return Tut < TutStep.BuildSawmill; } }
    }
}
