using System;

namespace Mineros.Core
{
    /// <summary>Reloj inyectable: la logica nunca lee el reloj del sistema directamente.</summary>
    public interface IClock
    {
        /// <summary>Segundos unix (con fraccion).</summary>
        double Now();
        /// <summary>Fecha local "yyyy-MM-dd" (para el reseteo diario).</summary>
        string Today();
    }

    /// <summary>Implementacion real; en pruebas se usa un reloj falso.</summary>
    public sealed class SystemClock : IClock
    {
        public double Now()
        {
            return (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        public string Today()
        {
            return DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Donde se guarda la partida (PlayerPrefs, archivo, memoria...). Load devuelve null si no hay.</summary>
    public interface ISaveStore
    {
        void Save(string json);
        string Load();
    }

    /// <summary>Guardado en memoria (pruebas) o descartado (si no se necesita).</summary>
    public sealed class MemorySaveStore : ISaveStore
    {
        public string Text;
        public int SaveCount;
        public void Save(string json) { Text = json; SaveCount++; }
        public string Load() { return Text; }
    }
}
