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
        public static int FaceOf(Miner m) { return FaceOfId(m.Id); }
        public static bool IsWoman(Miner m) { return IsWomanId(m.Id); }
        public static bool IsWomanId(int id) { return FaceOfId(id) % 3 == 2; }

        /// <summary>Nombre y apellido estadounidenses (coinciden con la cara: hombre o mujer).</summary>
        public static string FullName(Miner m) { return FullNameId(m.Id); }
        public static string FullNameId(int id)
        {
            var first = IsWomanId(id) ? FemaleNames[Mix(id, 23) % FemaleNames.Length] : MaleNames[Mix(id, 23) % MaleNames.Length];
            return first + " " + LastNames[Mix(id, 37) % LastNames.Length];
        }

        public static string FirstName(Miner m) { return FirstNameId(m.Id); }
        public static string FirstNameId(int id) { var n = FullNameId(id); int i = n.IndexOf(' '); return i > 0 ? n.Substring(0, i) : n; }

        public static string Hometown(Miner m) { return Towns[Mix(m.Id, 53) % Towns.Length]; }

        // ------------------------------------------------------------ rasgos de cada cara (para que el muñeco 3D se parezca a su foto)
        /// <summary>Piel 0..8 (muy clara a muy oscura), vello 0 nada / 1 bigote / 2 barba de dias / 3 chivita / 4 barba corta /
        /// 5 barba tupida / 6 barba larga / 7 patillas; color de pelo/barba (hex); anteojos; mujer.</summary>
        public struct FaceLook
        {
            public int Skin, Facial; public string Hair; public bool Glasses, Woman;
            public FaceLook(int skin, int facial, string hair, int glasses, int woman) { Skin = skin; Facial = facial; Hair = hair; Glasses = glasses == 1; Woman = woman == 1; }
        }

        /// <summary>Sale de como se generaron las 48 caras (unity_tools: caras.json): misma persona en la foto y en la isla.</summary>
        public static readonly FaceLook[] Looks =
        {
            new FaceLook(2, 0, "2a1d16", 1, 0),
            new FaceLook(0, 5, "5a3a22", 0, 0),
            new FaceLook(6, 0, "b8482a", 0, 1),
            new FaceLook(6, 0, "b8482a", 1, 0),
            new FaceLook(0, 0, "8a3a1e", 0, 0),
            new FaceLook(0, 0, "e889b0", 0, 1),
            new FaceLook(2, 1, "5a3a22", 0, 0),
            new FaceLook(2, 0, "1f1712", 0, 0),
            new FaceLook(1, 0, "e889b0", 0, 1),
            new FaceLook(0, 5, "8a3a1e", 0, 0),
            new FaceLook(7, 4, "8a3a1e", 0, 0),
            new FaceLook(2, 0, "5a3a22", 1, 1),
            new FaceLook(7, 4, "e8e8e8", 0, 0),
            new FaceLook(8, 2, "2a1d16", 0, 0),
            new FaceLook(6, 0, "1f1712", 1, 1),
            new FaceLook(5, 6, "e8e8e8", 0, 0),
            new FaceLook(7, 1, "9a9a9a", 0, 0),
            new FaceLook(1, 0, "1f1712", 0, 1),
            new FaceLook(7, 1, "9a9a9a", 1, 0),
            new FaceLook(7, 2, "e8e8e8", 0, 0),
            new FaceLook(0, 0, "5a3a22", 0, 1),
            new FaceLook(3, 1, "9a9a9a", 0, 0),
            new FaceLook(7, 7, "2a1d16", 0, 0),
            new FaceLook(8, 0, "9a9a9a", 0, 1),
            new FaceLook(6, 2, "d9b25a", 1, 0),
            new FaceLook(3, 0, "d9b25a", 0, 0),
            new FaceLook(4, 0, "1f1712", 0, 1),
            new FaceLook(5, 0, "8a3a1e", 0, 0),
            new FaceLook(8, 0, "8a3a1e", 0, 0),
            new FaceLook(6, 0, "8a3a1e", 1, 1),
            new FaceLook(6, 5, "b8482a", 1, 0),
            new FaceLook(2, 6, "1f1712", 0, 0),
            new FaceLook(0, 0, "1f1712", 0, 1),
            new FaceLook(0, 5, "1f1712", 0, 0),
            new FaceLook(4, 0, "e8e8e8", 0, 0),
            new FaceLook(1, 0, "e8e8e8", 0, 1),
            new FaceLook(4, 2, "1f1712", 1, 0),
            new FaceLook(4, 2, "9a9a9a", 0, 0),
            new FaceLook(8, 0, "2a1d16", 0, 1),
            new FaceLook(4, 3, "9a9a9a", 0, 0),
            new FaceLook(5, 7, "d9b25a", 0, 0),
            new FaceLook(3, 0, "1f1712", 0, 1),
            new FaceLook(3, 7, "d9b25a", 0, 0),
            new FaceLook(0, 3, "b8482a", 0, 0),
            new FaceLook(5, 0, "e8e8e8", 0, 1),
            new FaceLook(1, 1, "d9b25a", 0, 0),
            new FaceLook(5, 4, "d9b25a", 0, 0),
            new FaceLook(5, 0, "b8482a", 1, 1)
        };

        public static int FaceOfId(int minerId) { return Mix(minerId, 11) % FaceCount; }
        public static FaceLook LookOfId(int minerId) { return Looks[FaceOfId(minerId) % Looks.Length]; }

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
