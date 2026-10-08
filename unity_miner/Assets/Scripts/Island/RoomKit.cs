using System.Collections.Generic;
using Mineros.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mineros.IslandView
{
    /// <summary>
    /// Kit de habitaciones modeladas (TRELLIS.2): Resources/RoomKit/&lt;tema&gt;_&lt;pieza&gt;.json + .png, con tema = el mineral
    /// (piedra, cobre, hierro, carbon, oro, cristal, diamante, raros, maestro) y pieza = pared, ventana, techo, marco
    /// (pared con el hueco de la puerta) u hoja (la hoja de la puerta, pivote en la bisagra). Lo convierte
    /// unity_tools/blender_miner/room_kit.py. Cada pieza se estira para calzar justo en su lugar del modulo; lo que falte
    /// se sigue dibujando por codigo, asi el kit puede llegar de a partes.
    /// </summary>
    public static class RoomKit
    {
        /// <summary>Pieza ubicada: archivo y matriz (coordenadas del piso del Complejo).</summary>
        public struct Slot { public string File; public Matrix4x4 M; public Module Mod; }

        static readonly string[] Themes = { "piedra", "cobre", "hierro", "carbon", "oro", "cristal", "diamante", "raros", "maestro" };
        static readonly Dictionary<string, bool> has = new Dictionary<string, bool>();

        /// <summary>
        /// El frente (la cara de afuera) de las piezas del kit: en Blender miran a -Y (asi se las pide en
        /// PROMPT_LAPTOP_3D) y el conversor lo pasa a +Z de Unity.
        /// </summary>
        static readonly Vector3 KitFront = Vector3.forward;

        /// <summary>Tema de un modulo: el dormitorio, su especialista; el resto, el oficio que mas se le parece.</summary>
        public static string ThemeOf(Module m)
        {
            int t;
            switch (m.Kind)
            {
                case ModKind.Dorm: t = m.Ch; break;
                case ModKind.Central: case ModKind.Classroom: case ModKind.Trophy: case ModKind.Clock: t = 8; break;
                case ModKind.Crusher: case ModKind.Storage: case ModKind.Unload: case ModKind.Corridor: case ModKind.Stairs: case ModKind.Bath: t = 0; break;
                case ModKind.Smelter: case ModKind.PortOven: case ModKind.Splitter: t = 1; break;
                case ModKind.Tools: case ModKind.Drill: case ModKind.Magnet: t = 2; break;
                case ModKind.Generator: t = 3; break;
                case ModKind.Treasury: case ModKind.Vault: case ModKind.Mess: case ModKind.PartyBell: t = 4; break;
                case ModKind.Lab: case ModKind.PortLab: case ModKind.Observatory: t = 5; break;
                case ModKind.Polisher: t = 6; break;
                case ModKind.Secret: case ModKind.Experimental: case ModKind.Dovecote: t = 7; break;
                default: t = -1; break;
            }
            return t >= 0 && t < Themes.Length ? Themes[t] : null;
        }

        /// <summary>Archivo de la pieza del tema si esta en Resources (si no, null y va la de codigo).</summary>
        public static string Piece(Module m, string piece)
        {
            string th = ThemeOf(m);
            if (th == null) return null;
            string f = "RoomKit/" + th + "_" + piece;
            bool ok;
            if (!has.TryGetValue(f, out ok)) { ok = Resources.Load<TextAsset>(f) != null; has[f] = ok; }
            return ok ? f : null;
        }

        /// <summary>Archivo de una pieza comun a todos los temas (Resources/RoomKit/comun_&lt;pieza&gt;), o null.</summary>
        public static string Common(string piece)
        {
            string f = "RoomKit/comun_" + piece;
            bool ok;
            if (!has.TryGetValue(f, out ok)) { ok = Resources.Load<TextAsset>(f) != null; has[f] = ok; }
            return ok ? f : null;
        }

        /// <summary>
        /// Ubica una pieza SIN estirarla (escala 1): el origen de Blender (centro de la celda, borde de la pared) va a
        /// `pos`, con los ejes de Blender sobre los del piso (X = X, Y = Z). Para el techo comun y los remates.
        /// </summary>
        public static bool Place(List<Slot> into, string file, Vector3 pos, Module mod = null)
        {
            if (into == null || file == null) return false;
            Material mat;
            if (IslandArt.TripoModel(file, out mat) == null) return false;
            into.Add(new Slot { File = file, M = Matrix4x4.TRS(pos, Quaternion.Euler(0f, 180f, 0f), Vector3.one), Mod = mod });
            return true;
        }

        /// <summary>
        /// Ubica una pieza para que su caja ocupe `size` (ancho a lo largo de la pared, alto, grosor) con la base en
        /// `baseCenter` y el frente hacia `outward`. Alto o grosor en 0: en proporcion al ancho.
        /// </summary>
        public static bool Fit(List<Slot> into, string file, Vector3 baseCenter, Vector3 outward, Vector3 size, Module mod = null)
        {
            if (into == null || file == null) return false;
            Material mat;
            var mesh = IslandArt.TripoModel(file, out mat);
            if (mesh == null) return false;
            var b = mesh.bounds;
            float sx = size.x / Mathf.Max(b.size.x, 1e-3f);
            float sy = size.y > 0f ? size.y / Mathf.Max(b.size.y, 1e-3f) : sx;
            float sz = size.z > 0f ? size.z / Mathf.Max(b.size.z, 1e-3f) : sx;
            var rot = Quaternion.LookRotation(-outward, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(-KitFront, Vector3.up));
            var m = Matrix4x4.TRS(baseCenter, rot, Vector3.one)
                  * Matrix4x4.Scale(new Vector3(sx, sy, sz))
                  * Matrix4x4.Translate(new Vector3(-b.center.x, -b.min.y, -b.center.z));
            into.Add(new Slot { File = file, M = m, Mod = mod });
            return true;
        }

        /// <summary>Junta las piezas de un piso: una malla por archivo (cada una con su textura).</summary>
        public static void Bake(List<Slot> slots, Transform parent, string name, bool shadows)
        {
            if (slots == null || slots.Count == 0) return;
            // por material: con el atlas del kit (IslandArt) todas las piezas del piso comparten uno = una sola malla
            var byMat = new Dictionary<Material, List<CombineInstance>>();
            foreach (var s in slots)
            {
                Material mat;
                var mesh = IslandArt.TripoModel(s.File, out mat);
                if (mesh == null || mat == null) continue;
                List<CombineInstance> l;
                if (!byMat.TryGetValue(mat, out l)) { l = new List<CombineInstance>(); byMat[mat] = l; }
                l.Add(new CombineInstance { mesh = mesh, transform = s.M });
            }
            foreach (var kv in byMat)
            {
                Material mat = kv.Key;
                var m = new Mesh { name = name + "_" + mat.name };
                int verts = 0;
                foreach (var ci in kv.Value) verts += ci.mesh.vertexCount;
                m.indexFormat = verts > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                m.CombineMeshes(kv.Value.ToArray(), true, true);
                m.RecalculateBounds();
                m.UploadMeshData(true);
                IslandArt.MakeRenderer(parent, name + "Kit", m, new[] { mat }, shadows);
            }
        }
    }
}
