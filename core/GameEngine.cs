namespace Relick.Core;

/// <summary>
/// Pure C# rules: no Godot, network, timers or UI. Commands validate ownership and phase
/// before mutating. A future server must provide the random source and call these rules.
/// </summary>
public sealed partial class GameEngine
{
    public static IReadOnlyList<int> SkillSlotCosts { get; } = Array.AsReadOnly(new[] { 0, 300, 500, 1000 });
    public static int MaxSkills => SkillSlotCosts.Count;
    private readonly IGameRandom _random;
    public GameState State { get; } = new();
    public event Action<int>? AttackPerformed;
    public event Action<int, int>? DamageReceived;
    public event Action<int>? DieRolled;
    public event Action<int, int>? CardDrawn;
    public event Action<int, int, string>? CardReturnedToDeck;
    // Signals the end of a batch for the deck animation; does not change deck order.
    public event Action<int>? CardsReturnedToDeck;
    public event Action<int, string>? BonusPlaced;
    public event Action<int, int, int, string>? BonusClaimed;

    public GameEngine(int seed) : this(new SeededRandom(seed)) { }

    public GameEngine(IGameRandom random)
    {
        _random = random;
        foreach (var player in State.Players)
        {
            player.Deck.AddRange(CardCatalog.TrainingDeck());
            player.Deck.Remove(CardCatalog.Magnet.Id);
            player.Hand.Add(CardCatalog.Magnet.Id);
            Shuffle(player.Deck);
            for (var i = 0; i < 4; i++) DrawOne(player);
        }
        Log("Sala de práctica: ambos relikarios empiezan con 8 vidas.");
        Log("Elige cartas para cambiarlas, o conserva tu mano inicial.");
    }

    public CommandResult Start(IEnumerable<int> mulligan)
    {
        if (State.Phase != Phase.Preparation) return CommandResult.Fail("La partida ya empezó.");
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
        int human, bot;
        do { human = Roll(); bot = Roll(); } while (human == bot);
        State.CurrentPlayer = human > bot ? 0 : 1;
        Log($"Dado inicial: {State.Players[0].Name} {human}, {State.Players[1].Name} {bot}. Empieza {State.Active.Name}.");
        BeginTurn();
        return CommandResult.Ok();
    }

    public int SkillCost(PlayerState player) => player.Skills.Count < MaxSkills ? SkillSlotCosts[player.Skills.Count] : int.MaxValue;

    /// <summary>Read-only query for plays other than advancing; never consumes cards or randomness.</summary>
    public bool HasAvailablePlay(int actor)
    {
        if (State.Phase == Phase.Discard)
            return !State.IsOver && actor == State.DecisionPlayer && State.DiscardsRemaining > 0 && State.Players[actor].Hand.Count > 0;
        if (State.Phase is Phase.DefenseChoice or Phase.MagnetChoice or Phase.AttackRerollChoice or Phase.DieRerollChoice or Phase.CodeResponse or Phase.PointsChoice or Phase.DestroyChoice or Phase.ErrorResponse)
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
        var die = Roll();
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
        if (card.CancelActivation) return PlayLoadError(actor, handIndex);
        if (card.RerollOwnDie) return PlayRerollCombo(actor, handIndex);
        SendHandToMemory(p, handIndex);
        p.ActionsLeft -= card.ActionCost;
        p.Relikia -= card.PrCost;
        Log($"{p.Name} juega {card.Name}.");
        ActivateCode(actor, card, false, p.Memory.Count - 1);
        return CommandResult.Ok();
    }

    private void ResolveCode(PlayerState p, CardDefinition card)
    {
        if (card.DestroyNonSkill)
        {
            State.DestroyActor = Array.IndexOf(State.Players, p); State.Phase = Phase.DestroyChoice;
            return;
        }
        if (card.ChangePointsFromDie)
        {
            RollWithRelikario(p, DiePurpose.PointsChange, die =>
            {
                var amount = die switch { 1 => 300, 2 => 400, 3 or 4 => 500, 5 => 1000, _ => 1500 };
                State.PendingPoints = new(Array.IndexOf(State.Players, p), die, amount);
                State.Phase = Phase.PointsChoice;
                State.LastCombat = $"{p.Name} · Aumento de Relick Points · D6 = {die} · Elige destino de {amount} PR";
                Log(State.LastCombat);
            });
            return;
        }
        if (card.HealFromDie)
        {
            RollWithRelikario(p, DiePurpose.ExtraLife, die =>
            {
                var amount = die switch { 1 => 1, 2 => 2, 6 => 4, _ => 3 };
                var healed = Heal(p, amount);
                State.LastCombat = $"{p.Name} · Vida Extra · D6 = {die} · Recupera {healed} vida(s)";
                Log($"{p.Name} resuelve Vida Extra: D6 = {die}, gana {amount} vida(s); recupera {healed} hasta su máximo de {p.MaxHealth}.");
            });
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
        Log($"{p.Name} recupera {p.Health - old} vida(s).");
        return p.Health - old;
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

    private void PlaceInExchange(PlayerState p, string id, Action? completed = null)
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
        else completed?.Invoke();
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

    private void DealDamage(int target, int amount)
    {
        if (amount <= 0) return;
        var p = State.Players[target];
        var wasZero = p.Health == 0;
        p.Health = Math.Max(0, p.Health - amount);
        DamageReceived?.Invoke(target, amount);
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

    private void DrawCards(PlayerState p, int count, Action? completed = null)
    {
        if (State.IsOver) return;
        if (count <= 0) { completed?.Invoke(); return; }
        DrawOne(p, () => DrawCards(p, count - 1, completed));
    }

    private void DrawOne(PlayerState p, Action? completed = null) => MoveTopCard(p, id =>
    {
        p.Hand.Add(id);
        CardDrawn?.Invoke(Array.IndexOf(State.Players, p), p.Hand.Count - 1);
    }, completed);

    private void MoveTopCard(PlayerState p, Action<string> receive, Action? completed = null)
    {
        if (State.IsOver) return;
        void Move()
        {
            if (State.IsOver) return;
            receive(p.Deck[0]); p.Deck.RemoveAt(0);
            if (p.Deck.Count == 0 && State.Phase != Phase.Preparation) Exhaust(p, completed);
            else completed?.Invoke();
        }
        if (p.Deck.Count == 0) Exhaust(p, Move);
        else Move();
    }

    private void Exhaust(PlayerState p, Action? completed = null)
    {
        if (!p.Recycled && p.Memory.Count > 0)
        {
            p.Recycled = true;
            RollWithRelikario(p, DiePurpose.Recycling, rolled =>
            {
                var count = Math.Min(rolled, p.Memory.Count);
                Shuffle(p.Memory);
                p.Deck.AddRange(p.Memory.Take(count)); p.Memory.RemoveRange(0, count);
                if (count > 0) CardsReturnedToDeck?.Invoke(Array.IndexOf(State.Players, p));
                Log($"{p.Name} agota su mazo: D6 = {rolled}. Recicla {count} carta(s) al azar (demo).");
                completed?.Invoke();
            });
            return;
        }
        var target = Array.IndexOf(State.Players, p);
        DealDamage(target, 2);
        if (State.IsOver) return;
        var a = State.Players[0].Health; var b = State.Players[1].Health;
        Finish(a == b ? -1 : a > b ? 0 : 1,
            $"{p.Name} agota su mazo y pierde 2 vidas. Se comparan las vidas restantes.");
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

