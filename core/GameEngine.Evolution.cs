namespace Relick.Core;

public sealed partial class GameEngine
{
    public string? EvolutionUnavailableReason(int actor)
    {
        if (actor < 0 || actor >= State.Players.Length) return "Jugador inválido.";
        var error = ValidateTurn(actor);
        if (error != null) return error;
        var p = State.Players[actor];
        if (State.Phase is not (Phase.Combat or Phase.Actions) &&
            !(p.Relikario == RelikarioCatalog.Ironwing && State.Phase is Phase.Draw or Phase.Final))
            return "Completa el efecto pendiente antes de evolucionar.";
        var next = RelikarioCatalog.EvolutionFor(p.Relikario);
        if (next == null) return "El Relikario ya está en su nivel máximo.";
        if (next == RelikarioCatalog.IronwingLv2)
        {
            if (p.Area != CardCatalog.BlackHole.Id) return "Requiere tener Agujero Negro como tu Área activa.";
        }
        else if (next.EvolutionMaxHealth is int maxHealth)
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
        if (next == RelikarioCatalog.IronwingLv2 && !EffectsSuppressed(actor, FieldZone.Relikario))
            RecoverIronwingAttacks(actor);
        return CommandResult.Ok();
    }

    private void RegisterNaturalSix(PlayerState p, int die)
    {
        if (die != 6 || State.Phase == Phase.Preparation || p.HasNaturalSix || p.Relikario.Family != RelikarioCatalog.Yuno.Id) return;
        p.HasNaturalSix = true;
        Log($"{p.Name} obtiene un 6 natural: condición de evolución cumplida para esta partida.");
    }

    // A continuation pauses the effect before it consumes the chosen result.
    private DieEffect? _pendingDieEffect;

    private void RollWithRelikario(PlayerState p, DiePurpose purpose, DieEffect effect, int? rolledDie = null)
    {
        var actor = Array.IndexOf(State.Players, p);
        var equipment = ActiveEquipment(actor)?.Id;
        var caption = DiePurposes.Caption(purpose, effect);
        var die = rolledDie ?? Roll(caption, roller: actor);
        if (purpose == DiePurpose.TurboOverlapDamage && effect.Slot == 0)
        {
            var firstIsD10 = CurrentDieIsD10;
            var firstCaption = CurrentDieCaption;
            effect.OverlapSecondDie = Roll("Turbo Overlap · Dados de daño", roller: actor, completesPair: true);
            effect.OverlapSecondDieIsD10 = CurrentDieIsD10;
            CurrentDieIsD10 = firstIsD10;
            if (firstIsD10) CurrentDieCaption = firstCaption;
        }
        if (rolledDie.HasValue)
        {
            CurrentDieIsD10 = effect.OverlapSecondDieIsD10 || die > 6;
            CurrentDieCaption = CurrentDieIsD10 ? $"Era Futurista · D10 = {die} · {caption}" : caption;
        }
        var rolled = die;
        die = AdjustBlackHoleDie(actor, purpose, rolled);
        var modified = die != rolled;
        State.LastDie = die;
        var resume = State.Phase;
        State.PendingDieRoll = new(actor, purpose, die, resume, Modified: modified);
        State.Phase = Phase.DieRerollChoice;
        if (CanRespondToEffectDie())
        {
            _pendingDieEffect = effect;
            return;
        }
        State.PendingDieRoll = null;
        State.Phase = resume;
        if (!modified) RegisterNaturalSix(p, die);
        FinishEffectDieWithGhost(effect, die);
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
            die = AdjustBlackHoleDie(actor, pending.Purpose, die);
            Log($"{p.Name} usa {p.Relikario.DisplayName} Lv {p.Level}: relanzamiento {count}/{p.Relikario.MaxRerolls} de {pending.Purpose}, resultado {die}.");
            State.PendingDieRoll = pending with { Die = die, Rerolls = count, WasRerolled = true, Modified = true, RerollStopped = die == 1 };
            if (CanRespondToEffectDie()) return CommandResult.Ok();
        }
        if (!State.PendingDieRoll!.Modified) RegisterNaturalSix(p, die);
        var effect = _pendingDieEffect!;
        _pendingDieEffect = null;
        State.PendingDieRoll = null;
        State.Phase = pending.ResumePhase;
        FinishEffectDieWithGhost(effect, die);
        CompleteActivationIfFinished();
        return CommandResult.Ok();
    }
}
