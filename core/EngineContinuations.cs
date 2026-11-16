namespace Relick.Core;

public enum ContinuationKind { Draw, MoveHand, MoveBonus, RefillDone, RefillBonus, RelikiaPlacement, RollRelikia, ResumeCombo, ResumeMindReset, CompleteExchange, AttackDieReady, AttackOneEquipment, FinishCodeDestruction, UsePendant, UsePc, BoxDefense, BoxCode, EquipInstall, ApplyDamage, FinishAttackDamage, RerollDraw, TurnStartDraw, FieldCodeReward, QuickLoadDie, ReloadHandsResume, DrawStageReady, ReplaceEquipment, GoldenTotemPlacement, GoldenTotemReplacement, PlaceSuperMagnet, PlaceZahira, FinishZahiraDestruction, OfferBlueEggDraw, CardEffectStep }
public sealed class EngineContinuation
{
    public ContinuationKind Kind { get; set; }
    public int Actor { get; set; }
    public int Count { get; set; }
    public int Value { get; set; }
    public bool Flag { get; set; }
    public bool Prevented { get; set; }
    public FieldTarget? Target { get; set; }
    public string Text { get; set; } = "";
    /// <summary>For <see cref="ContinuationKind.CardEffectStep"/>: the step of the card in <see cref="Text"/>.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Step { get; set; }
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
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int AttackDamageBonus { get; set; }
    public string SkillId { get; set; } = "";
    /// <summary>For a card module's roll: the step of the card in <see cref="SkillId"/> that receives the result.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Step { get; set; }
    public bool Rerolled { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int Slot { get; set; }
    public EngineContinuation? Next { get; set; }
}
public enum ActivationKind { Code, Equipment, Reroll, Counter, MindReset, TurnBuff, FieldCode }
public sealed class EngineActivation
{
    public int Actor { get; set; }
    public int Actions { get; set; }
    public int Pr { get; set; }
    public string CardId { get; set; } = "";
    public Phase ResumePhase { get; set; }
    public ActivationKind Kind { get; set; }
    public bool Canceled { get; set; }
    public bool ReplayCorrupted { get; set; }
    public bool FinishSniperExchange { get; set; }
    public bool CanInterrupt { get; set; } = true;
    public bool FromMemory { get; set; }
    public int MemoryIndex { get; set; }
    // Seat whose memory holds the activating card; null means the actor's own memory.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? MemoryOwner { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public int MemorySeat => MemoryOwner ?? Actor;
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public FieldTarget? FieldSource { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public EngineActivation? Parent { get; set; }
    public EngineActivation() { }
    public EngineActivation(int actor, string id, int actions, int pr, Phase phase)
    { Actor = actor; CardId = id; Actions = actions; Pr = pr; ResumePhase = phase; }
}

public sealed partial class GameEngine
{
    private void Continue(EngineContinuation? next)
    {
        if (State.IsOver) return;
        if (next == null) { CompletePendingPlay(); return; }
        var p = State.Players[next.Actor];
        switch (next.Kind)
        {
            case ContinuationKind.AttackDieReady: ContinueAttackDie(); break;
            case ContinuationKind.AttackOneEquipment: DestroyAttackOneEquipment(next.Actor, next.Next); break;
            case ContinuationKind.FinishCodeDestruction: FinishCodeDestruction(next); break;
            case ContinuationKind.OfferBlueEggDraw: OfferBlueEggDraw(next.Actor); break;
            case ContinuationKind.CardEffectStep: ContinueCardEffect(next); break;
            case ContinuationKind.UsePendant: FinishPendant(next.Actor, (PendantEffect)next.Value); break;
            case ContinuationKind.UsePc: FinishPc(next.Actor, next.Value); break;
            case ContinuationKind.BoxDefense:
                Log($"{p.Name} usa Caja de Cartón y cancela el daño del ataque.");
                ResolveAttackDamage(true, "Caja de Cartón"); break;
            case ContinuationKind.BoxCode: FinishBoxCode(next.Actor); break;
            case ContinuationKind.EquipInstall: FinishEquipmentInstall(); break;
            case ContinuationKind.ReplaceEquipment: ReplaceEquipment(next); break;
            case ContinuationKind.PlaceSuperMagnet: FinishSuperMagnetPlacement(next.Actor); break;
            case ContinuationKind.PlaceZahira: FinishZahiraPlacement(next); break;
            case ContinuationKind.FinishZahiraDestruction: FinishZahiraDestruction(next); break;
            case ContinuationKind.GoldenTotemPlacement: ContinueGoldenTotemPlacement(next); break;
            case ContinuationKind.GoldenTotemReplacement:
                if (next.Next != null) next.Next.Prevented = true;
                Log($"{p.Name} conserva su Tótem de Salvación gracias a la sustitución de Tótem de Oro.");
                Continue(next.Next); break;
            case ContinuationKind.ApplyDamage: ApplyHealthDamage(next.Actor, next.Count, next.Value, next.Next); break;
            case ContinuationKind.FinishAttackDamage: FinishAttackDamage(next.Flag); break;
            case ContinuationKind.RerollDraw: FinishRerollDraw(next); break;
            case ContinuationKind.TurnStartDraw: DrawAtTurnStart(); break;
            case ContinuationKind.DrawStageReady: FinishDrawStage(next.Flag); break;
            case ContinuationKind.ReloadHandsResume: ContinueReloadHands(); break;
            case ContinuationKind.QuickLoadDie:
                RollWithRelikario(p, DiePurpose.QuickLoad, new DieEffect { Purpose = DiePurpose.QuickLoad, Actor = next.Actor });
                break;
            case ContinuationKind.FieldCodeReward:
                if (next.Flag) DrawCards(p, 1);
                break;
            case ContinuationKind.CompleteExchange: CompleteExchange(p, next.Next); break;
            case ContinuationKind.Draw: DrawCards(p, next.Count, next.Next); break;
            case ContinuationKind.MoveHand: MoveTopCard(p, false, next.Next); break;
            case ContinuationKind.MoveBonus: MoveTopCard(p, true, next.Next); break;
            case ContinuationKind.RefillDone:
                if (p.Bonus != null) Log($"{p.Name} coloca {CardCatalog.Get(p.Bonus).Name} como nuevo Bonus.");
                Continue(next.Next); break;
            case ContinuationKind.RefillBonus: RefillBonus(p, next.Next); break;
            case ContinuationKind.RelikiaPlacement: BeginRelikiaChoice(RelikiaChoice.PlaceHandCard); break;
            case ContinuationKind.RollRelikia: RollWithRelikario(p, DiePurpose.Relikia, new DieEffect { Purpose = DiePurpose.Relikia, Actor = next.Actor }); break;
            case ContinuationKind.ResumeMindReset:
                State.Phase = next.Phase; State.PendingAttackRoll = next.Attack; State.PendingDieRoll = next.Die; _pendingDieEffect = next.SavedDieEffect;
                if (State.Phase == Phase.MemoryRecoveryChoice && _activeActivation is { } recovery) BeginMemoryRecovery(recovery.Actor);
                RecheckDecision();
                CompletePendingPlay();
                break;
            case ContinuationKind.ResumeCombo:
                State.Phase = next.Phase; State.PendingAttackRoll = next.Attack; State.PendingDieRoll = next.Die; _pendingDieEffect = next.SavedDieEffect;
                if (next.Attack != null) { State.LastAttackDie = next.Attack.Die; if (!CanRespondToAttackDie()) FinishAttackRoll(next.Attack.Die, next.Attack.WasRerolled); }
                else if (!CanRespondToEffectDie()) ResolveDieReroll(next.Actor, false);
                break;
        }
    }

    private void ResolveDieEffect(DieEffect effect, int die)
    {
        if (effect.Step != null)
        {
            ContinueCardEffect(new EngineContinuation { Kind = ContinuationKind.CardEffectStep, Actor = effect.Actor, Text = effect.SkillId, Step = effect.Step, Value = die });
            return;
        }
        // Below: rolls of cards without a module, and rolls saved before their card moved to one.
        var p = State.Players[effect.Actor];
        switch (effect.Purpose)
        {
            case DiePurpose.QuickLoad: ResolveQuickLoadDie(effect.Actor, die); break;
            case DiePurpose.Zahira: ResolveZahiraDie(effect.Actor, die, effect.Slot); break;
            case DiePurpose.FieldCombat: ResolveFieldCombatDie(effect, die); break;
            case DiePurpose.SkillLearning:
                Log($"{p.Name} resuelve el aprendizaje de Reverse Shuffle: D6 = {die}, roba {(die + 1) / 2} carta(s).");
                DrawCards(p, (die + 1) / 2, effect.Next);
                break;
            case DiePurpose.PcStore:
                if (p.Equipment == CardCatalog.Pc.Id) p.EquipmentDie = die;
                Log($"{p.Name} guarda un {die} sobre PC 8BITS.");
                break;
            case DiePurpose.Dodge:
                _activeActivation = null;
                if (die <= 3)
                {
                    Log($"{p.Name} resuelve Esquivar: dado {die}, roba una carta; el ataque sigue pendiente.");
                    DrawCards(p, 1);
                }
                else
                {
                    var attack = State.PendingAttack!;
                    var attacker = State.Players[attack.Attacker];
                    if (!attack.RewardPending)
                        attacker.Relikia = Math.Max(0, attacker.Relikia - CardCatalog.Get(attack.SkillId).Reward);
                    State.PendingAttack = null; State.Phase = Phase.Combat;
                    State.LastCombat = $"{p.Name} niega el ataque con Esquivar (dado {die})."; Log(State.LastCombat);
                }
                break;
            case DiePurpose.MagnetDamage:
                Log($"{p.Name} lanza D6 por el daño normal de Magnet Shuffle: {die}.");
                ResolveAttackResult(effect.Actor, CardCatalog.Get(effect.SkillId), effect.AttackDie, die, effect.Rerolled, attackDamageBonus: effect.AttackDamageBonus); break;
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
        var p = State.Players[frame.Actor];
        switch (frame.Kind)
        {
            case ActivationKind.Code: ActivateCodeAfterErrors(frame.Actor, CardCatalog.Get(frame.CardId), frame.FromMemory, frame.MemoryIndex); break;
            case ActivationKind.TurnBuff:
                // Error de Carga is decided before any activation discards are paid.
                if (State.PendingTurnBuff!.DiscardsPaid < State.PendingTurnBuff.RequiredDiscards)
                    State.Phase = Phase.TurnBuffDiscard;
                else ActivateCodeAfterErrors(frame.Actor, CardCatalog.Get(frame.CardId), frame.FromMemory, frame.MemoryIndex);
                break;
            case ActivationKind.FieldCode:
                RollWithRelikario(p, DiePurpose.FieldCombat, new DieEffect { Purpose = DiePurpose.FieldCombat, Actor = frame.Actor, SkillId = frame.CardId });
                break;
            case ActivationKind.Equipment:
                frame.CanInterrupt = false;
                ReplaceEquipment(new EngineContinuation { Kind = ContinuationKind.ReplaceEquipment, Actor = frame.Actor });
                break;
            case ActivationKind.MindReset: BeginMindReset(frame.Actor); break;
            case ActivationKind.Reroll: _activeActivation = frame.Parent; ResolveRerollComboEffect(frame.Actor, CardCatalog.Get(frame.CardId)); break;
            default: throw new InvalidOperationException("Activación pendiente desconocida.");
        }
    }
    private void CancelActivation(EngineActivation frame)
    {
        if (frame.Kind == ActivationKind.TurnBuff) { CompleteTurnBuff(); return; }
        if (frame.Kind == ActivationKind.FieldCode)
        {
            if (frame.FieldSource is { } source) DestroyCardNow(source);
        }
        else if (frame.Kind == ActivationKind.Equipment) NotifyPileMove(State.Players[frame.Actor], frame.CardId, CardZone.Activation, memoryOwner: frame.MemorySeat);
        State.Phase = frame.ResumePhase;
        if (frame.Kind == ActivationKind.Reroll) _activeActivation = frame.Parent;
    }
}
