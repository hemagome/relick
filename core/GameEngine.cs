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

    public static GameEngine FromOpening(DuelOpening opening, IGameRandom random, bool legacyBlueEgg = false)
    {
        var state = new GameState { Phase = Phase.Combat, Turn = 1, CurrentPlayer = opening.StartingSeat };
        for (var seat = 0; seat < 2; seat++)
        {
            var player = new PlayerState { Name = opening.Names[seat], Bonus = opening.Bonuses[seat],
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
        var game = new GameEngine(state, random) { LegacyBlueEgg = legacyBlueEgg };
        if (game.HasTimeBenderForDraw()) state.Phase = Phase.Draw;
        return game;
    }

    public CommandResult Concede(int actor)
    {
        if (actor is < 0 or > 1 || State.IsOver) return CommandResult.Fail("La partida terminó.");
        Finish(1 - actor, $"{State.Players[actor].Name} abandona el duelo.");
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
        if (State.Phase is Phase.DestructionResponse or Phase.TurnBuffDiscard or Phase.EffectDamageChoice or Phase.TotemChoice or Phase.GoldenTotemChoice or Phase.TrapCraftChoice or Phase.ZahiraTarget or Phase.BlueEggDrawChoice or Phase.Decision) return actor == State.DecisionPlayer;
        if (State.Phase == Phase.Discard)
            return !State.IsOver && actor == State.DecisionPlayer && State.DiscardsRemaining > 0 && State.Players[actor].Hand.Count > 0;
        if (State.Phase is Phase.DefenseChoice or Phase.MagnetChoice or Phase.AttackRerollChoice or Phase.DieRerollChoice or Phase.CodeResponse or Phase.PointsChoice or Phase.DestroyChoice or Phase.ErrorResponse or Phase.MindResetChoice or Phase.SniperChoice or Phase.MemoryRecoveryChoice)
            return actor == State.DecisionPlayer;
        if (ValidateTurn(actor) != null) return false;
        var p = State.Active;
        if (p.Hand.Select((id, index) => (id, index)).Any(card => CardCatalog.Get(card.id).IsCombo && CodeUnavailableReason(actor, card.index) == null)) return true;
        if (CanEvolve(actor)) return true;
        return State.Phase switch
        {
            Phase.Combat => FieldCombatTargets(actor).Any() ||
                CanAttack(actor) ||
                (!State.LearnedThisTurn && p.Skills.Count < MaxSkills && p.Relikia >= SkillCost(p) &&
                 p.Hand.Any(id => CardCatalog.Get(id).Kind == CardKind.Skill)),
            Phase.Actions => (State.ActionsLeft > 0 && p.Hand.Select((id, i) => (id, i)).Any(c => p.Bonus != null || CardCatalog.Get(c.id).Kind == CardKind.Equipment || CodeUnavailableReason(actor, c.i) == null)) ||
                RelikiaEffects.All.Any(e => CanUseRelikiaEffect(actor, e.Id)),
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
        if (!InHand(p, handIndex)) return CommandResult.Fail("Elige una carta de tu mano.");
        var id = p.Hand[handIndex];
        if (CardCatalog.Get(id).Kind != CardKind.Skill) return CommandResult.Fail("Esta carta no es una habilidad.");
        if (State.LearnedThisTurn)
            return CommandResult.Fail("Solo puedes enseñar una habilidad por turno.");
        if (p.Skills.Count >= MaxSkills) return CommandResult.Fail("Los cuatro espacios de habilidad están ocupados.");
        var cost = SkillCost(p);
        var position = p.NextSkillPosition();
        if (p.Relikia < cost) return CommandResult.Fail($"Necesitas {cost} PR para enseñar otra habilidad.");
        p.Relikia -= cost;
        MoveHandCardToField(p, handIndex);
        p.Skills.Add(id);
        if (p.SkillPositions != null || position != p.Skills.Count - 1)
        {
            p.SkillPositions ??= new();
            p.SkillPositions[p.Skills.Count - 1] = position;
        }
        State.LearnedThisTurn = true;
        Log($"{p.Name} enseña {CardCatalog.Get(id).Name} ({cost} PR).");
        if (CardCatalog.Get(id).DrawOnExpensiveLearn && cost is 500 or 1000)
            RollWithRelikario(p, DiePurpose.SkillLearning, new DieEffect { Purpose = DiePurpose.SkillLearning, Actor = actor });
        return CommandResult.Ok();
    }

    private string? AttackUnavailableReason(int actor)
    {
        var error = ValidateTurn(actor, Phase.Combat);
        if (error != null) return error;
        if (State.Turn <= 1) return "Quien inicia la partida no puede atacar en su primer turno.";
        return State.AttackedThisTurn ? "Ya atacaste este turno." : null;
    }

    public bool CanAttack(int actor) => AttackUnavailableReason(actor) == null &&
        State.Active.Skills.Count > 0 && State.Active.Hand.Any(id => CardCatalog.Get(id).Kind == CardKind.Attack);

    public CommandResult Attack(int actor, int handIndex, int skillIndex)
    {
        var error = AttackUnavailableReason(actor);
        if (error != null) return CommandResult.Fail(error);
        var p = State.Active;
        if (!InHand(p, handIndex) || CardCatalog.Get(p.Hand[handIndex]).Kind != CardKind.Attack)
            return CommandResult.Fail("Selecciona una carta de ataque.");
        if (skillIndex < 0 || skillIndex >= p.Skills.Count)
            return CommandResult.Fail("Primero enseña y selecciona una habilidad.");
        var skill = CardCatalog.Get(p.Skills[skillIndex]);
        var suppressed = EffectsSuppressed(actor, FieldZone.Skill, skillIndex);
        var superAttack = p.Hand[handIndex] == CardCatalog.SuperAttack.Id;
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
        State.LastCombat = $"{p.Name} · Dado de ataque {die} · Decide si relanza con {p.Relikario.DisplayName}";
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
        var card = CardCatalog.Get(p.Hand[handIndex]);
        if (card.ResetMind) return PlayMindReset(actor, handIndex);
        if (card.CancelActivation) return PlayLoadError(actor, handIndex);
        if (card.RerollOwnDie || card.SetOwnDie > 0) return PlayRerollCombo(actor, handIndex);
        var played = SendHandToMemory(p, handIndex);
        p.ActionsLeft -= card.ActionCost;
        p.Relikia -= CodePrCost(card);
        Log($"{p.Name} juega {card.Name}.");
        ActivateCode(actor, card, false, played?.Index ?? -1, played?.Owner);
        return CommandResult.Ok();
    }

    private void ResolveCode(PlayerState p, CardDefinition card)
    {
        if (TryResolveWithEffect(p, card)) return;
        if (card.DeployTotems) { BeginGoldenTotem(Array.IndexOf(State.Players, p)); return; }
        if (card.RepeatStage) { ResolveRepeatedStage(); return; }
        if (card.ReloadBothHands) { BeginReloadHands(Array.IndexOf(State.Players, p)); return; }
        if (card.DiscardForTurnBuff) { ResolveTurnBuff(); return; }
        if (card.DodgeAttack) { RollWithRelikario(p, DiePurpose.Dodge, new DieEffect { Purpose = DiePurpose.Dodge, Actor = Array.IndexOf(State.Players, p), SkillId = card.Id }); return; }
        if (card.ResetMind) { BeginMindReset(Array.IndexOf(State.Players, p)); return; }
        if (card.SwapHands) { SwapHands(Array.IndexOf(State.Players, p)); return; }
        if (card.SearchAttacks > 0)
        {
            var count = Math.Min(card.SearchAttacks, p.Deck.Count(id => CardCatalog.Get(id).Kind == CardKind.Attack));
            if (_activeActivation != null) _activeActivation.CanInterrupt = false;
            Log($"{p.Name} añade {count} ataque(s) de su mazo a su mano con {card.Name}.");
            // All current Attack cards are identical. Choose their first copies in deck order.
            // Freeze the count so recycling cannot add further targets to this effect.
            for (var i = 0; i < count; i++)
                MoveDeckCard(p, p.Deck.FindIndex(id => CardCatalog.Get(id).Kind == CardKind.Attack), false);
            return;
        }
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
        if (!InHand(p, handIndex)) return CommandResult.Fail("Elige una carta de tu mano.");
        if (p.Bonus == null) return CommandResult.Fail("No hay una carta de Bonus disponible.");
        var id = MoveHandCardToField(p, handIndex);
        State.ActionsLeft--;
        PlaceInExchange(p, id);
        return CommandResult.Ok();
    }

    private void PlaceInExchange(PlayerState p, string id, EngineContinuation? completed = null, int? fieldPlacedTurn = null)
    {
        p.Exchange.Add(id);
        if (CardCatalog.Get(id).FieldCombatDie)
        {
            p.ExchangePlacementTurns ??= new();
            p.ExchangePlacementTurns[p.Exchange.Count - 1] = fieldPlacedTurn ?? Math.Max(1, State.Turn);
        }
        Log($"{p.Name} coloca {CardCatalog.Get(id).Name} en canje ({p.Exchange.Count}/2).");
        CompleteExchange(p, completed);
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
            ShiftExchangeEffects(p, 0, 2);
            TakeBonus(p, p);
            Log($"{p.Name} completa el canje y obtiene su bonus.");
            RefillBonus(p, new EngineContinuation { Kind = ContinuationKind.CompleteExchange, Actor = actor, Next = completed });
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
            Log($"{State.Active.Name} entra en acciones ({State.ActionsLeft} disponibles).");
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
        if (!InHand(p, handIndex)) return CommandResult.Fail("Elige una carta de tu mano.");
        var id = p.Hand[handIndex];
        var discarded = SendHandToMemory(p, handIndex);
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
            player.AttackRerollUsedThisTurn = false;
            player.BonusEffectUsed = false; player.ExchangeEffectsUsed = null;
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
                DestroyEquipment(State.Active, new EngineContinuation { Kind = ContinuationKind.TurnStartDraw, Actor = State.CurrentPlayer });
                return;
            }
        }
        DrawAtTurnStart();
    }

    private void DrawAtTurnStart(bool explicitStage = false)
    {
        if (explicitStage) State.Phase = Phase.Draw;
        var count = State.Active.Hand.Count == 0 ? 3 : 1;
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
        CompleteAttackIfReady();
        if (!TryPresentTrapCraft()) CompleteEndTurnIfReady();
    }

    private void CompleteEndTurnIfReady()
    {
        // Discard triggers, responses and their draws finish before checking the limit again.
        if (State.Phase != Phase.Final || State.RepeatedFinalAwaitingAdvance || _activeActivation != null || State.PendingSniper != null || State.PendingTurnBuff != null ||
            State.Active.Hand.Count > MaxHandSize) return;
        if (TryRollZahiraAtFinal()) return;
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

    private void DealDamage(int target, int amount, int attackDie = 0, EngineContinuation? completed = null, int? source = null)
    {
        if (amount <= 0) { Continue(completed); return; }
        var p = State.Players[target];
        if (source is int owner && State.Players[owner].StarEffects > 0)
        {
            var original = amount;
            for (var i = 0; i < State.Players[owner].StarEffects; i++) amount *= 2;
            Log($"Estrella del Fontanero aumenta el daño de {State.Players[owner].Name} de {original} a {amount}.");
            State.LastCombat += $" · Estrella del Fontanero: {amount} de daño";
        }
        if (p.StarEffects > 0)
        {
            Log($"{p.Name} transforma {amount} de daño en 0 con Estrella del Fontanero.");
            State.LastCombat += " · Estrella del Fontanero: 0 daño recibido";
            Continue(completed); return;
        }
        var protection = ActiveEquipment(target);
        if (protection != null && (protection.PreventDamage || protection.DamageReduction > 0))
        {
            var reduced = protection.PreventDamage ? 0 : Math.Max(0, amount - protection.DamageReduction);
            Log($"{p.Name} reduce el daño de {amount} a {reduced} con {protection.Name}.");
            State.LastCombat += $" · {protection.Name}: {reduced} daño recibido";
            amount = reduced;
            if (amount == 0) { Continue(completed); return; }
        }
        if (protection is { DamageCounterLimit: > 0 })
        {
            p.EquipmentCounters++;
            if (p.EquipmentCounters >= protection.DamageCounterLimit)
            {
                DestroyEquipment(p, new EngineContinuation { Kind = ContinuationKind.ApplyDamage, Actor = target, Count = amount, Value = attackDie, Next = completed });
                return;
            }
        }
        if (!TryTotemDamage(target, amount, attackDie, completed)) ApplyHealthDamage(target, amount, attackDie, completed);
    }

    private void ApplyHealthDamage(int target, int amount, int attackDie, EngineContinuation? completed)
    {
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
    private void MoveDeckCard(PlayerState p, int index, bool bonus, EngineContinuation? completed = null)
    {
        var actor = Array.IndexOf(State.Players, p);
        var id = p.Deck[index];
        if (bonus)
        {
            ForgetFieldCodeSource(p, FieldZone.Bonus); p.Bonus = id;
            if (CardCatalog.Get(id).FieldCombatDie) p.BonusPlacedTurn = Math.Max(1, State.Turn);
            BonusPlaced?.Invoke(actor, id);
        }
        else { p.Hand.Add(id); CardDrawn?.Invoke(actor, p.Hand.Count - 1); }
        p.Deck.RemoveAt(index);
        if (p.Deck.Count == 0 && State.Phase != Phase.Preparation) Exhaust(p, completed);
        else Continue(completed);
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
        ValidateTurn(actor, Phase.Actions) ?? (State.ActionsLeft <= 0 ? "Ya usaste tus dos acciones." : null);

    private static bool InHand(PlayerState p, int index) => index >= 0 && index < p.Hand.Count;
    public string CurrentDieCaption { get; private set; } = "";
    private int RollAttack(out bool modified, string caption = "", bool convertSix = false, bool superAttack = false)
    {
        var die = Roll(caption, convertSix);
        var bonus = EffectsSuppressed(State.CurrentPlayer, FieldZone.Relikario) ? 0 : State.Active.Relikario.AttackDieBonusExceptOne;
        modified = bonus > 0 && die is >= 2 and < 6;
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
    private int Roll(string caption = "", bool convertSix = false, int roller = -1)
    {
        CurrentDieRoller = roller >= 0 ? roller : State.CurrentPlayer;
        var value = _random.Next(6) + 1;
        State.LastDie = value;
        CurrentDieCaption = convertSix && value == 6 ? $"{State.Players[CurrentDieRoller].Relikario.DisplayName} · 6 se convierte en 5" : caption;
        DieRolled?.Invoke(value);
        return value;
    }
    private void Shuffle(List<string> cards)
    {
        for (var i = cards.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
    }
    private void Log(string text) => State.Log.Add(text);
}

