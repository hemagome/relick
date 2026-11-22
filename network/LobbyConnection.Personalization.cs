using Relick.Core;
using Relick.Network.Types;
using SpacetimeDB;

namespace Relick.Network;

// The home's look, deck covers and results per Relikario. Subscribed apart, like the profiles, so a server published
// before they existed keeps syncing everything else and the game hides those options.
public sealed partial class LobbyConnection
{
    public bool PersonalizationAvailable { get; private set; }
    public int PersonalizationRevision { get; private set; }
    private int _personalizationWaiting;
    public bool PersonalizationPending => _personalizationWaiting > 0;
    private string _personalizationError = "";

    /// <summary>Why the last change was refused, reported once.</summary>
    public string TakePersonalizationError()
    {
        var error = _personalizationError; _personalizationError = "";
        return error;
    }

    public string HomeHero => _connection?.Db.MyHomeStyle.Iter().SingleOrDefault()?.Hero ?? "";
    public string HomeBackdrop => _connection?.Db.MyHomeStyle.Iter().SingleOrDefault()?.Backdrop ?? ProfileCatalog.DefaultPlaymat;
    public string DeckCover(ulong deck) => _connection?.Db.MyDeckCovers.Deck.Find(deck)?.Card ?? "";

    public IReadOnlyList<RelikarioRecord> Records => _connection?.Db.MyRecords.Iter()
        .Select(record => new RelikarioRecord(record.Relikario, record.Wins, record.Losses, record.Draws)).ToList() ?? [];

    public void SetHomeStyle(string hero, string backdrop) => RequestPersonalization(reducers => reducers.SetHomeStyle(hero, backdrop));
    public void SetDeckCover(ulong deck, string card) => RequestPersonalization(reducers => reducers.SetDeckCover(deck, card));

    private void RequestPersonalization(Action<RemoteReducers> send)
    {
        if (!Ready || !PersonalizationAvailable) return;
        _personalizationWaiting++;
        try { send(_connection!.Reducers); }
        catch (Exception) { _personalizationWaiting--; _personalizationError = "No se pudo enviar la solicitud. Comprueba tu conexión."; }
    }

    private void SubscribePersonalization(DbConnection connection)
    {
        connection.SubscriptionBuilder().OnApplied(_ => { PersonalizationAvailable = true; PersonalizationRevision++; DeckRevision++; })
            .OnError((_, _) => PersonalizationAvailable = false)
            .Subscribe(["SELECT * FROM my_home_style", "SELECT * FROM my_deck_covers", "SELECT * FROM my_records"]);
    }

    private void RegisterPersonalizationReplies()
    {
        void Changed() => PersonalizationRevision++;
        void Covered() { Changed(); DeckRevision++; }
        void Reply(ReducerEventContext context)
        {
            if (_personalizationWaiting > 0) _personalizationWaiting--;
            if (context.Event.Status is Status.Failed(var reason)) _personalizationError = reason;
            else if (context.Event.Status is not Status.Committed) _personalizationError = "El servidor no pudo guardar el cambio.";
            Changed();
        }
        var db = _connection!.Db;
        db.MyHomeStyle.OnInsert += (_, _) => Changed();
        db.MyHomeStyle.OnUpdate += (_, _, _) => Changed();
        db.MyHomeStyle.OnDelete += (_, _) => Changed();
        db.MyDeckCovers.OnInsert += (_, _) => Covered();
        db.MyDeckCovers.OnUpdate += (_, _, _) => Covered();
        db.MyDeckCovers.OnDelete += (_, _) => Covered();
        db.MyRecords.OnInsert += (_, _) => Changed();
        db.MyRecords.OnUpdate += (_, _, _) => Changed();
        db.MyRecords.OnDelete += (_, _) => Changed();
        _connection.Reducers.OnSetHomeStyle += (context, _, _) => Reply(context);
        _connection.Reducers.OnSetDeckCover += (context, _, _) => Reply(context);
        _personalizationWaiting = 0; _personalizationError = ""; Changed();
    }

    private void ForgetPersonalization()
    {
        PersonalizationAvailable = false;
        _personalizationWaiting = 0;
        PersonalizationRevision++;
    }
}
