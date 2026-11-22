using SpacetimeDB;
using Relick.Core;

public static partial class Module
{
    // No base table is public. Views derive access from the authenticated sender.
    [Table(Accessor = "Room")]
    public partial struct Room
    {
        [PrimaryKey] public string Code;
        public bool Dealt;
    }

    [Table(Accessor = "Member")]
    public partial struct Member
    {
        [PrimaryKey] public Identity Player;
        [SpacetimeDB.Index.BTree] public string RoomCode;
        public byte Seat;
        public string Name;
    }

    [Table(Accessor = "Card")]
    public partial struct Card
    {
        [PrimaryKey, AutoInc] public ulong Id;
        [SpacetimeDB.Index.BTree] public Identity Owner;
        [SpacetimeDB.Index.BTree] public string RoomCode;
        public string Definition;
        public uint Position;
        public bool InHand;
    }

    [SpacetimeDB.Type]
    public partial struct RoomInfo
    {
        public string Code;
        public byte MySeat;
        public string PlayerOne;
        public string PlayerTwo;
        public bool Dealt;
    }

    [SpacetimeDB.Type]
    public partial struct HandCard
    {
        public ulong Id;
        public string Definition;
        public uint Position;
    }

    [View(Accessor = "MyRoom", Public = true)]
    public static RoomInfo? MyRoom(ViewContext ctx)
    {
        if (ctx.Db.Member.Player.Find(ctx.Sender) is not Member me) return null;
        if (ctx.Db.Room.Code.Find(me.RoomCode) is not Room room) return null;
        var members = ctx.Db.Member.RoomCode.Filter(me.RoomCode).ToList();
        return new RoomInfo
        {
            Code = room.Code, MySeat = me.Seat, Dealt = room.Dealt,
            PlayerOne = members.FirstOrDefault(m => m.Seat == 0).Name ?? "",
            PlayerTwo = members.FirstOrDefault(m => m.Seat == 1).Name ?? ""
        };
    }

    [View(Accessor = "MyHand", Public = true, PrimaryKey = "Id")]
    public static IEnumerable<HandCard> MyHand(ViewContext ctx)
    {
        if (ctx.Db.Member.Player.Find(ctx.Sender) is Member me && ctx.Db.CurrentDuel.RoomCode.Find(me.RoomCode) is CurrentDuel live && live.Revision > 0)
            return new List<HandCard>(); // The live hand is now available only in my_duel.
        return ctx.Db.Card.Owner.Filter(ctx.Sender).Where(c => c.InHand)
            .Select(c => new HandCard { Id = c.Id, Definition = c.Definition, Position = c.Position }).ToList();
    }

    [Reducer]
    public static void CreateRoom(ReducerContext ctx, string name)
    {
        name = PlayerName(ctx, name);
        // Retrying after a lost acknowledgement does not create extra rooms or cards.
        if (ctx.Db.Member.Player.Find(ctx.Sender) is not null) return;
        RequireNoQuickSearch(ctx);
        RequireNoRankedSearch(ctx);
        RequireNotCheckedIn(ctx);
        CreateRoomFor(ctx, ctx.Sender, name);
    }

    private static Room CreateRoomFor(ReducerContext ctx, Identity player, string name)
    {
        AdmitRoom(ctx);
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        string code;
        do { code = new string(Enumerable.Range(0, 8).Select(_ => alphabet[ctx.Rng.Next(alphabet.Length)]).ToArray()); }
        while (ctx.Db.Room.Code.Find(code) is not null);
        var room = ctx.Db.Room.Insert(new Room { Code = code });
        ctx.Db.Member.Insert(new Member { Player = player, RoomCode = code, Seat = 0, Name = name });
        TouchRoom(ctx, code);
        return room;
    }

    [Reducer]
    public static void JoinRoom(ReducerContext ctx, string code, string name)
    {
        name = PlayerName(ctx, name);
        if (code.Length > 32) throw new Exception("Código de sala inválido.");
        code = code.Trim().ToUpperInvariant();
        if (ctx.Db.Member.Player.Find(ctx.Sender) is Member current)
        {
            if (current.RoomCode == code) return;
            throw new Exception("Ya estás en una sala. Sal de ella antes de entrar a otra.");
        }
        RequireNoQuickSearch(ctx);
        RequireNoRankedSearch(ctx);
        RequireNotCheckedIn(ctx);
        if (ctx.Db.Room.Code.Find(code) is not Room room) throw new Exception("No se encontró la sala.");
        var members = ctx.Db.Member.RoomCode.Filter(code).ToList();
        if (room.Dealt || members.Count != 1) throw new Exception("La sala ya está completa.");
        CompleteRoom(ctx, room, members[0], ctx.Sender, name);
    }

    private static void CompleteRoom(ReducerContext ctx, Room room, Member first, Identity player, string name)
    {
        var member = new Member { Player = player, RoomCode = room.Code, Seat = 1, Name = name };
        ctx.Db.Member.Insert(member);
        Deal(ctx, first);
        Deal(ctx, member);
        room.Dealt = true;
        ctx.Db.Room.Code.Update(room);
        TouchRoom(ctx, room.Code);
    }

    [Reducer]
    public static void LeaveRoom(ReducerContext ctx)
    {
        if (ctx.Db.Member.Player.Find(ctx.Sender) is not Member me) return;
        if (ctx.Db.CurrentDuel.RoomCode.Find(me.RoomCode) is CurrentDuel live && live.Revision > 0 && !live.Finished)
            throw new Exception("Abandona el duelo antes de cerrar la sala.");
        ForfeitOnLeave(ctx, me);
        DeleteRoom(ctx, me.RoomCode);
    }

    private static string ValidateName(string name)
    {
        if (name.Length > 128) throw new Exception("Usa un nombre de 1 a 24 caracteres.");
        name = name.Trim();
        if (name.Length is < 1 or > 24 || name.Any(char.IsControl))
            throw new Exception("Usa un nombre de 1 a 24 caracteres.");
        return name;
    }

    private static void Deal(ReducerContext ctx, Member member)
    {
        // The deck is copied into the room, so later edits never change a match already dealt.
        var (deck, traps, sleeve, dice, relikario) = DeckFor(ctx, member.Player);
        ctx.Db.Loadout.Player.Delete(member.Player);
        ctx.Db.Loadout.Insert(new Loadout { Player = member.Player, RoomCode = member.RoomCode, Traps = traps });
        ctx.Db.LoadoutStyle.Player.Delete(member.Player);
        ctx.Db.LoadoutStyle.Insert(new LoadoutStyle { Player = member.Player, RoomCode = member.RoomCode, Sleeve = sleeve, Dice = dice });
        ctx.Db.LoadoutRelikario.Player.Delete(member.Player);
        ctx.Db.LoadoutRelikario.Insert(new LoadoutRelikario { Player = member.Player, Relikario = relikario });
        for (var i = 0; i < deck.Count; i++)
            ctx.Db.Card.Insert(new Card
            {
                Owner = member.Player, RoomCode = member.RoomCode,
                Definition = deck[i], Position = (uint)i, InHand = false
            });
    }
}
