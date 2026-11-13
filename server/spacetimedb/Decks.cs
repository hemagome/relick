using SpacetimeDB;
using Relick.Core;

public static partial class Module
{
    private const int MaxDecksPerPlayer = 20;

    [Table(Accessor = "Deck")]
    public partial struct Deck
    {
        [PrimaryKey, AutoInc] public ulong Id;
        [SpacetimeDB.Index.BTree] public Identity Owner;
        public string Name;
        public string Relikario;
        public List<string> Cards;
        public List<string> Traps;
        public string CommandId;
        public long UpdatedAt;
    }

    // The Trap deck a player brought into a room, kept apart from the main cards dealt as Card rows.
    [Table(Accessor = "Loadout")]
    public partial struct Loadout
    {
        [PrimaryKey] public Identity Player;
        public string RoomCode;
        public List<string> Traps;
    }

    [SpacetimeDB.Type]
    public partial struct DeckInfo
    {
        public ulong Id;
        public string Name;
        public string Relikario;
        public List<string> Cards;
        public List<string> Traps;
        public bool Valid;
        public bool Active;
    }

    [View(Accessor = "MyDecks", Public = true, PrimaryKey = "Id")]
    public static IEnumerable<DeckInfo> MyDecks(ViewContext ctx)
    {
        var active = ctx.Db.Account.Player.Find(ctx.Sender)?.ActiveDeck ?? 0;
        return ctx.Db.Deck.Owner.Filter(ctx.Sender).Select(deck => new DeckInfo
        {
            Id = deck.Id, Name = deck.Name, Relikario = deck.Relikario, Cards = deck.Cards, Traps = deck.Traps,
            Valid = DeckRules.Validate(deck.Relikario, deck.Cards, deck.Traps).Count == 0, Active = deck.Id == active
        }).ToList();
    }

    /// <summary>Creates a deck when <paramref name="id"/> is zero, otherwise replaces the caller's deck.
    /// An illegal deck is kept as a draft; it cannot be made active or dealt.</summary>
    [Reducer]
    public static void SaveDeck(ReducerContext ctx, ulong id, string name, string relikario, List<string> cards, List<string> traps, string commandId)
    {
        if (RequireAccount(ctx).Guest) throw new Exception(GuestsKeepNoDecks);
        if (!Guid.TryParseExact(commandId, "N", out _)) throw new Exception("Solicitud de mazo inválida.");
        if (name.Length > 128) throw new Exception("Usa un nombre de mazo de 1 a 32 caracteres.");
        name = name.Trim();
        if (name.Length is < 1 or > 32 || name.Any(char.IsControl)) throw new Exception("Usa un nombre de mazo de 1 a 32 caracteres.");
        // Drafts may break the rules, but never grow without bound or reference cards that do not exist.
        if (cards.Count > DeckRules.MainDeckSize * 2 || traps.Count > DeckRules.MaxTraps * 2 ||
            RelikarioCatalog.Playable.All(r => r.Id != relikario) || cards.Concat(traps).Any(card => !CardCatalog.All.ContainsKey(card)))
            throw new Exception("El mazo contiene cartas o un Relikario que no existen.");

        var mine = ctx.Db.Deck.Owner.Filter(ctx.Sender).ToList();
        var now = ctx.Timestamp.MicrosecondsSinceUnixEpoch;
        if (id == 0)
        {
            // Retrying after a lost acknowledgement does not create a second deck.
            if (mine.Any(deck => deck.CommandId == commandId)) return;
            if (mine.Count >= MaxDecksPerPlayer) throw new Exception($"Puedes guardar hasta {MaxDecksPerPlayer} mazos.");
            ctx.Db.Deck.Insert(new Deck { Owner = ctx.Sender, Name = name, Relikario = relikario, Cards = cards, Traps = traps, CommandId = commandId, UpdatedAt = now });
            return;
        }
        if (mine.FirstOrDefault(deck => deck.Id == id) is not { Id: > 0 } current) throw new Exception("No se encontró el mazo.");
        current.Name = name; current.Relikario = relikario; current.Cards = cards; current.Traps = traps;
        current.CommandId = commandId; current.UpdatedAt = now;
        ctx.Db.Deck.Id.Update(current);
        // A deck edited into a draft stops being the one dealt.
        if (ctx.Db.Account.Player.Find(ctx.Sender) is Account account && account.ActiveDeck == id &&
            DeckRules.Validate(relikario, cards, traps).Count > 0) SetActive(ctx, account, 0);
    }

    [Reducer]
    public static void DeleteDeck(ReducerContext ctx, ulong id)
    {
        if (ctx.Db.Deck.Id.Find(id) is not Deck deck) return;
        if (deck.Owner != ctx.Sender) throw new Exception("No se encontró el mazo.");
        ctx.Db.Deck.Id.Delete(id);
        if (ctx.Db.Account.Player.Find(ctx.Sender) is Account account && account.ActiveDeck == id) SetActive(ctx, account, 0);
    }

    [Reducer]
    public static void SetActiveDeck(ReducerContext ctx, ulong id)
    {
        if (id != 0)
        {
            if (RequireAccount(ctx).Guest) throw new Exception(GuestsKeepNoDecks);
            if (ctx.Db.Deck.Id.Find(id) is not Deck deck || deck.Owner != ctx.Sender) throw new Exception("No se encontró el mazo.");
            var errors = DeckRules.Validate(deck.Relikario, deck.Cards, deck.Traps);
            if (errors.Count > 0) throw new Exception("Este mazo no cumple las reglas: " + errors[0]);
        }
        SetActive(ctx, RequireAccount(ctx), id);
    }

    private static void SetActive(ReducerContext ctx, Account account, ulong id)
    {
        account.ActiveDeck = id;
        ctx.Db.Account.Player.Update(account);
    }

    /// <summary>The deck a player takes into a room: their active deck while it is legal, otherwise the training deck.</summary>
    private static (List<string> Cards, List<string> Traps) DeckFor(ReducerContext ctx, Identity player)
    {
        if (ctx.Db.Account.Player.Find(player) is Account { ActiveDeck: > 0, Guest: false } account && ctx.Db.Deck.Id.Find(account.ActiveDeck) is Deck deck &&
            deck.Owner == player && DeckRules.Validate(deck.Relikario, deck.Cards, deck.Traps).Count == 0)
            return (deck.Cards, deck.Traps);
        return (CardCatalog.TrainingDeck(), CardCatalog.TrainingTraps());
    }
}
