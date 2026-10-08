using System.Collections;
using Mineros.Audio;
using Mineros.Core;
using UnityEngine;
using FxApi = Mineros.Fx.Fx;
using Juice = Mineros.Fx.Juice;

namespace Mineros.IslandView
{
    /// <summary>
    /// Colocar y mover edificios como en Clash of Clans:
    /// - Al elegir un edificio nuevo, aparece flotando en la parcela del "+" y se arrastra a cualquier lugar libre.
    /// - Un edificio existente se mueve manteniendolo apretado medio segundo (o con "Mover" en su hoja).
    /// - La huella abajo se ve verde si entra y roja si no; ✓ confirma y ✗ deja todo como estaba.
    /// - Al soltar, el edificio cae con un rebote y el caminito se vuelve a conectar creciendo hasta la puerta.
    /// Las reglas (donde entra, que se corre, guardado) viven en el nucleo: Island.CanPlace / Island.MovePlot.
    /// </summary>
    public sealed partial class IslandGame
    {
        sealed class PlaceState
        {
            public PlotView V;
            public int Kind;                 // lo que se coloca (el edificio de la parcela o el nuevo)
            public bool IsNew;
            public Vector3 Orig, Pos;
            public bool Valid, Dragging;
            public Vector3 Grab;             // diferencia entre el dedo y el centro al agarrarlo
            public Transform Ghost;          // edificio nuevo (fantasma); al mover se usa el edificio real
            public System.Action Confirm;
            public float T;
            public Mineros.UI.Spring3 Follow;      // el edificio sigue al dedo con masa (resorte pesado)
            public Mineros.UI.Spring TiltX, TiltZ; // se inclina hacia donde va y se endereza al frenar
            public bool Inited;
            public float NextBuzz;
            public float BlobY;
            public Vector3 BlobLocal;
            // modulo del Complejo (0.11): tipo, especialista, celda imantada (CF = -1: suelto), giro y el que se mueve
            public int ModK = -1, ModCh = -1, CX, CZ, CF = -1, Rot;
            public Module MoveMod;
            public double Delta;
            public System.Collections.Generic.List<int> Partners = new System.Collections.Generic.List<int>();
            public float Yaw, CurYaw;
        }

        PlaceState place;
        float downT;

        public bool Placing { get { return place != null; } }
        public Vector3 PlacePos { get { return place != null ? place.Pos : Vector3.zero; } }
        public bool PlaceValid { get { return place != null && place.Valid; } }
        public float PlaceRadius { get { return place == null ? 1f : place.ModK >= 0 ? 1.0f : Island.Footprint(place.Kind); } }
        /// <summary>Lugar de donde se levanto el edificio (null si es uno nuevo): se marca con un anillo tenue.</summary>
        public Vector3? PlaceOrigin { get { return place != null && !place.IsNew ? (Vector3?)place.Orig : null; } }
        public Transform PlaceObject { get { return place == null ? null : place.IsNew ? place.Ghost : place.V.Root; } }

        /// <summary>Mover un edificio que ya esta construido.</summary>
        public bool BeginMove(Plot p)
        {
            if (place != null || !Isl.Movable(p) || p.Building < 0) return false;
            var v = plots[p.Id];
            place = new PlaceState { V = v, Kind = p.Building, Orig = v.Root.position, Pos = v.Root.position };
            if (v.Blob != null) { place.BlobY = v.Blob.position.y; place.BlobLocal = v.Blob.localPosition; }
            place.Valid = true;
            v.Driven = true;
            Sfx.Play("cx_grab", -4f);
            Mineros.Fx.Haptics.Prepare();
            Mineros.Fx.Haptics.Medium();
            FxApi.Play("dust", v.Root.position + Vector3.up * 0.2f, IslandGround.Dirt, 1.6f);
            Ui.PlaceStarted(false);
            return true;
        }

        /// <summary>Colocar un edificio nuevo: `confirm` se llama con la parcela ya en su lugar (ahi se paga y empieza la obra).</summary>
        public void BeginPlace(Plot p, BKind kind, System.Action confirm)
        {
            if (place != null) CancelPlace();
            var v = plots[p.Id];
            var ghost = new GameObject("Fantasma").transform;
            ghost.SetParent(root, false);
            GhostBody(kind, ghost);
            ghost.position = v.Root.position;
            place = new PlaceState { V = v, Kind = (int)kind, IsNew = true, Orig = v.Root.position, Pos = v.Root.position, Ghost = ghost, Confirm = confirm };
            place.Valid = Isl.CanPlace(p, (int)kind, p.X, p.Z);
            if (!place.Valid) SeekFree();
            FocusOn(place.Pos);
            Sfx.Play("cx_grab", -5f);
            Ui.PlaceStarted(true);
        }

        /// <summary>Si la parcela del "+" no alcanza para este edificio (es mas grande), lo acerca al lugar libre mas proximo.</summary>
        void SeekFree()
        {
            var p = place.V.P;
            for (float r = 0.5f; r < 8f; r += 0.5f)
                for (int i = 0; i < 16; i++)
                {
                    float a = i * Mathf.PI / 8f;
                    float x = Island.Snap(p.X + Mathf.Cos(a) * r), z = Island.Snap(p.Z + Mathf.Sin(a) * r);
                    if (!Isl.CanPlace(p, place.Kind, x, z)) continue;
                    place.Pos = new Vector3(x, 0f, z);
                    place.Valid = true;
                    return;
                }
        }

        void GhostBody(BKind k, Transform parent)
        {
            Material tm;
            var tmesh = IslandArt.TripoBuilding(k, 1, out tm);
            if (tmesh != null)
            {
                var r = IslandArt.MakeRenderer(parent, "Modelo", tmesh, new[] { tm });
                r.transform.localRotation = Quaternion.Euler(0f, BuildingYaw, 0f);
                r.transform.localScale = Vector3.one * 0.935f;
            }
            else
            {
                Material[] mats;
                var mesh = IslandArt.ProcBuilding(k, 1, out mats);
                var r = IslandArt.MakeRenderer(parent, "Modelo", mesh, mats);
                r.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
            }
        }

        public void ConfirmPlace()
        {
            if (place == null) return;
            if (place.ModK >= 0) { ConfirmMod(); return; }
            if (!place.Valid) { Ui.PlaceDenied(); return; }
            var s = place;
            var p = s.V.P;
            Vector3 lifted = s.V.Root.localPosition;   // desde donde flotaba (MovePlot fija la posicion final)
            bool changed = Mathf.Abs(s.Pos.x - p.X) > 0.01f || Mathf.Abs(s.Pos.z - p.Z) > 0.01f;
            if (changed && !Isl.MovePlot(p, s.Pos.x, s.Pos.z)) { Ui.PlaceDenied(); return; }
            place = null;
            Ui.PlaceEnded();
            if (s.IsNew)
            {
                Destroy(s.Ghost.gameObject);
                SyncPlotRoots();
                s.Confirm?.Invoke();
            }
            else
            {
                s.V.Driven = false;
                SyncPlotRoots();
                if (s.V.Blob != null) s.V.Blob.localPosition = s.BlobLocal;
                s.V.Root.rotation = Quaternion.identity;
                s.V.Root.localPosition = lifted;
                StartCoroutine(DropShow(s.V, changed));
            }
            Save();
        }

        public void CancelPlace()
        {
            if (place == null) return;
            var s = place;
            place = null;
            Ui.PlaceEnded();
            if (s.IsNew) Destroy(s.Ghost.gameObject);
            else
            {
                s.V.Root.position = s.Orig; s.V.Root.rotation = Quaternion.identity; s.V.Driven = false; s.V.Body.localScale = Vector3.one;
                if (s.V.Blob != null) s.V.Blob.localPosition = s.BlobLocal;
            }
            Sfx.Play("cx_close", -8f);
        }

        /// <summary>Las raices de las parcelas siguen al nucleo (el jugador movio una y quiza se corrieron lotes vacios).</summary>
        void SyncPlotRoots()
        {
            foreach (var v in plots) v.Root.localPosition = new Vector3(v.P.X, 0f, v.P.Z);
        }

        /// <summary>El nucleo movio una parcela: se repinta el suelo sin su camino, que despues crece hasta la puerta.</summary>
        void OnPlotMoved(Plot p)
        {
            SyncPlotRoots();
            // vetas que quedaron debajo: se deshacen en polvo
            var dead = new System.Collections.Generic.List<int>();
            foreach (var kv in ores) if (kv.Value.O.Dead) dead.Add(kv.Key);
            foreach (var id in dead)
            {
                var ov = ores[id];
                FxApi.Play("dust", ov.T.position + Vector3.up * 0.3f, IslandGround.Dirt, 1.2f);
                Destroy(ov.T.gameObject);
                ores.Remove(id);
            }
            Ground.Repaint(p.Building >= 0 && p.Level >= 1 ? p.Id : -1);
            BuildLamps();
            MarkOptimize();
            if (p.Building >= 0 && p.Level >= 1) StartCoroutine(GrowPath(p.Id));
        }

        /// <summary>El edificio baja desde donde flotaba con un rebote, polvo, un golpe y una vibracion suave.</summary>
        IEnumerator DropShow(PlotView v, bool moved)
        {
            v.Driven = true;
            Vector3 to = new Vector3(v.P.X, 0f, v.P.Z);
            Vector3 from = v.Root.localPosition;
            float t = 0f;
            while (t < 0.16f)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / 0.16f);
                v.Root.localPosition = Vector3.Lerp(from, to, u * u);
                yield return null;
            }
            v.Root.localPosition = to;
            FxApi.Play("dust", v.Root.position + Vector3.up * 0.2f, IslandGround.Dirt, 2.4f);
            if (Ambient != null) Ambient.ShakeNear(v.Root.position, 6f, 6f);   // la onda del golpe sacude los arboles
            Sfx.Play("cx_drop", -2f);
            Sfx.PlayLater("cx_done", 0.35f, -6f);
            Mineros.Fx.Haptics.Heavy();
            // aplastado y rebote
            t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / 0.35f);
                float k = Mathf.Sin(u * Mathf.PI * 2f) * (1f - u) * 0.12f;
                if (v.Body != null) v.Body.localScale = new Vector3(1f + k, 1f - k, 1f + k);
                yield return null;
            }
            if (v.Body != null) v.Body.localScale = Vector3.one;
            if (moved) { v.ShownKind = -2; RefreshPlot(v); }   // el jardincito se reacomoda mirando al camino nuevo
            v.Driven = false;
        }

        // ------------------------------------------------------------ toques mientras se coloca
        /// <summary>Al empezar un toque: si cae sobre lo que se esta colocando, se agarra (la camara no se mueve).</summary>
        bool PlaceGrab(Vector2 screen)
        {
            if (place == null) return false;
            Vector3 g = ScreenToGround(screen);
            Vector3 obj = place.Pos;
            float r = Island.Footprint(place.Kind) + 0.9f;
            Vector2 sp = Cam.WorldToScreenPoint(obj + Vector3.up * 1.2f);
            float px = Cam.pixelHeight / 1544f;
            bool hit = (new Vector3(g.x - obj.x, 0f, g.z - obj.z)).sqrMagnitude < r * r || Vector2.Distance(screen, sp) < 110f * px;
            if (!hit) return false;
            place.Dragging = true;
            place.Grab = obj - g; place.Grab.y = 0f;
            Sfx.Play("cx_tick", -6f);
            return true;
        }

        void PlaceDrag(Vector2 screen, float dt)
        {
            if (place == null || !place.Dragging) return;
            Vector3 g = ScreenToGround(screen) + place.Grab;
            float x = Island.Snap(g.x), z = Island.Snap(g.z);
            if (place.ModK >= 0) ModDrag(g);
            else if (Mathf.Abs(x - place.Pos.x) > 0.01f || Mathf.Abs(z - place.Pos.z) > 0.01f)
            {
                place.Pos = new Vector3(x, 0f, z);
                bool ok = Isl.CanPlace(place.V.P, place.Kind, x, z);
                if (ok != place.Valid)
                {
                    Sfx.Play(ok ? "cx_snap" : "cx_deny", ok ? -9f : -8f);
                    if (ok) Sfx.Note(4, -16f);
                    if (!ok) Mineros.Fx.Haptics.Warning();
                }
                else Mineros.Fx.Haptics.Selection();   // un tick por celda de la grilla
                place.Valid = ok;
                Sfx.Play("cx_tick", ok ? -14f : -18f);
            }
            // cerca del borde de la pantalla la camara acompaña sola
            float ex = 0f, ey = 0f, w = Cam.pixelWidth, h = Cam.pixelHeight, m = 0.09f;
            if (screen.x < w * m) ex = -1f; else if (screen.x > w * (1f - m)) ex = 1f;
            if (screen.y < h * 0.14f) ey = -1f; else if (screen.y > h * (1f - m)) ey = 1f;
            if (ex != 0f || ey != 0f)
            {
                Vector3 right = Cam.transform.right; right.y = 0f; right.Normalize();
                Vector3 fwd = Cam.transform.up; fwd.y = 0f; fwd.Normalize();
                camRig.position += (right * ex + fwd * ey) * Cam.orthographicSize * 0.9f * dt;
            }
        }

        void PlaceRelease() { if (place != null) place.Dragging = false; }

        /// <summary>Tocar el suelo (sin arrastrar) lleva lo que se coloca hasta ahi.</summary>
        void PlaceTapTo(Vector2 screen)
        {
            if (place == null) return;
            Vector3 g = ScreenToGround(screen);
            if (place.ModK >= 0) { ModDrag(g); return; }
            float x = Island.Snap(g.x), z = Island.Snap(g.z);
            place.Pos = new Vector3(x, 0f, z);
            place.Valid = Isl.CanPlace(place.V.P, place.Kind, x, z);
            Sfx.Play("cx_snap", -10f);
        }

        /// <summary>Mantener apretado medio segundo sobre un edificio lo levanta para moverlo.</summary>
        void CheckLongPress()
        {
            if (place != null || !dragging || pinching || dragDist >= TapSlop || overUi) return;
            if (Time.unscaledTime - downT < 0.5f || Ui.BlocksWorld || DecorMode) return;
            float px = Cam.pixelHeight / 1544f;
            PlotView best = null; float bestD = 80f * px;
            foreach (var v in plots)
            {
                if (!v.Root.gameObject.activeSelf || v.P.Building < 0 || !Isl.Movable(v.P)) continue;
                Vector3 sp = Cam.WorldToScreenPoint(v.Root.position + Vector3.up * v.Height * 0.45f);
                float d = Vector2.Distance(downPos, sp);
                if (d < bestD) { bestD = d; best = v; }
            }
            if (best == null) { downT = float.MaxValue; return; }
            if (!BeginMove(best.P)) return;
            dragging = false;                // el dedo ahora lleva el edificio, no la camara
            place.Dragging = true;
            place.Grab = best.Root.position - ScreenToGround(downPos); place.Grab.y = 0f;
        }

        /// <summary>Para capturas: lleva lo que se coloca a (x, z) como si el dedo lo arrastrara.</summary>
        public void DebugPlaceAt(float x, float z)
        {
            if (place == null) return;
            if (place.ModK >= 0) { ModDrag(new Vector3(x, 0f, z)); return; }
            place.Pos = new Vector3(Island.Snap(x), 0f, Island.Snap(z));
            place.Valid = Isl.CanPlace(place.V.P, place.Kind, place.Pos.x, place.Pos.z);
        }

        /// <summary>Para capturas: un lugar libre lejos de donde esta (o null).</summary>
        public Vector3? DebugFreeSpot(Plot p, int kind)
        {
            for (float r = 5f; r < Isl.Radius - 1f; r += 0.5f)
                for (int i = 0; i < 48; i++)
                {
                    float a = i * Mathf.PI / 24f;
                    float x = Island.Snap(Mathf.Cos(a) * r), z = Island.Snap(Mathf.Sin(a) * r);
                    if ((x - p.X) * (x - p.X) + (z - p.Z) * (z - p.Z) < 36f) continue;
                    if (Isl.CanPlace(p, kind, x, z)) return new Vector3(x, 0f, z);
                }
            return null;
        }

        void UpdatePlace(float dt)
        {
            CheckLongPress();
            if (place == null) return;
            place.T += dt;
            var obj = PlaceObject;
            if (obj == null) return;
            if (!place.Inited)
            {
                place.Inited = true;
                place.Follow = new Mineros.UI.Spring3(Mineros.UI.Motion.Reduced ? 400f : 170f, Mineros.UI.Motion.Reduced ? 1f : 0.62f, obj.position);
                place.TiltX = Mineros.UI.Spring.Bouncy(); place.TiltZ = Mineros.UI.Spring.Bouncy();
            }
            // sube 0.6 m al levantarlo y flota con un vaivén muy suave; sigue al destino con masa (resorte)
            float lift = place.ModK >= 0 ? (place.CF >= 0 ? 0.3f + IslandArt.FloorY(place.CF) : 0.6f + Mathf.Sin(place.T * 3.2f) * 0.06f + IslandArt.FloorY(ViewFloor))
                : 0.6f + Mathf.Sin(place.T * 3.2f) * 0.06f;   // imantado flota justo encima de su celda
            place.Follow.Target = place.Pos + Vector3.up * lift;
            Vector3 p = place.Follow.Step(dt);
            obj.position = p;
            // inclinacion por velocidad: hasta 11 grados hacia donde se mueve; al frenar se balancea y se endereza
            Vector3 v = place.Follow.Velocity; v.y = 0f;
            float maxT = Mineros.UI.Motion.Reduced ? 0f : 11f;
            place.TiltX.Target = Mathf.Clamp(v.z * 2.2f, -maxT, maxT);
            place.TiltZ.Target = Mathf.Clamp(-v.x * 2.2f, -maxT, maxT);
            float tx = place.TiltX.Step(dt), tz = place.TiltZ.Step(dt);
            place.CurYaw = Mathf.LerpAngle(place.CurYaw, place.Yaw, 1f - Mathf.Exp(-dt * 16f));
            obj.rotation = Quaternion.Euler(tx, 0f, tz) * Quaternion.Euler(0f, place.ModK >= 0 ? place.CurYaw : 0f, 0f);
            // "motor" suave mientras se arrastra rapido
            float spd = v.magnitude;
            if (place.Dragging && spd > 6f && Time.unscaledTime > place.NextBuzz) { place.NextBuzz = Time.unscaledTime + 0.09f; Mineros.Fx.Haptics.Soft(Mathf.Clamp01(spd / 25f) * 0.5f); }
            // la sombra queda en el suelo, mas chica y clara cuanto mas alto
            var blob = place.IsNew ? null : place.V.Blob;
            if (blob != null) { blob.position = new Vector3(p.x, place.BlobY, p.z); blob.rotation = Quaternion.identity; }
            if (!place.IsNew && place.V.Body != null) place.V.Body.localScale = Vector3.one * 1.03f;
        }
    }
}
