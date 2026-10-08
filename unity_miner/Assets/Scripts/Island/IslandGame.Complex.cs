using System.Collections;
using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using Mineros.UI;
using Mineros.World;
using UnityEngine;
using FxApi = Mineros.Fx.Fx;
using Juice = Mineros.Fx.Juice;

namespace Mineros.IslandView
{
    /// <summary>
    /// Vista del Complejo minero (el Cuartel como LEGO). Las carcasas de cada piso se hornean con autounion (una malla de
    /// paredes y una de techos por piso); adentro van las piezas de Tripo y los detalles de cada modulo. Las vagonetas
    /// recorren las lineas y cada llegada hace CLACK en la maquina. En el Modo Cuartel la camara se acerca, los techos del
    /// piso elegido se esconden (se ve adentro: mineros, maquinas, vagonetas) y los pisos de arriba desaparecen.
    /// Todas las reglas las decide el nucleo; aca solo se dibuja y se anima.
    /// </summary>
    public sealed partial class IslandGame
    {
        sealed class CartView { public Transform T; public MeshRenderer Load; public bool Bar; public int LastStep = -1; }

        Transform cxRoot;
        readonly Transform[] cxFloor = new Transform[3];
        readonly MeshRenderer[] cxWalls = new MeshRenderer[3], cxRoofs = new MeshRenderer[3];
        readonly Dictionary<int, Transform> cxMods = new Dictionary<int, Transform>();
        readonly Dictionary<int, Transform> cxProps = new Dictionary<int, Transform>();
        readonly Dictionary<int, CartView> cxCarts = new Dictionary<int, CartView>();
        bool cxDirty = true;
        int cxBuiltPlot = -1;
        float moodFxT;

        public bool ComplexMode { get; private set; }
        public int ViewFloor { get; private set; }
        public Module SelectedMod;
        /// <summary>Celdas que se acaban de habilitar (al subir el Cuartel): la interfaz las hace latir unos segundos.</summary>
        public readonly List<int[]> NewCells = new List<int[]>();
        public float NewCellsT;
        int lastBarracksLevel = -1, lastPowered = -1;

        void InitComplex()
        {
            Isl.LayoutChanged += () => cxDirty = true;
            Isl.ModuleChanged += m => cxDirty = true;
            Isl.CartSpawned += OnCartSpawned;
            Isl.CartArrived += OnCartArrived;
            Isl.CartSold += OnCartSold;
            Isl.ModuleExpired += m => { var w = ModWorld(m); FxApi.Play("dust", w + Vector3.up * 0.5f, new Color(0.9f, 0.85f, 0.7f), 1.6f); Sfx.Play("whoosh", -8f, 1.3f); Ui.Toast(Island.MDef(m.Kind).Name + Loc.T(": se terminó"), new Color(0.85f, 0.88f, 0.92f)); };
            Isl.ModuleReady += m =>
            {
                // obra terminada: se va el andamio (re-horneado) con estallido, aura y "ta-da"
                cxDirty = true;
                var w = ModWorld(m);
                FxApi.Play("levelup_aura", w + Vector3.up * 0.3f, new Color(1f, 0.92f, 0.5f), 1.4f);
                FxApi.Play("unlock_burst", w + Vector3.up * 1.0f, new Color(1f, 0.9f, 0.5f), 1.3f);
                FxApi.Play("dust", w + Vector3.up * 0.2f, IslandGround.Dirt, 1.4f);
                Sfx.Play("cx_evolve", -3f);
                Sfx.PlayLater("fanfare_short", 0.15f, -8f);
                Mineros.Fx.Haptics.Success();
            };
            Isl.GemsFromVault += n => { var v = Isl.FirstMod(ModKind.Vault); if (v != null) Ui.FlyGemsFromWorld(ModWorld(v) + Vector3.up * 1.2f, n); Sfx.Play("gem", -6f, 1.2f); };
            Isl.ComboDiscovered += i => Ui.ComboFound(i);
            Isl.SecretRevealed += (m, o, d) => StartCoroutine(SecretShow(m, o, d));
            Isl.PlansFound += n => { Ui.Toast(Loc.T("¡Plano secreto!"), new Color(0.75f, 0.55f, 1f), Icons.Get("star"), true); Sfx.Play("gleam", -6f); };
            Isl.PortableGot += k => Ui.Toast(Island.MDef(k).Name + Loc.T(" (portátil)"), new Color(0.6f, 0.9f, 1f), null, true);
            Isl.ContractEnded += (c, ok) => Ui.ContractEnded(c, ok);
            Isl.CxEventStarted += e => { Sfx.Play("bell", -8f, 1.3f); Ui.CxEventStarted(e); };
            Isl.CxEventEnded += e => Ui.CxEventEnded(e);
            Isl.MeteorSoon += () => { Ui.Toast(Loc.T("¡El Observatorio ve meteoritos en camino!"), Kit3.Blue, Icons.Get("star"), true); Sfx.Play("gleam", -6f); };
        }

        // ------------------------------------------------------------ horneado
        PlotView BarracksView { get { var p = Isl.BarracksPlot; return p == null ? null : plots[p.Id]; } }

        /// <summary>Punto del mundo del centro de un modulo (a la altura de su piso).</summary>
        public Vector3 ModWorld(Module m)
        {
            if (cxRoot != null) return cxRoot.TransformPoint(IslandArt.CellPos(m.X, m.Z, m.F));
            float x, z; Isl.ModWorld(m, out x, out z);
            return new Vector3(x, IslandArt.FloorY(m.F), z);
        }

        public Vector3 CellWorld(int x, int z, int f)
        {
            if (cxRoot != null) return cxRoot.TransformPoint(IslandArt.CellPos(x, z, f));
            var b = Isl.BarracksPlot;
            float ox, oz; Island.CellOffset(x, z, out ox, out oz);
            return new Vector3((b != null ? b.X : 0f) + ox, IslandArt.FloorY(f), (b != null ? b.Z : 0f) + oz);
        }

        /// <summary>Habitacion que se esta armando con piezas que caen: el horneado la deja afuera hasta que termina.</summary>
        Module cxBuilding;

        void RebuildComplex()
        {
            cxDirty = false;
            var v = BarracksView;
            if (v == null || v.P.Level < 1 || Isl.Modules.Count == 0)
            {
                if (cxRoot != null) Destroy(cxRoot.gameObject);
                cxRoot = null; cxMods.Clear(); cxProps.Clear(); cxDoors.Clear(); life.Clear();
                return;
            }
            if (cxRoot == null || cxBuiltPlot != v.P.Id)
            {
                if (cxRoot != null) Destroy(cxRoot.gameObject);
                cxRoot = new GameObject("Complejo").transform;
                cxRoot.SetParent(v.Root, false);
                cxRoot.localRotation = Quaternion.Euler(0f, Island.SlotYaw, 0f);
                cxBuiltPlot = v.P.Id;
            }
            life.Clear();
            for (int f = 0; f < 3; f++)
            {
                if (cxFloor[f] != null) Destroy(cxFloor[f].gameObject);
                cxFloor[f] = new GameObject("Piso" + f).transform;
                cxFloor[f].SetParent(cxRoot, false);
                var walls = new MeshBuilder();
                var roofs = new MeshBuilder();
                var kitWalls = new List<RoomKit.Slot>();
                var kitRoofs = new List<RoomKit.Slot>();
                var slabs = new MeshBuilder();
                IslandArt.ComplexFloor(Isl, f, walls, roofs, kitWalls, kitRoofs, slabs, cxBuilding);
                cxWalls[f] = IslandArt.Bake(walls, cxFloor[f], "Paredes", true);
                cxRoofs[f] = IslandArt.Bake(roofs, cxFloor[f], "Techos", true);
                // piezas modeladas del kit: hijas de las paredes y techos de codigo (bajan y se esconden con ellos)
                RoomKit.Bake(kitWalls, cxWalls[f].transform, "Paredes", true);
                CollectLife(f, kitWalls, cxWalls[f].transform);
                CollectLife(f, kitRoofs, cxRoofs[f].transform);
                RoomKit.Bake(kitRoofs, cxRoofs[f].transform, "Techos", true);
                MakeRoofsCuttable(f);   // antes de las losas: esas no se disuelven
                MakeWallsCuttable(cxWalls[f].transform, cxFloor[f].position.y + IslandArt.SlabH);   // y las paredes del lado de la camara
                if (slabs.TriCount > 0) IslandArt.Bake(slabs, cxRoofs[f].transform, "Losas", true);
            }
            cxMods.Clear(); cxProps.Clear();
            foreach (var m in Isl.Modules) BuildModuleObject(m);
            BuildDoors();
            ApplyFloorView(true);
            MarkOptimize();
        }

        void BuildModuleObject(Module m)
        {
            var t = new GameObject("Modulo" + m.Id).transform;
            t.SetParent(cxFloor[Mathf.Clamp(m.F, 0, 2)], false);
            t.localPosition = IslandArt.CellPos(m.X, m.Z, m.F);
            t.localRotation = Quaternion.Euler(0f, m.Rot * 90f, 0f);
            cxMods[m.Id] = t;
            var prop = MakeProp(m.Kind, t, m);
            if (prop != null) cxProps[m.Id] = prop;
            var mb = new MeshBuilder();
            IslandArt.ModuleDetails(mb, m, prop != null);
            if (mb.TriCount > 0) IslandArt.Bake(mb, t, "Detalles", false);
            if (m.Expires > 0) FxApi.Attach("aura", t, new Color(0.6f, 0.9f, 1f), 0.8f);
            if (m.Work > 0 && m.Kind == ModKind.Secret) FxApi.Attach("aura", t, new Color(0.7f, 0.45f, 1f), 1.2f);
            else if (m.Work > 0) MakeScaffold(t);   // sube de etapa: andamio hasta que termina la obra
        }

        /// <summary>Pieza de Tripo del modulo (null si no tiene o falta el archivo).</summary>
        Transform MakeProp(ModKind k, Transform parent, Module m)
        {
            string file = IslandArt.PropFile(k);
            if (file == "") return null;
            Material mat;
            var mesh = IslandArt.TripoModel(file, out mat);
            if (mesh == null) return null;
            // adentro de las habitaciones la sombra no se ve: sin sombra (la mitad de triangulos)
            var r = IslandArt.MakeRenderer(parent, "Pieza", mesh, new[] { mat }, k == ModKind.Windmill || k == ModKind.Drill);
            var b = mesh.bounds;
            float foot = Mathf.Max(b.size.x, b.size.z, 0.01f);
            float s = (m != null ? IslandArt.PropSize(m) : 1.1f) / foot;
            r.transform.localScale = Vector3.one * s;
            r.transform.localPosition = new Vector3(0f, IslandArt.SlabH, k == ModKind.Treasury || k == ModKind.Mess ? 0.1f : 0f);
            return r.transform;
        }

        /// <summary>Paredes del piso elegido bajas en el Modo Cuartel (como una casa de munecas): se ve todo adentro.</summary>
        void UpdateWallsDown(float dt)
        {
            for (int f = 0; f < 3; f++)
            {
                var w = cxWalls[f];
                if (w == null) continue;
                // techo v2: a zoom maximo con el techo abierto las paredes bajan como en el Modo Cuartel (se ve adentro);
                // solo con un piso (si hay pisos arriba quedarian flotando)
                bool peek = !ComplexMode && f == 0 && roofCutAmt > 0.8f && Cam.orthographicSize <= 6.3f && MaxModFloor() == 0;
                float want = ComplexMode && f == ViewFloor ? 0.45f : peek ? 0.55f : 1f;
                var t = w.transform;
                float s = Mathf.MoveTowards(t.localScale.y, want, dt * 3.5f);
                if (Mathf.Abs(s - t.localScale.y) < 1e-4f && Mathf.Abs(t.localScale.y - want) < 1e-4f) continue;
                t.localScale = new Vector3(1f, s, 1f);
                t.localPosition = new Vector3(0f, IslandArt.FloorY(f) * (1f - s), 0f);   // baja desde el piso de ese nivel
            }
        }

        int MaxModFloor() { int mf = 0; foreach (var m in Isl.Modules) if (m.F > mf) mf = m.F; return mf; }

        /// <summary>Que se ve segun el piso elegido: abajo todo, el piso elegido sin techo, arriba nada.</summary>
        void ApplyFloorView(bool instant)
        {
            if (cxRoot == null) return;
            for (int f = 0; f < 3; f++)
            {
                if (cxFloor[f] == null) continue;
                bool show = !ComplexMode || f <= ViewFloor;
                if (cxFloor[f].gameObject.activeSelf != show) cxFloor[f].gameObject.SetActive(show);
                // el techo del piso elegido se disuelve (no se apaga de golpe): RoofCut lo anima
                if (cxRoofs[f] != null) SetRoofGone(f, ComplexMode && f >= ViewFloor, instant);
            }
        }

        // ------------------------------------------------------------ modo Cuartel
        public void EnterComplex()
        {
            var p = Isl.BarracksPlot;
            if (p == null || p.Level < 1) return;
            if (place != null) CancelPlace();
            ComplexMode = true;
            ViewFloor = 0;
            SelectedMod = null;
            ApplyFloorView(false);
            var c = new Vector3(p.X, 0f, p.Z);
            focusing = false; vel = Vector3.zero;
            StartCoroutine(ComplexCamera(c, 8.4f));
            Sfx.Play("cx_open", -5f);
            Mineros.Fx.Haptics.Light();
            Ui.ComplexOpened();
        }

        public void ExitComplex()
        {
            if (!ComplexMode) return;
            if (place != null && place.ModK >= 0) CancelPlace();
            ComplexMode = false;
            SelectedMod = null;
            ApplyFloorView(false);
            zoomTarget = Mathf.Max(zoomTarget, 12.5f);
            Sfx.Play("cx_close", -7f);
            Ui.ComplexClosed();
        }

        public void SetViewFloor(int f)
        {
            f = Mathf.Clamp(f, 0, MaxFloorUnlocked());
            if (f == ViewFloor) return;
            ViewFloor = f;
            ApplyFloorView(false);
            Sfx.Play("cx_tick", -6f, 1f + f * 0.12f);
            Sfx.Note(3 + f * 2, -16f);   // cada piso, una nota mas arriba
            Mineros.Fx.Haptics.Selection();
            if (place != null && place.ModK >= 0) { place.CF = -1; place.Valid = false; }
        }

        public int MaxFloorUnlocked() { int lv = Isl.BarracksLevel; return lv >= 9 ? 2 : lv >= 5 ? 1 : 0; }

        IEnumerator ComplexCamera(Vector3 target, float size)
        {
            Vector3 from = camRig.position;
            float z0 = Cam.orthographicSize;
            // el destino se calcula con el zoom FINAL (con el de antes el Cuartel quedaba bajo, tapado por la bandeja)
            Cam.orthographicSize = size;
            Vector3 now = ScreenToGround(new Vector2(Cam.pixelWidth * 0.5f, Cam.pixelHeight * 0.58f));
            Cam.orthographicSize = z0;
            Vector3 to = camRig.position + new Vector3(target.x - now.x, 0f, target.z - now.z);
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                float u = Mineros.UI.Tw.Eval(Mineros.UI.Ease.OutCubic, t / 0.35f);
                camRig.position = Vector3.Lerp(from, to, u);
                Cam.orthographicSize = zoomTarget = Mathf.Lerp(z0, size, u);
                yield return null;
            }
            camRig.position = to; Cam.orthographicSize = zoomTarget = size;
        }

        /// <summary>Toque en el Modo Cuartel: selecciona un modulo, o sale si se toca lejos del Complejo.</summary>
        bool ComplexTap(Vector2 screen)
        {
            if (!ComplexMode) return false;
            float px = Cam.pixelHeight / 1544f;
            Module best = null; float bd = 80f * px;
            foreach (var m in Isl.Modules)
            {
                if (m.F > ViewFloor) continue;
                Vector2 sp = Cam.WorldToScreenPoint(ModWorld(m) + Vector3.up * 0.6f);
                float d = Vector2.Distance(screen, sp) - (m.F == ViewFloor ? 12f * px : 0f);
                if (d < bd) { bd = d; best = m; }
            }
            // comerciante: se lo puede llevar una vez a otra puerta exterior
            var ev = Isl.CurEvent;
            if (best != null && ev != null && ev.Kind == CxEventKind.Merchant && !ev.Moved && best.Id != ev.ModId && Isl.MoveMerchant(best.X, best.Z))
            {
                Sfx.Play("whoosh", -6f, 1.2f);
                Mineros.Fx.Haptics.Medium();
                Ui.Toast(Loc.T("El comerciante se mudó de puerta"), Kit3.Yellow, null, true);
                return true;
            }
            if (best != null)
            {
                SelectedMod = SelectedMod == best ? null : best;
                Transform t;
                if (SelectedMod != null && cxMods.TryGetValue(best.Id, out t)) Juice.Punch(t, 0.08f, 0.2f);
                Sfx.Play("ui", -6f, 1.2f);
                Mineros.Fx.Haptics.Light();
                Ui.ModSelected(SelectedMod);
                return true;
            }
            var b = Isl.BarracksPlot;
            Vector3 g = ScreenToGround(screen);
            if (b != null && (new Vector2(g.x - b.X, g.z - b.Z)).magnitude > Island.BarracksRadius(b.Level) + 2.5f) { ExitComplex(); return true; }
            if (SelectedMod != null) { SelectedMod = null; Ui.ModSelected(null); }
            return true;
        }

        // ------------------------------------------------------------ colocar modulos (imán)
        public bool PlacingMod { get { return place != null && place.ModK >= 0; } }
        public double PlaceDelta { get { return place != null ? place.Delta : 0; } }
        public int PlaceModKind { get { return place != null ? place.ModK : -1; } }
        public List<int> PlacePartners { get { return place != null ? place.Partners : null; } }

        /// <summary>Empezar a colocar un modulo nuevo (o el especialista `ch` en un dormitorio).</summary>
        public bool BeginModPlace(ModKind k, int ch = -1)
        {
            return BeginModInternal(k, ch, null);
        }

        public bool BeginModMove(Module m)
        {
            if (m == null || m.Kind == ModKind.Central) return false;
            return BeginModInternal(m.Kind, m.Ch, m);
        }

        bool BeginModInternal(ModKind k, int ch, Module moving)
        {
            var bp = Isl.BarracksPlot;
            if (bp == null || bp.Level < 1) return false;
            if (place != null) CancelPlace();
            if (!ComplexMode) EnterComplex();
            var v = plots[bp.Id];
            var ghost = new GameObject("Fantasma").transform;
            ghost.SetParent(root, false);
            var body = new GameObject("Cuerpo").transform;
            body.SetParent(ghost, false);
            var gm = new Module { Kind = k, Ch = ch, Stage = moving != null ? moving.Stage : 1 };
            var mb = new MeshBuilder();
            IslandArt.ModuleGhost(mb, k, ch, gm.Stage);
            IslandArt.Bake(mb, body, "Carcasa", true);
            var prop = MakeProp(k, body, gm);
            var db = new MeshBuilder();
            IslandArt.ModuleDetails(db, gm, prop != null);
            if (db.TriCount > 0) IslandArt.Bake(db, body, "Detalles", true);
            // empieza en la mejor celda segun la vista previa (o al frente, afuera)
            int bx = 0, bz = -2, bf = ViewFloor;
            double bestD = double.MinValue; bool any = false;
            var cells = Isl.FreeCells(k, moving);
            foreach (var c in cells)
            {
                if (c[2] != ViewFloor && cells.Exists(q => q[2] == ViewFloor)) continue;
                int r = Isl.BestRotation(k, c[0], c[1], c[2], moving);
                double d = Isl.PreviewDelta(k, ch, c[0], c[1], c[2], r, moving) - 0.01 * (Mathf.Abs(c[0]) + Mathf.Abs(c[1]));
                if (d > bestD) { bestD = d; bx = c[0]; bz = c[1]; bf = c[2]; any = true; }
            }
            if (bf != ViewFloor) { ViewFloor = bf; ApplyFloorView(false); }
            Vector3 start = CellWorld(bx, bz, any ? bf : ViewFloor);
            if (!any) start += Vector3.back * 1.5f;
            ghost.position = start + Vector3.up * 0.6f;
            place = new PlaceState { V = v, Kind = -1, IsNew = true, Orig = start, Pos = start, Ghost = ghost, ModK = (int)k, ModCh = ch, MoveMod = moving };
            place.Yaw = place.CurYaw = Island.SlotYaw;
            ModDrag(start);
            Sfx.Play("cx_grab", -5f);
            Mineros.Fx.Haptics.Light();
            Ui.PlaceStarted(true, true);
            return true;
        }

        /// <summary>El modulo sigue al dedo; cerca de una celda valida salta a ella y gira solo para conectar (CLACK).</summary>
        void ModDrag(Vector3 g)
        {
            var b = place.V.P;
            float lx, lz;
            Island.WorldToCell(g.x - b.X, g.z - b.Z, out lx, out lz);
            var k = (ModKind)place.ModK;
            int bestX = 0, bestZ = 0, bestR = -1; float bd = 0.75f * 0.75f;
            int f = ViewFloor;
            for (int x = -2; x <= 2; x++)
                for (int z = -2; z <= 2; z++)
                {
                    float d = (x - lx) * (x - lx) + (z - lz) * (z - lz);
                    if (d >= bd) continue;
                    int r = Isl.BestRotation(k, x, z, f, place.MoveMod);
                    if (r < 0) continue;
                    bd = d; bestX = x; bestZ = z; bestR = r;
                }
            bool snapped = bestR >= 0;
            bool changed = snapped != (place.CF >= 0) || (snapped && (bestX != place.CX || bestZ != place.CZ || f != place.CF));
            if (snapped)
            {
                place.CX = bestX; place.CZ = bestZ; place.CF = f;
                if (changed) place.Rot = bestR;
                place.Pos = CellWorld(bestX, bestZ, 0);
                place.Valid = true;
                place.Yaw = Island.SlotYaw + place.Rot * 90f;
                if (changed)
                {
                    place.Delta = Isl.PreviewDelta(k, place.ModCh, bestX, bestZ, f, place.Rot, place.MoveMod);
                    place.Partners = Isl.PreviewPartners(k, place.ModCh, bestX, bestZ, f, place.Rot, place.MoveMod);
                    Sfx.Play("cx_snap", -4f);
                    // el lugar "canta": cuantas mas conexiones y ganancia, mas aguda la nota (se busca la mejor)
                    int tone = Mathf.Clamp(place.Partners.Count + (place.Delta > 5 ? 2 : place.Delta > 0.5 ? 1 : 0), 0, 7);
                    Sfx.Note(tone, -11f);
                    Mineros.Fx.Haptics.Medium();
                    if (place.Ghost != null) Juice.Punch(place.Ghost, 0.08f, 0.16f);
                }
            }
            else
            {
                if (place.CF >= 0) { Sfx.Play("cx_tick", -10f, 0.85f); Mineros.Fx.Haptics.Light(); }
                place.CF = -1;
                place.Valid = false;
                place.Delta = 0;
                place.Partners.Clear();
                place.Pos = new Vector3(g.x, 0f, g.z);
            }
        }

        /// <summary>Boton ⟳: el siguiente giro valido (solo los que no rompen lo conectado).</summary>
        public void RotatePlacing()
        {
            if (place == null || place.ModK < 0 || place.CF < 0) return;
            var rs = Isl.ValidRotations((ModKind)place.ModK, place.CX, place.CZ, place.CF, place.MoveMod);
            if (rs.Count <= 1) { Sfx.Play("cx_deny", -6f); Ui.PlaceDenied(); return; }
            int i = rs.IndexOf(place.Rot);
            place.Rot = rs[(i + 1) % rs.Count];
            place.Yaw = Island.SlotYaw + place.Rot * 90f;
            place.Delta = Isl.PreviewDelta((ModKind)place.ModK, place.ModCh, place.CX, place.CZ, place.CF, place.Rot, place.MoveMod);
            place.Partners = Isl.PreviewPartners((ModKind)place.ModK, place.ModCh, place.CX, place.CZ, place.CF, place.Rot, place.MoveMod);
            Sfx.Play("cx_rot", -4f);
            Sfx.Note(Mathf.Clamp(place.Partners.Count + 1, 0, 7), -14f);
            Mineros.Fx.Haptics.Selection();
        }

        public int PlaceRotations { get { return place == null || place.ModK < 0 || place.CF < 0 ? 0 : Isl.ValidRotations((ModKind)place.ModK, place.CX, place.CZ, place.CF, place.MoveMod).Count; } }

        void ConfirmMod()
        {
            var s = place;
            if (!s.Valid || s.CF < 0) { Sfx.Play("cx_deny", -5f); Ui.PlaceDenied(); return; }
            var k = (ModKind)s.ModK;
            Module m;
            if (s.MoveMod != null) m = Isl.MoveModule(s.MoveMod, s.CX, s.CZ, s.CF, s.Rot) ? s.MoveMod : null;
            else if (k == ModKind.Dorm) m = Isl.BuyModule(k, s.CX, s.CZ, s.CF, s.Rot, s.ModCh);
            else if (k == ModKind.Secret) m = Isl.BuySecret(s.CX, s.CZ, s.CF, s.Rot);
            else if (k == ModKind.Experimental) m = Isl.BuyExperimental(s.CX, s.CZ, s.CF, s.Rot);
            else if (Island.MDef(k).Portable) m = Isl.PlacePortable(k, s.CX, s.CZ, s.CF, s.Rot);
            else m = Isl.BuyModule(k, s.CX, s.CZ, s.CF, s.Rot);
            if (m == null)
            {
                Sfx.Play("cx_deny", -5f);
                Ui.PlaceDenied();
                Ui.Toast(Loc.T("Te faltan monedas o materiales"), new Color(0.85f, 0.85f, 0.9f), null, true);
                return;
            }
            Vector3 from = s.Ghost.position;
            Quaternion rot = s.Ghost.rotation;
            place = null;
            Ui.PlaceEnded();
            Destroy(s.Ghost.gameObject);
            // nueva (no mudanza): se arma pieza por pieza; mientras tanto el horneado no la incluye
            bool assemble = s.MoveMod == null && BuildAnimOk(m);
            if (assemble) cxBuilding = m;
            RebuildComplex();
            if (assemble) StartCoroutine(AssembleRoom(m));
            StartCoroutine(ModDrop(m, from, rot));
            Save();
        }

        /// <summary>El modulo cae a su celda: CLACK, polvo, y cada union con un vecino hace su propio "clack".</summary>
        IEnumerator ModDrop(Module m, Vector3 fromWorld, Quaternion fromRot)
        {
            Transform t;
            if (!cxMods.TryGetValue(m.Id, out t)) yield break;
            Vector3 to = t.localPosition;
            Vector3 fromLocal = t.parent.InverseTransformPoint(fromWorld);
            for (float k = 0f; k < 0.14f; k += Time.deltaTime)
            {
                float u = k / 0.14f;
                t.localPosition = Vector3.Lerp(fromLocal, to, u * u);
                yield return null;
            }
            t.localPosition = to;
            Vector3 at = ModWorld(m);
            Sfx.Play("cx_drop", -2f);
            Mineros.Fx.Haptics.Heavy();
            FxApi.Play("dust", at + Vector3.up * 0.2f, IslandGround.Dirt, 1.8f);
            if (Ambient != null) Ambient.ShakeNear(at, 5f, 4f);
            var bv = BarracksView;
            if (bv != null && bv.Body != null) Juice.Punch(bv.Body, 0.05f, 0.2f);
            // clack por cada union (vecinos, arriba/abajo), uno tras otro
            int i = 0;
            foreach (var n in Isl.Neighbors(m))
            {
                yield return new WaitForSeconds(0.07f);
                Vector3 mid = (at + ModWorld(n)) * 0.5f + Vector3.up * 1.0f;
                FxApi.Play("hit_spark", mid, new Color(1f, 0.9f, 0.55f), 0.8f);
                Sfx.Play("cx_snap", -12f, 1.1f);
                Sfx.Note(i, -8f);   // do, re, mi, sol, la...: cada union una nota mas
                Mineros.Fx.Haptics.Light();
                Transform nt;
                if (cxMods.TryGetValue(n.Id, out nt)) Juice.Punch(nt, 0.05f, 0.18f);
                i++;
            }
            // squash
            for (float k = 0f; k < 0.35f; k += Time.deltaTime)
            {
                float u = Mathf.Clamp01(k / 0.35f);
                float q = Mathf.Sin(u * Mathf.PI * 2f) * (1f - u) * 0.12f;
                if (t != null) t.localScale = new Vector3(1f + q, 1f - q, 1f + q);
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one;
            Sfx.Play("cx_done", -5f);
            Mineros.Fx.Haptics.Success();
            Ui.ModPlacedFx(m);
        }

        // ------------------------------------------------------------ vagonetas
        Mesh cartMesh; Material cartMat;

        void OnCartSpawned(Cart c)
        {
            if (cxRoot == null) RebuildComplex();
            if (cxRoot == null) return;
            var cv = new CartView();
            cv.T = new GameObject("Vagoneta" + c.Id).transform;
            cv.T.SetParent(cxFloor[0], false);
            if (cartMesh == null) cartMesh = IslandArt.TripoModel("cx_vagoneta", out cartMat);
            if (cartMesh != null)
            {
                var r = IslandArt.MakeRenderer(cv.T, "Vagoneta", cartMesh, new[] { cartMat }, false);
                float foot = Mathf.Max(cartMesh.bounds.size.x, cartMesh.bounds.size.z, 0.01f);
                r.transform.localScale = Vector3.one * (0.62f / foot);
                r.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            else
            {
                Material[] mats;
                var m = IslandArt.CartMesh(c.Kind, out mats);
                IslandArt.MakeRenderer(cv.T, "Vagoneta", m, mats);
            }
            Material[] lm;
            var lmesh = IslandArt.LoadMesh(c.Kind, false, out lm);
            cv.Load = IslandArt.MakeRenderer(cv.T, "Carga", lmesh, lm, false);
            cv.Load.transform.localPosition = new Vector3(0f, 0.36f, 0f);
            cxCarts[c.Id] = cv;
            PlaceCart(c, cv);
            var from = Isl.ModById(c.From);
            if (from != null) { Transform mt; if (cxMods.TryGetValue(from.Id, out mt)) Juice.Punch(mt, 0.05f, 0.15f); }
        }

        void PlaceCart(Cart c, CartView cv)
        {
            var a = Isl.ModById(c.From);
            var b = c.Step < c.Path.Length ? Isl.ModById(c.Path[c.Step]) : null;
            if (a == null) return;
            Vector3 pa = IslandArt.CellPos(a.X, a.Z, 0) + Vector3.up * (IslandArt.SlabH + 0.02f);
            Vector3 pb = b != null ? IslandArt.CellPos(b.X, b.Z, 0) + Vector3.up * (IslandArt.SlabH + 0.02f) : pa;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(c.T));
            cv.T.localPosition = Vector3.Lerp(pa, pb, u) + Vector3.up * Mathf.Abs(Mathf.Sin(u * Mathf.PI * 3f)) * 0.015f;
            Vector3 dir = pb - pa;
            if (dir.sqrMagnitude > 1e-4f) cv.T.localRotation = Quaternion.LookRotation(dir, Vector3.up);
            bool jam = Isl.JamCart == c.Id;
            if (jam) cv.T.localPosition += new Vector3(Mathf.Sin(Time.time * 40f) * 0.02f, 0f, 0f);
        }

        void OnCartArrived(Cart c, Module m)
        {
            Transform t;
            Vector3 at = ModWorld(m) + Vector3.up * 0.9f;
            bool visible = ComplexMode || cxRoofs[0] == null || !cxRoofs[0].enabled || roofCutAmt > 0.5f;
            if (cxProps.TryGetValue(m.Id, out t) || cxMods.TryGetValue(m.Id, out t)) Juice.Punch(t, 0.14f, 0.22f);
            var col = IslandArt.ModRoof(m);
            FxApi.Play("hit_spark", at, col, ComplexMode ? 1f : 0.6f);
            if (!ComplexMode) FxApi.Play("dust", ModWorld(m) + Vector3.up * (IslandArt.SlabH + IslandArt.WallH + 0.6f), new Color(1f, 1f, 1f, 0.6f), 0.5f);
            // la cascada sube por la escala: do-mi-sol-la... cada maquina una nota (mas lejos de la Descarga, mas aguda)
            float vol = ComplexMode ? 0f : -10f;
            string hit = m.Kind == ModKind.Smelter || m.Kind == ModKind.Polisher ? "cx_m_smelt" : m.Kind == ModKind.Crusher ? "cx_m_crush" : "cx_m_wood";
            Sfx.Play(hit, -8f + vol);
            Sfx.Note(c.Step * 2, -10f + vol);
            if (ComplexMode && visible) Mineros.Fx.Haptics.Light();
            CartView cv;
            if (cxCarts.TryGetValue(c.Id, out cv) && m.Kind == ModKind.Smelter && !cv.Bar && (c.Kind == Island.OreIron || c.Kind == Island.OreGold || c.Kind == Island.OreCopper))
            {
                cv.Bar = true;
                Material[] lm;
                cv.Load.GetComponent<MeshFilter>().sharedMesh = IslandArt.LoadMesh(c.Kind, true, out lm);
            }
            if (c.Experimental) FxApi.Play("unlock_burst", at, new Color(0.75f, 0.45f, 1f), 1.2f);
        }

        void OnCartSold(Cart c, Module m, double v)
        {
            CartView cv;
            if (cxCarts.TryGetValue(c.Id, out cv)) { if (cv.T != null) Destroy(cv.T.gameObject); cxCarts.Remove(c.Id); }
            Vector3 at = ModWorld(m) + Vector3.up * 1.4f;
            if (ComplexMode) Ui.CoinsFrom(at, v);
            else Ui.Popup(at + Vector3.up * 0.8f, "+" + BigNum.Fmt(v), new Color(1f, 0.9f, 0.35f), 22);
            Sfx.Play("cx_sell", ComplexMode ? -6f : -16f);
            Sfx.Play("coin", ComplexMode ? -8f : -18f, 1.2f);
            if (ComplexMode) Mineros.Fx.Haptics.Medium();
        }

        void UpdateCarts()
        {
            if (cxCarts.Count == 0 && Isl.Carts.Count == 0) return;
            // vagonetas que ya no estan en el nucleo (linea cambiada, guardado): se van
            var gone = new List<int>();
            foreach (var kv in cxCarts) { bool alive = false; foreach (var c in Isl.Carts) if (c.Id == kv.Key) { alive = true; break; } if (!alive) gone.Add(kv.Key); }
            foreach (var id in gone) { if (cxCarts[id].T != null) Destroy(cxCarts[id].T.gameObject); cxCarts.Remove(id); }
            foreach (var c in Isl.Carts)
            {
                CartView cv;
                if (!cxCarts.TryGetValue(c.Id, out cv)) { OnCartSpawned(c); continue; }
                if (cv.T == null) continue;
                PlaceCart(c, cv);
            }
        }

        // ------------------------------------------------------------ cada cuadro
        void UpdateComplex(float dt)
        {
            int lv = Isl.BarracksLevel;
            if (lastBarracksLevel >= 0 && lv > lastBarracksLevel)
            {
                NewCells.Clear();
                for (int f = 0; f <= 2; f++)
                    for (int x = -2; x <= 2; x++)
                        for (int z = -2; z <= 2; z++)
                            if (Island.CellUnlocked(x, z, f, lv) && !Island.CellUnlocked(x, z, f, lastBarracksLevel)) NewCells.Add(new[] { x, z, f });
                NewCellsT = 3.5f;
                cxDirty = true;
            }
            lastBarracksLevel = lv;
            if (NewCellsT > 0f) NewCellsT -= dt;
            var bv = BarracksView;
            if (bv != null && cxRoot != null && cxRoot.parent != bv.Root) cxDirty = true;
            if (cxDirty) RebuildComplex();
            if (bv != null && bv.Model != null && bv.Model.enabled && lv >= 1) bv.Model.enabled = false;   // la Sala central reemplaza al modelo suelto
            if (ComplexMode && (lv < 1 || Ui.BlocksWorld && !Ui.ComplexUiOpen)) { if (lv < 1) ExitComplex(); }
            UpdateCarts();
            UpdateWallsDown(dt);
            UpdateRoofCut(dt);
            UpdateRoomLife(dt);
            UpdateDoors(dt);
            UpdateScaffolds(dt);
            int powered = cxRoot != null ? Isl.Summary.Powered.Count : 0;
            if (powered > lastPowered && lastPowered >= 0) { Sfx.Play("cx_power", -4f); Mineros.Fx.Haptics.Light(); }
            lastPowered = powered;
            // animo: corazones o humo gris sobre la Sala central
            if (cxRoot != null)
            {
                moodFxT -= dt;
                if (moodFxT <= 0f)
                {
                    moodFxT = 3.5f + Random.value * 2f;
                    int mood = Isl.Summary.Mood;
                    var central = Isl.FirstMod(ModKind.Central);
                    if (central != null)
                    {
                        Vector3 top = ModWorld(central) + Vector3.up * 2.6f;
                        if (mood >= 75) Ui.FloatText(top, "♥", new Color(1f, 0.45f, 0.6f), 40);
                        else if (mood < 40) FxApi.Play("dust", top, new Color(0.5f, 0.5f, 0.55f), 0.8f);
                    }
                }
            }
        }

        // ------------------------------------------------------------ Sala secreta
        IEnumerator SecretShow(Module m, SecretOut o, int detail)
        {
            Vector3 at = ModWorld(m);
            FocusOn(at);
            FxApi.Play("unlock_burst", at + Vector3.up * 1f, new Color(0.75f, 0.45f, 1f), 1.8f);
            Sfx.Play("chest", -3f, 0.9f);
            Sfx.Play("cx_magic", -4f);
            Mineros.Fx.Haptics.Heavy();
            yield return new WaitForSeconds(0.45f);
            FxApi.Play("confetti", at + Vector3.up * 2f, default(Color), 1.6f);
            Sfx.Play("tadaa", -4f);
            Mineros.Fx.Haptics.Success();
            Ui.SecretRevealed(m, o, detail);
        }

        /// <summary>Para capturas: lleva el modulo que se coloca a la celda (x, z) del piso visible.</summary>
        public void DebugModAt(int x, int z)
        {
            if (place == null || place.ModK < 0) return;
            ModDrag(CellWorld(x, z, 0));
        }

        /// <summary>Para capturas: vuelve a vestir a todos los mineros (despues de cambiarles el nivel).</summary>
        public void DebugRefreshMiners() { foreach (var mv in miners.Values) RefreshMinerLook(mv); }
    }
}
