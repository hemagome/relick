using SpacetimeDB;
using Relick.Core;
using System.Text.Json;

public static partial class Module
{
    [Table(Accessor = "CurrentDuel")]
    public partial struct CurrentDuel
    {
        [PrimaryKey] public string RoomCode;
        public ulong Revision;
        public uint RulesVersion;
        public string SnapshotJson;
        public bool Finished;
    }
    [Table(Accessor = "DuelBoard")]
    public partial struct DuelBoard
    {
        [PrimaryKey] public string RoomCode;
        public ulong Revision;
        public string Json;
    }
    [Table(Accessor = "DuelHand")]
    public partial struct DuelHand
    {
        [PrimaryKey] public Identity Player;
        public string RoomCode;
        public ulong Revision;
        public string Json;
    }
    [Table(Accessor = "DuelChoices")]
    public partial struct DuelChoices
    {
        [PrimaryKey] public Identity Player;
        public string RoomCode;
        public ulong Revision;
        public string Json;
    }
    [Table(Accessor = "DuelReceipt")]
    public partial struct DuelReceipt
    {
        [PrimaryKey] public string Key;
        [SpacetimeDB.Index.BTree] public string RoomCode;
        public ulong Revision;
        public int Option;
        public List<int> SelectedCards;
    }
    [Table(Accessor = "DuelAudit")]
    public partial struct DuelAudit
    {
        [PrimaryKey] public string Key;
        [SpacetimeDB.Index.BTree] public string RoomCode;
        public ulong Revision;
        public string EntryJson;
    }
    [Table(Accessor = "MoveResult")]
    public partial struct MoveResult
    {
        [PrimaryKey] public Identity Player;
        public string CommandId;
        public bool Accepted;
        public string Error;
        public ulong Revision;
    }
    [View(Accessor = "MyBoard", Public = true, PrimaryKey = "RoomCode")]
    public static DuelBoard? MyBoard(ViewContext ctx) => ctx.Db.Member.Player.Find(ctx.Sender) is Member me ? ctx.Db.DuelBoard.RoomCode.Find(me.RoomCode) : null;
    [View(Accessor = "MyDuelHand", Public = true, PrimaryKey = "Player")]
    public static DuelHand? MyDuelHand(ViewContext ctx) => ctx.Db.DuelHand.Player.Find(ctx.Sender);
    [View(Accessor = "MyChoices", Public = true, PrimaryKey = "Player")]
    public static DuelChoices? MyChoices(ViewContext ctx) => ctx.Db.DuelChoices.Player.Find(ctx.Sender);
    [View(Accessor = "MyMoveResult", Public = true, PrimaryKey = "Player")]
    public static MoveResult? MyMoveResult(ViewContext ctx) => ctx.Db.MoveResult.Player.Find(ctx.Sender);

    private static void WriteMoveResult(ReducerContext ctx, string commandId, bool accepted, string error, ulong revision = 0)
    {
        var row = new MoveResult { Player = ctx.Sender, CommandId = commandId, Accepted = accepted, Error = error, Revision = revision };
        if (ctx.Db.MoveResult.Player.Find(ctx.Sender) is MoveResult old)
        {
            if (old.CommandId != commandId || old.Accepted != accepted || old.Error != error || old.Revision != revision) ctx.Db.MoveResult.Player.Update(row);
        }
        else ctx.Db.MoveResult.Insert(row);
    }

    private static void CacheDuel(ReducerContext ctx, string roomCode, GameEngine game, ulong revision, List<DuelVisualEvent>? visuals = null)
    {
        var players = ctx.Db.Member.RoomCode.Filter(roomCode).OrderBy(m => m.Seat).ToArray();
        var packet = new DuelBoardPacket { Board = DuelSnapshot.For(game, -1, revision) };
        packet.Board.Visuals = visuals ?? [];
        // Counts travel once on the shared board. Only actual public revelations need slots.
        foreach (var player in packet.Board.Players) if (player.Hand.All(c => c == null)) player.Hand = [];
        foreach (var member in players)
        {
            var view = DuelSnapshot.For(game, member.Seat, revision);
            var privateJson = JsonSerializer.Serialize(new DuelPrivateData { MySeat = member.Seat,
                OwnHand = view.Players[member.Seat].Hand, OpponentHand = game.State.Phase == Phase.MagnetChoice && game.State.CurrentPlayer == member.Seat ? view.Players[1 - member.Seat].Hand : [] }, DuelJson.Default.DuelPrivateData);
            var hand = ctx.Db.DuelHand.Player.Find(member.Player);
            var handVersion = hand?.Revision ?? revision;
            if (hand == null || hand.Value.Json != privateJson)
            {
                var row = new DuelHand { Player = member.Player, RoomCode = roomCode, Revision = revision, Json = privateJson };
                if (hand == null) ctx.Db.DuelHand.Insert(row); else ctx.Db.DuelHand.Player.Update(row);
                handVersion = revision;
            }
            packet.HandVersions[member.Seat] = handVersion;
            var optionsJson = JsonSerializer.Serialize(view.Options, DuelJson.Default.ListDuelOption);
            var choices = ctx.Db.DuelChoices.Player.Find(member.Player);
            var choicesVersion = choices?.Revision ?? revision;
            if (choices == null || choices.Value.Json != optionsJson)
            {
                var row = new DuelChoices { Player = member.Player, RoomCode = roomCode, Revision = revision, Json = optionsJson };
                if (choices == null) ctx.Db.DuelChoices.Insert(row); else ctx.Db.DuelChoices.Player.Update(row);
                choicesVersion = revision;
            }
            packet.OptionVersions[member.Seat] = choicesVersion;
        }
        var board = new DuelBoard { RoomCode = roomCode, Revision = revision, Json = JsonSerializer.Serialize(packet, DuelJson.Default.DuelBoardPacket) };
        if (ctx.Db.DuelBoard.RoomCode.Find(roomCode) == null) ctx.Db.DuelBoard.Insert(board); else ctx.Db.DuelBoard.RoomCode.Update(board);
    }

    private static CurrentDuel EnsureCurrentDuel(ReducerContext ctx, string roomCode)
    {
        if (ctx.Db.CurrentDuel.RoomCode.Find(roomCode) is CurrentDuel current)
        {
            if (current.RulesVersion != 1) throw new Exception("Esta partida requiere otra versión de reglas.");
            return current;
        }
        if (ctx.Db.Opening.RoomCode.Find(roomCode) is not Opening opening) throw new Exception("Ambos jugadores deben confirmar primero.");
        GameEngine game; ulong revision = 0;
        if (ctx.Db.Duel.RoomCode.Find(roomCode) is Duel legacy)
        {
            // The sole history replay path: migrate a version-1 room once, atomically.
            var steps = ctx.Db.DuelStep.RoomCode.Filter(roomCode).OrderBy(s => s.Revision).ToArray();
            game = RestoreDuel(legacy, steps).Game; revision = legacy.Revision;
            foreach (var step in steps)
            {
                ctx.Db.DuelReceipt.Insert(new DuelReceipt { Key = step.Receipt, RoomCode = roomCode, Revision = step.Revision, Option = step.Option, SelectedCards = step.SelectedCards });
                if (step.Revision + 128 > revision) ctx.Db.DuelAudit.Insert(new DuelAudit { Key = roomCode + ":" + step.Revision, RoomCode = roomCode, Revision = step.Revision, EntryJson = step.EntryJson });
                ctx.Db.DuelStep.Receipt.Delete(step.Receipt);
            }
            ctx.Db.Duel.RoomCode.Delete(roomCode);
        }
        else game = GameEngine.FromOpening(InitialDuel(opening, ctx.Db.Member.RoomCode.Filter(roomCode), ctx.Db.Card.RoomCode.Filter(roomCode)), new DuelRandom());
        current = new CurrentDuel { RoomCode = roomCode, Revision = revision, RulesVersion = 1, SnapshotJson = game.SaveSnapshot(false), Finished = game.State.IsOver };
        ctx.Db.CurrentDuel.Insert(current); CacheDuel(ctx, roomCode, game, revision);
        return current;
    }

    private static void AppendAudit(ReducerContext ctx, string roomCode, ulong revision, string entryJson)
    {
        ctx.Db.DuelAudit.Insert(new DuelAudit { Key = roomCode + ":" + revision, RoomCode = roomCode, Revision = revision, EntryJson = entryJson });
        if (revision > 128) ctx.Db.DuelAudit.Key.Delete(roomCode + ":" + (revision - 128));
    }

    [Reducer]
    public static void EnsureDuel(ReducerContext ctx)
    {
        if (ctx.Db.Member.Player.Find(ctx.Sender) is not Member me) return;
        if (ctx.Db.Opening.RoomCode.Find(me.RoomCode) is not null) EnsureCurrentDuel(ctx, me.RoomCode);
        TouchRoom(ctx, me.RoomCode);
    }

    // Protocol 1 assumed that every committed transaction was an accepted move.
    // Protocol 2 observes the private result, including committed rate-limit rejections.
    [Reducer]
    public static void PlayDuel(ReducerContext ctx, string roomCode, ulong expectedRevision, string commandId, int option, List<int> selectedCards)
        => throw new Exception("Actualiza y vuelve a abrir el juego para continuar esta partida.");

    [Reducer]
    public static void PlayDuelV2(ReducerContext ctx, string roomCode, ulong expectedRevision, string commandId, int option, List<int> selectedCards)
    {
        if (ctx.Db.Member.Player.Find(ctx.Sender) is not Member me) throw new Exception("No perteneces a esta sala.");
        if (commandId.Length != 32 || !Guid.TryParseExact(commandId, "N", out _) || roomCode.Length != 8 || selectedCards.Count > 6 || selectedCards.Any(i => i < 0) || selectedCards.Distinct().Count() != selectedCards.Count)
            throw new Exception("Selección de jugada inválida.");
        var selected = selectedCards.Order().ToList(); var key = roomCode + ":" + me.Seat + ":" + commandId;
        if (me.RoomCode == roomCode && ctx.Db.DuelReceipt.Key.Find(key) is DuelReceipt prior)
        {
            var identical = expectedRevision < ulong.MaxValue && prior.Revision == expectedRevision + 1 && prior.Option == option && prior.SelectedCards.SequenceEqual(selected);
            WriteMoveResult(ctx, commandId, identical, identical ? "" : "El identificador ya corresponde a otra jugada.", prior.Revision); return;
        }
        // Rejections commit the budget and a private result; throwing would roll the budget back.
        if (!ChargePlay(ctx)) { WriteMoveResult(ctx, commandId, false, "Demasiadas solicitudes. Espera unos segundos."); return; }
        if (me.RoomCode != roomCode) { WriteMoveResult(ctx, commandId, false, "No perteneces a esta sala."); return; }
        if (ctx.Db.Opening.RoomCode.Find(roomCode) == null) { WriteMoveResult(ctx, commandId, false, "Ambos jugadores deben confirmar primero."); return; }
        var current = EnsureCurrentDuel(ctx, roomCode);
        if (current.Revision != expectedRevision) { WriteMoveResult(ctx, commandId, false, "La partida cambió. Revisa el tablero antes de volver a jugar."); return; }
        if (current.Finished) { WriteMoveResult(ctx, commandId, false, "La partida terminó."); return; }
        var random = new DuelRandom(); var game = GameEngine.FromSnapshot(current.SnapshotJson, random);
        var options = DuelOptions.For(game, me.Seat);
        if (option < 0 || option >= options.Count) { WriteMoveResult(ctx, commandId, false, "Esa jugada no está disponible."); return; }
        var choice = options[option];
        var selectionCount = choice.Move.Kind == DuelMoveKind.Recycle ? game.State.Players[me.Seat].Memory.Count : game.State.Players[me.Seat].Hand.Count;
        if (selected.Count != choice.RequiredDiscards || selected.Any(i => i >= selectionCount)) { WriteMoveResult(ctx, commandId, false, $"Selecciona exactamente {choice.RequiredDiscards} carta(s) válidas."); return; }
        choice.Move.Discards = selected.ToArray(); random.Record(max => ctx.Rng.Next(max));
        var visuals = DuelVisualEvents.Capture(game);
        var result = choice.Move.Apply(game, me.Seat);
        if (!result.Success) { WriteMoveResult(ctx, commandId, false, result.Message); return; }
        current.Revision++; current.Finished = game.State.IsOver; current.SnapshotJson = game.SaveSnapshot(false);
        ctx.Db.CurrentDuel.RoomCode.Update(current); CacheDuel(ctx, roomCode, game, current.Revision, visuals);
        ctx.Db.DuelReceipt.Insert(new DuelReceipt { Key = key, RoomCode = roomCode, Revision = current.Revision, Option = option, SelectedCards = selected });
        AppendAudit(ctx, roomCode, current.Revision, JsonSerializer.Serialize(new DuelEntry { Actor = me.Seat, Move = choice.Move, RandomValues = random.Finish() }, DuelJson.Default.DuelEntry));
        WriteMoveResult(ctx, commandId, true, "", current.Revision); TouchRoom(ctx, roomCode);
    }
}
