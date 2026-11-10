using Relick.Core;
using System.Text.Json;

static class DuelOutcomeTests
{
    public static IEnumerable<(string Name, Action Run)> Cases => new (string, Action)[]
    {
        ("Resultado público: abandono, privacidad y recuperación sin log interno", () =>
        {
            var game = Fixture(); game.State.Log.Add("SECRETO_INTERNO");
            Check(game.Concede(0).Success); Verify(game, "abandona el duelo");
            var checkpoint = JsonSerializer.Deserialize(game.SaveSnapshot(false), EngineJson.Default.EngineSnapshot)!;
            checkpoint.EndReason = null;
            var legacy = GameEngine.FromSnapshot(JsonSerializer.Serialize(checkpoint, EngineJson.Default.EngineSnapshot), new SeededRandom(11));
            Check(DuelSnapshot.For(legacy, 0, 1).Winner == 1 && DuelSnapshot.For(legacy, 0, 1).EndReason == null);
        }),
        ("Resultado público: daño definitivo conserva su causa al recuperar", () =>
        {
            var rng = new ScriptedRandom(); var game = Fixture(rng);
            game.State.Phase = Phase.Combat; game.State.Turn = 2;
            game.State.Active.Skills.Add(CardCatalog.Destiny.Id); game.State.Active.AttackRerollUsedThisTurn = true;
            game.State.Players[1].Health = 0; game.State.Players[1].HasReachedZero = true;
            rng.EnqueueDie(5); Check(game.Attack(0, 0, 0).Success); Verify(game, "daño definitivo");
        }),
        ("Resultado público: agotamiento y empate conservan una causa pública común", () =>
        {
            var game = Fixture(); game.State.Phase = Phase.Actions;
            var player = game.State.Active; player.Recycled = true; player.Health = 8;
            player.Hand.Clear(); player.Hand.Add(CardCatalog.Senshuni.Id);
            player.Deck.Clear(); player.Deck.Add(CardCatalog.Attack.Id); game.State.Players[1].Health = 6;
            Check(game.PlayCode(0, 0).Success && game.State.Winner == -1); Verify(game, "agota su mazo por segunda vez");
        })
    };
    private static GameEngine Fixture(IGameRandom? random = null)
    {
        var game = GameEngine.FromOpening(new DuelOpening { Names = ["Ana", "Beto"], StartingSeat = 0,
            Hands = [[CardCatalog.Attack.Id], [CardCatalog.Senshuni.Id]],
            Decks = [Enumerable.Repeat(CardCatalog.Attack.Id, 12).ToList(), Enumerable.Repeat(CardCatalog.Attack.Id, 12).ToList()], Bonuses = [null, null] }, random ?? new SeededRandom(11));
        foreach (var player in game.State.Players) player.Dexterity = 0;
        return game;
    }
    private static void Verify(GameEngine game, string cause)
    {
        var first = DuelSnapshot.For(game, 0, 1); var second = DuelSnapshot.For(game, 1, 1);
        Check(first.Winner != null && first.EndReason!.Contains(cause) && first.EndReason == second.EndReason);
        Check(!JsonSerializer.Serialize(first, DuelJson.Default.DuelSnapshot).Contains("SECRETO_INTERNO"));
        var restored = GameEngine.FromSnapshot(game.SaveSnapshot(false), new SeededRandom(11));
        Check(DuelSnapshot.For(restored, 0, 1).EndReason == first.EndReason);
    }
    private static void Check(bool value) { if (!value) throw new Exception("Resultado público incorrecto."); }
}
