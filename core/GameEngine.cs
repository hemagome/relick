namespace Relick.Core;

/// <summary>
/// Pure C# rules: no Godot, network, timers or UI. Commands validate ownership and phase
/// before mutating. The server supplies the random source and calls the same rules.
/// </summary>
public sealed partial class GameEngine
{
    public static IReadOnlyList<int> SkillSlotCosts { get; } = Array.AsReadOnly(new[] { 0, 300, 500, 1000 });
    public static int MaxSkills => SkillSlotCosts.Count;
    private readonly IGameRandom _random;
    public GameState State { get; } = new();
    public event Action<int>? AttackPerformed;
    public event Action<int, int, int>? DamageReceived;
    public event Action<int, int>? HealingReceived;
    public event Action<int>? DieRolled;
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
            var player = new PlayerState { Name = opening.Names[seat], Bonus = opening.Bonuses[seat] };
            player.Hand.AddRange(opening.Hands[seat]);
            player.Deck.AddRange(opening.Decks[seat]);
            state.Players[seat] = player;
        }
        return new GameEngine(state, random);
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
        foreach (var player in State.Players) player.Deck.AddRange(CardCatalog.TrainingDeck());
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

    public int SkillCost(PlayerState player) => player.Skills.Count < MaxSkills ? SkillSlotCosts[player.Skills.Count] : int.MaxValue;

    /// <summary>Read-only query for plays other than advancing; never consumes cards or randomness.</summary>
    public bool HasAvailablePlay(int actor)
    {
        if (State.Phase == Phase.Discard)
            return !State.IsOver && actor == State.DecisionPlayer && State.DiscardsRemaining > 0 && State.Players[actor].Hand.Count > 0;
        if (State.Phase is Phase.DefenseChoice or Phase.MagnetChoice or Phase.AttackRerollChoice or Phase.DieRerollChoice or Phase.CodeResponse or Phase.PointsChoice or Phase.DestroyChoice or Phase.ErrorResponse or Phase.MindResetChoice)
            return actor == State.DecisionPlayer;
        if (ValidateTurn(actor) != null) return false;
        var p = State.Active;
        if (CanEvolve(actor)) return true;
        return State.Phase switch
        {
            Phase.Combat => !State.AttackedThisTurn && (
                (p.Skills.Count > 0 && p.Hand.Contains(CardCatalog.Attack.Id)) ||
                (!State.LearnedThisTurn && p.Skills.Count < MaxSkills && p.Relikia >= SkillCost(p) &&
                 p.Hand.Any(id => CardCatalog.Get(id).Kind == CardKind.Skill))),
            Phase.Actions => (State.ActionsLeft > 0 && p.Hand.Select((id, i) => (id, i)).Any(c => p.Bonus != null || CardCatalog.Get(c.id).Kind == CardKind.Equipment || CodeUnavailableReason(actor, c.i) == null)) ||
                RelikiaEffects.All.Any(e => CanUseRelikiaEffect(actor, e.Id)),
            Phase.Discard => State.DiscardsRemaining > 0 && p.Hand.Count > 0,
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
        if (State.LearnedThisTurn || State.AttackedThisTurn)
            return CommandResult.Fail("Solo puedes enseñar una habilidad, antes de atacar.");
        if (p.Skills.Count >= MaxSkills) return CommandResult.Fail("Los cuatro espacios de habilidad están ocupados.");
        var cost = SkillCost(p);
        if (p.Relikia < cost) return CommandResult.Fail($"Necesitas {cost} PR para enseñar otra habilidad.");
        p.Relikia -= cost;
        p.RemoveHandCard(handIndex);
        p.Skills.Add(id);
        State.LearnedThisTurn = true;
        Log($"{p.Name} enseña {CardCatalog.Get(id).Name} ({cost} PR).");
        return CommandResult.Ok();
    }

    public CommandResult Attack(int actor, int handIndex, int skillIndex)
    {
        var error = ValidateTurn(actor, Phase.Combat);
        if (error != null) return CommandResult.Fail(error);
        var p = State.Active;
        if (!InHand(p, handIndex) || p.Hand[handIndex] != CardCatalog.Attack.Id)
            return CommandResult.Fail("Selecciona una carta de ataque.");
        if (skillIndex < 0 || skillIndex >= p.Skills.Count)
            return CommandResult.Fail("Primero enseña y selecciona una habilidad.");
        if (State.AttackedThisTurn) return CommandResult.Fail("Ya atacaste este turno.");
        var skill = CardCatalog.Get(p.Skills[skillIndex]);
        SendHandToMemory(p, handIndex);
        State.AttackedThisTurn = true;
        var die = RollAttack();
        State.LastAttackDie = die;
        State.PendingAttackRoll = new(actor, skill.Id, die);
        State.Phase = Phase.AttackRerollChoice;
        if (CanRespondToAttackDie())
        {
            State.Phase = Phase.AttackRerollChoice;
            State.LastCombat = $"{p.Name} · Dado de ataque {die} · Decide si relanza con Yuno";
        }
        else { RegisterNaturalSix(p, die); FinishAttackRoll(die); }
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
        SendHandToMemory(p, handIndex);
        p.ActionsLeft -= card.ActionCost;
        p.Relikia -= card.PrCost;
        Log($"{p.Name} juega {card.Name}.");
        ActivateCode(actor, card, false, p.Memory.Count - 1);
        return CommandResult.Ok();
    }

    private void ResolveCode(PlayerState p, CardDefinition card)
    {
        if (card.ResetMind) { BeginMindReset(Array.IndexOf(State.Players, p)); return; }
        if (card.SwapHands) { SwapHands(Array.IndexOf(State.Players, p)); return; }
        if (card.SearchNonSkill)
        {
            if (!SearchableDeckCards(Array.IndexOf(State.Players, p)).Any())
            {
                Log($"{card.Name} se resuelve sin no-habilidades disponibles en el mazo.");
                return;
            }
            State.Phase = Phase.DeckSearchChoice;
            State.LastCombat = $"{p.Name} · {card.Name} · Elige una no-habilidad de tu mazo";
            return;
        }
        if (card.DestroyChosenZone && !TimeBombTargets().Any())
        {
            Log($"{card.Name} se resuelve sin objetivos disponibles.");
            return;
        }
        if (card.DestroyNonSkill || card.DestroyChosenZone)
        {
            State.DestroyActor = Array.IndexOf(State.Players, p); State.Phase = Phase.DestroyChoice;
            return;
        }
        if (card.ChangePointsFromDie)
        {
            RollWithRelikario(p, DiePurpose.PointsChange, new DieEffect { Purpose = DiePurpose.PointsChange, Actor = Array.IndexOf(State.Players, p) });
            return;
        }
        if (card.HealFromDie)
        {
            RollWithRelikario(p, DiePurpose.ExtraLife, new DieEffect { Purpose = DiePurpose.ExtraLife, Actor = Array.IndexOf(State.Players, p) });
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
        var id = p.Hand[handIndex];
        p.RemoveHandCard(handIndex);
        State.ActionsLeft--;
        PlaceInExchange(p, id);
        return CommandResult.Ok();
    }

    private void PlaceInExchange(PlayerState p, string id, EngineContinuation? completed = null)
    {
        p.Exchange.Add(id);
        Log($"{p.Name} coloca {CardCatalog.Get(id).Name} en canje ({p.Exchange.Count}/2).");
        if (p.Exchange.Count == 2 && p.Bonus != null)
        {
            for (var slot = 0; slot < p.Exchange.Count; slot++)
                NotifyPileMove(p, p.Exchange[slot], CardZone.Exchange, slot);
            p.Memory.AddRange(p.Exchange);
            p.Exchange.Clear();
            TakeBonus(p, p);
            Log($"{p.Name} completa el canje y obtiene su bonus.");
            RefillBonus(p, completed);
        }
        else Continue(completed);
    }

    public CommandResult Advance(int actor)
    {
        var error = ValidateTurn(actor);
        if (error != null) return CommandResult.Fail(error);
        if (State.Phase == Phase.Combat)
        {
            State.Phase = Phase.Actions;
            Log($"{State.Active.Name} entra en acciones ({State.ActionsLeft} disponibles).");
        }
        else if (State.Phase == Phase.Actions) EndTurn();
        else return CommandResult.Fail("Completa el efecto pendiente antes de continuar.");
        return CommandResult.Ok();
    }

    public CommandResult Discard(int actor, int handIndex)
    {
        if (State.Phase != Phase.Discard || actor != State.DecisionPlayer)
            return CommandResult.Fail("No te corresponde descartar ahora.");
        if (State.DiscardsRemaining <= 0) return CommandResult.Fail("Solo puedes descartar cuando un efecto de carta lo indique.");
        var p = State.Players[actor];
        if (!InHand(p, handIndex)) return CommandResult.Fail("Elige una carta de tu mano.");
        var id = p.Hand[handIndex];
        SendHandToMemory(p, handIndex);
        Log($"{p.Name} descarta {CardCatalog.Get(id).Name}.");
        State.DiscardsRemaining--;
        if (p.Hand.Count == 0) State.DiscardsRemaining = 0;
        if (State.DiscardsRemaining == 0)
        {
            State.Phase = State.DiscardResumePhase;
            State.DiscardPlayer = null;
        }
        return CommandResult.Ok();
    }

    private void BeginTurn()
    {
        State.Turn++;
        foreach (var player in State.Players) player.AttackRerollUsedThisTurn = false;
        State.Phase = Phase.Combat;
        foreach (var player in State.Players) player.ActionsLeft = 2;
        State.AttackedThisTurn = false;
        State.LearnedThisTurn = false;
        var count = State.Active.Hand.Count == 0 ? 3 : 1;
        Log($"Turno {State.Turn} · {State.Active.Name}: roba {count} carta(s).");
        DrawCards(State.Active, count);
    }

    private void EndTurn()
    {
        State.CurrentPlayer = 1 - State.CurrentPlayer;
        BeginTurn();
    }

    private void DealDamage(int target, int amount, int attackDie = 0)
    {
        if (amount <= 0) return;
        var p = State.Players[target];
        var reduction = p.Equipment == null ? 0 : CardCatalog.Get(p.Equipment).DamageReduction;
        if (reduction > 0)
        {
            var reduced = DamageAfterEquipment(p, amount);
            Log($"{p.Name} reduce el daño de {amount} a {reduced} con {CardCatalog.Get(p.Equipment!).Name}.");
            State.LastCombat += $" · {CardCatalog.Get(p.Equipment!).Name}: {reduced} daño recibido";
            amount = reduced;
            if (amount == 0) return;
        }
        var wasZero = p.Health == 0;
        p.Health = Math.Max(0, p.Health - amount);
        DamageReceived?.Invoke(target, amount, attackDie);
        if (wasZero || (p.Health == 0 && p.HasReachedZero))
        {
            Finish(1 - target, $"{p.Name} recibe el daño definitivo.");
            return;
        }
        if (p.Health == 0)
        {
            p.HasReachedZero = true;
            Log($"{p.Name} queda en 0 vidas: un nuevo daño terminará la partida.");
        }
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
        if (bonus) { p.Bonus = id; BonusPlaced?.Invoke(actor, id); }
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
    private int RollAttack(string caption = "", bool convertSix = false)
    {
        var die = Roll(caption, convertSix);
        if (die == 1)
            foreach (var player in State.Players)
                if (player.Equipment is { } id && CardCatalog.Get(id).DestroyOnAttackOne) DestroyEquipment(player);
        return die;
    }
    private int Roll(string caption = "", bool convertSix = false)
    {
        var value = _random.Next(6) + 1;
        State.LastDie = value;
        CurrentDieCaption = convertSix && value == 6 ? "Yuno · 6 se convierte en 5" : caption;
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

