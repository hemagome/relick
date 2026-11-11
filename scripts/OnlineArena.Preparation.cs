using Godot;
using Relick.Core;
using Relick.Network.Types;
using NetworkCard = Relick.Network.Types.HandCard;

namespace Relick;

public partial class OnlineArena
{
    private const int OpeningHandSize = 5;
    private bool _preparing;
    private bool _choosingOpeningSkill;
    private readonly bool[] _openingHandsPresented = new bool[2];
    private bool _openingConnected, _openingPending;
    private PreparationInfo? _openingInfo;
    private NetworkCard[] _openingCards = [];
    private string? _openingInspection;
    private readonly HashSet<ulong> _openingReturns = [];
    private string? _openingKey;
    private (ulong Revision, ulong Epoch, bool Ready, bool Confirming, bool Choosing)? _preparationVersion;
    internal Button? PreparationConfirmButton { get; private set; }
    internal int OpeningCardCount => _openingCards.Length;
    internal int OpeningSelectionCount => _openingReturns.Count;
    internal bool IsPreparing => _preparing;
    private bool CanChooseOpening => _openingConnected && !_openingPending &&
        _openingInfo is { MyConfirmed: false, Started: false } && _openingCards.Length == OpeningHandSize;

    private void UpdatePreparation()
    {
        // Keep the current deal intact when the other player updates their preparation.
        if (_preparing && IsAnimating) return;
        var version = (Connection.PreparationRevision, Connection.Epoch, Connection.Ready, Connection.ConfirmationPending, Connection.InitialSkillPending);
        if (_preparing && _preparationVersion == version) return;
        _preparationVersion = version;
        if (!Connection.Ready || Connection.Room is not { Dealt: true } room)
        {
            _openingConnected = false; _initialSkillConnected = false; _lastDisabled = null; return;
        }
        if (Connection.InitialSkills is { Selected: false } skills)
        {
            PresentInitialSkillChoice(room, skills, Connection.Ready, Connection.InitialSkillPending);
            return;
        }
        var info = Connection.Preparation;
        var cards = Connection.Hand;
        if (info == null || (!info.Started && cards.Length != OpeningHandSize))
        {
            if (!_preparing) PresentPreparation(room, null, [], false, false);
            _openingConnected = false; _lastDisabled = null; return;
        }
        // Keep the original identities/positions until the combat snapshot arrives.
        if (info.Started)
        {
            if (_openingInfo?.Started != true)
            {
                _openingInfo = info; BuildMoves(); _combatMessage.Text = PreparationMessage();
            }
            return;
        }
        PresentPreparation(room, info, cards, Connection.Ready, Connection.ConfirmationPending);
    }

    // This is a presentation of server-authorized views, never a local rules engine.
    internal void PresentPreparation(RoomInfo room, PreparationInfo? info, NetworkCard[] cards, bool connected, bool pending)
    {
        var key = $"{room.Code}:{room.MySeat}:{room.PlayerOne}:{room.PlayerTwo}:{connected}:{pending}:{info?.MyConfirmed}:{info?.OpponentConfirmed}:{info?.Started}:" +
            $"{info?.HandCountOne}:{info?.HandCountTwo}:{info?.DeckCountOne}:{info?.DeckCountTwo}:" +
            string.Join(',', cards.Select(c => $"{c.Id}/{c.Definition}"));
        if (_openingKey == key && !_choosingOpeningSkill && _preparing && _openingConnected == connected && _openingPending == pending) return;
        _choosingOpeningSkill = false;
        if (!cards.Any(c => c.Definition == _openingInspection)) _openingInspection = null;
        _openingKey = key; _openingCards = cards; _openingInfo = info;
        _openingConnected = connected; _openingPending = pending; _preparing = true;
        if (info?.MyConfirmed == true)
        {
            _openingReturns.Clear(); _openingReturns.UnionWith(info.MyReturnedCards);
        }
        _openingReturns.IntersectWith(cards.Select(c => c.Id));
        SyncOpeningSelection();
        DuelPlayerView Player(int seat) => new()
        {
            Name = seat == 0 ? room.PlayerOne : room.PlayerTwo,
            Health = RelikarioCatalog.Yuno.MaxHealth, MaxHealth = RelikarioCatalog.Yuno.MaxHealth, Level = 1,
            Dexterity = RelikarioCatalog.Yuno.StartingDexterity,
            HandCount = (int)(seat == 0 ? info?.HandCountOne ?? 0 : info?.HandCountTwo ?? 0),
            DeckCount = (int)(seat == 0 ? info?.DeckCountOne ?? 0 : info?.DeckCountTwo ?? 0),
            Hand = seat == room.MySeat ? cards.Select(c => (string?)c.Definition).ToArray() :
                new string?[(int)(seat == 0 ? info?.HandCountOne ?? 0 : info?.HandCountTwo ?? 0)]
        };
        _snapshot = new DuelSnapshot { MySeat = room.MySeat, Phase = Phase.Preparation, Stage = Phase.Preparation,
            DecisionPlayer = room.MySeat, Players = [Player(0), Player(1)] };
        var newlyPresented = new[] { room.MySeat, (byte)(1 - room.MySeat) }.Where(seat =>
            !_openingHandsPresented[seat] && _snapshot.Players[seat].Hand.Length == OpeningHandSize).ToArray();
        foreach (var seat in newlyPresented) _openingHandsPresented[seat] = true;
        _needsBaseline = false;
        // Rebuild only when preparation data changes; cancel flights tied to the previous layout.
        ResetAnimations(); Render();
        if (newlyPresented.Length > 0 && connected && info is { MyConfirmed: false, Started: false })
        {
            for (var slot = 0; slot < OpeningHandSize; slot++)
                foreach (var seat in newlyPresented)
                    _flights.Enqueue(new(new DuelVisualEvent { Kind = DuelVisualKind.Draw, Player = seat, Value = slot }, Duration: 0.20));
            HideOpeningDestinations(); _flightLayoutFrames = 2; _lastDisabled = null;
        }
    }

    private void SyncOpeningSelection()
    {
        _selected.Clear();
        for (var i = 0; i < _openingCards.Length; i++) if (_openingReturns.Contains(_openingCards[i].Id)) _selected.Add(i);
    }

    private string PreparationCaption() => _choosingOpeningSkill ? "ELIGE UNA HABILIDAD" : _openingInfo?.Started == true ? "COMPLETADA" :
        _openingInfo?.MyConfirmed == true ? "ESPERANDO AL RIVAL" : _openingPending ? "CONFIRMANDO" : "ELIGE TU MANO";

    private string PreparationMessage() => _choosingOpeningSkill ? "Elige una habilidad de tu mazo" : _openingInfo?.Started == true ? "Preparación completada" :
        _openingInfo?.MyConfirmed == true ? "Mano confirmada\nEsperando al rival…" :
        _openingPending ? "Confirmando tu mano…" : _openingCards.Length != OpeningHandSize ? "Repartiendo las manos…" : "Elige tu mano inicial";

    private void BuildPreparationMoves()
    {
        if (_choosingOpeningSkill) { BuildInitialSkillMoves(); return; }
        _selection.Text = _openingInfo?.Started == true ? "Entrando al combate…" : _openingInfo?.MyConfirmed == true
            ? "Tu selección está confirmada. El combate comenzará cuando confirme tu rival."
            : "Toca las cartas que quieres cambiar o conserva tu mano.";
        var label = _openingInfo?.Started == true ? "Preparación completada" : _openingPending ? "Confirmando…" :
            _openingInfo?.MyConfirmed == true ? "Mano confirmada · esperando rival" :
            _openingReturns.Count == 0 ? "Conservar mano y confirmar" : $"Cambiar {_openingReturns.Count} carta(s) y confirmar";
        PreparationConfirmButton = ArenaPresentation.Button(_moves, label, () =>
        {
            if (CanChooseOpening && !IsAnimating) Connection.Confirm(_openingReturns);
        });
        PreparationConfirmButton.Name = "ConfirmOpeningHand";
        PreparationConfirmButton.SetMeta("discards", 0); _moveButtons.Add(PreparationConfirmButton);
        if (Compact)
        {
            PreparationConfirmButton.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            PreparationConfirmButton.CustomMinimumSize = new(Short ? 280 : 0, 48);
        }
        _lastDisabled = null; UpdateEnabled();
    }

    private bool IsLiveOpening(DuelSnapshot combat) => _preparing && !_needsBaseline && _openingCards.Length == OpeningHandSize &&
        combat is { Revision: 0, Turn: 1, Phase: Phase.Combat, Winner: null };

    private List<Flight> CaptureMulliganOrigins(DuelSnapshot combat)
    {
        var flights = new List<Flight>();
        for (var slot = _openingCards.Length - 1; slot >= 0; slot--)
        {
            if (!_openingReturns.Contains(_openingCards[slot].Id)) continue;
            var node = ZoneNode(0, CardZone.Hand, slot);
            if (node == null || !RectVisible(node, ZoneRect(node))) continue;
            flights.Add(new(new DuelVisualEvent { Kind = DuelVisualKind.Return, Player = combat.MySeat, Value = slot },
                ZoneRect(node), _openingCards[slot].Definition, Duration: 0.28, OriginRotation: node.Rotation));
        }
        return flights;
    }

    private void AnimateOpening(DuelSnapshot combat, List<Flight> returns, PreparationInfo? opening)
    {
        foreach (var flight in returns) _flights.Enqueue(flight);
        if (_openingReturns.Count > 0)
        {
            _flights.Enqueue(new(new DuelVisualEvent { Kind = DuelVisualKind.Shuffle, Player = combat.MySeat }));
            var kept = _openingCards.Length - _openingReturns.Count;
            for (var slot = kept; slot < _openingCards.Length; slot++)
                _flights.Enqueue(new(new DuelVisualEvent { Kind = DuelVisualKind.Draw, Player = combat.MySeat, Value = slot }, Duration: 0.28));
        }
        // Opponent mulligan identities/counts are not in the authorized preparation view.
        foreach (var seat in new[] { 0, 1 })
            _flights.Enqueue(new(new DuelVisualEvent { Kind = DuelVisualKind.BonusPlace, Player = seat }, Face: combat.Players[seat].Bonus, Duration: 0.28));
        var starter = combat.CurrentPlayer;
        var hand = combat.Players[starter].Hand;
        var firstTurnDraw = hand.Length == OpeningHandSize + 1
            ? new Flight(new DuelVisualEvent { Kind = DuelVisualKind.Draw, Player = starter, Value = OpeningHandSize }, Duration: 0.28)
            : null;
        QueueOpeningInitiative(combat, opening, firstTurnDraw);
        HideOpeningDestinations(); _flightLayoutFrames = 2; _lastDisabled = null;
    }

    private void HideOpeningDestinations() => SetQueuedDestinationsPending(true);

    private void SetQueuedDestinationsPending(bool pending)
    {
        foreach (var flight in _openingTurnDraw is { } draw ? _flights.Append(draw) : _flights)
        {
            var relative = flight.Event.Player == _snapshot!.MySeat ? 0 : 1;
            var target = flight.Event.Kind switch
            {
                DuelVisualKind.Draw => ZoneNode(relative, CardZone.Hand, flight.Event.Value),
                DuelVisualKind.BonusPlace => ZoneNode(relative, CardZone.Bonus, -1),
                DuelVisualKind.Pile when flight.IsPlacement => ZoneNode(relative, flight.Event.Destination, flight.Event.Value),
                _ => null
            };
            SetDestinationPending(flight, target, pending);
        }
    }
}
