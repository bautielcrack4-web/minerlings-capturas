using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using UnityEngine;
using FxApi = Mineros.Fx.Fx;

namespace Mineros.IslandView
{
    /// <summary>
    /// Vida de las habitaciones del kit (PLAN_HABITACIONES §3.1): las piezas traen anclas en su JSON (kit_lib.py,
    /// anchor) y un solo recorrido por cuadro las anima, sin un MonoBehaviour por objeto. Solo con zoom &lt;= 12 y a la
    /// vista. Hoy: "vapor" (chorro cada 4-7 s con siseo) y "aguja" (manometro que tiembla y salta a rojo cuando la
    /// habitacion trabaja), "humo" (chimenea del carbon: bocanada oscura cada ~1.2 s, mas seguido trabajando). Lo que late con luz (brasas, faroles) no pasa por aca: lo hace el shader (filas emisivas).
    /// </summary>
    public sealed partial class IslandGame
    {
        sealed class LifeAnchor
        {
            public string N;
            public int F;
            public Transform Parent;     // las paredes del piso: las anclas bajan y se esconden con ellas
            public Vector3 P, D;         // en el espacio de Parent
            public Module M;
            public Transform Needle;
            public float T, Ang, Kick;
        }

        const float LifeZoom = 12f;
        readonly List<LifeAnchor> life = new List<LifeAnchor>();
        static Mesh needleMesh;
        static Material needleMat;

        /// <summary>Anclas de las piezas recien horneadas de un piso (las agujas se crean aca, hijas de `parent`).</summary>
        void CollectLife(int f, List<RoomKit.Slot> slots, Transform parent)
        {
            foreach (var s in slots)
                foreach (var a in IslandArt.TripoAnchors(s.File))
                {
                    var la = new LifeAnchor
                    {
                        N = a.N, F = f, Parent = parent, M = s.Mod, T = Random.Range(1f, 6f),
                        P = s.M.MultiplyPoint3x4(a.P), D = s.M.MultiplyVector(a.D).normalized
                    };
                    if (a.N == "aguja") la.Needle = MakeNeedle(parent, la.P, la.D);
                    life.Add(la);
                }
        }

        static Transform MakeNeedle(Transform parent, Vector3 p, Vector3 d)
        {
            if (needleMesh == null)
            {
                // aguja: tira fina con la base en el eje y la punta hacia +Y local
                needleMesh = new Mesh { name = "Aguja" };
                needleMesh.vertices = new[]
                {
                    new Vector3(-0.007f, -0.012f, 0f), new Vector3(0.007f, -0.012f, 0f), new Vector3(0.0025f, 0.07f, 0f), new Vector3(-0.0025f, 0.07f, 0f),
                };
                needleMesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 };
                needleMesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                needleMesh.RecalculateBounds();
                var sh = Shader.Find("Mineros/MinerToon");
                needleMat = new Material(sh) { name = "Aguja" };
                needleMat.SetColor("_Color", new Color(0.08f, 0.06f, 0.05f));
            }
            var go = new GameObject("Aguja");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = p;
            go.AddComponent<MeshFilter>().sharedMesh = needleMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = needleMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        bool ModWorking(Module m)
        {
            if (m == null || m.Broken) return false;
            var def = Island.MDef(m.Kind);
            return def.NeedsPower ? Isl.Summary.Powered.Contains(m.Id) : m.Kind != ModKind.Dorm && m.Kind != ModKind.Corridor;
        }

        void UpdateRoomLife(float dt)
        {
            if (life.Count == 0) return;
            bool near = Cam.orthographicSize <= LifeZoom;
            bool hear = Cam.orthographicSize <= 9f;
            float t = Time.time;
            for (int i = 0; i < life.Count; i++)
            {
                var a = life[i];
                if (a.Parent == null || !a.Parent.gameObject.activeInHierarchy) continue;
                if (a.Needle != null)
                {
                    // tiembla siempre; con la habitacion trabajando salta a la zona roja (con un tic al cruzar)
                    if (!near) continue;
                    bool work = ModWorking(a.M);
                    float target = work ? 52f : -38f;
                    float jitter = (Mathf.PerlinNoise(t * 3.1f, i * 1.7f) - 0.5f) * (work ? 14f : 7f);
                    float prev = a.Ang;
                    a.Ang = Mathf.MoveTowards(a.Ang, target, dt * 160f);
                    if (prev < 30f && a.Ang >= 30f && hear && OnScreen(a.Parent.TransformPoint(a.P))) Sfx.Play("gauge_tick", -20f, 1f);
                    Vector3 up = Vector3.ProjectOnPlane(Vector3.up, a.D);
                    if (up.sqrMagnitude < 1e-4f) up = Vector3.forward;
                    a.Needle.localRotation = Quaternion.AngleAxis(a.Ang + jitter, a.D) * Quaternion.LookRotation(a.D, up.normalized);
                    continue;
                }
                if (a.N == "humo")
                {
                    a.T -= dt;
                    if (a.T > 0f) continue;
                    a.T = ModWorking(a.M) ? 0.7f : 1.3f;
                    if (!near) continue;
                    Vector3 w = a.Parent.TransformPoint(a.P);
                    if (OnScreen(w) && RoofCutAt(w) < 0.2f) FxApi.Play("smoke", w, new Color(0.35f, 0.33f, 0.32f), 0.45f);   // sin techo, la chimenea no esta
                    continue;
                }
                if (a.N == "vapor")
                {
                    a.T -= dt;
                    if (a.T > 0f) continue;
                    a.T = Random.Range(4f, 7f) * (ModWorking(a.M) ? 0.6f : 1f);
                    if (!near) continue;
                    Vector3 w = a.Parent.TransformPoint(a.P);
                    if (!OnScreen(w)) continue;
                    FxApi.Play("steam", w, default(Color), 1f);
                    if (hear) Sfx.Play("steam_hiss", -18f, Random.Range(0.92f, 1.08f));
                }
            }
        }
    }
}
