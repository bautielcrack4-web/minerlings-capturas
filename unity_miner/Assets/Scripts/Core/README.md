# Mineros.Core: logica de economia y meta-progresion (C# puro)

Port 1:1 de `miner_idle/scripts/game_state.gd` (autoload `G`) y `scripts/num.gd` (`Num`).
No depende de `UnityEngine`: compila en Unity 6 (C# 9, .NET Standard 2.1) y en .NET 8 fuera del editor.
Namespace: `Mineros.Core`. Fuente de verdad del balance: `docs/ECONOMY.md` del juego Godot.

## Archivos

| Archivo | Contenido |
|---|---|
| `GameState.cs` | Estado, senales (eventos C#), `Tick`, estadisticas, progreso de etapas, `ResetAll` |
| `Economy.cs` | Formulas: costos, valores de mejoras, vida/oro de rocas, hitos, ingreso por segundo |
| `Meta.cs` | Renacer + Esencia, desbloqueos, metas, logros, offline |
| `Events.cs` | Eventos aleatorios (Fiebre de Oro, Frenesi, Meteoro, Roca Cofre) |
| `Tools.cs` | Inventario, equipo, fusion, cofre de herramientas (gacha) |
| `Daily.cs` | Misiones diarias, pase, premio diario, boost, cofre gratis, alcancia, cascos |
| `Persistence.cs` | `ToDict`/`ApplyDict`/`SaveGame`/`LoadGame` (mismas claves JSON que Godot) |
| `SaveData.cs` | Mini JSON propio (`Json`, `JsonRead`), sin `System.Text.Json` ni `JsonUtility` |
| `Content.cs` | Tablas: cascos, nodos de Esencia, logros, metas, misiones, premios, funciones |
| `Balance.cs` | Constantes de balance (`HP0`, `POWER_GROW`, ... en PascalCase) |
| `BigNum.cs` | `BigNum.Fmt` y `BigNum.TimeHms` (= `Num.fmt` y `Num.time_hms`) |
| `Services.cs` | `IClock`, `SystemClock`, `ISaveStore`, `MemorySaveStore` |
| `Tool.cs` | `Tool`, `SlotRef`, `SlotKind`, `MoveResult` |
| `Mineros.Core.asmdef` | Assembly Definition con `noEngineReferences` (garantiza que nadie meta `UnityEngine` aca) |

## Uso desde Unity

La logica no lee el reloj ni abre archivos: todo se inyecta.

```csharp
using System.IO;
using Mineros.Core;
using UnityEngine;

// Reloj real para el juego (la logica solo conoce IClock).
public sealed class UnityClock : IClock
{
    public double Now() { return System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0; }
    public string Today() { return System.DateTime.Now.ToString("yyyy-MM-dd"); }
}

// Guardado en un archivo de Application.persistentDataPath (o PlayerPrefs, a gusto).
public sealed class FileSaveStore : ISaveStore
{
    readonly string path = Path.Combine(Application.persistentDataPath, "save.json");
    public void Save(string json) { File.WriteAllText(path, json); }
    public string Load() { return File.Exists(path) ? File.ReadAllText(path) : null; }
}
// Variante PlayerPrefs: Save => PlayerPrefs.SetString("save", json); PlayerPrefs.Save();
//                       Load => PlayerPrefs.HasKey("save") ? PlayerPrefs.GetString("save") : null;

public sealed class GameBootstrap : MonoBehaviour
{
    public static GameState G { get; private set; }

    void Awake()
    {
        G = new GameState(new UnityClock(), new System.Random(), new FileSaveStore());
        G.FeatureUnlocked += f => Debug.Log("Nuevo: " + f);   // senales -> eventos C#
        G.Start();                                             // equivale a _ready(): carga la partida
    }

    void Update() { G.Tick(Time.unscaledDeltaTime); }          // equivale a _process(dt)

    void OnApplicationPause(bool paused) { if (paused) G.OnAppPausedOrClosing(); }
    void OnApplicationQuit() { G.OnAppPausedOrClosing(); }
}
```

Notas:

* **`Tick(dt)`** lo llama un solo MonoBehaviour por frame. Hace: alcancia (+1 por minuto), autoguardado cada 5 s,
  compra automatica, eventos aleatorios y, cada 0.25 s, desbloqueos y meta completada.
* **Guardado**: `SaveGame()` escribe en el `ISaveStore`. Se llama solo al superar etapas, renacer, comprar Esencia,
  reclamar cosas, cambiar herramientas y cada 5 s desde `Tick`. En Android/iOS guardar en `OnApplicationPause`.
* **Ganancias offline**: `Start()` -> `LoadGame()` calcula `PendingOffline` / `PendingOfflineTime`; la UI las muestra y
  llama a `CollectOffline(mult)` (mult = 2 si se mira un anuncio, etc.).
* **Migrar partidas de Godot**: el JSON es el mismo (`user://save.json`). Copiar su contenido al `ISaveStore.Load()`
  o llamar `G.ApplyJson(texto)`. Los campos ausentes toman valores por defecto, las partidas viejas migran
  desbloqueos y metas igual que en Godot.
* **Aleatoriedad**: se usa el `System.Random` inyectado (eventos, cofres). Con una semilla fija el resultado es
  determinista.
* **Hilos**: no es thread-safe; usar solo desde el hilo principal.
* El mundo (rocas, jefe, Roca Cofre, Excavar) sigue siendo responsabilidad de la escena: llama a `OnRockBroken(boss)`,
  `ChestOpened()`, `OnPlayFinished(won, stars)`, `AddGold(...)`, `AddStat("crits")`, etc.

## Tabla Godot -> C#

Convencion: `snake_case` -> `PascalCase`; las funciones privadas con `_` pasan a metodos privados (o `internal` si las
usan las pruebas). `Array`/`Dictionary` de Godot -> `List<T>`/`Dictionary<K,V>`/clases tipadas.

### Senales -> eventos

| Godot | C# |
|---|---|
| `changed` | `event Action Changed` |
| `equip_changed` | `event Action EquipChanged` |
| `stage_cleared(world_n, sub_n)` | `event Action<int,int> StageCleared` |
| `boss_needed` | `event Action BossNeeded` |
| `toast(text)` | `event Action<string> Toast` |
| `upgraded(kind)` | `event Action<string> Upgraded` |
| `feature_unlocked(feature)` | `event Action<string> FeatureUnlocked` |
| `milestone_reached(kind, level)` | `event Action<string,int> MilestoneReached` |
| `goal_completed(goal)` | `event Action<Goal> GoalCompleted` |
| `event_started(kind, duration)` | `event Action<string,double> EventStarted` (double, como el float de 64 bits de Godot) |
| `event_ended(kind)` | `event Action<string> EventEnded` |
| `stats_changed` | `event Action StatsChanged` |
| `rebirthed(gain)` | `event Action<int> Rebirthed` |
| `boss_defeated(gems)` | `event Action<int> BossDefeated` |

### Funciones y propiedades

| Godot | C# |
|---|---|
| `_ready()` | `Start()` |
| `_process(dt)` | `Tick(double dt)` |
| `_notification(CLOSE/PAUSED)` | `OnAppPausedOrClosing()` |
| `now()`, `today()` | `Now()`, `Today()` (delegan en `IClock`) |
| `stage_idx/biome/stage_name/stage_label` | `StageIdx/Biome/StageName/StageLabel` |
| `power_value/speed_value/speed_mult_total/money_mult` | `PowerValue/SpeedValue/SpeedMultTotal/MoneyMult` |
| `cost/can_buy/buy/_auto_buy` | `Cost/CanBuy/Buy/AutoBuy(privado)` |
| `rock_hp/rock_base_gold/rock_gold` | `RockHp/RockBaseGold/RockGold` |
| `boost_active/gold_mult_now/activate_boost` | `BoostActive/GoldMultNow/ActivateBoost` |
| `tool_power/tool_interval/tool_base_interval/tool_damage` | `ToolPower/ToolInterval/ToolBaseInterval/ToolDamage` |
| `crit_chance/crit_mult/rocks_needed/miners_dps/income_per_sec` | `CritChance/CritMult/RocksNeeded/MinersDps/IncomePerSec` |
| `milestone_count/milestone_level/is_milestone_level/milestone_mult/next_milestone` | `MilestoneCount/MilestoneLevel/IsMilestoneLevel/MilestoneMult/NextMilestone` |
| `add_gold/add_gems/on_rock_broken/chest_opened/on_play_finished/boss_gems` | `AddGold/AddGems/OnRockBroken/ChestOpened/OnPlayFinished/BossGems` |
| `add_stat/stat/total_rocks` | `AddStat/Stat (long)/TotalRocks (long)` |
| `can_rebirth/essence_gain_preview/rebirth_gain/rebirth_mult/start_stage/do_rebirth` | `CanRebirth/EssenceGainPreview/RebirthGain/RebirthMult/StartStage/DoRebirth` |
| `essence_level/essence_maxed/essence_cost/buy_essence` | `EssenceLevel/EssenceMaxed/EssenceCost/BuyEssence` |
| `active_event/event_time_left/event_gold_mult/event_speed_mult/debug_start_event` | `ActiveEvent/EventTimeLeft/EventGoldMult/EventSpeedMult/DebugStartEvent` |
| `events_enabled` (setter cancela el evento) | `EventsEnabled` |
| `is_unlocked/newly_unlocked/mark_feature_seen/unlock_all` | `IsUnlocked/NewlyUnlocked/MarkFeatureSeen/UnlockAll` |
| `current_goal()` (Dictionary) | `CurrentGoal()` -> `Goal {Id, Text, Icon, Progress, Target, Gems, Done}` |
| `claim_goal` | `ClaimGoal` |
| `achievement_progress/achievement_ready/achievements_ready_count/claim_achievement` | `AchievementProgress/AchievementReady/AchievementsReadyCount/ClaimAchievement` |
| `tool_name/free_inv_slot/gacha_cost/gacha/same_tool/equipped_count/unequip` | `ToolName/FreeInvSlot/GachaCost/Gacha/SameTool/EquippedCount/Unequip` |
| `move_tool({"c","i"}, {"c","i"})` -> `"merge"`/`"swap"`/`""` | `MoveTool(SlotRef, SlotRef)` -> `MoveResult.Merge/Swap/None` |
| `add_daily/mission_progress/mission_ready/missions_ready_count/claim_mission` | `AddDaily/MissionProgress/MissionReady/MissionsReadyCount/ClaimMission(Mission)` |
| `pass_level/pass_reward/add_pass_xp` | `PassLevel/PassReward/AddPassXp` |
| `login_available/claim_login` | `LoginAvailable/ClaimLogin` |
| `free_chest_ready/claim_free_chest/collect_piggy/buy_skin/spend_gems` | `FreeChestReady/ClaimFreeChest/CollectPiggy/BuySkin/SpendGems` |
| `offline_cap/offline_eff/collect_offline` | `OfflineCap/OfflineEff/CollectOffline` |
| `to_dict/save_game/load_game/apply_dict/reset_all` | `ToDict/SaveGame/LoadGame/ApplyDict/ResetAll` (+ `ToJson`, `ApplyJson`) |
| Constantes `SKINS/MISSIONS/GOALS/ACHIEVEMENTS/ESSENCE_NODES/FEATURES/DAILY_GEMS/RANKS/TOOL_NAMES/BIOME_NAMES/KIND_NAMES` | `Content.Skins/Missions/Goals/Achievements/EssenceNodes/Features/DailyGems/Ranks/ToolNames/BiomeNames/KindName()` |
| Constantes de balance (`HP0`, `COST_POWER`, `EVENT_FIRST`, ...) | `Balance.Hp0`, `Balance.CostPower0/CostPowerGrow`, `Balance.EventFirst`, ... |
| `Num.fmt(v)` / `Num.time_hms(s)` | `BigNum.Fmt(v)` / `BigNum.TimeHms(s)` |
| Variables (`gold`, `gems`, `lv`, `lv_best`, `stats`, `inv`, `equip`, ...) | Propiedades `Gold`, `Gems`, `Lv`, `LvBest`, `Stats`, `Inv`, `Equip`, ... |

Tipos: oro y tiempos `double`; niveles, gemas y etapas `int`; contadores (`Stat`, `Stats`, metas y logros) `long`.
`SKINS[i].col` pasa a `Skin.ColorHex` (RGB sin `#`; usar `ColorUtility.TryParseHtmlString("#" + hex, out c)`).

## Diferencias deliberadas respecto a Godot

* `BigNum.Fmt(inf/nan)` devuelve `"inf"`/`"nan"` (en Godot el resultado es indefinido).
* Un guardado corrupto con `k`/`r` de herramienta fuera de rango se acota a valores validos (Godot fallaria al indexar).
* `BuySkin`, `Unequip` y `MoveTool` con indices fuera de rango devuelven `false`/no hacen nada (Godot lanzaria error).
* El JSON escribe los `double` enteros con `.0` y usa cultura invariante.
* `EvNext`, `EvKind`, `CheckUnlocks`, `ActivateGoal`, `PollGoal`, `RollEvent` son `internal` (los usan las pruebas).

## Pruebas

```
cd unity_tools/core_tests
dotnet run                # suite completa (imprime PASS/FAIL, sale con codigo != 0 si algo falla)
dotnet run -- --quiet     # solo FAIL y el resumen
dotnet run -- --dump      # tabla de valores para comparar contra Godot (ver mas abajo)
```

La suite incluye el port de las 144 comprobaciones de `miner_idle/tools/test_meta.gd` (`MetaSuite.cs`) y pruebas extra
(`ExtraSuite.cs`): formato de numeros, JSON, guardado ida y vuelta, partidas viejas de Godot, determinismo con semilla,
ganancias offline, diario/pase/misiones, herramientas, tienda y metas generadas.

Equivalencia numerica con Godot: `dotnet run -- --dump` imprime `clave<TAB>valor` (los `double` en hexadecimal IEEE-754).
Un script GDScript equivalente (`godot --headless --path miner_idle -s dump.gd`, con `root.get_node("G")`) imprime las
mismas claves y se comparan con `diff`/Python.
