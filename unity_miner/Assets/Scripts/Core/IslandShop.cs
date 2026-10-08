using System;
using System.Collections.Generic;

namespace Mineros.Core
{
    /// <summary>Lugar donde se ofrece un anuncio con premio (biblia de produccion C.2).</summary>
    public enum AdPlace { OfflineX2 = 0, WorkCut = 1, Chest = 2, Merchant = 3, Spin = 4 }

    /// <summary>Producto de la tienda. Price es el precio de referencia en dolares (la tienda muestra el localizado).</summary>
    public sealed class ShopItem
    {
        public string Id, Name, Desc;
        public double Price;
        public bool Consumable = true, Subscription;
        public int Gems;
    }

    /// <summary>
    /// Tienda y anuncios con premio (docs/PRODUCCION_1_0.md seccion C): todo lo pago tambien se puede ganar jugando,
    /// los anuncios solo los elige el jugador y cada uno vale mas que el tiempo que cuesta mirarlo, con topes diarios.
    /// Las reglas viven aca (probadas); la vista solo habla con Unity IAP y AdMob.
    /// </summary>
    public sealed partial class Island
    {
        static ShopItem S(string id, string name, string desc, double price, int gems, bool consumable = true, bool sub = false)
        {
            return new ShopItem { Id = id, Name = name, Desc = desc, Price = price, Gems = gems, Consumable = consumable, Subscription = sub };
        }

        public static readonly ShopItem[] Shop =
        {
            S("starter", Loc.T("Oferta de inicio"), Loc.T("500 gemas, un constructor más para siempre y 30 minutos de Turbo x2"), 1.99, 500, false),
            S("gems_s", Loc.T("Puñado de gemas"), Loc.T("80 gemas"), 0.99, 80),
            S("gems_m", Loc.T("Bolsa de gemas"), Loc.T("500 gemas"), 4.99, 500),
            S("gems_l", Loc.T("Cofre de gemas"), Loc.T("1 200 gemas"), 9.99, 1200),
            S("gems_xl", Loc.T("Carro de gemas"), Loc.T("2 600 gemas"), 19.99, 2600),
            S("gems_xxl", Loc.T("Vagón de gemas"), Loc.T("7 000 gemas"), 49.99, 7000),
            S("gems_xxxl", Loc.T("Mina de gemas"), Loc.T("15 000 gemas"), 99.99, 15000),
            S("builder", Loc.T("Constructor extra"), Loc.T("Un constructor más para siempre"), 4.99, 0, false),
            S("pass", Loc.T("Pase dorado"), Loc.T("Desbloquea el carril dorado de la temporada"), 9.99, 0),
            S("piggy", Loc.T("Romper la alcancía"), Loc.T("Todas las gemas que juntó la alcancía"), 2.99, 0),
            S("capataz", Loc.T("Capataz"), Loc.T("Premios de anuncios sin anuncios, un constructor más y x2 sin conexión"), 4.99, 0, false, true),
        };

        public static ShopItem ShopOf(string id)
        {
            foreach (var s in Shop) if (s.Id == id) return s;
            return null;
        }

        // ------------------------------------------------------------ estado
        public readonly HashSet<string> Owned = new HashSet<string>();       // no consumibles comprados
        public readonly HashSet<string> FirstDone = new HashSet<string>();   // packs de gemas ya comprados (el primero vale x2)
        public int Piggy;                                                    // gemas en la alcancia
        public const int PiggyCap = 400, PiggyShow = 60;
        public bool Capataz;                                                 // suscripcion activa (la confirma la tienda)
        public event Action<ShopItem> Purchased;

        public bool PiggyReady { get { return Piggy >= PiggyShow; } }

        /// <summary>La alcancia se llena jugando (metas, vagones, barcos): se rompe pagando.</summary>
        public void FeedPiggy(int n) { Piggy = Math.Min(PiggyCap, Piggy + n); }

        /// <summary>Se puede comprar ahora (los no consumibles una sola vez; la alcancia con algo adentro).</summary>
        public bool CanOffer(string id)
        {
            var s = ShopOf(id);
            if (s == null) return false;
            if (!s.Consumable && !s.Subscription && Owned.Contains(id)) return false;
            if (id == "piggy" && !PiggyReady) return false;
            if (id == "pass" && PassGold) return false;
            return true;
        }

        /// <summary>Entrega lo comprado (lo llama la vista cuando la tienda confirma el pago). Devuelve el texto del premio.</summary>
        public string Grant(string id)
        {
            var s = ShopOf(id);
            if (s == null) return "";
            string got = "";
            switch (id)
            {
                case "starter":
                    if (Owned.Contains(id)) return "";
                    // todo fijo (nada al azar en lo que se paga): gemas, constructor y turbo
                    Gems += 500; BonusBuilders++; TurboT += 1800f;
                    got = Loc.T("+500 gemas, +1 constructor y 30 min de Turbo x2");
                    break;
                case "builder":
                    if (Owned.Contains(id)) return "";
                    BonusBuilders++;
                    got = Loc.T("+1 constructor para siempre");
                    break;
                case "pass":
                    PassGold = true;
                    got = Loc.T("¡Pase dorado activado!");
                    break;
                case "piggy":
                    got = "+" + Piggy + Loc.T(" gemas");
                    Gems += Piggy; Piggy = 0;
                    break;
                case "capataz":
                    Capataz = true;
                    got = Loc.T("¡Sos Capataz!");
                    break;
                default:
                    int g = s.Gems * (FirstDone.Contains(id) ? 1 : 2);   // la primera compra de cada pack vale doble
                    FirstDone.Add(id);
                    Gems += g;
                    got = "+" + g + Loc.T(" gemas");
                    break;
            }
            if (!s.Consumable || s.Subscription) Owned.Add(id);
            AddStat("purchases", 1);
            Purchased?.Invoke(s);
            return got;
        }

        /// <summary>Restaurar compras (obligatorio en iOS): vuelve a entregar los no consumibles que la tienda dice que tenes.</summary>
        public void Restore(string id)
        {
            var s = ShopOf(id);
            if (s == null || s.Consumable) return;
            if (s.Subscription) { Capataz = true; Owned.Add(id); return; }
            if (!Owned.Contains(id)) Grant(id);
        }

        // ------------------------------------------------------------ anuncios con premio
        public const int AdsPerDay = 12, GoldenTvAt = 5;
        public static readonly int[] AdCap = { 3, 6, 4, 3, 1 };              // por lugar y por dia
        public const double AdChestEvery = 4 * 3600, AdWorkCut = 1800;
        public int AdDay = -1;
        public readonly int[] AdToday = new int[5];
        public int AdTotalToday;
        public bool GoldenTvClaimed;
        public double AdChestAt;                                             // reloj del jugador (s) desde el que hay cofre

        void AdRollDay(int today)
        {
            if (AdDay == today) return;
            AdDay = today;
            for (int i = 0; i < AdToday.Length; i++) AdToday[i] = 0;
            AdTotalToday = 0;
            GoldenTvClaimed = false;
        }

        /// <summary>Se puede ofrecer el anuncio de ese lugar hoy.</summary>
        public bool CanAd(AdPlace place, int today, double now = 0)
        {
            AdRollDay(today);
            if (AdTotalToday >= AdsPerDay || AdToday[(int)place] >= AdCap[(int)place]) return false;
            if (place == AdPlace.Chest && now < AdChestAt) return false;
            return true;
        }

        /// <summary>
        /// Entrega el premio de un anuncio visto (o del Capataz, que no mira anuncios). `p` es la obra (WorkCut) y
        /// `amount` lo ganado sin conexion (OfflineX2). Devuelve el texto del premio, o "" si no correspondia.
        /// </summary>
        public string GrantAd(AdPlace place, int today, double now, Plot p = null, double amount = 0)
        {
            if (!CanAd(place, today, now)) return "";
            string got = "";
            switch (place)
            {
                case AdPlace.OfflineX2:
                    Earn(amount);
                    got = "x2: +" + BigNum.Fmt(amount);
                    break;
                case AdPlace.WorkCut:
                    if (p == null || p.Work <= 0) return "";
                    CutWork(p, AdWorkCut);
                    got = Loc.T("-30 min de obra");
                    break;
                case AdPlace.Chest:
                    GiveChest(1);
                    AdChestAt = now + AdChestEvery;
                    got = Loc.T("Cofre de plata");
                    break;
                case AdPlace.Merchant:
                    RefreshMerchant();
                    got = Loc.T("Mercader nuevo");
                    break;
                case AdPlace.Spin:
                    Spins++;
                    got = Loc.T("Giro gratis");
                    break;
            }
            AdToday[(int)place]++;
            AdTotalToday++;
            AddStat("ads", 1);
            // TV dorada: al quinto anuncio del dia, un cofre epico de regalo
            if (AdTotalToday >= GoldenTvAt && !GoldenTvClaimed) { GoldenTvClaimed = true; GiveChest(2); got += Loc.T(" · ¡TV dorada: cofre de oro!"); }
            return got;
        }

        // ------------------------------------------------------------ guardado
        Dictionary<string, object> ShopObj()
        {
            var owned = new List<object>(); foreach (var s in Owned) owned.Add(s);
            var first = new List<object>(); foreach (var s in FirstDone) first.Add(s);
            var ads = new List<object>(); foreach (var a in AdToday) ads.Add(a);
            return new Dictionary<string, object>
            {
                { "owned", owned }, { "first", first }, { "piggy", Piggy }, { "cap", Capataz ? 1 : 0 },
                { "adday", AdDay }, { "ads", ads }, { "adt", AdTotalToday }, { "tv", GoldenTvClaimed ? 1 : 0 }, { "chestAt", AdChestAt },
            };
        }

        void LoadShop(Dictionary<string, object> d)
        {
            Owned.Clear(); FirstDone.Clear();
            object o;
            if (d.TryGetValue("owned", out o) && o is List<object> ol) foreach (var x in ol) if (x is string sx) Owned.Add(sx);
            if (d.TryGetValue("first", out o) && o is List<object> fl) foreach (var x in fl) if (x is string sx) FirstDone.Add(sx);
            Piggy = JsonRead.Int(d, "piggy", 0);
            Capataz = JsonRead.Int(d, "cap", 0) == 1;
            AdDay = JsonRead.Int(d, "adday", -1);
            if (d.TryGetValue("ads", out o) && o is List<object> al)
                for (int i = 0; i < al.Count && i < AdToday.Length; i++) AdToday[i] = (int)JsonRead.ToDouble(al[i], 0);
            AdTotalToday = JsonRead.Int(d, "adt", 0);
            GoldenTvClaimed = JsonRead.Int(d, "tv", 0) == 1;
            AdChestAt = JsonRead.Dbl(d, "chestAt", 0);
        }
    }
}
