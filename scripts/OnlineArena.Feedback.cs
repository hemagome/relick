using Godot;
using Relick.Core;

namespace Relick;

// Decision feedback: whose move it is, refused plays, cards with a play and what an attack can do.
// Everything here reads the authorized snapshot and the connection; nothing predicts a rule.
public partial class OnlineArena
{
    /// <summary>How long a refused play stays in the inspector unless the player plays again first.</summary>
    internal const ulong RejectionMilliseconds = 6000;

    private Label? _stageStatus, _stageDetail;
    private IconView? _stageStatusIcon;
    private Control? _stageStatusCell;
    private string? _stageStatusKey;
    private ulong _seenRejectionSerial;
    private string _rejectionText = "";
    private ulong _rejectionUntil;
    private int? _inspectorPreviewOwner;
    private string? _inspectorPreviewRelikario;

    internal string StageStatusText => _stageStatus?.Text ?? "";
    internal bool ShowingRejection => _rejectionUntil > Time.GetTicksMsec() && _rejectionText.Length > 0;

    private void BindStageStatus(Control bar)
    {
        _stageStatusCell = bar.FindChild("StageStatusCell", true, false) as Control;
        _stageStatus = bar.FindChild("StageStatus", true, false) as Label;
        _stageDetail = bar.FindChild("StageDetail", true, false) as Label;
        _stageStatusIcon = bar.FindChild("StageStatusIcon", true, false) as IconView;
        _stageStatusKey = null;
        // Filled while building, so a rebuilt bar never shows an empty headline for a frame.
        if (_snapshot != null) RefreshStageStatus(_snapshot);
    }

    private void ClearStageStatus()
    {
        _stageStatus = _stageDetail = null; _stageStatusIcon = null; _stageStatusCell = null; _stageStatusKey = null;
    }

    /// <summary>Called every frame; it only touches nodes when the presented state changes.</summary>
    private void RefreshDecisionFeedback()
    {
        if (Connection.PlayRejectionSerial != _seenRejectionSerial)
        {
            _seenRejectionSerial = Connection.PlayRejectionSerial;
            _rejectionText = Connection.PlayRejection;
            _rejectionUntil = Time.GetTicksMsec() + RejectionMilliseconds;
            ShakeRejected();
        }
        if (_root == null || _snapshot == null) return;
        _inspector.SetAlert(ShowingRejection ? _rejectionText : "");
        RefreshStageStatus(_snapshot);
    }

    private void ClearRejection() { _rejectionUntil = 0; _rejectionText = ""; }

    private (string Headline, string Detail, Color Color, GameIcon Icon) StageStatus(DuelSnapshot s) =>
        DecisionStatus(s, Connection.Ready, Connection.PlayPending, IsAnimating, ShowingRejection);

    /// <summary>The status for a snapshot and connection state; separate from the live values so every branch can be tested offline.</summary>
    internal (string Headline, string Detail, Color Color, GameIcon Icon) DecisionStatus(DuelSnapshot s, bool ready, bool pending, bool animating, bool rejected)
    {
        var turn = $"TURNO {s.Turn:00}";
        if (_preparing) return ("PREPARACIÓN", PreparationCaption(), BoardArt.Gold, GameIcon.Cards);
        if (s.Winner is int winner)
            return (winner == -1 ? "EMPATE" : winner == s.MySeat ? "¡VICTORIA!" : "DERROTA", "DUELO TERMINADO",
                winner == -1 ? BoardArt.Gold : winner == s.MySeat ? ArenaPresentation.Mint : ArenaPresentation.Coral, GameIcon.Flag);
        var stage = StageAppearance.Current(s);
        var detail = stage != s.Phase ? $"{turn} · {PhaseName(s)}" : turn;
        var danger = RelickVisuals.State.Danger;
        if (!ready) return ("SIN CONEXIÓN", detail, danger, GameIcon.Alert);
        if (rejected) return ("JUGADA RECHAZADA", detail, danger, GameIcon.Alert);
        if (pending) return ("ENVIANDO…", detail, BoardArt.Ink, GameIcon.Reload);
        if (animating) return ("RESOLVIENDO…", detail, BoardArt.Gold, GameIcon.Reload);
        var ownTurn = s.CurrentPlayer == s.MySeat;
        if (s.DecisionPlayer == s.MySeat)
            return ownTurn ? ("TU TURNO", detail, ArenaPresentation.Mint, GameIcon.Bolt) : ("RESPONDE", detail, BoardArt.Gold, GameIcon.Hand);
        return ownTurn ? ("RIVAL RESPONDE", detail, ArenaPresentation.Coral, GameIcon.Hourglass) : ("TURNO RIVAL", detail, ArenaPresentation.Coral, GameIcon.Hourglass);
    }

    private void RefreshStageStatus(DuelSnapshot s)
    {
        if (_stageStatus == null || !IsInstanceValid(_stageStatus)) return;
        var (headline, detail, color, icon) = StageStatus(s);
        var key = $"{headline}|{detail}|{color.ToHtml()}|{icon}";
        if (key == _stageStatusKey) return;
        _stageStatusKey = key;
        _stageStatus.Text = headline;
        _stageStatus.AddThemeColorOverride("font_color", color);
        if (_stageDetail != null) { _stageDetail.Text = detail; _stageDetail.TooltipText = detail; }
        if (_stageStatusCell != null) _stageStatusCell.TooltipText = $"{headline} · {detail}";
        if (_stageStatusIcon != null) { _stageStatusIcon.Icon = icon; _stageStatusIcon.Color = color; _stageStatusIcon.QueueRedraw(); }
    }

    /// <summary>Marks hand cards that have at least one play the server authorized for this snapshot.</summary>
    private void RefreshPlayableHand()
    {
        var s = _snapshot;
        var playable = new HashSet<int>();
        if (s != null && !_preparing && s.Winner == null && !ChoosingRecycling && !ChoosingEffectCards)
            foreach (var option in s.Options)
                if (option.Move.Index >= 0 && option.Move.Kind is DuelMoveKind.Learn or DuelMoveKind.Attack or
                    DuelMoveKind.PlayCode or DuelMoveKind.ActivateArea or DuelMoveKind.Equip or DuelMoveKind.Exchange)
                    playable.Add(option.Move.Index);
        foreach (var card in _handCards)
        {
            var value = int.TryParse(card.Name.ToString().AsSpan(8), out var index) && playable.Contains(index);
            if (card.Playable == value) continue;
            card.Playable = value; card.QueueRedraw();
        }
    }

    internal IReadOnlyList<int> PlayableHandIndices => _handCards.Where(c => c.Playable)
        .Select(c => int.Parse(c.Name.ToString()[8..])).Order().ToArray();

    /// <summary>
    /// The attack table for the card being read: a skill by itself, or the selected Attack with the skill it
    /// would use. Super Attack changes the die, so it gets no table rather than a wrong one.
    /// </summary>
    private AttackOutlook? InspectorOutlook(CardDefinition? card, int? counters, int? ownerSeat)
    {
        var s = _snapshot;
        if (s == null || card == null) return null;
        RelikarioDefinition? Form(int? seat) => seat is int owner ? RelikarioCatalog.Find(s.Players[owner].RelikarioId) : null;
        int Multiplier(int? seat) => seat is int owner && s.Players[owner].StarEffects > 0 ? 1 << s.Players[owner].StarEffects : 1;
        if (card.Kind == CardKind.Skill)
            return AttackOutlook.For(card, counters ?? 0, Form(ownerSeat)?.AttackDieBonus ?? 0, Multiplier(ownerSeat),
                ownerSeat == null ? "Daño" : null, Form(ownerSeat)?.AttackDieBonusIncludingOne > 0);
        if (card.Kind == CardKind.Attack && card.Id != CardCatalog.SuperAttack.Id && _selectedSkill >= 0 &&
            SelectedAttacks().Any(entry => entry.Option.Move.Value == _selectedSkill))
        {
            var me = s.Players[s.MySeat];
            if (me.Skills.ElementAtOrDefault(_selectedSkill) is not { } id) return null;
            var skill = CardCatalog.Get(id);
            return AttackOutlook.For(skill, me.Counters.ElementAtOrDefault(_selectedSkill), Form(s.MySeat)?.AttackDieBonus ?? 0, Multiplier(s.MySeat),
                $"Atacando con {skill.Name}", Form(s.MySeat)?.AttackDieBonusIncludingOne > 0);
        }
        return null;
    }

    /// <summary>Who owns the skill being read, when the inspector knows it from the board.</summary>
    private int? InspectorSkillOwner(bool fromPreview)
    {
        var s = _snapshot!;
        if (fromPreview) return _inspectorPreviewOwner;
        if (_fieldSelection is { Zone: FieldZone.Skill } field) return field.Owner;
        if (_skillContext && _selectedSkill >= 0) return s.MySeat;
        return null;
    }

    /// <summary>Board skill tiles are named Skill{relative}_{slot}: 0 is the viewer, 1 the rival.</summary>
    private int? SkillTileOwner(Control control) =>
        control is SkillTile && control.Name.ToString() is var name && name.Length > 5 && name[5] is '0' or '1' && _snapshot is { } s
            ? name[5] == '0' ? s.MySeat : 1 - s.MySeat : null;
}
