using Mineros.Audio;
using Mineros.Core;
using Mineros.World;
using UnityEngine;
using FxApi = Mineros.Fx.Fx;
using Juice = Mineros.Fx.Juice;

namespace Mineros.IslandView
{
    /// <summary>
    /// Sorpresas en el mundo (biblia 3.5): botella que llega con las olas y queda en la arena con un destello; al tocarla
    /// se descorcha, se ve el mapa y aparece una X de tierra que late en la isla; un minero va, cava en tres tandas y sale
    /// un cofre girando. Bichos que duran unos segundos: cangrejo con una gema en las pinzas, gaviota que baja en picada a
    /// robar una moneda, topo que asoma de un pozo y mariposa dorada en zigzag.
    /// </summary>
    public sealed partial class IslandGame
    {
        Transform bottleT, xMark, critterT, critterHole;
        float bottleGlint, digFxT, critterFx;
        Vector3 critterFrom, critterTo;
        static Mesh bottleMesh, crabMesh, moleMesh, butterflyMesh;
        static Material[] bottleMats, crabMats, moleMats, butterflyMats;

        void InitSurprises()
        {
            Isl.BottleArrived += b => { ShowBottle(b); Ui.Toast(Loc.T("La marea trajo una botella…"), new Color(0.35f, 0.6f, 0.75f)); };
            Isl.BottleOpened += OnBottleOpened;
            Isl.TreasureDug += OnTreasure;
            Isl.CritterCame += OnCritter;
            Isl.CritterGone += OnCritterGone;
            if (Isl.CurBottle != null) { ShowBottle(Isl.CurBottle); if (Isl.CurBottle.Opened) MakeXMark(Isl.CurBottle); }
        }

        // ------------------------------------------------------------ botella
        void ShowBottle(Bottle b)
        {
            if (bottleMesh == null)
            {
                var mb = new MeshBuilder();
                Color glass = IslandArt.H("7fd1a8"), cork = IslandArt.H("b5824a"), paper = IslandArt.H("fff1cf");
                mb.Cyl(new Vector3(0, 0, 0), new Vector3(0, 0.42f, 0), 0.15f, 0.15f, 10, glass, 0.15f);
                mb.Cyl(new Vector3(0, 0.42f, 0), new Vector3(0, 0.56f, 0), 0.15f, 0.07f, 10, glass, 0.15f);
                mb.Cyl(new Vector3(0, 0.56f, 0), new Vector3(0, 0.7f, 0), 0.07f, 0.07f, 8, glass, 0.15f);
                mb.Cyl(new Vector3(0, 0.7f, 0), new Vector3(0, 0.8f, 0), 0.075f, 0.07f, 8, cork, 0f);
                mb.Box(new Vector3(0, 0.2f, 0), new Vector3(0.12f, 0.26f, 0.12f), paper, 0.1f);   // el mapa enrollado adentro
                bottleMesh = mb.ToMesh(null, "Botella", out bottleMats);
                bottleMats = VertexColorMerge.Apply(bottleMesh, bottleMats);
            }
            if (bottleT == null) bottleT = IslandArt.MakeRenderer(root, "Botella", bottleMesh, bottleMats).transform;
            bottleT.gameObject.SetActive(true);
            bottleT.position = new Vector3(b.X, 0f, b.Z) * 1.25f;   // empieza en el agua y la ola la acerca
            bottleGlint = 0f;
        }

        void UpdateSurprises(float dt)
        {
            var b = Isl.CurBottle;
            if (bottleT != null && bottleT.gameObject.activeSelf)
            {
                if (b == null || b.Opened) bottleT.gameObject.SetActive(false);
                else
                {
                    Vector3 rest = new Vector3(b.X, 0.02f, b.Z);
                    Vector3 p = Vector3.Lerp(bottleT.position, rest, 1f - Mathf.Exp(-dt * 0.8f));
                    bool inWater = (p - rest).magnitude > 0.3f;
                    p.y = inWater ? -0.3f + Mathf.Sin(Time.time * 2f) * 0.08f : 0.02f;
                    bottleT.position = p;
                    // acostada e inclinada en la arena; en el agua se mece
                    float bob = inWater ? Mathf.Sin(Time.time * 1.7f) * 12f : 0f;
                    bottleT.rotation = Quaternion.Euler(0f, Mathf.Atan2(b.X, b.Z) * Mathf.Rad2Deg, 72f + bob);
                    bottleGlint -= dt;
                    if (bottleGlint <= 0f) { bottleGlint = 3f; FxApi.Play("glint", bottleT.position + Vector3.up * 0.3f, new Color(0.85f, 1f, 0.9f), 1.2f); }
                }
            }
            // X del tesoro que late y el minero que cava
            if (xMark != null)
            {
                if (b == null || !b.Opened) { Destroy(xMark.gameObject); xMark = null; }
                else
                {
                    float pulse = 1f + Mathf.Sin(Time.time * 4f) * 0.08f;
                    xMark.localScale = new Vector3(1.4f * pulse, 1f, 1.4f * pulse);
                    foreach (var mv in miners.Values)
                        if (mv.M.Id == b.Digger && mv.M.State == MState.Digging)
                        {
                            digFxT -= dt;
                            if (digFxT <= 0f)
                            {
                                // tierra que salta en tres tandas
                                digFxT = Island.DigTime / 3f;
                                FxApi.Play("chips", new Vector3(b.TX, 0.2f, b.TZ), IslandGround.Dirt, 1.2f);
                                FxApi.Play("dust", new Vector3(b.TX, 0.1f, b.TZ), new Color(0.75f, 0.6f, 0.42f), 1.2f);
                                Sfx.Play("dig", -10f);
                                Juice.Punch(xMark, 0.2f, 0.2f);
                            }
                        }
                }
            }
            UpdateCritter(dt);
        }

        void OnBottleOpened(Bottle b)
        {
            Sfx.Play("cork", -4f);
            FxApi.Play("sparkle", new Vector3(b.X, 0.4f, b.Z), new Color(0.9f, 1f, 0.9f), 1f);
            MakeXMark(b);
            Ui.ShowTreasureMap(b);
        }

        static Texture2D xTex;

        void MakeXMark(Bottle b)
        {
            if (xMark != null) Destroy(xMark.gameObject);
            if (xTex == null)
            {
                // X pintada con tierra oscura (dos trazos con borde suave)
                const int N = 128;
                xTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "X" };
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float u = (x + 0.5f) / N - 0.5f, v = (y + 0.5f) / N - 0.5f;
                        float d1 = Mathf.Abs(u - v) / 1.414f, d2 = Mathf.Abs(u + v) / 1.414f;
                        float len = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
                        float d = Mathf.Min(d1, d2);
                        float a = Mathf.Clamp01((0.075f - d) * N * 0.5f) * Mathf.Clamp01((0.42f - len) * N * 0.3f);
                        px[y * N + x] = new Color32(120, 52, 30, (byte)(a * 235));
                    }
                xTex.SetPixels32(px);
                xTex.Apply(false, true);
            }
            var go = new GameObject("XTesoro");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(b.TX, 0.04f, b.TZ);
            go.AddComponent<MeshFilter>().sharedMesh = IslandFxKit.Quad();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = new Material(IslandFxKit.Alpha()) { mainTexture = xTex, name = "X" };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var holder = new GameObject("X").transform;
            holder.SetParent(root, false);
            holder.position = go.transform.position;
            go.transform.SetParent(holder, true);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            xMark = holder;
            FxApi.Play("ring", holder.position, new Color(1f, 0.85f, 0.4f), 2f);
        }

        void OnTreasure(Bottle b, int tier)
        {
            Vector3 at = new Vector3(b.TX, 0f, b.TZ);
            StartCoroutine(TreasurePop(at, tier));
            Sfx.Play("thud", -6f, 1.1f);
            Sfx.PlayLater("goal", 0.5f, -4f);
            FocusOn(at, true);
        }

        System.Collections.IEnumerator TreasurePop(Vector3 at, int tier)
        {
            Material tm;
            var mesh = IslandArt.TripoModel("chest", out tm);
            Transform c;
            if (mesh != null) c = IslandArt.MakeRenderer(root, "Tesoro", mesh, new[] { tm }).transform;
            else c = new GameObject("Tesoro").transform;
            FxApi.Play("dust", at, new Color(0.8f, 0.65f, 0.45f), 2.2f);
            FxApi.Play("coin_burst", at + Vector3.up * 0.5f, default(Color), 1.4f);
            for (float t = 0f; t < 1.4f; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / 0.6f);
                c.position = at + Vector3.up * (Mathf.Sin(k * Mathf.PI * 0.5f) * 1.6f);
                c.rotation = Quaternion.Euler(0f, t * 360f, 0f);
                c.localScale = Vector3.one * (0.9f + Mathf.Sin(k * Mathf.PI) * 0.2f);
                yield return null;
            }
            FxApi.Play("sparkle", c.position, new Color(1f, 0.9f, 0.5f), 1.4f);
            Destroy(c.gameObject);
        }

        // ------------------------------------------------------------ bichos
        void OnCritter(Critter c)
        {
            if (critterT != null) Destroy(critterT.gameObject);
            if (critterHole != null) { Destroy(critterHole.gameObject); critterHole = null; }
            critterT = MakeCritter(c.Kind);
            // mas grandes que en la vida real: se tienen que ver y tocar en el telefono
            critterT.localScale = Vector3.one * (c.Kind == CritterKind.Crab ? 1.7f : c.Kind == CritterKind.Mole ? 1.6f : 1f);
            Vector3 p = new Vector3(c.X, 0f, c.Z);
            switch (c.Kind)
            {
                case CritterKind.Crab:
                {
                    // cruza la playa de costado (tangente a la orilla)
                    Vector3 tang = new Vector3(-c.Z, 0f, c.X).normalized;
                    critterFrom = p - tang * 2.5f; critterTo = p + tang * 2.5f;
                    critterT.position = critterFrom;
                    Sfx.Play("crab", -14f);
                    break;
                }
                case CritterKind.Gull:
                    critterTo = p; critterFrom = p + new Vector3(8f, 9f, -6f);
                    critterT.position = critterFrom;
                    Sfx.Play("gull", -8f, 1.2f);
                    break;
                case CritterKind.Mole:
                    critterFrom = critterTo = p;
                    critterHole = IslandArt.Blob(root, 0.45f, 0.6f);
                    critterHole.position = p + Vector3.up * 0.03f;
                    critterT.position = p + Vector3.down * 0.6f;
                    FxApi.Play("chips", p + Vector3.up * 0.2f, IslandGround.Dirt, 0.8f);
                    break;
                case CritterKind.Butterfly:
                    critterFrom = critterTo = p + Vector3.up * 1.4f;
                    critterT.position = critterFrom;
                    Sfx.Play("chimes", -16f, 1.3f);
                    break;
            }
            Ui.MarkCritter(c);
        }

        void UpdateCritter(float dt)
        {
            var c = Isl.CurCritter;
            if (c == null || critterT == null) return;
            float a = c.Age, stay = Island.CritterStay(c.Kind);
            switch (c.Kind)
            {
                case CritterKind.Crab:
                {
                    float k = Mathf.Clamp01(a / stay);
                    Vector3 p = Vector3.Lerp(critterFrom, critterTo, k);
                    p.y = Mathf.Abs(Mathf.Sin(a * 14f)) * 0.04f;
                    critterT.position = p;
                    Vector3 side = critterTo - critterFrom;
                    critterT.rotation = Quaternion.LookRotation(Vector3.Cross(side, Vector3.up).normalized) * Quaternion.Euler(0f, 0f, Mathf.Sin(a * 14f) * 6f);
                    break;
                }
                case CritterKind.Gull:
                {
                    // baja en picada, agarra la moneda y se va despacio (dando tiempo a tocarla)
                    float k = Mathf.Clamp01(a / 1.2f);
                    Vector3 p = a < 1.2f ? Vector3.Lerp(critterFrom, critterTo + Vector3.up * 0.5f, k * k) : critterTo + Vector3.up * 0.5f + new Vector3(-1f, 0.45f, 1f) * (a - 1.2f) * 1.1f;
                    Vector3 v = p - critterT.position;
                    critterT.position = p;
                    if (v.sqrMagnitude > 1e-5f) critterT.rotation = Quaternion.LookRotation(v.normalized);
                    float flap = Mathf.Sin(Time.time * 12f);
                    critterT.localScale = new Vector3(1.2f, 1.2f * (1f + flap * 0.7f), 1.2f);
                    if (a > 1.2f && a - dt <= 1.2f) { FxApi.Play("sparkle", critterTo + Vector3.up * 0.4f, new Color(1f, 0.85f, 0.3f), 0.8f); Sfx.Play("coin", -10f, 1.3f); }
                    break;
                }
                case CritterKind.Mole:
                {
                    // asoma, mira a los lados, se esconde y asoma otra vez
                    float cyc = Mathf.Repeat(a, 2.6f);
                    float up = cyc < 0.25f ? cyc / 0.25f : cyc < 1.8f ? 1f : cyc < 2.05f ? 1f - (cyc - 1.8f) / 0.25f : 0f;
                    critterT.position = critterTo + Vector3.up * Mathf.Lerp(-0.6f, 0f, up);
                    critterT.rotation = Quaternion.Euler(0f, Mathf.Sin(a * 3f) * 50f, 0f);
                    if (cyc < 0.25f && Mathf.Repeat(a - dt, 2.6f) > cyc) FxApi.Play("chips", critterTo + Vector3.up * 0.2f, IslandGround.Dirt, 0.6f);
                    break;
                }
                case CritterKind.Butterfly:
                {
                    Vector3 p = critterFrom + new Vector3(Mathf.Sin(a * 1.3f) * 2.2f, Mathf.Sin(a * 3.1f) * 0.4f, Mathf.Sin(a * 0.9f + 1f) * 1.6f);
                    Vector3 v = p - critterT.position;
                    critterT.position = p;
                    if (v.sqrMagnitude > 1e-5f) critterT.rotation = Quaternion.LookRotation(new Vector3(v.x, 0f, v.z).normalized + Vector3.forward * 1e-3f);
                    float flap = Mathf.Abs(Mathf.Sin(Time.time * 16f));
                    critterT.localScale = new Vector3(0.3f + flap * 1.1f, 1f, 1f) * 2.6f;
                    critterFx -= dt;
                    if (critterFx <= 0f) { critterFx = 0.15f; FxApi.Play("glint", p, new Color(1f, 0.85f, 0.3f), 0.6f); }
                    break;
                }
            }
        }

        public bool CritterVisible { get { return critterT != null && (Isl.CurCritter == null || Isl.CurCritter.Kind != CritterKind.Mole || critterT.position.y > -0.35f); } }
        public Vector3 CritterPos { get { return critterT != null ? critterT.position : Vector3.zero; } }

        Vector3 CritterTapPoint() { return critterT != null ? critterT.position + Vector3.up * 0.3f : Vector3.zero; }

        /// <summary>Toque sobre el bicho (antes que vetas y edificios). true si lo atrapo.</summary>
        bool TapCritter(Vector2 screen)
        {
            var c = Isl.CurCritter;
            if (c == null || critterT == null) return false;
            if (c.Kind == CritterKind.Mole && critterT.position.y < -0.35f) return false;   // escondido
            Vector2 sp = Cam.WorldToScreenPoint(CritterTapPoint());
            float px = Cam.pixelHeight / 1544f;
            if (Vector2.Distance(screen, sp) > 95f * px) return false;
            Vector3 at = CritterTapPoint();
            double coins = Isl.CatchCritter();
            Juice.Vibrate(30);
            switch (c.Kind)
            {
                case CritterKind.Crab: Ui.Popup(at + Vector3.up, Loc.T("¡Una gema!"), new Color(0.85f, 0.6f, 1f), 30); Ui.FlyGemsFromWorld(at, 1); Sfx.Play("gem", -4f); break;
                case CritterKind.Gull: Ui.CoinsFrom(at, coins); Sfx.Play("wings", -10f, 1.3f); break;
                case CritterKind.Mole: Ui.CoinsFrom(at, coins); Sfx.Play("pop", -4f); break;
                case CritterKind.Butterfly: Ui.Popup(at + Vector3.up, Loc.T("¡Turbo 60 s!"), new Color(1f, 0.85f, 0.3f), 32); Sfx.Play("powerup", -4f); break;
            }
            return true;
        }

        void OnCritterGone(Critter c, bool caught)
        {
            if (critterT == null) return;
            var t = critterT; critterT = null;
            Vector3 p = t.position;
            if (caught)
            {
                FxApi.Play("unlock_burst", p + Vector3.up * 0.3f, new Color(1f, 0.9f, 0.5f), 1f);
                if (c.Kind == CritterKind.Crab) FxApi.Play("dust", p, IslandArt.Sand, 1f);   // se entierra en la arena
            }
            else
            {
                if (c.Kind == CritterKind.Crab) { FxApi.Play("dust", p, IslandArt.Sand, 1f); Sfx.Play("dig", -16f, 1.4f); }
                if (c.Kind == CritterKind.Mole) FxApi.Play("chips", p + Vector3.up * 0.2f, IslandGround.Dirt, 0.7f);
            }
            Destroy(t.gameObject);
            if (critterHole != null) { var h = critterHole; critterHole = null; Destroy(h.gameObject, 2f); }
        }

        Transform MakeCritter(CritterKind k)
        {
            switch (k)
            {
                case CritterKind.Crab:
                    if (crabMesh == null)
                    {
                        var mb = new MeshBuilder();
                        Color red = IslandArt.H("ff6a4d"), redD = IslandArt.H("d64a32");
                        mb.Blob(new Vector3(0, 0.14f, 0), new Vector3(0.22f, 0.12f, 0.17f), 1, 3, 0.04f, redD, red, 0f, -1f);
                        for (int s = -1; s <= 1; s += 2)
                        {
                            mb.Octa(new Vector3(s * 0.26f, 0.2f, 0.14f), new Vector3(0.11f, 0.1f, 0.12f), red, 0f);       // pinzas
                            for (int l = 0; l < 3; l++) mb.Box(new Vector3(s * 0.24f, 0.06f, -0.08f + l * 0.08f), new Vector3(0.14f, 0.03f, 0.03f), redD, 0f);
                            mb.Box(new Vector3(s * 0.07f, 0.3f, 0.1f), new Vector3(0.03f, 0.1f, 0.03f), redD, 0f);
                            mb.Octa(new Vector3(s * 0.07f, 0.37f, 0.1f), Vector3.one * 0.05f, Color.white, 0.2f);
                        }
                        mb.Crystal(new Vector3(0.26f, 0.24f, 0.24f), Vector3.up, 0.16f, 0.06f, 5, IslandArt.H("b06ef0"), 0.6f);   // la gema
                        crabMesh = mb.ToMesh(null, "Cangrejo", out crabMats);
                        crabMats = VertexColorMerge.Apply(crabMesh, crabMats);
                    }
                    return IslandArt.MakeRenderer(root, "Cangrejo", crabMesh, crabMats).transform;
                case CritterKind.Mole:
                    if (moleMesh == null)
                    {
                        var mb = new MeshBuilder();
                        Color fur = IslandArt.H("7a5a46"), furL = IslandArt.H("9b7a62");
                        mb.Blob(new Vector3(0, 0.3f, 0), new Vector3(0.2f, 0.3f, 0.18f), 1, 4, 0.03f, fur, furL, 0f, -1f);
                        mb.Octa(new Vector3(0, 0.42f, 0.17f), Vector3.one * 0.07f, IslandArt.H("ff9aa8"), 0.1f);           // nariz
                        mb.Octa(new Vector3(-0.07f, 0.5f, 0.15f), Vector3.one * 0.035f, IslandArt.H("2b2440"), 0f);
                        mb.Octa(new Vector3(0.07f, 0.5f, 0.15f), Vector3.one * 0.035f, IslandArt.H("2b2440"), 0f);
                        mb.Cyl(new Vector3(0, 0.58f, 0), new Vector3(0, 0.66f, 0), 0.19f, 0.17f, 10, IslandArt.H("f5a823"), 0.05f);   // casquito
                        moleMesh = mb.ToMesh(null, "Topo", out moleMats);
                        moleMats = VertexColorMerge.Apply(moleMesh, moleMats);
                    }
                    return IslandArt.MakeRenderer(root, "Topo", moleMesh, moleMats).transform;
                case CritterKind.Butterfly:
                    if (butterflyMesh == null)
                    {
                        var mb = new MeshBuilder();
                        Color g = IslandArt.H("ffd23a");
                        for (int s = -1; s <= 1; s += 2)
                        {
                            Vector3 a = Vector3.zero, b = new Vector3(s * 0.24f, 0f, 0.13f), d = new Vector3(s * 0.2f, 0f, -0.15f);
                            mb.Tri(a, b, d, Vector3.up, g, 0.6f);
                            mb.Tri(a, d, b, Vector3.down, g, 0.6f);
                        }
                        butterflyMesh = mb.ToMesh(null, "MariposaDorada", out butterflyMats);
                        butterflyMats = VertexColorMerge.Apply(butterflyMesh, butterflyMats);
                    }
                    return IslandArt.MakeRenderer(root, "MariposaDorada", butterflyMesh, butterflyMats, false).transform;
                default:
                    Material[] gm = null;
                    var gull = Ambient != null ? Ambient.GullMesh(out gm) : null;
                    if (gull != null) return IslandArt.MakeRenderer(root, "GaviotaLadrona", gull, gm).transform;
                    return new GameObject("Gaviota").transform;
            }
        }
    }
}
