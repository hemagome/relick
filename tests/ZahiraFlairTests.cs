using Relick.Core;

static class ZahiraFlairTests
{
    public static (string, Action)[] Cases =>
    [
        ("Zahira Flair: solo dos Personajes abren la creación, el destino se recupera y la elección es privada", () =>
        {
            Check(CardCatalog.ZahiraFlair.CodeType == CodeType.Trap && !CardCatalog.TrainingDeck().Contains(CardCatalog.ZahiraFlair.Id));
            var (game, random) = Fixture();
            game.State.Active.Hand.AddRange([CardCatalog.Senshuni.Id, CardCatalog.Attack.Id]);
            Check(game.Exchange(0, 0).Success && game.Exchange(0, 0).Success && game.State.Phase == Phase.Actions);
            Offer(game);
            Check(DuelSnapshot.For(game, 0, 1).TrapCardId == CardCatalog.ZahiraFlair.Id);
            Check(game.ResolveTrapCraft(0, false).Success && game.State.Active.ZahiraAvailable);
            Offer(game);
            Check(game.ResolveTrapCraft(0, true).Success && ZahiraPlacement(game));
            var before = game.SaveSnapshot();
            Check(!game.ResolveZahiraTarget(1, 1, FieldZone.Relikario, -1).Success &&
                  !game.ResolveZahiraTarget(0, 1, FieldZone.Hand, 0).Success && game.SaveSnapshot() == before);
            game = GameEngine.FromSnapshot(before, random);
            Check(DuelOptions.For(game, 1).All(option => option.Move.Kind != DuelMoveKind.Choose));
            var target = DuelOptions.For(game, 0).Single(option => option.Move.Kind == DuelMoveKind.Choose &&
                option.Field is { Owner: 1, Zone: FieldZone.Relikario });
            Check(target.Move.Apply(game, 0).Success && game.EffectsSuppressed(1, FieldZone.Relikario));
            Check(game.State.Active.ActionsLeft == 0 && game.State.Active.Relikia == 0 && !game.State.Active.ZahiraAvailable);
        }),
        ("Zahira Flair: los tres rangos del dado se resuelven en la etapa final rival", () =>
        {
            foreach (var die in Enumerable.Range(1, 6))
            {
                var (game, random) = Fixture();
                Create(game, 1, FieldZone.Relikario);
                Check(game.Advance(0).Success && game.State.CurrentPlayer == 1 && game.State.Players[0].ZahiraHost != null);
                var rival = game.State.Players[1];
                random.EnqueueDie(die);
                game.State.Phase = Phase.Actions;
                Check(game.Advance(1).Success && game.State.LastDie == die);
                if (die <= 2)
                    Check(game.State.CurrentPlayer == 0 && game.State.Players[0].ZahiraHost == null && rival.Health == 8);
                else if (die <= 4)
                {
                    Check(ZahiraPlacement(game) && game.State.DecisionPlayer == 0);
                    Check(!game.ResolveZahiraTarget(0, 1, FieldZone.Relikario, -1).Success);
                    game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
                    Check(game.ResolveZahiraTarget(0, 0, FieldZone.Bonus, -1).Success && game.State.CurrentPlayer == 0);
                    Check(game.EffectsSuppressed(0, FieldZone.Bonus) && !game.EffectsSuppressed(1, FieldZone.Relikario));
                }
                else Check(rival.Health == 6 && game.State.CurrentPlayer == 0 && game.EffectsSuppressed(1, FieldZone.Relikario));
                Check(game.State.Players.All(p => !p.Memory.Contains(CardCatalog.ZahiraFlair.Id)));
            }
        }),
        ("Zahira Flair: el dueño puede modificar el dado y recuperar su decisión tras reconectar", () =>
        {
            var (game, random) = Fixture();
            Create(game, 1, FieldZone.Bonus);
            game.State.Players[0].Dexterity = 1;
            game.State.CurrentPlayer = 1;
            random.EnqueueDie(2);
            Check(game.Advance(1).Success && game.State.Phase == Phase.DieRerollChoice && game.State.DecisionPlayer == 0);
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(game.UseDexterity(0).Success && ZahiraPlacement(game) && game.State.LastDie == 3);
            Check(game.ResolveZahiraTarget(0, 1, FieldZone.Relikario, -1).Success && game.State.CurrentPlayer == 0);
        }),
        ("Zahira Flair: el Bonus destruido se repone y la trampa sale del juego con él", () =>
        {
            var (game, random) = Fixture();
            var rival = game.State.Players[1];
            rival.Bonus = CardCatalog.Jessica.Id;
            Create(game, 1, FieldZone.Bonus);
            game.State.CurrentPlayer = 1;
            game.State.Phase = Phase.Combat;
            Check(!game.FieldCombatTargets(1).Any() && !game.UseFieldCode(1, FieldZone.Bonus).Success);
            Check(game.Advance(1).Success);
            random.EnqueueDie(5);
            Check(game.Advance(1).Success && game.State.CurrentPlayer == 0);
            Check(rival.Bonus == CardCatalog.Attack.Id && rival.Memory.Contains(CardCatalog.Jessica.Id));
            Check(game.State.Active.ZahiraHost == null && !game.State.Active.ZahiraAvailable);
        }),
        ("Zahira Flair: una habilidad cubierta conserva su daño y premio, y pierde su efecto crítico", () =>
        {
            var (game, random) = Fixture();
            var rival = game.State.Players[1];
            rival.Skills.Add(CardCatalog.Magnet.Id);
            rival.Hand.Add(CardCatalog.Attack.Id);
            Create(game, 1, FieldZone.Skill, 0);
            game.State.CurrentPlayer = 1;
            game.State.Phase = Phase.Combat;
            random.EnqueueDie(6);
            Check(game.Attack(1, 0, 0).Success && game.State.Phase == Phase.Combat);
            Check(game.State.Players[0].Health == 3 && rival.Relikia == CardCatalog.Magnet.Reward);
        }),
        ("Zahira Flair: sigue a su carta entre zonas del campo y sale al pasar a memoria", () =>
        {
            var (game, _) = Fixture();
            var rival = game.State.Players[1];
            rival.Bonus = CardCatalog.Jessica.Id;
            Create(game, 1, FieldZone.Bonus);
            game.State.CurrentPlayer = 1;
            rival.Relikia = 100;
            Check(game.UseRelikiaEffect(1, RelikiaEffect.ChangeBonus).Success && game.EffectsSuppressed(1, FieldZone.Exchange, 0));
            rival.Hand.Add(CardCatalog.Attack.Id);
            Check(game.Exchange(1, 0).Success && game.State.Players[0].ZahiraHost == null);
            Check(rival.Memory.Contains(CardCatalog.Jessica.Id) && game.State.Players.All(p => !p.Memory.Contains(CardCatalog.ZahiraFlair.Id)));
        }),
        ("Zahira Flair: cubre una sola copia de un Tótem y destruye esa copia", () =>
        {
            var (game, random) = Fixture();
            var rival = game.State.Players[1];
            rival.Equipment = CardCatalog.Totem.Id;
            rival.EquipmentStackedCopies = 1;
            Create(game, 1, FieldZone.Equipment, 0);
            Check(game.EffectsSuppressed(1, FieldZone.Equipment, 0) && !game.EffectsSuppressed(1, FieldZone.Equipment, 1));
            game.State.CurrentPlayer = 1;
            random.EnqueueDie(6);
            Check(game.Advance(1).Success);
            Check(rival.Equipment == CardCatalog.Totem.Id && rival.EquipmentStackedCopies == 0 && rival.Memory.Single() == CardCatalog.Totem.Id);
            Check(game.State.Players[0].ZahiraHost == null);
        }),
        ("Zahira Flair: Súper Imán puede sustituir la colocación sobre una carta rival", () =>
        {
            var (game, _) = Fixture();
            var rival = game.State.Players[1];
            rival.Equipment = CardCatalog.SuperMagnet.Id;
            Create(game, 1, FieldZone.Bonus);
            Check(rival.EquipmentCounters == 1 && game.State.Active.ZahiraHost == null && !game.State.Active.ZahiraAvailable);
        }),
        ("Zahira Flair: dos copias propias cubren cartas distintas y cada una lanza su dado", () =>
        {
            var (game, random) = Fixture(copies: 2);
            Check(game.State.Players[0] is { ZahiraAvailable: true, ZahiraSpares: 1 });
            Place(game, 0, 1, FieldZone.Relikario);
            Check(game.State.Players[0] is { ZahiraAvailable: true, ZahiraSpares: 0, ZahiraHost2: null });
            Place(game, 0, 1, FieldZone.Bonus);
            var ana = game.State.Players[0];
            Check(ana is { ZahiraAvailable: false, ZahiraHost.Zone: FieldZone.Relikario, ZahiraHost2.Zone: FieldZone.Bonus });
            Check(game.EffectsSuppressed(1, FieldZone.Relikario) && game.EffectsSuppressed(1, FieldZone.Bonus));
            Trade(game, 0);
            Check(game.State.Phase == Phase.Actions);

            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            ana = game.State.Players[0];
            var view = DuelSnapshot.For(game, 1, 1).Players[0];
            Check(view.Zahira(0) == ana.ZahiraHost && view.Zahira(1) == ana.ZahiraHost2 && view.ZahiraHost2 != null);
            Check(game.Advance(0).Success && game.State.CurrentPlayer == 1);
            // First copy deals its damage; the second destroys itself and frees the Bonus.
            random.EnqueueDie(5); random.EnqueueDie(1);
            game.State.Phase = Phase.Actions;
            Check(game.Advance(1).Success && game.State.CurrentPlayer == 0 && game.State.Players[1].Health == 6);
            Check(ana.ZahiraHost != null && ana.ZahiraHost2 == null);
            Check(game.EffectsSuppressed(1, FieldZone.Relikario) && !game.EffectsSuppressed(1, FieldZone.Bonus));

            // The freed place takes the next copy only if one remains: both were already created.
            Trade(game, 0);
            Check(game.State.Phase == Phase.Actions);
        }),
        ("Zahira Flair: cuatro en campo, dos por jugador, y las cubiertas no actúan", () =>
        {
            var (game, random) = Fixture(copies: 2);
            Place(game, 0, 1, FieldZone.Relikario);
            // Beto's own exchanges replace his Bonus, so Ana's second copy goes on a card that stays put.
            Place(game, 0, 0, FieldZone.Bonus);
            game.State.CurrentPlayer = 1;
            Trade(game, 1);
            Check(game.ResolveTrapCraft(1, true).Success && ZahiraPlacement(game));
            Check(game.ZahiraTargets(1).Count(target => target.Zone == FieldZone.Zahira) == 2 &&
                  game.ZahiraTargets(1).All(target => target.Zone != FieldZone.Zahira || target.Owner == 0));
            Check(game.ResolveZahiraTarget(1, 0, FieldZone.Zahira, GameEngine.ZahiraIndex(0)).Success);
            Trade(game, 1);
            Check(game.ResolveTrapCraft(1, true).Success);
            // Both rival copies stay eligible, even the one already covered; one's own copies never are.
            Check(game.ZahiraTargets(1).Count(target => target.Zone == FieldZone.Zahira) == 2);
            Check(!game.ResolveZahiraTarget(1, 1, FieldZone.Zahira, GameEngine.ZahiraIndex(0)).Success);
            Check(game.ResolveZahiraTarget(1, 0, FieldZone.Zahira, GameEngine.ZahiraIndex(1)).Success);

            Check(game.State.Players.All(p => p.ZahiraHost != null && p.ZahiraHost2 != null));
            Check(!game.EffectsSuppressed(1, FieldZone.Relikario) && !game.EffectsSuppressed(0, FieldZone.Bonus));
            Check(game.EffectsSuppressed(0, FieldZone.Zahira, GameEngine.ZahiraIndex(0)) && game.EffectsSuppressed(0, FieldZone.Zahira, GameEngine.ZahiraIndex(1)));

            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            // Ana's covered copies do not roll in Beto's final stage.
            var rolls = game.State.LastDie;
            game.State.Phase = Phase.Actions;
            Check(game.Advance(1).Success && game.State.CurrentPlayer == 0 && game.State.LastDie == rolls);
            // Beto's first copy destroys the Zahira Flair under it and leaves with it; his second destroys itself.
            random.EnqueueDie(6); random.EnqueueDie(2);
            game.State.Phase = Phase.Actions;
            Check(game.Advance(0).Success && game.State.CurrentPlayer == 1);
            var (ana, beto) = (game.State.Players[0], game.State.Players[1]);
            Check(ana.ZahiraHost == null && ana.ZahiraHost2 is { Zone: FieldZone.Bonus } && beto.ZahiraHost == null && beto.ZahiraHost2 == null);
            Check(game.EffectsSuppressed(0, FieldZone.Bonus) && !game.EffectsSuppressed(1, FieldZone.Relikario));
            Check(game.State.Players.All(p => p.Health == 8 && !p.Memory.Contains(CardCatalog.ZahiraFlair.Id)));
        }),
    ];

    private static void Trade(GameEngine game, int actor)
    {
        var p = game.State.Players[actor];
        var index = p.Hand.Count;
        p.Hand.AddRange([CardCatalog.Senshuni.Id, CardCatalog.Jessica.Id]);
        p.ActionsLeft = 2;
        game.State.Phase = Phase.Actions;
        Check(game.Exchange(actor, index).Success && game.Exchange(actor, index).Success);
    }

    private static void Place(GameEngine game, int actor, int owner, FieldZone zone, int index = -1)
    {
        Trade(game, actor);
        Check(game.State.Phase == Phase.TrapCraftChoice && game.ResolveTrapCraft(actor, true).Success &&
              game.ResolveZahiraTarget(actor, owner, zone, index).Success);
    }

    private static bool ZahiraPlacement(GameEngine game) => game.State.Phase == Phase.Decision && game.State.PendingDecision?.Source == CardCatalog.ZahiraFlair.Id;

    private static (GameEngine, ScriptedRandom) Fixture(int copies = 0)
    {
        var random = new ScriptedRandom();
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Ana", "Beto"], Hands = [[], []],
            Decks = [Enumerable.Repeat(CardCatalog.Attack.Id, 16).ToList(), Enumerable.Repeat(CardCatalog.Attack.Id, 16).ToList()],
            Bonuses = [CardCatalog.Attack.Id, CardCatalog.Attack.Id], IncludeZahiraFlair = true,
            Traps = copies == 0 ? null : [Enumerable.Repeat(CardCatalog.ZahiraFlair.Id, copies).ToList(), Enumerable.Repeat(CardCatalog.ZahiraFlair.Id, copies).ToList()]
        }, random);
        game.State.Phase = Phase.Actions;
        game.State.Turn = 2;
        foreach (var player in game.State.Players)
        {
            player.Dexterity = 0;
            player.AttackRerollUsedThisTurn = true;
        }
        return (game, random);
    }

    private static void Offer(GameEngine game)
    {
        var p = game.State.Active;
        var index = p.Hand.Count;
        p.Hand.AddRange([CardCatalog.Senshuni.Id, CardCatalog.Jessica.Id]);
        p.ActionsLeft = 2;
        Check(game.Exchange(0, index).Success && game.Exchange(0, index).Success && game.State.Phase == Phase.TrapCraftChoice);
    }

    private static void Create(GameEngine game, int owner, FieldZone zone, int index = -1)
    {
        Offer(game);
        Check(game.ResolveTrapCraft(0, true).Success && game.ResolveZahiraTarget(0, owner, zone, index).Success);
    }

    private static void Check(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Resultado inesperado al resolver Zahira Flair.");
    }
}
