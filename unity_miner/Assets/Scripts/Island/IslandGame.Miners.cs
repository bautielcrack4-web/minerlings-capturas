using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using UnityEngine;
using FxApi = Mineros.Fx.Fx;
using Juice = Mineros.Fx.Juice;

namespace Mineros.IslandView
{
    /// <summary>
    /// Mineros como personajes (biblia 3.4): casco del color de su rareza, dorados que dejan estela de brillo, linterna
    /// del casco encendida de noche, poses por estado (dormir con "Zzz", comer con corazones, cansado encorvado, correr
    /// inclinado a la veta gigante), huellas y polvito en los caminos de tierra, subida de nivel con columna de luz,
    /// amistades con corazon y choque de manos, y el barco que trae a los candidatos.
    /// </summary>
    public sealed partial class IslandGame
    {
        public static readonly Color[] RarityCol =
        {
            new Color32(0xf5, 0xa8, 0x23, 255),   // comun: casco amarillo clasico
            new Color32(0x4a, 0xa3, 0xf0, 255),   // raro
            new Color32(0xb0, 0x6e, 0xf0, 255),   // epico
            new Color32(0xff, 0x7a, 0x2e, 255),   // legendario
        };
        public static readonly Color GoldCol = new Color32(0xff, 0xd2, 0x3a, 255);

        /// <summary>Casco del color de su mineral (0.10: el especialista se reconoce de un vistazo); el dorado, dorado.</summary>
        public static Color HelmetOf(Miner m) { return m.Golden ? GoldCol : (Color)new Color32(0xf5, 0xa8, 0x23, 255); }   // casco amarillo para todos; el oficio lo dicen el pico y la mochila

        sealed class Step { public Transform T; public MeshRenderer R; public float Age; }
        readonly List<Step> steps = new List<Step>();

        void MinerExtras(MinerView mv, float dt)
        {
            var m = mv.M;
            var t = mv.Model.transform;
            // pose del cuerpo segun el estado (el modelo gira solo; aca se inclina la raiz)
            float lean = 0f, squat = 1f;
            if (m.State == MState.Resting) { lean = 18f + Mathf.Sin(Time.time * 1.6f) * 3f; squat = 0.86f; }
            else if (m.State == MState.Eating) { lean = 6f + Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 6f; }
            else if (m.Moving && m.Energy < 25f) lean = 14f;                 // cansado: encorvado
            else if (m.State == MState.ToOre && Isl.Giant != null && m.Target == Isl.Giant.Id) lean = 12f;   // corre a la gigante
            else if (m.CarryKind >= 0) lean = 8f;                            // carga la bolsa
            mv.Lean = Mathf.Lerp(mv.Lean, lean, 1f - Mathf.Exp(-dt * 8f));
            var dir = new Vector3(Mathf.Sin(m.Face), 0f, Mathf.Cos(m.Face));
            Vector3 axis = Vector3.Cross(Vector3.up, dir);
            if (axis.sqrMagnitude < 1e-4f) axis = Vector3.right;
            if (t.localScale.x > MinerScale * 0.4f)
            {
                t.localRotation = Quaternion.AngleAxis(mv.Lean, axis);
                t.localScale = new Vector3(MinerScale, MinerScale * Mathf.Lerp(t.localScale.y / MinerScale, squat, 1f - Mathf.Exp(-dt * 6f)), MinerScale);
            }
            // Zzz al dormir, corazones al comer (con ronquido / masticar si la camara esta cerca)
            mv.FxT -= dt;
            float near = Sound != null ? Sound.Zoom01 : 0.5f;
            if (mv.FxT <= 0f)
            {
                if (m.State == MState.Resting)
                {
                    mv.FxT = 1.1f;
                    Ui.FloatText(t.position + Vector3.up * MH(2.1f), "z", new Color(0.8f, 0.88f, 1f), 26 + Random.Range(0, 10));
                    if (near > 0.6f && OnScreen(t.position) && Random.value < 0.4f) Sfx.Play("snore", -26f + 6f * near);
                }
                else if (m.State == MState.Eating)
                {
                    mv.FxT = 0.8f;
                    if (near > 0.6f && OnScreen(t.position)) Sfx.Play("munch", -28f + 6f * near);
                    if (Random.value < 0.5f) Ui.FloatText(t.position + Vector3.up * MH(2f), "♥", new Color(1f, 0.45f, 0.55f), 30);
                }
                else mv.FxT = 0.5f;
            }
            // dorado: estela de brillo al caminar
            if (m.Golden && (m.Moving || Random.value < dt * 2f))
            {
                mv.TrailT -= dt;
                if (mv.TrailT <= 0f) { mv.TrailT = 0.12f; FxApi.Play("glint", t.position + Vector3.up * (0.4f + Random.value), GoldCol, 0.7f); }
            }
            // huellas y polvito en los caminos de tierra
            if (m.Moving && Isl.OnPath(m.X, m.Z, 0f))
            {
                mv.StepT -= dt * Isl.Perf(m) * Isl.WalkMult(m);
                if (mv.StepT <= 0f)
                {
                    mv.StepT = 0.32f;
                    mv.StepSide = -mv.StepSide;
                    Footprint(t.position + axis * 0.1f * mv.StepSide, m.Face);
                    if (Random.value < 0.35f) FxApi.Play("tinydust", t.position + Vector3.up * 0.05f, new Color(0.85f, 0.72f, 0.52f), 0.5f);
                }
            }
            // linterna del casco de noche
            float night = Isl.Night;
            if (night > 0.3f)
            {
                if (mv.Lamp == null) mv.Lamp = IslandFxKit.Halo(root, "Linterna", 0.75f);
                mv.Lamp.enabled = t.localScale.x > MinerScale * 0.4f;
                mv.Lamp.transform.position = mv.Model.LampWorldPos - Cam.transform.forward * 0.3f;
                IslandFxKit.Face(mv.Lamp.transform, Cam);
                IslandFxKit.Tint(mv.Lamp, new Color(1f, 0.92f, 0.65f, 0.7f * Mathf.Clamp01((night - 0.3f) / 0.4f)));
            }
            else if (mv.Lamp != null) mv.Lamp.enabled = false;
        }

        /// <summary>El minero festeja (saluda) un rato.</summary>
        public void Cheer(Miner m, float amount)
        {
            MinerView mv;
            if (miners.TryGetValue(m.Id, out mv)) mv.Celebrate = Mathf.Max(mv.Celebrate, amount);
        }

        void Footprint(Vector3 p, float face)
        {
            Step s;
            if (steps.Count >= 40) { s = steps[0]; steps.RemoveAt(0); }
            else
            {
                var tr = IslandArt.Blob(root, 0.09f, 0.4f);
                s = new Step { T = tr, R = tr.GetComponent<MeshRenderer>() };
            }
            s.T.position = new Vector3(p.x, 0.032f, p.z);
            s.T.localScale = new Vector3(0.08f, 1f, 0.13f);
            s.T.rotation = Quaternion.Euler(0f, face * Mathf.Rad2Deg, 0f);
            s.Age = 0f;
            steps.Add(s);
        }

        void UpdateSteps(float dt)
        {
            var mpb = new MaterialPropertyBlock();
            foreach (var s in steps)
            {
                s.Age += dt;
                float a = Mathf.Clamp01(1f - s.Age / 4f) * 0.35f;
                mpb.SetColor("_Color", new Color(0.35f, 0.24f, 0.12f, a));
                s.R.SetPropertyBlock(mpb);
            }
        }

        // ------------------------------------------------------------ nivel, amistad y suerte
        void OnMinerLevel(Miner m)
        {
            MinerView mv;
            if (!miners.TryGetValue(m.Id, out mv)) return;
            Vector3 p = mv.Model.transform.position;
            FxApi.Play("levelup_aura", p, new Color(1f, 0.92f, 0.5f), 1.2f);
            FxApi.Play("rays", p + Vector3.up * MH(1.2f), new Color(1f, 0.9f, 0.5f), 0.6f);
            Ui.Popup(p + Vector3.up * MH(2.6f), Island.FirstName(m) + Loc.T(" nivel ") + m.Level, new Color(0.7f, 1f, 0.5f), 28);
            Sfx.Play("powerup", -8f, 1.1f);
            mv.Celebrate = 1f;
            Juice.Punch(mv.Model.transform, 0.25f, 0.3f);
            RefreshMinerLook(mv);
            // cambio de etapa (niveles 4 y 8): equipo nuevo = momento propio, no un numero mas (PLAN_CARTAS §4)
            if (Island.Tier(m) > (m.Level - 1 >= 8 ? 3 : m.Level - 1 >= 4 ? 2 : 1))
            {
                FxApi.Play("unlock_burst", p + Vector3.up * MH(1.0f), new Color(1f, 0.9f, 0.45f), 1.6f);
                FxApi.Play("levelup_aura", p, new Color(1f, 0.95f, 0.7f), 2.2f);
                Juice.SlowMo(0.3f, 0.35f);
                Sfx.PlayLater("fanfare_short", 0.1f, -6f);
                Mineros.Fx.Haptics.Success();
                Ui.Toast(Loc.T("¡") + Island.FirstName(m) + Loc.T(" estrena equipo!"), new Color(1f, 0.85f, 0.4f), null, true);
            }
        }

        void OnFriends(Miner a, Miner b)
        {
            MinerView va, vb;
            if (!miners.TryGetValue(a.Id, out va) || !miners.TryGetValue(b.Id, out vb)) return;
            Vector3 mid = (va.Model.transform.position + vb.Model.transform.position) * 0.5f;
            va.Celebrate = vb.Celebrate = 1f;
            Ui.FloatText(mid + Vector3.up * MH(2.2f), "♥", new Color(1f, 0.4f, 0.55f), 70);
            Ui.Popup(mid + Vector3.up * 3f, Loc.T("¡") + Island.FirstName(a) + Loc.T(" y ") + Island.FirstName(b) + Loc.T(" son amigos!"), new Color(1f, 0.6f, 0.75f), 26);
            FxApi.Play("hit_spark", mid + Vector3.up * MH(1.4f), new Color(1f, 0.85f, 0.5f), 0.9f);   // choque de manos
            Sfx.Play("jingle_small", -8f, 1.15f);
        }

        void OnLucky(Miner m)
        {
            MinerView mv;
            if (!miners.TryGetValue(m.Id, out mv)) return;
            Vector3 p = mv.Model.transform.position + Vector3.up * MH(2f);
            Ui.Popup(p + Vector3.up * 0.4f, Loc.T("¡Una gema!"), new Color(0.85f, 0.6f, 1f), 26);
            Ui.FlyGemsFromWorld(p, 1);
            FxApi.Play("gem_sparkle", p, new Color(0.8f, 0.55f, 1f), 1f);
        }

        void RefreshMinerLook(MinerView mv)
        {
            int smith = Mathf.Clamp(Isl.Level(BKind.Smithy) / 2, 0, 5);
            int rank = Mathf.Clamp(Mathf.Max(smith, (mv.M.Level - 1) / 3), 0, 5);
            if (mv.Model.SetRig(RigFor(mv.M))) mv.Gear = -1;   // otra etapa con modelo propio: se rearmo
            mv.Model.SetLook(HelmetOf(mv.M), new Tool(mv.M.Char == 0 ? 1 : 0, rank));   // el de piedra usa mazo
            mv.Model.SetSpecialist(IslandArt.SpecColor(mv.M.Char));
            int gear = mv.M.Char * 10 + Island.Tier(mv.M);
            // el modelo con esqueleto ya trae su equipo dibujado (mochila, cristales, corona)
            // el minero de siempre: el oficio lo dicen el pico y la mochila del color de su mineral (sin equipo extra)
            if (gear != mv.Gear) mv.Gear = gear;
        }

        /// <summary>Modelo con esqueleto del especialista en su etapa (Resources/MinerRig/mr_oro_2); si no esta, el de siempre.</summary>
        static string RigFor(Miner m)
        {
            return Mineros.Miners.RiggedMiner.NameFor(m.Char, Island.Tier(m));
        }

        // ------------------------------------------------------------ barco con candidatos
        void OnRecruits()
        {
            Ambient.RecruitCome(() => Ui.ShowRecruits());
            Sfx.Play("bell", -6f, 1.1f);
        }

        /// <summary>El jugador eligio: baja del barco en la orilla y corre a la plaza.</summary>
        public Miner Recruit(int i)
        {
            var m = Isl.ChooseRecruit(i);
            if (m == null) return null;
            Vector3 beach = Ambient.RecruitShore;
            m.X = beach.x; m.Z = beach.z;
            MinerView mv;
            if (miners.TryGetValue(m.Id, out mv)) mv.Model.transform.localPosition = new Vector3(m.X, m.Y, m.Z);
            if (Isl.Recruits.Count == 0) Ambient.RecruitGo();
            Save();
            return m;
        }
    }
}
