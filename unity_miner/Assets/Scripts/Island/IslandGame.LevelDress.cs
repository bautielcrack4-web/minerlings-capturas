using System.Collections;
using Mineros.Audio;
using Mineros.Core;
using Mineros.World;
using UnityEngine;
using FxApi = Mineros.Fx.Fx;
using Juice = Mineros.Fx.Juice;

namespace Mineros.IslandView
{
    /// <summary>
    /// Cada nivel se ve (pedido del dueño: "mejoro la casa al 2 y queda igual"). Ademas de las etapas grandes (3, 6, 9),
    /// cada nivel suma UNA pieza fija delante del edificio, siempre en el mismo orden, para que el antes/despues se lea
    /// de un vistazo: farol, banco, macetas, cartel con estrella, segundo farol, banderines, camino de piedras, barriles
    /// y, desde el 10, tachas doradas alrededor. Lo de niveles anteriores va horneado junto; la pieza del nivel actual va
    /// aparte (Nv) para que la ceremonia de nivel la haga aparecer.
    /// </summary>
    public sealed partial class IslandGame
    {
        static readonly Color LampC = IslandArt.H("ffd77a"), Pennant1 = IslandArt.H("e0594a"), Pennant2 = IslandArt.H("4aa3f0"), Pennant3 = IslandArt.H("ffd23a");
        static readonly Color GoldDress = IslandArt.H("f2c230"), Flower1 = IslandArt.H("ff6f91"), Flower2 = IslandArt.H("ffd23a");

        /// <summary>Medio ancho del edificio en metros (para poner las piezas en sus esquinas de adelante).</summary>
        float DressR(PlotView v)
        {
            if (v.Model == null) return 1.3f;
            var e = v.Model.bounds.extents;
            return Mathf.Clamp(Mathf.Max(e.x, e.z) * 0.78f, 1.0f, 2.2f);
        }

        void LevelDress(PlotView v)
        {
            var k = (BKind)v.P.Building;
            if (k == BKind.Dock || k == BKind.Depot || k == BKind.Barracks) return;
            int lv = v.P.Level;
            if (lv < 2) return;
            float R = DressR(v);
            var holder = new GameObject("Adornos").transform;
            holder.SetParent(v.Body, false);
            holder.position = v.Root.position;
            holder.rotation = Quaternion.Euler(0f, 35f, 0f);   // frente (-Z) hacia la camara
            var old = new MeshBuilder();
            for (int l = 2; l < lv; l++) DressPiece(old, l, R);
            if (old.TriCount > 0) IslandArt.Bake(old, holder, "Viejos", true);
            var cur = new MeshBuilder();
            DressPiece(cur, lv, R);
            if (cur.TriCount > 0)
            {
                var r = IslandArt.Bake(cur, holder, "Nv" + lv, true);
                r.gameObject.name = "Nv" + lv;
            }
        }

        /// <summary>Pieza del nivel `l` (coordenadas del marco del edificio: -Z = frente, hacia la camara).</summary>
        static void DressPiece(MeshBuilder mb, int l, float R)
        {
            float fz = -R - 0.25f;   // linea de adelante
            switch (l)
            {
                case 2: DressLamp(mb, new Vector3(-R, 0f, fz + 0.2f), false); break;
                case 3:   // banco
                    mb.Box(new Vector3(R * 0.62f, 0.26f, fz - 0.05f), new Vector3(0.7f, 0.07f, 0.24f), IslandArt.Wood, 0f);
                    mb.Box(new Vector3(R * 0.62f, 0.42f, fz + 0.06f), new Vector3(0.7f, 0.2f, 0.05f), IslandArt.Wood, 0f);
                    mb.Box(new Vector3(R * 0.62f - 0.28f, 0.12f, fz - 0.05f), new Vector3(0.06f, 0.24f, 0.2f), IslandArt.WoodD, 0f);
                    mb.Box(new Vector3(R * 0.62f + 0.28f, 0.12f, fz - 0.05f), new Vector3(0.06f, 0.24f, 0.2f), IslandArt.WoodD, 0f);
                    break;
                case 4:   // dos macetas con flores
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 c = new Vector3(s * R * 0.32f, 0f, fz + 0.05f);
                        mb.Box(c + Vector3.up * 0.13f, new Vector3(0.42f, 0.26f, 0.24f), IslandArt.WoodD, 0f);
                        for (int i = 0; i < 4; i++)
                            mb.Octa(c + new Vector3(-0.15f + i * 0.1f, 0.32f + (i % 2) * 0.05f, 0f), new Vector3(0.08f, 0.08f, 0.08f), i % 2 == 0 ? Flower1 : Flower2, 0.15f);
                        mb.Box(c + Vector3.up * 0.27f, new Vector3(0.38f, 0.04f, 0.2f), IslandArt.GrassD, 0f);
                    }
                    break;
                case 5:   // cartel con estrella dorada
                {
                    Vector3 c = new Vector3(R + 0.15f, 0f, fz + 0.55f);
                    mb.Cyl(c, c + Vector3.up * 1.0f, 0.045f, 0.045f, 6, IslandArt.WoodD, 0f);
                    mb.Box(c + new Vector3(0f, 0.95f, -0.04f), new Vector3(0.62f, 0.38f, 0.06f), IslandArt.Wood, 0f);
                    mb.Octa(c + new Vector3(0f, 0.95f, -0.1f), new Vector3(0.16f, 0.16f, 0.05f), GoldDress, 0.45f);
                    break;
                }
                case 6: DressLamp(mb, new Vector3(R, 0f, fz + 0.2f), false); break;
                case 7:   // banderines entre los dos faroles
                {
                    Vector3 a = new Vector3(-R, 1.12f, fz + 0.2f), b = new Vector3(R, 1.12f, fz + 0.2f);
                    const int n = 9;
                    Color[] cols = { Pennant1, Pennant2, Pennant3 };
                    for (int i = 0; i < n; i++)
                    {
                        float u = (i + 0.5f) / n;
                        Vector3 p = Vector3.Lerp(a, b, u) + Vector3.down * Mathf.Sin(u * Mathf.PI) * 0.22f;
                        Vector3 w = (b - a).normalized * 0.09f;
                        mb.Tri(p - w, p + w, p + Vector3.down * 0.2f, Vector3.back, cols[i % 3], 0.05f);
                        mb.Tri(p + w, p - w, p + Vector3.down * 0.2f, Vector3.forward, cols[i % 3], 0.05f);
                    }
                    break;
                }
                case 8:   // camino de piedras hasta la puerta
                    for (int i = 0; i < 3; i++)
                        mb.Cyl(new Vector3((i % 2) * 0.12f - 0.06f, 0f, fz - 0.15f - i * 0.42f), new Vector3((i % 2) * 0.12f - 0.06f, 0.05f, fz - 0.15f - i * 0.42f), 0.2f, 0.18f, 8, IslandArt.Stone, 0f);
                    break;
                case 9:   // barriles y cajon
                {
                    Vector3 c = new Vector3(-R - 0.25f, 0f, fz + 0.75f);
                    mb.Cyl(c, c + Vector3.up * 0.42f, 0.17f, 0.17f, 10, IslandArt.Wood, 0f);
                    mb.Cyl(c + new Vector3(0.36f, 0f, 0.05f), c + new Vector3(0.36f, 0.38f, 0.05f), 0.15f, 0.15f, 10, IslandArt.WoodD, 0f);
                    mb.Box(c + new Vector3(0.12f, 0.58f, 0f), new Vector3(0.3f, 0.3f, 0.3f), IslandArt.Wood, 0f);
                    break;
                }
                default:   // 10+: tachas doradas alrededor (4 por nivel)
                {
                    int n = Mathf.Min(16, (l - 9) * 4);
                    for (int i = 0; i < 4 && (l - 10) * 4 + i < n; i++)
                    {
                        int idx = (l - 10) * 4 + i;
                        float a = idx / 16f * Mathf.PI * 2f + 0.2f;
                        Vector3 c = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (R + 0.55f);
                        mb.Cyl(c, c + Vector3.up * 0.18f, 0.08f, 0.06f, 8, GoldDress, 0.35f);
                        mb.Octa(c + Vector3.up * 0.24f, new Vector3(0.08f, 0.08f, 0.08f), GoldDress, 0.5f);
                    }
                    break;
                }
            }
        }

        static void DressLamp(MeshBuilder mb, Vector3 c, bool gold)
        {
            mb.Cyl(c, c + Vector3.up * 1.15f, 0.05f, 0.04f, 6, gold ? GoldDress : IslandArt.Dark, 0f);
            mb.Box(c + Vector3.up * 1.2f, new Vector3(0.2f, 0.22f, 0.2f), LampC, 0.9f);
            mb.Box(c + Vector3.up * 1.34f, new Vector3(0.26f, 0.06f, 0.26f), IslandArt.Dark, 0f);
        }

        /// <summary>
        /// Ceremonia de nivel (sin cambio de etapa), ~1.6 s: el edificio se agacha y junta luz (anticipacion), salta y
        /// se estira, cae con rebote; un anillo de luz recorre el suelo, la pieza nueva del nivel aparece con rebote y
        /// destellos y una nota que sube con el nivel; los mineros cercanos festejan.
        /// </summary>
        IEnumerator LevelUpCeremony(PlotView v)
        {
            v.Driven = true;
            Vector3 c = v.Root.position;
            var body = v.Body;
            // 1) anticipacion: se agacha y brilla, la luz se junta
            StartCoroutine(Suction(c + Vector3.up * v.Height * 0.5f, 0.55f));
            Sfx.Play("magic_rise", -8f, 1.2f + v.P.Level * 0.02f);
            for (float t = 0f; t < 0.45f; t += Time.deltaTime)
            {
                if (body == null) break;
                float u = t / 0.45f;
                float e = u * u;
                body.localScale = new Vector3(1f + 0.06f * e, 1f - 0.12f * e, 1f + 0.06f * e);
                SetGlow(v, e * 0.55f);
                yield return null;
            }
            // 2) cambia (pieza nueva, un poco mas grande) en el punto mas bajo y salta
            RefreshPlot(v);
            body = v.Body;
            Transform piece = body != null ? FindDeep(body, "Nv" + v.P.Level) : null;
            if (piece != null) piece.localScale = Vector3.zero;
            FxApi.Play("unlock_burst", c + Vector3.up * 0.3f, new Color(1f, 0.9f, 0.5f), 1.8f);
            FxApi.Play("levelup_aura", c, new Color(1f, 0.9f, 0.4f), 1.8f);
            Sfx.Play("upgrade", -2f, 0.95f + Mathf.Min(v.P.Level, 15) * 0.025f);
            Juice.Vibrate(25);
            Cheer(c, 6f);
            for (float t = 0f; t < 0.7f; t += Time.deltaTime)
            {
                float u = t / 0.7f;
                if (body != null)
                {
                    float st = u < 0.3f ? Mathf.Lerp(0.88f, 1.16f, OutBack(u / 0.3f)) : Mathf.Lerp(1.16f, 1f, OutBack((u - 0.3f) / 0.7f));
                    body.localScale = new Vector3(1f / Mathf.Sqrt(st), st, 1f / Mathf.Sqrt(st));
                    SetGlow(v, Mathf.Lerp(0.55f, 0f, u));
                }
                yield return null;
            }
            if (body != null) body.localScale = Vector3.one;
            SetGlow(v, 0f);
            // 3) la pieza nueva aparece con rebote y destellos
            if (piece != null)
            {
                Vector3 at = piece.GetComponent<Renderer>() != null ? piece.GetComponent<Renderer>().bounds.center : c;
                FxApi.Play("sparkle", at, new Color(1f, 0.95f, 0.6f), 1.3f);
                Sfx.Play("crystal_chime", -6f, 1f + Mathf.Min(v.P.Level, 15) * 0.03f);
                for (float t = 0f; t < 0.45f; t += Time.deltaTime)
                {
                    if (piece == null) break;
                    piece.localScale = Vector3.one * Mathf.Max(0.001f, OutBack(t / 0.45f));
                    yield return null;
                }
                if (piece != null) piece.localScale = Vector3.one;
            }
            Ui.Popup(c + Vector3.up * (v.Height + 1f), Loc.T("Nivel ") + v.P.Level, new Color(1f, 0.92f, 0.45f), 34);
            v.Driven = false;
        }

        /// <summary>Para capturas: pone el nivel sin obra y redibuja.</summary>
        public void DebugSetLevel(Plot p, int lv) { p.Level = lv; p.Work = 0; var v = plots[p.Id]; v.ShownLevel = -1; RefreshPlot(v); }

        /// <summary>Para capturas: sube al nivel `lv` con la ceremonia.</summary>
        public void DebugLevelUp(Plot p, int lv) { p.Level = lv; p.Work = 0; StartCoroutine(LevelUpCeremony(plots[p.Id])); }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++) { var r = FindDeep(t.GetChild(i), name); if (r != null) return r; }
            return null;
        }
    }
}
