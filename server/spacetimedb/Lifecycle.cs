using SpacetimeDB;

public static partial class Module
{
    [Table(Accessor = "PlayBudget")]
    public partial struct PlayBudget
    {
        [PrimaryKey] public Identity Player;
        public long WindowStart;
        public uint Count;
    }
    [Table(Accessor = "AdmissionBudget")]
    public partial struct AdmissionBudget
    {
        [PrimaryKey] public byte Id;
        public long WindowStart;
        public uint Count;
    }
    [Table(Accessor = "RoomExpiry", Scheduled = nameof(ExpireRoom), ScheduledAt = nameof(RoomExpiry.ScheduledAt))]
    public partial struct RoomExpiry
    {
        [PrimaryKey, AutoInc] public ulong ScheduledId;
        [Unique] public string RoomCode;
        public long Deadline;
        public ScheduleAt ScheduledAt;
    }

    private static bool ChargePlay(ReducerContext ctx)
    {
        var now = ctx.Timestamp.MicrosecondsSinceUnixEpoch;
        var old = ctx.Db.PlayBudget.Player.Find(ctx.Sender);
        var budget = old ?? new PlayBudget { Player = ctx.Sender, WindowStart = now };
        if (now - budget.WindowStart >= 10_000_000) { budget.WindowStart = now; budget.Count = 0; }
        if (budget.Count >= 120) return false;
        budget.Count++;
        if (old == null) ctx.Db.PlayBudget.Insert(budget); else ctx.Db.PlayBudget.Player.Update(budget);
        return true;
    }
    private static void AdmitRoom(ReducerContext ctx)
    {
        if (ctx.Db.Room.Count >= 500) throw new Exception("Las salas de prueba están completas. Inténtalo más tarde.");
        var now = ctx.Timestamp.MicrosecondsSinceUnixEpoch;
        var old = ctx.Db.AdmissionBudget.Id.Find(0);
        var budget = old ?? new AdmissionBudget { Id = 0, WindowStart = now };
        if (now - budget.WindowStart >= 60_000_000) { budget.WindowStart = now; budget.Count = 0; }
        if (budget.Count >= 200) throw new Exception("Se están creando muchas salas. Espera un momento.");
        budget.Count++;
        if (old == null) ctx.Db.AdmissionBudget.Insert(budget); else ctx.Db.AdmissionBudget.Id.Update(budget);
    }
    private static void TouchRoom(ReducerContext ctx, string roomCode)
    {
        var current = ctx.Db.CurrentDuel.RoomCode.Find(roomCode);
#if RELICK_SHORT_LEASES
        var delay = 2_000_000L; // Dedicated local lifecycle test build, never the deployed module.
#else
        var delay = current?.Finished == true ? 86_400_000_000L : (current != null || ctx.Db.Duel.RoomCode.Find(roomCode) != null || ctx.Db.Opening.RoomCode.Find(roomCode) != null) ? 7_200_000_000L : 600_000_000L;
#endif
        var old = ctx.Db.RoomExpiry.RoomCode.Find(roomCode);
        var deadline = ctx.Timestamp.MicrosecondsSinceUnixEpoch + delay;
        var row = new RoomExpiry { ScheduledId = 0, RoomCode = roomCode, Deadline = deadline,
            ScheduledAt = new ScheduleAt.Time(ctx.Timestamp + new TimeDuration(delay)) };
        // New schedule identity prevents a queued stale callback from deleting a refreshed lease.
        if (old != null) ctx.Db.RoomExpiry.ScheduledId.Delete(old.Value.ScheduledId);
        ctx.Db.RoomExpiry.Insert(row);
    }
    [Reducer]
    public static void ExpireRoom(ReducerContext ctx, RoomExpiry expiry)
    {
        if (ctx.Sender != ctx.DatabaseIdentity) throw new Exception("Solo el planificador puede expirar salas.");
        if (ctx.Db.RoomExpiry.ScheduledId.Find(expiry.ScheduledId) is not RoomExpiry current ||
            current.Deadline != expiry.Deadline || current.Deadline > ctx.Timestamp.MicrosecondsSinceUnixEpoch) return;
        DeleteRoom(ctx, current.RoomCode);
    }
    [Reducer(ReducerKind.ClientConnected)]
    public static void ClientConnected(ReducerContext ctx)
    {
        StartLegacyExpirySweep(ctx);
        if (ctx.Db.Member.Player.Find(ctx.Sender) is not Member me) return;
        if (ctx.Db.Opening.RoomCode.Find(me.RoomCode) != null) EnsureCurrentDuel(ctx, me.RoomCode);
        TouchRoom(ctx, me.RoomCode);
    }
    [Table(Accessor = "LegacySweepControl")]
    public partial struct LegacySweepControl
    {
        [PrimaryKey] public byte Id;
        public long LastStart;
    }
    [Table(Accessor = "LegacyExpirySweep", Scheduled = nameof(SweepLegacyExpiry), ScheduledAt = nameof(LegacyExpirySweep.ScheduledAt))]
    public partial struct LegacyExpirySweep
    {
        [PrimaryKey, AutoInc] public ulong ScheduledId;
        public ScheduleAt ScheduledAt;
    }
    private static void StartLegacyExpirySweep(ReducerContext ctx)
    {
        var now = ctx.Timestamp.MicrosecondsSinceUnixEpoch;
        var old = ctx.Db.LegacySweepControl.Id.Find(0);
        if (ctx.Db.LegacyExpirySweep.Count != 0 || (old != null && now - old.Value.LastStart < 60_000_000)) return;
        var control = new LegacySweepControl { Id = 0, LastStart = now };
        if (old == null) ctx.Db.LegacySweepControl.Insert(control); else ctx.Db.LegacySweepControl.Id.Update(control);
        ctx.Db.LegacyExpirySweep.Insert(new LegacyExpirySweep { ScheduledAt = new ScheduleAt.Time(ctx.Timestamp + new TimeDuration(1_000_000)) });
    }
    [Reducer]
    public static void SweepLegacyExpiry(ReducerContext ctx, LegacyExpirySweep job)
    {
        if (ctx.Sender != ctx.DatabaseIdentity) throw new Exception("Solo el planificador puede ejecutar el mantenimiento.");
        // Background upgrade path only. New rooms always receive a lease on creation.
        // The admission cap bounds this scan; at most 16 legacy rooms are changed per tick.
        var missing = ctx.Db.Room.Iter().Where(r => ctx.Db.RoomExpiry.RoomCode.Find(r.Code) == null).Take(16).ToArray();
        foreach (var room in missing) TouchRoom(ctx, room.Code);
        if (missing.Length == 16) ctx.Db.LegacyExpirySweep.Insert(new LegacyExpirySweep { ScheduledAt = new ScheduleAt.Time(ctx.Timestamp + new TimeDuration(1_000_000)) });
    }

    private static void DeleteRoom(ReducerContext ctx, string code)
    {
        foreach (var card in ctx.Db.Card.RoomCode.Filter(code).ToList()) ctx.Db.Card.Id.Delete(card.Id);
        foreach (var member in ctx.Db.Member.RoomCode.Filter(code).ToList())
        {
            ctx.Db.InitialSkill.Player.Delete(member.Player);
            ctx.Db.Preparation.Player.Delete(member.Player); ctx.Db.DuelHand.Player.Delete(member.Player);
            ctx.Db.DuelChoices.Player.Delete(member.Player); ctx.Db.MoveResult.Player.Delete(member.Player);
            ctx.Db.PlayBudget.Player.Delete(member.Player); ctx.Db.Member.Player.Delete(member.Player);
        }
        foreach (var step in ctx.Db.DuelStep.RoomCode.Filter(code).ToList()) ctx.Db.DuelStep.Receipt.Delete(step.Receipt);
        foreach (var receipt in ctx.Db.DuelReceipt.RoomCode.Filter(code).ToList()) ctx.Db.DuelReceipt.Key.Delete(receipt.Key);
        foreach (var audit in ctx.Db.DuelAudit.RoomCode.Filter(code).ToList()) ctx.Db.DuelAudit.Key.Delete(audit.Key);
        ctx.Db.DuelBoard.RoomCode.Delete(code); ctx.Db.CurrentDuel.RoomCode.Delete(code);
        ctx.Db.Duel.RoomCode.Delete(code); ctx.Db.Opening.RoomCode.Delete(code); ctx.Db.Room.Code.Delete(code);
        if (ctx.Db.RoomExpiry.RoomCode.Find(code) is RoomExpiry expiry) ctx.Db.RoomExpiry.ScheduledId.Delete(expiry.ScheduledId);
    }
}
