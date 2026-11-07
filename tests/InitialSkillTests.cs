using Relick.Core;
using System.Text.Json;

static class InitialSkillTests
{
    public static (string, Action)[] Cases => new (string, Action)[]
    {
        ("Habilidad inicial: antes de elegir no se reparte ni se permite mulligan", () =>
        {
            var game = new GameEngine(8);
            Check(game.State.Players.All(p => p.Hand.Count == 0 && p.Deck.Count == 20));
            Check(game.InitialSkillsFor(0).ToHashSet().SetEquals([CardCatalog.Magnet.Id, CardCatalog.Destiny.Id]));
            Check(!game.Start([]).Success);
        }),
        ("Habilidad inicial: elección de Destiny, una copia extraída y otras cuatro cartas", () =>
        {
            foreach (var id in new[] { CardCatalog.Destiny.Id, CardCatalog.Magnet.Id })
            {
                var game = new GameEngine(3);
                Check(game.ChooseInitialSkill(0, id).Success);
                Check(game.State.Players[0].Hand.Count == 5 && game.State.Players[0].Hand[0] == id);
                Check(game.State.Players[0].Deck.Count == 15 && game.State.Players[1].Hand.Count == 0);
                Check(game.State.Players[0].Hand.Concat(game.State.Players[0].Deck).Count(c => c == id) == 2);
                Check(!game.Start([]).Success);
                Check(game.ChooseInitialSkill(1, CardCatalog.Destiny.Id).Success && game.Start([]).Success);
            }
        }),
        ("Habilidad inicial: inválidos y segunda elección no alteran estado ni azar", () =>
        {
            var random = new CountingRandom(); var game = new GameEngine(random);
            foreach (var (actor,id) in new[] { (-1,"destiny-shuffle"), (2,"destiny-shuffle"), (0,"attack"), (0,"missing") })
            {
                var before = game.SaveSnapshot(); Check(!game.ChooseInitialSkill(actor,id).Success);
                Check(before == game.SaveSnapshot() && random.Count == 0);
            }
            game.State.Players[0].Deck.RemoveAll(id => id == CardCatalog.Destiny.Id);
            Check(!game.InitialSkillsFor(0).Contains(CardCatalog.Destiny.Id) && !game.ChooseInitialSkill(0, CardCatalog.Destiny.Id).Success);
            Check(game.ChooseInitialSkill(0, CardCatalog.Magnet.Id).Success);
            var saved = game.SaveSnapshot(); var consumed = random.Count;
            Check(!game.ChooseInitialSkill(0, CardCatalog.Magnet.Id).Success && saved == game.SaveSnapshot() && consumed == random.Count);
        }),
        ("Habilidad inicial: se puede devolver la elegida durante el mulligan", () =>
        {
            var game = new GameEngine(3); game.ChooseInitialSkill(0, CardCatalog.Destiny.Id); game.ChooseInitialSkill(1, CardCatalog.Magnet.Id);
            Check(game.Start([0]).Success && game.State.Players[0].Deck[^1] == CardCatalog.Destiny.Id);
            Check(!game.ChooseInitialSkill(0, CardCatalog.Magnet.Id).Success);
        }),
        ("Habilidad inicial: opciones dinámicas según habilidades presentes en el mazo", () =>
        {
            var card = new CardDefinition("test-other-skill", "Otra habilidad", CardKind.Skill, "Solo prueba");
            var catalog = (Dictionary<string, CardDefinition>)CardCatalog.All; catalog.Add(card.Id,card);
            try
            {
                var game = new GameEngine(8); var p = game.State.Players[0];
                p.Deck.RemoveAll(id => CardCatalog.Get(id).Kind == CardKind.Skill); p.Deck.AddRange([card.Id,card.Id]);
                Check(game.InitialSkillsFor(0).SequenceEqual([card.Id]) && game.ChooseInitialSkill(0,card.Id).Success && p.Hand[0] == card.Id);
            }
            finally { catalog.Remove(card.Id); }
        }),
        ("Habilidad inicial: guardar y recuperar antes y después de elegir", () =>
        {
            var game = GameEngine.FromSnapshot(new GameEngine(9).SaveSnapshot(), new SeededRandom(9));
            Check(game.InitialSkillsFor(0).Length == 2 && game.ChooseInitialSkill(0, CardCatalog.Destiny.Id).Success);
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), new SeededRandom(10));
            Check(game.InitialSkillsFor(0).Length == 0 && game.State.Players[0].InitialSkillId == CardCatalog.Destiny.Id);
            Check(game.InitialSkillsFor(1).Length == 2 && game.ChooseInitialSkill(1, CardCatalog.Magnet.Id).Success && game.Start([]).Success);
        })
    };
    static void Check(bool condition) { if (!condition) throw new Exception("Habilidad inicial incorrecta."); }
    sealed class CountingRandom : IGameRandom { public int Count; public int Next(int max) { Count++; return 0; } }
}
