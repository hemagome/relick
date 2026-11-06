namespace Relick.Core;

public sealed partial class GameEngine
{
    public bool CanUseRelikiaEffect(int actor, RelikiaEffect effect) => RelikiaEffectUnavailableReason(actor, effect) == null;

    public string? RelikiaEffectUnavailableReason(int actor, RelikiaEffect effect)
    {
        var error = ValidateAction(actor);
        if (error != null) return error;
        if (!Enum.IsDefined(effect)) return "Efecto desconocido.";
        var definition = RelikiaEffects.Get(effect);
        var p = State.Active;
        if (State.ActionsLeft < definition.Actions) return $"Necesitas {definition.Actions} acciones disponibles.";
        if (p.Relikia < definition.Cost) return $"Necesitas {definition.Cost} PR.";
        return effect switch
        {
            RelikiaEffect.ChangeBonus when p.Exchange.Count != 0 => "Tu zona de canje debe estar vacía.",
            RelikiaEffect.ChangeBonus or RelikiaEffect.ClaimBonus when p.Bonus == null => "No tienes Bonus disponible.",
            RelikiaEffect.ReplayCode when !p.Memory.Where(IsCode).Any(id => CodeEffectUnavailable(p, CardCatalog.Get(id), definition.Cost) == null) => "No tienes códigos sin corromper en tu memoria.",
            _ => null
        };
    }

    public CommandResult UseRelikiaEffect(int actor, RelikiaEffect effect, int memoryIndex = -1)
    {
        var error = RelikiaEffectUnavailableReason(actor, effect);
        if (error != null) return CommandResult.Fail(error);
        var p = State.Active;
        if (effect == RelikiaEffect.ReplayCode && !IsMemoryCode(actor, memoryIndex))
            return CommandResult.Fail("Elige un código sin corromper de tu memoria.");
        var definition = RelikiaEffects.Get(effect);
        if (effect == RelikiaEffect.ReplayCode)
        {
            var extraError = CodeEffectUnavailable(p, CardCatalog.Get(p.Memory[memoryIndex]), definition.Cost);
            if (extraError != null) return CommandResult.Fail(extraError);
        }
        p.Relikia -= definition.Cost;
        if (effect == RelikiaEffect.ReplayCode) p.Relikia -= CardCatalog.Get(p.Memory[memoryIndex]).PrCost;
        State.ActionsLeft -= definition.Actions;
        Log($"{p.Name} usa Relikia: {definition.Name} ({definition.Cost} PR, {definition.ActionLabel}).");
        switch (effect)
        {
            case RelikiaEffect.ChangeBonus:
                var bonus = p.Bonus!;
                p.Bonus = null;
                PlaceInExchange(p, bonus, () => RefillBonus(p));
                break;
            case RelikiaEffect.DrawAndPlace:
                DrawOne(p, () => BeginRelikiaChoice(RelikiaChoice.PlaceHandCard));
                break;
            case RelikiaEffect.ReplayCode:
                var code = CardCatalog.Get(p.Memory[memoryIndex]);
                ActivateCode(actor, code, true, memoryIndex);
                break;
            case RelikiaEffect.ClaimBonus:
                TakeBonus(p, p);
                RefillBonus(p, () => RollWithRelikario(p, DiePurpose.Relikia, die =>
                {
                    State.LastCombat = $"{p.Name} · Relikia: robar el Bonus · D6 = {die}";
                    Log($"{p.Name} lanza D6 por Relikia: {die}." + (die >= 5 ? " Elige un efecto adicional." : " Sin efecto adicional."));
                    if (die >= 5) BeginRelikiaChoice(RelikiaChoice.BonusReward);
                }));
                break;
        }
        return CommandResult.Ok();
    }

    public CommandResult ResolveRelikiaPlacement(int actor, int handIndex, CardDestination destination)
    {
        var error = ValidateRelikiaChoice(actor, RelikiaChoice.PlaceHandCard);
        if (error != null) return CommandResult.Fail(error);
        var p = State.Active;
        if (!InHand(p, handIndex) || !Enum.IsDefined(destination)) return CommandResult.Fail("Elige una carta y su destino.");
        if (destination == CardDestination.Exchange && p.Bonus == null) return CommandResult.Fail("No hay Bonus disponible para canjear.");
        var id = p.Hand[handIndex];
        p.RemoveHandCard(handIndex);
        EndRelikiaChoice();
        if (destination == CardDestination.Exchange) PlaceInExchange(p, id);
        else
        {
            p.Deck.Add(id);
            Log($"{p.Name} envía {CardCatalog.Get(id).Name} al fondo del mazo por Relikia.");
        }
        return CommandResult.Ok();
    }

    public CommandResult ChooseRelikiaReward(int actor, RelikiaReward reward)
    {
        var error = ValidateRelikiaChoice(actor, RelikiaChoice.BonusReward);
        if (error != null) return CommandResult.Fail(error);
        if (!Enum.IsDefined(reward)) return CommandResult.Fail("Elige un efecto adicional.");
        EndRelikiaChoice();
        if (reward == RelikiaReward.OpponentDiscards)
        {
            var rival = 1 - actor;
            State.DiscardsRemaining = Math.Min(2, State.Players[rival].Hand.Count);
            Log($"{State.Players[rival].Name} debe elegir {State.DiscardsRemaining} carta(s) para descartar por Relikia.");
            if (State.DiscardsRemaining > 0)
            {
                State.DiscardPlayer = rival;
                State.DiscardResumePhase = Phase.Actions;
                State.Phase = Phase.Discard;
            }
        }
        else
        {
            State.CorruptionsRemaining = Math.Min(2, State.Players.Sum(p => p.Memory.Count(IsCode)));
            Log($"{State.Active.Name} elige {State.CorruptionsRemaining} código(s) para corromper.");
            if (State.CorruptionsRemaining > 0) BeginRelikiaChoice(RelikiaChoice.CorruptCode);
        }
        return CommandResult.Ok();
    }

    public CommandResult ResolveRelikiaCorruption(int actor, int owner, int memoryIndex)
    {
        var error = ValidateRelikiaChoice(actor, RelikiaChoice.CorruptCode);
        if (error != null) return CommandResult.Fail(error);
        if (!IsMemoryCode(owner, memoryIndex)) return CommandResult.Fail("Elige un código sin corromper de una memoria.");
        Corrupt(owner, memoryIndex);
        if (--State.CorruptionsRemaining == 0) EndRelikiaChoice();
        return CommandResult.Ok();
    }

    private void TakeBonus(PlayerState source, PlayerState recipient)
    {
        var id = source.Bonus!;
        source.Bonus = null;
        recipient.Hand.Add(id);
        BonusClaimed?.Invoke(Array.IndexOf(State.Players, source), Array.IndexOf(State.Players, recipient), recipient.Hand.Count - 1, id);
    }

    private void DealBonus(PlayerState p, Action? completed = null) => MoveTopCard(p, id =>
    {
        p.Bonus = id;
        BonusPlaced?.Invoke(Array.IndexOf(State.Players, p), id);
    }, completed);

    private void RefillBonus(PlayerState p, Action? completed = null)
    {
        DealBonus(p, () =>
        {
            if (p.Bonus != null) Log($"{p.Name} coloca {CardCatalog.Get(p.Bonus).Name} como nuevo Bonus.");
            completed?.Invoke();
        });
    }

    private static bool IsCode(string id) => CardCatalog.Get(id).Kind == CardKind.Code;
    private bool IsMemoryCode(int owner, int index) => owner >= 0 && owner < State.Players.Length &&
        index >= 0 && index < State.Players[owner].Memory.Count && IsCode(State.Players[owner].Memory[index]);
    private void Corrupt(int owner, int index)
    {
        var p = State.Players[owner];
        var id = p.Memory[index];
        p.Memory.RemoveAt(index);
        p.CorruptedMemory.Add(id);
        Log($"Se corrompe {CardCatalog.Get(id).Name} de la memoria de {p.Name} (boca abajo).");
    }
    private void BeginRelikiaChoice(RelikiaChoice choice)
    {
        State.PendingRelikia = choice;
        State.Phase = Phase.RelikiaChoice;
    }
    private void EndRelikiaChoice()
    {
        State.PendingRelikia = null;
        State.Phase = Phase.Actions;
    }
    private string? ValidateRelikiaChoice(int actor, RelikiaChoice choice) => ValidateTurn(actor, Phase.RelikiaChoice) ??
        (State.PendingRelikia != choice ? "No está pendiente esa elección de Relikia." : null);
}
