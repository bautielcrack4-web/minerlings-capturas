using System.Collections;
using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using Mineros.World;
using UnityEngine;
using FxApi = Mineros.Fx.Fx;

namespace Mineros.IslandView
{
    /// <summary>
    /// Construir con momento (PLAN_HABITACIONES §5): una habitacion nueva no aparece de golpe, se arma pieza por pieza.
    /// Mientras dura, el horneado del piso la deja afuera (cxBuilding) y sus piezas son objetos sueltos que caen en
    /// orden: el piso brota, las paredes caen de a una (polvo y una nota mas aguda cada una), el techo golpea y despues
    /// se disuelve como el resto si se esta mirando adentro. Al final se vuelve a hornear sin costura.
    /// </summary>
    public sealed partial class IslandGame
    {
        bool BuildAnimOk(Module m) { return cxRoot != null && m != null && m.Kind != ModKind.Central; }

        /// <summary>Pivote en la base de la pieza (para que caiga y rebote sobre su lugar) con la malla adentro.</summary>
        static Transform Pivot(Transform parent, Vector3 at, string name, out Transform inner)
        {
            var p = new GameObject(name).transform;
            p.SetParent(parent, false);
            p.localPosition = at;
            inner = new GameObject("Malla").transform;
            inner.SetParent(p, false);
            inner.localPosition = -at;
            return p;
        }

        IEnumerator AssembleRoom(Module m)
        {
            int f = Mathf.Clamp(m.F, 0, 2);
            var wb = new MeshBuilder(); var rb = new MeshBuilder(); var sb = new MeshBuilder();
            var kw = new List<RoomKit.Slot>(); var kr = new List<RoomKit.Slot>();
            IslandArt.ComplexFloor(Isl, f, wb, rb, kw, kr, sb, null, m);
            var holder = new GameObject("Armando" + m.Id).transform;
            holder.SetParent(cxRoot, false);
            var wallsH = new GameObject("Paredes").transform; wallsH.SetParent(holder, false);
            var roofH = new GameObject("Techo").transform; roofH.SetParent(holder, false);
            Vector3 c = IslandArt.CellPos(m.X, m.Z, m.F);

            // piezas: piso y lo de codigo, cada pared del kit, el techo entero
            Transform inner;
            var basePv = Pivot(wallsH, c, "Base", out inner);
            if (wb.TriCount > 0) IslandArt.Bake(wb, inner, "Base", true);
            basePv.localScale = new Vector3(1f, 0.001f, 1f);
            var walls = new List<Transform>();
            foreach (var s in kw)
            {
                Material mat;
                var mesh = IslandArt.TripoModel(s.File, out mat);
                if (mesh == null) continue;
                var b = mesh.bounds;
                Vector3 at = s.M.MultiplyPoint3x4(new Vector3(b.center.x, b.min.y, b.center.z));
                var pv = Pivot(wallsH, at, "Pieza", out inner);
                RoomKit.Bake(new List<RoomKit.Slot> { s }, inner, "Pieza", true);
                pv.gameObject.SetActive(false);
                walls.Add(pv);
            }
            Transform roofPv = null;
            var roofR = new List<Renderer>();
            if (rb.TriCount > 0 || kr.Count > 0)
            {
                roofPv = Pivot(roofH, c + Vector3.up * (IslandArt.SlabH + IslandArt.WallH), "Techo", out inner);
                if (rb.TriCount > 0) IslandArt.Bake(rb, inner, "Techo", true);
                RoomKit.Bake(kr, inner, "Techo", true);
                foreach (var r in roofPv.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = CutMat(mats[i]);
                    r.sharedMaterials = mats;
                    roofR.Add(r);
                }
                SetCut(roofR, 0f);
                roofPv.gameObject.SetActive(false);
            }

            // las paredes siguen a las del piso (en el Modo Cuartel bajan como una casa de munecas)
            System.Action follow = () =>
            {
                if (wallsH == null) return;
                var w = cxWalls[f];
                if (w != null) { wallsH.localScale = w.transform.localScale; wallsH.localPosition = w.transform.localPosition; }
            };

            yield return new WaitForSeconds(0.16f);   // primero el CLACK del mueble (ModDrop)
            // 1) el piso brota
            for (float t = 0f; t < 0.22f; t += Time.deltaTime)
            {
                if (holder == null) { EndAssemble(m); yield break; }
                follow();
                basePv.localScale = new Vector3(1f, Mathf.Max(0.001f, Mineros.UI.Tw.Eval(Mineros.UI.Ease.OutBack, t / 0.22f)), 1f);
                yield return null;
            }
            if (holder == null) { EndAssemble(m); yield break; }
            basePv.localScale = Vector3.one;
            // 2) las paredes caen de a una, superpuestas
            const float drop = 0.22f, gap = 0.12f, h0 = 2.4f;
            float total = walls.Count == 0 ? 0f : (walls.Count - 1) * gap + drop + 0.16f;
            var landed = new bool[walls.Count];
            for (float t = 0f; t < total; t += Time.deltaTime)
            {
                if (holder == null) { EndAssemble(m); yield break; }
                follow();
                for (int i = 0; i < walls.Count; i++)
                {
                    float k = t - i * gap;
                    var pv = walls[i];
                    if (k < 0f) continue;
                    if (!pv.gameObject.activeSelf) pv.gameObject.SetActive(true);
                    float u = Mathf.Clamp01(k / drop);
                    float y = (1f - u * u) * h0;
                    // rebote corto al tocar
                    float q = k > drop ? Mathf.Sin(Mathf.Clamp01((k - drop) / 0.16f) * Mathf.PI) * 0.1f : 0f;
                    pv.GetChild(0).localPosition = -PivotAt(pv) + Vector3.up * y;
                    pv.localScale = new Vector3(1f + q, 1f - q, 1f + q);
                    if (u >= 1f && !landed[i])
                    {
                        landed[i] = true;
                        Vector3 w = pv.position;
                        FxApi.Play("dust", w + Vector3.up * 0.1f, IslandGround.Dirt, 0.9f);
                        Sfx.Play("wood_place", -6f, 0.95f + i * 0.05f);
                        Sfx.Note(i, -12f);   // do, re, mi...: cada pared una nota mas
                        Mineros.Fx.Haptics.Light();
                    }
                }
                yield return null;
            }
            foreach (var pv in walls) if (pv != null) { pv.gameObject.SetActive(true); pv.localScale = Vector3.one; pv.GetChild(0).localPosition = -PivotAt(pv); }
            // 3) el techo golpea
            if (roofPv != null)
            {
                roofPv.gameObject.SetActive(true);
                for (float t = 0f; t < 0.26f; t += Time.deltaTime)
                {
                    if (holder == null) { EndAssemble(m); yield break; }
                    follow();
                    float u = Mathf.Clamp01(t / 0.26f);
                    roofPv.GetChild(0).localPosition = -PivotAt(roofPv) + Vector3.up * ((1f - u * u) * 3.2f);
                    yield return null;
                }
                roofPv.GetChild(0).localPosition = -PivotAt(roofPv);
                Vector3 rw = roofPv.position;
                Sfx.Play("cx_drop", -3f, 0.9f);
                Mineros.Fx.Haptics.Heavy();
                FxApi.Play("dust", rw + Vector3.up * 0.2f, IslandGround.Dirt, 1.6f);
                FxApi.Play("hit_spark", rw + Vector3.up * 0.4f, new Color(1f, 0.9f, 0.55f), 1.0f);
                if (Ambient != null) Ambient.ShakeNear(rw, 4f, 3f);
                for (float t = 0f; t < 0.2f; t += Time.deltaTime)
                {
                    if (holder == null) { EndAssemble(m); yield break; }
                    float q = Mathf.Sin(Mathf.Clamp01(t / 0.2f) * Mathf.PI) * 0.08f;
                    roofPv.localScale = new Vector3(1f + q, 1f - q, 1f + q);
                    yield return null;
                }
                roofPv.localScale = Vector3.one;
                // si se esta mirando adentro (Modo Cuartel o zoom), el techo nuevo se va como los demas
                float gone = roofGone[f];
                if (gone > 0.01f)
                {
                    yield return new WaitForSeconds(0.25f);
                    for (float t = 0f; t < 0.35f; t += Time.deltaTime)
                    {
                        if (holder == null) { EndAssemble(m); yield break; }
                        float u = Mathf.Clamp01(t / 0.35f) * gone;
                        SetCut(roofR, u * u * (3f - 2f * u));
                        yield return null;
                    }
                }
            }
            // 4) listo: horneado completo, destello
            Vector3 at2 = ModWorld(m);
            if (holder != null) Destroy(holder.gameObject);
            EndAssemble(m);
            FxApi.Play("sparkle", at2 + Vector3.up * 0.9f, new Color(1f, 0.92f, 0.6f), 1.2f);
            Sfx.Play("cx_done", -5f);
            Sfx.PlayLater("fanfare_short", 0.12f, -9f);
        }

        // ------------------------------------------------------------ mejorar con escena: andamio y martillazos
        float hammerT;

        /// <summary>Andamio de madera alrededor de una habitacion que sube de etapa (PLAN_HABITACIONES §5).</summary>
        void MakeScaffold(Transform t)
        {
            var mb = new MeshBuilder();
            float hw = IslandArt.ModW * 0.5f + 0.13f, h = IslandArt.WallH + 0.55f, y0 = IslandArt.SlabH;
            Color pole = IslandArt.WoodD, plank = IslandArt.Wood;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    mb.Box(new Vector3(sx * hw, y0 + h * 0.5f, sz * hw), new Vector3(0.07f, h, 0.07f), pole, 0f);
            foreach (float y in new[] { 0.42f, 0.92f, 1.42f })
            {
                mb.Box(new Vector3(0f, y0 + y, -hw), new Vector3(hw * 2f + 0.1f, 0.05f, 0.16f), plank, 0f);
                mb.Box(new Vector3(0f, y0 + y, hw), new Vector3(hw * 2f + 0.1f, 0.05f, 0.16f), plank, 0f);
                mb.Box(new Vector3(-hw, y0 + y, 0f), new Vector3(0.16f, 0.05f, hw * 2f + 0.1f), plank, 0f);
                mb.Box(new Vector3(hw, y0 + y, 0f), new Vector3(0.16f, 0.05f, hw * 2f + 0.1f), plank, 0f);
            }
            // cruz de refuerzo en el frente
            mb.Box(new Vector3(0f, y0 + h * 0.5f, -hw - 0.04f), new Vector3(0.05f, h * 1.05f, 0.05f), pole, 0f);
            IslandArt.Bake(mb, t, "Andamio", true);
        }

        /// <summary>Martillazos con chispas en las habitaciones en obra (y su sonido si la camara esta cerca).</summary>
        void UpdateScaffolds(float dt)
        {
            hammerT -= dt;
            if (hammerT > 0f || cxRoot == null) return;
            hammerT = 0.5f + Random.value * 0.35f;
            int shown = 0;
            foreach (var m in Isl.Modules)
            {
                if (m.Work <= 0 || m.Kind == ModKind.Secret || shown >= 2) continue;
                shown++;
                Vector3 c = ModWorld(m);
                float hw = IslandArt.ModW * 0.5f + 0.13f;
                Vector3 p = c + new Vector3(Random.Range(-hw, hw), IslandArt.SlabH + Random.Range(0.5f, 1.5f), -hw);
                FxApi.Play("hit_spark", p, new Color(1f, 0.85f, 0.5f), 0.7f);
                if (Cam.orthographicSize < 10f) Sfx.Play("scaffold_hammer", -14f, 0.9f + Random.value * 0.2f);
            }
        }

        /// <summary>Base de la pieza (donde esta el pivote) en coordenadas del padre.</summary>
        static Vector3 PivotAt(Transform pv) { return pv.localPosition; }

        void SetCut(List<Renderer> rs, float g)
        {
            if (cutMpb == null) cutMpb = new MaterialPropertyBlock();
            foreach (var r in rs)
            {
                if (r == null) continue;
                cutMpb.SetFloat(idCutFull, g);
                r.SetPropertyBlock(cutMpb);
                r.enabled = g < 0.999f;
            }
        }

        void EndAssemble(Module m)
        {
            if (cxBuilding != m) return;
            cxBuilding = null;
            cxDirty = true;
            RebuildComplex();
        }
    }
}
