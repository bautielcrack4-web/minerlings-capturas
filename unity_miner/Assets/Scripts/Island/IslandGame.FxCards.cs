using System.Collections;
using Mineros.Audio;
using Mineros.Core;
using UnityEngine;
using FxApi = Mineros.Fx.Fx;
using Juice = Mineros.Fx.Juice;

namespace Mineros.IslandView
{
    /// <summary>
    /// Cartas de efecto en el mundo: lo que pasa al jugar cada una (aterriza con un golpe de luz del color de la
    /// variante y despues su efecto: monedas que brotan, fuegos, flores, rocas que nacen brillando), las rocas de carta
    /// con su color y halo, y la melodia de la carta cuando se rompen.
    /// </summary>
    public sealed partial class IslandGame
    {
        void InitFxCards()
        {
            Isl.FxCardPlayed += OnFxCardPlayed;
            Isl.FancyBroken += OnFancyBroken;
            Isl.FxCardGot += id => Ui.FxCardGot(id);
        }

        /// <summary>Color y halo de una roca que salio de una carta (lo llama AddOre).</summary>
        void DressFancyOre(OreView v)
        {
            if (v.O.Fancy < 0) return;
            var c = Island.FxCatalog[v.O.Fancy];
            Color vc = FxCardArt.VariantColor(c.Variant);
            v.Aura = FxApi.Attach("aura", v.T, vc, 1.4f + c.Rarity * 0.3f);
            if (v.Rend != null)
            {
                var mpb = new MaterialPropertyBlock();
                v.Rend.GetPropertyBlock(mpb);
                mpb.SetColor("_EmissionColor", vc * 0.35f);
                v.Rend.SetPropertyBlock(mpb);
            }
            v.T.localScale = Vector3.one * OreScale(v.O);
        }

        void OnFxCardPlayed(FxCard c, float x, float z)
        {
            Vector3 at = new Vector3(x, 0.2f, z);
            Color vc = FxCardArt.VariantColor(c.Variant);
            FxApi.Play("unlock_burst", at, vc, 1.6f + c.Rarity * 0.3f);
            FxApi.Play("dust", at, new Color(0.85f, 0.75f, 0.6f), 1.4f);
            Sfx.Play("thud", -8f, 1.2f);
            switch ((Island.FxKind)c.Effect)
            {
                case Island.FxKind.Fountain: StartCoroutine(CoinFountain(at, vc)); break;
                case Island.FxKind.Fireworks: StartCoroutine(Fireworks(at, vc, 4 + c.Rarity * 2)); break;
                case Island.FxKind.Flowers:
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i / 12f * Mathf.PI * 2f;
                        FlowerAt(at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.8f, 0.05f * i, 90f);
                    }
                    FxApi.Play("sparkle", at + Vector3.up, vc, 1.6f);
                    Sfx.PlayLater("crystal_chime", 0.3f, -6f);
                    break;
                case Island.FxKind.Frenzy: Ui.Frenzy(); Sfx.Play("powerup", -2f); break;
                case Island.FxKind.Clover:
                    FxApi.Play("sparkle", at + Vector3.up, new Color(0.4f, 1f, 0.45f), 2f);
                    Cheer(at, 8f);
                    break;
                case Island.FxKind.RainbowRock: FxApi.Play("rays", at + Vector3.up, vc, 2f); break;
            }
        }

        IEnumerator CoinFountain(Vector3 at, Color vc)
        {
            for (int i = 0; i < 10; i++)
            {
                FxApi.Play("hit_spark", at + Vector3.up * 0.6f, new Color(1f, 0.85f, 0.3f), 1.2f);
                Sfx.Play("coin", -10f, 1f + i * 0.06f);
                Ui.FlyCoinsFrom(Cam.WorldToScreenPoint(at + Vector3.up * (0.6f + i * 0.08f)), 1);
                yield return new WaitForSeconds(0.12f);
            }
        }

        IEnumerator Fireworks(Vector3 at, Color vc, int n)
        {
            for (int i = 0; i < n; i++)
            {
                Vector3 p = at + new Vector3(Random.Range(-2f, 2f), Random.Range(3f, 5.5f), Random.Range(-2f, 2f));
                Color c = i % 2 == 0 ? vc : Color.HSVToRGB(Random.value, 0.6f, 1f);
                Sfx.Play("whoosh", -12f, 1.6f);
                yield return new WaitForSeconds(0.25f);
                FxApi.Play("unlock_burst", p, c, 1.6f);
                FxApi.Play("confetti", p, c, 1.2f);
                Sfx.Play("pop", -6f, 0.8f + Random.value * 0.4f);
                yield return new WaitForSeconds(0.15f);
            }
        }

        void OnFancyBroken(Ore o, double pay)
        {
            var c = Island.FxCatalog[o.Fancy];
            Vector3 at = new Vector3(o.X, 1f, o.Z);
            Color vc = FxCardArt.VariantColor(c.Variant);
            Sfx.PlayExact("cj_" + c.Id, -3f);
            FxApi.Play("confetti", at, vc, 1.6f);
            FxApi.Play("unlock_burst", at, vc, 1.4f);
            Ui.Popup(at + Vector3.up * 1.2f, "+" + BigNum.Fmt(pay), new Color(1f, 0.88f, 0.35f), 32);
            Ui.CoinsFrom(at, pay);
            if (o.FancyGems > 0) Ui.FlyGemsFrom(Cam.WorldToScreenPoint(at), o.FancyGems);
            if (c.Effect == (int)Island.FxKind.Pinata) for (int i = 0; i < 3; i++) FxApi.Play("confetti", at + Random.insideUnitSphere, Color.HSVToRGB(Random.value, 0.7f, 1f), 1.2f);
            Juice.Vibrate(25);
        }
    }
}
