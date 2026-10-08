using System.Collections.Generic;
using System.Collections;
using Mineros.Audio;
using Mineros.Core;
using UnityEngine;
using FxApi = Mineros.Fx.Fx;
using Juice = Mineros.Fx.Juice;

namespace Mineros.IslandView
{
    /// <summary>
    /// Los "shows" de la isla: construir (polvo, andamio, martillazos que suben de tono, el edificio sale de la tierra con
    /// estirón y rebote, onda en el pasto, confeti y cartel), mejorar (martillazos y salto) y evolucionar (tiembla y
    /// brilla cada vez más, destello, nuevo modelo con rayos de luz y cartel antes → después). Tambien la veta de oro
    /// que cae del cielo y el paneo suave de la camara hacia lo que pasa (sin zoom en lo frecuente).
    /// </summary>
    public sealed partial class IslandGame
    {
        // ------------------------------------------------------------ camara hacia un punto
        bool focusing;
        Vector3 focusTo;

        /// <summary>Ultima vez que el jugador movio la camara (arrastre o pellizco).</summary>
        public float LastPanT = -99f;

        /// <summary>
        /// Camara como en los juegos de gestion buenos (pedido del dueño: "los movimientos automaticos rapidos molestan"):
        /// la camara es del jugador. Nada la mueve si lo que importa ya se ve. Si quedo fuera de la zona util (o debajo
        /// de una hoja abierta), se corre lo MINIMO para que entre, despacio y con freno suave; si el jugador toca la
        /// pantalla, se corta. `auto` = lo pide un evento (veta gigante, tesoro, golem): esos nunca mueven la camara,
        /// avisan con un cartel en el borde que se toca para ir. `screenY` > 0.6 = hay un cajon abajo: la zona util
        /// empieza mas arriba.
        /// </summary>
        public void FocusOn(Vector3 world, bool auto = false, float screenY = 0.45f)
        {
            if (auto) return;
            Reveal(world, screenY > 0.6f ? screenY - 0.2f : 0.2f);
        }

        /// <summary>Lo trae a la vista si no se ve (tutorial y botones del jugador). `minY` = borde inferior util (0-1).</summary>
        public void Reveal(Vector3 world, float minY = 0.2f)
        {
            Vector3 c = new Vector3(world.x, 0f, world.z);
            Vector3 vp = Cam.WorldToViewportPoint(c + Vector3.up * 1.2f);
            float x0 = 0.14f, x1 = 0.86f, y0 = minY, y1 = 0.82f;
            if (vp.z > 0f && vp.x >= x0 && vp.x <= x1 && vp.y >= y0 && vp.y <= y1) return;   // ya se ve: quieto
            // el punto mas cercano de la zona util (un poco hacia adentro, para que no quede pegado al borde)
            float tx = Mathf.Clamp(vp.x, x0 + 0.08f, x1 - 0.08f), ty = Mathf.Clamp(vp.y, y0 + 0.08f, y1 - 0.08f);
            Vector3 from = ScreenToGround(new Vector2(vp.x * Cam.pixelWidth, vp.y * Cam.pixelHeight));
            Vector3 to = ScreenToGround(new Vector2(tx * Cam.pixelWidth, ty * Cam.pixelHeight));
            Vector3 d = from - to; d.y = 0f;
            focusTo = camRig.position + d;
            focusing = true;
            focusV = Vector3.zero;
            vel = Vector3.zero;
        }

        Vector3 focusV;

        bool UpdateFocus(float dt)
        {
            if (!focusing) return false;
            // freno suave (amortiguado critico, ~0.55 s): arranca y llega sin tirones
            camRig.position = Vector3.SmoothDamp(camRig.position, focusTo, ref focusV, 0.32f, 40f, dt);
            if ((camRig.position - focusTo).sqrMagnitude < 0.0025f) focusing = false;
            return true;
        }

        // ------------------------------------------------------------ construir
        IEnumerator BuildShow(PlotView v)
        {
            v.Driven = true;
            RefreshPlot(v);
            var body = v.Body;
            if (body == null) { v.Driven = false; yield break; }
            body.localScale = Vector3.zero;
            Vector3 c = v.Root.position;
            FocusOn(c);
            // golpe sordo, polvo y andamio que cae rebotando
            Sfx.Play("thud", -2f, 0.8f);
            Sfx.Play("rumble", -8f);
            FxApi.Play("dust", c + Vector3.up * 0.2f, new Color(0.85f, 0.75f, 0.6f), 2.6f);
            Juice.Vibrate(20);
            Juice.Punch(v.Scaffold, 0.35f, 0.35f);
            // martillazos: chispas en el andamio, cada uno un poco mas agudo
            for (int i = 0; i < 6; i++)
            {
                yield return new WaitForSeconds(0.17f);
                Vector3 at = c + new Vector3(Random.Range(-1.1f, 1.1f), Random.Range(0.6f, 2.2f), Random.Range(-0.9f, 0.9f));
                FxApi.Play("hit_spark", at, default(Color), 1f);
                Sfx.Play("build", -4f, 0.92f + i * 0.07f);
                if (i == 3) Sfx.Play("anvil", -12f, 1.1f);
                Juice.Punch(v.Scaffold, 0.06f, 0.12f);
            }
            while (v.P.BuildT >= 0f) yield return null;
            // nube de polvo y el edificio sale de la tierra
            FxApi.Play("dust", c + Vector3.up * 0.4f, new Color(0.95f, 0.9f, 0.8f), 3.4f);
            FxApi.Play("rock_break", c + Vector3.up * 0.5f, new Color(0.8f, 0.7f, 0.55f), 1.4f);
            Sfx.Play("rumble", -6f, 1.1f);
            Sfx.Play("whoosh", -8f, 0.8f);
            yield return Pop(v, 0.7f, -0.6f);
            Sfx.Play("thud", -5f, 1.1f);
            // el caminito de tierra aparece desde la red hasta la puerta
            yield return GrowPath(v.P.Id);
            BuildLamps();   // el camino nuevo trae sus faroles
            PathFlowers(v.P.Id);
            v.SmokeT = 0.2f;   // primer humo: el edificio cobra vida
            FxApi.Play("ring", c + Vector3.up * 0.1f, new Color(1f, 0.95f, 0.6f), 3.2f);
            FxApi.Play("levelup_aura", c, new Color(1f, 0.9f, 0.4f), 1.8f);
            FxApi.Play("confetti", c + Vector3.up * (v.Height + 1.2f), default(Color), 1.8f);
            Sfx.Play("milestone", -3f);
            Sfx.Play("pop", -6f);
            Sfx.PlayLater("cheer", 0.25f, -8f);
            Mineros.Fx.Haptics.Success();
            Cheer(c, 7f);
            var d = Island.Def((BKind)v.P.Building);
            Ui.Banner(Loc.T("¡") + d.Name + Loc.T(" construida!"), d.Desc, Kit3.Yellow, IslandStage.I.BuildingIcon((BKind)v.P.Building, 1), null);
            v.Driven = false;
        }

        /// <summary>Aparicion con estirón: crece desde abajo, se estira, se aplasta y se asienta.</summary>
        IEnumerator Pop(PlotView v, float dur, float fromY)
        {
            var body = v.Body;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                if (body == null) yield break;
                float u = t / dur;
                float s = OutBack(Mathf.Min(1f, u * 1.3f));
                float sq = 1f + Mathf.Sin(u * Mathf.PI * 2.5f) * 0.22f * (1f - u);
                body.localScale = new Vector3(s / Mathf.Sqrt(sq), s * sq, s / Mathf.Sqrt(sq));
                body.localPosition = new Vector3(0f, Mathf.Lerp(fromY, 0f, Mathf.Min(1f, u * 2f)), 0f);
                yield return null;
            }
            if (body != null) { body.localScale = Vector3.one; body.localPosition = Vector3.zero; }
        }

        /// <summary>Parcela nueva: una nube de niebla se disuelve y las piedritas del lote caen de a una.</summary>
        IEnumerator FogReveal(PlotView v)
        {
            Vector3 c = v.Root.position;
            var fog = new List<Transform>();
            for (int i = 0; i < 7; i++)
            {
                var h = IslandFxKit.Halo(root, "Niebla", 3.2f);
                h.sharedMaterial = IslandFxKit.Alpha();
                h.transform.position = c + new Vector3(Random.Range(-1.2f, 1.2f), 0.6f + Random.value * 0.6f, Random.Range(-1.2f, 1.2f));
                fog.Add(h.transform);
            }
            var pad = v.Pad;
            if (pad != null) pad.localScale = Vector3.zero;
            yield return new WaitForSeconds(0.25f);
            for (float t = 0f; t < 1.4f; t += Time.deltaTime)
            {
                float u = t / 1.4f;
                for (int i = 0; i < fog.Count; i++)
                {
                    var f = fog[i];
                    IslandFxKit.Face(f, Cam);
                    f.localScale = Vector3.one * (3.2f + u * 2.5f);
                    IslandFxKit.Tint(f.GetComponent<MeshRenderer>(), new Color(1f, 1f, 1f, (1f - u) * 0.85f));
                    f.position += (f.position - c).normalized * Time.deltaTime * 0.8f;
                }
                if (pad != null) pad.localScale = Vector3.one * OutBack(Mathf.Clamp01((u - 0.3f) / 0.5f));
                yield return null;
            }
            foreach (var f in fog) Destroy(f.gameObject);
            if (pad != null) pad.localScale = Vector3.one;
            FxApi.Play("ring", c + Vector3.up * 0.05f, new Color(1f, 0.95f, 0.7f), 2.4f);
            FxApi.Play("motes", c + Vector3.up * 0.4f, new Color(0.75f, 1f, 0.55f), 1.4f);   // vida que sale del pasto nuevo (4)
            Ui.Popup(c + Vector3.up * 1.5f, Loc.T("Nuevo terreno"), new Color(0.7f, 1f, 0.55f), 28);
        }

        /// <summary>Niebla que se disuelve en un punto (algo nuevo aparece en la isla).</summary>
        IEnumerator FogRevealAt(Vector3 c)
        {
            var fog = new List<Transform>();
            for (int i = 0; i < 9; i++)
            {
                var h = IslandFxKit.Halo(root, "Niebla", 3.6f);
                h.sharedMaterial = IslandFxKit.Alpha();
                h.transform.position = c + new Vector3(Random.Range(-1.6f, 1.6f), 0.6f + Random.value * 0.9f, Random.Range(-1.6f, 1.6f));
                fog.Add(h.transform);
            }
            for (float t = 0f; t < 1.6f; t += Time.deltaTime)
            {
                float u = t / 1.6f;
                foreach (var f in fog)
                {
                    IslandFxKit.Face(f, Cam);
                    f.localScale = Vector3.one * (3.6f + u * 3f);
                    IslandFxKit.Tint(f.GetComponent<MeshRenderer>(), new Color(1f, 1f, 1f, (1f - u) * 0.9f));
                    f.position += (f.position - c).normalized * Time.deltaTime;
                }
                yield return null;
            }
            foreach (var f in fog) Destroy(f.gameObject);
            FxApi.Play("ring", c + Vector3.up * 0.05f, new Color(1f, 0.95f, 0.7f), 3f);
            Sfx.Play("magic_rise", -8f);
        }

        /// <summary>Antes del destello las particulas son aspiradas hacia el edificio (anticipacion).</summary>
        IEnumerator Suction(Vector3 center, float dur)
        {
            var dots = new List<Transform>();
            var from = new List<Vector3>();
            for (int i = 0; i < 16; i++)
            {
                var h = IslandFxKit.Halo(root, "Chispa", 0.35f);
                float a = Random.value * Mathf.PI * 2f, r = Random.Range(2.5f, 4f);
                Vector3 p = center + new Vector3(Mathf.Cos(a) * r, Random.Range(-1f, 1.5f), Mathf.Sin(a) * r);
                h.transform.position = p;
                dots.Add(h.transform); from.Add(p);
            }
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float u = t / dur;
                for (int i = 0; i < dots.Count; i++)
                {
                    float k = Mathf.Clamp01(u * 1.4f - (i % 4) * 0.08f);
                    dots[i].position = Vector3.Lerp(from[i], center, k * k);
                    IslandFxKit.Face(dots[i], Cam);
                    IslandFxKit.Tint(dots[i].GetComponent<MeshRenderer>(), new Color(1f, 0.9f, 0.55f, Mathf.Sin(k * Mathf.PI) * 0.9f));
                }
                yield return null;
            }
            foreach (var d in dots) Destroy(d.gameObject);
        }

        /// <summary>Etapa maxima: lluvia de destellos dorados durante 2 s.</summary>
        IEnumerator GoldRain(Vector3 c, float h)
        {
            for (float t = 0f; t < 2f; t += 0.08f)
            {
                FxApi.Play("glint", c + new Vector3(Random.Range(-1.6f, 1.6f), h + Random.Range(0.5f, 2.5f), Random.Range(-1.6f, 1.6f)), new Color(1f, 0.85f, 0.3f), 1f);
                yield return new WaitForSeconds(0.08f);
            }
        }

        /// <summary>
        /// El caminito se conecta solo (0.9.2): crece de forma continua desde la red hasta la puerta en ~1.1 s (arranca
        /// y frena suave), con una nubecita de tierra y piedritas en la punta y un rasguido bajito que acompaña.
        /// La textura del suelo se actualiza cada dos cuadros (sube entera a la GPU: a 30 Hz se ve fluido y no traba).
        /// </summary>
        IEnumerator GrowPath(int plotId)
        {
            PathLine line = null;
            foreach (var l in Isl.Paths) if (l.Plot == plotId) { line = l; break; }
            if (line == null || line.X.Length < 2) { Ground.RevealPath(plotId, 1f); yield break; }
            float total = 0f;
            for (int i = 0; i < line.X.Length - 1; i++) total += Vector2.Distance(new Vector2(line.X[i], line.Z[i]), new Vector2(line.X[i + 1], line.Z[i + 1]));
            float dur = Mathf.Clamp(0.5f + total * 0.07f, 0.7f, 1.4f);
            float t = 0f, puffT = 0f;
            int frame = 0;
            Sfx.Play("whoosh", -14f, 0.6f);
            while (t < dur)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dur);
                float e = u * u * (3f - 2f * u);   // arranca y frena suave
                if ((frame++ & 1) == 0 || u >= 1f) Ground.RevealPath(plotId, e);
                puffT -= Time.deltaTime;
                if (puffT <= 0f && u < 0.97f)
                {
                    puffT = 0.09f;
                    Vector3 tip = PathAt(line, total * e);
                    FxApi.Play("dust", tip + Vector3.up * 0.15f, IslandGround.Dirt, 0.7f);
                }
                yield return null;
            }
            Ground.RevealPath(plotId, 1f);
            FxApi.Play("dust", PathAt(line, total) + Vector3.up * 0.2f, IslandGround.Dirt, 1.3f);
        }

        static Vector3 PathAt(PathLine l, float dist)
        {
            for (int i = 0; i < l.X.Length - 1; i++)
            {
                Vector2 a = new Vector2(l.X[i], l.Z[i]), b = new Vector2(l.X[i + 1], l.Z[i + 1]);
                float seg = Vector2.Distance(a, b);
                if (dist <= seg || i == l.X.Length - 2) { Vector2 p = Vector2.Lerp(a, b, seg > 0f ? Mathf.Clamp01(dist / seg) : 1f); return new Vector3(p.x, 0f, p.y); }
                dist -= seg;
            }
            return new Vector3(l.X[l.X.Length - 1], 0f, l.Z[l.Z.Length - 1]);
        }

        /// <summary>Al construir, brotan flores a los costados del camino nuevo (de a una).</summary>
        void PathFlowers(int plot)
        {
            foreach (var path in Isl.Paths)
            {
                if (path.Plot != plot) continue;
                int n = 0;
                for (int i = 2; i < path.X.Length - 1; i += 3)
                {
                    float dx = path.X[i + 1] - path.X[i - 1], dz = path.Z[i + 1] - path.Z[i - 1];
                    float len = Mathf.Sqrt(dx * dx + dz * dz);
                    if (len < 1e-4f) continue;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 p = new Vector3(path.X[i] - dz / len * s * (Island.PathHalf + 0.35f), 0f, path.Z[i] + dx / len * s * (Island.PathHalf + 0.35f));
                        FlowerAt(p, 0.15f * n++, 150f);
                    }
                }
            }
        }

        static float OutBack(float t) { const float c1 = 1.70158f, c3 = c1 + 1f; return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f); }

        void Cheer(Vector3 c, float radius)
        {
            foreach (var mv in miners.Values)
                if ((mv.Model.transform.position - c).sqrMagnitude < radius * radius) mv.Celebrate = 1f;
        }

        // ------------------------------------------------------------ mejorar (mismo aspecto)
        IEnumerator UpgradeShow(PlotView v)
        {
            v.Driven = true;
            Vector3 c = v.Root.position;
            for (int i = 0; i < 3; i++)
            {
                FxApi.Play("hit_spark", c + new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(0.8f, v.Height), -0.6f), default(Color), 0.9f);
                Sfx.Play("build", -5f, 1.05f + i * 0.1f);
                if (v.Body != null) Juice.Punch(v.Body, 0.05f, 0.1f);
                yield return new WaitForSeconds(0.12f);
            }
            RefreshPlot(v);
            if (v.Body != null)
            {
                var body = v.Body;
                for (float t = 0f; t < 0.4f; t += Time.deltaTime)
                {
                    if (body == null) break;
                    float u = t / 0.4f;
                    float sq = 1f + Mathf.Sin(u * Mathf.PI * 2f) * 0.16f * (1f - u);
                    body.localScale = new Vector3(1f / Mathf.Sqrt(sq), sq, 1f / Mathf.Sqrt(sq)) * (1f + Mathf.Sin(u * Mathf.PI) * 0.1f);
                    yield return null;
                }
                if (body != null) body.localScale = Vector3.one;
            }
            FxApi.Play("levelup_aura", c, new Color(1f, 0.9f, 0.4f), 1.6f);
            Ui.Popup(c + Vector3.up * (v.Height + 1f), Loc.T("Nivel ") + v.P.Level, new Color(0.6f, 1f, 0.5f), 32);
            Sfx.Play("upgrade", -2f);
            Juice.Vibrate(20);
            v.Driven = false;
        }

        // ------------------------------------------------------------ evolucionar (nueva etapa = nuevo modelo)
        IEnumerator EvolveShow(PlotView v)
        {
            v.Driven = true;
            Vector3 c = v.Root.position;
            var k = (BKind)v.P.Building;
            int oldTier = v.ShownTier, newTier = Island.Tier(v.P.Level);
            FocusOn(c);
            Sfx.Play("magic_rise", -4f);
            Sfx.Duck(8f, 4.5f);
            StartCoroutine(Suction(c + Vector3.up * v.Height * 0.5f, 1.5f));
            // tiembla y brilla cada vez mas
            const float charge = 1.5f;
            for (float t = 0f; t < charge; t += Time.deltaTime)
            {
                float u = t / charge;
                if (v.Body != null)
                {
                    float a = 0.02f + u * u * 0.09f;
                    v.Body.localPosition = new Vector3(Mathf.Sin(Time.time * 55f) * a, 0f, Mathf.Cos(Time.time * 47f) * a);
                    v.Body.localScale = Vector3.one * (1f + u * 0.08f);
                }
                SetGlow(v, u * u * 1.3f);
                if (Random.value < Time.deltaTime * (6f + u * 20f))
                    FxApi.Play("glint", c + new Vector3(Random.Range(-1.4f, 1.4f), Random.Range(0.3f, v.Height + 0.6f), Random.Range(-1.2f, 1.2f)), new Color(1f, 0.9f, 0.5f), 1.1f);
                yield return null;
            }
            // destello y cambio de modelo
            Ui.Flash(new Color(1f, 0.98f, 0.9f), 0.45f);
            Sfx.Play("rebirth", -2f);
            Sfx.Play("thud", -4f, 0.7f);
            Sfx.PlayLater("firework", 0.35f, -8f);
            Sfx.PlayLater("goal", 0.9f, -4f);
            Sfx.PlayLater("cheer", 0.5f, -7f);
            Juice.Vibrate(80);
            Juice.ZoomPunch(0.04f, 0.3f);   // momento raro y grande: un zoom corto esta permitido
            v.Glow = 1.4f;
            RefreshPlot(v);
            var rays = IslandStage.MakeRays(v.Root, Vector3.up * (v.Height * 0.55f), v.Height * 2.6f, new Color(1f, 0.88f, 0.5f, 0.75f));
            FxApi.Play("unlock_burst", c + Vector3.up * v.Height * 0.5f, default(Color), 2.6f);
            FxApi.Play("ring", c + Vector3.up * 0.1f, new Color(1f, 0.9f, 0.5f), 4f);
            FxApi.Play("confetti", c + Vector3.up * (v.Height + 1.5f), default(Color), 2.4f);
            Cheer(c, 9f);
            // onda de color: el pasto alrededor florece
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f, dd = 2.2f + (i % 2) * 0.9f;
                Vector3 fp = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * dd;
                if (Isl.FreeSpot(fp.x, fp.z, 0.25f)) FlowerAt(fp, 0.1f + dd * 0.15f, 120f);
            }
            if (newTier >= 4) StartCoroutine(GoldRain(c, v.Height));
            var d = Island.Def(k);
            Ui.Banner(Loc.T("¡EVOLUCIÓN!"), d.Name + Loc.T(": etapa ") + newTier, Kit3.Yellow,
                IslandStage.I.BuildingIcon(k, oldTier), IslandStage.I.BuildingIcon(k, newTier));
            // aparece mas grande, se asienta, el brillo se apaga y los rayos giran un rato
            var body = v.Body;
            for (float t = 0f; t < 2.6f; t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / 0.6f);
                if (body != null)
                {
                    float s = Mathf.Lerp(1.3f, 1f, OutBack(u));
                    body.localScale = Vector3.one * s;
                    body.localPosition = Vector3.zero;
                }
                SetGlow(v, Mathf.Lerp(1.4f, 0f, Mathf.Clamp01(t / 1.2f)));
                if (rays != null)
                {
                    rays.rotation = Quaternion.LookRotation(Cam.transform.forward, Cam.transform.up) * Quaternion.Euler(0f, 0f, t * 40f);
                    rays.localScale = Vector3.one * v.Height * 2.6f * (1f - Mathf.Clamp01((t - 1.8f) / 0.8f));
                }
                yield return null;
            }
            if (rays != null) Destroy(rays.gameObject);
            if (body != null) body.localScale = Vector3.one;
            SetGlow(v, 0f);
            v.Driven = false;
        }

        // ------------------------------------------------------------ veta de oro que cae del cielo
        void SkyFall(OreView v, float size, ref float y, ref float sq)
        {
            const float fall = 1.35f;
            float a = Mathf.Clamp01(v.O.Age / fall);
            y = Mathf.Lerp(24f, 0f, a * a);
            sq = 1f;
            if (v.O.Age < fall)
            {
                if (Random.value < Time.deltaTime * 30f) FxApi.Play("glint", v.T.position + Vector3.up * size, new Color(1f, 0.85f, 0.3f), 1.4f);
                if (!v.Whistled) { v.Whistled = true; Sfx.Play("meteor", v.O.Giant ? -4f : -10f, v.O.Giant ? 1.3f : 1.45f); }
                return;
            }
            float b = v.O.Age - fall;
            sq = 1f - Mathf.Sin(Mathf.Min(b, 0.5f) / 0.5f * Mathf.PI) * 0.25f * Mathf.Exp(-b * 3f);
            if (v.Landed) return;
            v.Landed = true;
            v.GlintT = 0.5f;
            Vector3 at = new Vector3(v.O.X, 0.1f, v.O.Z);
            float big = v.O.Giant ? 1f : 0.45f;   // los meteoritos chicos de la lluvia pegan menos
            FxApi.Play("meteor_impact", at, new Color(1f, 0.85f, 0.3f), 2.4f * big);
            Sfx.Play("thud", v.O.Giant ? 0f : -6f, 0.6f);
            Sfx.Play("break", -4f - (1f - big) * 6f, 0.7f);
            if (v.O.Giant) Sfx.Play("rumble", -3f, 0.9f);
            FxApi.Play("dust", at, new Color(0.9f, 0.8f, 0.6f), 3.6f * big);
            FxApi.Play("ring", at, new Color(1f, 0.9f, 0.5f), 4.5f * big);
            Juice.Vibrate(v.O.Giant ? 90 : 25);
            if (v.O.Giant)
            {
                foreach (var p in plots) if (p.Body != null && (p.Root.position - at).sqrMagnitude < 36f) Juice.Punch(p.Body, 0.08f, 0.25f);
            }
            else DirtMark(new Vector3(v.O.X, 0f, v.O.Z), 0.9f);
        }
    }
}
