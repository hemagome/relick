using Relick.Core;

static class YunoOroTests
{
    private static readonly string[] Five = [CardCatalog.LetterR.Id, CardCatalog.LetterE.Id,
        CardCatalog.LetterL.Id, CardCatalog.LetterI.Id, CardCatalog.LetterC.Id];

    public static (string, Action)[] Cases =>
    [
        ("RELICK: la sexta letra conserva su modo en RAM y admite respuestas antes de entrar", () =>
        {
            var (game, random) = Fixture();
            game.State.Active.AttachedCards.AddRange(Five.Reverse());
            game.State.Active.Hand.Add(CardCatalog.LetterK.Id);
            game.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
            Check(game.PlayCode(0, 0, placeLetter: true).Success && game.State.Phase == Phase.ErrorResponse);
            Check(game.State.ActivationChain.Single().PlaceLetter && !game.State.IsOver);
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(game.PassErrorResponse(1).Success);
            Check(game.State.Active.AttachedCards.Contains(CardCatalog.LetterK.Id) && game.State.Winner == 0);
        }),
        ("RELICK: Corromper una de las cinco letras en respuesta impide la victoria", () =>
        {
            var (game, _) = Fixture(3);
            game.State.Active.AttachedCards.AddRange(Five);
            game.State.Active.Hand.Add(CardCatalog.LetterK.Id);
            game.State.Players[1].Hand.AddRange([CardCatalog.LetterE.Id, CardCatalog.LetterL.Id]);
            Check(game.PlayCode(0, 0, placeLetter: true).Success && game.PlayCode(1, 0).Success);
            Choose(game, 1, CardCatalog.LetterL.Id);
            Choose(game, 1, null, "Resolver");
            Choose(game, 1, CardCatalog.LetterR.Id);
            Check(!game.State.IsOver && game.State.Active.AttachedCards.Count == 5 &&
                game.State.Active.AttachedCards.Contains(CardCatalog.LetterK.Id) &&
                game.State.Active.CorruptedMemory.Contains(CardCatalog.LetterR.Id));
        }),
        ("RELICK: el clon que completa seis letras gana antes que la colocación original", () =>
        {
            var (game, _) = Fixture();
            foreach (var p in game.State.Players) p.AttachedCards.AddRange(Five);
            game.State.Active.Hand.Add(CardCatalog.LetterK.Id);
            game.State.Players[1].Hand.Add(CardCatalog.CopyHelmet.Id);
            Check(game.PlayCode(0, 0, placeLetter: true).Success && game.PlayCode(1, 0).Success);
            Choose(game, 1, CardCatalog.LetterK.Id, "Colocar");
            Check(game.State.Winner == 1 && game.State.Active.AttachedCards.Count == 5 &&
                game.State.Players[1].AttachedCards.Any(id => CardCatalog.Get(id).Letter == 'K'));
        }),
        ("RELICK: Combo en turno rival, coste de I y duplicados", () =>
        {
            var (game, _) = Fixture();
            var rival = game.State.Players[1];
            rival.Hand.AddRange([CardCatalog.LetterR.Id, CardCatalog.LetterI.Id, CardCatalog.LetterI.Id]);
            Check(!game.PlayCode(1, 0).Success && game.PlayCode(1, 0, placeLetter: true).Success);
            Check(game.PlayCode(1, 0, placeLetter: true).Success && rival.ActionsLeft == 1);
            Check(game.PlayCode(1, 0).Success && rival.ActionsLeft == 2 && rival.AttachedCards.Count == 2);
            game.State.Active.AttachedCards.AddRange([.. Five[..4], CardCatalog.LetterI.Id]);
            game.State.Active.Hand.Add(CardCatalog.LetterK.Id);
            Check(game.PlayCode(0, 0, placeLetter: true).Success && !game.State.IsOver);
        }),
        ("Yuno Oro: evoluciona con cinco cartas letra y puede colocar desde memoria", () =>
        {
            var (game, _) = Fixture();
            var player = game.State.Active;
            player.AttachedCards.AddRange([.. Five[..3], Five[0]]);
            player.Relikia = 3500;
            Check(!game.CanEvolve(0));
            player.AttachedCards.Add(Five[3]);
            player.Memory.Add(Five[4]);
            player.CorruptedMemory.Add(CardCatalog.LetterK.Id);
            Check(game.Evolve(0).Success && player.Relikario == RelikarioCatalog.YunoOroLv2 && player.Relikia == 0);
            Choose(game, 0, Five[4]);
            player.Hand.Add(CardCatalog.LetterK.Id);
            Check(game.PlayCode(0, 0, placeLetter: true).Success && game.State.Winner == 0);
        }),
        ("Yuno Oro: el relanzamiento pide dos letras distintas y un 6 natural busca en el mazo", () =>
        {
            var (game, random) = Fixture(1, 2, 6);
            var player = game.State.Active;
            game.State.Phase = Phase.Combat;
            player.Skills.Add(CardCatalog.GoldDigger.Id);
            player.Hand.Add(CardCatalog.AttackYunoOro.Id);
            player.AttachedCards.AddRange(Five[..2]);
            Check(game.Attack(0, 0, 0).Success && game.CanUseRelikarioOnPendingDie(0));
            Check(game.ResolveAttackReroll(0, true).Success && !game.CanUseRelikarioOnPendingDie(0));
            game.State.AttackedThisTurn = false;
            player.Hand.Add(CardCatalog.AttackYunoOro.Id);
            player.Deck.Insert(0, CardCatalog.LetterK.Id);
            game.State.Players[1].AutoPassCombos = true;
            Check(game.Attack(0, 0, 0).Success && player.Hand.Contains(CardCatalog.LetterK.Id));
            Check(game.State.Players[1].Health == 5 && player.Relikia == 500);
        }),
        ("Código Caótico: investigar permite enviar el tope al fondo", () =>
        {
            var (game, _) = Fixture(1);
            var player = game.State.Active;
            player.Hand.Add(CardCatalog.LetterC.Id);
            player.Deck.Insert(0, CardCatalog.LetterK.Id);
            Check(game.PlayCode(0, 0).Success);
            Choose(game, 0, CardCatalog.LetterK.Id, "Enviar al fondo");
            Check(player.Deck[^1] == CardCatalog.LetterK.Id && player.Deck[0] == CardCatalog.AttackYunoOro.Id);
        }),
        ("Código Caótico: la copia de memoria desaparece al salir y la carta original sigue en memoria", () =>
        {
            var (game, _) = Fixture(4);
            var player = game.State.Active;
            player.Hand.Add(CardCatalog.LetterC.Id);
            player.Memory.Add(CardCatalog.LetterK.Id);
            Check(game.PlayCode(0, 0).Success);
            Choose(game, 0, CardCatalog.LetterK.Id);
            Choose(game, 0, CardCatalog.LetterK.Id, "Colocar");
            var instance = player.AttachedCards.Single();
            Check(CardCatalog.Get(instance).Letter == 'K' && CardCatalog.PhysicalId(instance) == null &&
                player.Memory.Count(id => id == CardCatalog.LetterK.Id) == 1);
            game.State.Players[1].Hand.Add(CardCatalog.BlueEgg.Id);
            game.State.CurrentPlayer = 1;
            Check(game.PlayCode(1, 0).Success && player.AttachedCards.Count == 0 &&
                player.Memory.Count(id => id == CardCatalog.LetterK.Id) == 1);
        }),
        ("Código Caótico: una letra directamente encima del Relikario completa RELICK desde cualquier memoria", () =>
        {
            var (game, _) = Fixture(5);
            var player = game.State.Active;
            player.Hand.Add(CardCatalog.LetterC.Id);
            player.AttachedCards.AddRange(Five);
            game.State.Players[1].Memory.Add(CardCatalog.LetterK.Id);
            Check(game.PlayCode(0, 0).Success);
            Check(game.ResolveDieReroll(0, false).Success);
            var host = game.DecisionChoices(0).ToList().FindIndex(c => c.Field is { Owner: 0, Zone: FieldZone.Relikario });
            Check(host >= 0 && game.ResolveDecision(0, host).Success && game.ResolveDecision(0, 0).Success);
            Choose(game, 0, CardCatalog.LetterK.Id);
            Check(game.State.Winner == 0 && game.State.Players[1].Memory.Count == 0 &&
                player.ForeignFieldCards?.Any(id => CardCatalog.Get(id).Letter == 'K') == true);
        }),
        ("Gold Digger: recupera solo cartas del lote, premia la letra y conserva el uso al guardar", () =>
        {
            var (game, random) = Fixture(4);
            var player = game.State.Active;
            player.Skills.Add(CardCatalog.GoldDigger.Id);
            player.Memory.Add(CardCatalog.LetterC.Id);
            player.Deck.InsertRange(0, [CardCatalog.LetterI.Id, CardCatalog.AttackYunoOro.Id]);
            Check(game.UseSkillAction(0, 0).Success);
            Check(game.DecisionChoices(0).All(c => c.CardId != CardCatalog.LetterC.Id));
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Choose(game, 0, CardCatalog.LetterI.Id);
            Check(game.State.Active.Hand.Contains(CardCatalog.LetterI.Id) && game.State.Active.Relikia == 500 &&
                game.State.Active.ActionsLeft == 1 && !game.CanUseSkillAction(0, 0));
        })
    ];

    private static (GameEngine Game, ScriptedRandom Random) Fixture(params int[] dice)
    {
        var random = new ScriptedRandom();
        var game = TestGame.Create(random);
        game.State.CurrentPlayer = 0;
        game.State.Phase = Phase.Actions;
        game.State.Turn = 3;
        foreach (var player in game.State.Players)
        {
            player.Relikario = RelikarioCatalog.YunoOro;
            player.Hand.Clear();
            player.Deck.Clear();
            player.Deck.AddRange(Enumerable.Repeat(CardCatalog.AttackYunoOro.Id, 9));
            player.Dexterity = 0;
        }
        foreach (var die in dice) random.EnqueueDie(die);
        return (game, random);
    }

    private static void Choose(GameEngine game, int actor, string? card, string? button = null)
    {
        if (game.State.Phase != Phase.Decision) return;
        var choices = game.DecisionChoices(actor);
        var index = choices.ToList().FindIndex(c => (card == null || c.CardId == card) && (button == null || c.Button == button));
        Check(index >= 0 && game.ResolveDecision(actor, index).Success);
    }

    private static void Check(bool condition, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    {
        if (!condition) throw new InvalidOperationException($"Yuno Oro · línea {line}");
    }
}
