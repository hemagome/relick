using SpacetimeDB;
using Relick.Core;

public static partial class Module
{
    // Only players signed in through the game's own SpacetimeAuth client are admitted. The client ID is public.
    private const string AuthIssuer = "https://auth.spacetimedb.com/oidc";
    private static readonly string[] AuthClients = ["client_034x0qkKtrinrInTSvJZAI"];
    private const string SignInRequired = "Inicia sesión para jugar.";
    // The database owner's CLI identity. It may connect to inspect tables and call reducers, but gets no player
    // account; it can already publish this module, so admitting it grants nothing new.
    private static readonly Identity Owner = Identity.FromHexString("c2006ee7c0a78424ad24f733d02e9b9906f6c654c211055530bd912128236c40");

    [Table(Accessor = "Account")]
    public partial struct Account
    {
        [PrimaryKey] public Identity Player;
        public string Name;
        public ulong ActiveDeck; // Zero plays the training deck.
        public long CreatedAt;
        [Default(false)] public bool Guest; // Signed in without an identity of their own: plays, but keeps no decks.
    }

    [SpacetimeDB.Type]
    public partial struct AccountInfo
    {
        public string Name;
        public ulong ActiveDeck;
        public bool Guest;
    }

    [View(Accessor = "MyAccount", Public = true)]
    public static AccountInfo? MyAccount(ViewContext ctx) =>
        ctx.Db.Account.Player.Find(ctx.Sender) is Account account
            ? new AccountInfo { Name = account.Name, ActiveDeck = account.ActiveDeck, Guest = account.Guest } : null;

    /// <summary>Runs on every connection: rejects credentials that are not a game sign-in and creates the account on the first one.</summary>
    private static void SignIn(ReducerContext ctx)
    {
        if (ctx.Sender == Owner) return;
        var claims = ctx.SenderAuth.Jwt;
        // The server's own anonymous credentials carry another issuer, so they never pass as an account.
        var signedIn = claims != null && claims.Issuer == AuthIssuer && claims.Audience.Any(AuthClients.Contains);
#if !RELICK_DEV_AUTH
        if (!signedIn) throw new Exception(SignInRequired);
#endif
        var guest = signedIn && AccountClaims.IsGuest(claims!.RawPayload);
        if (ctx.Db.Account.Player.Find(ctx.Sender) is Account known)
        {
            // The provider decides on every sign-in whether this is still a guest.
            if (known.Guest != guest) { known.Guest = guest; ctx.Db.Account.Player.Update(known); }
            return;
        }
        // Anonymous players exist only in local development builds; they keep typing a name for each room.
        ctx.Db.Account.Insert(new Account
        {
            Player = ctx.Sender, Name = signedIn ? AccountClaims.SuggestedName(claims!.RawPayload) : "",
            CreatedAt = ctx.Timestamp.MicrosecondsSinceUnixEpoch, Guest = guest
        });
    }

    private const string GuestsKeepNoDecks = "Como invitado juegas con el mazo de entrenamiento. Inicia sesión con una cuenta para guardar mazos.";

    // Accounts are created only by an accepted connection, so this also covers calls that never opened one.
    private static Account RequireAccount(ReducerContext ctx) =>
        ctx.Db.Account.Player.Find(ctx.Sender) ?? throw new Exception(SignInRequired);

    /// <summary>The name shown to the rival: the account's own, so a typed name cannot impersonate another player.</summary>
    private static string PlayerName(ReducerContext ctx, string typed)
    {
        var account = RequireAccount(ctx);
        return account.Name.Length > 0 ? account.Name : ValidateName(typed);
    }

    [Reducer]
    public static void SetDisplayName(ReducerContext ctx, string name)
    {
        var account = RequireAccount(ctx);
        account.Name = ValidateName(name);
        ctx.Db.Account.Player.Update(account);
    }

    /// <summary>Erases everything the game stores about the caller. The sign-in record kept by the account
    /// provider is outside this database; signing in again later starts a new, empty account.</summary>
    [Reducer]
    public static void DeleteAccount(ReducerContext ctx)
    {
        RequireAccount(ctx);
        EraseAccount(ctx, ctx.Sender);
    }

    /// <summary>The same erasure on a player's behalf, for requests handled by the database owner.</summary>
    [Reducer]
    public static void DeleteAccountOf(ReducerContext ctx, Identity player)
    {
        if (ctx.Sender != Owner) throw new Exception("Solo el dueño de la base puede eliminar cuentas ajenas.");
        if (ctx.Db.Account.Player.Find(player) == null) throw new Exception("No existe esa cuenta.");
        EraseAccount(ctx, player);
    }

    private static void EraseAccount(ReducerContext ctx, Identity player)
    {
        // A room holds the rival's match too, so it has to be closed or finished first.
        if (ctx.Db.Member.Player.Find(player) != null)
            throw new Exception("Cierra tu sala o termina el duelo antes de eliminar la cuenta.");
        ctx.Db.QuickSearch.Player.Delete(player);
        foreach (var deck in ctx.Db.Deck.Owner.Filter(player).ToList()) { ctx.Db.Deck.Id.Delete(deck.Id); ctx.Db.DeckSide.Deck.Delete(deck.Id); }
        ctx.Db.Loadout.Player.Delete(player);
        ctx.Db.PlayBudget.Player.Delete(player);
        ctx.Db.Account.Player.Delete(player);
    }
}
