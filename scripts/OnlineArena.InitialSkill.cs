using Godot;
using Relick.Core;
using Relick.Network.Types;

namespace Relick;

public partial class OnlineArena
{
    private string[] _availableInitialSkills = [];
    private string? _initialSkillCandidate;
    private bool _initialSkillConnected, _initialSkillPending;
    private string? _initialSkillViewKey;
    internal Button? InitialSkillConfirmButton { get; private set; }
    internal bool IsChoosingInitialSkill => _choosingOpeningSkill;
    private bool CanChooseInitialSkill => _initialSkillConnected && !_initialSkillPending;

    internal void PresentInitialSkillChoice(RoomInfo room, InitialSkillInfo info, bool connected, bool pending, PreparationInfo? preparation = null)
    {
        preparation ??= Connection.Preparation;
        var relikarios = RelikariosForRoom(room);
        var key = $"{room.Code}:{room.MySeat}:{room.PlayerOne}:{room.PlayerTwo}:{preparation?.HandCountOne}:{preparation?.HandCountTwo}:{preparation?.DeckCountOne}:{preparation?.DeckCountTwo}:{relikarios?.RelikarioOne}:{relikarios?.RelikarioTwo}:" + string.Join(',', info.Options);
        if (_choosingOpeningSkill && _initialSkillViewKey == key &&
            _initialSkillConnected == connected && _initialSkillPending == pending) return;
        _initialSkillViewKey = key; _availableInitialSkills = info.Options.ToArray();
        if (_initialSkillCandidate != null && !_availableInitialSkills.Contains(_initialSkillCandidate)) _initialSkillCandidate = null;
        _initialSkillConnected = connected; _initialSkillPending = pending;
        _choosingOpeningSkill = true; _preparing = true; _openingCards = []; _openingInfo = null;
        PreparationConfirmButton = null;
        _snapshot = new DuelSnapshot { MySeat = room.MySeat, DecisionPlayer = room.MySeat, Phase = Phase.Preparation,
            Players = [PreparationPlayer(room, preparation, relikarios, 0), PreparationPlayer(room, preparation, relikarios, 1)] };
        var opponent = 1 - room.MySeat;
        if (_snapshot.Players[opponent].Hand.Length == OpeningHandSize) _openingHandsPresented[opponent] = true;
        ResetAnimations(); Render();
    }

    private void SelectInitialSkillCandidate(string id)
    {
        if (!CanChooseInitialSkill || !_availableInitialSkills.Contains(id)) return;
        _inspectorPreview = null; _relikarioPreview = null;
        _initialSkillCandidate = id;
        foreach (var card in _initialSkillCards) { card.Chosen = card.Card.Id == id; card.QueueRedraw(); }
        if (InitialSkillConfirmButton != null) InitialSkillConfirmButton.Disabled = !CanChooseInitialSkill;
        RefreshInspector();
    }

    private void BuildInitialSkillMoves()
    {
        PreparationConfirmButton = null;
        _selection.Text = "";
        InitialSkillConfirmButton = ArenaPresentation.Button(_moves, _initialSkillPending ? "Eligiendo habilidad…" : "Elegir habilidad", () =>
        {
            if (CanChooseInitialSkill && _initialSkillCandidate != null) Connection.ChooseInitialSkill(_initialSkillCandidate);
        });
        InitialSkillConfirmButton.Name = "ConfirmInitialSkill";
        _moves.Alignment = FlowContainer.AlignmentMode.Center;
        RelickVisuals.Primary(InitialSkillConfirmButton);
        InitialSkillConfirmButton.SetMeta("discards", 0); _moveButtons.Add(InitialSkillConfirmButton);
        if (Compact) InitialSkillConfirmButton.CustomMinimumSize = new(Short ? 280 : 0, 48);
        _lastDisabled = null; UpdateEnabled();
        InitialSkillConfirmButton.Disabled |= _initialSkillCandidate == null;
    }
}
