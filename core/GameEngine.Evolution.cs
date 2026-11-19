namespace Relick.Core;

public sealed partial class GameEngine
{
    public string? EvolutionUnavailableReason(int actor)
    {
        if (actor < 0 || actor >= State.Players.Length) return "Jugador inválido.";
        var error = ValidateTurn(actor);
        if (error != null) return error;
        if (State.Phase is not (Phase.Combat or Phase.Actions)) return "Completa el efecto pendiente antes de evolucionar.";
        var p = State.Players[actor];
        var next = RelikarioCatalog.EvolutionFor(p.Relikario);
        if (next == null) return "El Relikario ya está en su nivel máximo.";
        if (next.EvolutionMaxHealth is int maxHealth)
        {
            if (p.Health > maxHealth) return $"Requiere tener {maxHealth} vidas o menos.";
        }
        else if (!p.HasNaturalSix) return "Requiere haber obtenido un 6 natural en un D6 durante la partida, sin relanzarlo ni modificarlo.";
        if (p.Relikia < next.EvolutionCost) return $"Necesitas {next.EvolutionCost} PR para evolucionar.";
        return null;
    }

    public bool CanEvolve(int actor) => EvolutionUnavailableReason(actor) == null;

    public CommandResult Evolve(int actor)
    {
        var error = EvolutionUnavailableReason(actor);
        if (error != null) return CommandResult.Fail(error);
        var p = State.Players[actor];
        var next = RelikarioCatalog.EvolutionFor(p.Relikario)!;
        p.Relikia -= next.EvolutionCost;
        p.Relikario = next;
        var previousHealth = p.Health;
        if (!EffectsSuppressed(actor, FieldZone.Relikario)) p.Health = Math.Min(p.MaxHealth, p.Health + next.EvolutionHealing);
        foreach (var (owner, slot, host) in Zahiras().ToList())
            if (host.Zone == FieldZone.Relikario && host.Owner == actor)
                State.Players[owner].SetZahira(slot, host with { CardId = next.Id });
        // Evolution does not refresh an ability already used during this turn.
        Log($"{p.Name} evoluciona a {p.Relikario.Name} Lv {p.Level}: paga {next.EvolutionCost} PR, recupera {p.Health - previousHealth} vida(s), hasta el máximo de {p.MaxHealth} de su nivel 1.");
        if (next == RelikarioCatalog.OsamuLv2 && !EffectsSuppressed(actor, FieldZone.Relikario))
            OfferOsamuSearch(actor, next.Id, OsamuEvolutionSearch);
        return CommandResult.Ok();
    }

    private void RegisterNaturalSix(PlayerState p, int die)
    {
        if (die != 6 || State.Phase == Phase.Preparation || p.HasNaturalSix || p.Relikario.Family == RelikarioCatalog.Osamu.Id) return;
        p.HasNaturalSix = true;
        Log($"{p.Name} obtiene un 6 natural: condición de evolución cumplida para esta partida.");
    }

    // A continuation pauses the effect before it consumes the chosen result.
    private DieEffect? _pendingDieEffect;

    private void RollWithRelikario(PlayerState p, DiePurpose purpose, DieEffect effect, int? rolledDie = null)
    {
        var actor = Array.IndexOf(State.Players, p);
        var equipment = ActiveEquipment(actor)?.Id;
        var caption = purpose switch
        {
            DiePurpose.Zahira => "Zahira Flair · Etapa final",
            DiePurpose.QuickLoad => "Carga Rápida · Robo o descarte",
            DiePurpose.FieldCombat => "Jessica Lee · Efecto de canje/Bonus",
            DiePurpose.SkillLearning => "Reverse Shuffle · Aprendizaje",
            DiePurpose.PcStore => "PC 8BITS · Valor guardado",
            DiePurpose.Dodge => "Esquivar · Defensa",
            DiePurpose.ExtraLife => "Vida Extra · Curación",
            DiePurpose.PointsChange => "Aumento de Relick Points",
            DiePurpose.TurboSwordDefense => "Turbo Sword · Defensa",
            DiePurpose.TurboSwordDamage => "Turbo Sword · Daño",
            DiePurpose.TurboOverlapDamage => $"Turbo Overlap · Dado de daño {(effect.Slot == 0 ? 1 : 2)}",
            _ => ""
        };
        var die = rolledDie ?? Roll(caption, roller: actor);
        if (purpose == DiePurpose.TurboOverlapDamage && effect.Slot == 0 && !ReplaySequentialOverlap)
            effect.OverlapSecondDie = Roll("Turbo Overlap · Dados de daño", roller: actor, completesPair: true);
        if (rolledDie.HasValue) CurrentDieCaption = caption;
        State.LastDie = die;
        if (purpose == DiePurpose.SkillLearning || HasTurnBuffInHand(actor) || HasComboParry(actor) || (p.Relikario.CanRerollAnyDie && !EffectsSuppressed(actor, FieldZone.Relikario) && !p.AttackRerollUsedThisTurn && die != 1) || equipment == CardCatalog.Pendant.Id || HasRerollCombo(actor) || (equipment == CardCatalog.Pc.Id && p.EquipmentDie > 0) || (p.Dexterity > 0 && CanBoostWithDexterity(die)))
        {
            _pendingDieEffect = effect;
            State.PendingDieRoll = new(actor, purpose, die, State.Phase);
            State.Phase = Phase.DieRerollChoice;
        }
        else
        {
            RegisterNaturalSix(p, die);
            if (!TryOpenErrorAfterDie(effect, die)) ResolveDieEffect(effect, die);
        }
    }

    public CommandResult ResolveDieReroll(int actor, bool reroll)
    {
        if (State.Phase != Phase.DieRerollChoice || actor != State.DecisionPlayer)
            return CommandResult.Fail("No te corresponde decidir esta tirada.");
        var pending = State.PendingDieRoll!;
        var p = State.Players[actor];
        if (reroll && !CanUseRelikarioOnPendingDie(actor))
            return CommandResult.Fail("No puedes volver a relanzar este dado.");
        var die = pending.Die;
        var count = pending.Rerolls;
        if (reroll)
        {
            OnOwnDieRerolled(actor);
            p.AttackRerollUsedThisTurn = true;
            die = Roll($"{p.Relikario.DisplayName} Lv {p.Level} · Relanzamiento", roller: actor); count++;
            Log($"{p.Name} usa {p.Relikario.DisplayName} Lv {p.Level}: relanzamiento {count}/{p.Relikario.MaxRerolls} de {pending.Purpose}, resultado {die}.");
            State.PendingDieRoll = pending with { Die = die, Rerolls = count, WasRerolled = true, Modified = true, RerollStopped = die == 1 };
            if (CanRespondToEffectDie()) return CommandResult.Ok();
        }
        if (!State.PendingDieRoll!.Modified) RegisterNaturalSix(p, die);
        var effect = _pendingDieEffect!;
        _pendingDieEffect = null;
        State.PendingDieRoll = null;
        State.Phase = pending.ResumePhase;
        if (!TryOpenErrorAfterDie(effect, die)) ResolveDieEffect(effect, die);
        CompleteActivationIfFinished();
        return CommandResult.Ok();
    }
}
