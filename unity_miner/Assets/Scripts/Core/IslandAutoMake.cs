using System;
using System.Collections.Generic;

namespace Mineros.Core
{
    /// <summary>
    /// Produccion inteligente (auditoria final, oct-2026): los edificios de transformacion con lugar en la cola fabrican
    /// solos lo que falta para la proxima mejora (primero el Ayuntamiento) y nunca gastan insumos en algo que nadie pide.
    /// Antes el jugador tenia que planificar la cadena (hierro → lingote → carretilla) como en una planilla, y la
    /// simulacion del Core quedaba trabada en el Ayuntamiento 4 y 5. "El jugador decide, el juego hace lo dificil".
    /// Lo encolado a mano se respeta: esto solo llena los lugares libres.
    /// </summary>
    public sealed partial class Island
    {
        float autoMakeT;
        readonly int[] demand = new int[22];

        /// <summary>Lo que falta (unidades por recurso) para las mejoras que se pueden hacer con el Ayuntamiento actual.</summary>
        public int[] Demand() { return Demand(false); }

        /// <summary>`depotOnly`: solo lo del proximo Ayuntamiento (va primero: es lo que abre todo lo demas).</summary>
        int[] Demand(bool depotOnly)
        {
            Array.Clear(demand, 0, demand.Length);
            var dep = Plots[0];
            if (dep.Level < MaxTh) AddNeed(MatsFor(BKind.Depot, dep.Level + 1), 1);
            if (!depotOnly)
                foreach (var p in Plots)
                {
                    if (p.Building < 0 || p.Building == (int)BKind.Depot || p.Level < 1) continue;
                    if (p.Level >= LevelCap((BKind)p.Building)) continue;
                    AddNeed(MatsFor((BKind)p.Building, p.Level + 1), 1);
                }
            // lo que piden las recetas de lo que falta (un nivel: carretilla → lingote)
            for (int i = 0; i < Recipes.Length; i++)
            {
                var rc = Recipes[i];
                int want = demand[(int)rc.Out] - Stock[(int)rc.Out];
                if (want <= 0 || Find(rc.Kind) == null) continue;
                for (int j = 0; j < rc.In.Length; j++)
                    if (!RDef(rc.In[j]).Raw) demand[(int)rc.In[j]] += rc.InN[j] * ((want + rc.OutN - 1) / rc.OutN);
            }
            return demand;
        }

        void AddNeed(List<KeyValuePair<Res, int>> mats, int weight)
        {
            foreach (var kv in mats) demand[(int)kv.Key] += kv.Value * weight;
        }

        /// <summary>Unidades de `r` que ya vienen en camino (en colas o listas para cobrar).</summary>
        int Incoming(Res r)
        {
            int n = 0;
            foreach (var p in Plots)
            {
                if (p.ReadyRes == (int)r) n += p.Ready;
                foreach (int q in p.Queue) if (Recipes[q].Out == r) n += Recipes[q].OutN;
            }
            return n;
        }

        void TickAutoMake(float dt)
        {
            autoMakeT -= dt;
            if (autoMakeT > 0f) return;
            autoMakeT = 2f;
            AutoMake();
        }

        /// <summary>Llena los lugares libres de las colas con lo mas faltante que cada edificio pueda hacer.</summary>
        public int AutoMake()
        {
            var dd = (int[])Demand(true).Clone();
            var d = Demand(false);
            int queued = 0;
            foreach (var p in Plots)
            {
                if (p.Building < 0 || p.Level < 1 || p.Work > 0 || !Produces(p.Building)) continue;
                for (int guard = 0; guard < 4 && p.Queue.Count < QueueSlots(p); guard++)
                {
                    int best = -1; double bestScore = 0;
                    foreach (int rc in RecipesOf((BKind)p.Building))
                    {
                        var r = Recipes[rc];
                        double have = Stock[(int)r.Out] + Incoming(r.Out);
                        double needDep = dd[(int)r.Out] - have, need = d[(int)r.Out] - have;
                        if (need <= 0 || !CanQueue(p, rc)) continue;
                        // primero lo del Ayuntamiento (abre todo lo demas), despues lo mas faltante
                        double score = (needDep > 0 ? 1e6 + needDep : 0) + need;
                        if (score > bestScore) { bestScore = score; best = rc; }
                    }
                    if (best < 0 || !QueueRecipe(p, best)) break;
                    AddStat("automade", 1);
                    queued++;
                }
            }
            return queued;
        }
    }
}
