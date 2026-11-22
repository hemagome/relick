using SpacetimeDB;
using Relick.Core;

public static partial class Module
{
    // How the player's own home looks. No row: the equipped deck's Relikario over the classic backdrop.
    [Table(Accessor = "HomeStyle")]
    public partial struct HomeStyle
    {
        [PrimaryKey] public Identity Player;
        public string Hero; // Empty follows the equipped deck.
        public string Backdrop; // A playmat id, used only as a tint.
    }

    // A deck's cover. Its own table, like DeckStyle, so saved decks need no migration.
    [Table(Accessor = "DeckCover")]
    public partial struct DeckCover
    {
        [PrimaryKey] public ulong Deck;
        public string Card;
    }

    [SpacetimeDB.Type]
    public partial struct HomeStyleInfo
    {
        public string Hero;
        public string Backdrop;
    }

    [SpacetimeDB.Type]
    public partial struct DeckCoverInfo
    {
        public ulong Deck;
        public string Card;
    }

    [SpacetimeDB.Type]
    public partial struct RecordInfo
    {
        public string Relikario;
        public uint Wins;
        public uint Losses;
        public uint Draws;
    }

    [View(Accessor = "MyHomeStyle", Public = true)]
    public static HomeStyleInfo? MyHomeStyle(ViewContext ctx) =>
        ctx.Db.HomeStyle.Player.Find(ctx.Sender) is HomeStyle style ? new HomeStyleInfo { Hero = style.Hero, Backdrop = style.Backdrop } : null;

    [View(Accessor = "MyDeckCovers", Public = true, PrimaryKey = "Deck")]
    public static IEnumerable<DeckCoverInfo> MyDeckCovers(ViewContext ctx) =>
        ctx.Db.Deck.Owner.Filter(ctx.Sender)
            .Select(deck => ctx.Db.DeckCover.Deck.Find(deck.Id))
            .Where(cover => cover != null)
            .Select(cover => new DeckCoverInfo { Deck = cover!.Value.Deck, Card = cover.Value.Card })
            .ToList();

    /// <summary>The caller's results with each Relikario, which <see cref="MyProfile"/> only totals.</summary>
    [View(Accessor = "MyRecords", Public = true, PrimaryKey = "Relikario")]
    public static IEnumerable<RecordInfo> MyRecords(ViewContext ctx) =>
        ctx.Db.PlayerRecord.Player.Filter(ctx.Sender)
            .Select(record => new RecordInfo { Relikario = record.Relikario, Wins = record.Wins, Losses = record.Losses, Draws = record.Draws })
            .ToList();

    [Reducer]
    public static void SetHomeStyle(ReducerContext ctx, string hero, string backdrop)
    {
        RequireAccount(ctx);
        if (hero.Length > 0 && !ProfileCatalog.IsAvatar(hero)) throw new Exception("Ese Relikario de inicio no existe.");
        if (!ProfileCatalog.IsPlaymat(backdrop)) throw new Exception("Ese fondo no existe.");
        ctx.Db.HomeStyle.Player.Delete(ctx.Sender);
        if (hero.Length > 0 || backdrop != ProfileCatalog.DefaultPlaymat)
            ctx.Db.HomeStyle.Insert(new HomeStyle { Player = ctx.Sender, Hero = hero, Backdrop = backdrop });
    }

    /// <summary>An empty card returns the cover to the Relikario's portrait.</summary>
    [Reducer]
    public static void SetDeckCover(ReducerContext ctx, ulong deck, string card)
    {
        RequireAccount(ctx);
        if (ctx.Db.Deck.Id.Find(deck) is not Deck mine || mine.Owner != ctx.Sender) throw new Exception("No se encontró el mazo.");
        if (card.Length > 0 && !mine.Cards.Contains(card) && !mine.Traps.Contains(card)) throw new Exception("La portada debe ser una carta de ese mazo.");
        ctx.Db.DeckCover.Deck.Delete(deck);
        if (card.Length > 0) ctx.Db.DeckCover.Insert(new DeckCover { Deck = deck, Card = card });
    }
}
