using SpacetimeDB;
using Relick.Core;
using System.Text.Json;

// Test-only module. This file is not included by the deployed server project.
public static partial class Module
{
    [SpacetimeDB.Reducer]
    public static void AttemptExpiryForTest(SpacetimeDB.ReducerContext ctx) => ExpireRoom(ctx, new RoomExpiry());

    [SpacetimeDB.Type]
    public partial struct FixtureStats
    {
        public ulong Revision;
        public uint LegacySteps;
        public uint Receipts;
        public uint Audit;
        public int SnapshotBytes;
        public bool LeasePresent;
    }
    [View(Accessor = "MyFixtureStats", Public = true)]
    public static FixtureStats? MyFixtureStats(ViewContext ctx)
    {
        if (ctx.Db.Member.Player.Find(ctx.Sender) is not Member me) return null;
        var current = ctx.Db.CurrentDuel.RoomCode.Find(me.RoomCode);
        return new FixtureStats { Revision = current?.Revision ?? ulong.MaxValue,
            LegacySteps = (uint)ctx.Db.DuelStep.RoomCode.Filter(me.RoomCode).Count(), Receipts = (uint)ctx.Db.DuelReceipt.RoomCode.Filter(me.RoomCode).Count(),
            Audit = (uint)ctx.Db.DuelAudit.RoomCode.Filter(me.RoomCode).Count(), SnapshotBytes = current?.SnapshotJson.Length ?? 0, LeasePresent = ctx.Db.RoomExpiry.RoomCode.Find(me.RoomCode) != null };
    }
    [Reducer]
    public static void SeedLegacyFixture(ReducerContext ctx, string nonce, bool unsupported)
    {
        var me = ctx.Db.Member.Player.Find(ctx.Sender)!.Value;
        if (ctx.Db.CurrentDuel.RoomCode.Find(me.RoomCode) is not CurrentDuel current || current.Revision != 0) throw new Exception("Se requiere una sala nueva preparada.");
        var opening = InitialDuel(ctx.Db.Opening.RoomCode.Find(me.RoomCode)!.Value, ctx.Db.Member.RoomCode.Filter(me.RoomCode), ctx.Db.Card.RoomCode.Filter(me.RoomCode));
        var random = new DuelRandom(); var game = GameEngine.FromOpening(opening, random); var actor = opening.StartingSeat;
        var option = DuelOptions.For(game, actor).FindIndex(o => o.Move.Kind == DuelMoveKind.Learn);
        var move = DuelOptions.For(game, actor)[option].Move; random.Record(max => ctx.Rng.Next(max));
        if (!move.Apply(game, actor).Success) throw new Exception("No se pudo preparar el registro legado.");
        ctx.Db.Duel.Insert(new Duel { RoomCode = me.RoomCode, Revision = 1, RulesVersion = unsupported ? 999U : 1U,
            InitialJson = JsonSerializer.Serialize(opening, DuelJson.Default.DuelOpening) });
        ctx.Db.DuelStep.Insert(new DuelStep { Receipt = me.RoomCode + ":" + actor + ":" + nonce, RoomCode = me.RoomCode,
            Revision = 1, Option = option, SelectedCards = [], EntryJson = JsonSerializer.Serialize(new DuelEntry { Actor = actor, Move = move, RandomValues = random.Finish() }, DuelJson.Default.DuelEntry) });
        ctx.Db.CurrentDuel.RoomCode.Delete(me.RoomCode); ctx.Db.DuelBoard.RoomCode.Delete(me.RoomCode);
        if (ctx.Db.RoomExpiry.RoomCode.Find(me.RoomCode) is RoomExpiry expiry) ctx.Db.RoomExpiry.ScheduledId.Delete(expiry.ScheduledId);
        ctx.Db.LegacySweepControl.Id.Delete(0); StartLegacyExpirySweep(ctx);
        foreach (var member in ctx.Db.Member.RoomCode.Filter(me.RoomCode)) { ctx.Db.DuelHand.Player.Delete(member.Player); ctx.Db.DuelChoices.Player.Delete(member.Player); }
    }
    [Reducer]
    public static void CompactFixture(ReducerContext ctx)
    {
        var me = ctx.Db.Member.Player.Find(ctx.Sender)!.Value; var current = EnsureCurrentDuel(ctx, me.RoomCode);
        for (var i = 0; i < 200; i++)
        {
            current.Revision++;
            ctx.Db.DuelReceipt.Insert(new DuelReceipt { Key = me.RoomCode + ":test:" + i, RoomCode = me.RoomCode, Revision = current.Revision, Option = -1, SelectedCards = [] });
            AppendAudit(ctx, me.RoomCode, current.Revision, "{}");
        }
        ctx.Db.CurrentDuel.RoomCode.Update(current);
        CacheDuel(ctx, me.RoomCode, GameEngine.FromSnapshot(current.SnapshotJson, new DuelRandom()), current.Revision);
    }
    [Reducer]
    public static void ClearFixture(ReducerContext ctx)
    {
        if (ctx.Db.Member.Player.Find(ctx.Sender) is Member me) DeleteRoom(ctx, me.RoomCode);
    }
}
