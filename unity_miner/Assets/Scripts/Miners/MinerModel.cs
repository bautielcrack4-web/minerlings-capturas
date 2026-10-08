using System.Collections.Generic;
using Mineros.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mineros.Miners
{
    /// <summary>
    /// Minero 3D chibi de diseño propio (cabeza grande, chaleco reflectivo, casco con lampara), generado por codigo.
    /// CONTRATO con el mundo (no cambiar firmas). La IA y el movimiento los maneja el mundo; esto es solo visual.
    /// Unidades: ~1 unidad = 1 metro; el minero mide ~1.2 de alto; los pies estan en el origen local.
    /// Port de miner_view.gd: mismas proporciones (el modelo se construye en las unidades de Godot y se escala por ModelScale).
    /// </summary>
    public sealed class MinerModel : MonoBehaviour
    {
        const float ModelScale = 0.7f;

        static readonly Color[] Skins =
        {
            new Color32(0xf7, 0xcf, 0xae, 255), new Color32(0xe2, 0xa7, 0x7c, 255),
            new Color32(0xa8, 0x70, 0x4a, 255), new Color32(0xf2, 0xc1, 0x9a, 255),
        };
        static readonly Color[] Hairs =
        {
            new Color32(0x5a, 0x3a, 0x22, 255), new Color32(0x2a, 0x1d, 0x16, 255),
            new Color32(0x1f, 0x17, 0x12, 255), new Color32(0xc9, 0x81, 0x3a, 255),
        };
        // Colores por rango D..SS (art.gd RANK_COLS).
        static readonly Color[] RankCols =
        {
            new Color32(0xa9, 0xb3, 0xbd, 255), new Color32(0x6f, 0xd3, 0x6a, 255), new Color32(0x4a, 0xa3, 0xf0, 255),
            new Color32(0xb0, 0x6e, 0xf0, 255), new Color32(0xff, 0xcf, 0x3a, 255), new Color32(0xff, 0x5a, 0x4e, 255),
        };

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BlinkId = Shader.PropertyToID("_Blink");

        // Materiales compartidos entre mineros (se recrean si Unity los destruyo, p. ej. al recargar).
        static readonly Dictionary<string, Material> Shared = new Dictionary<string, Material>();

        // ---- jerarquia
        Transform model, hips, torso, head, legL, legR, armL, armR, toolGrp, pickNode, hammerNode, lamp;
        Renderer faceRenderer;
        Material helmetMat, metalMat;
        MaterialPropertyBlock blinkBlock;

        /// <summary>Si es true y existe Resources/Miner/miner.json, usa el minero modelado en Blender (unity_tools/blender_miner).</summary>
        public static bool UseImported = true;

        /// <summary>
        /// Si es true, cada minero se fusiona en UNA malla con esqueleto (las piezas pasan a ser huesos): se ve y se
        /// anima igual, pero se dibuja con una llamada por material en vez de ~20 (y lo mismo en la pasada de sombras).
        /// </summary>
        public static bool MergeIntoSkin;
        bool merged;
        static bool loggedMerge;
        float hipBase = 0.36f, bobScale = 1f;
        // modelo con esqueleto: los brazos vienen en pose T y las piernas abiertas; esto los lleva a la pose de descanso
        // del minero de siempre, asi su animacion se aplica tal cual encima
        Quaternion armFixL = Quaternion.identity, armFixR = Quaternion.identity, legFixL = Quaternion.identity, legFixR = Quaternion.identity, toolFix = Quaternion.identity;
        Vector3 hipOff;

        // ---- estado
        float yaw;
        bool yawInit;
        float mv;            // mezcla suave quieto (0) / caminando (1)
        float clock;         // reloj propio para respiracion y balanceo en reposo
        float celebrate, celeTarget;
        bool blinkOn;
        Color curHelmet = new Color(-1f, 0f, 0f, 0f);
        int curToolKey = -1;
        Camera cam;

        /// <summary>Crea un minero (variant elige tono de piel/pelo) como hijo de parent.</summary>
        public static MinerModel Create(Transform parent, int variant, string rig = null)
        {
            var go = new GameObject("Miner");
            go.transform.SetParent(parent, false);
            var m = go.AddComponent<MinerModel>();
            m.variant = variant;
            m.rigName = rig;
            m.Build(variant);
            return m;
        }

        int variant;
        string rigName;

        /// <summary>True si se dibuja con un modelo con esqueleto (Resources/MinerRig): trae su propio equipo y colores.</summary>
        public bool IsRigged { get; private set; }

        /// <summary>
        /// Cambia el modelo con esqueleto (p. ej. al subir de etapa: mr_oro_1 -> mr_oro_2). Si ese modelo no existe queda el
        /// minero de siempre. Devuelve true si se rearmo (el equipo colgado del modelo anterior se pierde).
        /// </summary>
        public bool SetRig(string rig)
        {
            if (rig == rigName) return false;
            bool had = IsRigged, has = RiggedMiner.Get(rig) != null;
            rigName = rig;
            if (!had && !has) return false;
            Teardown();
            Build(variant);
            return true;
        }

        void Teardown()
        {
            if (model != null) { model.gameObject.SetActive(false); Destroy(model.gameObject); }
            var smr = GetComponent<SkinnedMeshRenderer>();
            if (smr != null) Destroy(smr);
            if (helmetMat != null) Destroy(helmetMat);
            if (metalMat != null) Destroy(metalMat);
            if (mergedMesh != null) Destroy(mergedMesh);
            model = hips = torso = head = legL = legR = armL = armR = toolGrp = pickNode = hammerNode = lamp = null;
            faceRenderer = null; helmetMat = metalMat = null; mergedMesh = null; mergedCols = baseCols = null; helmetIdx = null;
            merged = false; IsRigged = false; blinkOn = false; packIdx = null; hasSpec = false;
            armFixL = armFixR = legFixL = legFixR = toolFix = Quaternion.identity;
            hipOff = Vector3.zero; hipBase = 0.36f; bobScale = 1f;
            curHelmet = new Color(-1f, 0f, 0f, 0f);
            curToolKey = -1;
        }

        /// <summary>Color del casco (skin) y herramienta equipada (k: 0 pico, 1 mazo; r: rango 0..5).</summary>
        public void SetLook(Color helmet, Tool tool)
        {
            if (model == null) return;
            if (helmet != curHelmet)
            {
                curHelmet = helmet;
                SetMatColor(helmetMat, helmet);
                TintHelmetVerts(helmet);
            }
            int k = 0, r = 0;
            if (tool != null) { k = tool.K == 1 ? 1 : 0; r = Mathf.Clamp(tool.R, 0, RankCols.Length - 1); }
            int key = k * 10 + r;
            if (key == curToolKey) return;
            curToolKey = key;
            // con el cuerpo fusionado (malla con esqueleto) las piezas no se apagan: se esconden con escala cero
            if (merged) { pickNode.localScale = k == 0 ? Vector3.one : Vector3.zero; hammerNode.localScale = k == 1 ? Vector3.one : Vector3.zero; }
            else { pickNode.gameObject.SetActive(k == 0); hammerNode.gameObject.SetActive(k == 1); }
            Color rc = RankCols[r];
            if (r == 0) rc = new Color(rc.r * 0.88f, rc.g * 0.88f, rc.b * 0.88f, 1f);
            SetMatColor(metalMat, hasSpec ? specCol : rc);
        }

        bool hasSpec;
        Color specCol;

        /// <summary>
        /// Especialista: el pico y la mochila del color de su mineral (el casco queda amarillo). Igual para los 9: el
        /// minero de siempre, solo cambia el color de su herramienta y su mochila.
        /// </summary>
        public void SetSpecialist(Color c)
        {
            if (hasSpec && c == specCol) return;
            hasSpec = true; specCol = c;
            SetMatColor(metalMat, c);
            TintPackVerts(c);
        }

        void TintPackVerts(Color c)
        {
            if (mergedMesh == null || packIdx == null || packIdx.Length == 0) return;
            // conserva las sombras pintadas de la mochila: la parte mas clara toma el color pleno
            float refL = 0f;
            foreach (int i in packIdx) refL = Mathf.Max(refL, Lum(baseCols[i]));
            if (refL <= 0f) return;
            foreach (int i in packIdx)
            {
                Color o = baseCols[i];
                float l = Lum(o);
                if (l < 0.012f) { mergedCols[i] = o; continue; }       // correas y costuras negras quedan como estan
                float k = Mathf.Clamp(0.45f + 0.75f * l / refL, 0.45f, 1.15f);
                mergedCols[i] = new Color(c.r * k, c.g * k, c.b * k, o.a);
            }
            mergedMesh.colors = mergedCols;
        }

        static float Lum(Color c) { return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b; }

        /// <summary>
        /// Pose por frame. dir: direccion de movimiento/mirada en el plano XZ (no hace falta normalizar).
        /// walkPhase: fase de caminata acumulada EN RADIANES (un ciclo de zancada = 2*pi); moving: si camina;
        /// swing: -1 sin golpe, 0..1 fase del golpe (impacto en 0.62); blink: parpadeo; dt: delta del frame.
        /// </summary>
        public void SetPose(Vector3 dir, float walkPhase, bool moving, float swing, bool blink, float dt)
        {
            if (model == null) return;
            if (dt < 0f) dt = 0f;
            clock += dt;
            mv = Mathf.MoveTowards(mv, moving ? 1f : 0f, dt * 9f);
            bool striking = swing >= 0f;
            celebrate = Mathf.MoveTowards(celebrate, striking ? 0f : celeTarget, dt * 8f);

            if (blink != blinkOn && faceRenderer != null)
            {
                blinkOn = blink;
                faceRenderer.GetPropertyBlock(blinkBlock);
                blinkBlock.SetFloat(BlinkId, blink ? 1f : 0f);
                faceRenderer.SetPropertyBlock(blinkBlock);
            }

            // orientacion: hacia dir; al festejar, de frente a la camara
            float targetYaw = yaw;
            bool haveDir = false;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-6f) { targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg; haveDir = true; }
            if (celebrate > 0.1f)
            {
                if (cam == null) cam = Camera.main;
                if (cam != null)
                {
                    Vector3 to = cam.transform.position - transform.position;
                    to.y = 0f;
                    if (to.sqrMagnitude > 1e-6f) { targetYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg; haveDir = true; }
                }
            }
            if (!yawInit)
            {
                if (haveDir) { yaw = targetYaw; yawInit = true; }
            }
            else if (haveDir)
            {
                float rate = striking ? 18f : 13f;
                yaw = Mathf.LerpAngle(yaw, targetYaw, 1f - Mathf.Exp(-dt * rate));
            }
            model.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Apply(walkPhase, moving, swing);
        }

        /// <summary>Nodo que gira con la mirada del minero y el torso (al que se cuelga el equipo del especialista).</summary>
        public Transform Rig { get { return model; } }
        public Transform Torso { get { return torso; } }

        /// <summary>Festejo (0 = nada, 1 = salto con brazos arriba); el mundo lo baja con el tiempo.</summary>
        public void SetCelebrate(float amount) { celeTarget = Mathf.Clamp01(amount); }

        /// <summary>Posicion mundial de la lampara del casco (para luces en cueva/volcan).</summary>
        public Vector3 LampWorldPos
        {
            get { return lamp != null ? lamp.position : transform.position + Vector3.up * 1.1f; }
        }

        void OnDestroy()
        {
            if (helmetMat != null) Destroy(helmetMat);
            if (mergedMesh != null) Destroy(mergedMesh);
            if (metalMat != null) Destroy(metalMat);
        }

        // ------------------------------------------------------------------ animacion
        static float EaseOut(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x); } // ease(x, 0.5) de Godot
        static float EaseIn(float x) { x = Mathf.Clamp01(x); return x * x; }                     // ease(x, 2.0) de Godot

        void Apply(float stride, bool moving, float swing)
        {
            float s = Mathf.Sin(stride);
            float amp = 0.6f;
            float idleArm = Mathf.Sin(clock * 2f) * 0.05f;
            float deg = Mathf.Rad2Deg;

            legL.localRotation = Quaternion.Euler(s * amp * mv * deg, 0f, 0f) * legFixL;
            legR.localRotation = Quaternion.Euler(-s * amp * mv * deg, 0f, 0f) * legFixR;
            float armLx = Mathf.Lerp(idleArm, -s * amp * 0.9f, mv);
            float armLz = -0.18f;
            float hipY = hipBase + Mathf.Abs(s) * 0.05f * bobScale * mv;
            float torsoX = 0.08f * mv;
            torso.localScale = Vector3.one * (1f + Mathf.Sin(clock * 3f) * 0.012f);
            float headZ = Mathf.Sin(clock * 1.7f) * 0.04f;

            // brazo de la herramienta: a = giro del brazo, w = muñeca
            float a = -1f, w = -2f;
            if (swing >= 0f)
            {
                float u = swing;
                if (u < 0.5f)
                {
                    float k = EaseOut(u / 0.5f);
                    a = Mathf.Lerp(-1f, -3.25f, k);
                    w = Mathf.Lerp(-2f, -0.7f, k);
                    torsoX = Mathf.Lerp(0f, -0.18f, k);
                }
                else if (u < 0.66f)
                {
                    float k = (u - 0.5f) / 0.16f;
                    a = Mathf.Lerp(-3.25f, -0.7f, k);
                    w = Mathf.Lerp(-0.7f, 0f, k);
                    torsoX = Mathf.Lerp(-0.18f, 0.28f, k);
                }
                else
                {
                    float k = EaseIn((u - 0.66f) / 0.34f);
                    a = Mathf.Lerp(-0.7f, -1f, k);
                    w = Mathf.Lerp(0f, -2f, k);
                    torsoX = Mathf.Lerp(0.28f, 0f, k);
                }
            }
            else if (moving)
            {
                a = -1f + s * 0.15f;
            }
            float armRx = a, armRz = 0.12f, toolX = w;
            float headX = 0f;
            float legLx = s * amp * mv, legRx = -s * amp * mv;
            float jump = 0f;

            if (celebrate > 0f)
            {
                float c = celebrate;
                float wob = Mathf.Sin(clock * 16f) * 0.12f;
                armLx = Mathf.Lerp(armLx, -2.9f + wob, c);
                armLz = Mathf.Lerp(armLz, -0.35f, c);
                armRx = Mathf.Lerp(armRx, -2.9f - wob, c);
                armRz = Mathf.Lerp(armRz, 0.35f, c);
                toolX = Mathf.Lerp(toolX, -0.2f, c);
                torsoX = Mathf.Lerp(torsoX, -0.12f, c);
                headX = -0.12f * c;
                legLx = Mathf.Lerp(legLx, 0.25f, c);
                legRx = Mathf.Lerp(legRx, -0.25f, c);
                legL.localRotation = Quaternion.Euler(legLx * deg, 0f, 0f) * legFixL;
                legR.localRotation = Quaternion.Euler(legRx * deg, 0f, 0f) * legFixR;
                jump = Mathf.Abs(Mathf.Sin(clock * 9f)) * 0.22f * c;
            }

            armL.localRotation = Quaternion.Euler(armLx * deg, 0f, armLz * deg) * armFixL;
            armR.localRotation = Quaternion.Euler(armRx * deg, 0f, armRz * deg) * armFixR;
            toolGrp.localRotation = toolFix * Quaternion.Euler(toolX * deg, 0f, 0f);
            torso.localRotation = Quaternion.Euler(torsoX * deg, 0f, 0f);
            head.localRotation = Quaternion.Euler(headX * deg, 0f, headZ * deg);
            hips.localPosition = new Vector3(hipOff.x, hipY, hipOff.z);
            model.localPosition = new Vector3(0f, jump * ModelScale, 0f);
        }

        // ------------------------------------------------------------------ materiales
        static Shader FindShader(string name)
        {
            Shader sh = Shader.Find(name);
            if (sh != null && !sh.isSupported) sh = null;
            return sh;
        }

        // Material compartido; si el shader propio no existe cae a Standard con el color para que nunca quede rosa.
        static Material GetMat(string key, string shaderName, Color col, Color? emission = null, float rim = -1f, float spec = 0f)
        {
            Material m;
            if (Shared.TryGetValue(key, out m) && m != null) return m;
            m = MakeMat(shaderName, col, emission, rim, spec);
            m.name = "Miner_" + key;
            Shared[key] = m;
            return m;
        }

        static Material MakeMat(string shaderName, Color col, Color? emission, float rim, float spec)
        {
            Shader sh = FindShader(shaderName);
            Material m;
            if (sh != null)
            {
                m = new Material(sh);
                if (shaderName == "Mineros/MinerToon")
                {
                    m.SetColor("_Color", col);
                    if (emission.HasValue) m.SetColor("_EmissionColor", emission.Value);
                    if (rim >= 0f) m.SetFloat("_Rim", rim);
                    if (spec > 0f) m.SetFloat("_Spec", spec);
                }
                else if (shaderName == "Mineros/MinerFace")
                {
                    m.SetColor("_Skin", col);
                }
            }
            else
            {
                Shader st = Shader.Find("Standard");
                if (st == null) st = Shader.Find("Sprites/Default");
                m = new Material(st);
                Color c = col;
                if (shaderName == "Mineros/MinerVest") c = new Color(1f, 0.54f, 0.16f, 1f);
                if (emission.HasValue && emission.Value.maxColorComponent > 0.5f) c = emission.Value;
                m.color = c;
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.15f);
            }
            m.hideFlags = HideFlags.DontSave;
            return m;
        }

        static void SetMatColor(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            else m.color = c;
        }

        static Color Dark(Color c, float amt) { return new Color(c.r * (1f - amt), c.g * (1f - amt), c.b * (1f - amt), 1f); }

        // ------------------------------------------------------------------ construccion
        const string Toon = "Mineros/MinerToon";

        static Transform Node(Transform parent, string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            return go.transform;
        }

        static MeshRenderer Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Vector3? scale = null, Vector3? eulerRad = null)
        {
            var go = new GameObject(name);
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            if (scale.HasValue) t.localScale = scale.Value;
            if (eulerRad.HasValue) t.localRotation = Quaternion.Euler(eulerRad.Value * Mathf.Rad2Deg);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            // piezas sueltas (herramienta, casco, partes de codigo): sin sombra real, el minero ya tiene su mancha (blob);
            // con 12 mineros eran ~100 objetos mas en la pasada de sombras (auditoria final)
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return mr;
        }

        void Build(int variant)
        {
            var rd = RiggedMiner.Get(rigName);
            if (rd != null) { BuildRigged(rd); return; }
            if (UseImported && ImportedMiner.Data != null) { BuildImported(variant); return; }
            int vi = ((variant % 4) + 4) % 4;
            Color skin = Skins[vi], hair = Hairs[vi];
            blinkBlock = new MaterialPropertyBlock();

            Material shirtM = GetMat("shirt", Toon, new Color32(0x4d, 0x55, 0x63, 255));
            Material pantsM = GetMat("pants", Toon, new Color32(0x8a, 0x6a, 0x48, 255));
            Material bootM = GetMat("boot", Toon, new Color32(0x5a, 0x3d, 0x2c, 255));
            Material gloveM = GetMat("glove", Toon, new Color32(0xb5, 0x7a, 0x48, 255));
            Material scarfM = GetMat("scarf", Toon, new Color32(0x2f, 0xb3, 0xa6, 255));
            Material woodM = GetMat("wood", Toon, new Color32(0xb9, 0x82, 0x4a, 255));
            Material skinM = GetMat("skin" + vi, Toon, skin);
            Material noseM = GetMat("nose" + vi, Toon, Dark(skin, 0.06f));
            Material hairM = GetMat("hair" + vi, Toon, hair);
            Material vestM = GetMat("vest", "Mineros/MinerVest", Color.white);
            Material faceM = GetMat("face" + vi, "Mineros/MinerFace", skin);
            if (faceM.HasProperty("_Brow")) faceM.SetColor("_Brow", Dark(hair, 0.2f));
            Material housingM = GetMat("lamphousing", Toon, new Color32(0x8c, 0x91, 0x98, 255));
            Material lensM = GetMat("lens", Toon, new Color32(0xff, 0xf2, 0xa6, 255), new Color(1f, 0.9f, 0.5f, 1f));

            // por instancia: casco y metal de la herramienta
            helmetMat = MakeMat(Toon, new Color32(0xf5, 0xc5, 0x31, 255), null, 0.3f, 0.55f);
            helmetMat.name = "Miner_helmet";
            metalMat = MakeMat(Toon, new Color32(0x8d, 0x97, 0xa3, 255), null, 0.25f, 0.35f);
            metalMat.name = "Miner_metal";

            model = Node(transform, "Model", Vector3.zero);
            model.localScale = Vector3.one * ModelScale;
            hips = Node(model, "Hips", new Vector3(0f, 0.36f, 0f));

            // piernas
            legL = Node(hips, "LegL", new Vector3(-0.15f, 0f, 0f));
            legR = Node(hips, "LegR", new Vector3(0.15f, 0f, 0f));
            Mesh legMesh = MinerMeshes.Capsule(0.11f, 0.34f, 20);
            Mesh bootMesh = MinerMeshes.Sphere(0.15f, 20);
            foreach (Transform leg in new[] { legL, legR })
            {
                Part(leg, "Leg", legMesh, pantsM, new Vector3(0f, -0.13f, 0f));
                Part(leg, "Boot", bootMesh, bootM, new Vector3(0f, -0.3f, 0.05f), new Vector3(1f, 0.62f, 1.3f));
            }

            // torso
            torso = Node(hips, "Torso", new Vector3(0f, 0.02f, 0f));
            Part(torso, "Vest", MinerMeshes.Capsule(0.27f, 0.72f, 32), vestM, new Vector3(0f, 0.27f, 0f), new Vector3(1f, 1f, 0.86f));
            Part(torso, "Hip", MinerMeshes.Capsule(0.27f, 0.6f, 24), pantsM, new Vector3(0f, 0.07f, 0f), new Vector3(1.02f, 0.5f, 0.88f));
            Part(torso, "Scarf", MinerMeshes.Cylinder(0.2f, 0.25f, 0.09f, 28), scarfM, new Vector3(0f, 0.6f, 0f), new Vector3(1f, 1f, 0.9f));
            Part(torso, "Knot", MinerMeshes.Sphere(0.08f, 16), scarfM, new Vector3(0f, 0.56f, 0.21f), new Vector3(1f, 0.8f, 0.6f));

            // brazos (hombro como pivote)
            armL = Node(torso, "ArmL", new Vector3(-0.31f, 0.5f, 0f));
            armR = Node(torso, "ArmR", new Vector3(0.31f, 0.5f, 0f));
            Mesh armMesh = MinerMeshes.Capsule(0.085f, 0.36f, 16);
            Mesh gloveMesh = MinerMeshes.Sphere(0.11f, 20);
            foreach (Transform arm in new[] { armL, armR })
            {
                Part(arm, "Arm", armMesh, shirtM, new Vector3(0f, -0.15f, 0f));
                Part(arm, "Glove", gloveMesh, gloveM, new Vector3(0f, -0.34f, 0f));
            }

            // herramientas en la mano derecha (pico y mazo; SetLook muestra una)
            toolGrp = Node(armR, "Tool", new Vector3(0f, -0.34f, 0f));
            Part(toolGrp, "Shaft", MinerMeshes.Cylinder(0.04f, 0.045f, 1f, 16), woodM, new Vector3(0f, -0.3f, 0f));
            pickNode = Node(toolGrp, "Pick", new Vector3(0f, -0.78f, 0f));
            Part(pickNode, "Hub", MinerMeshes.Cylinder(0.075f, 0.075f, 0.16f, 16), metalMat, Vector3.zero);
            Mesh spike = MinerMeshes.Cylinder(0f, 0.085f, 0.44f, 16);
            Part(pickNode, "SpikeF", spike, metalMat, new Vector3(0f, 0f, 0.28f), null, new Vector3(Mathf.PI * 0.5f + 0.18f, 0f, 0f));
            Part(pickNode, "SpikeB", spike, metalMat, new Vector3(0f, 0f, -0.28f), null, new Vector3(-Mathf.PI * 0.5f - 0.18f, 0f, 0f));
            hammerNode = Node(toolGrp, "Hammer", new Vector3(0f, -0.8f, 0f));
            Part(hammerNode, "Block", MinerMeshes.Box(new Vector3(0.24f, 0.24f, 0.52f)), metalMat, Vector3.zero);
            Mesh capMesh = MinerMeshes.Cylinder(0.13f, 0.13f, 0.06f, 20);
            Part(hammerNode, "CapF", capMesh, metalMat, new Vector3(0f, 0f, 0.27f), null, new Vector3(Mathf.PI * 0.5f, 0f, 0f));
            Part(hammerNode, "CapB", capMesh, metalMat, new Vector3(0f, 0f, -0.27f), null, new Vector3(Mathf.PI * 0.5f, 0f, 0f));

            // cabeza
            head = Node(torso, "Head", new Vector3(0f, 0.66f, 0f));
            faceRenderer = Part(head, "Face", MinerMeshes.Sphere(0.52f, 48), faceM, new Vector3(0f, 0.46f, 0f), new Vector3(1f, 0.95f, 0.95f));
            Mesh earMesh = MinerMeshes.Sphere(0.1f, 16);
            Part(head, "EarL", earMesh, skinM, new Vector3(-0.5f, 0.42f, -0.02f), new Vector3(0.6f, 1f, 1f));
            Part(head, "EarR", earMesh, skinM, new Vector3(0.5f, 0.42f, -0.02f), new Vector3(0.6f, 1f, 1f));
            Part(head, "Nose", MinerMeshes.Sphere(0.075f, 16), noseM, new Vector3(0f, 0.42f, 0.5f));
            Part(head, "Hair", MinerMeshes.Sphere(0.52f, 32), hairM, new Vector3(0f, 0.6f, -0.1f), new Vector3(1.05f, 0.72f, 0.96f));

            // casco
            Transform hat = Node(head, "Hat", new Vector3(0f, 0.8f, -0.04f));
            hat.localRotation = Quaternion.Euler(-0.22f * Mathf.Rad2Deg, 0f, 0f);
            Part(hat, "Dome", MinerMeshes.Dome(0.5f, 40), helmetMat, Vector3.zero, new Vector3(1.04f, 0.78f * 1.0f, 1f));
            Part(hat, "Brim", MinerMeshes.Cylinder(0.6f, 0.62f, 0.06f, 40), helmetMat, new Vector3(0f, 0f, 0.07f));
            lamp = Node(hat, "Lamp", new Vector3(0f, 0.2f, 0.47f));
            float tilt = Mathf.PI * 0.5f - 0.25f;
            Part(lamp, "Housing", MinerMeshes.Cylinder(0.13f, 0.15f, 0.12f, 24), housingM, Vector3.zero, null, new Vector3(tilt, 0f, 0f));
            Part(lamp, "Lens", MinerMeshes.Cylinder(0.1f, 0.1f, 0.02f, 24), lensM, new Vector3(0f, 0.02f, 0.07f), null, new Vector3(tilt, 0f, 0f));

            // El frente del minero apunta a +Z local; arranca mirando a +Z.
            curToolKey = -1;
            SetLook(new Color32(0xf5, 0xc5, 0x31, 255), new Tool(0, 0));
            Apply(0f, false, -1f);
        }

        // ------------------------------------------------------------------ minero modelado en Blender
        const float ImportedScale = 1.8f;   // el modelo mide 1.0; un poco mas alto que el procedural (~1.55 m) para leerse igual de bien

        void BuildImported(int variant)
        {
            int vi = ((variant % 4) + 4) % 4;
            blinkBlock = new MaterialPropertyBlock();
            var d = ImportedMiner.Data;
            Material vcM = ImportedMiner.VertexColorMaterial();
            Material skinM = GetMat("skin" + vi, Toon, ImportedSkins[vi]);
            Material hairM = GetMat("hairI" + vi, Toon, ImportedHairs[vi]);
            Material lensM = GetMat("lensI", Toon, new Color(1f, 0.98f, 0.92f, 1f), new Color(0.95f, 0.95f, 0.9f, 1f));
            helmetMat = MakeMat(Toon, new Color32(0xf5, 0xa8, 0x23, 255), null, 0.3f, 0.55f);
            helmetMat.name = "Miner_helmet";
            metalMat = MakeMat(Toon, new Color32(0x8d, 0x97, 0xa3, 255), null, 0.25f, 0.35f);
            metalMat.name = "Miner_metal";

            model = Node(transform, "Model", Vector3.zero);
            model.localScale = Vector3.one * ImportedScale;
            var nodes = new Dictionary<string, Transform>();
            var pending = new List<ImportedMiner.Group>(d.groups);
            while (pending.Count > 0)
            {
                // padres antes que hijos (el JSON no garantiza el orden)
                int gi = pending.FindIndex(x => string.IsNullOrEmpty(x.parent) || nodes.ContainsKey(x.parent));
                if (gi < 0) { Debug.LogError("Minero importado: jerarquia rota"); break; }
                var g = pending[gi];
                pending.RemoveAt(gi);
                Transform parent = string.IsNullOrEmpty(g.parent) ? model : nodes[g.parent];
                Transform t = Node(parent, g.name, new Vector3(g.pivot[0], g.pivot[1], g.pivot[2]));
                nodes[g.name] = t;
                var mats = new Material[g.subs.Length];
                for (int i = 0; i < g.subs.Length; i++)
                {
                    switch (g.subs[i].channel)
                    {
                        case "skin": mats[i] = skinM; break;
                        case "hair": mats[i] = hairM; break;
                        case "helmet": mats[i] = helmetMat; break;
                        case "lens": mats[i] = lensM; break;
                        default: mats[i] = vcM; break;
                    }
                }
                var go = new GameObject("Mesh");
                go.transform.SetParent(t, false);
                go.AddComponent<MeshFilter>().sharedMesh = ImportedMiner.MeshOf(g);
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = mats;
                mr.shadowCastingMode = ShadowCastingMode.On;
                mr.receiveShadows = true;
                mr.lightProbeUsage = LightProbeUsage.Off;
                mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            hips = nodes["hips"]; torso = nodes["torso"]; head = nodes["head"];
            armL = nodes["armL"]; armR = nodes["armR"]; legL = nodes["legL"]; legR = nodes["legR"];
            hipBase = hips.localPosition.y;
            bobScale = 1f / 1.85f;

            // herramientas (las procedurales, con el metal por rango) en la mano derecha, al mismo tamaño en el mundo
            toolGrp = Node(armR, "Tool", new Vector3(0f, -d.handLen, 0f));
            toolGrp.localScale = Vector3.one * (ModelScale / ImportedScale);
            BuildTools(GetMat("wood", Toon, new Color32(0x7a, 0x4a, 0x2a, 255)));

            lamp = Node(head, "Lamp", new Vector3(0f, 0.25f, 0.16f));
            curToolKey = -1;
            if (MergeIntoSkin) MergeSkinned();
            SetLook(new Color32(0xf5, 0xa8, 0x23, 255), new Tool(0, 0));
            Apply(0f, false, -1f);
        }

        // ------------------------------------------------------------------ minero con esqueleto (TRELLIS.2 + UniRig)
        /// <summary>Alto en el mundo del modelo con esqueleto (mide 1) por etapa: crece un poco al evolucionar.</summary>
        static float RigScaleOf(string rig)
        {
            char t = string.IsNullOrEmpty(rig) ? '1' : rig[rig.Length - 1];
            return ImportedScale * (t == '3' ? 1.18f : t == '2' ? 1.08f : 1f);
        }

        void BuildRigged(RiggedMiner.Data d)
        {
            IsRigged = true;
            blinkBlock = new MaterialPropertyBlock();
            float scale = RigScaleOf(rigName);
            model = Node(transform, "Model", Vector3.zero);
            model.localScale = Vector3.one * scale;

            // huesos: sin giro, en la cabeza de cada uno (padres antes que hijos)
            int nb = d.bones.Length;
            var bt = new Transform[nb];
            var done = new bool[nb];
            for (int pass = 0, left = nb; left > 0 && pass <= nb; pass++)
                for (int b = 0; b < nb; b++)
                {
                    int p = d.parent[b];
                    if (done[b] || (p >= 0 && !done[p])) continue;
                    Vector3 at = d.Head(b) - (p >= 0 ? d.Head(p) : Vector3.zero);
                    bt[b] = Node(p >= 0 ? bt[p] : model, d.bones[b], at);
                    done[b] = true; left--;
                }
            for (int b = 0; b < nb; b++) if (bt[b] == null) bt[b] = Node(model, d.bones[b], d.Head(b));   // jerarquia rota: quedan sueltos

            hips = bt[d.Role(RiggedMiner.Hips)]; torso = bt[d.Role(RiggedMiner.Chest)]; head = bt[d.Role(RiggedMiner.HeadR)];
            armL = bt[d.Role(RiggedMiner.UpL)]; armR = bt[d.Role(RiggedMiner.UpR)];
            legL = bt[d.Role(RiggedMiner.LegL)]; legR = bt[d.Role(RiggedMiner.LegR)];
            hipOff = hips.localPosition;
            hipBase = hipOff.y;
            bobScale = ModelScale / scale;

            // brazos de la pose T hacia abajo (un poco abiertos para no atravesar el cuerpo); piernas derechas
            const float Out = 10f * Mathf.Deg2Rad;
            armFixL = Fix(d, RiggedMiner.UpL, RiggedMiner.LoL, new Vector3(-Mathf.Sin(Out), -Mathf.Cos(Out), 0f), 0f);
            armFixR = Fix(d, RiggedMiner.UpR, RiggedMiner.LoR, new Vector3(Mathf.Sin(Out), -Mathf.Cos(Out), 0f), 0f);
            legFixL = Fix(d, RiggedMiner.LegL, RiggedMiner.ShinL, Vector3.down, 8f);
            legFixR = Fix(d, RiggedMiner.LegR, RiggedMiner.ShinR, Vector3.down, 8f);

            var go = new GameObject("Skin");
            go.transform.SetParent(model, false);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = d.Mesh;
            smr.bones = bt;
            smr.sharedMaterial = d.Mat;
            smr.quality = SkinQuality.Bone4;
            smr.updateWhenOffscreen = false;
            smr.shadowCastingMode = ShadowCastingMode.On;
            smr.receiveShadows = true;
            smr.lightProbeUsage = LightProbeUsage.Off;
            smr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            var bb = d.Mesh.bounds; bb.Expand(0.8f);   // margen para brazos y herramienta en movimiento
            smr.localBounds = bb;

            // herramienta procedural (metal por rango) en la mano derecha, al mismo tamaño en el mundo que siempre
            helmetMat = null;
            metalMat = MakeMat(Toon, new Color32(0x8d, 0x97, 0xa3, 255), null, 0.25f, 0.35f);
            metalMat.name = "Miner_metal";
            Transform hand = bt[d.Role(RiggedMiner.HandR)];
            Vector3 along = (d.Head(d.Role(RiggedMiner.HandR)) - d.Head(d.Role(RiggedMiner.LoR))).normalized;
            toolGrp = Node(hand, "Tool", along * 0.035f);
            toolGrp.localScale = Vector3.one * (ModelScale / scale);
            toolFix = Quaternion.Inverse(armFixR);
            Material woodM = GetMat("wood", Toon, new Color32(0x7a, 0x4a, 0x2a, 255));
            BuildTools(woodM);

            lamp = Node(head, "Lamp", new Vector3(0f, 0.9f - d.Head(d.Role(RiggedMiner.HeadR)).y, 0.12f));
            curToolKey = -1;
            SetLook(Color.white, new Tool(0, 0));
            Apply(0f, false, -1f);
        }

        /// <summary>Giro que lleva el hueso (de su cabeza a la del siguiente) a la direccion de descanso.</summary>
        static Quaternion Fix(RiggedMiner.Data d, int role, int next, Vector3 rest, float minDeg)
        {
            Vector3 dir = d.Head(d.Role(next)) - d.Head(d.Role(role));
            if (dir.sqrMagnitude < 1e-8f) return Quaternion.identity;
            if (Vector3.Angle(dir, rest) < minDeg) return Quaternion.identity;
            return Quaternion.FromToRotation(dir.normalized, rest.normalized);
        }

        /// <summary>Pico y mazo bajo toolGrp (SetLook muestra uno).</summary>
        void BuildTools(Material woodM)
        {
            Part(toolGrp, "Shaft", MinerMeshes.Cylinder(0.04f, 0.045f, 1f, 16), woodM, new Vector3(0f, -0.3f, 0f));
            pickNode = Node(toolGrp, "Pick", new Vector3(0f, -0.78f, 0f));
            Part(pickNode, "Hub", MinerMeshes.Cylinder(0.075f, 0.075f, 0.16f, 16), metalMat, Vector3.zero);
            Mesh spike = MinerMeshes.Cylinder(0f, 0.085f, 0.44f, 16);
            Part(pickNode, "SpikeF", spike, metalMat, new Vector3(0f, 0f, 0.28f), null, new Vector3(Mathf.PI * 0.5f + 0.18f, 0f, 0f));
            Part(pickNode, "SpikeB", spike, metalMat, new Vector3(0f, 0f, -0.28f), null, new Vector3(-Mathf.PI * 0.5f - 0.18f, 0f, 0f));
            hammerNode = Node(toolGrp, "Hammer", new Vector3(0f, -0.8f, 0f));
            Part(hammerNode, "Block", MinerMeshes.Box(new Vector3(0.24f, 0.24f, 0.52f)), metalMat, Vector3.zero);
            Mesh capMesh = MinerMeshes.Cylinder(0.13f, 0.13f, 0.06f, 20);
            Part(hammerNode, "CapF", capMesh, metalMat, new Vector3(0f, 0f, 0.27f), null, new Vector3(Mathf.PI * 0.5f, 0f, 0f));
            Part(hammerNode, "CapB", capMesh, metalMat, new Vector3(0f, 0f, -0.27f), null, new Vector3(Mathf.PI * 0.5f, 0f, 0f));
        }

        /// <summary>Junta todas las piezas en una malla con pesos rigidos (cada vertice sigue a su pieza).</summary>
        void MergeSkinned()
        {
            var parts = model.GetComponentsInChildren<MeshRenderer>(true);
            var bones = new List<Transform>();
            var bind = new List<Matrix4x4>();
            var verts = new List<Vector3>();
            var nrms = new List<Vector3>();
            var cols = new List<Color>();
            var weights = new List<BoneWeight>();
            var matOrder = new List<Material>();
            var pv = new List<int>();   // vertices de la mochila (para teñirla por especialista)
            var tris = new Dictionary<Material, List<int>>();
            Matrix4x4 rootW2L = transform.worldToLocalMatrix, rootL2W = transform.localToWorldMatrix;
            foreach (var mr in parts)
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mesh = mf.sharedMesh;
                var bone = mr.transform;
                int bi = bones.Count;
                bones.Add(bone);
                bind.Add(bone.worldToLocalMatrix * rootL2W);
                Matrix4x4 toRoot = rootW2L * bone.localToWorldMatrix;
                int baseV = verts.Count;
                var mv = mesh.vertices; var mn = mesh.normals; var mc = mesh.colors;
                bool isTorso = torso != null && bone.parent == torso && mc.Length == mv.Length;
                for (int i = 0; i < mv.Length; i++)
                {
                    if (isTorso && mv[i].z < -0.10f) pv.Add(verts.Count);
                    verts.Add(toRoot.MultiplyPoint3x4(mv[i]));
                    nrms.Add(mn.Length == mv.Length ? toRoot.MultiplyVector(mn[i]).normalized : Vector3.up);
                    cols.Add(mc.Length == mv.Length ? mc[i] : Color.white);
                    weights.Add(new BoneWeight { boneIndex0 = bi, weight0 = 1f });
                }
                var mats = mr.sharedMaterials;
                for (int sm = 0; sm < mesh.subMeshCount; sm++)
                {
                    var mat = mats[Mathf.Min(sm, mats.Length - 1)];
                    List<int> list;
                    if (!tris.TryGetValue(mat, out list)) { list = new List<int>(); tris[mat] = list; matOrder.Add(mat); }
                    foreach (var ix in mesh.GetTriangles(sm)) list.Add(ix + baseV);
                }
                Object.Destroy(mr);
                Object.Destroy(mf);
            }
            if (bones.Count == 0) return;
            // el casco del modelo de Blender viene pintado en colores de vertice: se guardan sus vertices para teñirlo
            var hv = new List<int>();
            for (int i = 0; i < verts.Count; i++)
            {
                var bt = bones[weights[i].boneIndex0];
                if (head == null || (bt != head && bt.parent != head)) continue;
                float hh, ss, vv;
                Color.RGBToHSV(cols[i], out hh, out ss, out vv);
                bool ginger = Mathf.Abs(cols[i].r - 0.79f) < 0.06f && Mathf.Abs(cols[i].g - 0.51f) < 0.06f && Mathf.Abs(cols[i].b - 0.23f) < 0.06f;   // pelo colorado
                if (hh > 0.07f && hh < 0.17f && ss > 0.6f && vv > 0.45f && !ginger) hv.Add(i);
            }
            helmetIdx = hv.ToArray();
            packIdx = pv.ToArray();
            mergedCols = cols.ToArray();
            baseCols = cols.ToArray();
            var m = new Mesh { name = "MineroFusionado" };
            m.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            m.SetVertices(verts); m.SetNormals(nrms); m.SetColors(cols);
            m.subMeshCount = matOrder.Count;
            for (int i = 0; i < matOrder.Count; i++) m.SetTriangles(tris[matOrder[i]], i);
            m.boneWeights = weights.ToArray();
            m.bindposes = bind.ToArray();
            m.RecalculateBounds();
            mergedMesh = m;
            var smr = gameObject.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = m;
            smr.bones = bones.ToArray();
            smr.sharedMaterials = matOrder.ToArray();
            smr.quality = SkinQuality.Bone1;
            smr.updateWhenOffscreen = false;
            smr.shadowCastingMode = ShadowCastingMode.On;
            smr.receiveShadows = true;
            smr.lightProbeUsage = LightProbeUsage.Off;
            smr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            var b = m.bounds; b.Expand(1.5f);   // margen para brazos y herramienta en movimiento
            smr.localBounds = b;
            merged = true;
            if (!loggedMerge) { loggedMerge = true; Debug.Log("fusion minero: verts=" + verts.Count + " sub=" + matOrder.Count + " huesos=" + bones.Count + " bounds=" + b + " partes=" + parts.Length); }
        }

        Mesh mergedMesh;
        Color[] mergedCols, baseCols;
        int[] helmetIdx;
        int[] packIdx;

        /// <summary>Tiñe los vertices del casco (fusionado) conservando su sombreado pintado.</summary>
        void TintHelmetVerts(Color helmet)
        {
            if (mergedMesh == null || helmetIdx == null || helmetIdx.Length == 0) return;
            foreach (int i in helmetIdx)
            {
                Color o = baseCols[i];
                float k = Mathf.Max(o.r, Mathf.Max(o.g, o.b)) / 0.96f;
                mergedCols[i] = new Color(helmet.r * k, helmet.g * k, helmet.b * k, o.a);
            }
            mergedMesh.colors = mergedCols;
        }

        // piel y pelo del modelo de Blender (variante 0 = la referencia)
        static readonly Color[] ImportedSkins =
        {
            new Color32(0xef, 0xae, 0x86, 255), new Color32(0xe2, 0xa7, 0x7c, 255),
            new Color32(0xa8, 0x70, 0x4a, 255), new Color32(0xf7, 0xcf, 0xae, 255),
        };
        static readonly Color[] ImportedHairs =
        {
            new Color32(0x2e, 0x20, 0x19, 255), new Color32(0x5a, 0x3a, 0x22, 255),
            new Color32(0x1f, 0x17, 0x12, 255), new Color32(0xc9, 0x81, 0x3a, 255),
        };
    }
}
