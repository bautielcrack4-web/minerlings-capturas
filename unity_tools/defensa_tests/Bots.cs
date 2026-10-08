using System;
using System.Collections.Generic;
using Fortin.Core;

namespace DefensaTests
{
    /// <summary>Jugadores automaticos para medir el balance (seccion 13 de la especificacion).</summary>
    public abstract class Bot
    {
        public Battle B;
        protected float think;
        public abstract void Decide();

        /// <summary>Juega la partida entera. Devuelve true si gana (con un revivir si se permite).</summary>
        public bool Play(Battle b, bool allowRevive = false, float maxTime = 1500f)
        {
            B = b;
            while (b.Time < maxTime)
            {
                if (b.State == BState.Won) return true;
                if (b.State == BState.Lost)
                {
                    if (allowRevive && b.Revive()) continue;
                    return false;
                }
                think -= Battle.Dt;
                if (think <= 0f || b.State == BState.Cards) { Decide(); think = 0.5f; }
                b.Step();
                b.Events.Clear();
            }
            return false;
        }
    }

    /// <summary>Novato: invoca siempre que puede y fusiona al azar; elige cartas al azar.</summary>
    public sealed class NoviceBot : Bot
    {
        readonly Random r;
        public NoviceBot(int seed) { r = new Random(seed); }

        public override void Decide()
        {
            if (B.State == BState.Cards) { B.PickCard(r.Next(3)); return; }
            if (B.Sparks >= B.SummonCost && B.EmptyCells() > 0) B.Summon();
            if (r.NextDouble() < 0.5)
            {
                var pairs = new List<(int, int)>();
                for (int a = 0; a < 15; a++) for (int c = 0; c < 15; c++) if (B.CanFuse(a, c)) pairs.Add((a, c));
                if (pairs.Count > 0) { var p = pairs[r.Next(pairs.Count)]; B.Drop(p.Item1, p.Item2); }
            }
        }
    }

    /// <summary>
    /// Bueno: fusiona por nivel (primero los bajos), mantiene casilleros para invocar, sube de rango al tipo que mas
    /// pega cuando le sobra, acomoda a los de poco alcance cerca de la pista y elige bien las cartas.
    /// </summary>
    public sealed class GoodBot : Bot
    {
        public override void Decide()
        {
            if (B.State == BState.Cards) { PickCard(); return; }
            foreach (var pk in B.Pickups) if (pk.Alive && pk.Kind != 2) { B.TapPickup(pk.Id); break; }
            // 1. fusionar (el nivel mas bajo primero)
            for (int guard = 0; guard < 4; guard++)
            {
                int ba = -1, bb = -1, bl = 99;
                for (int a = 0; a < 15; a++)
                {
                    var u = B.Cells[a];
                    if (u.Empty || u.Level >= bl) continue;
                    for (int c = 0; c < 15; c++)
                        if (B.CanFuse(a, c)) { ba = a; bb = c; bl = u.Level; break; }
                }
                if (ba < 0) break;
                // conviene dejar el resultado en el casillero con mejor alcance (el que esta mas cerca de la pista)
                if (Edge(ba) > Edge(bb)) { int t = ba; ba = bb; bb = t; }
                B.Drop(ba, bb);
            }
            // 2. invocar
            int empty = B.EmptyCells();
            if (empty > 0 && B.Sparks >= B.SummonCost) B.Summon();
            // 3. con el tablero lleno, o con chispas de sobra, mejorar el tipo que mas pega
            int best = -1; float bestScore = 0f;
            for (int s = 0; s < 5; s++)
            {
                float score = 0f;
                foreach (var u in B.Cells) if (!u.Empty && u.Hero == B.Deck[s]) score += (float)Math.Pow(1.85, u.Level - 1);
                if (score > bestScore) { bestScore = score; best = s; }
            }
            if (best >= 0)
            {
                int cost = B.RankCost(best);
                // mejorar el tipo cuando invocar ya sale mas caro que la mejora, o el tablero esta lleno
                bool worth = empty == 0 || B.SummonCost > cost * 1.3f;
                if (worth && B.Sparks >= cost) B.RankUp(best);
            }
            // 4. tablero lleno sin fusiones: vender el mas debil que no tenga pareja
            if (B.EmptyCells() == 0 && B.Sparks >= B.SummonCost * 2)
            {
                int weak = -1;
                for (int a = 0; a < 15; a++)
                {
                    var u = B.Cells[a];
                    if (u.Empty || Defs.Heroes[u.Hero].Kind == AttackKind.Economy) continue;
                    if (weak < 0 || u.Level < B.Cells[weak].Level) weak = a;
                }
                if (weak >= 0 && B.Cells[weak].Level <= 1) B.Sell(weak);
            }
            // 5. los de poco alcance no van en el centro de arriba
            var mid = B.Cells[2];
            if (!mid.Empty && Defs.Heroes[mid.Hero].Range < 3.5f)
            {
                for (int a = 0; a < 15; a++)
                {
                    var u = B.Cells[a];
                    if (a == 2 || (!u.Empty && Defs.Heroes[u.Hero].Range < 3.5f)) continue;
                    if (u.Empty || !B.CanFuse(2, a)) { B.Drop(2, a); break; }
                }
            }
        }

        /// <summary>Que tan cerca de la pista esta el casillero (mas alto = mejor).</summary>
        static float Edge(int cell)
        {
            var p = Layout.CellPos(cell);
            float dx = Layout.PathX - Math.Abs(p.X), dy = p.Y - Layout.PathBottom;
            return -Math.Min(dx, dy);
        }

        void PickCard()
        {
            int best = 0; float bs = float.MinValue;
            for (int k = 0; k < 3; k++)
            {
                int i = B.Offer[k];
                if (i < 0) continue;
                var s = Skills.All[i];
                float sc = (int)s.Rarity * 2f;
                if (s.Id.Contains("dmg") || s.Id.Contains("spd") || s.Id == "fusion_poder") sc += 3f;
                if (s.Hero != null)
                {
                    int h = Defs.HeroIndex(s.Hero);
                    foreach (var u in B.Cells) if (!u.Empty && u.Hero == h) sc += u.Level;
                }
                if (s.Id == "vidas" && B.Lives < 12) sc += 6f;
                if (s.Id.StartsWith("invocar") || s.Id.StartsWith("chispas")) sc += B.WaveIndex < 4 ? 3f : 0f;
                if (sc > bs) { bs = sc; best = k; }
            }
            B.PickCard(best);
        }
    }
}
