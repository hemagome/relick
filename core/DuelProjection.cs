namespace Relick.Core;

public sealed class DuelPlayerView
{
    public string Name { get; set; } = "";
    public string RelikarioId { get; set; } = "yuno";
    public int Health { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool HasReachedZero { get; set; }
    public int MaxHealth { get; set; }
    public int Level { get; set; }
    public int Pr { get; set; }
    public int Dexterity { get; set; }
    /// <summary>Cosmetic sleeve and die (CosmeticCatalog ids), public to both players.</summary>
    public string Sleeve { get; set; } = CosmeticCatalog.DefaultSleeve;
    public string Dice { get; set; } = CosmeticCatalog.DefaultDice;
    public int Actions { get; set; }
    public int DeckCount { get; set; }
    public int HandCount { get; set; }
    public string?[] Hand { get; set; } = [];
    public string[] Skills { get; set; } = [];
    public int[] SkillPositions { get; set; } = [];
    public int SkillPosition(int index) => SkillPositions.Length == Skills.Length ? SkillPositions[index] : index;
    public int SkillIndexAtPosition(int position) => Enumerable.Range(0, Skills.Length).FirstOrDefault(index => SkillPosition(index) == position, -1);
    public int[] Counters { get; set; } = [];
    public int EquipmentCounters { get; set; }
    public int EquipmentDie { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public FieldTarget? ZahiraHost { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public FieldTarget? ZahiraHost2 { get; set; }
    public FieldTarget? Zahira(int slot) => slot == 0 ? ZahiraHost : ZahiraHost2;
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int EquipmentStackedCopies { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string[]? EquipmentCovers { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int StarEffects { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool BonusEffectUsed { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int[]? ExchangeEffectsUsed { get; set; }
    public string? Equipment { get; set; }
    public string? Bonus { get; set; }
    public string[] Attached { get; set; } = [];
    public string[] Exchange { get; set; } = [];
    public string[] Memory { get; set; } = [];
    // Original owner of each hand slot. Null at an index whose card is hidden;
    // the array itself is omitted when no slot is visible.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int?[]? HandOwners { get; set; }
    public int CorruptedCount { get; set; }
}

public sealed class DuelSnapshot
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool LegacyBlueEgg { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool AutoPassCombos { get; set; }
    public ulong Revision { get; set; }
    public int MySeat { get; set; }
    public int CurrentPlayer { get; set; }
    public int DecisionPlayer { get; set; }
    public int Turn { get; set; }
    public Phase Phase { get; set; }
    public Phase? Stage { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? TrapCardId { get; set; }
    /// <summary>The two public cards whose exchange met the pending Trap recipe; older servers leave it empty.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string[]? TrapIngredients { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int DiscardsRemaining { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? DiscardActivationCard { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int ActivationDiscardsRemaining { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int ExtraDrawStages { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int ExtraCombatStages { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int ExtraActionStages { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int ExtraFinalStages { get; set; }
    public int? Winner { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? EndReason { get; set; }
    public int Die { get; set; }
    public string DieCaption { get; set; } = "";
    public string LastCombat { get; set; } = "";
    public List<DuelVisualEvent> Visuals { get; set; } = [];
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public DuelResolutionView? Resolution { get; set; }
    /// <summary>The pending card decision as both players may see it; its choices reach the decider as options.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public DecisionView? Decision { get; set; }
    /// <summary>The Error de Carga window follows a Code's die result, shown in <see cref="Die"/>.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool ErrorAfterDie { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<DuelHistoryEntry>? History { get; set; }
    public DuelPlayerView[] Players { get; set; } = [];
    public List<DuelOption> Options { get; set; } = [];

    private static bool HandSlotVisible(GameState s, PlayerState p, int slot, int ownerSeat, int viewerSeat)
        => ownerSeat == viewerSeat || p.IsHandCardRevealed(slot) ||
           (s.Phase == Phase.MagnetChoice && s.CurrentPlayer == viewerSeat) ||
           (s.Phase == Phase.Decision && s.PendingDecision is { } decision && decision.Decider == viewerSeat && decision.ShowsHandOf == ownerSeat) ||
           // Legacy: a duel saved while Resetear la Mente had its own phase.
           (s.Phase == Phase.MindResetChoice && s.PendingMindReset?.Actor == viewerSeat && s.PendingMindReset.Target == ownerSeat);

    private static int?[]? VisibleHandOwners(GameState s, PlayerState p, int ownerSeat, int viewerSeat)
    {
        var visible = false;
        var owners = new int?[p.Hand.Count];
        for (var slot = 0; slot < p.Hand.Count; slot++)
        {
            if (!HandSlotVisible(s, p, slot, ownerSeat, viewerSeat)) continue;
            owners[slot] = p.IsForeignHandCard(slot) ? 1 - ownerSeat : ownerSeat;
            visible = true;
        }
        return visible ? owners : null;
    }

    public static DuelSnapshot For(GameEngine game, int seat, ulong revision)
    {
        var s = game.State;
        var stagePhase = s.Phase == Phase.DestructionResponse ? s.PendingDestruction!.ResumePhase : s.Phase;
        if (s.Phase == Phase.TrapCraftChoice) stagePhase = s.TrapCraftResumePhase!.Value;
        if (s.Phase == Phase.ZahiraTarget) stagePhase = s.PendingZahiraPlacement!.ResumePhase;
        if (s.Phase == Phase.ErrorResponse && game.IsFieldCodeActivation) stagePhase = Phase.Combat;
        if (s.PendingTurnBuff != null) stagePhase = s.PendingTurnBuff.ResumePhase;
        if (s.PendingReload != null && !s.IsOver) stagePhase = Phase.Actions;
        if (s.PendingTotemDamage != null && !s.IsOver) stagePhase = s.PendingTotemDamage.ResumePhase;
        if (s.PendingGoldenTotem != null && !s.IsOver) stagePhase = Phase.Actions;
        return new DuelSnapshot
        {
            LegacyBlueEgg = game.LegacyBlueEgg,
            AutoPassCombos = seat >= 0 && s.Players[seat].AutoPassCombos,
            TrapCardId = s.Phase == Phase.TrapCraftChoice ? s.TrapCraftQueue![0].CardId : null,
            TrapIngredients = s.Phase == Phase.TrapCraftChoice ? s.TrapCraftQueue![0].Ingredients : null,
            Revision = revision, MySeat = seat, CurrentPlayer = s.CurrentPlayer, DecisionPlayer = s.DecisionPlayer,
            DiscardsRemaining = s.DiscardsRemaining,
            ExtraDrawStages = s.ExtraDrawStages, ExtraCombatStages = s.ExtraCombatStages,
            ExtraActionStages = s.ExtraActionStages, ExtraFinalStages = s.ExtraFinalStages,
            DiscardActivationCard = s.PendingTurnBuff?.CardId,
            ActivationDiscardsRemaining = s.PendingTurnBuff is { Completed: false } cost ? cost.RequiredDiscards - cost.DiscardsPaid : 0,
            Turn = s.Turn, Phase = s.Phase, Winner = s.Winner, Die = s.PendingAttackRoll?.Die ?? s.PendingDieRoll?.Die ?? s.PendingTurnBuff?.AttackRoll?.Die ?? s.PendingTurnBuff?.DieRoll?.Die ?? s.LastDie,
            Stage = stagePhase == Phase.SniperChoice ? s.PendingSniper!.ResumePhase : stagePhase == Phase.MindResetChoice ? (s.PendingMindReset ?? s.PendingTurnBuff?.MindReset)!.ResumePhase : stagePhase is Phase.CodeResponse or Phase.PointsChoice or Phase.DestroyChoice or Phase.ErrorResponse or Phase.RelikiaChoice or Phase.DeckSearchChoice or Phase.BlueEggDrawChoice ? Phase.Actions :
                stagePhase == Phase.Decision ? (s.PendingDecision ?? s.PendingTurnBuff?.Decision)!.ResumePhase :
                stagePhase == Phase.MemoryRecoveryChoice ? Phase.Actions :
                stagePhase is Phase.DieRerollChoice or Phase.RecycleChoice ? (s.PendingDieRoll ?? s.PendingTurnBuff?.DieRoll)!.ResumePhase : stagePhase == Phase.Discard ? s.DiscardResumePhase :
                stagePhase is Phase.DefenseChoice or Phase.MagnetChoice or Phase.AttackRerollChoice or Phase.EffectDamageChoice ? Phase.Combat : stagePhase,
            DieCaption = game.CurrentDieCaption,
            LastCombat = s.LastCombat, EndReason = s.IsOver ? game.PublicEndReason : null, Resolution = game.PublicResolution(),
            ErrorAfterDie = s.ErrorAfterDie && s.Phase == Phase.ErrorResponse,
            Decision = s.Phase == Phase.Decision && s.PendingDecision is { } decision ? new DecisionView { Source = decision.Source, Decider = decision.Decider,
                Kind = decision.Kind, Title = CardCatalog.Get(decision.Source).Name, Instruction = decision.Instruction } : null,
            History = game.PublicHistory?.Entries.ToList(),
            Options = seat < 0 ? [] : DuelOptions.For(game, seat),
            Players = s.Players.Select((p, index) => new DuelPlayerView
            {
                Name = p.Name, Health = p.Health, MaxHealth = p.MaxHealth, Level = p.Level, Pr = p.Relikia, Dexterity = p.Dexterity, Sleeve = p.Sleeve ?? CosmeticCatalog.DefaultSleeve, Dice = p.Dice ?? CosmeticCatalog.DefaultDice,
                HasReachedZero = p.HasReachedZero,
                RelikarioId = p.Relikario.Id,
                Actions = p.ActionsLeft, DeckCount = p.Deck.Count, HandCount = p.Hand.Count,
                Hand = p.Hand.Select((id, i) => HandSlotVisible(s, p, i, index, seat) ? id : null).ToArray(),
                // Ownership is only disclosed for slots whose identity is already visible;
                // a hidden card must not leak whether it originally belonged to the rival.
                HandOwners = VisibleHandOwners(s, p, index, seat),
                Skills = p.Skills.ToArray(), SkillPositions = Enumerable.Range(0, p.Skills.Count).Select(p.SkillPosition).ToArray(), Counters = Enumerable.Range(0, p.Skills.Count).Select(p.CountersOnSkill).ToArray(),
                BonusEffectUsed = p.BonusEffectUsed, ExchangeEffectsUsed = p.ExchangeEffectsUsed?.ToArray(),
                ZahiraHost = p.ZahiraHost, ZahiraHost2 = p.ZahiraHost2,
                Equipment = p.Equipment, EquipmentDie = p.EquipmentDie, EquipmentCounters = p.EquipmentCounters, EquipmentStackedCopies = p.EquipmentStackedCopies, EquipmentCovers = p.EquipmentCovers?.ToArray(), StarEffects = p.StarEffects, Bonus = p.Bonus, Attached = p.AttachedCards.ToArray(), Exchange = p.Exchange.ToArray(),
                Memory = p.Memory.ToArray(), CorruptedCount = p.CorruptedMemory.Count
            }).ToArray()
        };
    }
}
