using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using Mineros.World;
using UnityEngine;
using FxApi = Mineros.Fx.Fx;
using Juice = Mineros.Fx.Juice;

namespace Mineros.IslandView
{
    /// <summary>
    /// Picar (biblia 3.1): toque con aplastado y destello, esquirlas, numero que sale del dedo (critico dorado), anillo
    /// en el dedo, carteles de combo que escalan de color, frenesi con combo x30, combo que se corta con un "pff".
    /// Vetas que nacen (la tierra se abulta y la veta asoma girando) y que se rompen (trozos en arco que se hunden,
    /// mancha de tierra y un brote de pasto donde estaba).
    /// </summary>
    public sealed partial class IslandGame
    {
        Vector2 lastTapScreen;
        Vector3 lastTapWorld;

        void TapOre(OreView v, Vector2 screen)
        {
            combo = Mathf.Min(combo + 1f, 40f);
            comboT = 1.1f;
            int c = (int)combo;
            double got = Isl.TapOre(v.O, combo);
            if (got < 0) return;
            bool crit = Isl.LastCrit, broke = Isl.LastBroke;
            lastTapScreen = screen;
            float sz = Island.Ores[v.O.Kind].Size * OreScale(v.O);
            Vector3 at = v.T.position + Vector3.up * (sz + 0.4f);
            lastTapWorld = at;
            // el objeto acusa el golpe: aplastado, destello blanco, esquirlas del color del mineral
            v.Squash = 1f;
            v.Flash = crit ? 0.09f : 0.06f;
            FxApi.Play("chips", v.T.position + Vector3.up * sz * 0.6f, IslandArt.OreCol[v.O.Kind], crit ? 1.2f : 0.8f);
            if (crit) FxApi.Play("hit_spark", v.T.position + Vector3.up * sz * 0.6f, new Color(1f, 0.85f, 0.3f), 1.4f);
            // el tono sube con el combo (+3 % por toque, tope +60 %)
            Sfx.Play("pick", -4f, 0.95f + Mathf.Min(combo, 20f) * 0.03f);
            // vocabulario de vibracion (19): toque 6 ms, rotura 12, critico 20
            if (crit) { Sfx.Play("crit", -6f, 1.1f); Juice.Vibrate(20); }
            else Juice.Vibrate(broke ? 12 : 6);
            Ui.TapRing(screen, ComboColor(c), c);
            // numero que sale del punto tocado (el dano en monedas si paga; si no, la marca del golpe)
            if (crit) Ui.PopupAt(screen, got > 0 ? "+" + BigNum.Fmt(got) : Loc.T("¡CRÍTICO!"), new Color(1f, 0.82f, 0.2f), 40, true);
            else if (got > 0 && !broke) Ui.PopupAt(screen, "+" + BigNum.Fmt(got), new Color(1f, 0.9f, 0.45f), 26, false);
            if ((c == 5 || c == 10 || c == 20 || c == Island.FrenzyCombo) && Isl.FrenzyT < Island.FrenzyTime - 0.01f)
            {
                Ui.ComboBanner(c, ComboColor(c));
                Sfx.Play("combo", -6f, 1f + c * 0.02f);
            }
            if (got > 0 && broke) Ui.CoinsFrom(at, got);
            if (broke && v.O.Kind == 4) Ui.FlyGemsFrom(screen, 1);
            else if (got > 0) Ui.FlyCoinsFrom(screen, 1);
        }

        /// <summary>Para pruebas: toca la veta `o` como si fuera el dedo (devuelve false si no hay vista).</summary>
        public bool DebugTapOre(Ore o)
        {
            OreView v;
            if (o == null || !ores.TryGetValue(o.Id, out v)) return false;
            float sz = Island.Ores[o.Kind].Size * OreScale(o);
            Vector2 sp = Cam.WorldToScreenPoint(v.T.position + Vector3.up * sz * 0.5f);
            TapOre(v, sp);
            return true;
        }

        /// <summary>Color del combo: blanco → naranja → rojo → arcoiris (x30).</summary>
        public static Color ComboColor(int c)
        {
            if (c >= Island.FrenzyCombo) return Color.HSVToRGB(Mathf.Repeat(Time.time * 0.8f, 1f), 0.65f, 1f);
            if (c >= 20) return new Color(1f, 0.32f, 0.28f);
            if (c >= 10) return new Color(1f, 0.6f, 0.2f);
            return Color.white;
        }

        /// <summary>El combo se corto: un "pff" de humito donde estaba el ultimo toque, sin castigo.</summary>
        void ComboEnded(int c)
        {
            if (c < 5) return;
            FxApi.Play("smoke", lastTapWorld, new Color(0.9f, 0.9f, 0.92f), 0.6f);
            Ui.PopupAt(lastTapScreen, "x" + c, new Color(0.85f, 0.85f, 0.9f), 26, false);
            Sfx.Play("whoosh", -16f, 0.6f);
        }

        void OnFrenzy()
        {
            Ui.Frenzy();
            Sfx.Play("powerup", -2f);
            Sfx.Play("event_start", -4f, 1.1f);
            Sfx.Duck(6f, 2f);
            DoublePulse();
        }

        // ------------------------------------------------------------ nacimiento de una veta
        static Mesh moundMesh;
        static Material[] moundMats;

        Transform MakeMound(Transform parent)
        {
            if (moundMesh == null)
            {
                var mb = new MeshBuilder();
                mb.Blob(Vector3.zero, new Vector3(0.62f, 0.3f, 0.62f), 1, 5, 0.18f, IslandGround.DirtD, IslandGround.Dirt, 0f, 0f);
                moundMesh = mb.ToMesh(null, "Monticulo", out moundMats);
                moundMats = VertexColorMerge.Apply(moundMesh, moundMats);
            }
            var r = IslandArt.MakeRenderer(parent.parent, "Monticulo", moundMesh, moundMats, false);
            r.transform.localPosition = parent.localPosition;
            r.transform.localScale = new Vector3(1f, 0f, 1f);
            Sfx.Play("rumble", -20f, 1.3f);
            return r.transform;
        }

        /// <summary>
        /// 0-0.3 s: la tierra se abulta (domo de 0.2 m) y salta tierra; 0.3-0.85 s: la veta asoma girando 20° y se asienta
        /// con aplastamiento; el domo se aplana y desaparece.
        /// </summary>
        void Emerge(OreView v, float size, out float y, out float sq)
        {
            float t = v.O.Age;
            y = -size; sq = 1f;
            if (v.Mound != null)
            {
                float m = t < 0.3f ? OutBack(t / 0.3f) : Mathf.Clamp01(1f - (t - 0.6f) / 0.6f);
                v.Mound.localScale = new Vector3(1f + (1f - m) * 0.3f, Mathf.Max(0.001f, m * 0.7f), 1f + (1f - m) * 0.3f);
                if (t >= 1.2f) { Destroy(v.Mound.gameObject); v.Mound = null; }
            }
            if (t < 0.3f) { if (v.Vis != null) v.Vis.localRotation = Quaternion.Euler(0, (v.O.Id * 73) % 360 - 20f, 0); return; }
            if (!v.Landed)
            {
                v.Landed = true;
                FxApi.Play("dust", v.T.position, new Color(0.75f, 0.6f, 0.42f), 1.0f);
                FxApi.Play("chips", v.T.position + Vector3.up * 0.2f, IslandGround.Dirt, 0.8f);
            }
            float a = Mathf.Clamp01((t - 0.3f) / 0.55f);
            y = Mathf.Lerp(-size, 0f, OutBack(a));
            sq = 1f + Mathf.Sin(a * Mathf.PI) * 0.28f - (a > 0.75f ? Mathf.Sin((a - 0.75f) / 0.25f * Mathf.PI) * 0.12f : 0f);
            if (v.Vis != null) v.Vis.localRotation = Quaternion.Euler(0, (v.O.Id * 73) % 360 - 20f * (1f - a), 0);
        }

        // ------------------------------------------------------------ trozos que vuelan, manchas y brotes
        sealed class Chunk { public Transform T; public Vector3 V; public float Age, Spin; public int Bounces; public Vector3 Axis; }
        readonly List<Chunk> chunks = new List<Chunk>();
        readonly Stack<Transform> chunkPool = new Stack<Transform>();
        static readonly Dictionary<int, Mesh> chunkMesh = new Dictionary<int, Mesh>();
        static readonly Dictionary<int, Material[]> chunkMats = new Dictionary<int, Material[]>();

        sealed class Mark { public Transform T; public float Age, Size, Life = 25f; public MeshRenderer R; public Transform Sprout; }
        readonly List<Mark> marks = new List<Mark>();
        static Mesh sproutMesh;
        static Material[] sproutMats;

        Transform ChunkObj(int kind)
        {
            Mesh m;
            if (!chunkMesh.TryGetValue(kind, out m))
            {
                var mb = new MeshBuilder();
                Color rock = kind == 4 ? IslandArt.H("5b5f6a") : IslandArt.Stone;
                mb.Blob(Vector3.zero, new Vector3(0.2f, 0.16f, 0.18f), 1, 9 + kind, 0.25f, IslandArt.StoneD, rock, 0f, 0f);
                if (kind > 0) mb.Crystal(new Vector3(0.05f, 0.05f, 0f), Vector3.up, 0.16f, 0.06f, 4, IslandArt.OreCol[kind], 0.4f);
                Material[] mats;
                m = mb.ToMesh(null, "Trozo" + kind, out mats);
                chunkMats[kind] = VertexColorMerge.Apply(m, mats);
                chunkMesh[kind] = m;
            }
            var r = IslandArt.MakeRenderer(root, "Trozo", m, chunkMats[kind], false);
            return r.transform;
        }

        /// <summary>La veta se parte en `n` trozos que salen en arco, rebotan una vez y se hunden en la tierra.</summary>
        void Debris(Vector3 c, int kind, int n, float scale = 1f)
        {
            for (int i = 0; i < n; i++)
            {
                var t = ChunkObj(kind);
                t.position = c + Random.insideUnitSphere * 0.2f * scale;
                t.localScale = Vector3.one * scale * Random.Range(0.8f, 1.25f);
                float a = (i + Random.value * 0.6f) / n * Mathf.PI * 2f;
                var v = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(1.4f, 2.6f) * scale + Vector3.up * Random.Range(3.2f, 4.6f);
                chunks.Add(new Chunk { T = t, V = v, Spin = Random.Range(200f, 520f), Axis = Random.onUnitSphere });
            }
        }

        void UpdateDebris(float dt)
        {
            for (int i = chunks.Count - 1; i >= 0; i--)
            {
                var c = chunks[i];
                c.Age += dt;
                if (c.T == null) { chunks.RemoveAt(i); continue; }
                if (c.Bounces < 2)
                {
                    c.V += Vector3.down * 14f * dt;
                    c.T.position += c.V * dt;
                    c.T.Rotate(c.Axis, c.Spin * dt, Space.World);
                    if (c.T.position.y < 0.06f && c.V.y < 0f)
                    {
                        c.T.position = new Vector3(c.T.position.x, 0.06f, c.T.position.z);
                        c.V = new Vector3(c.V.x * 0.45f, -c.V.y * 0.35f, c.V.z * 0.45f);
                        c.Spin *= 0.4f;
                        c.Bounces++;
                        if (c.Bounces == 1) FxApi.Play("tinydust", c.T.position, default(Color), 0.6f);
                        if (c.Bounces >= 2) c.Age = 0f;
                    }
                }
                else
                {
                    // se hunde en la tierra
                    c.T.position += Vector3.down * dt * 0.35f;
                    if (c.Age > 0.7f) { Destroy(c.T.gameObject); chunks.RemoveAt(i); }
                }
            }
            for (int i = marks.Count - 1; i >= 0; i--)
            {
                var m = marks[i];
                m.Age += dt;
                // la mancha de tierra se desvanece en 3 s; a los 2.5 s brota un mechon de pasto que dura un rato
                float a = Mathf.Clamp01(1f - m.Age / 3f) * 0.45f;
                if (m.R != null)
                {
                    var mpb = new MaterialPropertyBlock();
                    mpb.SetColor("_Color", new Color(0.25f, 0.16f, 0.07f, a));
                    m.R.SetPropertyBlock(mpb);
                }
                if (m.Age > 2.5f && m.Sprout == null) m.Sprout = MakeSprout(m.T.position, m.Size);
                if (m.Sprout != null)
                {
                    float k = m.Age - 2.5f;
                    float grow = k < 0.4f ? OutBack(k / 0.4f) : (m.Age > m.Life - 1f ? Mathf.Clamp01(m.Life - m.Age) : 1f);
                    m.Sprout.localScale = new Vector3(1f, Mathf.Max(0.001f, grow), 1f) * m.Size;
                    m.Sprout.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 2.1f + i) * 4f, m.Sprout.localEulerAngles.y, 0f);
                }
                if (m.Age > m.Life)
                {
                    if (m.T != null) Destroy(m.T.gameObject);
                    if (m.Sprout != null) Destroy(m.Sprout.gameObject);
                    marks.RemoveAt(i);
                }
            }
        }

        /// <summary>Flor que brota sola (despues de la lluvia, al evolucionar), tras `delay` segundos.</summary>
        void FlowerAt(Vector3 at, float delay, float life = 70f)
        {
            if (marks.Count > 40) return;
            var holder = new GameObject("Flor").transform;
            holder.SetParent(root, false);
            holder.position = new Vector3(at.x, 0f, at.z);
            marks.Add(new Mark { T = holder, Age = 2.5f - delay, Size = Random.Range(1.1f, 1.5f), Life = life });
        }

        void DirtMark(Vector3 at, float size)
        {
            if (marks.Count > 24) return;
            var t = IslandArt.Blob(root, 0.9f * Mathf.Max(0.6f, size), 0.45f);
            t.position = new Vector3(at.x, 0.03f, at.z);
            marks.Add(new Mark { T = t, R = t.GetComponent<MeshRenderer>(), Size = Mathf.Clamp(size * 1.4f, 0.7f, 1.6f) });
        }

        Transform MakeSprout(Vector3 at, float size)
        {
            if (sproutMesh == null)
            {
                var mb = new MeshBuilder();
                Color g = IslandArt.GrassD, l = IslandArt.H("a6d65a");
                for (int i = 0; i < 5; i++)
                {
                    float a = i / 5f * Mathf.PI * 2f + 0.3f;
                    var b = new Vector3(Mathf.Cos(a) * 0.07f, 0f, Mathf.Sin(a) * 0.07f);
                    var tip = b * 2.2f + Vector3.up * (0.26f + (i % 2) * 0.1f);
                    var side = Vector3.Cross(Vector3.up, b.normalized) * 0.045f;
                    mb.Tri(b - side, tip, b + side, (b.normalized + Vector3.up).normalized, i % 2 == 0 ? g : l, 0f);
                    mb.Tri(b + side, tip, b - side, (-b.normalized + Vector3.up).normalized, i % 2 == 0 ? g : l, 0f);
                }
                mb.Blob(new Vector3(0.02f, 0.3f, 0f), Vector3.one * 0.05f, 0, 3, 0f, IslandArt.H("ffe066"), IslandArt.H("ffd23a"), 0.2f, -1f);
                sproutMesh = mb.ToMesh(null, "Brote", out sproutMats);
                sproutMats = VertexColorMerge.Apply(sproutMesh, sproutMats);
            }
            var r = IslandArt.MakeRenderer(root, "Brote", sproutMesh, sproutMats, false);
            r.transform.position = new Vector3(at.x, 0.02f, at.z);
            r.transform.localRotation = Quaternion.Euler(0f, Random.value * 360f, 0f);
            r.transform.localScale = new Vector3(1f, 0.001f, 1f);
            return r.transform;
        }
    }
}
