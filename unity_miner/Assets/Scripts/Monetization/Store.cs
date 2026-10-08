using System;
using System.Collections.Generic;
using Mineros.Core;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Mineros.Monetization
{
    /// <summary>
    /// Compras dentro del juego (Unity IAP 5, Google Play Billing 9 / StoreKit). Los productos y lo que entrega cada
    /// uno estan en el nucleo (Island.Shop / Island.Grant); aca solo se habla con la tienda: precios localizados, compra,
    /// confirmacion despues de entregar y restaurar (obligatorio en iOS). En el editor se simula y entrega al instante.
    /// </summary>
    public static class Store
    {
        static StoreController store;
        static bool started, connected;
        /// <summary>Por que no conecto la tienda (se muestra entre parentesis, sirve para diagnosticar en el telefono).</summary>
        public static string LastError = "";
        static readonly Dictionary<string, Action<bool, string>> waiting = new Dictionary<string, Action<bool, string>>();

        /// <summary>La tienda confirmo un pago: el juego entrega el producto (y devuelve el texto del premio).</summary>
        public static Func<string, string> Grant;
        /// <summary>La tienda dice que el usuario ya tenia este producto (restaurar o reinstalar).</summary>
        public static Action<string> Restored;
        /// <summary>La suscripcion no aparece entre lo comprado (vencio o se cancelo).</summary>
        public static Action<string> SubscriptionLapsed;

        public static bool Simulated { get { return Application.isEditor; } }
        public static bool Ready { get { return Simulated || connected; } }

        public static async void Init()
        {
            if (started) return;
            started = true;
            if (Simulated) return;
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
                // los eventos se enganchan una sola vez (al reintentar no hay que duplicarlos: una compra se entregaria dos veces)
                if (store == null) Hook(UnityIAPServices.StoreController());
                await store.Connect();
                FetchAll();
            }
            catch (Exception e) { LastError = e.Message; started = false; Debug.LogWarning("tienda: no conecto " + e.Message); }
        }

        static void Hook(StoreController s)
        {
            store = s;
            store.OnPurchasePending += OnPending;
            store.OnPurchaseFailed += OnFailed;
            store.OnPurchasesFetched += OnFetched;
            store.OnProductsFetched += p => { connected = true; LastError = ""; store.FetchPurchases(); };
            store.OnProductsFetchFailed += f => { LastError = "products " + f.FailureReason; Debug.LogWarning("tienda: productos " + f.FailureReason); };
            store.OnStoreDisconnected += d => { connected = false; LastError = "disconnected " + d.message; started = false; };
        }

        static void FetchAll()
        {
            var defs = new List<ProductDefinition>();
            foreach (var s in Island.Shop)
                defs.Add(new ProductDefinition(s.Id, s.Subscription ? ProductType.Subscription : s.Consumable ? ProductType.Consumable : ProductType.NonConsumable));
            store.FetchProducts(defs);
        }

        /// <summary>Precio localizado de la tienda ("$ 1.999,00", "US$ 1,99"...), o el de referencia si todavia no cargo.</summary>
        public static string Price(string id)
        {
            var s = Island.ShopOf(id);
            if (!Simulated && store != null)
            {
                var p = store.GetProductById(id);
                if (p != null && p.metadata != null && !string.IsNullOrEmpty(p.metadata.localizedPriceString)) return p.metadata.localizedPriceString;
            }
            return s != null ? "US$ " + s.Price.ToString("0.00") : "";
        }

        public static void Buy(string id, Action<bool, string> done)
        {
            if (Simulated)
            {
                string got = Grant != null ? Grant(id) : "";
                done?.Invoke(true, got);
                return;
            }
            if (!connected || store == null)
            {
                // reintenta conectar (sin red al abrir, Play actualizandose...) y avisa con el motivo
                if (!started) Init();
                string why = string.IsNullOrEmpty(LastError) ? "" : " (" + LastError + ")";
                done?.Invoke(false, Loc.T("La tienda no está disponible ahora") + why);
                return;
            }
            waiting[id] = done;
            store.PurchaseProduct(id);
        }

        public static void Restore(Action<bool> done)
        {
            if (Simulated || store == null) { done?.Invoke(Simulated); return; }
            store.RestoreTransactions((ok, err) => { if (ok) store.FetchPurchases(); done?.Invoke(ok); });
        }

        static string IdOf(UnityEngine.Purchasing.Order o)
        {
            foreach (var item in o.CartOrdered.Items()) if (item.Product != null && item.Product.definition != null) return item.Product.definition.id;
            return null;
        }

        /// <summary>Pago aprobado: primero se entrega, despues se confirma (si la app se cierra en el medio, vuelve a llegar).</summary>
        static void OnPending(PendingOrder order)
        {
            string id = IdOf(order);
            string got = id != null && Grant != null ? Grant(id) : "";
            store.ConfirmPurchase(order);
            Action<bool, string> cb;
            if (id != null && waiting.TryGetValue(id, out cb)) { waiting.Remove(id); cb?.Invoke(true, got); }
        }

        static void OnFailed(FailedOrder order)
        {
            string id = IdOf(order);
            Action<bool, string> cb;
            if (id != null && waiting.TryGetValue(id, out cb))
            {
                waiting.Remove(id);
                bool cancelled = order.FailureReason == PurchaseFailureReason.UserCancelled;
                cb?.Invoke(false, cancelled ? "" : Loc.T("No se pudo completar la compra"));
            }
        }

        static void OnFetched(Orders orders)
        {
            bool sub = false;
            foreach (var o in orders.ConfirmedOrders)
            {
                string id = IdOf(o);
                if (id == null) continue;
                var s = Island.ShopOf(id);
                if (s == null || s.Consumable) continue;
                if (s.Subscription) sub = true;
                Restored?.Invoke(id);
            }
            foreach (var o in orders.PendingOrders) OnPending(o);
            if (!sub) SubscriptionLapsed?.Invoke("capataz");
        }
    }
}
