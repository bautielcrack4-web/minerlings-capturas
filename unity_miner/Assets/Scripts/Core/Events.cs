using System;

namespace Mineros.Core
{
    /// <summary>Eventos aleatorios: gold_rush (x3 oro), frenzy (x2 velocidad), meteor y chest (los resuelve el mundo).</summary>
    public sealed partial class GameState
    {
        internal string EvKind = "";
        internal double EvLeft;
        internal double EvTotal;
        internal double EvNext;
        bool EventsEnabledField;

        /// <summary>Apagarlo cancela el evento en curso.</summary>
        public bool EventsEnabled
        {
            get { return EventsEnabledField; }
            set
            {
                EventsEnabledField = value;
                if (!value && EvKind != "") EndEvent();
            }
        }

        /// <summary>Tipo del evento activo, "" si no hay.</summary>
        public string ActiveEvent() { return EvKind; }

        public double EventTimeLeft() { return EvKind != "" ? Math.Max(EvLeft, 0.0) : 0.0; }

        public double EventGoldMult() { return EvKind == "gold_rush" ? 3.0 : 1.0; }

        public double EventSpeedMult() { return EvKind == "frenzy" ? 2.0 : 1.0; }

        void TickEvents(double dt)
        {
            if (!EventsEnabledField) return;
            if (EvKind != "")
            {
                EvLeft -= dt;
                if (EvLeft <= 0.0) EndEvent();
                return;
            }
            EvNext -= dt;
            if (EvNext <= 0.0) StartEvent(RollEvent());
        }

        internal string RollEvent()
        {
            int total = 0;
            for (int i = 0; i < Content.EventWeights.Length; i++) total += Content.EventWeights[i];
            int r = rng.Next(total);
            for (int i = 0; i < Content.EventWeights.Length; i++)
            {
                r -= Content.EventWeights[i];
                if (r < 0) return Content.EventKinds[i];
            }
            return "gold_rush";
        }

        void StartEvent(string kind)
        {
            EvKind = kind;
            EvTotal = Content.EventTime(kind);
            EvLeft = EvTotal;
            AddStat("events");
            EventStarted?.Invoke(kind, EvTotal);
        }

        void EndEvent()
        {
            string kind = EvKind;
            EvKind = "";
            EvLeft = 0.0;
            // randf_range(min, max) / (1 + 10% por nivel de Suerte)
            EvNext = (Balance.EventMin + rng.NextDouble() * (Balance.EventMax - Balance.EventMin)) / (1.0 + 0.1 * EssenceLevel("luck"));
            if (kind != "") EventEnded?.Invoke(kind);
        }

        /// <summary>Depuracion/capturas: fuerza un evento ("gold_rush","frenzy","meteor","chest").</summary>
        public void DebugStartEvent(string kind)
        {
            if (!Content.IsEventKind(kind)) return;
            if (EvKind != "") EndEvent();
            StartEvent(kind);
        }
    }
}
