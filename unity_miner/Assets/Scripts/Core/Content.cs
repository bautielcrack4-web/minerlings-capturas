using System.Collections.Generic;

namespace Mineros.Core
{
    // ---- Tipos de contenido (datos inmutables) -------------------------------------------

    /// <summary>Casco de color. ColorHex es RGB sin '#': en Unity se convierte con ColorUtility.TryParseHtmlString.</summary>
    public sealed class Skin
    {
        public readonly string Name;
        public readonly string ColorHex;
        public readonly int Cost;
        public Skin(string name, string colorHex, int cost) { Name = name; ColorHex = colorHex; Cost = cost; }
    }

    /// <summary>Nodo del arbol de Esencia (mejora permanente).</summary>
    public sealed class EssenceNode
    {
        public readonly string Id, Title, Desc, Icon;
        public readonly int Max, BaseCost;
        public readonly double CostMult;
        public EssenceNode(string id, string title, string desc, int max, int baseCost, double costMult, string icon)
        { Id = id; Title = title; Desc = desc; Max = max; BaseCost = baseCost; CostMult = costMult; Icon = icon; }
    }

    /// <summary>Logro de por vida: se reclama una vez cuando el contador Stat llega a Goal.</summary>
    public sealed class Achievement
    {
        public readonly string Id, Title, Desc, Stat, Icon;
        public readonly long Goal;
        public readonly int Gems;
        public Achievement(string id, string title, string desc, string stat, long goal, int gems, string icon)
        { Id = id; Title = title; Desc = desc; Stat = stat; Goal = goal; Gems = gems; Icon = icon; }
    }

    public enum GoalType { Level, Stage, Stat, Equipped, Collection }

    /// <summary>Definicion de una meta del rastreador (curada o generada).</summary>
    public sealed class GoalDef
    {
        public readonly string Id, Text, Icon, Arg;
        public readonly GoalType Type;
        public readonly long Target;
        public readonly int Gems;
        public GoalDef(string id, string text, string icon, GoalType type, string arg, long target, int gems)
        { Id = id; Text = text; Icon = icon; Type = type; Arg = arg; Target = target; Gems = gems; }
    }

    /// <summary>Estado de la meta actual (equivale al Dictionary de current_goal()).</summary>
    public sealed class Goal
    {
        public string Id, Text, Icon;
        public long Progress, Target;
        public int Gems;
        public bool Done;
    }

    /// <summary>Mision diaria: cuenta contra el contador diario Stat.</summary>
    public sealed class Mission
    {
        public readonly string Id, Stat, Title, Desc;
        public readonly int Goal, Gems;
        public Mission(string id, string stat, int goal, string title, string desc, int gems)
        { Id = id; Stat = stat; Goal = goal; Title = title; Desc = desc; Gems = gems; }
    }

    /// <summary>Tablas de contenido y constantes de listas. Textos en ASCII, identicos al juego Godot.</summary>
    public static class Content
    {
        public static readonly string[] Ranks = { "D", "C", "B", "A", "S", "SS" };
        public static readonly string[] ToolNames = { "Pico", "Mazo" };
        public static readonly string[] BiomeNames = { "Pradera", "Desierto", "Cueva", "Volcan" };
        public static readonly int[] DailyGems = { 50, 100, 200, 300, 500, 800, 1200 };

        /// <summary>Funciones que se desbloquean de a una (el resto es nucleo y siempre esta activo).</summary>
        public static readonly string[] Features =
            { "speed", "money", "boost", "tools", "missions", "play", "shop", "skin", "piggy", "auto", "rebirth", "achievements" };

        /// <summary>Pares (evento, peso) para el sorteo de eventos.</summary>
        public static readonly string[] EventKinds = { "gold_rush", "frenzy", "meteor", "chest" };
        public static readonly int[] EventWeights = { 35, 30, 20, 15 };

        public static double EventTime(string kind)
        {
            switch (kind)
            {
                case "gold_rush": return 20.0;
                case "frenzy": return 15.0;
                case "meteor": return 12.0;
                case "chest": return 30.0;
            }
            return -1.0;
        }

        public static bool IsEventKind(string kind) { return EventTime(kind) > 0.0; }

        /// <summary>Nombre visible de las mejoras (para metas generadas).</summary>
        public static string KindName(string kind)
        {
            switch (kind)
            {
                case "power": return "Fuerza";
                case "speed": return "Velocidad";
                case "money": return "Oro";
            }
            return null;
        }

        public static readonly string[] UpgradeKinds = { "power", "speed", "money" };

        public static EssenceNode FindNode(string id)
        {
            for (int i = 0; i < EssenceNodes.Length; i++)
                if (EssenceNodes[i].Id == id) return EssenceNodes[i];
            return null;
        }

        public static readonly Skin[] Skins =
        {
            new Skin("Clasico", "f5c531", 0),
            new Skin("Rubi", "e5484d", 100),
            new Skin("Cielo", "4aa3f0", 150),
            new Skin("Musgo", "5fbf5a", 200),
            new Skin("Amatista", "a36be8", 300),
            new Skin("Oro puro", "ffd84a", 1000),
        };

        public static readonly EssenceNode[] EssenceNodes =
        {
            new EssenceNode("power", "Fuerza ancestral", "+25% de dano por nivel", 25, 1, 1.40, "power"),
            new EssenceNode("gold", "Oro ancestral", "+25% de oro por nivel", 25, 1, 1.40, "money"),
            new EssenceNode("speed", "Manos rapidas", "+5% de velocidad por nivel", 20, 2, 1.45, "speed"),
            new EssenceNode("crit", "Ojo critico", "+1.5% de prob. critica", 10, 3, 1.50, "star"),
            new EssenceNode("brutal", "Golpe brutal", "+25% de dano critico", 20, 2, 1.45, "hammer"),
            new EssenceNode("start", "Inicio rapido", "+1 etapa de inicio al renacer", 8, 3, 1.70, "rebirth"),
            new EssenceNode("night", "Minero nocturno", "+10% offline y +1 h de tope", 5, 4, 1.80, "clock"),
            new EssenceNode("luck", "Suerte", "+10% de eventos", 5, 3, 1.60, "gem"),
            new EssenceNode("boss", "Gemas de jefe", "+2 gemas por jefe", 10, 3, 1.50, "boss"),
            new EssenceNode("chest", "Cofre generoso", "+25% gemas de Roca Cofre", 8, 3, 1.50, "chest"),
        };

        public static readonly Achievement[] Achievements =
        {
            new Achievement("rocks_100", "Primeros golpes", "Rompe 100 rocas", "rocks", 100L, 10, "pick"),
            new Achievement("rocks_1k", "Picapedrero", "Rompe 1.000 rocas", "rocks", 1000L, 20, "pick"),
            new Achievement("rocks_5k", "Cantero", "Rompe 5.000 rocas", "rocks", 5000L, 40, "pick"),
            new Achievement("rocks_20k", "Terremoto", "Rompe 20.000 rocas", "rocks", 20000L, 80, "pick"),
            new Achievement("rocks_50k", "Montanas menos", "Rompe 50.000 rocas", "rocks", 50000L, 150, "pick"),
            new Achievement("gold_1k", "Primeras monedas", "Junta 1.000 de oro", "gold", 1000L, 10, "coin"),
            new Achievement("gold_1m", "Millonario", "Junta 1 millon de oro", "gold", 1000000L, 30, "coin"),
            new Achievement("gold_1b", "Magnate", "Junta 1.000 millones de oro", "gold", 1000000000L, 60, "coin"),
            new Achievement("gold_1t", "Dragon del tesoro", "Junta 1 billon de oro", "gold", 1000000000000L, 120, "coin"),
            new Achievement("stage_5", "Nuevo mundo", "Llega a la Etapa 2-1", "max_stage", 5L, 15, "star"),
            new Achievement("stage_10", "Camino al fondo", "Llega a la Etapa 3-1", "max_stage", 10L, 25, "star"),
            new Achievement("stage_20", "Explorador", "Llega a la Etapa 5-1", "max_stage", 20L, 50, "star"),
            new Achievement("stage_45", "Leyenda del canon", "Llega a la Etapa 10-1", "max_stage", 45L, 120, "star"),
            new Achievement("boss_1", "Rompegeodas", "Vence 1 jefe", "bosses", 1L, 10, "boss"),
            new Achievement("boss_10", "Cazajefes", "Vence 10 jefes", "bosses", 10L, 40, "boss"),
            new Achievement("boss_50", "Azote de geodas", "Vence 50 jefes", "bosses", 50L, 100, "boss"),
            new Achievement("rebirth_1", "Otra vez", "Renace 1 vez", "rebirths", 1L, 40, "rebirth"),
            new Achievement("rebirth_5", "Ciclo eterno", "Renace 5 veces", "rebirths", 5L, 100, "rebirth"),
            new Achievement("rebirth_20", "Fenix minero", "Renace 20 veces", "rebirths", 20L, 300, "rebirth"),
            new Achievement("merge_5", "Herrero", "Fusiona 5 herramientas", "merges", 5L, 25, "hammer"),
            new Achievement("merge_25", "Maestro forjador", "Fusiona 25 herramientas", "merges", 25L, 80, "hammer"),
            new Achievement("coll_4", "Coleccionista", "Descubre 4 herramientas", "collection", 4L, 15, "chest"),
            new Achievement("coll_8", "Arsenal", "Descubre 8 herramientas", "collection", 8L, 40, "chest"),
            new Achievement("coll_12", "Coleccion completa", "Descubre las 12 herramientas", "collection", 12L, 100, "chest"),
            new Achievement("dig_1", "Excavador", "Gana 1 excavacion", "plays_won", 1L, 15, "trophy"),
            new Achievement("dig_10", "Topo experto", "Gana 10 excavaciones", "plays_won", 10L, 60, "trophy"),
            new Achievement("crit_100", "Buen ojo", "Logra 100 criticos", "crits", 100L, 20, "power"),
            new Achievement("crit_1k", "Punteria letal", "Logra 1.000 criticos", "crits", 1000L, 60, "power"),
            new Achievement("events_10", "Siempre atento", "Vive 10 eventos", "events", 10L, 40, "clock"),
        };

        public static readonly GoalDef[] Goals =
        {
            new GoalDef("g01", "Sube Fuerza a nivel 3", "power", GoalType.Level, "power", 3L, 5),
            new GoalDef("g02", "Rompe 12 rocas", "pick", GoalType.Stat, "rocks", 12L, 5),
            new GoalDef("g03", "Sube Velocidad a nivel 3", "speed", GoalType.Level, "speed", 3L, 6),
            new GoalDef("g04", "Supera la Etapa 1-1", "star", GoalType.Stage, "", 1L, 8),
            new GoalDef("g05", "Sube Fuerza a nivel 10", "power", GoalType.Level, "power", 10L, 8),
            new GoalDef("g06", "Supera la Etapa 1-2", "star", GoalType.Stage, "", 2L, 10),
            new GoalDef("g07", "Activa el x3 Oro", "money", GoalType.Stat, "boosts", 1L, 10),
            new GoalDef("g08", "Sube Oro a nivel 5", "money", GoalType.Level, "money", 5L, 8),
            new GoalDef("g09", "Vive un evento", "clock", GoalType.Stat, "events", 1L, 12),
            new GoalDef("g10", "Supera la Etapa 1-3", "star", GoalType.Stage, "", 3L, 12),
            new GoalDef("g11", "Abre un cofre de herramientas", "chest", GoalType.Stat, "gacha", 1L, 15),
            new GoalDef("g12", "Equipa 3 mineros", "pick", GoalType.Equipped, "", 3L, 15),
            new GoalDef("g13", "Sube Fuerza a nivel 15", "power", GoalType.Level, "power", 15L, 12),
            new GoalDef("g14", "Supera la Etapa 1-4", "star", GoalType.Stage, "", 4L, 15),
            new GoalDef("g15", "Reclama una mision", "mission", GoalType.Stat, "missions", 1L, 15),
            new GoalDef("g16", "Velocidad a nivel 25 (x1.25)", "speed", GoalType.Level, "speed", 25L, 15),
            new GoalDef("g17", "Vence a la Geoda gigante", "boss", GoalType.Stat, "bosses", 1L, 20),
            new GoalDef("g18", "Juega a Excavar", "hammer", GoalType.Stat, "plays", 1L, 20),
            new GoalDef("g19", "Sube Oro a nivel 20", "money", GoalType.Level, "money", 20L, 18),
            new GoalDef("g20", "Sube Fuerza a nivel 20", "power", GoalType.Level, "power", 20L, 18),
            new GoalDef("g21", "Gana una excavacion", "trophy", GoalType.Stat, "plays_won", 1L, 20),
            new GoalDef("g22", "Supera la Etapa 2-2", "star", GoalType.Stage, "", 6L, 20),
            new GoalDef("g23", "Oro a nivel 25 (x2)", "money", GoalType.Level, "money", 25L, 22),
            new GoalDef("g24", "Fusiona una herramienta", "hammer", GoalType.Stat, "merges", 1L, 18),
            new GoalDef("g25", "Fuerza a nivel 25 (x2)", "power", GoalType.Level, "power", 25L, 22),
            new GoalDef("g26", "Supera la Etapa 2-4", "star", GoalType.Stage, "", 8L, 22),
            new GoalDef("g27", "Descubre 4 herramientas", "chest", GoalType.Collection, "", 4L, 22),
            new GoalDef("g28", "Oro a nivel 50 (x2)", "money", GoalType.Level, "money", 50L, 28),
            new GoalDef("g29", "Vive 5 eventos", "clock", GoalType.Stat, "events", 5L, 28),
            new GoalDef("g30", "Sube Fuerza a nivel 40", "power", GoalType.Level, "power", 40L, 25),
            new GoalDef("g31", "Llega a la Etapa 3-1", "star", GoalType.Stage, "", 10L, 30),
            new GoalDef("g32", "Renace por primera vez", "rebirth", GoalType.Stat, "rebirths", 1L, 40),
            new GoalDef("g33", "Fusiona 3 herramientas", "hammer", GoalType.Stat, "merges", 3L, 25),
            new GoalDef("g34", "Oro a nivel 75 (x2)", "money", GoalType.Level, "money", 75L, 30),
            new GoalDef("g35", "Fuerza a nivel 50 (x2)", "power", GoalType.Level, "power", 50L, 30),
            new GoalDef("g36", "Logra 100 criticos", "power", GoalType.Stat, "crits", 100L, 25),
            new GoalDef("g37", "Llega a la Etapa 3-3", "star", GoalType.Stage, "", 12L, 32),
            new GoalDef("g38", "Vence a 3 jefes", "boss", GoalType.Stat, "bosses", 3L, 32),
            new GoalDef("g39", "Junta 10M de oro", "coin", GoalType.Stat, "gold", 10000000L, 35),
            new GoalDef("g40", "Fuerza a nivel 100 (x2)", "power", GoalType.Level, "power", 100L, 40),
        };

        public static readonly Mission[] Missions =
        {
            new Mission("rocks100", "rocks", 100, "Picar rocas", "Pica 100 rocas", 20),
            new Mission("rocks300", "rocks", 300, "Picar rocas", "Pica 300 rocas", 30),
            new Mission("rocks700", "rocks", 700, "Picar rocas", "Pica 700 rocas", 40),
            new Mission("rocks2000", "rocks", 2000, "Picar rocas", "Pica 2000 rocas", 60),
            new Mission("up10", "upgrades", 10, "Mejorar", "Compra 10 mejoras", 20),
            new Mission("up40", "upgrades", 40, "Mejorar", "Compra 40 mejoras", 40),
            new Mission("stage2", "stages", 2, "Avanzar", "Supera 2 etapas", 30),
            new Mission("play1", "plays", 1, "Excavar", "Juega 1 excavacion", 20),
            new Mission("play3", "plays", 3, "Excavar", "Juega 3 excavaciones", 40),
            new Mission("gacha1", "gacha", 1, "Cofre", "Abre 1 cofre de herramientas", 30),
            new Mission("merge1", "merges", 1, "Fusionar", "Fusiona 1 herramienta", 30),
        };
    }
}
