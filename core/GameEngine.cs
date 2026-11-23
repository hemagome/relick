namespace Relick.Core;

/// <summary>
/// Pure C# rules: no Godot, network, timers or UI. Commands validate ownership and phase
/// before mutating. The server supplies the random source and calls the same rules.
/// </summary>
public sealed partial class GameEngine
{
    public static IReadOnlyList<int> SkillSlotCosts { get; } = Array.AsReadOnly(new[] { 0, 300, 500, 1000 });
    public static int MaxSkills => SkillSlotCosts.Count;
    public const int MaxHandSize = 7;
    private readonly IGameRandom _random;
    public GameState State { get; } = new();
    internal string? PublicEndReason { get; private set; }
    public event Action<int>? AttackPerformed;
    public event Action<int, int, int>? DamageReceived;
    public event Action<int, int>? HealingReceived;
    public event Action<int>? DieRolled;
    public event Action<int>? DieAdjusted;
    public event Action<int, int>? CardDrawn;
    public event Action<int, int, string>? CardReturnedToDeck;
    // Signals the end of a batch for the deck animation; does not change deck order.
    public event Action<int>? CardsReturnedToDeck;
    public event Action<int, string>? BonusPlaced;
    public event Action<int, int, int, string>? BonusClaimed;

    public GameEngine(int seed) : this(new SeededRandom(seed)) { }

    private GameEngine(GameState state, IGameRandom random)
    {
        State = state;
        _random = random;
    }

    public static GameEngine FromOpening(DuelOpening opening, IGameRandom random)
    {
        var state = new GameState { Phase = Phase.Combat, Turn = 1, CurrentPlayer = opening.StartingSeat };
        for (var seat = 0; seat < 2; seat++)
        {
            var player = new PlayerState(RelikarioCatalog.Find(opening.Relikarios?.ElementAtOrDefault(seat))) { Name = opening.Names[seat], Bonus = opening.Bonuses[seat],
                SuperMagnetCopies = Math.Clamp(opening.SuperMagnetCopies?.ElementAtOrDefault(seat) ?? 0, 0, 2),
                ZahiraAvailable = opening.IncludeZahiraFlair, SuperAttackAvailable = opening.IncludeSuperAttack,
                Sleeve = CosmeticCatalog.Sleeve(opening.Sleeves?.ElementAtOrDefault(seat)).Id is var sleeve && sleeve != CosmeticCatalog.DefaultSleeve ? sleeve : null,
                Dice = CosmeticCatalog.DiceFor(opening.Dice?.ElementAtOrDefault(seat)).Id is var dice && dice != CosmeticCatalog.DefaultDice ? dice : null };
            if (opening.Traps?.ElementAtOrDefault(seat) is { } traps) LoadTraps(player, traps);
            if (player.Bonus is { } bonus && CardCatalog.Get(bonus).FieldCombatDie) player.BonusPlacedTurn = 1;
            player.Hand.AddRange(opening.Hands[seat]);
            player.Deck.AddRange(opening.Decks[seat]);
            state.Players[seat] = player;
        }
        var game = new GameEngine(state, random);
        game.PromoteInitialAreas();
        if (game.HasTimeBenderForDraw()) state.Phase = Phase.Draw;
        game.PresentPendingArea();
        return game;
    }

    public CommandResult Concede(int actor)
    {
        if (actor is < 0 or > 1 || State.IsOver) return CommandResult.Fail("La partida terminó.");
        Finish(1 - actor, $"{State.Players[actor].Name} abandona el duelo.");
        return CommandResult.Ok();
    }

    /// <summary>Ends the duel against <paramref name="actor"/> for a reason outside the rules, such as the clock of a rated duel.</summary>
    public CommandResult Forfeit(int actor, string reason)
    {
        if (actor is < 0 or > 1 || State.IsOver) return CommandResult.Fail("La partida terminó.");
        Finish(1 - actor, reason);
        return CommandResult.Ok();
    }

    public GameEngine(IGameRandom random)
    {
        _random = random;
        foreach (var player in State.Players)
        {
            player.Deck.AddRange(CardCatalog.TrainingDeck());
            player.SuperMagnetCopies = TrainingSuperMagnetCopies;
            player.ZahiraAvailable = true;
            player.SuperAttackAvailable = true;
        }
        Log("Preparación: ambos relikarios empiezan con 8 vidas.");
        Log("Elige una habilidad de tu mazo para formar tu mano inicial.");
    }

    public CommandResult Start(IEnumerable<int> mulligan)
    {
        if (State.Phase != Phase.Preparation) return CommandResult.Fail("La partida ya empezó.");
        if (State.Players.Any(p => p.Hand.Count != 5)) return CommandResult.Fail("Ambos jugadores deben elegir su habilidad inicial antes del mulligan.");
        var indices = mulligan.Distinct().OrderByDescending(i => i).ToArray();
        var player = State.Players[0];
        if (indices.Any(i => i < 0 || i >= player.Hand.Count))
            return CommandResult.Fail("Selección de cartas inválida.");
        // Deck[0] is the top; returned cards go to the bottom before drawing replacements.
        foreach (var index in indices)
        {
            CardReturnedToDeck?.Invoke(0, index, player.Hand[index]);
            player.Deck.Add(player.Hand[index]);
            player.RemoveHandCard(index);
        }
        if (indices.Length > 0) CardsReturnedToDeck?.Invoke(0);
        foreach (var _ in indices) DrawOne(player);
        if (indices.Length > 0) Log($"{player.Name} cambia {indices.Length} carta(s) de su mano inicial.");
        foreach (var p in State.Players) DealBonus(p);
        PromoteInitialAreas();
        int first, second;
        do { first = Roll(); second = Roll(); } while (first == second);
        State.CurrentPlayer = first > second ? 0 : 1;
        Log($"Dado inicial: {State.Players[0].Name} {first}, {State.Players[1].Name} {second}. Empieza {State.Active.Name}.");
        BeginTurn();
        return CommandResult.Ok();
    }

    public int SkillCost(PlayerState player) => player.NextSkillPosition() is var position && position >= 0 ? SkillSlotCosts[position] : int.MaxValue;

    /// <summary>Read-only query for plays other than advancing; never consumes cards or randomness.</summary>
    public bool HasAvailablePlay(int actor)
    {
        if (State.Phase is Phase.DestructionResponse or Phase.TurnBuffDiscard or Phase.EffectDamageChoice or Phase.TrapCraftChoice or Phase.Decision) return actor == State.DecisionPlayer;
        if (State.Phase == Phase.Discard)
            return !State.IsOver && actor == State.DecisionPlayer && State.DiscardsRemaining > 0 && State.Players[actor].Hand.Count > 0;
        if (State.Phase is Phase.DefenseChoice or Phase.MagnetChoice or Phase.AttackRerollChoice or Phase.DieRerollChoice or Phase.CodeResponse or Phase.ErrorResponse or Phase.SniperChoice)
            return actor == State.DecisionPlayer;
        if (ValidateTurn(actor) != null) return false;
        var p = State.Active;
        if (PlayableCards(actor).Any(card => CardCatalog.Get(card.Id).IsCombo && CodeUnavailableReason(actor, card.Index) == null)) return true;
        if (CanEvolve(actor)) return true;
        if (AvailablePurpleRings(actor).Any(c => CanUsePurpleRing(actor, c.Index))) return true;
        if (IronwingFireDestructions(actor).Any()) return true;
        return State.Phase switch
        {
            Phase.Combat => FieldCombatTargets(actor).Any() ||
                CanAttack(actor) ||
                (!State.LearnedThisTurn && p.Skills.Count < MaxSkills && p.Relikia >= SkillCost(p) &&
                 PlayableCards(actor).Any(entry => CanLearnSkill(p, CardCatalog.Get(entry.Id)))),
            Phase.Actions => (AvailableActions(actor) > 0 && PlayableCards(actor).Any(c => p.Bonus != null || CardCatalog.Get(c.Id).Kind == CardKind.Equipment ||
                CanActivateArea(actor, c.Index) || CodeUnavailableReason(actor, c.Index) == null || CanRevealGhostDog(actor, c.Index))) ||
                CanUseOsamuSearch(actor) || DestinyPickMemorySlots(actor).Any() || RelikiaEffects.All.Any(e => CanUseRelikiaEffect(actor, e.Id)),
            Phase.Discard => State.DiscardsRemaining > 0 && p.Hand.Count > 0,
            Phase.Final => p.Hand.Count > MaxHandSize,
            Phase.RelikiaChoice => true,
            _ => false
        };
    }

    public CommandResult Learn(int actor, int handIndex)
    {
        var error = ValidateTurn(actor, Phase.Combat);
        if (error != null) return CommandResult.Fail(error);
        var p = State.Active;
        if (PlayableHandCard(p, handIndex) is not { } id) return CommandResult.Fail("Elige una carta que puedas usar.");
        if (!CanLearnSkill(p, CardCatalog.Get(id))) return CommandResult.Fail("Elige una habilidad compatible con tu Relikario.");
        if (State.LearnedThisTurn)
            return CommandResult.Fail("Solo puedes enseñar una habilidad por turno.");
        if (p.Skills.Count >= MaxSkills) return CommandResult.Fail("Los cuatro espacios de habilidad están ocupados.");
        var cost = SkillCost(p);
        var position = p.NextSkillPosition();
        if (p.Relikia < cost) return CommandResult.Fail($"Necesitas {cost} PR para enseñar otra habilidad.");
        p.Relikia -= cost;
        MoveHandCardToField(p, handIndex, CardZone.Skill, p.Skills.Count);
        p.Skills.Add(id);
        if (p.SkillPositions != null || position != p.Skills.Count - 1)
        {
            p.SkillPositions ??= new();
            p.SkillPositions[p.Skills.Count - 1] = position;
        }
        State.LearnedThisTurn = true;
        Log($"{p.Name} enseña {CardCatalog.Get(id).Name} ({cost} PR).");
        if (TryFinishWithGameOver()) return CommandResult.Ok();
        if (CardCatalog.Get(id).DrawOnExpensiveLearn && cost is 500 or 1000)
            RollWithRelikario(p, DiePurpose.SkillLearning, new DieEffect { Purpose = DiePurpose.SkillLearning, Actor = actor });
        if (id == CardCatalog.TurboOverlap.Id)
            OfferOsamuSearch(actor, id, OsamuAgilitySearch);
        return CommandResult.Ok();
    }

    internal static bool CanLearnSkill(PlayerState player, CardDefinition card) =>
        card.Kind == CardKind.Skill && DeckRules.Compatible(card, player.Relikario.Family);

    private string? AttackUnavailableReason(int actor)
    {
        var error = ValidateTurn(actor, Phase.Combat);
        if (error != null) return error;
        if (State.Turn <= 1) return "Quien inicia la partida no puede atacar en su primer turno.";
        return State.AttackedThisTurn ? "Ya atacaste este turno." : null;
    }

    public bool CanAttack(int actor) => AttackUnavailableReason(actor) == null &&
        State.Active.Skills.Any(id => CanAttackWithTaughtSkill(State.Active, id)) &&
        PlayableCards(actor).Any(card => CardCatalog.Get(card.Id).Kind == CardKind.Attack);

    public CommandResult Attack(int actor, int handIndex, int skillIndex)
    {
        var error = AttackUnavailableReason(actor);
        if (error != null) return CommandResult.Fail(error);
        var p = State.Active;
        if (PlayableHandCard(p, handIndex) is not { } id || CardCatalog.Get(id).Kind != CardKind.Attack)
            return CommandResult.Fail("Selecciona una carta de ataque.");
        if (skillIndex < 0 || skillIndex >= p.Skills.Count)
            return CommandResult.Fail("Primero enseña y selecciona una habilidad.");
        if (!CanAttackWithTaughtSkill(p, p.Skills[skillIndex]))
            return CommandResult.Fail("Selecciona una habilidad compatible con tu Relikario.");
        var skill = CardCatalog.Get(p.Skills[skillIndex]);
        var suppressed = EffectsSuppressed(actor, FieldZone.Skill, skillIndex);
        var superAttack = id == CardCatalog.SuperAttack.Id;
        SendHandToMemory(p, handIndex);
        State.AttackedThisTurn = true;
        var die = RollAttack(out var modified, superAttack: superAttack);
        State.LastAttackDie = die;
        State.PendingAttackRoll = new(actor, skill.Id, die, Modified: modified,
            SkillSlot: skill.DamagePerReroll > 0 && !suppressed ? skillIndex : null,
            NormalDamage: skill.DamagePerReroll > 0 && !suppressed ? skill.NormalDamageAt(p.CountersOnSkill(skillIndex)) : null,
            CriticalDamage: skill.DamagePerReroll > 0 && !suppressed ? skill.CriticalDamageAt(p.CountersOnSkill(skillIndex)) : null,
            EffectsSuppressed: suppressed, SuperAttack: superAttack);
        State.Phase = Phase.AttackRerollChoice;
        State.LastCombat = p.Relikario.Family == RelikarioCatalog.Ironwing.Id || p.Relikario.Family == RelikarioCatalog.Wd.Id
            ? $"{p.Name} · Dado de ataque {die} · Conserva o modifica el resultado"
            : $"{p.Name} · Dado de ataque {die} · Decide si relanza con {p.Relikario.DisplayName}";
        ProcessAttackDie(die, new EngineContinuation { Kind = ContinuationKind.AttackDieReady, Actor = actor });
        return CommandResult.Ok();
    }

    public static int DamageFor(int die, int normal, int critical) => die switch
    {
        4 => (normal + 1) / 2,
        5 => normal,
        6 => critical,
        _ => 0
    };

    public CommandResult PlayCode(int actor, int handIndex)
    {
        var error = CodeUnavailableReason(actor, handIndex);
        if (error != null) return CommandResult.Fail(error);
        var p = State.Players[actor];
        var card = CardCatalog.Get(PlayableHandCard(p, handIndex)!);
        if (card.Id == CardCatalog.CopyHelmet.Id) return PlayCopyHelmet(actor, handIndex);
        if (CardEffects.For(card.Id) is CardEffect { HoldsActivationCard: true } ||
            card.Id == CardCatalog.MagicTome.Id || card.Id == CardCatalog.GalacticShield.Id || handIndex >= TomeHandBase &&
            (card.Id == CardCatalog.Robotize.Id || card.Id == CardCatalog.GoldenTotem.Id))
        {
            var frame = new EngineActivation(actor, card.Id, card.ActionCost, CodePrCost(card), State.Phase);
            HoldActivationCard(frame, handIndex);
            p.ActionsLeft -= frame.Actions;
            p.Relikia -= frame.Pr;
            Log($"{p.Name} juega {card.Name}.");
            BeginActivation(frame);
            return CommandResult.Ok();
        }
        // Resetear la Mente keeps its own activation: no PR cost and no "juega" line, as before it became a module.
        if (card.Id == CardCatalog.MindReset.Id) return PlayMindReset(actor, handIndex);
        if (card.CancelActivation) return PlayLoadError(actor, handIndex);
        if (card.RerollOwnDie || card.SetOwnDie > 0) return PlayRerollCombo(actor, handIndex);
        var deferCorruption = (card.Id == CardCatalog.GoldenTotem.Id || card.Id == CardCatalog.Robotize.Id) &&
            p.CorruptsOnMemory(CardZone.Hand, handIndex);
        // Robotizar waits in memory like an equipment: it announces its flight when it lands or when it is canceled.
        var robotize = card.Id == CardCatalog.Robotize.Id;
        var played = robotize ? SendToMemory(p, card.Id, CardZone.Hand, handIndex, notify: false, deferCorruption: deferCorruption)
            : SendHandToMemory(p, handIndex, deferCorruption);
        if (robotize) p.RemoveHandCard(handIndex);
        p.ActionsLeft -= card.ActionCost;
        p.Relikia -= CodePrCost(card);
        Log($"{p.Name} juega {card.Name}.");
        ActivateCode(actor, card, false, played?.Index ?? -1, played?.Owner, deferCorruption);
        return CommandResult.Ok();
    }

    private void ResolveCode(PlayerState p, CardDefinition card)
    {
        if (TryResolveWithEffect(p, card)) return;
        if (card.RepeatStage) { ResolveRepeatedStage(); return; }
        if (card.DiscardForTurnBuff) { ResolveTurnBuff(); return; }
        // Plain heal and draw data, used by the test fixtures; every real code has its own rules.
        if (card.Heal > 0) Heal(p, card.Heal);
        if (_activeActivation != null) _activeActivation.CanInterrupt = false;
        DrawCards(p, card.Draw);
    }

    private int Heal(PlayerState p, int amount)
    {
        var old = p.Health;
        p.Health = Math.Min(p.MaxHealth, p.Health + amount);
        var restored = p.Health - old;
        Log($"{p.Name} recupera {restored} vida(s).");
        if (restored > 0) HealingReceived?.Invoke(Array.IndexOf(State.Players, p), restored);
        return restored;
    }

    public CommandResult Exchange(int actor, int handIndex)
    {
        var error = ValidateAction(actor);
        if (error != null) return CommandResult.Fail(error);
        var p = State.Active;
        if (PlayableHandCard(p, handIndex) == null) return CommandResult.Fail("Elige una carta que puedas usar.");
        if (p.Bonus == null) return CommandResult.Fail("No hay una carta de Bonus disponible.");
        var id = MoveHandCardToField(p, handIndex, CardZone.Exchange, p.Exchange.Count);
        State.ActionsLeft--;
        PlaceInExchange(p, id);
        return CommandResult.Ok();
    }

    private void PlaceInExchange(PlayerState p, string id, EngineContinuation? completed = null, int? fieldPlacedTurn = null)
    {
        AddToExchange(p, id, fieldPlacedTurn);
        CompleteExchange(p, completed);
    }

    private void AddToExchange(PlayerState p, string id, int? fieldPlacedTurn = null)
    {
        p.Exchange.Add(id);
        if (CardCatalog.Get(id).FieldCombatDie)
        {
            p.ExchangePlacementTurns ??= new();
            p.ExchangePlacementTurns[p.Exchange.Count - 1] = fieldPlacedTurn ?? Math.Max(1, State.Turn);
        }
        Log($"{p.Name} coloca {CardCatalog.Get(id).Name} en canje ({p.Exchange.Count}/2).");
        QueueExchangeTrigger(Seat(p), id, ExchangeMoment.EnteredExchange, FieldZone.Exchange, p.Exchange.Count - 1);
    }

    private void CompleteExchange(PlayerState p, EngineContinuation? completed = null)
    {
        var actor = Array.IndexOf(State.Players, p);
        if (State.PendingSniper?.Actor == actor) { Continue(completed); return; }
        if (p.Exchange.Count >= 2 && p.Bonus != null)
        {
            QueueTrapCraft(actor);
            for (var slot = 0; slot < 2; slot++)
            {
                ForgetFieldCodeSource(p, FieldZone.Exchange, slot);
                SendToMemory(p, p.Exchange[slot], CardZone.Exchange, slot);
            }
            ChangeZahiraHosts(actor, FieldZone.Exchange, 0, 2);
            p.Exchange.RemoveRange(0, 2);
            p.RemoveCorruptionSlots(CardZone.Exchange, 0, 2);
            ShiftExchangeEffects(p, 0, 2);
            TakeBonus(p, p);
            Log($"{p.Name} completa el canje y obtiene su bonus.");
            RefillBonus(p, new EngineContinuation { Kind = ContinuationKind.CompleteExchange, Actor = actor, Next = completed }, fromExchange: true);
        }
        else Continue(completed);
    }

    public CommandResult Advance(int actor)
    {
        var error = ValidateTurn(actor);
        if (error != null) return CommandResult.Fail(error);
        if (State.Phase == Phase.Draw)
        {
            if (!ConsumeRepeatedStage(Phase.Draw)) State.Phase = Phase.Combat;
        }
        else if (State.Phase == Phase.Combat)
        {
            if (ConsumeRepeatedStage(Phase.Combat)) return CommandResult.Ok();
            State.Phase = Phase.Actions;
            Log($"{State.Active.Name} entra en acciones ({AvailableActions(actor)} disponibles).");
            BeginActionStartEffects(actor);
        }
        else if (State.Phase == Phase.Actions)
        {
            if (!ConsumeRepeatedStage(Phase.Actions)) EndTurn();
        }
        else if (State.Phase == Phase.Final && State.Active.Hand.Count <= MaxHandSize)
        {
            State.RepeatedFinalAwaitingAdvance = false; CompleteEndTurnIfReady();
        }
        else return CommandResult.Fail("Completa el efecto pendiente antes de continuar.");
        return CommandResult.Ok();
    }

    public CommandResult Discard(int actor, int handIndex)
    {
        var handLimit = State.Phase == Phase.Final;
        if ((!handLimit && State.Phase != Phase.Discard) || actor != State.DecisionPlayer)
            return CommandResult.Fail("No te corresponde descartar ahora.");
        var p = State.Players[actor];
        if (handLimit ? p.Hand.Count <= MaxHandSize : State.DiscardsRemaining <= 0)
            return CommandResult.Fail("No tienes descartes pendientes.");
        if (PlayableHandCard(p, handIndex) is not { } id) return CommandResult.Fail("Elige una carta que puedas usar.");
        var discarded = DiscardHandCard(p, handIndex);
        Log($"{p.Name} descarta {CardCatalog.Get(id).Name}.");
        if (!handLimit)
        {
            State.DiscardsRemaining--;
            if (p.Hand.Count == 0) State.DiscardsRemaining = 0;
            if (State.DiscardsRemaining == 0)
            {
                State.Phase = State.DiscardResumePhase;
                State.DiscardPlayer = null;
            }
        }
        if (CardCatalog.Get(id).EquipOnDiscard && discarded != null) BeginSniperChoices(actor, [discarded]);
        CompleteActivationIfFinished();
        return CommandResult.Ok();
    }

    private void BeginTurn()
    {
        State.Turn++;
        State.ExtraDrawStages = 0; State.ExtraCombatStages = 0; State.ExtraActionStages = 0; State.ExtraFinalStages = 0;
        State.RepeatedFinalAwaitingAdvance = false;
        foreach (var player in State.Players)
        {
            player.LockedGhostCards = null;
            player.AttackRerollUsedThisTurn = false;
            player.WildDriveRedirects = 0;
            player.RelikarioSearchUsedThisTurn = false;
            player.BonusEffectUsed = false; player.ExchangeEffectsUsed = null; player.IronwingFireUsed = null;
        }
        State.Phase = Phase.Combat;
        foreach (var player in State.Players) player.ActionsLeft = 2;
        State.AttackedThisTurn = false;
        State.LearnedThisTurn = false;
        if (State.Active.EquipmentExpiresAtTurnStart)
        {
            State.Active.EquipmentExpiresAtTurnStart = false;
            if (ActiveEquipment(State.CurrentPlayer) != null)
            {
                DestroyEquipment(State.Active, new EngineContinuation { Kind = ContinuationKind.BikeTurnStart, Actor = State.CurrentPlayer });
                return;
            }
        }
        BeginTurnStartTriggers();
    }

    private void DrawAtTurnStart(bool explicitStage = false, int extraCards = 0)
    {
        if (explicitStage) State.Phase = Phase.Draw;
        var count = (State.Active.Hand.Count == 0 ? 3 : 1) + extraCards;
        Log($"Turno {State.Turn} · {State.Active.Name}: roba {count} carta(s).");
        DrawCards(State.Active, count, new EngineContinuation { Kind = ContinuationKind.DrawStageReady, Actor = State.CurrentPlayer, Flag = explicitStage });
    }

    private void EndTurn()
    {
        CompleteActivationIfFinished();
        State.Phase = Phase.Final;
        State.ZahiraRolledThisFinal = false; State.ZahiraFinalSlot = 0;
        if (State.Active.Hand.Count > MaxHandSize)
            Log($"{State.Active.Name} debe descartar hasta tener {MaxHandSize} cartas en la mano.");
        CompleteEndTurnIfReady();
    }

    private void CompletePendingPlay()
    {
        if (TryRunDiscardEffects() || TryFinishWdPayment()) return;
        if (FinishPlayedTargetResponse()) return;
        if (_activeActivation == null && _ramResolutions.LastOrDefault() is { } ram &&
            State.Phase == ram.PlayPhase)
        {
            ResumeRamResolution();
            return;
        }
        // An interrupt can spend or remove the last option before returning to the pending die.
        if (ResolveUnopposedDie()) return;
        if (TryContinueAttackResolution()) return;
        if (TryPresentExchangeTrigger()) return;
        // With nothing left to fire, the pending play goes on below, as it does when a trigger finishes by continuation.
        if (_activeActivation == null && State is { Phase: Phase.Actions, ActionStartActor: int actor } &&
            RunTurnTriggers(TurnMoment.ActionStart, actor)) return;
        if (!PresentPendingArea() && !TryPresentTrapCraft()) CompleteEndTurnIfReady();
    }

    private void CompleteEndTurnIfReady()
    {
        // Discard triggers, responses and their draws finish before checking the limit again.
        if (State.Phase != Phase.Final || State.RepeatedFinalAwaitingAdvance || _activeActivation != null || State.PendingSniper != null || State.PendingTurnBuff != null ||
            State.Active.Hand.Count > MaxHandSize) return;
        if (RunTurnTriggers(TurnMoment.FinalStage, State.CurrentPlayer)) return;
        if (ConsumeRepeatedStage(Phase.Final))
        {
            State.ZahiraRolledThisFinal = false; State.ZahiraFinalSlot = 0;
            return;
        }
        State.ZahiraRolledThisFinal = false; State.ZahiraFinalSlot = 0;
        foreach (var player in State.Players) player.StarEffects = 0;
        State.CurrentPlayer = 1 - State.CurrentPlayer;
        BeginTurn();
    }

    private void DealDamage(int target, int amount, int attackDie = 0, EngineContinuation? completed = null, int? source = null) =>
        ResumeDamage(target, amount, attackDie, completed, DamageStep.Amplify, source);

    /// <summary>
    /// Runs the damage chain from <paramref name="from"/>. A step that pauses (a die, a decision, a destruction) takes
    /// over and resumes the chain later from the next step; one that leaves no damage ends it with <paramref name="completed"/>.
    /// <paramref name="source"/> only matters from the first step: Estrella del Fontanero of the attacker and the guard feedback.
    /// </summary>
    private void ResumeDamage(int target, int amount, int attackDie, EngineContinuation? completed, DamageStep from, int? source = null)
    {
        var p = State.Players[target];
        int? attacker = attackDie > 0 ? source : null;
        for (var step = from; step <= DamageStep.Lethal; step++)
        {
            // From Turbo Sword on, nothing left to receive ends the chain before the step.
            if (step >= DamageStep.Respond && amount <= 0) { Continue(completed); return; }
            switch (step)
            {
                case DamageStep.Amplify:
                    if (amount <= 0 || LimitNonlethalDamage(target, amount, completed) == 0) { Continue(completed); return; }
                    if (source is int owner && State.Players[owner].StarEffects > 0)
                    {
                        var original = amount;
                        for (var i = 0; i < State.Players[owner].StarEffects; i++) amount *= 2;
                        Log($"Estrella del Fontanero aumenta el daño de {State.Players[owner].Name} de {original} a {amount}.");
                        State.LastCombat += $" · Estrella del Fontanero: {amount} de daño";
                    }
                    break;
                case DamageStep.Prevent:
                    if (PreventDamageWithStar(p, amount, completed, attacker)) return;
                    break;
                case DamageStep.Counters:
                    amount = LimitNonlethalDamage(target, amount, completed);
                    if (amount == 0) { Continue(completed); return; }
                    break;
            }
            foreach (var card in CardEffects.DamageTriggers(step))
            {
                var trigger = (IDamageTrigger)card;
                var ctx = EffectFor(target, card.CardId);
                amount = trigger.Modify(ctx, new DamageEvent(target, amount, attackDie));
                if (trigger.TryPause(ctx, new DamageEvent(target, amount, attackDie), completed)) return;
            }
        }
        ApplyHealthDamage(target, amount, attackDie, completed);
    }

    private bool PreventDamageWithStar(PlayerState player, int amount, EngineContinuation? completed, int? attacker = null)
    {
        if (player.StarEffects == 0) return false;
        NotifyGuard(Seat(player), attacker, CardCatalog.PlumberStar.Id, amount, 0, blocked: true);
        Log($"{player.Name} transforma {amount} de daño en 0 con Estrella del Fontanero.");
        State.LastCombat += " · Estrella del Fontanero: 0 daño recibido";
        Continue(completed);
        return true;
    }

    private void ApplyHealthDamage(int target, int amount, int attackDie, EngineContinuation? completed)
    {
        amount = LimitNonlethalDamage(target, amount, completed);
        if (amount == 0) { Continue(completed); return; }
        var p = State.Players[target];
        var wasZero = p.Health == 0;
        p.Health = Math.Max(0, p.Health - amount);
        DamageReceived?.Invoke(target, amount, attackDie);
        if (wasZero || (p.Health == 0 && p.HasReachedZero))
        {
            Finish(1 - target, $"{p.Name} recibe el daño definitivo.");
            State.PendingAttack = null;
            return;
        }
        if (p.Health == 0)
        {
            p.HasReachedZero = true;
            Log($"{p.Name} queda en 0 vidas: un nuevo daño terminará la partida.");
        }
        Continue(completed);
    }

    private int LimitNonlethalDamage(int target, int amount, EngineContinuation? completed)
    {
        if (completed?.NonlethalDamageTarget != target) return amount;
        var player = State.Players[target];
        // The first drop to zero leaves the player in the duel.
        return Math.Min(amount, Math.Max(0, player.Health - (player.HasReachedZero ? 1 : 0)));
    }

    private void DrawCards(PlayerState p, int count, EngineContinuation? completed = null)
    {
        if (State.IsOver) return;
        if (count <= 0) { Continue(completed); return; }
        DrawOne(p, new EngineContinuation { Kind = ContinuationKind.Draw, Actor = Array.IndexOf(State.Players, p), Count = count - 1, Next = completed });
    }
    private void DrawOne(PlayerState p, EngineContinuation? completed = null) => MoveTopCard(p, false, completed);
    private void MoveTopCard(PlayerState p, bool bonus, EngineContinuation? completed = null)
    {
        if (State.IsOver) return;
        var actor = Array.IndexOf(State.Players, p);
        if (p.Deck.Count == 0)
        {
            Exhaust(p, new EngineContinuation { Kind = bonus ? ContinuationKind.MoveBonus : ContinuationKind.MoveHand, Actor = actor, Next = completed });
            return;
        }
        MoveDeckCard(p, 0, bonus, completed);
    }
    private void MoveDeckCard(PlayerState p, int index, bool bonus, EngineContinuation? completed = null, bool mark = false)
    {
        var actor = Array.IndexOf(State.Players, p);
        var id = p.Deck[index];
        if (mark || p.CorruptsOnMemory(CardZone.Deck, index))
            p.MarkForCorruption(bonus ? CardZone.Bonus : CardZone.Hand, bonus ? -1 : p.Hand.Count);
        if (bonus) PlaceBonus(p, id);
        else { p.Hand.Add(id); CardDrawn?.Invoke(actor, p.Hand.Count - 1); }
        p.Deck.RemoveAt(index);
        p.RemoveCorruptionSlots(CardZone.Deck, index);
        if (p.Deck.Count == 0 && State.Phase != Phase.Preparation) Exhaust(p, completed);
        else Continue(completed);
    }

    private void PlaceBonus(PlayerState p, string id, bool fromDeck = true)
    {
        ForgetFieldCodeSource(p, FieldZone.Bonus);
        p.Bonus = id;
        p.InitialAreaWaitingInBonus = false;
        if (CardCatalog.Get(id).FieldCombatDie) p.BonusPlacedTurn = Math.Max(1, State.Turn);
        if (fromDeck) BonusPlaced?.Invoke(Seat(p), id);
    }
    private void Exhaust(PlayerState p, EngineContinuation? completed = null)
    {
        if (!p.Recycled)
        {
            p.Recycled = true;
            RollWithRelikario(p, DiePurpose.Recycling, new DieEffect { Purpose = DiePurpose.Recycling, Actor = Array.IndexOf(State.Players, p), Next = completed });
            return;
        }
        // This is unavoidable life loss followed by a comparison, not attack damage.
        // Equipment and the ordinary defeat-on-damage rule must not change the result.
        p.Health = Math.Max(0, p.Health - 2);
        if (p.Health == 0) p.HasReachedZero = true;
        DamageReceived?.Invoke(Array.IndexOf(State.Players, p), 2, 0);
        var a = State.Players[0].Health; var b = State.Players[1].Health;
        Finish(a == b ? -1 : a > b ? 0 : 1,
            $"{p.Name} agota su mazo por segunda vez y pierde 2 vidas inevitables. Se comparan las vidas restantes.");
    }

    private void Finish(int winner, string reason)
    {
        PublicEndReason = reason; // Finish callers supply only a public outcome, never diagnostic log entries.
        State.Winner = winner;
        State.Phase = Phase.Finished;
        Log(reason);
        Log(winner == -1 ? "Empate." : $"Victoria: {State.Players[winner].Name}.");
    }

    private string? ValidateTurn(int actor, Phase? phase = null)
    {
        if (State.IsOver) return "La partida terminó.";
        if (State.Phase == Phase.Preparation) return "Primero confirma tu mano inicial.";
        if (actor != State.CurrentPlayer) return "Es el turno del rival.";
        if (phase != null && State.Phase != phase) return "Esta jugada no corresponde a la etapa actual.";
        return null;
    }

    private string? ValidateAction(int actor) =>
        ValidateTurn(actor, Phase.Actions) ?? (AvailableActions(actor) <= 0 ? "No quedan acciones disponibles." : null);

    private static bool InHand(PlayerState p, int index) => index >= 0 && index < p.Hand.Count;
    public string CurrentDieCaption { get; private set; } = "";
    private int RollAttack(out bool modified, string caption = "", bool convertSix = false, bool superAttack = false)
    {
        var die = Roll(caption, convertSix);
        var relikario = State.Active.Relikario;
        var bonus = EffectsSuppressed(State.CurrentPlayer, FieldZone.Relikario) ? 0 :
            die == 1 ? relikario.AttackDieBonusIncludingOne : relikario.AttackDieBonus;
        modified = bonus > 0 && die < 6;
        if (modified)
        {
            var previous = die;
            die = Math.Min(6, die + bonus);
            State.LastDie = die;
            CurrentDieCaption = $"{State.Active.Relikario.Name} Lv {State.Active.Level} · +{bonus} al ataque";
            DieAdjusted?.Invoke(die);
            Log($"{State.Active.Name} suma +{bonus} por {State.Active.Relikario.Name} Lv {State.Active.Level} al dado de ataque: {previous} → {die}.");
        }
        if (superAttack && die < 5)
        {
            var previous = die;
            die = Math.Min(5, die + 2);
            modified = true;
            State.LastDie = die;
            CurrentDieCaption = $"Súper Ataque · +{die - previous}";
            DieAdjusted?.Invoke(die);
            Log($"{State.Active.Name} modifica el dado con Súper Ataque: {previous} → {die}.");
        }
        return die;
    }
    /// <summary>Whose die the latest roll was, for presentation only: the roller's cosmetic die is shown.</summary>
    public int CurrentDieRoller { get; private set; } = -1;
    internal bool CompletesDiePair { get; private set; }
    internal bool CurrentDieIsD10 { get; private set; }
    private int Roll(string caption = "", bool convertSix = false, int roller = -1, bool completesPair = false)
    {
        CurrentDieRoller = roller >= 0 ? roller : State.CurrentPlayer;
        CompletesDiePair = completesPair;
        CurrentDieIsD10 = State.Phase != Phase.Preparation && !State.IsOver && UsesD10(CurrentDieRoller);
        var value = _random.Next(CurrentDieIsD10 ? 10 : 6) + 1;
        if (CurrentDieIsD10)
        {
            State.Players[CurrentDieRoller].FutureDie = FutureDieState.Used;
            caption = $"Era Futurista · D10 = {value}" + (caption.Length > 0 ? $" · {caption}" : "");
            Log($"{State.Players[CurrentDieRoller].Name} lanza D10 con Era Futurista: {value}.");
        }
        State.LastDie = value;
        CurrentDieCaption = convertSix && value == 6
            ? (CurrentDieIsD10 ? caption + " · " : "") + $"{State.Players[CurrentDieRoller].Relikario.DisplayName} · 6 se convierte en 5"
            : caption;
        DieRolled?.Invoke(value);
        return value;
    }
    private void Shuffle(PlayerState player)
    {
        var cards = player.Deck;
        var marks = player.CorruptionMarks?.GetValueOrDefault(CardZone.Deck);
        for (var i = cards.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
            if (marks == null || i == j) continue;
            var first = marks.IndexOf(i);
            var second = marks.IndexOf(j);
            if (first >= 0) marks[first] = j;
            if (second >= 0) marks[second] = i;
        }
    }
    private void Log(string text) => State.Log.Add(text);
}

