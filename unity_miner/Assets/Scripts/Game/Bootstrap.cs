using Mineros.UI;
using Mineros.World;
using UnityEngine;

namespace Mineros.Game
{
    /// <summary>
    /// Arma toda la escena por codigo al iniciar (no hay escenas editadas a mano):
    /// GameManager, mundo 3D (camara, luz, cañon, mineros) e interfaz.
    /// </summary>
    public static class Bootstrap
    {
        /// <summary>Si es true, Bootstrap no corre solo (lo usa ShotTool para armar escenarios).</summary>
        public static bool Manual;

        /// <summary>true: arranca la Isla Minera (docs/ISLA_MINERA.md); false: el cañon clasico.</summary>
        public static bool UseIsland = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (Manual || GameManager.I != null) return;
            if (UseIsland) Mineros.IslandView.IslandGame.Build();
            else Build();
        }

        /// <param name="configure">Opcional (capturas): ajusta el estado despues de cargar y antes de armar mundo e interfaz.</param>
        public static GameManager Build(string saveFile = "save.json", System.Action<Mineros.Core.GameState> configure = null)
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            var root = new GameObject("Mineros");
            Object.DontDestroyOnLoad(root);
            var gm = root.AddComponent<GameManager>();
            var world = new GameObject("World").AddComponent<WorldController>();
            world.transform.SetParent(root.transform, false);
            var hud = new GameObject("Hud").AddComponent<HudController>();
            hud.transform.SetParent(root.transform, false);
            gm.Init(world, hud, saveFile);
            configure?.Invoke(gm.G);
            world.Init(gm);
            hud.Init(gm);
            return gm;
        }
    }
}
