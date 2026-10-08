using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using Juice = Mineros.Fx.Juice;

namespace Mineros.IslandView
{
    /// <summary>
    /// Camara y toques de la isla. El arrastre se ancla al suelo: el punto de la isla que se toco queda siempre bajo el
    /// dedo (antes se movia la camara con los pixeles de pantalla y el eje vertical salia invertido). Al soltar sigue con
    /// inercia, los bordes son elasticos y el pellizco hace zoom alrededor del punto medio entre los dedos.
    /// Un toque corto pica una veta, abre la tarjeta de un minero o la hoja de una parcela.
    /// </summary>
    public sealed partial class IslandGame
    {
        public const float ZoomMin = 5f, ZoomMax = 25f;   // isla mas grande y mineros mas chicos: mas rango
        const float TapSlop = 16f;
        const float MinerTapZoom = 11.5f;   // mas lejos que esto, tocar un minero no abre su tarjeta          // pixeles de referencia (a 1544 de alto) para que cuente como toque

        bool dragging, pinching, overUi;
        Vector3 dragAnchor;                  // punto del suelo bajo el dedo al empezar
        Vector2 downPos;
        float dragDist, combo, comboT;
        Vector3 vel;                         // velocidad de la camara (m/s) para la inercia
        float zoomTarget = 15.2f;

        /// <summary>Punto del plano del suelo (y = 0) bajo una posicion de pantalla (pixeles de la camara).</summary>
        public Vector3 ScreenToGround(Vector2 screen)
        {
            Ray r = Cam.ScreenPointToRay(screen);
            if (Mathf.Abs(r.direction.y) < 1e-4f) return r.origin;
            float t = -r.origin.y / r.direction.y;
            return r.origin + r.direction * t;
        }

        float BoundR { get { return Isl.Radius * 0.85f; } }

        void UpdateInput(float dt)
        {
            comboT -= dt;
            if (comboT <= 0f && combo > 0f) { ComboEnded((int)combo); combo = 0f; }
            bool blocked = Ui != null && Ui.BlocksWorld;
            if (blocked) { dragging = pinching = false; oreFingers.Clear(); }
            else if (Input.touchCount > 0) Touches(dt);
            else { oreFingers.Clear(); Mouse(dt); }
            if (!dragging && !pinching && !PlaceHeld) Coast(dt);
            UpdatePlace(dt);
            Cam.orthographicSize = Mathf.Lerp(Cam.orthographicSize, zoomTarget, 1f - Mathf.Exp(-dt * 14f));
        }

        // ------------------------------------------------------------ varios dedos
        // Multitoque (pedido del dueño: "con la veta gigante quiero tocar con 2 o 3 dedos rapido"): cada dedo que
        // APOYA sobre una veta la pica en el momento (como los clickers buenos), sin esperar a que se levante, y ese
        // dedo queda fuera de la camara. Los demas dedos arrastran o pellizcan como siempre.
        readonly HashSet<int> oreFingers = new HashSet<int>();
        readonly Dictionary<int, Vector2> oreDown = new Dictionary<int, Vector2>();
        readonly List<Touch> freeTouches = new List<Touch>();

        void Touches(float dt)
        {
            freeTouches.Clear();
            int n = Input.touchCount;
            for (int i = 0; i < n; i++)
            {
                var t = Input.GetTouch(i);
                int id = t.fingerId;
                if (t.phase == TouchPhase.Began && place == null && !ComplexMode && !OverUi(id))
                {
                    var o = OreAt(t.position);
                    if (o != null)
                    {
                        TapOre(o, t.position);
                        oreFingers.Add(id);
                        oreDown[id] = t.position;
                        continue;
                    }
                }
                if (oreFingers.Contains(id))
                {
                    if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) { oreFingers.Remove(id); continue; }
                    // un solo dedo que empezo en una veta y se arrastra lejos: pasa a mover la camara
                    if (n == 1 && t.phase == TouchPhase.Moved && Vector2.Distance(t.position, oreDown[id]) * 1544f / Mathf.Max(Cam.pixelHeight, 1) > 40f)
                    {
                        oreFingers.Remove(id);
                        Begin(t.position, false);
                        dragDist = TapSlop;   // ya no es un toque
                    }
                    else continue;
                }
                freeTouches.Add(t);
            }
            if (freeTouches.Count >= 2) Pinch(freeTouches[0], freeTouches[1]);
            else if (freeTouches.Count == 1) OneTouch(freeTouches[0], dt);
            else { dragging = false; pinching = false; }
        }

        /// <summary>Veta bajo el punto de pantalla (la gigante tiene un blanco mas grande).</summary>
        OreView OreAt(Vector2 screen)
        {
            float px = Cam.pixelHeight / 1544f;
            OreView bestO = null; float bd = 70f * px;
            foreach (var v in ores.Values)
            {
                float sz = Island.Ores[v.O.Kind].Size * (v.O.Giant ? 2.8f : 1f);
                Vector2 sp = Cam.WorldToScreenPoint(v.T.position + Vector3.up * sz * 0.5f);
                float d = Vector2.Distance(screen, sp) - (v.O.Giant ? 60f * px : 0f);
                if (d < bd) { bd = d; bestO = v; }
            }
            return bestO;
        }

        // ------------------------------------------------------------ un dedo / mouse
        void OneTouch(Touch t, float dt)
        {
            if (pinching) { if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) pinching = false; else { pinching = false; Begin(t.position, OverUi(t.fingerId)); } return; }
            switch (t.phase)
            {
                case TouchPhase.Began: Begin(t.position, OverUi(t.fingerId)); break;
                case TouchPhase.Moved:
                case TouchPhase.Stationary: Move(t.position, dt); break;
                case TouchPhase.Ended: End(t.position, true); break;
                case TouchPhase.Canceled: dragging = false; break;
            }
        }

        void Mouse(float dt)
        {
            // en el telefono el mouse simulado esta apagado (ver Init): esto es para el editor y la PC
            pinching = false;
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f && !OverUi(-1)) ZoomAround(Input.mousePosition, Mathf.Pow(0.88f, wheel));
            if (Input.GetMouseButtonDown(0)) Begin(Input.mousePosition, OverUi(-1));
            else if ((dragging || PlaceHeld) && Input.GetMouseButton(0)) Move(Input.mousePosition, dt);
            if ((dragging || PlaceHeld) && Input.GetMouseButtonUp(0)) End(Input.mousePosition);
        }

        static bool OverUi(int id)
        {
            var es = EventSystem.current;
            return es != null && (id < 0 ? es.IsPointerOverGameObject() : es.IsPointerOverGameObject(id));
        }

        void Begin(Vector2 p, bool ui)
        {
            overUi = ui;
            dragging = !ui;            // los toques sobre botones no mueven la camara
            if (!dragging) return;
            focusing = false;
            downPos = p;
            downT = Time.unscaledTime;
            if (PlaceGrab(p)) { dragging = false; return; }   // se agarro lo que se esta colocando
            dragDist = 0f;
            dragAnchor = ScreenToGround(p);
            vel = Vector3.zero;
        }

        bool PlaceHeld { get { return place != null && place.Dragging; } }

        void Move(Vector2 p, float dt)
        {
            if (PlaceHeld) { PlaceDrag(p, dt); return; }
            if (!dragging) return;
            dragDist = Mathf.Max(dragDist, Vector2.Distance(p, downPos) * 1544f / Mathf.Max(Cam.pixelHeight, 1));
            if (dragDist > 12f) LastPanT = Time.unscaledTime;   // el jugador esta moviendo la camara: nada automatico
            if (dragDist < TapSlop) return;   // todavia puede ser un toque: no mover
            Vector3 before = camRig.position;
            Anchor(p, dragAnchor);
            // limite elastico mientras se arrastra: pasado el borde el movimiento rinde cada vez menos
            Vector3 c = camRig.position; c.y = 0f;
            float m = c.magnitude, b = BoundR;
            if (m > b)
            {
                float over = m - b;
                float soft = b + over / (1f + over * 0.35f);
                camRig.position = c / m * soft;
                dragAnchor = ScreenToGround(p);   // el ancla se corre con el resorte para no "saltar" al volver
            }
            if (dt > 0f) vel = Vector3.Lerp(vel, (camRig.position - before) / dt, 0.5f);
        }

        void End(Vector2 p, bool touch = false)
        {
            if (PlaceHeld) { PlaceRelease(); return; }
            bool was = dragging;
            dragging = false;
            if (!was || overUi) return;
            if (dragDist < TapSlop) { vel = Vector3.zero; Tap(p, touch); }
        }

        /// <summary>Mueve la camara para que el punto `ground` quede bajo la posicion de pantalla `p`.</summary>
        void Anchor(Vector2 p, Vector3 ground)
        {
            Vector3 now = ScreenToGround(p);
            Vector3 d = ground - now; d.y = 0f;
            camRig.position += d;
        }

        /// <summary>Inercia y vuelta elastica al area permitida.</summary>
        void Coast(float dt)
        {
            if (UpdateFocus(dt)) return;
            camRig.position += vel * dt;
            vel *= Mathf.Exp(-dt * 4.5f);
            if (vel.sqrMagnitude < 0.0004f) vel = Vector3.zero;
            Vector3 c = camRig.position; c.y = 0f;
            float m = c.magnitude, b = BoundR;
            if (m > b)
            {
                Vector3 target = c / m * b;
                camRig.position = Vector3.Lerp(c, target, 1f - Mathf.Exp(-dt * 9f));
                vel *= 0.6f;
            }
        }

        // ------------------------------------------------------------ pellizco
        float pinchDist;
        Vector3 pinchAnchor;

        void Pinch(Touch t0, Touch t1)
        {
            Vector2 mid = (t0.position + t1.position) * 0.5f;
            float d = Mathf.Max(Vector2.Distance(t0.position, t1.position), 1f);
            if (!pinching || t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began)
            {
                if (OverUi(t0.fingerId) || OverUi(t1.fingerId)) return;
                PlaceRelease();
                pinching = true;
                LastPanT = Time.unscaledTime;
                dragging = false;
                dragDist = 999f;
                pinchDist = d;
                pinchAnchor = ScreenToGround(mid);
                vel = Vector3.zero;
                return;
            }
            float z = Mathf.Clamp(Cam.orthographicSize * pinchDist / d, ZoomMin * 0.9f, ZoomMax * 1.08f);
            Cam.orthographicSize = zoomTarget = z;
            pinchDist = d;
            // el punto entre los dedos queda fijo (y el pellizco tambien arrastra)
            Anchor(mid, pinchAnchor);
            if (t0.phase == TouchPhase.Ended || t1.phase == TouchPhase.Ended) zoomTarget = Mathf.Clamp(z, ZoomMin, ZoomMax);
        }

        void ZoomAround(Vector2 screen, float factor)
        {
            Vector3 g = ScreenToGround(screen);
            zoomTarget = Cam.orthographicSize = Mathf.Clamp(Cam.orthographicSize * factor, ZoomMin, ZoomMax);
            Anchor(screen, g);
        }

        /// <summary>Para pruebas: centra la camara en `world` con zoom `size` (sin animar).</summary>
        public void DebugLook(Vector3 world, float size)
        {
            focusing = false; vel = Vector3.zero;
            Cam.orthographicSize = zoomTarget = size;
            Vector3 now = ScreenToGround(new Vector2(Cam.pixelWidth * 0.5f, Cam.pixelHeight * 0.5f));
            camRig.position += new Vector3(world.x - now.x, 0f, world.z - now.z);
        }

        /// <summary>Para pruebas: toque sintetico en un punto de la pantalla (pixeles).</summary>
        public void DebugTap(Vector2 screen) { Tap(screen); }

        /// <summary>Para pruebas: arrastre sintetico de `from` a `to` (pixeles). Devuelve el error del ancla en metros.</summary>
        public float DebugDrag(Vector2 from, Vector2 to)
        {
            Begin(from, false);
            dragDist = 999f;
            Vector3 g = dragAnchor;
            Anchor(to, g);
            dragging = false;
            vel = Vector3.zero;
            Vector3 after = ScreenToGround(to);
            return Vector2.Distance(new Vector2(after.x, after.z), new Vector2(g.x, g.z));
        }

        // ------------------------------------------------------------ toques en el mundo
        /// <param name="oresDone">con el dedo, las vetas ya se picaron al apoyar (Touches): no picar dos veces</param>
        void Tap(Vector2 screen, bool oresDone = false)
        {
            float px = Cam.pixelHeight / 1544f;
            if (place != null) { PlaceTapTo(screen); return; }   // colocando: tocar el suelo lo lleva ahi
            if (ComplexTap(screen)) return;                       // Modo Cuartel: elegir modulos
            // 0) el globo del mercader: ruleta gratis
            if (Isl.BalloonHere && Ambient.BalloonVisible)
            {
                Vector2 bp = Cam.WorldToScreenPoint(Ambient.BalloonPos + Vector3.up * 1.2f);
                if (Vector2.Distance(screen, bp) < 95f * px)
                {
                    Isl.TapBalloon();
                    Ui.OpenWheel();
                    return;
                }
            }
            // 0b) bichos y botella: duran poco, van primero
            if (TapCritter(screen)) return;
            if (TapBoard(screen)) return;
            if (TapDecorSlot(screen)) return;
            if (TapWonder(screen)) return;
            if (Isl.CurBottle != null && !Isl.CurBottle.Opened && bottleT != null && bottleT.gameObject.activeSelf)
            {
                Vector2 bs = Cam.WorldToScreenPoint(bottleT.position + Vector3.up * 0.2f);
                if (Vector2.Distance(screen, bs) < 100f * px) { Isl.OpenBottle(); return; }
            }
            // 1) vetas: el jugador ayuda a picar (con combo)
            OreView bestO = OreAt(screen);
            if (bestO != null) { if (!oresDone) TapOre(bestO, screen); return; }
            // 2) mineros: tarjeta
            // de lejos los mineros son puntitos: tocarlos abria la tarjeta sin querer (pedido del dueño). Solo con zoom
            // de cerca, y con un blanco mas justo.
            MinerView bestM = null; float bm = Cam.orthographicSize > MinerTapZoom && Isl.TutDone ? -1f : (Isl.TutDone ? 48f : 80f) * px;
            foreach (var mv in miners.Values)
            {
                Vector2 sp = Cam.WorldToScreenPoint(mv.Model.transform.position + Vector3.up * MH(1.1f));
                float d = Vector2.Distance(screen, sp);
                if (d < bm) { bm = d; bestM = mv; }
            }
            if (bestM != null)
            {
                Juice.Punch(bestM.Model.transform, 0.18f, 0.25f);
                Sfx.Play("ui", -4f, 1.3f);
                bool tut = Isl.Tut == Island.TutStep.WatchMiner;
                Isl.AddStat("miner_taps", 1);
                if (tut) { bestM.Celebrate = 0.8f; Ui.Popup(bestM.Model.transform.position + Vector3.up * MH(2.5f), Loc.T("¡Yo pico solo mientras construís!"), Color.white, 28); }
                else MinerSays(bestM.M);   // salta y contesta segun como esta (13)
                Ui.ShowMiner(bestM.M);
                return;
            }
            // 2b) con el techo abierto por el zoom: lo de adentro del Cuartel antes que el Cuartel entero
            if (TapThroughRoof(screen)) return;
            // 3) parcelas y edificios
            PlotView best = null; float bestD = 90f * px;
            foreach (var v in plots)
            {
                if (!v.Root.gameObject.activeSelf) continue;
                Vector3 sp = Cam.WorldToScreenPoint(v.Root.position + Vector3.up * (v.P.Building >= 0 ? v.Height * 0.45f : 0.2f));
                float d = Vector2.Distance(screen, sp);
                if (d < bestD) { bestD = d; best = v; }
            }
            if (best == null)
            {
                // nada que tocar ahi: el suelo, el agua o un arbol igual responden
                Vector3 g = ScreenToGround(screen);
                Ui.GroundTap(screen, g, g.x * g.x + g.z * g.z > Isl.Radius * Isl.Radius);
                return;
            }
            Sfx.Play("ui", -6f);
            Juice.Punch(best.Root, 0.08f, 0.2f);
            if (best.P.Building < 0 && !Isl.Offered(best.P)) return;
            if (Ui.TapCollect(best.P)) return;   // tiene algo listo: tocar el edificio COBRA (el panel, con el segundo toque)
            Ui.OpenPlot(best.P);
        }

        public float Combo { get { return combo; } }
    }
}
