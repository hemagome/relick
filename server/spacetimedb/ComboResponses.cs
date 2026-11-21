using SpacetimeDB;
using Relick.Core;
using System.Text.Json;

public static partial class Module
{
    [Reducer]
    public static void SetComboResponses(ReducerContext ctx, string roomCode, string commandId, bool enabled)
    {
        if (ctx.Db.Member.Player.Find(ctx.Sender) is not Member me || me.RoomCode != roomCode)
            throw new Exception("No perteneces a esta sala.");
        if (roomCode.Length != 8 || commandId.Length != 32 || !Guid.TryParseExact(commandId, "N", out _))
            throw new Exception("Solicitud de respuestas COMBO inválida.");
        var key = roomCode + ":" + me.Seat + ":combo:" + commandId;
        var setting = enabled ? -2 : -3;
        if (ctx.Db.DuelReceipt.Key.Find(key) is DuelReceipt prior)
        {
            var same = prior.Option == setting;
            WriteMoveResult(ctx, commandId, same, same ? "" : "El identificador ya corresponde a otra preferencia.", prior.Revision);
            return;
        }
        if (!ChargePlay(ctx)) { WriteMoveResult(ctx, commandId, false, "Demasiadas solicitudes. Espera unos segundos."); return; }
        if (ctx.Db.Opening.RoomCode.Find(roomCode) == null)
        { WriteMoveResult(ctx, commandId, false, "La partida todavía no ha comenzado."); return; }
        var current = EnsureCurrentDuel(ctx, roomCode);
        var random = new DuelRandom(); random.Record(max => ctx.Rng.Next(max));
        var game = GameEngine.FromSnapshot(current.SnapshotJson, random);
        var visuals = DuelVisualEvents.Capture(game);
        var result = game.SetComboResponses(me.Seat, enabled, out var resumed);
        if (!result.Success) { WriteMoveResult(ctx, commandId, false, result.Message); return; }
        if (resumed) current.Revision++;
        var wasFinished = current.Finished;
        current.Finished = game.State.IsOver; current.SnapshotJson = game.SaveSnapshot(false);
        if (!wasFinished && current.Finished) RecordOutcome(ctx, roomCode, game.State.Winner);
        ctx.Db.CurrentDuel.RoomCode.Update(current);
        if (resumed)
        {
            CacheDuel(ctx, roomCode, game, current.Revision, visuals);
            AppendAudit(ctx, roomCode, current.Revision, JsonSerializer.Serialize(new DuelEntry
            { Actor = me.Seat, Move = new() { Kind = DuelMoveKind.ComboResponses, Flag = enabled }, RandomValues = random.Finish() }, DuelJson.Default.DuelEntry));
        }
        else
        {
            // A private preference must not change the public revision, packet versions,
            // opponent options or previous visual events. Retain both private row versions.
            var view = DuelSnapshot.For(game, me.Seat, current.Revision);
            var hand = ctx.Db.DuelHand.Player.Find(me.Player)!.Value;
            var data = JsonSerializer.Deserialize(hand.Json, DuelJson.Default.DuelPrivateData)!;
            data.AutoPassCombos = view.AutoPassCombos;
            hand.Json = JsonSerializer.Serialize(data, DuelJson.Default.DuelPrivateData);
            ctx.Db.DuelHand.Player.Update(hand);
            var choices = ctx.Db.DuelChoices.Player.Find(me.Player)!.Value;
            choices.Json = JsonSerializer.Serialize(view.Options, DuelJson.Default.ListDuelOption);
            ctx.Db.DuelChoices.Player.Update(choices);
            random.Finish();
        }
        ctx.Db.DuelReceipt.Insert(new DuelReceipt { Key = key, RoomCode = roomCode, Revision = current.Revision, Option = setting, SelectedCards = [] });
        WriteMoveResult(ctx, commandId, true, "", current.Revision); TouchRoom(ctx, roomCode);
    }
}
