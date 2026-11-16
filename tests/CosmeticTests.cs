using Relick.Core;

static class CosmeticTests
{
    public static (string, Action)[] Cases => new (string, Action)[]
    {
        ("Estilo: micas y dados viajan de la apertura a la vista pública, con el clásico para lo desconocido o ausente", () =>
        {
            var random = new ScriptedRandom();
            var opening = new DuelOpening
            {
                Names = ["Ana", "Beto"], Hands = [[], []], Decks = [CardCatalog.TrainingDeck(), CardCatalog.TrainingDeck()],
                Bonuses = [CardCatalog.Attack.Id, CardCatalog.Attack.Id], Sleeves = ["oro", "no-existe"], Dice = ["obsidiana"]
            };
            var game = GameEngine.FromOpening(opening, random);
            foreach (var seat in new[] { 0, 1 })
            {
                var view = DuelSnapshot.For(game, seat, 1);
                Check(view.Players[0].Sleeve == "oro" && view.Players[0].Dice == "obsidiana");
                Check(view.Players[1].Sleeve == CosmeticCatalog.DefaultSleeve && view.Players[1].Dice == CosmeticCatalog.DefaultDice);
            }
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(game.State.Players[0].Sleeve == "oro" && game.State.Players[0].Dice == "obsidiana");
            // The classic style is not written, so states saved before styles existed stay identical.
            Check(game.State.Players[1].Sleeve == null && !game.SaveSnapshot().Contains(CosmeticCatalog.DefaultSleeve));
            var legacy = GameEngine.FromOpening(new DuelOpening { Names = ["Ana", "Beto"], Hands = [[], []],
                Decks = [CardCatalog.TrainingDeck(), CardCatalog.TrainingDeck()], Bonuses = [null, null] }, random);
            Check(DuelSnapshot.For(legacy, 0, 1).Players.All(p => p.Sleeve == CosmeticCatalog.DefaultSleeve && p.Dice == CosmeticCatalog.DefaultDice));
            Check(CosmeticCatalog.Sleeves.Select(s => s.Id).Distinct().Count() == CosmeticCatalog.Sleeves.Count && CosmeticCatalog.Sleeves[0].Printed &&
                CosmeticCatalog.Dice.Select(d => d.Id).Distinct().Count() == CosmeticCatalog.Dice.Count);
        }),
        ("Estilo: cada tirada se atribuye a quien lanza, también el dado del defensor", () =>
        {
            var rng = new ScriptedRandom(); var g = TestGame.Create(rng); g.State.CurrentPlayer = 0; g.State.Phase = Phase.Combat; g.State.Turn = 2;
            foreach (var p in g.State.Players) { p.Hand.Clear(); p.Skills.Clear(); p.Equipment = null; p.Deck.Clear(); p.Deck.AddRange(Enumerable.Repeat("attack", 8)); }
            g.State.Active.Hand.Add("attack"); g.State.Active.Skills.Add("magnet-shuffle"); g.State.Active.Relikia = 0;
            g.State.Players[1].Hand.Add(CardCatalog.Dodge.Id);
            var cues = DuelVisualEvents.Capture(g);
            rng.EnqueueDie(6); Check(g.Attack(0, 0, 0).Success && g.ResolveAttackReroll(0, false).Success && g.State.Phase == Phase.DefenseChoice);
            rng.EnqueueDie(5); Check(g.PlayCode(1, 0).Success);
            var rolls = cues.Where(c => c.Kind == DuelVisualKind.Die).Select(c => c.Player).ToArray();
            Check(rolls.SequenceEqual([0, 1]));
        })
    };

    private static void Check(bool value) { if (!value) throw new Exception("Estilo cosmético no satisfecho"); }
}
