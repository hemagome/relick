using Relick.Core;

static class CompetitiveTests
{
    public static (string, Action)[] Cases => new (string, Action)[]
    {
        ("ELO: entre iguales sube y baja lo mismo, el doble durante la colocación", () =>
        {
            Check(CompetitiveRules.After(1000, 1000, 1, 0) == 1024 && CompetitiveRules.After(1000, 1000, 0, 0) == 976);
            Check(CompetitiveRules.After(1000, 1000, 1, 5) == 1012 && CompetitiveRules.After(1000, 1000, 0, 5) == 988);
            Check(CompetitiveRules.After(1000, 1000, 0.5, 9) == 1000);
        }),
        ("ELO: ganar siempre suma, perder siempre resta y nadie baja del piso", () =>
        {
            Check(CompetitiveRules.After(2400, 100, 1, 50) == 2401 && CompetitiveRules.After(100, 2400, 0, 50) == 100);
            Check(CompetitiveRules.After(500, 2400, 0, 50) == 499);
            Check(CompetitiveRules.After(1000, 1200, 1, 9) > CompetitiveRules.After(1000, 800, 1, 9));
            Check(Math.Abs(CompetitiveRules.Expected(1200, 1000) + CompetitiveRules.Expected(1000, 1200) - 1) < 1e-9);
        }),
        ("ELO: la búsqueda se amplía con la espera y a los dos minutos acepta a cualquiera", () =>
        {
            Check(CompetitiveRules.Window(0) == 100 && CompetitiveRules.Window(10) == 150 && CompetitiveRules.Window(-3) == 100);
            Check(CompetitiveRules.Window(119) < int.MaxValue && CompetitiveRules.Window(120) == int.MaxValue);
        }),
        ("ELO: ligas, colocación y reinicio suave", () =>
        {
            Check(CompetitiveRules.LeagueOf(949) == League.Bronze && CompetitiveRules.LeagueOf(950) == League.Silver);
            Check(CompetitiveRules.LeagueOf(1100) == League.Gold && CompetitiveRules.LeagueOf(1250) == League.Platinum && CompetitiveRules.LeagueOf(1400) == League.Legend);
            Check(CompetitiveRules.Standing(1180, 4) == "Colocación 4/5" && CompetitiveRules.Standing(1180, 5) == "Oro · 1180");
            Check(CompetitiveRules.SoftReset(1400) == 1200 && CompetitiveRules.SoftReset(800) == 900 && CompetitiveRules.SoftReset(1000) == 1000);
        }),
        ("Torneo: cada tamaño reparte a todos una vez y los pases van a las mejores semillas", () =>
        {
            for (var entrants = CompetitiveRules.MinEntrants; entrants <= CompetitiveRules.MaxEntrants; entrants++)
            {
                var places = CompetitiveRules.FirstRound(entrants);
                var rounds = CompetitiveRules.Rounds(entrants);
                Check(places.Length == 1 << rounds && places.Length < entrants * 2);
                Check(places.Where(seed => seed > 0).Order().SequenceEqual(Enumerable.Range(1, entrants)));
                var byes = places.Length - entrants;
                var rested = new List<int>();
                for (var i = 0; i < places.Length; i += 2)
                {
                    Check(places[i] + places[i + 1] > 0);
                    if (places[i] == 0 || places[i + 1] == 0) rested.Add(places[i] + places[i + 1]);
                }
                Check(rested.Order().SequenceEqual(Enumerable.Range(1, byes)));
                Check(Array.IndexOf(places, 1) < places.Length / 2 && Array.IndexOf(places, 2) >= places.Length / 2);
            }
        }),
        ("Torneo: el puesto sale de la ronda en que caes", () =>
        {
            Check(CompetitiveRules.Placing(3, 4) == "Campeón" && CompetitiveRules.Placing(3, 3) == "2.º");
            Check(CompetitiveRules.Placing(3, 2) == "Top 4" && CompetitiveRules.Placing(3, 1) == "Top 8");
        }),
        ("Premios: existen en sus catálogos, son únicos y ningún predeterminado se gana", () =>
        {
            Check(RewardCatalog.All.Select(r => r.Key).Distinct().Count() == RewardCatalog.All.Count);
            foreach (var reward in RewardCatalog.All)
                Check(reward.Kind switch
                {
                    RewardKind.Title => ProfileCatalog.IsTitle(reward.Id),
                    RewardKind.Frame => ProfileCatalog.IsFrame(reward.Id),
                    RewardKind.Sleeve => CosmeticCatalog.IsSleeve(reward.Id),
                    _ => CosmeticCatalog.IsDice(reward.Id)
                });
            Check(RewardCatalog.Of(RewardKind.Title, ProfileCatalog.DefaultTitle) == null && RewardCatalog.Of(RewardKind.Frame, ProfileCatalog.DefaultFrame) == null);
            Check(RewardCatalog.Of(RewardKind.Sleeve, CosmeticCatalog.DefaultSleeve) == null && RewardCatalog.Of(RewardKind.Dice, CosmeticCatalog.DefaultDice) == null);
        }),
        ("Premios: la temporada paga según duelos y liga", () =>
        {
            Check(!RewardCatalog.ForSeason(1600, 4).Any());
            Check(RewardCatalog.ForSeason(900, 5).Single() == RewardCatalog.SeasonTitle);
            Check(RewardCatalog.ForSeason(1100, 5).Count() == 2 && RewardCatalog.ForSeason(1250, 5).Count() == 3);
        }),
        ("Tiempo: perder por reloj termina el duelo una sola vez, con su motivo", () =>
        {
            var game = new GameEngine(42);
            game.ChooseInitialSkill(0, CardCatalog.Magnet.Id); game.ChooseInitialSkill(1, CardCatalog.Destiny.Id); game.Start([]);
            Check(!game.Forfeit(2, "x").Success && game.Forfeit(0, "agotó el tiempo").Success);
            Check(game.State.IsOver && game.State.Winner == 1 && !game.Forfeit(1, "otra vez").Success && game.State.Winner == 1);
            Check(DuelSnapshot.For(game, 1, 3).EndReason!.Contains("agotó el tiempo"));
        })
    };

    private static void Check(bool condition) { if (!condition) throw new Exception("Comprobación competitiva fallida"); }
}
