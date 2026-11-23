namespace Relick.Core;

public enum ContinuationKind { Draw, MoveHand, MoveBonus, RefillDone, RefillBonus, RelikiaPlacement, RollRelikia, ResumeCombo, ResumeMindReset, CompleteExchange, AttackDieReady, AttackOneEquipment, UsePendant, UsePc, BoxDefense, BoxCode, EquipInstall, ApplyDamage, FinishAttackDamage, RerollDraw, TurnStartDraw, FieldCodeReward, QuickLoadDie, ReloadHandsResume, DrawStageReady, ReplaceEquipment, GoldenTotemPlacement, PlaceSuperMagnet, PlaceZahira, FinishZahiraDestruction, CardEffectStep, ResolveSkillAttack, AreaRaidSideB, EquipFromDeck, CheckAreaEquipDeck, PurpleRingPoints, LoadGalacticShield, OrientMovedArea, ConfirmedAttackDie, ConfirmedEffectDie, BreakRobotize, BlockOpponentEquipment, BreakMetalHood, IronwingBonus, IronwingRoll, IronwingFireDamage, IronwingFireDestroy, IronwingFieldDestroyed, BlackHoleOtherSide, BlackHoleDestroy, BlackHoleCardDestroyed, BlackHoleFinish, IronwingDiePaid, LoadMagicTome, FieldReturned, BikeTurnStart, GhostAttackDie, GhostEffectDie, TakeMagnetTarget, RelikiaOpponentDiscards, InstallOpponentEquipment, DiscardEffectsComplete, WdBoostPaid, MultiDrivePaid, ApplyAttackDamage }
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
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? NonlethalDamageTarget { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<int>? UsedLightEquipment { get; set; }
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
    // Turbo Overlap rolls both dice up front, then resolves any modifiers in order.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? OverlapSecondDie { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool OverlapSecondDieIsD10 { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public PendingAttackRoll? SkillAttack { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int IncomingDamage { get; set; }
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
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool EquipOpponent { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public int EquipmentRecipient => EquipOpponent ? 1 - Actor : Actor;
    public bool CanInterrupt { get; set; } = true;
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool RespondsToTarget { get; set; }
    public bool FromMemory { get; set; }
    public int MemoryIndex { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool CorruptOnMemory { get; set; }
    // Seat whose memory holds the activating card; null means the actor's own memory.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? MemoryOwner { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public int MemorySeat => MemoryOwner ?? Actor;
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public FieldTarget? FieldSource { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? FieldCardId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? HeldCard { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public TomeCard? TomeSource { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool ChooseDieResult { get; set; }
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
            case ContinuationKind.ConfirmedAttackDie: ResolveConfirmedAttackDie(next.Attack!); break;
            case ContinuationKind.ConfirmedEffectDie: ApplyDieEffect(next.SavedDieEffect!, next.Value); break;
            case ContinuationKind.BreakRobotize:
            case ContinuationKind.BreakMetalHood:
                Log($"{p.Name} saca 1 en su D6 y debe destruir {FieldCardName(next.Target!)}.");
                RequestDestruction(next.Target!, next.Next);
                break;
            case ContinuationKind.AttackOneEquipment: DestroyAttackOneEquipment(next.Actor, next.Next); break;
            case ContinuationKind.IronwingDiePaid: ResumeIronwingDie(next); break;
            case ContinuationKind.IronwingBonus: OfferIronwingBonus(next.Attack!, next.Count); break;
            case ContinuationKind.IronwingRoll: FinishIronwingRoll(next); break;
            case ContinuationKind.IronwingFireDamage: FinishIronwingFireDamage(next.Actor); break;
            case ContinuationKind.IronwingFireDestroy: DestroyWithIronwingFire(next); break;
            case ContinuationKind.IronwingFieldDestroyed: FinishIronwingFieldDestruction(next); break;
            case ContinuationKind.BlackHoleOtherSide: RollBlackHole(next.Actor, 1 - next.Value, sideA: false, destroyArea: next.Flag); break;
            case ContinuationKind.BlackHoleDestroy: ContinueBlackHoleDestruction(next); break;
            case ContinuationKind.BlackHoleCardDestroyed: FinishBlackHoleDestruction(next); break;
            case ContinuationKind.BlackHoleFinish: FinishBlackHole(next); break;
            case ContinuationKind.CardEffectStep: ContinueCardEffect(next); break;
            case ContinuationKind.AreaRaidSideB: PresentRaidSideB(next.Value, next.Actor); break;
            case ContinuationKind.EquipFromDeck: FinishEquipFromDeck(next); break;
            case ContinuationKind.CheckAreaEquipDeck: Exhaust(p); break;
            case ContinuationKind.PurpleRingPoints: AwardPurpleRingPoints(next.Actor); CompletePendingPlay(); break;
            case ContinuationKind.LoadGalacticShield: LoadGalacticShield(next); break;
            case ContinuationKind.OrientMovedArea:
                AskAreaOrientation(next.Actor, moved: true);
                break;
            case ContinuationKind.UsePendant: FinishPendant(next.Actor, (PendantEffect)next.Value); break;
            case ContinuationKind.UsePc: FinishPc(next.Actor, next.Value); break;
            case ContinuationKind.BoxDefense:
                Log($"{p.Name} usa Caja de Cartón y cancela el daño del ataque.");
                ResolveAttackDamage(true, "Caja de Cartón", cardId: CardCatalog.CardboardBox.Id); break;
            case ContinuationKind.BoxCode: FinishBoxCode(next.Actor); break;
            case ContinuationKind.EquipInstall: FinishEquipmentInstall(); break;
            case ContinuationKind.ReplaceEquipment: ReplaceEquipment(next); break;
            case ContinuationKind.BlockOpponentEquipment: CompleteActivationIfFinished(); break;
            case ContinuationKind.InstallOpponentEquipment: BeginEquipmentInstall(_activeActivation!, offerResponse: false); break;
            case ContinuationKind.TakeMagnetTarget: FinishMagnetChoice(next); break;
            case ContinuationKind.RelikiaOpponentDiscards: ResolveRelikiaOpponentDiscards(next.Actor); break;
            case ContinuationKind.PlaceSuperMagnet: FinishSuperMagnetPlacement(next.Actor); break;
            case ContinuationKind.PlaceZahira: FinishZahiraPlacement(next); break;
            case ContinuationKind.FinishZahiraDestruction: FinishZahiraDestruction(next); break;
            case ContinuationKind.GoldenTotemPlacement: ContinueGoldenTotemPlacement(next); break;
            case ContinuationKind.ApplyDamage: ApplyHealthDamage(next.Actor, next.Count, next.Value, next.Next); break;
            case ContinuationKind.FinishAttackDamage: FinishAttackDamage(next.Flag); break;
            case ContinuationKind.ApplyAttackDamage: ApplyAttackDamage(next.Flag, next.Text, cardId: next.Step); break;
            case ContinuationKind.ResolveSkillAttack: ResolveSkillAttack(next.Attack!); break;
            case ContinuationKind.RerollDraw: FinishRerollDraw(next); break;
            case ContinuationKind.TurnStartDraw: DrawAtTurnStart(); break;
            case ContinuationKind.LoadMagicTome: LoadMagicTome(next); break;
            case ContinuationKind.FieldReturned: FinishFieldReturn(next); break;
            // The turn-start triggers after an expiring equipment; the name predates the trigger modules.
            case ContinuationKind.BikeTurnStart: BeginTurnStartTriggers(); break;
            case ContinuationKind.GhostAttackDie:
                if (next.Flag || !OfferGhostGift(next.Actor, new EngineContinuation
                    { Kind = ContinuationKind.GhostAttackDie, Actor = next.Actor, Attack = next.Attack, Flag = true }))
                    ResolveConfirmedAttackDie(next.Attack!);
                break;
            case ContinuationKind.GhostEffectDie: ResolveDieAfterGhost(next.SavedDieEffect!, next.Value); break;
            case ContinuationKind.DrawStageReady: FinishDrawStage(next.Flag); break;
            case ContinuationKind.ReloadHandsResume: ContinueReloadHands(); break;
            case ContinuationKind.QuickLoadDie:
                RollWithRelikario(p, DiePurpose.QuickLoad, new DieEffect { Purpose = DiePurpose.QuickLoad, Actor = next.Actor });
                break;
            case ContinuationKind.FieldCodeReward:
                if (next.Flag) DrawCards(p, 1);
                break;
            case ContinuationKind.CompleteExchange: CompleteExchange(p, next.Next); break;
            case ContinuationKind.DiscardEffectsComplete: CompleteActivationIfFinished(); break;
            case ContinuationKind.WdBoostPaid: ResumeWdDie(next); break;
            case ContinuationKind.MultiDrivePaid: RerollMultiDrive(next.Attack!); break;
            case ContinuationKind.Draw: DrawCards(p, next.Count, next.Next); break;
            case ContinuationKind.MoveHand: MoveTopCard(p, false, next.Next); break;
            case ContinuationKind.MoveBonus: MoveTopCard(p, true, next.Next); break;
            case ContinuationKind.RefillDone:
                if (p.Bonus is { } bonus)
                {
                    Log($"{p.Name} coloca {CardCatalog.Get(bonus).Name} como nuevo Bonus.");
                    if (next.Flag) QueueExchangeTrigger(next.Actor, bonus, ExchangeMoment.RefilledBonus, FieldZone.Bonus, -1);
                }
                Continue(next.Next); break;
            case ContinuationKind.RefillBonus: RefillBonus(p, next.Next); break;
            case ContinuationKind.RelikiaPlacement: BeginRelikiaChoice(RelikiaChoice.PlaceHandCard); break;
            case ContinuationKind.RollRelikia: RollWithRelikario(p, DiePurpose.Relikia, new DieEffect { Purpose = DiePurpose.Relikia, Actor = next.Actor }); break;
            case ContinuationKind.ResumeMindReset:
                State.Phase = next.Phase; State.PendingAttackRoll = next.Attack; State.PendingDieRoll = next.Die; _pendingDieEffect = next.SavedDieEffect;
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
        if (die == 1 && TryBreakCardsOnOwnOne(effect.Actor, new EngineContinuation
            { Kind = ContinuationKind.ConfirmedEffectDie, Actor = effect.Actor, Value = die, SavedDieEffect = effect })) return;
        ApplyDieEffect(effect, die);
    }

    private void ApplyDieEffect(DieEffect effect, int die)
    {
        if (effect.Step != null)
        {
            ContinueCardEffect(new EngineContinuation { Kind = ContinuationKind.CardEffectStep, Actor = effect.Actor, Text = effect.SkillId, Step = effect.Step, Value = die });
            return;
        }
        // Below: rolls of cards whose outcome is still in the engine.
        var p = State.Players[effect.Actor];
        switch (effect.Purpose)
        {
            case DiePurpose.BlackHole: ResolveBlackHoleDie(effect, die); break;
            case DiePurpose.TurboSwordDamage:
            case DiePurpose.TurboOverlapDamage: ResolveTurboDamageDie(effect, die); break;
            case DiePurpose.TurboSwordDefense: ResolveTurboSwordDefense(effect, die); break;
            case DiePurpose.Zahira: ResolveZahiraDie(effect.Actor, die, effect.Slot); break;
            case DiePurpose.FieldCombat: ResolveFieldCombatDie(effect, die); break;
            case DiePurpose.SkillLearning:
                Log($"{p.Name} resuelve el aprendizaje de Reverse Shuffle: D6 = {die}, roba {(die + 1) / 2} carta(s).");
                DrawCards(p, (die + 1) / 2, effect.Next);
                break;
            case DiePurpose.PcStore:
                if (p.Equipment == CardCatalog.Pc.Id) p.EquipmentDie = die;
                Log($"{p.Name} guarda un {die} sobre PC 8BITS.");
                if (effect.Next != null) Continue(effect.Next);
                break;
            case DiePurpose.MagnetDamage:
                Log($"{p.Name} lanza D6 por el daño normal de Magnet Shuffle: {die}.");
                ResolveAttackResult(effect.Actor, CardCatalog.Get(effect.SkillId), effect.AttackDie, die, effect.Rerolled, attackDamageBonus: effect.AttackDamageBonus); break;
            case DiePurpose.Recycling:
                _pendingDieEffect = effect;
                State.PendingDieRoll = new(effect.Actor, DiePurpose.Recycling, die, State.Phase);
                State.Phase = Phase.RecycleChoice;
                Log($"{p.Name} agota su mazo por primera vez: D6 = {die}. Elige {Math.Min(die, p.Memory.Count)} carta(s) de su memoria para devolverlas al mazo y barajarlo.");
                if (p.Memory.Count == 0) ResolveRecycling(effect.Actor, []);
                break;
            case DiePurpose.Relikia:
                State.LastCombat = $"{p.Name} · Relikia: robar el Bonus · D6 = {die}";
                Log($"{p.Name} lanza D6 por Relikia: {die}." + (die is 5 or 6 ? " Elige un efecto adicional." : " Sin efecto adicional."));
                if (die is 5 or 6) BeginRelikiaChoice(RelikiaChoice.BonusReward); break;
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
                BeginEquipmentInstall(frame);
                break;
            case ActivationKind.MindReset: TryResolveWithEffect(p, CardCatalog.Get(frame.CardId)); break;
            case ActivationKind.Reroll: _activeActivation = frame.Parent; ResolveRerollComboEffect(frame.Actor, CardCatalog.Get(frame.CardId)); break;
            default: throw new InvalidOperationException("Activación pendiente desconocida.");
        }
    }
    private void CancelActivation(EngineActivation frame)
    {
        if (frame.Kind == ActivationKind.TurnBuff) { CompleteTurnBuff(); return; }
        if (frame.HeldCard != null) { FinishHeldCard(frame); State.Phase = frame.ResumePhase; return; }
        if (frame.Kind == ActivationKind.FieldCode)
        {
            if (frame.FieldSource is { } source) DestroyCardNow(source);
        }
        else if (!CorruptWaitingFieldCard(frame) && (frame.Kind == ActivationKind.Equipment || frame.CardId == CardCatalog.Robotize.Id))
            NotifyPileMove(State.Players[frame.Actor], frame.CardId, CardZone.Activation, memoryOwner: frame.MemorySeat);
        State.Phase = frame.ResumePhase;
        if (frame.Kind == ActivationKind.Reroll) _activeActivation = frame.Parent;
    }
}
