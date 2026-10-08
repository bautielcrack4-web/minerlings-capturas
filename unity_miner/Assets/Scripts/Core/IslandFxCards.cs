using System;
using System.Collections.Generic;

namespace Mineros.Core
{
    /// <summary>Una carta de efecto del catalogo (se arma sola a partir de su numero).</summary>
    public sealed class FxCard
    {
        public int Id, Effect, Variant, Rarity;
        public string Name;
    }

    /// <summary>
    /// Cartas de efecto (pedido del dueño, 8-oct): variedad y sorpresa SIN tocar el progreso. 12 efectos x 20 variantes
    /// = 240 cartas unicas, cada una con su nombre, su color y su propia melodia (la vista la sintetiza con el numero).
    /// Se juegan sobre la isla: rocas especiales (oro, gema, piñata, arcoiris, misterio, mini veta), lluvia de
    /// meteoritos chicos, fuente de monedas, fuegos artificiales, anillo de flores, frenesi corto y trebol. Lo que pagan
    /// esta topado a segundos de ingreso, asi que no adelantan la economia. Salen de sobres (tienda, con gemas), de
    /// cofres a veces y la primera se regala al terminar el tutorial.
    /// </summary>
    public sealed partial class Island
    {
        public const int FxEffects = 12, FxVariants = 20, FxCount = FxEffects * FxVariants;
        public enum FxKind { GoldRock, GemRock, Pinata, Meteors, Fountain, Fireworks, Flowers, RainbowRock, Frenzy, Clover, MiniGiant, Mystery }

        static readonly string[] FxNames =
        {
            Loc.T("Roca de Oro"), Loc.T("Roca de Gema"), Loc.T("Piñata"), Loc.T("Meteoritos"), Loc.T("Fuente de Monedas"), Loc.T("Fuegos Artificiales"),
            Loc.T("Anillo de Flores"), Loc.T("Roca Arcoíris"), Loc.T("Frenesí"), Loc.T("Trébol"), Loc.T("Mini Veta"), Loc.T("Roca Misteriosa"),
        };
        /// <summary>Variantes: nombre y color (hex). Las ultimas son mas raras.</summary>
        public static readonly string[] FxVariantNames =
        {
            Loc.T("de Piedra"), Loc.T("Musgosa"), Loc.T("de Arena"), Loc.T("de Coral"), Loc.T("de Menta"), Loc.T("de Caramelo"),
            Loc.T("de Rubí"), Loc.T("de Esmeralda"), Loc.T("de Zafiro"), Loc.T("de Amatista"), Loc.T("de Escarcha"), Loc.T("de Lava"),
            Loc.T("Neón"), Loc.T("del Atardecer"), Loc.T("del Océano"), Loc.T("de Obsidiana"),
            Loc.T("Estelar"), Loc.T("Real"), Loc.T("Fantasma"), Loc.T("Dorada"),
        };
        public static readonly string[] FxVariantHex =
        {
            "9aa1aa", "6fae4a", "e3c27a", "ff8a73", "7de0b6", "ff7ab8",
            "e0344a", "2fbf6a", "2f6fe0", "9b59d6", "9fe3ff", "ff6a1f",
            "39ff9c", "ff9f45", "1fb3c9", "3a3446",
            "c9b8ff", "7b3fd6", "dff3ff", "ffcc33",
        };
        static int VariantRarity(int v) { return v >= 16 ? 3 : v >= 12 ? 2 : v >= 6 ? 1 : 0; }
        static readonly double[] FxRarityWeight = { 62, 27, 9, 2 };
        public static readonly string[] FxRarityName = { Loc.T("Común"), Loc.T("Rara"), Loc.T("Épica"), Loc.T("Legendaria") };

        static FxCard[] fxCatalog;
        public static FxCard[] FxCatalog
        {
            get
            {
                if (fxCatalog != null) return fxCatalog;
                fxCatalog = new FxCard[FxCount];
                for (int i = 0; i < FxCount; i++)
                {
                    int e = i / FxVariants, v = i % FxVariants;
                    fxCatalog[i] = new FxCard { Id = i, Effect = e, Variant = v, Rarity = VariantRarity(v), Name = Loc.En ? FxVariantNames[v] + " " + FxNames[e] : FxNames[e] + " " + FxVariantNames[v] };
                }
                return fxCatalog;
            }
        }

        /// <summary>Cuantas tiene de cada carta (0 = nunca la tuvo o las uso todas) y si alguna vez la vio (album).</summary>
        public readonly int[] FxOwned = new int[FxCount];
        public readonly bool[] FxSeen = new bool[FxCount];
        public int FxSeenCount { get { int n = 0; foreach (var s in FxSeen) if (s) n++; return n; } }
        public int FxHandCount { get { int n = 0; foreach (var s in FxOwned) n += s; return n; } }

        public event Action<int> FxCardGot;                    // carta nueva en la mano
        public event Action<FxCard, float, float> FxCardPlayed; // carta, x, z (la vista hace el espectaculo)
        public event Action<Ore, double> FancyBroken;          // roca de carta rota: lo que pago

        public void GiveFxCard(int id)
        {
            if (id < 0 || id >= FxCount) return;
            FxOwned[id]++;
            bool first = !FxSeen[id];
            FxSeen[id] = true;
            if (first) AddStat("fx_unique", 1);
            FxCardGot?.Invoke(id);
        }

        /// <summary>Una carta al azar segun las rarezas (`minRarity` sube el piso: sobres grandes).</summary>
        public int RollFxCard(int minRarity = 0)
        {
            double sum = 0; for (int r = minRarity; r < 4; r++) sum += FxRarityWeight[r];
            double x = rng.NextDouble() * sum;
            int rar = 3;
            for (int r = minRarity; r < 4; r++) { x -= FxRarityWeight[r]; if (x <= 0) { rar = r; break; } }
            var pool = new List<int>();
            foreach (var c in FxCatalog) if (c.Rarity == rar) { pool.Add(c.Id); if (!FxSeen[c.Id]) pool.Add(c.Id); }   // lo nuevo pesa doble
            return pool[rng.Next(pool.Count)];
        }

        // ------------------------------------------------------------ sobres
        public const int PackGems = 25, BigPackGems = 90;

        /// <summary>Abre un sobre: 3 cartas (el grande: 5, con una epica o mejor segura). Devuelve los ids, o null si no alcanza.</summary>
        public List<int> OpenPack(bool big, bool free = false)
        {
            int cost = big ? BigPackGems : PackGems;
            if (!free && Gems < cost) return null;
            if (!free) Gems -= cost;
            var got = new List<int>();
            int n = big ? 5 : 3;
            for (int i = 0; i < n; i++) got.Add(RollFxCard(big && i == n - 1 ? 2 : 0));
            got.Sort((a, b) => FxCatalog[a].Rarity.CompareTo(FxCatalog[b].Rarity));   // la mejor al final
            foreach (var id in got) GiveFxCard(id);
            AddStat(big ? "packs_big" : "packs", 1);
            return got;
        }

        // ------------------------------------------------------------ jugar una carta
        /// <summary>Monedas de un efecto: segundos de ingreso (con piso), para que nunca adelante la economia.</summary>
        double FxCoins(double secs) { return Math.Round(Math.Max(15.0, IncomePerSec() * secs)); }

        public bool PlayFxCard(int id, float x, float z)
        {
            if (id < 0 || id >= FxCount || FxOwned[id] <= 0) return false;
            var c = FxCatalog[id];
            FxOwned[id]--;
            AddStat("fx_played", 1);
            var k = (FxKind)c.Effect;
            if (k == FxKind.Mystery) k = (FxKind)rng.Next(0, FxEffects - 1);   // cualquiera de las otras
            float bonus = 1f + c.Rarity * 0.35f;   // las raras pagan un poco mas (sigue topado)
            switch (k)
            {
                case FxKind.GoldRock: FancyRock(c, x, z, 3, 8, 30 * bonus, 0); break;
                case FxKind.GemRock: FancyRock(c, x, z, 4, 10, 10 * bonus, 1); break;
                case FxKind.Pinata: FancyRock(c, x, z, 1, 6, 40 * bonus, 0); break;
                case FxKind.RainbowRock: FancyRock(c, x, z, 4, 12, 25 * bonus, 0); RainbowT = Math.Max(RainbowT, 30f); break;
                case FxKind.MiniGiant: FancyRock(c, x, z, 3, 40, 60 * bonus, 0); break;
                case FxKind.Meteors:
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i / 5f * 6.283f, r = 1.6f;
                        SpawnOreAt(x + (float)Math.Cos(a) * r, z + (float)Math.Sin(a) * r, 2, o => { o.Sky = true; o.Fancy = id; o.FancyPay = FxCoins(4 * bonus); });
                    }
                    break;
                case FxKind.Fountain: Earn(FxCoins(35 * bonus)); break;
                case FxKind.Fireworks: Earn(FxCoins(15 * bonus)); break;
                case FxKind.Flowers: Earn(FxCoins(10 * bonus)); break;
                case FxKind.Frenzy: FrenzyT = Math.Max(FrenzyT, 8f + 2f * c.Rarity); FrenzyStarted?.Invoke(); break;
                case FxKind.Clover: Earn(FxCoins(20 * bonus)); if (rng.NextDouble() < 0.15 + 0.1 * c.Rarity) Gems += 1; break;
            }
            FxCardPlayed?.Invoke(c, x, z);
            return true;
        }

        /// <summary>Roca de carta: nace en el punto, se ve con el color de la carta y paga al romperse (toques o mineros).</summary>
        void FancyRock(FxCard c, float x, float z, int kind, double taps, double paySecs, int gems)
        {
            SpawnOreAt(x, z, kind, o =>
            {
                o.Fancy = c.Id; o.FancyPay = FxCoins(paySecs); o.FancyGems = gems;
                o.MaxHp = o.Hp = Math.Max(1.0, PickPower() * 0.6) * taps;
            });
        }

        /// <summary>Veta en un punto (el mas cercano libre).</summary>
        Ore SpawnOreAt(float x, float z, int kind, Action<Ore> setup)
        {
            for (int t = 0; t < 30; t++)
            {
                float r = t * 0.25f, a = t * 2.4f;
                float px = x + (float)Math.Cos(a) * r, pz = z + (float)Math.Sin(a) * r;
                if (px * px + pz * pz > (Radius - 1f) * (Radius - 1f)) continue;
                if (!FreeSpot(px, pz, 0.9f)) continue;
                var o = new Ore { Id = nextOre++, Kind = kind, X = px, Z = pz, MaxHp = Ores[kind].Hp, Hp = Ores[kind].Hp, Age = 0f };
                setup?.Invoke(o);
                OreList.Add(o);
                OreSpawned?.Invoke(o);
                return o;
            }
            return null;
        }

        /// <summary>Premio de una roca de carta (lo llaman el toque y el minero al romperla).</summary>
        void FancyBroke(Ore o)
        {
            if (o.Fancy < 0 || o.FancyPaid) return;
            o.FancyPaid = true;
            Earn(o.FancyPay);
            Gems += o.FancyGems;
            AddStat("fx_rocks", 1);
            FancyBroken?.Invoke(o, o.FancyPay);
        }

        // ------------------------------------------------------------ guardado
        object FxObj()
        {
            var own = new List<object>(); var seen = new List<object>();
            for (int i = 0; i < FxCount; i++) { if (FxOwned[i] > 0) { own.Add(i); own.Add(FxOwned[i]); } if (FxSeen[i]) seen.Add(i); }
            return new Dictionary<string, object> { { "own", own }, { "seen", seen } };
        }

        void ReadFx(Dictionary<string, object> d)
        {
            Array.Clear(FxOwned, 0, FxCount); Array.Clear(FxSeen, 0, FxCount);
            object fo;
            if (!d.TryGetValue("fx", out fo) || !(fo is Dictionary<string, object> f)) return;
            object x;
            if (f.TryGetValue("own", out x) && x is List<object> own)
                for (int i = 0; i + 1 < own.Count; i += 2) { int id = (int)JsonRead.ToDouble(own[i], -1), n = (int)JsonRead.ToDouble(own[i + 1], 0); if (id >= 0 && id < FxCount) FxOwned[id] = n; }
            if (f.TryGetValue("seen", out x) && x is List<object> seen)
                foreach (var s in seen) { int id = (int)JsonRead.ToDouble(s, -1); if (id >= 0 && id < FxCount) FxSeen[id] = true; }
        }
    }
}
