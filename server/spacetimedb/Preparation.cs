using SpacetimeDB;
using Relick.Core;

public static partial class Module
{
    [Table(Accessor = "Preparation")]
    public partial struct Preparation
    {
        [PrimaryKey] public Identity Player;
        public string CommandId;
        public List<ulong> ReturnedCards;
    }

    // Persist the opening in a private table, never in static module memory.
    [Table(Accessor = "Opening")]
    public partial struct Opening
    {
        [PrimaryKey] public string RoomCode;
        public byte StartingSeat;
        public byte RollOne;
        public byte RollTwo;
        public ulong BonusOne;
        public ulong BonusTwo;
    }

    [SpacetimeDB.Type]
    public partial struct PreparationInfo
    {
        public bool MyConfirmed;
        public List<ulong> MyReturnedCards;
        public bool OpponentConfirmed;
        public bool Started;
        public byte StartingSeat;
        public byte RollOne;
        public byte RollTwo;
        public string BonusOne;
        public string BonusTwo;
        public uint HandCountOne;
        public uint HandCountTwo;
        public uint DeckCountOne;
        public uint DeckCountTwo;
    }

    [View(Accessor = "MyPreparation", Public = true)]
    public static PreparationInfo? MyPreparation(ViewContext ctx)
    {
        if (ctx.Db.Member.Player.Find(ctx.Sender) is not Member me) return null;
        var members = ctx.Db.Member.RoomCode.Filter(me.RoomCode).ToList();
        var opponent = members.Where(m => m.Player != ctx.Sender).ToList();
        var opening = ctx.Db.Opening.RoomCode.Find(me.RoomCode);
        var ownPreparation = ctx.Db.Preparation.Player.Find(ctx.Sender);
        var result = new PreparationInfo
        {
            MyConfirmed = ownPreparation is not null,
            MyReturnedCards = ownPreparation?.ReturnedCards ?? new List<ulong>(),
            OpponentConfirmed = opponent.Count == 1 && ctx.Db.Preparation.Player.Find(opponent[0].Player) is not null,
            Started = opening is not null,
            BonusOne = "", BonusTwo = ""
        };
        if (opening is Opening start)
        {
            result.StartingSeat = start.StartingSeat;
            result.RollOne = start.RollOne;
            result.RollTwo = start.RollTwo;
            result.BonusOne = ctx.Db.Card.Id.Find(start.BonusOne)!.Value.Definition;
            result.BonusTwo = ctx.Db.Card.Id.Find(start.BonusTwo)!.Value.Definition;
        }
        foreach (var member in members)
        {
            var cards = ctx.Db.Card.Owner.Filter(member.Player).ToList();
            var hand = (uint)cards.Count(c => c.InHand);
            var deck = (uint)cards.Count(c => !c.InHand);
            if (opening is not null) deck--; // Bonus occupies its own zone, outside the deck.
            if (member.Seat == 0) { result.HandCountOne = hand; result.DeckCountOne = deck; }
            else { result.HandCountTwo = hand; result.DeckCountTwo = deck; }
        }
        return result;
    }

    [SpacetimeDB.Type]
    public partial struct RoomRelikarioInfo
    {
        public string RoomCode;
        public string RelikarioOne;
        public string RelikarioTwo;
    }

    [View(Accessor = "MyRoomRelikarios", Public = true)]
    public static RoomRelikarioInfo? MyRoomRelikarios(ViewContext ctx)
    {
        if (ctx.Db.Member.Player.Find(ctx.Sender) is not Member me) return null;
        var result = new RoomRelikarioInfo
        {
            RoomCode = me.RoomCode, RelikarioOne = RelikarioCatalog.Yuno.Id, RelikarioTwo = RelikarioCatalog.Yuno.Id
        };
        foreach (var member in ctx.Db.Member.RoomCode.Filter(me.RoomCode))
        {
            var id = ctx.Db.LoadoutRelikario.Player.Find(member.Player)?.Relikario ?? RelikarioCatalog.Yuno.Id;
            if (member.Seat == 0) result.RelikarioOne = id;
            else result.RelikarioTwo = id;
        }
        return result;
    }

    [Reducer]
    public static void ConfirmPreparation(ReducerContext ctx, string roomCode, List<ulong> returnedCards, string commandId)
    {
        if (ctx.Db.Member.Player.Find(ctx.Sender) is not Member me)
            throw new Exception("No estás en una sala.");
        if (me.RoomCode != roomCode) throw new Exception("Esa confirmación pertenece a otra sala.");
        if (!Guid.TryParseExact(commandId, "N", out _)) throw new Exception("Comando de preparación inválido.");
        if (returnedCards.Count > 5 || returnedCards.Distinct().Count() != returnedCards.Count)
            throw new Exception("Selecciona hasta cinco cartas distintas de tu mano.");
        var requested = returnedCards.Order().ToList();
        if (ctx.Db.Preparation.Player.Find(ctx.Sender) is Preparation previous)
        {
            if (previous.CommandId == commandId && previous.ReturnedCards.SequenceEqual(requested)) return;
            throw new Exception("Ya confirmaste tu mano inicial.");
        }
        if (ctx.Db.Room.Code.Find(me.RoomCode) is not Room room || !room.Dealt)
            throw new Exception("Espera a que entre tu rival antes de confirmar.");
        if (ctx.Db.Opening.RoomCode.Find(me.RoomCode) is not null)
            throw new Exception("La partida ya empezó.");
        var hand = ctx.Db.Card.Owner.Filter(ctx.Sender).Where(c => c.InHand).ToList();
        if (hand.Count != 5 || requested.Any(id => hand.All(c => c.Id != id)))
            throw new Exception("Solo puedes cambiar cartas de tu mano inicial actual.");

        // Save both choices before applying either one. A lost acknowledgement is safe to retry.
        ctx.Db.Preparation.Insert(new Preparation { Player = ctx.Sender, CommandId = commandId, ReturnedCards = requested });
        var members = ctx.Db.Member.RoomCode.Filter(me.RoomCode).OrderBy(m => m.Seat).ToList();
        if (members.Count != 2 || members.Any(m => ctx.Db.Preparation.Player.Find(m.Player) is null)) return;

        foreach (var member in members) ApplyMulligan(ctx, member);
        var bonusOne = TakeBonus(ctx, members[0]);
        var bonusTwo = TakeBonus(ctx, members[1]);
        byte first, second;
        do { first = (byte)ctx.Rng.Next(1, 7); second = (byte)ctx.Rng.Next(1, 7); } while (first == second);
        var startingSeat = (byte)(first > second ? 0 : 1);
        // BeginTurn in the practice engine also draws one card for the starting player.
        var starter = members[startingSeat];
        var bonusId = startingSeat == 0 ? bonusOne : bonusTwo;
        var draw = ctx.Db.Card.Owner.Filter(starter.Player).Where(c => !c.InHand && c.Id != bonusId).OrderBy(c => c.Position).First();
        draw.InHand = true;
        draw.Position = (uint)ctx.Db.Card.Owner.Filter(starter.Player).Count(c => c.InHand);
        ctx.Db.Card.Id.Update(draw);
        ctx.Db.Opening.Insert(new Opening
        {
            RoomCode = me.RoomCode, StartingSeat = startingSeat, RollOne = first, RollTwo = second,
            BonusOne = bonusOne, BonusTwo = bonusTwo
        });
        EnsureCurrentDuel(ctx, me.RoomCode);
        TouchRoom(ctx, me.RoomCode);
    }

    private static void ApplyMulligan(ReducerContext ctx, Member member)
    {
        var preparation = ctx.Db.Preparation.Player.Find(member.Player)!.Value;
        var cards = ctx.Db.Card.Owner.Filter(member.Player).ToList();
        var hand = cards.Where(c => c.InHand).OrderBy(c => c.Position).ToList();
        var deck = cards.Where(c => !c.InHand).OrderBy(c => c.Position).ToList();
        // Match the local rules: return selected cards, right to left, to the bottom.
        var returned = hand.Where(c => preparation.ReturnedCards.Contains(c.Id)).Reverse().ToList();
        hand.RemoveAll(c => preparation.ReturnedCards.Contains(c.Id));
        deck.AddRange(returned);
        hand.AddRange(deck.Take(returned.Count));
        deck.RemoveRange(0, returned.Count);
        for (var i = 0; i < hand.Count; i++)
        {
            var card = hand[i]; card.InHand = true; card.Position = (uint)i;
            ctx.Db.Card.Id.Update(card);
        }
        for (var i = 0; i < deck.Count; i++)
        {
            var card = deck[i]; card.InHand = false; card.Position = (uint)i;
            ctx.Db.Card.Id.Update(card);
        }
    }

    private static ulong TakeBonus(ReducerContext ctx, Member member)
        => ctx.Db.Card.Owner.Filter(member.Player).Where(c => !c.InHand).OrderBy(c => c.Position).First().Id;
}
