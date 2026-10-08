using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using Mineros.World;
using UnityEngine;

namespace Mineros.IslandView
{
    /// <summary>
    /// Puertas a la calle del Complejo: una hoja con bisagra que se abre hacia adentro cuando un minero se acerca y se
    /// cierra con un rebotecito cuando pasa. La hoja es por codigo; si existe la pieza separada (Resources/Island/
    /// cx_puerta_hoja) o la del kit del tema (RoomKit/<tema>_hoja, la que separa PartCrafter) se usa esa.
    /// </summary>
    public sealed partial class IslandGame
    {
        sealed class DoorView
        {
            public Transform Pivot;
            public Vector3 World;      // punto medio del hueco (para medir la cercania de los mineros)
            public float Sign;         // sentido de giro para abrir hacia adentro
            public float Open, Vel;    // 0 cerrada .. 1 abierta (resorte)
            public bool Want;
            public float SoundT;
            public Quaternion Base;    // hoja cerrada, a lo largo del hueco
        }

        readonly List<DoorView> cxDoors = new List<DoorView>();
        Mesh doorLeafMesh;
        Material[] doorLeafMats;
        const float DoorAngle = 105f, DoorNear = 1.05f;

        /// <summary>Hojas de las puertas exteriores del piso de abajo (se arman con el horneado del Complejo).</summary>
        void BuildDoors()
        {
            cxDoors.Clear();
            var walls = cxWalls[0];
            if (walls == null) return;
            foreach (var m in Isl.Modules)
            {
                int side = IslandArt.DoorSide(Isl, m);
                if (side < 0) continue;
                Vector3 hinge, along, inward;
                IslandArt.DoorFrame(m, side, out hinge, out along, out inward);
                var pivot = new GameObject("Puerta" + m.Id).transform;
                pivot.SetParent(walls.transform, false);   // baja con las paredes en el Modo Cuartel
                pivot.localPosition = hinge;
                // giro solo en Y (FromToRotation a 180 grados puede voltear la hoja sobre otro eje)
                var baseRot = Quaternion.AngleAxis(-Mathf.Atan2(along.z, along.x) * Mathf.Rad2Deg, Vector3.up);
                pivot.localRotation = baseRot;
                MakeDoorLeaf(pivot, m);
                // sentido que lleva la punta de la hoja hacia adentro
                float sign = Vector3.Dot(Quaternion.AngleAxis(90f, Vector3.up) * baseRot * Vector3.right, inward) > 0f ? 1f : -1f;
                // en las coordenadas de los mineros (hijos de root)
                var w = root.InverseTransformPoint(cxFloor[0].TransformPoint(hinge + along * (IslandArt.DoorOpen * 0.5f)));
                cxDoors.Add(new DoorView { Pivot = pivot, World = w, Sign = sign, Base = baseRot });
            }
        }

        /// <summary>Hoja del tema del modulo (kit), o la suelta (cx_puerta_hoja), o la de codigo.</summary>
        void MakeDoorLeaf(Transform pivot, Module m)
        {
            string file = RoomKit.Piece(m, "hoja") ?? "cx_puerta_hoja";
            Material pm;
            var piece = IslandArt.TripoModel(file, out pm);
            if (piece != null)
            {
                // pieza externa: se estira al hueco (ancho a lo largo de X desde la bisagra, alto en Y, grosor en proporcion)
                var r = IslandArt.MakeRenderer(pivot, "Hoja", piece, new[] { pm }, true);
                var b = piece.bounds;
                float sx = (IslandArt.DoorOpen - 0.06f) / Mathf.Max(b.size.x, 1e-3f), sy = IslandArt.DoorHgt / Mathf.Max(b.size.y, 1e-3f);
                r.transform.localScale = new Vector3(sx, sy, sx);
                r.transform.localPosition = new Vector3(-b.min.x * sx, -b.min.y * sy, -b.center.z * sx);
                return;
            }
            if (doorLeafMesh == null)
            {
                var mb = new MeshBuilder();
                IslandArt.DoorLeaf(mb);
                var tmp = IslandArt.Bake(mb, pivot, "Hoja", true);
                doorLeafMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                doorLeafMats = tmp.sharedMaterials;
                return;
            }
            IslandArt.MakeRenderer(pivot, "Hoja", doorLeafMesh, doorLeafMats, true);
        }

        void UpdateDoors(float dt)
        {
            if (cxDoors.Count == 0) return;
            bool hear = ComplexMode || Cam.orthographicSize < 11f;
            foreach (var d in cxDoors)
            {
                if (d.Pivot == null) continue;
                bool want = false;
                foreach (var mv in miners.Values)
                {
                    var m = mv.M;
                    if (m.Y > 0.6f) continue;
                    float dx = m.X - d.World.x, dz = m.Z - d.World.z;
                    if (dx * dx + dz * dz < DoorNear * DoorNear) { want = true; break; }
                }
                if (want && !d.Want && d.Open < 0.3f && d.SoundT <= 0f && hear)
                {
                    Sfx.Play("creak", -16f, Random.Range(0.95f, 1.2f));
                    d.SoundT = 1.2f;
                }
                d.Want = want;
                d.SoundT -= dt;
                // resorte: abre rapido, cierra mas lento y rebota un poco al encajar
                float target = want ? 1f : 0f;
                float k = want ? 140f : 70f, damp = want ? 18f : 9f;
                // pasos cortos: con cuadros largos (telefono lento) el resorte no se dispara
                int n = Mathf.Clamp(Mathf.CeilToInt(dt / 0.01f), 1, 20);
                float h = Mathf.Min(dt, 0.2f) / n;
                bool slam = false;
                for (int i = 0; i < n; i++)
                {
                    d.Vel += ((target - d.Open) * k - d.Vel * damp) * h;
                    d.Open += d.Vel * h;
                    if (d.Open < 0f)
                    {
                        if (d.Vel < -0.6f) slam = true;
                        d.Open = -d.Open * 0.3f; d.Vel = -d.Vel * 0.3f;   // golpea el marco y rebota
                    }
                }
                if (slam && hear && d.SoundT <= 0f) { Sfx.Play("cx_m_wood", -20f, 1.25f); d.SoundT = 0.4f; }
                d.Pivot.localRotation = Quaternion.AngleAxis(d.Sign * DoorAngle * d.Open, Vector3.up) * d.Base;
            }
        }

        public int DebugDoorCount { get { return cxDoors.Count; } }

        /// <summary>Para capturas: punto del hueco de la puerta i (coordenadas de los mineros).</summary>
        public bool DebugDoor(int i, out Vector3 at)
        {
            at = i < cxDoors.Count ? cxDoors[i].World : Vector3.zero;
            return i < cxDoors.Count;
        }
    }
}
