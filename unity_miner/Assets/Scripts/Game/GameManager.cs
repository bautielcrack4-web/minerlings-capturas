using System;
using Mineros.Core;
using Mineros.UI;
using Mineros.World;
using UnityEngine;

namespace Mineros.Game
{
    /// <summary>
    /// Punto central del juego en Unity: envuelve GameState (Core), lo avanza con Tick y guarda la partida.
    /// Bootstrap lo crea al arrancar; los demas sistemas acceden por GameManager.I.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        /// <summary>Estado y reglas del juego (economia, metas, logros, eventos...).</summary>
        public GameState G { get; private set; }
        public WorldController World { get; private set; }
        public HudController Hud { get; private set; }
        public FileSaveStore Store { get; private set; }

        /// <summary>"idle" (cañon) o "play" (minijuego Excavar).</summary>
        public string Mode { get; set; } = "idle";

        /// <summary>Se dispara una vez por frame despues de Tick (para UI que refresca por tiempo).</summary>
        public event Action<float> Ticked;

        double saveTimer;

        public void Init(WorldController world, HudController hud, string saveFile = "save.json")
        {
            I = this;
            Store = new FileSaveStore(saveFile);
            G = new GameState(new UnityClock(), new System.Random(), Store);
            World = world;
            Hud = hud;
            G.Start();
        }

        void Update()
        {
            if (G == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.25f);
            G.Tick(dt);
            Ticked?.Invoke(dt);
            saveTimer += dt;
            if (saveTimer > 5.0)
            {
                saveTimer = 0;
                G.SaveGame();
            }
        }

        void OnApplicationPause(bool paused) { if (paused) G?.SaveGame(); }
        void OnApplicationQuit() { G?.SaveGame(); }
    }
}
