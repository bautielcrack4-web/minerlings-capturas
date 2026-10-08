using System;

namespace Mineros.Core
{
    /// <summary>Herramientas: inventario, equipo, fusion y cofre (gacha).</summary>
    public sealed partial class GameState
    {
        public string ToolName(Tool t) { return t == null ? "" : Content.ToolNames[t.K]; }

        public int FreeInvSlot()
        {
            for (int i = 0; i < Balance.InvSize; i++)
                if (Inv[i] == null) return i;
            return -1;
        }

        public int GachaCost() { return 100; }

        /// <summary>Abre un cofre de herramientas (100 gemas). Devuelve la herramienta o null si falto algo.</summary>
        public Tool Gacha()
        {
            if (Gems < GachaCost())
            {
                EmitToast("Faltan gemas");
                return null;
            }
            int slot = FreeInvSlot();
            if (slot < 0)
            {
                EmitToast("Inventario lleno");
                return null;
            }
            Gems -= GachaCost();
            double roll = rng.NextDouble();
            int r = 0;
            if (roll < 0.05) r = 2;
            else if (roll < 0.30) r = 1;
            Tool t = new Tool(rng.Next(2), r);
            Inv[slot] = t;
            Discover(t);
            AddDaily("gacha", 1);
            AddStat("gacha");
            CheckUnlocks();
            Changed?.Invoke();
            return t;
        }

        void Discover(Tool t)
        {
            string key = t.Key;
            if (!Collection.Contains(key)) Collection.Add(key);
        }

        public bool SameTool(Tool a, Tool b)
        {
            return a != null && b != null && a.K == b.K && a.R == b.R && a.R < Content.Ranks.Length - 1;
        }

        public int EquippedCount()
        {
            int n = 0;
            for (int i = 0; i < Equip.Length; i++)
                if (Equip[i] != null) n++;
            return n;
        }

        /// <summary>Mueve o fusiona entre contenedores (inventario / equipo).</summary>
        public MoveResult MoveTool(SlotRef src, SlotRef dst)
        {
            if (src.C == dst.C && src.I == dst.I) return MoveResult.None;
            if (!ValidSlot(src) || !ValidSlot(dst)) return MoveResult.None;
            Tool a = GetSlot(src);
            Tool b = GetSlot(dst);
            if (a == null) return MoveResult.None;
            if (SameTool(a, b))
            {
                Tool nt = new Tool(a.K, a.R + 1);
                SetSlot(dst, nt);
                SetSlot(src, null);
                if (EquippedCount() == 0)
                {
                    SetSlot(src, nt);
                    SetSlot(dst, null);
                }
                Discover(nt);
                AddDaily("merges", 1);
                AddStat("merges");
                AfterTools();
                return MoveResult.Merge;
            }
            // no se puede dejar sin herramientas equipadas
            if (src.C == SlotKind.Equip && dst.C == SlotKind.Inv && b == null && EquippedCount() <= 1)
            {
                EmitToast("Necesitas al menos un minero");
                return MoveResult.None;
            }
            SetSlot(dst, a);
            SetSlot(src, b);
            AfterTools();
            return MoveResult.Swap;
        }

        public void Unequip(int i)
        {
            if (i < 0 || i >= Equip.Length || Equip[i] == null) return;
            if (EquippedCount() <= 1)
            {
                EmitToast("Necesitas al menos un minero");
                return;
            }
            int s = FreeInvSlot();
            if (s < 0)
            {
                EmitToast("Inventario lleno");
                return;
            }
            Inv[s] = Equip[i];
            Equip[i] = null;
            AfterTools();
        }

        static bool ValidSlot(SlotRef d)
        {
            return d.I >= 0 && d.I < (d.C == SlotKind.Inv ? Balance.InvSize : Balance.EquipSlots);
        }

        Tool GetSlot(SlotRef d) { return d.C == SlotKind.Inv ? Inv[d.I] : Equip[d.I]; }

        void SetSlot(SlotRef d, Tool v)
        {
            if (d.C == SlotKind.Inv) Inv[d.I] = v;
            else Equip[d.I] = v;
        }

        void AfterTools()
        {
            CheckUnlocks();
            Changed?.Invoke();
            EquipChanged?.Invoke();
            SaveGame();
        }
    }
}
