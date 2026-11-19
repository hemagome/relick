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

    // A deck's side deck. Kept in its own table so decks saved before it existed need no migration.
    [Table(Accessor = "DeckSide")]
    public partial struct DeckSide
    {
        [PrimaryKey] public ulong Deck;
        public List<string> Cards;
    }

    private static List<string> SideOf(ReducerContext ctx, ulong deck) => ctx.Db.DeckSide.Deck.Find(deck)?.Cards ?? [];

    private static void StoreSide(ReducerContext ctx, ulong deck, List<string> side)
    {
        ctx.Db.DeckSide.Deck.Delete(deck);
        if (side.Count > 0) ctx.Db.DeckSide.Insert(new DeckSide { Deck = deck, Cards = side });
    }

    // A deck's cosmetic sleeve and die. Its own table, like DeckSide, so older decks need no migration:
    // a deck without a row plays with the classic ones.
    [Table(Accessor = "DeckStyle")]
    public partial struct DeckStyle
    {
        [PrimaryKey] public ulong Deck;
        public string Sleeve;
        public string Dice;
    }

    private static (string Sleeve, string Dice) StyleOf(ReducerContext ctx, ulong deck) =>
        ctx.Db.DeckStyle.Deck.Find(deck) is DeckStyle style ? (style.Sleeve, style.Dice) : (CosmeticCatalog.DefaultSleeve, CosmeticCatalog.DefaultDice);

    private static void StoreStyle(ReducerContext ctx, ulong deck, string sleeve, string dice)
    {
        ctx.Db.DeckStyle.Deck.Delete(deck);
        if (sleeve != CosmeticCatalog.DefaultSleeve || dice != CosmeticCatalog.DefaultDice)
            ctx.Db.DeckStyle.Insert(new DeckStyle { Deck = deck, Sleeve = sleeve, Dice = dice });
    }

    // The sleeve and die a player brought into a room; a room dealt before styles existed has no row.
    [Table(Accessor = "LoadoutStyle")]
    public partial struct LoadoutStyle
    {
        [PrimaryKey] public Identity Player;
        public string RoomCode;
        public string Sleeve;
        public string Dice;
    }

    [Table(Accessor = "LoadoutRelikario")]
    public partial struct LoadoutRelikario
    {
        [PrimaryKey] public Identity Player;
        public string Relikario;
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
        public List<string> Side;
        public bool Valid;
        public bool Active;
    }

    [View(Accessor = "MyDecks", Public = true, PrimaryKey = "Id")]
    public static IEnumerable<DeckInfo> MyDecks(ViewContext ctx)
    {
        var active = ctx.Db.Account.Player.Find(ctx.Sender)?.ActiveDeck ?? 0;
        return ctx.Db.Deck.Owner.Filter(ctx.Sender).Select(deck =>
        {
            var side = ctx.Db.DeckSide.Deck.Find(deck.Id)?.Cards ?? [];
            var cards = DeckRules.NormalizeAttackArt(deck.Relikario, deck.Cards);
            return new DeckInfo
            {
                Id = deck.Id, Name = deck.Name, Relikario = deck.Relikario, Cards = cards, Traps = deck.Traps, Side = side,
                Valid = DeckRules.Validate(deck.Relikario, cards, deck.Traps, side).Count == 0, Active = deck.Id == active
            };
        }).ToList();
    }

    [SpacetimeDB.Type]
    public partial struct DeckStyleInfo
    {
        public ulong Deck;
        public string Sleeve;
        public string Dice;
    }

    /// <summary>The caller's deck styles, apart from <see cref="MyDecks"/> so clients that predate styles keep reading
    /// their decks unchanged. A deck missing here plays with the classic sleeve and die.</summary>
    [View(Accessor = "MyDeckStyles", Public = true, PrimaryKey = "Deck")]
    public static IEnumerable<DeckStyleInfo> MyDeckStyles(ViewContext ctx) =>
        ctx.Db.Deck.Owner.Filter(ctx.Sender)
            .Select(deck => ctx.Db.DeckStyle.Deck.Find(deck.Id))
            .Where(style => style != null)
            .Select(style => new DeckStyleInfo { Deck = style!.Value.Deck, Sleeve = style.Value.Sleeve, Dice = style.Value.Dice })
            .ToList();

    /// <summary>Creates a deck when <paramref name="id"/> is zero, otherwise replaces the caller's deck.
    /// An illegal deck is kept as a draft; it cannot be made active or dealt.</summary>
    /// <summary>Saves a deck with its cosmetic sleeve and die. Older clients call <see cref="SaveDeck"/>, which keeps the current style.</summary>
    [Reducer]
    public static void SaveDeckV2(ReducerContext ctx, ulong id, string name, string relikario, List<string> cards, List<string> traps, List<string> side,
        string sleeve, string dice, string commandId)
    {
        if (!CosmeticCatalog.IsSleeve(sleeve) || !CosmeticCatalog.IsDice(dice)) throw new Exception("Esa mica o ese dado no existen.");
        StoreDeck(ctx, id, name, relikario, cards, traps, side, commandId, (sleeve, dice));
    }

    [Reducer]
    public static void SaveDeck(ReducerContext ctx, ulong id, string name, string relikario, List<string> cards, List<string> traps, List<string> side, string commandId) =>
        StoreDeck(ctx, id, name, relikario, cards, traps, side, commandId, null);

    private static void StoreDeck(ReducerContext ctx, ulong id, string name, string relikario, List<string> cards, List<string> traps, List<string> side,
        string commandId, (string Sleeve, string Dice)? style)
    {
        if (RequireAccount(ctx).Guest) throw new Exception(GuestsKeepNoDecks);
        if (!Guid.TryParseExact(commandId, "N", out _)) throw new Exception("Solicitud de mazo inválida.");
        if (name.Length > 128) throw new Exception("Usa un nombre de mazo de 1 a 32 caracteres.");
        name = name.Trim();
        if (name.Length is < 1 or > 32 || name.Any(char.IsControl)) throw new Exception("Usa un nombre de mazo de 1 a 32 caracteres.");
        // Drafts may break the rules, but never grow without bound or reference cards that do not exist.
        if (cards.Count > DeckRules.MainDeckSize * 2 || traps.Count > DeckRules.MaxTraps * 2 || side.Count > DeckRules.MaxSide * 2 ||
            RelikarioCatalog.Playable.All(r => r.Id != relikario) || cards.Concat(traps).Concat(side).Any(card => !CardCatalog.All.ContainsKey(card)))
            throw new Exception("El mazo contiene cartas o un Relikario que no existen.");
        cards = DeckRules.NormalizeAttackArt(relikario, cards);

        var mine = ctx.Db.Deck.Owner.Filter(ctx.Sender).ToList();
        var now = ctx.Timestamp.MicrosecondsSinceUnixEpoch;
        if (id == 0)
        {
            // Retrying after a lost acknowledgement does not create a second deck.
            if (mine.Any(deck => deck.CommandId == commandId)) return;
            if (mine.Count >= MaxDecksPerPlayer) throw new Exception($"Puedes guardar hasta {MaxDecksPerPlayer} mazos.");
            var created = ctx.Db.Deck.Insert(new Deck { Owner = ctx.Sender, Name = name, Relikario = relikario, Cards = cards, Traps = traps, CommandId = commandId, UpdatedAt = now });
            StoreSide(ctx, created.Id, side);
            if (style is { } chosen) StoreStyle(ctx, created.Id, chosen.Sleeve, chosen.Dice);
            return;
        }
        if (mine.FirstOrDefault(deck => deck.Id == id) is not { Id: > 0 } current) throw new Exception("No se encontró el mazo.");
        current.Name = name; current.Relikario = relikario; current.Cards = cards; current.Traps = traps;
        current.CommandId = commandId; current.UpdatedAt = now;
        ctx.Db.Deck.Id.Update(current);
        StoreSide(ctx, id, side);
        if (style is { } updated) StoreStyle(ctx, id, updated.Sleeve, updated.Dice);
        // A deck edited into a draft stops being the one dealt.
        if (ctx.Db.Account.Player.Find(ctx.Sender) is Account account && account.ActiveDeck == id &&
            DeckRules.Validate(relikario, cards, traps, side).Count > 0) SetActive(ctx, account, 0);
    }

    [Reducer]
    public static void DeleteDeck(ReducerContext ctx, ulong id)
    {
        if (ctx.Db.Deck.Id.Find(id) is not Deck deck) return;
        if (deck.Owner != ctx.Sender) throw new Exception("No se encontró el mazo.");
        ctx.Db.Deck.Id.Delete(id);
        ctx.Db.DeckSide.Deck.Delete(id);
        ctx.Db.DeckStyle.Deck.Delete(id);
        if (ctx.Db.Account.Player.Find(ctx.Sender) is Account account && account.ActiveDeck == id) SetActive(ctx, account, 0);
    }

    [Reducer]
    public static void SetActiveDeck(ReducerContext ctx, ulong id)
    {
        if (id != 0)
        {
            if (RequireAccount(ctx).Guest) throw new Exception(GuestsKeepNoDecks);
            if (ctx.Db.Deck.Id.Find(id) is not Deck deck || deck.Owner != ctx.Sender) throw new Exception("No se encontró el mazo.");
            var cards = DeckRules.NormalizeAttackArt(deck.Relikario, deck.Cards);
            var errors = DeckRules.Validate(deck.Relikario, cards, deck.Traps, SideOf(ctx, id));
            if (errors.Count > 0) throw new Exception("Este mazo no cumple las reglas: " + errors[0]);
        }
        SetActive(ctx, RequireAccount(ctx), id);
    }

    private static void SetActive(ReducerContext ctx, Account account, ulong id)
    {
        account.ActiveDeck = id;
        ctx.Db.Account.Player.Update(account);
    }

    /// <summary>The deck a player takes into a room: their active deck while it is legal, otherwise the training deck
    /// with the classic sleeve and die.</summary>
    private static (List<string> Cards, List<string> Traps, string Sleeve, string Dice, string Relikario) DeckFor(ReducerContext ctx, Identity player)
    {
        if (ctx.Db.Account.Player.Find(player) is Account { ActiveDeck: > 0, Guest: false } account &&
            ctx.Db.Deck.Id.Find(account.ActiveDeck) is Deck deck && deck.Owner == player)
        {
            var cards = DeckRules.NormalizeAttackArt(deck.Relikario, deck.Cards);
            if (DeckRules.Validate(deck.Relikario, cards, deck.Traps, SideOf(ctx, deck.Id)).Count == 0)
            {
                var (sleeve, dice) = StyleOf(ctx, deck.Id);
                return (cards, deck.Traps, sleeve, dice, deck.Relikario);
            }
        }
        return (CardCatalog.TrainingDeck(), CardCatalog.TrainingTraps(), CosmeticCatalog.DefaultSleeve, CosmeticCatalog.DefaultDice, RelikarioCatalog.Yuno.Id);
    }
}
