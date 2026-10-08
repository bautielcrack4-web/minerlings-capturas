using System;

namespace Mineros.Core
{
    /// <summary>
    /// Carnet de cada minero (pedido del dueño: "que no sean todos identicos, nombre real adaptado a EE. UU. y una foto
    /// de su cara"). Todo sale del id, sin guardar nada: la misma partida siempre muestra la misma persona.
    /// Las 48 caras viven en Resources/Faces (f00..f47); las de indice % 3 == 2 son mujeres.
    /// </summary>
    public sealed partial class Island
    {
        public const int FaceCount = 48;

        static readonly string[] MaleNames =
        {
            "Jake", "Tyler", "Cody", "Hank", "Buck", "Wyatt", "Dale", "Earl", "Travis", "Clint", "Wade", "Rusty", "Luke",
            "Billy", "Colt", "Jesse", "Bobby", "Gus", "Roy", "Duke", "Mason", "Carter", "Owen", "Hunter", "Logan", "Eli",
            "Ray", "Frank", "Walt", "Marcus", "Andre", "Diego", "Carlos", "Darnell", "Tommy", "Sam", "Ben", "Joe", "Nate", "Kyle"
        };
        static readonly string[] FemaleNames =
        {
            "Daisy", "Peggy", "Rosie", "Lucy", "Ruby", "Nora", "June", "Sadie", "Hazel", "Molly", "Tess", "Annie", "Kate",
            "Dolly", "Maya", "Jada", "Rita", "Lily", "Grace", "Ellie", "Ava", "Willa", "Bonnie", "Carmen", "Tasha", "Opal"
        };
        static readonly string[] LastNames =
        {
            "Miller", "Walker", "Brooks", "Carter", "Hayes", "Turner", "Parker", "Cooper", "Reed", "Bennett", "Coleman",
            "Foster", "Bailey", "Murphy", "Sullivan", "Dawson", "Tucker", "Harper", "Ellis", "Wallace", "Grant", "Morgan",
            "Porter", "Hudson", "Garrett", "Holt", "Jensen", "Lawson", "McCoy", "Nash", "Owens", "Pruitt", "Ramsey", "Slater",
            "Stone", "Whitaker", "Young", "Garcia", "Lopez", "Washington", "Jackson", "Kowalski", "O'Brien", "Schmidt", "Rivera"
        };
        static readonly string[] Towns =
        {
            "Tulsa, OK", "Butte, MT", "Leadville, CO", "Bisbee, AZ", "Deadwood, SD", "Elko, NV", "Wheeling, WV", "Scranton, PA",
            "Duluth, MN", "Boise, ID", "Casper, WY", "Ely, NV", "Silverton, CO", "Tombstone, AZ", "Hibbing, MN", "Galena, IL",
            "Amarillo, TX", "Knoxville, TN", "Spokane, WA", "Reno, NV", "Bakersfield, CA", "Joplin, MO", "Billings, MT",
            "Pikeville, KY", "Asheville, NC", "Laramie, WY", "Juneau, AK", "Moab, UT", "Taos, NM", "Fairbanks, AK"
        };

        static int Mix(int id, int salt)
        {
            unchecked
            {
                uint h = (uint)id * 2654435761u ^ (uint)salt * 40503u;
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
                return (int)(h & 0x7fffffff);
            }
        }

        /// <summary>Indice de la cara (0..47) del minero.</summary>
        public static int FaceOf(Miner m) { return Mix(m.Id, 11) % FaceCount; }
        public static bool IsWoman(Miner m) { return FaceOf(m) % 3 == 2; }

        /// <summary>Nombre y apellido estadounidenses (coinciden con la cara: hombre o mujer).</summary>
        public static string FullName(Miner m)
        {
            var first = IsWoman(m) ? FemaleNames[Mix(m.Id, 23) % FemaleNames.Length] : MaleNames[Mix(m.Id, 23) % MaleNames.Length];
            return first + " " + LastNames[Mix(m.Id, 37) % LastNames.Length];
        }

        public static string FirstName(Miner m) { var n = FullName(m); int i = n.IndexOf(' '); return i > 0 ? n.Substring(0, i) : n; }

        public static string Hometown(Miner m) { return Towns[Mix(m.Id, 53) % Towns.Length]; }

        // ------------------------------------------------------------ entrenar (subir de nivel pagando)
        /// <summary>Precio de subir un nivel al minero: crece con el nivel y con lo que gana la isla.</summary>
        public double TrainCost(Miner m)
        {
            return Math.Round(Math.Max(60.0, IncomePerSec() * 20.0) * Math.Pow(1.6, m.Level - 1));
        }

        public bool CanTrain(Miner m) { return m != null && m.Level < MaxMinerLevel && Coins >= TrainCost(m); }

        /// <summary>Paga y sube un nivel (la experiencia que tenia se conserva).</summary>
        public bool TrainMiner(Miner m)
        {
            if (!CanTrain(m)) return false;
            Coins -= TrainCost(m);
            m.Level++;
            AddStat("levelups", 1);
            AddStat("trained", 1);
            MinerLevelUp?.Invoke(m);
            return true;
        }
    }
}
