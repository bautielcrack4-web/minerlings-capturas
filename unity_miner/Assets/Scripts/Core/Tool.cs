namespace Mineros.Core
{
    /// <summary>Herramienta: K = tipo (0 Pico, 1 Mazo), R = rango (0..5 = D..SS). Los huecos vacios son null.</summary>
    public sealed class Tool
    {
        public int K;
        public int R;

        public Tool(int k, int r) { K = k; R = r; }

        public Tool Clone() { return new Tool(K, R); }

        public string Key { get { return K + "_" + R; } }

        public override bool Equals(object obj)
        {
            Tool o = obj as Tool;
            return o != null && o.K == K && o.R == R;
        }

        public override int GetHashCode() { return K * 31 + R; }

        public override string ToString() { return "Tool(" + K + "," + R + ")"; }
    }

    public enum SlotKind { Inv, Equip }

    /// <summary>Referencia a un hueco: equivale a {"c": "inv"|"eq", "i": n} de Godot.</summary>
    public struct SlotRef
    {
        public SlotKind C;
        public int I;
        public SlotRef(SlotKind c, int i) { C = c; I = i; }
    }

    public enum MoveResult { None, Merge, Swap }
}
