using System.Collections;
using Mineros.Audio;
using Mineros.Core;
using UnityEngine;
using FxApi = Mineros.Fx.Fx;
using Juice = Mineros.Fx.Juice;

namespace Mineros.IslandView
{
    /// <summary>
    /// Invocacion con carta (docs/PLAN_CARTAS.md): la carta soltada sobre la isla cae al suelo (THUMP, polvo), se carga
    /// de luz, estalla con la entrada propia de cada minero (el de oro entre monedas y destellos, el de piedra rompiendo
    /// roca, el de cristal entre esquirlas, el de diamante con un fogonazo...) y el minero sale de ella estirandose; la carta
    /// se deshace y el suelo queda marcado unos segundos. Despues el minero sale caminando a su veta: ya se entiende que hace.
    /// </summary>
    public sealed partial class IslandGame
    {
        /// <summary>La carta que cae: donde, su dibujo y su material. La pone la interfaz antes de Recruit.</summary>
        public sealed class CardDropInfo
        {
            public Vector3 At;
            public Texture2D Tex;
            public Color Frame, Glow;
            public int Ch;
            public bool Golden;
        }

        public CardDropInfo CardDrop;

        /// <summary>Se puede soltar una carta ahi: sobre la isla, lejos del agua y fuera de los edificios.</summary>
        public bool CanSummonAt(Vector3 g)
        {
            if (new Vector2(g.x, g.z).magnitude > Isl.Radius * 0.8f) return false;
            foreach (var p in Isl.Plots)
            {
                if (p.Building < 0) continue;
                float r = p.Building == (int)BKind.Barracks ? Island.BarracksRadius(p.Level) + 0.6f : Isl.FootprintOf(p, p.Building) + 0.4f;
                if (new Vector2(g.x - p.X, g.z - p.Z).magnitude < r) return false;
            }
            return true;
        }

        /// <summary>El punto libre mas cercano a `near` donde puede caer una carta (espiral de 0.5 m).</summary>
        public Vector3 FreeSummonPoint(Vector3 near)
        {
            if (CanSummonAt(near)) return near;
            for (float r = 0.5f; r < 12f; r += 0.5f)
                for (int k = 0; k < 16; k++)
                {
                    float a = k * Mathf.PI / 8f;
                    var q = new Vector3(near.x + Mathf.Cos(a) * r, 0f, near.z + Mathf.Sin(a) * r);
                    if (CanSummonAt(q)) return q;
                }
            return near;
        }

        /// <summary>Elige al candidato `i` y lo pone donde cayo la carta (en vez de la orilla del barco).</summary>
        public Miner Recruit(int i, Vector3 at)
        {
            var m = Recruit(i);
            if (m == null) return null;
            m.X = at.x; m.Z = at.z;
            m.Hold = 2.2f;   // quieto mientras sale de la carta
            MinerView mv;
            if (miners.TryGetValue(m.Id, out mv)) mv.Model.transform.localPosition = new Vector3(m.X, m.Y, m.Z);
            return m;
        }

        static Mesh cardQuad;

        /// <summary>Carta clavada en el suelo: marco de color y el dibujo encima (0.84 x 1.24), pivote en el borde de abajo.</summary>
        GameObject MakeWorldCard(CardDropInfo info, out Material artMat, out Material frameMat)
        {
            if (cardQuad == null)
            {
                cardQuad = new Mesh { name = "CartaSuelo" };
                // el borde de abajo en el origen: la carta queda clavada en el suelo y se inclina hacia la camara
                cardQuad.vertices = new[] { new Vector3(-0.5f, 0, 0f), new Vector3(0.5f, 0, 0f), new Vector3(0.5f, 0, 1f), new Vector3(-0.5f, 0, 1f) };
                cardQuad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                cardQuad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                cardQuad.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                cardQuad.RecalculateBounds();
            }
            var go = new GameObject("CartaInvocada");
            go.transform.SetParent(root, false);
            // 1.6x: a 0.84 x 1.24 quedaba del tamano de una ventana al lado de la casa (auditoria final): un monolito
            var big = new GameObject("Escala").transform;
            big.SetParent(go.transform, false);
            big.localScale = Vector3.one * 1.6f;
            var frame = new GameObject("Marco");
            frame.transform.SetParent(big, false);
            frame.transform.localScale = new Vector3(0.84f, 1f, 1.24f);
            frame.AddComponent<MeshFilter>().sharedMesh = cardQuad;
            frameMat = new Material(Shader.Find("Mineros/MinerToon")) { name = "MarcoCarta" };
            frameMat.SetColor("_Color", info.Frame);
            frame.AddComponent<MeshRenderer>().sharedMaterial = frameMat;
            var art = new GameObject("Dibujo");
            art.transform.SetParent(big, false);
            art.transform.localPosition = new Vector3(0f, 0.004f, 0.065f);
            art.transform.localScale = new Vector3(0.74f, 1f, 1.11f);
            art.AddComponent<MeshFilter>().sharedMesh = cardQuad;
            artMat = new Material(Shader.Find("Mineros/MinerToonTex")) { name = "DibujoCarta" };
            if (info.Tex != null) artMat.mainTexture = info.Tex;
            else artMat.SetColor("_Color", info.Glow);
            artMat.SetFloat("_Floor", 0.8f);
            art.AddComponent<MeshRenderer>().sharedMaterial = artMat;
            return go;
        }

        /// <summary>La escena entera: cae, golpe, se carga de luz, estalla, el minero sale, la carta se va, queda la marca.</summary>
        IEnumerator CardSummon(MinerView v, CardDropInfo info)
        {
            var m = v.M;
            var t = v.Model.transform;
            Vector3 at = info.At;
            Material artMat, frameMat;
            var card = MakeWorldCard(info, out artMat, out frameMat);
            // la camara es del jugador: ya no se acerca sola (pedido del dueño); la carta cae donde el la solto, que ya se ve
            float prevZoom = zoomTarget;
            FocusOn(at);
            float yaw = Cam.transform.eulerAngles.y;   // de frente a la camara: se lee el dibujo
            // 1) cae desde la mano y se clava en el suelo, inclinada hacia la camara (se lee el dibujo)
            const float Lean = -50f;
            Vector3 top = at + Vector3.up * 1.4f;
            for (float k = 0f; k < 0.2f; k += Time.deltaTime)
            {
                float u = k / 0.2f;
                card.transform.position = Vector3.Lerp(top, at, u * u);
                card.transform.rotation = Quaternion.Euler(Mathf.Lerp(-88f, Lean, u * u), yaw, 0f);
                yield return null;
            }
            card.transform.position = at;
            // 2) THUMP (y la carta cabecea un poco al clavarse)
            Sfx.Play("cx_drop", -3f, 1.1f);
            Mineros.Fx.Haptics.Heavy();
            FxApi.Play("dust", at + Vector3.up * 0.1f, IslandGround.Dirt, 1.7f);
            if (Ambient != null) Ambient.ShakeNear(at, 4f, 3f);
            for (float k = 0f; k < 0.25f; k += Time.deltaTime)
            {
                float u = k / 0.25f;
                card.transform.rotation = Quaternion.Euler(Lean + Mathf.Sin(u * Mathf.PI * 2f) * 10f * (1f - u), yaw, 0f);
                yield return null;
            }
            card.transform.rotation = Quaternion.Euler(Lean, yaw, 0f);
            // 3) se carga de luz (la carta brilla cada vez mas)
            Sfx.Play("magic_rise", -9f, 1.25f);
            FxApi.Play("glint", at + Vector3.up * 0.2f, info.Glow, 1.2f);
            for (float k = 0f; k < 0.45f; k += Time.deltaTime)
            {
                float u = k / 0.45f;
                Color e = info.Glow * (u * u * 0.9f);
                artMat.SetColor("_EmissionColor", e);
                frameMat.SetColor("_EmissionColor", e);
                card.transform.localScale = Vector3.one * (1f + Mathf.Sin(u * 30f) * 0.03f * u);   // vibra cargandose
                yield return null;
            }
            // 4) estalla con la entrada del minero y el minero sale de la carta
            SummonBurst(info.Ch, info.Golden, at);
            Juice.Vibrate(40);
            m.Y = -0.45f;
            for (float k = 0f; k < 0.55f; k += Time.deltaTime)
            {
                float u = k / 0.55f;
                float s = Mineros.UI.Tw.Eval(Mineros.UI.Ease.OutBack, u);
                float sq = 1f + Mathf.Sin(u * Mathf.PI * 2f) * 0.22f * (1f - u);
                t.localScale = new Vector3(s / Mathf.Sqrt(sq), s * sq, s / Mathf.Sqrt(sq)) * MinerScale;
                m.Y = Mathf.Lerp(-0.45f, 0f, Mathf.Min(1f, u * 1.4f));
                // la carta se hunde en el suelo mientras el minero sale
                card.transform.localScale = new Vector3(1f, 1f, Mathf.Max(0.001f, 1f - u));
                Color e = info.Glow * (0.9f * (1f - u));
                artMat.SetColor("_EmissionColor", e);
                frameMat.SetColor("_EmissionColor", e);
                yield return null;
            }
            m.Y = 0f;
            t.localScale = Vector3.one * MinerScale;
            Destroy(card);
            Destroy(artMat); Destroy(frameMat);
            v.Arriving = false;
            v.Celebrate = 1f;
            Ui.Popup(at + Vector3.up * MH(2.4f), Loc.T("¡") + IslandUi.MinerName(m) + Loc.T(" llegó!"), m.Golden ? GoldCol : info.Glow, 26);
            // 5) la marca en el suelo: unos segundos con las chispas de su mineral
            for (int i = 0; i < 5; i++)
            {
                yield return new WaitForSeconds(0.5f);
                SummonTrace(info.Ch, info.Golden, at + new Vector3(Random.Range(-0.3f, 0.3f), 0.08f, Random.Range(-0.3f, 0.3f)));
            }
            if (!ComplexMode && Mathf.Abs(zoomTarget - 9f) < 0.01f) zoomTarget = prevZoom;
        }

        /// <summary>La entrada de cada minero (efecto y sonido propios).</summary>
        void SummonBurst(int ch, bool golden, Vector3 at)
        {
            Vector3 up = at + Vector3.up * 0.5f;
            switch (ch)
            {
                case 0: FxApi.Play("rock_break", up, default(Color), 1.4f); FxApi.Play("debris", at, default(Color), 1.2f); Sfx.Play("break", -4f, 0.8f); break;
                case 1: FxApi.Play("hit_spark", up, new Color(1f, 0.6f, 0.25f), 2f); FxApi.Play("steam", at + Vector3.up * 0.2f, default(Color), 1.5f); Sfx.Play("anvil", -6f, 1.1f); break;
                case 2: FxApi.Play("crit", up, new Color(0.8f, 0.86f, 0.95f), 1.3f); FxApi.Play("hit_spark", up, Color.white, 1.6f); Sfx.Play("clink", -4f, 0.8f); break;
                case 3: FxApi.Play("smoke", at + Vector3.up * 0.2f, new Color(0.25f, 0.24f, 0.24f), 1.2f); FxApi.Play("hit_spark", up, new Color(1f, 0.45f, 0.15f), 1.8f); Sfx.Play("break", -6f, 1.1f); break;
                case 4: FxApi.Play("coin_burst", up, GoldCol, 1.4f); FxApi.Play("glint", up, GoldCol, 1.6f); Sfx.Play("coin", -3f, 1f); Sfx.PlayLater("coin", 0.12f, -6f, 1.2f); break;
                case 5: FxApi.Play("gem_sparkle", up, new Color(0.45f, 0.75f, 1f), 1.6f); FxApi.Play("rock_break", up, new Color(0.5f, 0.8f, 1f), 1.1f); Sfx.Play("gem", -4f, 1f); break;
                case 6:
                    FxApi.Play("glint", up, Color.white, 2.4f); FxApi.Play("gem_sparkle", up, new Color(0.6f, 0.85f, 1f), 2f);
                    FxApi.Play("ring", at + Vector3.up * 0.05f, new Color(0.7f, 0.9f, 1f), 2.6f);
                    Sfx.Play("gleam", -2f); Sfx.PlayLater("fanfare", 0.2f, -6f);
                    break;
                case 7: FxApi.Play("ring", at + Vector3.up * 0.05f, new Color(0.75f, 0.4f, 1f), 1.8f); FxApi.Play("sparkle", up, new Color(0.85f, 0.5f, 1f), 1.5f); Sfx.Play("magic_rise", -4f, 1.5f); break;
                case 8: FxApi.Play("levelup_aura", at, GoldCol, 1.4f); FxApi.Play("confetti", up, default(Color), 1.2f); Sfx.Play("fanfare", -3f); break;
                case 9: FxApi.Play("debris", at, default(Color), 1.6f); FxApi.Play("dust", at, IslandGround.Dirt, 2.2f); Sfx.Play("break", -3f, 0.7f); break;
                default: FxApi.Play("smoke", up, new Color(0.85f, 0.95f, 1f), 1.2f); FxApi.Play("sparkle", up, new Color(0.7f, 0.9f, 1f), 1.4f); Sfx.Play("magic_rise", -5f, 0.8f); break;
            }
            if (golden) { FxApi.Play("coin_burst", up, GoldCol, 1.2f); FxApi.Play("glint", up, GoldCol, 2f); }
        }

        /// <summary>Lo que queda en el suelo despues: chispas chiquitas del mineral del minero.</summary>
        void SummonTrace(int ch, bool golden, Vector3 at)
        {
            if (golden || ch == 4 || ch == 8) { FxApi.Play("sparkle", at, GoldCol, 0.6f); return; }
            switch (ch)
            {
                case 3: FxApi.Play("smoke", at, new Color(0.3f, 0.3f, 0.3f), 0.4f); break;
                case 5: case 6: FxApi.Play("gem_sparkle", at, new Color(0.6f, 0.85f, 1f), 0.6f); break;
                case 7: FxApi.Play("sparkle", at, new Color(0.8f, 0.5f, 1f), 0.6f); break;
                default: FxApi.Play("dust", at, IslandGround.Dirt, 0.5f); break;
            }
        }
    }
}
