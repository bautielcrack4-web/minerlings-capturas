using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using UnityEngine;
using FxApi = Mineros.Fx.Fx;
using Juice = Mineros.Fx.Juice;

namespace Mineros.IslandView
{
    /// <summary>
    /// Techos que se desvanecen: al acercar la camara (pellizco o rueda), los techos del Complejo dentro de un circulo
    /// alrededor del centro de la vista se levantan un poco y se disuelven con un tramado fino (con un brillo calido en el
    /// borde), y se ve adentro de las habitaciones: mineros, maquinas, vagonetas. Al alejar vuelven igual de suave. En el
    /// Modo Cuartel el techo del piso elegido se disuelve entero (antes se apagaba de golpe).
    /// Los materiales de los techos son copias con la palabra clave _ROOFCUT (MinerCut.cginc): el resto del juego no
    /// paga el descarte de pixeles. Las losas bajo otra habitacion solo se van en el Modo Cuartel, nunca con el zoom.
    /// </summary>
    public sealed partial class IslandGame
    {
        /// <summary>
        /// Todo o nada: por debajo de RoofCutOn el techo de la vista se va (0.4 s) y vuelve por encima de RoofCutOff. Antes
        /// el corte seguia al zoom (10 -> 6.8) y a zooms intermedios los techos quedaban a medio tramar: parecian edificios
        /// "fantasma" en modo colocacion. Con histeresis para que no titile mientras se pellizca.
        /// </summary>
        const float RoofCutOn = 7.6f, RoofCutOff = 8.4f;
        bool roofCutWant;
        /// <summary>Radio del circulo que se abre (m) = tamano de camara x RoofCutK + RoofCutR: chico para el efecto linterna.</summary>
        const float RoofCutK = 0.6f, RoofCutR = 1.0f;

        static readonly Dictionary<Material, Material> cutMats = new Dictionary<Material, Material>();
        static readonly int idRoofCut = Shader.PropertyToID("_RoofCut"), idRoofCutAmt = Shader.PropertyToID("_RoofCutAmt");
        static readonly int idCutFull = Shader.PropertyToID("_CutFull");

        readonly List<Renderer>[] cutR = { new List<Renderer>(), new List<Renderer>(), new List<Renderer>() };
        readonly float[] roofGone = new float[3], roofGoneTo = new float[3];
        MaterialPropertyBlock cutMpb;
        float roofCutAmt;
        Vector3 roofCutC;
        float roofCutR = 1f;
        bool roofCutOpen;

        /// <summary>0 con los techos puestos, 1 con los techos de la vista disueltos por el zoom.</summary>
        public float RoofCutAmount { get { return roofCutAmt; } }

        /// <summary>Pasa los techos de un piso recien horneado a la variante que se disuelve (losas aparte, despues).</summary>
        void MakeRoofsCuttable(int f)
        {
            cutR[f].Clear();
            if (cxRoofs[f] == null) return;
            foreach (var r in cxRoofs[f].GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = CutMat(mats[i]);
                r.sharedMaterials = mats;
                cutR[f].Add(r);
            }
            ApplyRoofGone(f);
        }

        static Material CutMat(Material m)
        {
            if (m == null) return null;
            Material c;
            if (cutMats.TryGetValue(m, out c)) return c;
            string sn = m.shader != null ? m.shader.name : "";
            if (sn != "Mineros/MinerToon" && sn != "Mineros/MinerToonVC" && sn != "Mineros/MinerToonTex") { cutMats[m] = m; return m; }
            c = new Material(m) { name = m.name + "_Corte" };
            c.EnableKeyword("_ROOFCUT");
            cutMats[m] = c;
            return c;
        }

        /// <summary>Techo (y losas) de un piso: disuelto entero o puesto. Sin `instant`, lo anima UpdateRoofCut.</summary>
        void SetRoofGone(int f, bool gone, bool instant)
        {
            roofGoneTo[f] = gone ? 1f : 0f;
            if (instant) roofGone[f] = roofGoneTo[f];
            // las losas (sin variante de corte) siguen como antes: se apagan junto con el piso
            var rt = cxRoofs[f].transform;
            for (int i = 0; i < rt.childCount; i++)
            {
                var ch = rt.GetChild(i);
                if (ch.name == "Losas") ch.gameObject.SetActive(!gone);
            }
            ApplyRoofGone(f);
        }

        void ApplyRoofGone(int f)
        {
            if (cutMpb == null) cutMpb = new MaterialPropertyBlock();
            float u = roofGone[f];
            float g = u * u * (3f - 2f * u);
            foreach (var r in cutR[f])
            {
                if (r == null) continue;
                cutMpb.SetFloat(idCutFull, g);
                r.SetPropertyBlock(cutMpb);
                bool on = g < 0.999f;
                if (r.enabled != on) r.enabled = on;   // del todo ido: ni se dibuja
            }
        }

        /// <summary>Cuanto se fue el techo en ese punto del mundo (la misma cuenta que MinerCut.cginc).</summary>
        float RoofCutAt(Vector3 w)
        {
            float d = new Vector2(w.x - roofCutC.x, w.z - roofCutC.z).magnitude;
            float radial = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(roofCutR * 0.5f, roofCutR, d));
            float a = roofCutAmt * roofCutAmt * (3f - 2f * roofCutAmt);
            return a * radial;
        }

        /// <summary>
        /// Tocar a traves (techo v2): con el techo abierto, tocar una habitacion de adentro la elige (entra al Modo
        /// Cuartel con esa habitacion marcada, en su piso) en vez de abrir la ficha del Cuartel entero.
        /// </summary>
        bool TapThroughRoof(Vector2 screen)
        {
            if (ComplexMode || cxRoot == null || roofCutAmt < 0.5f) return false;
            float px = Cam.pixelHeight / 1544f;
            Module best = null; float bd = 70f * px;
            foreach (var m in Isl.Modules)
            {
                if (Isl.ModAt(m.X, m.Z, m.F + 1) != null) continue;   // tapada por el piso de arriba
                Vector3 w = ModWorld(m) + Vector3.up * 0.6f;
                if (RoofCutAt(w) < 0.5f) continue;
                float d = Vector2.Distance(screen, Cam.WorldToScreenPoint(w));
                if (d < bd) { bd = d; best = m; }
            }
            if (best == null) return false;
            EnterComplex();
            if (best.F > 0) { ViewFloor = best.F; ApplyFloorView(false); }
            SelectedMod = best;
            Transform t;
            if (cxMods.TryGetValue(best.Id, out t)) Juice.Punch(t, 0.08f, 0.2f);
            Ui.ModSelected(best);
            return true;
        }

        void UpdateRoofCut(float dt)
        {
            for (int f = 0; f < 3; f++)
            {
                if (cxRoofs[f] == null || roofGone[f] == roofGoneTo[f]) continue;
                roofGone[f] = Mathf.MoveTowards(roofGone[f], roofGoneTo[f], dt / 0.35f);
                ApplyRoofGone(f);
            }

            // zoom: bajo RoofCutOn los techos de la vista se van, sobre RoofCutOff vuelven (en el Modo Cuartel manda el piso)
            if (ComplexMode) roofCutWant = false;   // (antes tambien sin Cuartel: ahora los edificios de kit tambien se abren)
            else if (!roofCutWant && Cam.orthographicSize < RoofCutOn) roofCutWant = true;
            else if (roofCutWant && Cam.orthographicSize > RoofCutOff) roofCutWant = false;
            float want = roofCutWant ? 1f : 0f;
            roofCutAmt = Mathf.MoveTowards(roofCutAmt, want, dt / 0.4f);

            // centro: donde el rayo del medio de la pantalla cruza la altura de los techos (el techo que tapa ese punto)
            float roofY = (cxRoot != null ? cxRoot.position.y : 0f) + IslandArt.SlabH + IslandArt.WallH;
            Ray ray = Cam.ScreenPointToRay(new Vector2(Cam.pixelWidth * 0.5f, Cam.pixelHeight * 0.5f));
            Vector3 c = Mathf.Abs(ray.direction.y) > 1e-4f ? ray.origin + ray.direction * ((roofY - ray.origin.y) / ray.direction.y) : ray.origin;
            float radius = Cam.orthographicSize * RoofCutK + RoofCutR;
            float a = roofCutAmt * roofCutAmt * (3f - 2f * roofCutAmt);
            Shader.SetGlobalVector(idRoofCut, new Vector4(c.x, c.y, c.z, radius));
            roofCutC = c; roofCutR = radius;
            Shader.SetGlobalFloat(idRoofCutAmt, a);

            // un soplido suave al abrirse y al cerrarse (solo si el Complejo esta cerca del centro de la vista)
            bool near = cxRoot != null && (new Vector2(cxRoot.position.x - c.x, cxRoot.position.z - c.z)).magnitude < radius + 5f;
            if (!roofCutOpen && a > 0.6f)
            {
                roofCutOpen = true;
                if (near)
                {
                    Sfx.Play("whoosh", -19f, 1.45f);
                    FxApi.Play("sparkle", c + Vector3.up * 0.2f, new Color(1f, 0.85f, 0.5f), 0.9f);   // polvito dorado que sube
                }
            }
            else if (roofCutOpen && a < 0.35f) { roofCutOpen = false; if (near) Sfx.Play("whoosh", -21f, 1.15f); }
        }
    }
}
