using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    private ComboToggle? _comboResponsesToggle;

    private void RefreshEffectCardOptions()
    {
        _deckSearchOptions = _snapshot!.Options.Where(o => o.CardId != null && Deciding == DecisionKind.ChooseCard && o.Move.Kind == DuelMoveKind.Choose ||
            ChoosingCorruption && o.Move.Kind == DuelMoveKind.Corrupt || _showReplayMemory && o.Move.Kind == DuelMoveKind.Relikia && o.Move.Value == (int)RelikiaEffect.ReplayCode).ToArray();
    }

    private int? EffectCardOwner(DuelOption option)
    {
        if (_showReplayMemory && option.Move.Kind == DuelMoveKind.Relikia && option.Move.Value == (int)RelikiaEffect.ReplayCode)
            return _snapshot!.MySeat;
        if (option.Move.Kind == DuelMoveKind.Choose) return option.Owner;
        return option.Move.Kind == DuelMoveKind.Corrupt ? option.Move.Target : null;
    }

    // Memory choices keep their flat option index for selection; the gallery only groups them.
    private IReadOnlyList<ArenaPresentation.HandSection>? MemorySections()
    {
        if (_deckSearchOptions.Length == 0) return null;
        var owners = _deckSearchOptions.Select(EffectCardOwner).ToArray();
        if (!ChoosingOsamuCards && !owners.Any(owner => owner is 0 or 1)) return null;
        var s = _snapshot!;
        var sections = new List<ArenaPresentation.HandSection>();
        var deck = Enumerable.Range(0, owners.Length).Where(i => owners[i] == null).ToArray();
        if (deck.Length > 0)
            sections.Add(new("TU MAZO", ArenaPresentation.Mint, new CardOwner(s.MySeat, s.Players[s.MySeat].Name, s.Players[s.MySeat].RelikarioId), deck, FromDeck: true));
        foreach (var seat in new[] { s.MySeat, 1 - s.MySeat })
        {
            var indices = Enumerable.Range(0, _deckSearchOptions.Length).Where(i => owners[i] == seat).ToArray();
            if (indices.Length == 0) continue;
            var name = s.Players[seat].Name;
            var title = ChoosingLetterHand ? seat == s.MySeat ? "TU MANO" : $"MANO DE {name}" :
                seat != s.MySeat ? $"MEMORIA DE {name}" : ChoosingDeckGallery ? "TU MEMORIA" : $"TU MEMORIA · {name}";
            sections.Add(new(title,
                seat == s.MySeat ? ArenaPresentation.Mint : ArenaPresentation.Coral, new CardOwner(seat, name, s.Players[seat].RelikarioId), indices));
        }
        return sections;
    }

    private void ApplyPrivateComboPreference(DuelSnapshot snapshot)
    {
        _snapshot = snapshot;
        // Authorized options are new objects even when the public revision is unchanged.
        // Rebind the gallery without losing the selected target or rebuilding the board.
        RefreshEffectCardOptions(); BuildMoves(); UpdateComboResponseToggle(snapshot);
    }

    private void BuildComboResponseToggle(Node header)
    {
        _comboResponsesToggle = null;
        if (_preparing || _snapshot?.Winner != null) return;
        var toggle = new ComboToggle { Name = "ComboResponsesToggle", Compact = Short, Stacked = Desktop };
        toggle.TooltipText = "Mano alzada · Preguntar: permite responder con cartas COMBO.\nSalto · Pasar: conserva tus COMBO y omite sus respuestas, incluidos contraerrores.\nNo omite decisiones de habilidades, equipos ni efectos ya iniciados.\nPreferencia privada; no consume acciones ni PR.";
        var room = RoomCode;
        toggle.Toggled += enabled => Connection.SetComboResponses(enabled, room);
        header.AddChild(toggle); _comboResponsesToggle = toggle;
        UpdateComboResponseToggle(_snapshot!);
    }

    private void UpdateComboResponseToggle(DuelSnapshot snapshot)
    {
        if (_comboResponsesToggle == null) return;
        var enabled = Connection.RequestedComboResponses ?? !snapshot.AutoPassCombos;
        _comboResponsesToggle.ShowAsking(enabled);
        _comboResponsesToggle.Disabled = !Connection.Ready || Connection.PlayPending || snapshot.Winner != null;
    }
}
