using System;
using System.Collections.Generic;

namespace Mineros.Core
{
    /// <summary>Lo que se deduce de la distribucion del Complejo. Se recalcula solo cuando algo cambia.</summary>
    public sealed class ComplexSummary
    {
        public readonly HashSet<int> Access = new HashSet<int>();           // modulos a los que se llega por puertas
        public readonly HashSet<int> Powered = new HashSet<int>();          // consumidores con energia
        public readonly HashSet<int> Unpowered = new HashSet<int>();        // consumidores sin energia
        public readonly HashSet<int> Loops = new HashSet<int>();            // rieles en circulo (no deberia pasar)
        public readonly Dictionary<int, int> Net = new Dictionary<int, int>();   // red de cable de cada modulo
        public readonly float[] CharBonus = new float[16];                 // +golpe por especialista (tope 0.6)
        public readonly Dictionary<long, double> CartBonus = new Dictionary<long, double>();   // (modulo, mineral) -> x
        public readonly List<int[]> Pairs = new List<int[]>();              // sinergias activas (ids) para dibujarlas
        public readonly bool[] Combos = new bool[Island.ComboCount];
        public readonly HashSet<int> ComboMods = new HashSet<int>();        // modulos que forman alguna combinacion
        public int Mood = 50;
        public float GlobalBonus;                                         // +golpe para todos (combinaciones)
        public int Lines;                                                 // lineas con final de venta
    }

    public sealed partial class Island
    {
        public const int ComboCount = 12;
        public static readonly string[] ComboNames =
        {
            Loc.T("Cámara experimental"), Loc.T("Forja legendaria"), Loc.T("Sala del tesoro"), Loc.T("Biblioteca"),
            Loc.T("Cocina minera"), Loc.T("Mirador"), Loc.T("Línea perfecta"), Loc.T("Cuarteto"),
            Loc.T("Taller de herreros"), Loc.T("Energía verde"), Loc.T("Topo en casa"), Loc.T("Noche eterna"),
        };
        /// <summary>Pistas cortas del libro (iconos en la vista).</summary>
        public static readonly string[] ComboHints =
        {
            "🟡 + 🔷 + 🧪", "⛓ 🟫 🔥 ⚫", "💎 + 🏦 + 🔒", "📚 / 🧪", "🍲 + 3 🛏", "🔭 ⬆ 🏠",
            "⛓⛓⛓⛓ → 🏦", "🛏🛏 / 🛏🛏", "🔨 + ⚙ + 🏠", "🌬 → 🧪", "🦔 + 🔩", "👻 / 🔭",
        };
        public readonly bool[] CombosFound = new bool[ComboCount];
        public event Action<int> ComboDiscovered;

        ComplexSummary summary;
        bool dirty = true;
        int summaryKey = -1;
        public void Dirty() { dirty = true; LayoutChanged?.Invoke(); }

        public ComplexSummary Summary
        {
            get
            {
                // lo que el resumen usa y puede cambiar sin tocar la distribucion (carbon, nivel, mineros, fiesta)
                int key = (FuelOk ? 1 : 0) | (BarracksLevel << 1) | (Miners.Count << 6) | (PartyT > 0 ? 1 << 14 : 0);
                if (dirty || summary == null || key != summaryKey) { summary = Compute(); dirty = false; summaryKey = key; AnnounceCombos(summary); }
                return summary;
            }
        }

        void AnnounceCombos(ComplexSummary s)
        {
            for (int i = 0; i < ComboCount; i++)
                if (s.Combos[i] && !CombosFound[i])
                {
                    CombosFound[i] = true;
                    AddStat("combos", 1);
                    ComboDiscovered?.Invoke(i);
                }
        }

        // ------------------------------------------------------------ vecinos
        /// <summary>Vecinos de un modulo: los 4 lados del mismo piso y el de arriba y el de abajo.</summary>
        public IEnumerable<Module> Neighbors(Module m)
        {
            for (int s = 0; s < 4; s++) { var n = ModAt(m.X + SideDX[s], m.Z + SideDZ[s], m.F); if (n != null) yield return n; }
            var up = ModAt(m.X, m.Z, m.F + 1); if (up != null) yield return up;
            if (m.F > 0) { var dn = ModAt(m.X, m.Z, m.F - 1); if (dn != null) yield return dn; }
        }

        bool Adjacent(Module a, Module b)
        {
            if (a.F == b.F) return Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z) == 1;
            return a.X == b.X && a.Z == b.Z && Math.Abs(a.F - b.F) == 1;
        }

        /// <summary>Sucesores por riel (mismo piso): el vecino que recibe en el lado opuesto.</summary>
        public List<Module> RailNext(Module m)
        {
            var l = new List<Module>(2);
            int rout = RotMask(MDef(m.Kind).RailOut, m.Rot);
            if (rout == 0) return l;
            for (int s = 0; s < 4; s++)
            {
                if ((rout & (1 << s)) == 0) continue;
                var nb = ModAt(m.X + SideDX[s], m.Z + SideDZ[s], m.F);
                if (nb == null) continue;
                if ((RotMask(MDef(nb.Kind).RailIn, nb.Rot) & (1 << ((s + 2) & 3))) != 0) l.Add(nb);
            }
            return l;
        }

        /// <summary>¿Agregar/mover este modulo cierra un circuito de rieles? (se prueba sin tocar el estado real).</summary>
        bool CreatesLoop(Module probe, Module self)
        {
            int sx = 0, sz = 0, sf = 0, sr = 0;
            if (self != null) { sx = self.X; sz = self.Z; sf = self.F; sr = self.Rot; self.X = probe.X; self.Z = probe.Z; self.F = probe.F; self.Rot = probe.Rot; }
            else Modules.Add(probe);
            var start = self ?? probe;
            bool loop = false;
            var stack = new Stack<Module>();
            var seen = new HashSet<int>();
            foreach (var n in RailNext(start)) stack.Push(n);
            while (stack.Count > 0 && !loop)
            {
                var c = stack.Pop();
                if (c == start) { loop = true; break; }
                if (!seen.Add(c.Id)) continue;
                foreach (var n in RailNext(c)) stack.Push(n);
            }
            if (self != null) { self.X = sx; self.Z = sz; self.F = sf; self.Rot = sr; }
            else Modules.Remove(probe);
            return loop;
        }

        // ------------------------------------------------------------ calculo
        ComplexSummary Compute()
        {
            var s = new ComplexSummary();
            if (Modules.Count == 0) return s;
            var central = FirstMod(ModKind.Central);
            // acceso: por puertas en el mismo piso; entre pisos por la Sala central y las escaleras
            if (central != null)
            {
                var q = new Queue<Module>();
                q.Enqueue(central); s.Access.Add(central.Id);
                while (q.Count > 0)
                {
                    var c = q.Dequeue();
                    foreach (var n in DoorNeighbors(c)) if (s.Access.Add(n.Id)) q.Enqueue(n);
                }
            }
            // energia: redes de cable (lados que se miran + vecinos de arriba/abajo con cable)
            var parent = new Dictionary<int, int>();
            Func<int, int> find = null;
            find = id => { int p; if (!parent.TryGetValue(id, out p) || p == id) return id; int r = find(p); parent[id] = r; return r; };
            foreach (var m in Modules) if (CableMaskOf(m) != 0) parent[m.Id] = m.Id;
            foreach (var m in Modules)
            {
                int cm = CableMaskOf(m);
                if (cm == 0) continue;
                for (int side = 0; side < 4; side++)
                {
                    if ((cm & (1 << side)) == 0) continue;
                    var n = ModAt(m.X + SideDX[side], m.Z + SideDZ[side], m.F);
                    if (n == null || (CableMaskOf(n) & (1 << ((side + 2) & 3))) == 0) continue;
                    parent[find(m.Id)] = find(n.Id);
                }
                var up = ModAt(m.X, m.Z, m.F + 1);
                if (up != null && CableMaskOf(up) != 0) parent[find(m.Id)] = find(up.Id);
            }
            var gen = new Dictionary<int, int>();     // red -> generadores con carbon
            var wind = new Dictionary<int, int>();    // red -> molinos
            var cons = new Dictionary<int, int>();    // red -> consumidores
            foreach (var m in Modules)
            {
                if (!parent.ContainsKey(m.Id)) continue;
                int r = find(m.Id);
                s.Net[m.Id] = r;
                int v;
                if (m.Kind == ModKind.Generator && FuelOk && !m.Broken && !Paused(m)) { gen.TryGetValue(r, out v); gen[r] = v + 1; }
                if (m.Kind == ModKind.Windmill) { wind.TryGetValue(r, out v); wind[r] = v + 1; }
                if (MDef(m.Kind).NeedsPower) { cons.TryGetValue(r, out v); cons[r] = v + 1; }
            }
            foreach (var m in Modules)
            {
                if (!MDef(m.Kind).NeedsPower) continue;
                int r; bool on = false;
                if (s.Net.TryGetValue(m.Id, out r))
                {
                    int g, w, c;
                    gen.TryGetValue(r, out g); wind.TryGetValue(r, out w); cons.TryGetValue(r, out c);
                    on = g > 0 || (w > 0 && c <= 2 * w);
                }
                if (on) s.Powered.Add(m.Id); else s.Unpowered.Add(m.Id);
            }
            // rieles en circulo (la colocacion lo impide; si una partida vieja lo trae, esa linea no corre)
            foreach (var m in Modules)
            {
                if (MDef(m.Kind).RailOut == 0) continue;
                var seen = new HashSet<int>();
                var st = new Stack<Module>();
                foreach (var n in RailNext(m)) st.Push(n);
                while (st.Count > 0)
                {
                    var c = st.Pop();
                    if (c == m) { s.Loops.Add(m.Id); break; }
                    if (!seen.Add(c.Id)) continue;
                    foreach (var n in RailNext(c)) st.Push(n);
                }
            }
            // sinergias por vecinos
            foreach (var m in Modules)
            {
                int ch = m.Kind == ModKind.Dorm ? m.Ch : m.Kind == ModKind.Central ? 0 : -1;
                if (ch < 0 || ch >= Roster.Length) continue;
                double k = m.Kind == ModKind.Dorm ? StageK(m) : 1.0;
                int spec = Roster[ch].Spec;
                foreach (var n in Neighbors(m))
                {
                    if (n.Kind == PreferredOf(ch) && Powered2(s, n))
                    {
                        s.CharBonus[ch] += (float)(PreferenceBonus(ch) * k);
                        s.Pairs.Add(new[] { m.Id, n.Id });
                    }
                    if (n.Kind == ModKind.Tools)
                    {
                        s.CharBonus[ch] += (float)(0.10 * StageK(n));
                        if (PreferredOf(ch) != ModKind.Tools) s.Pairs.Add(new[] { m.Id, n.Id });
                    }
                    if (spec >= 0 && ProcessesKind(n.Kind, spec))
                    {
                        long key = (long)n.Id * 16 + spec;
                        double cur; s.CartBonus.TryGetValue(key, out cur);
                        s.CartBonus[key] = Math.Max(cur, 1.15 + 0.05 * (k - 1.0) * 2);
                        if (n.Kind != PreferredOf(ch)) s.Pairs.Add(new[] { m.Id, n.Id });
                    }
                }
            }
            // combinaciones
            DetectCombos(s);
            if (s.Combos[7]) foreach (var id in s.ComboMods) { var m = ModById(id); if (m != null && m.Kind == ModKind.Dorm) s.CharBonus[m.Ch] += 0.10f; }
            if (s.Combos[8]) s.GlobalBonus += 0.05f;
            if (s.Combos[9]) s.CharBonus[5] += 0.10f;
            if (s.Combos[11]) s.CharBonus[10] += 0.30f;
            for (int i = 0; i < s.CharBonus.Length; i++) s.CharBonus[i] = Math.Min(0.6f, s.CharBonus[i]);
            foreach (var m in Modules) if (MDef(m.Kind).RailIn != 0 && RailNext(m).Count == 0 && (m.Kind == ModKind.Treasury || m.Kind == ModKind.Vault)) s.Lines++;
            s.Mood = ComputeMood(s);
            return s;
        }

        bool Powered2(ComplexSummary s, Module n) { return !MDef(n.Kind).NeedsPower || s.Powered.Contains(n.Id); }

        /// <summary>Modulo preferido de cada especialista (su dormitorio al lado: +20 %, el de Cristal +25 %).</summary>
        public static ModKind PreferredOf(int ch)
        {
            switch (ch)
            {
                case 0: return ModKind.Tools;
                case 1: return ModKind.Crusher;
                case 2: return ModKind.Smelter;
                case 3: return ModKind.Generator;
                case 4: return ModKind.Treasury;
                case 5: return ModKind.Lab;
                case 6: return ModKind.Vault;
                case 7: return ModKind.Drill;
                case 8: return ModKind.Classroom;
                case 9: return ModKind.Drill;
                case 10: return ModKind.Observatory;
                default: return ModKind.Trophy;
            }
        }

        public static double PreferenceBonus(int ch) { return ch == 5 ? 0.25 : 0.20; }

        /// <summary>El modulo trabaja sobre este mineral (para la sinergia "dormitorio vecino").</summary>
        public static bool ProcessesKind(ModKind k, int kind)
        {
            switch (k)
            {
                case ModKind.Storage: case ModKind.Treasury: return true;
                case ModKind.Crusher: return kind == OreStone || kind == OreCopper || kind == OreIron || kind == OreCoal;
                case ModKind.Smelter: return kind == OreCopper || kind == OreIron || kind == OreGold;
                case ModKind.Polisher: case ModKind.Vault: return kind == OreGem || kind == OreCrystal || kind == OreRare;
                default: return false;
            }
        }

        // ------------------------------------------------------------ combinaciones secretas
        bool IsDorm(Module m, int ch) { return m != null && m.Kind == ModKind.Dorm && m.Ch == ch; }

        Module Side(Module m, int side) { return ModAt(m.X + SideDX[side], m.Z + SideDZ[side], m.F); }

        void DetectCombos(ComplexSummary s)
        {
            foreach (var m in Modules)
            {
                switch (m.Kind)
                {
                    case ModKind.Lab:
                    {
                        // 0: Oro y Cristal a los costados del Laboratorio, en "L" (lados perpendiculares), con energia
                        if (s.Powered.Contains(m.Id))
                            for (int a = 0; a < 4; a++)
                            {
                                var p = Side(m, a); var q = Side(m, (a + 1) & 3);
                                if ((IsDorm(p, 4) && IsDorm(q, 5)) || (IsDorm(p, 5) && IsDorm(q, 4)))
                                { Mark(s, 0, m, p, q); break; }
                            }
                        // 3: Aula arriba o abajo del Laboratorio
                        var up = ModAt(m.X, m.Z, m.F + 1); var dn = m.F > 0 ? ModAt(m.X, m.Z, m.F - 1) : null;
                        if (up != null && up.Kind == ModKind.Classroom) Mark(s, 3, m, up);
                        if (dn != null && dn.Kind == ModKind.Classroom) Mark(s, 3, m, dn);
                        // 9: energia verde (molino en la red, sin generador)
                        int net;
                        if (s.Powered.Contains(m.Id) && s.Net.TryGetValue(m.Id, out net))
                        {
                            bool w = false, g = false;
                            foreach (var o in Modules)
                            {
                                int on;
                                if (!s.Net.TryGetValue(o.Id, out on) || on != net) continue;
                                if (o.Kind == ModKind.Windmill) w = true;
                                if (o.Kind == ModKind.Generator) g = true;
                            }
                            if (w && !g) Mark(s, 9, m);
                        }
                        break;
                    }
                    case ModKind.Smelter:
                        // 1: Hierro y Carbon a ambos lados de la Fundicion, en linea recta
                        for (int a = 0; a < 2; a++)
                        {
                            var p = Side(m, a); var q = Side(m, a + 2);
                            if ((IsDorm(p, 2) && IsDorm(q, 3)) || (IsDorm(p, 3) && IsDorm(q, 2))) { Mark(s, 1, m, p, q); break; }
                        }
                        break;
                    case ModKind.Vault:
                    {
                        // 2: Boveda junto a una Tesoreria y al Diamante
                        Module t = null, dd = null;
                        foreach (var n in Neighbors(m)) { if (n.Kind == ModKind.Treasury) t = n; if (IsDorm(n, 6)) dd = n; }
                        if (t != null && dd != null) Mark(s, 2, m, t, dd);
                        break;
                    }
                    case ModKind.Mess:
                    {
                        // 4: Comedor con 3 dormitorios alrededor
                        var ds = new List<Module>();
                        foreach (var n in Neighbors(m)) if (n.Kind == ModKind.Dorm) ds.Add(n);
                        if (ds.Count >= 3) { ds.Add(m); Mark(s, 4, ds.ToArray()); }
                        break;
                    }
                    case ModKind.Observatory:
                    {
                        // 5: Observatorio en la torre sobre la Sala central
                        var b1 = ModAt(m.X, m.Z, 1);
                        if (m.X == 0 && m.Z == 0 && b1 != null) Mark(s, 5, m, b1);
                        // 11: Fantasma justo debajo
                        var dn = ModAt(m.X, m.Z, m.F - 1);
                        if (IsDorm(dn, 10)) Mark(s, 11, m, dn);
                        break;
                    }
                    case ModKind.Unload:
                    {
                        // 6: linea con 4 o mas procesos que termina en Tesoreria
                        var path = TracePath(m, false);
                        int procs = 0;
                        foreach (var p in path) if (p.Kind == ModKind.Storage || p.Kind == ModKind.Crusher || p.Kind == ModKind.Smelter || p.Kind == ModKind.Polisher || p.Kind == ModKind.Experimental) procs++;
                        if (procs >= 4 && path.Count > 0 && path[path.Count - 1].Kind == ModKind.Treasury) { path.Add(m); Mark(s, 6, path.ToArray()); }
                        break;
                    }
                    case ModKind.Dorm:
                    {
                        // 7: cuatro dormitorios en cuadrado
                        var r = Side(m, 1); var u = Side(m, 0); var ru = r == null ? null : Side(r, 0);
                        if (r != null && u != null && ru != null && r.Kind == ModKind.Dorm && u.Kind == ModKind.Dorm && ru.Kind == ModKind.Dorm)
                            Mark(s, 7, m, r, u, ru);
                        // 10: el Topo junto al Taladro
                        if (m.Ch == 9) foreach (var n in Neighbors(m)) if (n.Kind == ModKind.Drill) Mark(s, 10, m, n);
                        break;
                    }
                    case ModKind.Tools:
                    {
                        // 8: Herramientas entre el Hierro y la Sala central (donde vive el de Piedra)
                        Module h = null, c = null;
                        foreach (var n in Neighbors(m)) { if (IsDorm(n, 2)) h = n; if (n.Kind == ModKind.Central) c = n; }
                        if (h != null && c != null) Mark(s, 8, m, h, c);
                        break;
                    }
                }
            }
        }

        static void Mark(ComplexSummary s, int combo, params Module[] mods)
        {
            s.Combos[combo] = true;
            foreach (var m in mods) if (m != null) s.ComboMods.Add(m.Id);
        }

        /// <summary>Recorrido de una vagoneta desde `start` (sin el inicio). `advance`: el Desvio alterna salidas.</summary>
        public List<Module> TracePath(Module start, bool advance)
        {
            var path = new List<Module>();
            var seen = new HashSet<int> { start.Id };
            var cur = start;
            for (int i = 0; i < 32; i++)
            {
                var next = RailNext(cur);
                if (next.Count == 0) break;
                Module n = next[0];
                if (next.Count > 1)
                {
                    n = next[cur.Splits % next.Count];
                    if (advance) cur.Splits++;
                }
                if (!seen.Add(n.Id)) break;
                path.Add(n);
                cur = n;
            }
            return path;
        }

        // ------------------------------------------------------------ animo
        int ComputeMood(ComplexSummary s)
        {
            if (BarracksLevel < 1) return 50;
            int score = 50;
            if (FirstMod(ModKind.Mess) != null) score += 15;
            if (s.Unpowered.Count == 0 && s.Powered.Count > 0) score += 10;
            foreach (var m in Modules) if (m.Broken) score -= 15;
            int deco = 0; foreach (var m in Modules) if (m.Deco > 0) deco++;
            score += Math.Min(20, deco * 2);
            if (Miners.Count > Modules.Count * 2 + 4) score -= 10;   // apretados
            if (PartyT > 0) score += 15;
            if (FirstMod(ModKind.PartyBell) != null) score += 10;
            if (s.Combos[4]) score += 15;
            return Math.Max(0, Math.Min(100, score));
        }

        public float PartyT;

        /// <summary>Animo -> velocidad: de -10 % a +10 % (sin Cuartel, neutro).</summary>
        public float MoodMult() { return BarracksLevel < 1 ? 1f : 0.9f + 0.2f * Summary.Mood / 100f; }

        /// <summary>Bono de golpe del especialista por su lugar en el Complejo (sinergias, preferencias, acceso).</summary>
        public float ComplexHit(Miner m)
        {
            if (Modules.Count == 0) return 1f;
            var s = Summary;
            int ch = Math.Max(0, Math.Min(s.CharBonus.Length - 1, m.Char));
            float k = 1f + s.CharBonus[ch] + s.GlobalBonus;
            var dorm = m.Char > 0 ? DormOf(m.Char) : null;
            if (dorm != null && !s.Access.Contains(dorm.Id)) k *= 0.8f;   // dormitorio sin acceso: -20 % (nunca bloquea)
            return k;
        }

        // ------------------------------------------------------------ vista previa
        /// <summary>
        /// Cuanto cambia el rendimiento del Complejo (en %) si el modulo `k` se pone en (x, z, f) con giro `rot`
        /// (o si `self` se mueve ahi). No toca el estado real: se prueba, se mide y se deshace.
        /// </summary>
        public double PreviewDelta(ModKind k, int ch, int x, int z, int f, int rot, Module self = null)
        {
            if (rot < 0 || !CanPlaceMod(k, x, z, f, rot, self)) return 0;
            double before = ComplexScore(Summary);
            int sx = 0, sz = 0, sf = 0, sr = 0;
            Module probe = null;
            if (self != null) { sx = self.X; sz = self.Z; sf = self.F; sr = self.Rot; self.X = x; self.Z = z; self.F = f; self.Rot = rot; }
            else { probe = new Module { Id = -9, Kind = k, Ch = ch, X = x, Z = z, F = f, Rot = rot }; Modules.Add(probe); }
            var tmp = Compute();
            double after = ComplexScore(tmp);
            if (self != null) { self.X = sx; self.Z = sz; self.F = sf; self.Rot = sr; }
            else Modules.Remove(probe);
            return before <= 0 ? (after > 0 ? 100 : 0) : (after / before - 1.0) * 100.0;
        }

        /// <summary>
        /// Con quien se conectaria el modulo si se suelta ahi: vecinos con sinergia y vecinos unidos por riel o cable
        /// (ids de modulos ya colocados). La vista los marca mientras se arrastra.
        /// </summary>
        public List<int> PreviewPartners(ModKind k, int ch, int x, int z, int f, int rot, Module self = null)
        {
            var res = new List<int>();
            if (rot < 0 || !CanPlaceMod(k, x, z, f, rot, self)) return res;
            int sx = 0, sz = 0, sf = 0, sr = 0;
            Module probe = null;
            if (self != null) { sx = self.X; sz = self.Z; sf = self.F; sr = self.Rot; self.X = x; self.Z = z; self.F = f; self.Rot = rot; }
            else { probe = new Module { Id = -9, Kind = k, Ch = ch, X = x, Z = z, F = f, Rot = rot }; Modules.Add(probe); }
            var me = self ?? probe;
            var tmp = Compute();
            foreach (var p in tmp.Pairs)
            {
                if (p[0] == me.Id && !res.Contains(p[1])) res.Add(p[1]);
                if (p[1] == me.Id && !res.Contains(p[0])) res.Add(p[0]);
            }
            foreach (var n in RailNext(me)) if (!res.Contains(n.Id)) res.Add(n.Id);
            foreach (var o in Modules) if (o != me && RailNext(o).Contains(me) && !res.Contains(o.Id)) res.Add(o.Id);
            int net;
            if (tmp.Net.TryGetValue(me.Id, out net))
                foreach (var n in Neighbors(me)) { int nn; if (tmp.Net.TryGetValue(n.Id, out nn) && nn == net && !res.Contains(n.Id)) res.Add(n.Id); }
            if (self != null) { self.X = sx; self.Z = sz; self.F = sf; self.Rot = sr; }
            else Modules.Remove(probe);
            return res;
        }

        /// <summary>Puntaje de rendimiento: golpe medio de los mineros por el multiplicador medio de las lineas.</summary>
        double ComplexScore(ComplexSummary s)
        {
            double hit = 0; int n = 0;
            foreach (var m in Miners)
            {
                int ch = Math.Max(0, Math.Min(s.CharBonus.Length - 1, m.Char));
                hit += 1.0 + s.CharBonus[ch] + s.GlobalBonus; n++;
            }
            if (n == 0) hit = 1; else hit /= n;
            double line = 1.0;
            foreach (var m in Modules)
            {
                if (m.Kind != ModKind.Unload) continue;
                double best = 1.0;
                for (int kind = 0; kind < Ores.Length; kind++) best = Math.Max(best, PathMult(m, kind, s));
                line = Math.Max(line, best);
            }
            return hit * (0.6 + 0.4 * line) * (0.9 + 0.2 * s.Mood / 100.0);
        }

        /// <summary>Multiplicador que gana un mineral recorriendo la linea desde `start` (sin avanzar los desvios).</summary>
        public double PathMult(Module start, int kind, ComplexSummary s = null)
        {
            s = s ?? Summary;
            double v = 1.0;
            foreach (var p in TracePath(start, false)) v *= StepMult(p, kind, s, false);
            return Math.Min(MaxLineMult, v);
        }

        public const double MaxLineMult = 4.0;
    }
}
