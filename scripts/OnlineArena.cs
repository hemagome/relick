using Godot;
using Relick.Core;
using Relick.Network;

namespace Relick;

// Network adapter: the shared presentation only sees the caller's authorized snapshot.
public partial class OnlineArena : Control
{
    public LobbyConnection Connection { get; set; } = null!;
    public string RoomCode { get; set; } = "";
    public event Action? ReturnToLobbyRequested;
    internal bool HasFinishedResult => _snapshot?.Winner != null;
    private VBoxContainer? _root;
    private HFlowContainer _moves = null!;
    private Label _status = null!;
    private Label _selection = null!;
    private Label _combatMessage = null!;
    private readonly HashSet<int> _selected = [];
    private readonly List<Button> _moveButtons = [];
    private readonly YunoPortrait[] _portraits =
    [
        new() { Name = "YunoPortrait", PortraitHeight = 150, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill },
        new() { Name = "EnemyYunoPortrait", PortraitHeight = 150, FlipHorizontal = true, AccentColor = ArenaPresentation.Coral,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill }
    ];
    private readonly LifeMeter[] _life = [new(), new()];
    private readonly ActionCrystalPool[] _actionPools =
    [
        new() { Name = "OwnActionsCounter", Accent = ArenaPresentation.Mint },
        new() { Name = "RivalActionsCounter", Accent = ArenaPresentation.Coral }
    ];
    private readonly DiceFace _die = new() { Name = "AnimatedDice", CustomMinimumSize = new(260, 210) };
    private CenterContainer _diceOverlay = null!;
    private Label _diceCaption = null!;
    private DuelSnapshot? _snapshot;
    private bool _needsBaseline = true;
    private ulong _connectionEpoch;
    private bool _layoutQueued;
    private int _selectedSkill = -1;
    private bool? _lastDisabled;
    private readonly List<HandCard> _handCards = [];
    private readonly List<HandCard> _initialSkillCards = [];
    private readonly List<SkillTile> _ownSkills = [];
    private Button? _evolveButton;
    private ScrollContainer? _hand;
    private ScrollContainer? _arenaScroll;
    internal int BoardBuildCount { get; private set; }
    internal int PlayedVisualRevisions { get; private set; }
    public bool IsAnimating => _die.IsBusy || _flights.Count > 0 || _flight != null || _openingInitiative != null;
    private bool Compact => ArenaViewport.Compact(this);
    private bool Short => ArenaViewport.Short(this);
    private bool ChoosingRecycling => _snapshot is { Phase: Phase.RecycleChoice } s && s.DecisionPlayer == s.MySeat;
    private bool ChoosingDeckSearch => _snapshot is { Phase: Phase.DeckSearchChoice } s && s.DecisionPlayer == s.MySeat;
    private bool ChoosingMemoryRecovery => _snapshot is { Phase: Phase.MemoryRecoveryChoice } s && s.DecisionPlayer == s.MySeat;
    private bool ChoosingResetCards => _snapshot is { Phase: Phase.MindResetChoice } s && s.DecisionPlayer == s.MySeat && s.Options.Any(o => o.CardId != null);
    private bool ChoosingCorruption => _snapshot is { Phase: Phase.RelikiaChoice } s && s.DecisionPlayer == s.MySeat && s.Options.Any(o => o.Move.Kind == DuelMoveKind.Corrupt);
    private bool ChoosingEffectCards => ChoosingDeckSearch || ChoosingResetCards || ChoosingMemoryRecovery || ChoosingCorruption || _showReplayMemory;
    private DuelOption[] _deckSearchOptions = [];
    private IReadOnlyList<string?> SelectableCards => ChoosingRecycling
        ? _snapshot!.Players[_snapshot.MySeat].Memory : ChoosingEffectCards
            ? _deckSearchOptions.Select(OptionCardId).ToArray() : _snapshot!.Players[_snapshot.MySeat].Hand;

    public override void _Ready()
    {
        Theme = RelickVisuals.Controls();
        Name = "OnlineArena"; SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = new ArenaBackground { Name = "ArenaBackdrop" }; AddChild(background);
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var pool in _actionPools)
        {
            pool.Hide();
            AddChild(pool);
        }
        _diceOverlay = new CenterContainer { Name = "DiceRollOverlay", Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_diceOverlay); _diceOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel = ArenaPresentation.Panel(_diceOverlay, "111f2c", new("927b54"));
        var column = new VBoxContainer(); panel.AddChild(column);
        column.AddChild(ArenaPresentation.Text("LANZANDO EL DADO", 12, BoardArt.Gold)); column.AddChild(_die);
        _diceCaption = ArenaPresentation.Text("", 13); _diceCaption.AutowrapMode = TextServer.AutowrapMode.WordSmart; column.AddChild(_diceCaption);
        _die.RollFinished += PlayPendingPortraits;
        BuildInitiativeOverlay();
        BuildStageBanner();
        foreach (var die in _initiativeDice.Append(_die)) die.RollStarted += () => PlaySound(SoundCue.DiceRoll);
        Resized += QueueLayout;
    }

    private void PlaySound(SoundCue cue) => GetNodeOrNull<SoundDirector>("/root/SoundDirector")?.Play(cue);

    private void QueueLayout()
    {
        if (_layoutQueued) return;
        _layoutQueued = true; CallDeferred(nameof(ApplyLayout));
    }
    private void ApplyLayout() { _layoutQueued = false; if (_snapshot != null) { ResetAnimations(); Render(); } }

    public override void _Process(double delta)
    {
        if (_connectionEpoch != Connection.Epoch)
        {
            _connectionEpoch = Connection.Epoch; _needsBaseline = true; ResetAnimations();
        }
        if (!Connection.Ready)
        {
            if (!_needsBaseline) { _needsBaseline = true; ResetAnimations(); }
        }
        var snapshot = Connection.Room?.Code == RoomCode ? Connection.Duel : null;
        snapshot ??= Connection.LastResultRoom == RoomCode ? Connection.LastResult : null;
        if (snapshot != null && _snapshot != null && snapshot.Revision == _snapshot.Revision &&
            snapshot.AutoPassCombos != _snapshot.AutoPassCombos)
        {
            // Private preference updates do not advance the public duel revision.
            ApplyPrivateComboPreference(snapshot);
        }
        if (snapshot == null) UpdatePreparation();
        if (Connection.Ready && snapshot != null && _openingInitiative == null && (_needsBaseline || _preparing || _snapshot?.Revision != snapshot.Revision))
        {
            var previous = _snapshot;
            var fromPreparation = _preparing;
            var openingFlights = fromPreparation && !_needsBaseline ? CaptureMulliganOrigins(snapshot) : null;
            var animate = !_needsBaseline && previous != null && previous.Revision + 1 == snapshot.Revision;
            var origins = CaptureOrigins(snapshot, previous);
            var placement = animate ? CaptureSkillLesson(snapshot, previous!) ?? CaptureEquipmentInstall(snapshot, previous!) : null;
            if (animate && IsAnimating) { ResetAnimations(); _die.Value = snapshot.Die; }
            _snapshot = snapshot; _preparing = false; _choosingOpeningSkill = false; ResetInteraction();
            if (!animate)
            {
                ResetAnimations(); _die.Value = snapshot.Die;
                for (var relative = 0; relative < 2; relative++)
                {
                    var p = snapshot.Players[relative == 0 ? snapshot.MySeat : 1 - snapshot.MySeat];
                    _life[relative].SetHealth(p.Health, p.MaxHealth, false);
                }
            }
            _needsBaseline = false; Render(animate || openingFlights != null, previous);
            if (openingFlights != null) AnimateOpening(snapshot, openingFlights, Connection.OpeningResult);
            else if (animate) Animate(snapshot.Visuals, origins, placement);
            QueueStageAnnouncement(previous, snapshot, animate || openingFlights != null);
        }
        if (_root == null) return;
        if (_snapshot != null) UpdateComboResponseToggle(_snapshot);
        if (_status.Text != Connection.Message) _status.Text = Connection.Message;
        UpdateFlights(delta);
        PresentQueuedStageAnnouncement();
        _diceOverlay.Visible = _die.IsPresented;
        _diceOverlay.Modulate = new Color(1, 1, 1, _die.PresentationOpacity);
        UpdateResolutionPresentation();
        if (_die.IsPresented) _diceCaption.Text = _die.RollCaption;
        UpdateEnabled(); RefreshRuntimeGuidance();
        UpdateResultPresentation();
    }

    private void Render(bool animateActions = false, DuelSnapshot? previous = null)
    {
        var handPosition = _hand?.ScrollHorizontal ?? 0;
        var handVerticalPosition = _hand?.ScrollVertical ?? 0;
        var boardPosition = _arenaScroll?.ScrollVertical ?? 0;
        foreach (var node in _portraits.Cast<Node>().Concat(_life).Concat(_actionPools)) node.GetParent()?.RemoveChild(node);
        if (_root != null) { RemoveChild(_root); _root.QueueFree(); }
        _root = new VBoxContainer { Name = "DuelInterface" }; AddChild(_root);
        _root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); ArenaViewport.ApplyInsets(_root, Compact ? 10 : 24, Compact ? 8 : 16);
        _root.AddThemeConstantOverride("separation", Short ? 4 : 8);
        var s = _snapshot!;
        GetNodeOrNull<MusicDirector>("/root/MusicDirector")?.PresentDuel(RoomCode, s);
        RefreshEffectCardOptions();
        if (_selectedSkill >= s.Players[s.MySeat].Skills.Length) _selectedSkill = -1;
        var header = new HBoxContainer(); _root.AddChild(header);
        var brand = RelickVisuals.Logo(Compact);
        var brandColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        brandColumn.AddThemeConstantOverride("separation", 0); brandColumn.AddChild(brand); header.AddChild(brandColumn);
        _status = ArenaPresentation.Text(Connection.Message, 12, BoardArt.Muted); _status.TooltipText = "Sala " + RoomCode;
        _status.Visible = !Short; brandColumn.AddChild(_status);
        var stage = StageAppearance.Current(s);
        var resultCaption = s.Winner == -1 ? "EMPATE" : s.Winner == s.MySeat ? "¡VICTORIA!" : "DERROTA";
        var stageDetail = _preparing ? PreparationCaption() : s.Winner != null ? resultCaption :
            $"TURNO {s.Turn:00} · {(s.CurrentPlayer == s.MySeat ? "TÚ" : "RIVAL")}";
        if (!_preparing && s.Winner == null && stage != s.Phase)
            stageDetail += $" · {PhaseName(s.Phase)} · {(s.DecisionPlayer == s.MySeat ? "DECIDES TÚ" : "DECIDE EL RIVAL")}";
        if (Short) stageDetail += $" · TÚ {s.Players[s.MySeat].Health}/{s.Players[s.MySeat].MaxHealth} · RIVAL {s.Players[1 - s.MySeat].Health}/{s.Players[1 - s.MySeat].MaxHealth}";
        ArenaPresentation.StageIndicator(Short ? _root : header, stage, stageDetail, Short);
        var rechargeActions = animateActions && previous != null && previous.Turn != s.Turn;
        BuildComboResponseToggle(header); BuildArenaMenu(header);
        if (Compact && !Short) ArenaPresentation.Scoreline(_root, [s.Players[s.MySeat], s.Players[1 - s.MySeat]]);
        var body = ArenaPresentation.Body(_root, Short && Size.X >= 700);
        var boardCompact = Compact || Short;
        var arena = ArenaPresentation.Arena(body.Board, boardCompact);
        _arenaScroll = arena.GetParent() as ScrollContainer;
        if (_arenaScroll != null) _arenaScroll.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, boardPosition);
        var relativePlayers = new[] { s.Players[s.MySeat], s.Players[1 - s.MySeat] };
        void Player(int relative)
        {
            var seat = relative == 0 ? s.MySeat : 1 - s.MySeat;
            var player = relativePlayers[relative];
            var crystals = _actionPools[relative];
            var actionsLabel = relative == 0 ? "Tus acciones" : "Acciones del rival";
            crystals.Present(player.Actions, $"{actionsLabel} · {player.Name}", animateActions, rechargeActions);
            crystals.Visible = !_preparing && s.Phase != Phase.Preparation;
            _portraits[relative].SetHealth(player.Health);
            ArenaPresentation.Player(arena, player, relative, relative == 1, !_preparing && s.CurrentPlayer == seat && s.Winner == null,
                _selectedSkill, _portraits[relative], _life[relative], crystals, new ArenaPlayerActions
                {
                    Memory = corrupt => ShowCards($"{(corrupt ? "Memoria corrupta" : "Memoria")} · {player.Name}", corrupt ? null : player.Memory, player.CorruptedCount),
                    Attached = () => SelectFieldPile(seat, FieldZone.Attached),
                    Equipment = () => SelectEquipment(seat),
                    EquipmentCovers = () => SelectFieldPile(seat, FieldZone.EquipmentCover),
                    Skill = slot =>
                    {
                        if (slot >= player.Skills.Length) return;
                        if (relative == 1) SelectField(seat, FieldZone.Skill, slot);
                        else SelectSkill(slot);
                    },
                    InspectSkill = slot => ShowCards("Habilidad", [player.Skills[slot]]),
                    Evolve = () => SendKind(DuelMoveKind.Evolve), CanEvolve = s.Options.Any(o => o.Move.Kind == DuelMoveKind.Evolve) && !Connection.PlayPending && !IsAnimating
                }, boardCompact, Short && Size.X < 820);
        }
        Player(0);
        var combatMessage = _preparing ? PreparationMessage() : s.Winner != null ? resultCaption : s.LastCombat;
        var combat = ArenaPresentation.Combat(arena, _preparing, s.Winner != null, combatMessage, relativePlayers, BuildOpponentHand,
            relative => SelectField(relative == 0 ? s.MySeat : 1 - s.MySeat, FieldZone.Bonus, -1),
            (relative, slot) => SelectField(relative == 0 ? s.MySeat : 1 - s.MySeat, FieldZone.Exchange, slot), boardCompact, BuildResolution);
        _combatMessage = (Label)combat.FindChild("CombatMessage", true, false);
        Player(1);
        var railColumns = Short ? Size.X >= 820 ? 2 : 1 : 0;
        BoxContainer interaction = new HBoxContainer();
        interaction.Name = "CardInteractionArea"; interaction.AddThemeConstantOverride("separation", Short ? 4 : 12);
        interaction.SizeFlagsVertical = Short ? SizeFlags.ExpandFill : SizeFlags.Fill;
        interaction.SizeFlagsStretchRatio = 0.55f;
        body.Controls.AddChild(interaction);
        var handColumn = new VBoxContainer { Name = "HandColumn",
            SizeFlagsHorizontal = Short ? SizeFlags.ShrinkEnd : SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill };
        var inspectorColumn = new VBoxContainer { Name = "InspectorColumn", CustomMinimumSize = new(Short ? 270 : 480, 0),
            SizeFlagsHorizontal = Short ? SizeFlags.ExpandFill : SizeFlags.Fill };
        if (Short) { interaction.AddChild(inspectorColumn); interaction.AddChild(handColumn); }
        else { interaction.AddChild(handColumn); interaction.AddChild(inspectorColumn); }
        BuildActionArea(inspectorColumn);
        BuildResultPanel(inspectorColumn);
        if (Short)
        {
            _turnControls.GetParent().RemoveChild(_turnControls); header.AddChild(_turnControls);
            header.MoveChild(_turnControls, header.GetChildCount() - 2);
        }
        _hand = _choosingOpeningSkill ? ArenaPresentation.InitialSkills(handColumn, _availableInitialSkills, _initialSkillCandidate,
            SelectInitialSkillCandidate, Short, railColumns) : ArenaPresentation.Hand(handColumn, SelectableCards.Select(id => id!).ToArray(), index => _selected.Contains(index), SelectCard, _preparing, Short,
                ChoosingRecycling ? "TU MEMORIA · ELIGE PARA RECICLAR" : _showReplayMemory ? "TU MEMORIA · ELIGE UN CÓDIGO" : ChoosingCorruption ? "MEMORIAS · ELIGE PARA CORROMPER" : ChoosingMemoryRecovery ? "DISQUETE · ELIGE DE CUALQUIER MEMORIA" : ChoosingResetCards ? "RESETEAR LA MENTE · ELIGE UNA CARTA" : ChoosingDeckSearch ? "TU MAZO · ELIGE UNA NO-HABILIDAD" : null, railColumns,
                useFan: !ChoosingRecycling && !ChoosingEffectCards);
        _hand.SetDeferred(ScrollContainer.PropertyName.ScrollHorizontal, handPosition);
        _hand.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, handVerticalPosition);
        _handCards.Clear(); _handCards.AddRange(_hand.FindChildren("HandCard*", "Button", true, false).OfType<HandCard>());
        _initialSkillCards.Clear(); _initialSkillCards.AddRange(_hand.FindChildren("InitialSkillCard*", "Button", true, false).OfType<HandCard>());
        _ownSkills.Clear(); _ownSkills.AddRange(_root.FindChildren("Skill0_*", "Button", true, false).OfType<SkillTile>());
        _evolveButton = _root.FindChild("EvolveRelikario", true, false) as Button;
        BuildMoves(); MoveChild(_diceOverlay, GetChildCount() - 1);
        UpdateResultPresentation();
        BoardBuildCount++;
    }

    private void BuildOpponentHand(VBoxContainer parent)
    {
        var opponent = _snapshot!.Players[1 - _snapshot.MySeat];
        var hand = opponent.Hand;
        ArenaPresentation.HandCounter(parent, opponent.HandCount, enemy: true);
        var scroll = new ScrollContainer { Name = "OpponentHandScroll", CustomMinimumSize = new(0, 90), VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 4); scroll.AddChild(row); parent.AddChild(scroll);
        for (var i = 0; i < hand.Length; i++)
        {
            var id = hand[i];
            var card = new OpponentHandCard { Name = $"OpponentHandCard{i}", PublicCard = id == null ? null : CardCatalog.Get(id), FaceSize = new(54, 76.4f) };
            var index = i;
            card.Pressed += () => SelectField(1 - _snapshot!.MySeat, FieldZone.Hand, index); row.AddChild(card);
        }
    }

    private void SelectCard(int index)
    {
        if (_preparing)
        {
            if (!CanChooseOpening || IsAnimating || index < 0 || index >= _openingCards.Length) return;
            var id = _openingCards[index].Id;
            _openingInspection = _openingCards[index].Definition;
            if (!_openingReturns.Add(id)) _openingReturns.Remove(id);
            SyncOpeningSelection(); RefreshSelection(); return;
        }
        if (IsAnimating || index < 0 || index >= SelectableCards.Count) return;
        if (!ChoosingEffectCards && !ChoosingRecycling && _snapshot!.Options.Any(o => o.Move.Kind == DuelMoveKind.Destroy && o.Move.Target == _snapshot.MySeat && o.Move.Value == (int)FieldZone.Hand))
        { SelectField(_snapshot.MySeat, FieldZone.Hand, -1); return; }
        _fieldSelection = null;
        _skillContext = _skillContext && _snapshot!.Options.Any(o => o.Move.Kind == DuelMoveKind.DestinyDefense && o.Move.Index == _selectedSkill);
        var defense = _snapshot!.Options.Any(o => o.RequiredDiscards > 0);
        if (!defense && !_selected.Contains(index)) _selected.Clear();
        if (!_selected.Add(index)) _selected.Remove(index);
        RefreshSelection();
    }
    private void RefreshSelection()
    {
        foreach (var card in _handCards)
        { card.Chosen = _selected.Contains(int.Parse(card.Name.ToString()[8..])); card.QueueRedraw(); }
        BuildMoves();
    }
    private void BuildMoves()
    {
        foreach (var node in new[] { (Node)_moves, _turnControls })
            foreach (var child in node.GetChildren()) { node.RemoveChild(child); child.QueueFree(); }
        _moveButtons.Clear(); _actionMenus.Clear(); _advanceButton = null;
        if (_preparing)
        {
            _turnControls.Visible = false;
            BuildPreparationMoves(); RefreshInspector(); return;
        }
        RefreshBoardTargets(); BuildTurnControls(); BuildContextMoves();
        _lastDisabled = null; UpdateEnabled();
    }

    private void UpdateEnabled()
    {
        var disabled = _preparing ? !(_choosingOpeningSkill ? CanChooseInitialSkill : CanChooseOpening) || IsAnimating : !Connection.Ready || Connection.PlayPending || IsAnimating;
        RefreshAdvanceInvitation(disabled);
        if (_lastDisabled == disabled) return;
        _lastDisabled = disabled;
        foreach (var button in _moveButtons)
        {
            var gold = (bool)button.GetMeta("golden_totem", false);
            button.Disabled = disabled || ((int)button.GetMeta("discards") > 0 || gold) && (int)button.GetMeta("discards") != _selected.Count ||
                gold && _selected.Any(i => _snapshot!.Players[_snapshot.MySeat].Hand[i] != CardCatalog.Totem.Id);
        }
        foreach (var menu in _actionMenus)
        {
            menu.Disabled = disabled;
            var popup = menu.GetPopup();
            for (var i = 0; i < popup.ItemCount; i++)
            {
                var option = popup.GetItemId(i);
                var source = (bool)menu.GetMeta("choose_source", false);
                popup.SetItemDisabled(i, disabled || !source && option < _snapshot!.Options.Count &&
                    _snapshot.Options[option].RequiredDiscards > 0 && _snapshot.Options[option].RequiredDiscards != _selected.Count);
            }
        }
        if (_arenaMenu != null)
        {
            var popup = _arenaMenu.GetPopup(); var abandon = popup.GetItemIndex(1);
            if (abandon >= 0) popup.SetItemDisabled(abandon, disabled);
        }
        RefreshAdvanceInvitation(disabled);
        if (_evolveButton is Button evolve)
            evolve.Disabled = disabled || !_snapshot!.Options.Any(o => o.Move.Kind == DuelMoveKind.Evolve);
        if (_choosingOpeningSkill && InitialSkillConfirmButton != null) InitialSkillConfirmButton.Disabled |= _initialSkillCandidate == null;
        if (_hand != null) foreach (var card in _handCards) card.Disabled = disabled;
        if (_choosingOpeningSkill && _hand != null)
            foreach (var card in _initialSkillCards) card.Disabled = disabled;
    }
    private void SendKind(DuelMoveKind kind)
    {
        var index = _snapshot!.Options.FindIndex(o => o.Move.Kind == kind);
        if (index >= 0 && !IsAnimating) PlayOption(index, _snapshot);
    }
    private void PlayOption(int index, DuelSnapshot state)
    {
        if (!Connection.Ready || Connection.PlayPending || IsAnimating) return;
        if (state.Options[index].Move.Kind == DuelMoveKind.Concede)
        {
            var dialog = new ConfirmationDialog { Title = "Abandonar duelo", DialogText = "Tu rival ganará esta partida. ¿Quieres abandonar?" };
            dialog.Confirmed += () => { Connection.Play(index, expectedRevision: state.Revision, expectedRoom: RoomCode, expectedSnapshot: state); dialog.QueueFree(); };
            dialog.Canceled += () => dialog.QueueFree(); AddChild(dialog); ArenaViewport.FitDialog(dialog, Size);
        }
        else Connection.Play(index, state.Options[index].RequiredDiscards > 0 ? _selected : null, state.Revision, RoomCode, state);
    }

    private void ShowCards(string title, IReadOnlyList<string?>? ids, int hiddenCount = 0, Action<int>? select = null)
    {
        if (GetChildren().OfType<Window>().Any(w => w.Visible)) return;
        var dialog = new AcceptDialog { Name = "CardCollectionDialog", Title = title, OkButtonText = "Volver al duelo" };
        if (ids is { Count: > 0 })
        {
            var view = new CardCollectionView { Cards = ids.ToArray(), ShortScreen = Short,
                InspectorWidth = Short ? 260 : Math.Clamp(Size.X * 0.3f, 320, 380),
                CustomMinimumSize = new(Math.Min(960, Size.X - 72), Math.Min(480, Size.Y - 140)) };
            if (select != null) view.CardPressed += index => { select(index); dialog.QueueFree(); };
            dialog.AddChild(view);
        }
        else
        {
            var message = ArenaPresentation.Text(ids == null ? $"{hiddenCount} cartas corruptas boca abajo. Su identidad permanece oculta." : "No hay cartas en esta zona.");
            message.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            message.CustomMinimumSize = new(Math.Min(620, Size.X - 72), Math.Min(120, Size.Y - 140));
            dialog.AddChild(message);
        }
        AddChild(dialog); // The shared inspector is ready before its initial card is presented.
        dialog.GetOkButton().CustomMinimumSize = new(0, 48);
        dialog.Confirmed += () => dialog.QueueFree(); dialog.Canceled += () => dialog.QueueFree();
        ArenaViewport.FitDialog(dialog, Size);
    }

    private static string PhaseName(Phase phase) => phase switch
    {
        Phase.Preparation => "PREPARACIÓN", Phase.Draw => "ROBO", Phase.Combat => "COMBATE", Phase.Actions => "ACCIONES", Phase.Discard => "DESCARTE", Phase.Final => "DESCARTE FINAL", Phase.Finished => "FINAL",
        Phase.AttackRerollChoice or Phase.DieRerollChoice => "DECISIÓN DEL DADO", Phase.DefenseChoice => "DEFENSA",
        Phase.CodeResponse or Phase.ErrorResponse or Phase.EffectDamageChoice => "RESPUESTA", Phase.GoldenTotemChoice or Phase.GoldenTotemResolving => "TÓTEM DE ORO", Phase.TotemChoice => "TÓTEM DE SALVACIÓN", Phase.DestructionResponse => "PROTEGER CARTA", Phase.RecycleChoice => "RECICLAJE", Phase.DeckSearchChoice => "BUSCAR EN EL MAZO", Phase.MemoryRecoveryChoice => "DISQUETE", Phase.TurnBuffDiscard => "DESCARTAR COSTE", Phase.MindResetChoice => "RESETEAR LA MENTE", Phase.SniperChoice => "MIRA DE SNIPER", _ => "RESOLVER EFECTO"
    };
}
