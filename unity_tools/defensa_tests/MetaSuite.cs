using System;
using System.Linq;
using Fortin.Core;

namespace DefensaTests
{
    public static partial class Suites
    {
        static partial void RunMeta(Action<bool, string> ok)
        {
            // energia: 30 max, +1 cada 6 min, 5 por partida
            var p = new Profile();
            long t0 = 1000000;
            p.TickEnergy(t0);
            ok(p.Energy == 30, "energia empieza llena (30)");
            ok(p.PayMatch(t0) && p.Energy == 25, "jugar cuesta 5 de energia");
            p.TickEnergy(t0 + 359); ok(p.Energy == 25, "a los 5:59 todavia no sube");
            p.TickEnergy(t0 + 360); ok(p.Energy == 26, "+1 energia cada 6 minutos");
            p.TickEnergy(t0 + 360 * 10); ok(p.Energy == 30, "no pasa de 30");
            p.Energy = 3; p.EnergyStamp = t0 + 5000; ok(!p.PayMatch(t0 + 5000), "sin energia no se juega");
            ok(p.AdEnergy(5) && p.AdEnergy(5) && p.AdEnergy(5) && !p.AdEnergy(5), "energia por anuncio: 3 por dia");
            // mazo inicial
            p = new Profile();
            var ids = p.Deck.Select(d => Defs.Heroes[d].Id).ToArray();
            ok(ids.SequenceEqual(new[] { "lancero", "arquera", "mago", "enano", "barbaro" }), "mazo inicial: Soldadito, Arquera, Mago, Granjero y Dino");
            ok(p.Heroes.Count(h => h.Owned) == 5, "al empezar se tienen 5 juguetes");
            // mejoras
            int sold = Defs.HeroIndex("lancero");
            p.Heroes[sold].Cards = 2; p.Coins = 1000;
            ok(p.CanUpgrade(sold) && p.Upgrade(sold) && p.Heroes[sold].Level == 2 && p.Coins == 980 && p.Heroes[sold].Cards == 0, "mejorar gasta cartas y monedas y sube el nivel");
            ok(!p.Upgrade(sold), "sin cartas no se mejora");
            ok(Profile.CardsFor(Defs.HeroIndex("hada"), 1) < Profile.CardsFor(sold, 1) || Profile.CardsFor(Defs.HeroIndex("hada"), 1) == 1, "los legendarios piden menos cartas");
            ok(p.DeckPerm()[0] == 2, "el nivel permanente llega a la batalla");
            // mazo: cambiar
            int robot = Defs.HeroIndex("chaman");
            ok(!p.SetDeck(0, robot), "no se puede usar un juguete que no se tiene");
            p.Heroes[robot].Owned = true;
            ok(p.SetDeck(4, robot) && p.Deck[4] == robot, "poner un juguete en el mazo");
            ok(p.SetDeck(0, robot) && p.Deck[0] == robot && p.Deck[4] == sold, "si ya estaba en el mazo, se intercambian");
            // cofres
            p = new Profile(); var rng = new Rng(3);
            int c0 = p.Coins;
            var r = p.OpenChest(1, rng);
            ok(r.Coins > 0 && p.Coins == c0 + r.Coins, "el cofre da monedas");
            ok(r.Heroes.Count == 3 && r.Counts.All(n => n > 0), "el cofre de plata trae 3 tandas de cartas");
            p.Chapter = 3; var leg = p.OpenChest(3, new Rng(9));
            ok(Defs.Heroes[leg.Heroes.Last()].Rarity >= Rarity.Epic, "el cofre legendario termina en una tanda epica o mejor");
            // campana, estrellas y premios
            p = new Profile();
            var w = p.Win(1, 1, 3, 10, new Rng(1));
            ok(w.FirstClear && w.Chest == 0 && p.Chests.Count == 1, "primera victoria: cofre de madera");
            ok(p.Stars[0, 0] == 3 && p.Level == 2, "estrellas guardadas y se abre el 1-2");
            ok(p.Unlocked(1, 2) && !p.Unlocked(1, 3), "los niveles se abren de a uno");
            for (int lv = 2; lv <= 10; lv++) p.Win(1, lv, 3, 10, new Rng(lv));
            ok(p.Chapter == 2 && p.Level == 1, "al ganar el 1-10 se abre el capitulo 2");
            ok(p.ChapterChest[0, 0] && p.ChapterChest[0, 1] && p.ChapterChest[0, 2], "las estrellas del capitulo dan sus 3 cofres");
            // misiones, racha y pase
            p = new Profile(); p.DailyReset(100);
            ok(p.Missions.Count == 3 && p.Missions.Select(m => m.Id).Distinct().Count() == 3, "3 misiones diarias distintas");
            var mid = p.Missions[0].Id;
            p.Progress(mid, 9999);
            int cc = p.Coins;
            ok(p.ClaimMission(0) && p.Coins > cc && !p.ClaimMission(0), "cobrar una mision una sola vez");
            ok(p.ClaimStreak(100) == 1 && p.ClaimStreak(100) == -1 && p.ClaimStreak(101) == 2, "racha: un premio por dia y suma");
            ok(p.ClaimStreak(103) == 1, "si se salta un dia la racha vuelve a empezar");
            for (int d = 0; d < 6; d++) p.ClaimStreak(104 + d);
            ok(p.StreakDay == 7 && p.Chests.Contains(2), "dia 7: cofre de oro");
            p.PassXp = 250;
            ok(p.PassLevel == 2 && p.ClaimPass(false) && p.ClaimPass(false) && !p.ClaimPass(false), "pase gratis: premios hasta el nivel alcanzado");
            ok(!p.ClaimPass(true), "el premium sin comprar no se cobra");
            // guardado: ida y vuelta
            p = new Profile(); p.Coins = 1234; p.Gems = 56; p.Heroes[robot].Owned = true; p.Heroes[robot].Level = 4; p.Heroes[robot].Cards = 7;
            p.SetDeck(2, robot); p.Win(1, 1, 2, 10, new Rng(2)); p.DailyReset(50); p.Missions[1].Progress = 3; p.PassPremium = true; p.EndlessBest = 33;
            var txt = Save.Write(p);
            var q = Save.Read(txt);
            ok(q.Coins == p.Coins && q.Gems == p.Gems, "guardado: monedas y gemas");
            ok(q.Heroes[robot].Owned && q.Heroes[robot].Level == 4 && q.Heroes[robot].Cards == 7, "guardado: juguetes");
            ok(q.Deck.SequenceEqual(p.Deck), "guardado: mazo");
            ok(q.Stars[0, 0] == 2 && q.Level == p.Level && q.Chests.SequenceEqual(p.Chests), "guardado: campana y cofres");
            ok(q.Missions.Count == 3 && q.Missions[1].Progress == 3 && q.PassPremium && q.EndlessBest == 33, "guardado: misiones, pase y Noche Eterna");
            ok(Save.Write(q) == txt, "guardado estable (escribir lo leido da lo mismo)");
            ok(Save.Read("") != null && Save.Read("basura\n=\nx=1").Coins == new Profile().Coins, "guardado vacio o roto: perfil nuevo");
        }
    }
}
