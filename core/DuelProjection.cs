namespace Relick.Core;

public sealed class DuelPlayerView
{
    public string Name { get; set; } = "";
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public int Level { get; set; }
    public int Pr { get; set; }
    public int Actions { get; set; }
    public int DeckCount { get; set; }
    public int HandCount { get; set; }
    public string?[] Hand { get; set; } = [];
    public string[] Skills { get; set; } = [];
    public int[] Counters { get; set; } = [];
    public int EquipmentCounters { get; set; }
    public int EquipmentDie { get; set; }
    public string? Equipment { get; set; }
    public string? Bonus { get; set; }
    public string[] Attached { get; set; } = [];
    public string[] Exchange { get; set; } = [];
    public string[] Memory { get; set; } = [];
    public int CorruptedCount { get; set; }
}

public sealed class DuelSnapshot
{
    public ulong Revision { get; set; }
    public int MySeat { get; set; }
    public int CurrentPlayer { get; set; }
    public int DecisionPlayer { get; set; }
    public int Turn { get; set; }
    public Phase Phase { get; set; }
    public Phase? Stage { get; set; }
    public int? Winner { get; set; }
    public int Die { get; set; }
    public string DieCaption { get; set; } = "";
    public string LastCombat { get; set; } = "";
    public List<DuelVisualEvent> Visuals { get; set; } = [];
    public DuelPlayerView[] Players { get; set; } = [];
    public List<DuelOption> Options { get; set; } = [];

    public static DuelSnapshot For(GameEngine game, int seat, ulong revision)
    {
        var s = game.State;
        return new DuelSnapshot
        {
            Revision = revision, MySeat = seat, CurrentPlayer = s.CurrentPlayer, DecisionPlayer = s.DecisionPlayer,
            Turn = s.Turn, Phase = s.Phase, Winner = s.Winner, Die = s.PendingAttackRoll?.Die ?? s.PendingDieRoll?.Die ?? s.LastDie,
            Stage = s.Phase == Phase.SniperChoice ? s.PendingSniper!.ResumePhase : s.Phase == Phase.MindResetChoice ? s.PendingMindReset!.ResumePhase : s.Phase is Phase.CodeResponse or Phase.PointsChoice or Phase.DestroyChoice or Phase.ErrorResponse or Phase.RelikiaChoice or Phase.DeckSearchChoice ? Phase.Actions :
                s.Phase is Phase.DieRerollChoice or Phase.RecycleChoice ? s.PendingDieRoll!.ResumePhase : s.Phase == Phase.Discard ? s.DiscardResumePhase :
                s.Phase is Phase.DefenseChoice or Phase.MagnetChoice or Phase.AttackRerollChoice ? Phase.Combat : s.Phase,
            DieCaption = game.CurrentDieCaption,
            LastCombat = s.LastCombat,
            Options = seat < 0 ? [] : DuelOptions.For(game, seat),
            Players = s.Players.Select((p, index) => new DuelPlayerView
            {
                Name = p.Name, Health = p.Health, MaxHealth = p.MaxHealth, Level = p.Level, Pr = p.Relikia,
                Actions = p.ActionsLeft, DeckCount = p.Deck.Count, HandCount = p.Hand.Count,
                Hand = p.Hand.Select((id, i) => index == seat || p.IsHandCardRevealed(i) ||
                    (s.Phase == Phase.MagnetChoice && s.CurrentPlayer == seat) ||
                    (s.Phase == Phase.MindResetChoice && s.PendingMindReset?.Actor == seat && s.PendingMindReset.Target == index) ? id : null).ToArray(),
                Skills = p.Skills.ToArray(), Counters = Enumerable.Range(0, p.Skills.Count).Select(p.CountersOnSkill).ToArray(),
                Equipment = p.Equipment, EquipmentDie = p.EquipmentDie, EquipmentCounters = p.EquipmentCounters, Bonus = p.Bonus, Attached = p.AttachedCards.ToArray(), Exchange = p.Exchange.ToArray(),
                Memory = p.Memory.ToArray(), CorruptedCount = p.CorruptedMemory.Count
            }).ToArray()
        };
    }
}
