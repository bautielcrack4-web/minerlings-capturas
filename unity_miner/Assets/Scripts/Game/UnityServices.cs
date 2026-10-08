using System.IO;
using Mineros.Core;
using UnityEngine;

namespace Mineros.Game
{
    /// <summary>Reloj real del juego (la logica de Core solo conoce IClock).</summary>
    public sealed class UnityClock : IClock
    {
        public double Now() { return System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0; }
        public string Today() { return System.DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture); }
    }

    /// <summary>Guardado en Application.persistentDataPath/save.json (mismo formato que la version Godot).</summary>
    public sealed class FileSaveStore : ISaveStore
    {
        readonly string path;
        public FileSaveStore(string fileName = "save.json") { path = Path.Combine(Application.persistentDataPath, fileName); }
        public void Save(string json) { File.WriteAllText(path, json); }
        public string Load() { return File.Exists(path) ? File.ReadAllText(path) : null; }
        public void Delete() { if (File.Exists(path)) File.Delete(path); }
    }
}
