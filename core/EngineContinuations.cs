namespace Relick.Core;

public enum ContinuationKind { Draw, MoveHand, MoveBonus, RefillDone, RefillBonus, RelikiaPlacement, RollRelikia, ResumeCombo }
public sealed class EngineContinuation
{
    public ContinuationKind Kind { get; set; }
    public int Actor { get; set; }
    public int Count { get; set; }
    public EngineContinuation? Next { get; set; }
    public Phase Phase { get; set; }
    public PendingAttackRoll? Attack { get; set; }
    public PendingDieRoll? Die { get; set; }
    public DieEffect? SavedDieEffect { get; set; }
}
public sealed class DieEffect
{
    public DiePurpose Purpose { get; set; }
    public int Actor { get; set; }
    public int AttackDie { get; set; }
    public string SkillId { get; set; } = "";
    public bool Rerolled { get; set; }
    public EngineContinuation? Next { get; set; }
}
public enum ActivationKind { Code, Equipment, Reroll, Counter }
public enum ActivationResume { Original, RestorePhase }
public sealed class EngineActivation
{
    public int Actor { get; set; }
    public int Actions { get; set; }
    public int Pr { get; set; }
    public string CardId { get; set; } = "";
    public Phase ResumePhase { get; set; }
    public Phase? CancelPhase { get; set; }
    public ActivationKind Kind { get; set; }
    public ActivationResume ResumeMode { get; set; }
    public bool Canceled { get; set; }
    public bool ReplayCorrupted { get; set; }
    public bool CanInterrupt { get; set; } = true;
    public int CorruptedIndex { get; set; }
    public bool FromMemory { get; set; }
    public int MemoryIndex { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public EngineActivation? Parent { get; set; }
    public EngineActivation() { }
    public EngineActivation(int actor, string id, int actions, int pr, Phase phase)
    { Actor = actor; CardId = id; Actions = actions; Pr = pr; ResumePhase = phase; }
}

public sealed partial class GameEngine
{
    private void Continue(EngineContinuation? next)
    {
        if (next == null || State.IsOver) return;
        var p = State.Players[next.Actor];
        switch (next.Kind)
        {
            case ContinuationKind.Draw: DrawCards(p, next.Count, next.Next); break;
            case ContinuationKind.MoveHand: MoveTopCard(p, false, next.Next); break;
            case ContinuationKind.MoveBonus: MoveTopCard(p, true, next.Next); break;
            case ContinuationKind.RefillDone:
                if (p.Bonus != null) Log($"{p.Name} coloca {CardCatalog.Get(p.Bonus).Name} como nuevo Bonus.");
                Continue(next.Next); break;
            case ContinuationKind.RefillBonus: RefillBonus(p, next.Next); break;
            case ContinuationKind.RelikiaPlacement: BeginRelikiaChoice(RelikiaChoice.PlaceHandCard); break;
            case ContinuationKind.RollRelikia: RollWithRelikario(p, DiePurpose.Relikia, new DieEffect { Purpose = DiePurpose.Relikia, Actor = next.Actor }); break;
            case ContinuationKind.ResumeCombo:
                State.Phase = next.Phase; State.PendingAttackRoll = next.Attack; State.PendingDieRoll = next.Die; _pendingDieEffect = next.SavedDieEffect;
                if (next.Attack != null) { State.LastAttackDie = next.Attack.Die; if (!CanRespondToAttackDie()) FinishAttackRoll(next.Attack.Die, true); }
                else if (!CanRespondToEffectDie()) ResolveDieReroll(next.Actor, false);
                break;
        }
    }

    private void ResolveDieEffect(DieEffect effect, int die)
    {
        var p = State.Players[effect.Actor];
        switch (effect.Purpose)
        {
            case DiePurpose.MagnetDamage:
                Log($"{p.Name} lanza D6 por el daño normal de Magnet Shuffle: {die}.");
                ResolveAttackResult(effect.Actor, CardCatalog.Get(effect.SkillId), effect.AttackDie, die, effect.Rerolled); break;
            case DiePurpose.PointsChange:
                var points = die switch { 1 => 300, 2 => 400, 3 or 4 => 500, 5 => 1000, _ => 1500 };
                State.PendingPoints = new(effect.Actor, die, points); State.Phase = Phase.PointsChoice;
                State.LastCombat = $"{p.Name} · Aumento de Relick Points · D6 = {die} · Elige destino de {points} PR"; Log(State.LastCombat); break;
            case DiePurpose.ExtraLife:
                var amount = die switch { 1 => 1, 2 => 2, 6 => 4, _ => 3 }; var healed = Heal(p, amount);
                State.LastCombat = $"{p.Name} · Vida Extra · D6 = {die} · Recupera {healed} vida(s)";
                Log($"{p.Name} resuelve Vida Extra: D6 = {die}, gana {amount} vida(s); recupera {healed} hasta su máximo de {p.MaxHealth}."); break;
            case DiePurpose.Recycling:
                _pendingDieEffect = effect;
                State.PendingDieRoll = new(effect.Actor, DiePurpose.Recycling, die, State.Phase);
                State.Phase = Phase.RecycleChoice;
                Log($"{p.Name} agota su mazo por primera vez: D6 = {die}. Elige {Math.Min(die, p.Memory.Count)} carta(s) de su memoria para devolverlas al mazo y barajarlo.");
                if (p.Memory.Count == 0) ResolveRecycling(effect.Actor, []);
                break;
            case DiePurpose.Relikia:
                State.LastCombat = $"{p.Name} · Relikia: robar el Bonus · D6 = {die}";
                Log($"{p.Name} lanza D6 por Relikia: {die}." + (die >= 5 ? " Elige un efecto adicional." : " Sin efecto adicional."));
                if (die >= 5) BeginRelikiaChoice(RelikiaChoice.BonusReward); break;
        }
    }

    private void ResumeActivation(EngineActivation frame)
    {
        if (frame.ResumeMode == ActivationResume.RestorePhase)
        {
            State.Phase = frame.ResumePhase;
            var card = CardCatalog.Get(frame.CardId);
            if (State.Phase == Phase.DestroyChoice && card.DestroyChosenZone && !TimeBombTargets().Any())
            {
                State.DestroyActor = null; State.Phase = frame.CancelPhase ?? Phase.Actions;
                ResolveCode(State.Players[frame.Actor], card);
            }
            return;
        }
        var p = State.Players[frame.Actor];
        switch (frame.Kind)
        {
            case ActivationKind.Code: ActivateCodeAfterErrors(frame.Actor, CardCatalog.Get(frame.CardId), frame.FromMemory, frame.MemoryIndex); break;
            case ActivationKind.Equipment:
                p.Memory.RemoveAt(frame.MemoryIndex); if (p.Equipment != null) DestroyEquipment(p); p.Equipment = frame.CardId;
                Log($"{p.Name} pone {CardCatalog.Get(frame.CardId).Name} en su zona de equipo (1 acción)."); break;
            case ActivationKind.Reroll: _activeActivation = frame.Parent; ResolveRerollComboEffect(frame.Actor, CardCatalog.Get(frame.CardId)); break;
            default: throw new InvalidOperationException("Activación pendiente desconocida.");
        }
    }
    private void CancelActivation(EngineActivation frame)
    {
        if (frame.Kind == ActivationKind.Code)
        {
            if (frame.ReplayCorrupted)
            {
                NotifyPileMove(State.Players[frame.Actor], frame.CardId, CardZone.Corrupted, frame.CorruptedIndex);
                State.Players[frame.Actor].CorruptedMemory.RemoveAt(frame.CorruptedIndex);
                State.Players[frame.Actor].Memory.Insert(Math.Min(frame.MemoryIndex, State.Players[frame.Actor].Memory.Count), frame.CardId);
            }
            ClearCodeDecisions();
        }
        else if (frame.Kind == ActivationKind.Equipment) NotifyPileMove(State.Players[frame.Actor], frame.CardId, CardZone.Activation);
        State.Phase = frame.CancelPhase ?? frame.ResumePhase;
        if (frame.Kind == ActivationKind.Reroll) _activeActivation = frame.Parent;
    }
}
