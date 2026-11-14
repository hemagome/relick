using Relick.Network.Types;
using SpacetimeDB;

namespace Relick.Network;

// Saved decks of the signed-in player. The server validates and owns them; this only relays requests.
public sealed partial class LobbyConnection
{
    private IReadOnlyList<DeckInfo>? _decks;
    /// <summary>Changes whenever the saved decks or the active one change, so screens rebuild only then.</summary>
    public int DeckRevision { get; private set; }
    /// <summary>A deck request is on its way; further ones wait for its answer.</summary>
    public bool DeckPending { get; private set; }
    /// <summary>Why the last deck request was refused, or empty.</summary>
    public string DeckError { get; private set; } = "";
    /// <summary>Counts accepted saves, so an editor knows its own save went through.</summary>
    public int DeckSaves { get; private set; }

    public IReadOnlyList<DeckInfo> Decks => _decks ??= _connection?.Db.MyDecks.Iter().OrderBy(deck => deck.Id).ToList() ?? [];

    public void SaveDeck(ulong id, string name, string relikario, IEnumerable<string> cards, IEnumerable<string> traps, IEnumerable<string> side) =>
        RequestDeck(reducers => reducers.SaveDeck(id, name.Trim(), relikario, cards.ToList(), traps.ToList(), side.ToList(), Guid.NewGuid().ToString("N")));
    public void DeleteDeck(ulong id) => RequestDeck(reducers => reducers.DeleteDeck(id));
    public void SetActiveDeck(ulong id) => RequestDeck(reducers => reducers.SetActiveDeck(id));

    /// <summary>The server erased this player's account; the session that reached it is no longer of use.</summary>
    public bool AccountDeleted { get; private set; }
    public bool AccountPending { get; private set; }
    private string _accountError = "";

    public void DeleteAccount()
    {
        if (!Ready || AccountPending || Room != null || Searching) return;
        AccountPending = true;
        try { _connection!.Reducers.DeleteAccount(); }
        catch (Exception) { AccountPending = false; _accountError = "No se pudo enviar la solicitud. Comprueba tu conexión."; }
    }

    /// <summary>Why the account could not be deleted, reported once.</summary>
    public string TakeAccountError()
    {
        var error = _accountError; _accountError = "";
        return error;
    }

    private void RequestDeck(Action<RemoteReducers> send)
    {
        if (!Ready || DeckPending) return;
        DeckPending = true; DeckError = "";
        try { send(_connection!.Reducers); }
        catch (Exception) { DeckPending = false; DeckError = "No se pudo enviar la solicitud. Comprueba tu conexión."; }
    }

    private void RegisterDeckReplies()
    {
        void Changed() { _decks = null; DeckRevision++; }
        void Reply(ReducerEventContext context, bool save = false)
        {
            DeckPending = false;
            DeckError = context.Event.Status is Status.Failed(var reason) ? reason
                : context.Event.Status is Status.Committed ? "" : "El servidor no pudo atender la solicitud.";
            if (save && DeckError.Length == 0) DeckSaves++;
        }
        _connection!.Db.MyDecks.OnInsert += (_, _) => Changed();
        _connection.Db.MyDecks.OnUpdate += (_, _, _) => Changed();
        _connection.Db.MyDecks.OnDelete += (_, _) => Changed();
        _connection.Reducers.OnSaveDeck += (context, _, _, _, _, _, _, _) => Reply(context, save: true);
        _connection.Reducers.OnDeleteDeck += (context, _) => Reply(context);
        _connection.Reducers.OnSetActiveDeck += (context, _) => Reply(context);
        _connection.Reducers.OnDeleteAccount += context =>
        {
            AccountPending = false;
            if (context.Event.Status is Status.Committed) AccountDeleted = true;
            else _accountError = context.Event.Status is Status.Failed(var reason) ? reason : "El servidor no pudo eliminar la cuenta.";
        };
        DeckPending = AccountPending = AccountDeleted = false; Changed();
    }
}
