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
    private ColorRect? _deckChoiceShade;
    private HFlowContainer _moves = null!;
    private Label _status = null!;
    private Label _selection = null!;
    private Label _combatMessage = null!;
    private readonly HashSet<int> _selected = [];
    private readonly List<Button> _moveButtons = [];
    private readonly RelikarioPortrait[] _portraits =
    [
        new() { Name = "RelikarioPortrait", PortraitHeight = 150, Inspectable = true, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill },
        new() { Name = "EnemyRelikarioPortrait", PortraitHeight = 150, Inspectable = true, FlipHorizontal = true, AccentColor = ArenaPresentation.Coral,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill }
    ];
    private readonly LifeMeter[] _life = [new(), new()];
    private readonly ActionCrystalPool[] _actionPools =
    [
        new() { Name = "OwnActionsCounter", Accent = ArenaPresentation.Mint },
        new() { Name = "RivalActionsCounter", Accent = ArenaPresentation.Coral }
    ];
    private readonly DiceFace _die = new() { Name = "AnimatedDice", CustomMinimumSize = new(260, 210) };
    private readonly DiceFace _secondDie = new() { Name = "SecondAnimatedDice", CustomMinimumSize = new(260, 210), Visible = false };
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
    private bool DiceBusy => _die.IsBusy || _secondDie.IsBusy;
    public bool IsAnimating => DiceBusy || _flights.Count > 0 || _flight != null || _openingInitiative != null || _handDeparture != null || _evolution.Visible;
    private bool Compact => ArenaViewport.Compact(this);
    private bool Short => ArenaViewport.Short(this);
    // The full board needs the desktop canvas; smaller windows keep the stacked layouts.
    private bool Desktop => ArenaViewport.Desktop(this);
    private ArenaBackground _background = null!;
    private readonly ImpactLayer _impacts = new() { Name = "ImpactLayer" };
    private readonly EvolutionReveal _evolution = new() { Name = "EvolutionReveal", Visible = false };
    private CardShowcase? _showcase;
    private Control? _combatCenter, _readerRail, _railReading, _historyFeed, _resolutionPanel;
    private bool _inlineHistory;
    private bool _fitArenaScroll;

    /// <summary>
    /// Gives the wide board its full height when it fits and only the height the rest of the
    /// screen leaves when it does not, so the hand, the inspector and the confirming button are
    /// never pushed below the window.
    /// </summary>
    private void FitArenaScroll()
    {
        if (!_fitArenaScroll || _root == null || _arenaScroll is not { } scroll || !IsInstanceValid(scroll) ||
            scroll.GetChildCount() == 0 || scroll.GetChild(0) is not Control arena) return;
        var available = _root.Size.Y > 0 ? _root.Size.Y : Size.Y;
        // The board lives directly in the root column: everything else is the root minimum without it.
        var rest = _root.GetCombinedMinimumSize().Y - scroll.GetCombinedMinimumSize().Y;
        var height = Mathf.Floor(Math.Clamp(arena.GetCombinedMinimumSize().Y, 0, Math.Max(0, available - rest)));
        if (!Mathf.IsEqualApprox(scroll.CustomMinimumSize.Y, height)) scroll.CustomMinimumSize = new(0, height);
    }
    // The desktop hand hangs over the bottom edge on purpose; only its upper band must be on screen.
    private bool _handLowered;
    internal bool HandFitsScreen => _hand == null || (_handLowered
        ? _hand.GetGlobalRect().Position.Y < Size.Y - 100 : _hand.GetGlobalRect().End.Y <= Size.Y + 1);
    private readonly List<Control> _pulsingTargets = [];
    // Board nodes looked up once per build; selections refresh them without walking the tree.
    private readonly ZoneCardButton?[] _equipmentZones = new ZoneCardButton?[2], _coverZones = new ZoneCardButton?[2];
    private readonly ExchangeTrack?[] _exchangeTracks = new ExchangeTrack?[2];
    private readonly List<OpponentHandCard> _opponentCards = [];
    private bool ChoosingRecycling => _snapshot is { Phase: Phase.RecycleChoice } s && s.DecisionPlayer == s.MySeat;
    private bool ChoosingDeckSearch => _snapshot is { Phase: Phase.DeckSearchChoice } s && s.DecisionPlayer == s.MySeat || Deciding == DecisionKind.ChooseCard;
    /// <summary>The kind of card decision this player must answer now, or null. Every card decision is drawn from it alone.</summary>
    private DecisionKind? Deciding => _snapshot is { Phase: Phase.Decision, Decision: { } decision } s && decision.Decider == s.MySeat ? decision.Kind : null;
    /// <summary>The gallery header of a card decision: the card and what to do, as the server describes them.</summary>
    private string? DecisionGalleryTitle => Deciding == DecisionKind.ChooseCard && _snapshot!.Decision is { } d ? $"{d.Title} · {d.Instruction}".ToUpperInvariant() : null;
    private bool ChoosingOsamuCards => Deciding == DecisionKind.ChooseCard && _snapshot!.Decision is { } decision &&
        (RelikarioCatalog.Find(decision.Source)?.Family == RelikarioCatalog.Osamu.Id || decision.Source == CardCatalog.TurboOverlap.Id);
    private bool ChoosingDeckGallery => ChoosingDeckSearch && (ChoosingOsamuCards || _snapshot!.Phase == Phase.DeckSearchChoice ||
        _snapshot.Decision?.Source == CardCatalog.KonamyCode.Id || _snapshot.Decision?.Source == CardCatalog.EquipmentRaid.Id);
    private bool ChoosingMemoryRecovery => _snapshot is { Phase: Phase.MemoryRecoveryChoice } s && s.DecisionPlayer == s.MySeat ||
        Deciding == DecisionKind.ChooseCard && _snapshot!.Options.Any(o => o.Move.Kind == DuelMoveKind.Choose && o.Owner != null);
    private bool ChoosingResetCards => _snapshot is { Phase: Phase.MindResetChoice } s && s.DecisionPlayer == s.MySeat && s.Options.Any(o => o.CardId != null);
    private bool ChoosingCorruption => _snapshot is { Phase: Phase.RelikiaChoice } s && s.DecisionPlayer == s.MySeat && s.Options.Any(o => o.Move.Kind == DuelMoveKind.Corrupt);
    private bool ChoosingEffectCards => ChoosingDeckSearch || ChoosingResetCards || ChoosingMemoryRecovery || ChoosingCorruption || _showReplayMemory;
    private DuelOption[] _deckSearchOptions = [];
    private IReadOnlyList<string?> SelectableCards => ChoosingRecycling
        ? _snapshot!.Players[_snapshot.MySeat].Memory : ChoosingEffectCards
            ? _deckSearchOptions.Select(OptionCardId).ToArray() : _handDeparture?.Hand ?? _snapshot!.Players[_snapshot.MySeat].Hand;

    public override void _Ready()
    {
        Theme = RelickVisuals.Controls();
        Name = "OnlineArena"; SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = new ArenaBackground { Name = "ArenaBackdrop" }; AddChild(background);
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _background = background;
        BindRelikarioPreviews();
        foreach (var pool in _actionPools)
        {
            pool.Hide();
            AddChild(pool);
        }
        _diceOverlay = new CenterContainer { Name = "DiceRollOverlay", Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_diceOverlay); _diceOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel = ArenaPresentation.Panel(_diceOverlay, "111f2c", new("927b54"));
        var column = new VBoxContainer(); panel.AddChild(column);
        // The rolling die needs no title; the caption below says whose roll it is and what modifies it.
        var dice = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.AddChild(dice);
        dice.AddChild(_die);
        dice.AddChild(_secondDie);
        _diceCaption = ArenaPresentation.Text("", 13); _diceCaption.AutowrapMode = TextServer.AutowrapMode.WordSmart; column.AddChild(_diceCaption);
        _die.RollFinished += PlayPendingPortraits;
        _secondDie.RollFinished += PlayPendingPortraits;
        BuildInitiativeOverlay();
        BuildStageBanner();
        _cardActions.AddThemeConstantOverride("separation", 4);
        AddChild(_cardActions);
        AddChild(_responseChain);
        AddChild(_impacts); _impacts.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_evolution); _evolution.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var die in _initiativeDice.Append(_die)) die.RollStarted += () => PlaySound(SoundCue.DiceRoll);
        Resized += QueueLayout;
        // Refusals from before this arena existed belong to another screen.
        _seenRejectionSerial = Connection?.PlayRejectionSerial ?? 0;
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
        PresentQueuedPlay();
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
            PresentDuelSnapshot(snapshot);
        if (_root == null) return;
        if (_snapshot != null) UpdateComboResponseToggle(_snapshot);
        if (_status.Text != Connection.Message) _status.Text = Connection.Message;
        PresentQueuedStageAnnouncement();
        UpdateFlights(delta);
        if (_evolution.Visible) _evolution.AdvancePlayback(delta, EvolutionAnchor(_evolution.RelativePlayer));
        UpdateDiceOverlay();
        UpdateResolutionPresentation();
        UpdateRelikiaPreview();
        UpdateEnabled(); RefreshRuntimeGuidance();
        PositionCardActions(); RefreshDecisionFeedback();
        PositionResponseChain();
        UpdateResultPresentation();
        PresentPlaymat();
        foreach (var target in _pulsingTargets)
            if (IsInstanceValid(target)) target.QueueRedraw();
    }

    private void PresentDuelSnapshot(DuelSnapshot snapshot)
    {
        var previous = _snapshot;
        var openingFlights = IsLiveOpening(snapshot) ? CaptureMulliganOrigins(snapshot) : null;
        var animate = !_preparing && !_needsBaseline && previous != null && previous.Revision + 1 == snapshot.Revision;
        var origins = CaptureOrigins(snapshot, previous);
        var recipe = animate ? CaptureTrapRecipe(previous!, snapshot) : null;
        var placement = animate ? CaptureExchangePlacement(snapshot, previous!, origins) ??
            CaptureSkillLesson(snapshot, previous!) ?? CaptureEquipmentInstall(snapshot, previous!, recipe) : null;
        if (animate && IsAnimating) { ResetAnimations(preserveStageAnnouncements: true); _die.Value = snapshot.Die; }
        if (animate && snapshot.Visuals.FirstOrDefault(cue => cue.Kind is DuelVisualKind.Die or DuelVisualKind.DieAdjustment) is { } nextDie)
        {
            // A modifier turns from the value of the pending die, which may be the second die of a pair.
            if (nextDie.Kind == DuelVisualKind.DieAdjustment) _die.Value = previous!.Die;
            _secondDie.Value = 0;
        }
        if (animate) PresentEvolution(previous!, snapshot);
        _snapshot = snapshot; _preparing = false; _choosingOpeningSkill = false; ResetInteraction();
        _handDeparture = animate && placement == null ? CaptureHandDeparture(previous!, snapshot) : null;
        if (!animate)
        {
            _pendingTrapRecipe = null;
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
        // Render just rebuilt the board; its controls get their places at the end of this frame, so the
        // played-code flash and the Trap summon read them on the next one (see PresentQueuedPlay).
        if (animate) QueuePlayedCard(previous!, snapshot, recipe);
        SyncResponseChain(snapshot, animate);
        QueueStageAnnouncement(previous, snapshot, animate || openingFlights != null);
    }

    private void PresentPlaymat()
    {
        var s = _snapshot;
        if (s == null || _combatCenter == null || _readerRail == null || !IsInstanceValid(_combatCenter) || Size.Y <= 0)
        {
            _background.Present(-1, 1, 0);
            return;
        }
        var active = _preparing || s.Winner != null ? 0 : s.CurrentPlayer == s.MySeat ? 1 : -1;
        var origin = GlobalPosition;
        _background.Present((_combatCenter.GetGlobalRect().GetCenter().Y - origin.Y) / Size.Y,
            (_readerRail.GlobalPosition.X - origin.X) / Size.X, active);
    }

    private void Render(bool animateActions = false, DuelSnapshot? previous = null)
    {
        _relikiaMenu = null;
        _inspectorPreview = null; _relikarioPreview = null;
        ClearStageStatus();
        _fieldPress = null;
        var handPosition = _hand?.ScrollHorizontal ?? 0;
        var handVerticalPosition = _hand?.ScrollVertical ?? 0;
        var boardPosition = _arenaScroll?.ScrollVertical ?? 0;
        foreach (var node in _portraits.Cast<Node>().Concat(_life).Concat(_actionPools)) node.GetParent()?.RemoveChild(node);
        if (_root != null) { RemoveChild(_root); _root.QueueFree(); }
        if (_deckChoiceShade != null) { RemoveChild(_deckChoiceShade); _deckChoiceShade.QueueFree(); _deckChoiceShade = null; }
        if (_decisionPopup != null) { RemoveChild(_decisionPopup); _decisionPopup.QueueFree(); _decisionPopup = null; }
        _root = new VBoxContainer { Name = "DuelInterface" }; AddChild(_root);
        var desktop = Desktop;
        _pulsingTargets.Clear(); _showcase = null; _combatCenter = null; _readerRail = null;
        _railReading = null; _historyFeed = null; _resolutionPanel = null; _inlineHistory = false; _relikiaPreview = null;
        foreach (var meter in _life) meter.Stacked = desktop;
        var choiceGallery = _choosingOpeningSkill || ChoosingRecycling || ChoosingEffectCards;
        VBoxContainer? choicePanel = null;
        _fitArenaScroll = false;
        _handLowered = false;
        _root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ArenaViewport.ApplyInsets(_root, desktop ? 0 : Compact ? 10 : 24, desktop ? 0 : Compact ? 8 : 16);
        _root.AddThemeConstantOverride("separation", Short ? 4 : 8);
        var s = _snapshot!;
        GetNodeOrNull<MusicDirector>("/root/MusicDirector")?.PresentDuel(RoomCode, s);
        RefreshEffectCardOptions();
        var deckChoices = ChoosingDeckGallery;
        var sections = MemorySections();
        var deckColumns = 0;
        if (_selectedSkill >= s.Players[s.MySeat].Skills.Length) _selectedSkill = -1;
        var stage = StageAppearance.Current(s);
        var resultCaption = s.Winner == -1 ? "EMPATE" : s.Winner == s.MySeat ? "¡VICTORIA!" : "DERROTA";
        var stageDetail = _preparing ? PreparationCaption() : s.Winner != null ? resultCaption :
            $"TURNO {s.Turn:00} · {(s.CurrentPlayer == s.MySeat ? "TÚ" : "RIVAL")}";
        if (!_preparing && s.Winner == null && stage != s.Phase)
            stageDetail += $" · {PhaseName(s)} · {(s.DecisionPlayer == s.MySeat ? "DECIDES TÚ" : "DECIDE EL RIVAL")}";
        if (Short) stageDetail += $" · TÚ {s.Players[s.MySeat].Health}/{s.Players[s.MySeat].MaxHealth} · RIVAL {s.Players[1 - s.MySeat].Health}/{s.Players[1 - s.MySeat].MaxHealth}";
        var rechargeActions = animateActions && previous != null && previous.Turn != s.Turn;
        var relativePlayers = new[] { s.Players[s.MySeat], s.Players[1 - s.MySeat] };
        bool Active(int relative) => !_preparing && s.CurrentPlayer == (relative == 0 ? s.MySeat : 1 - s.MySeat) && s.Winner == null;
        ArenaPlayerActions Prepare(int relative)
        {
            var seat = relative == 0 ? s.MySeat : 1 - s.MySeat;
            var player = relativePlayers[relative];
            var crystals = _actionPools[relative];
            var actionsLabel = relative == 0 ? "Tus acciones" : "Acciones del rival";
            crystals.Present(player.Actions, $"{actionsLabel} · {player.Name}", animateActions, rechargeActions);
            crystals.Visible = !_preparing && s.Phase != Phase.Preparation;
            _portraits[relative].SetRelikario(player.RelikarioId);
            _portraits[relative].SetHealth(player.Health);
            return new ArenaPlayerActions
            {
                Memory = corrupt => ShowCards($"{(corrupt ? "Memoria corrupta" : "Memoria")} · {player.Name}", corrupt ? null : player.Memory,
                    player.CorruptedCount, relikarioOf: _ => player.RelikarioId),
                Attached = () => SelectFieldPile(seat, FieldZone.Attached),
                Area = () => SelectField(seat, FieldZone.Area, -1),
                Equipment = () => SelectEquipment(seat),
                LightEquipment = () => SelectFieldPile(seat, FieldZone.LightEquipment),
                EquipmentCovers = () => SelectFieldPile(seat, FieldZone.EquipmentCover),
                Skill = slot =>
                {
                    if (slot >= player.Skills.Length) return;
                    if (relative == 1) SelectField(seat, FieldZone.Skill, slot);
                    else SelectSkill(slot);
                },
                InspectSkill = slot => ShowCards("Habilidad", [player.Skills[slot]]),
                CanLearn = relative == 0 && !_preparing && s.Winner == null && s.Options.Any(o => o.Move.Kind == DuelMoveKind.Learn),
                Evolve = () => SendKind(DuelMoveKind.Evolve), CanEvolve = s.Options.Any(o => o.Move.Kind == DuelMoveKind.Evolve) && !Connection.PlayPending && !IsAnimating,
                Relikia = relative == 0 && !_preparing && s.Winner == null ? OpenRelikiaMenu : null
            };
        }
        var combatMessage = _preparing ? PreparationMessage() : s.Winner != null ? resultCaption : s.LastCombat;
        var railColumns = Short ? Size.X >= 820 ? 2 : 1 : 0;
        VBoxContainer handColumn;
        if (desktop)
        {
            var columns = new HBoxContainer { Name = "ArenaColumns", SizeFlagsVertical = SizeFlags.ExpandFill };
            columns.AddThemeConstantOverride("separation", 0); _root.AddChild(columns);
            // One playmat: every zone has the same card size and the hands hang over its edges.
            var canvas = new BoardCanvas { Name = "DuelArena", SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipContents = true };
            columns.AddChild(canvas);
            _arenaScroll = null;
            canvas.GuiInput += ClearContextOnFieldClick;

            var brand = new HBoxContainer { Name = "RivalStrip", MouseFilter = MouseFilterEnum.Ignore };
            brand.AddThemeConstantOverride("separation", 10);
            // The status cell of the phase bar and the inspector already say what the connection is doing;
            // the room code stays on the logo's tooltip.
            var logo = RelickVisuals.Logo(true); logo.MouseFilter = MouseFilterEnum.Pass; logo.TooltipText = "Sala " + RoomCode;
            brand.AddChild(logo);
            _status = ArenaPresentation.Text(Connection.Message, 12, BoardArt.Muted); _status.Visible = false;
            brand.AddChild(_status);
            canvas.Place(brand, new Rect2(24, 14, 300, 42), BoardAnchor.Top);

            ArenaPresentation.Mat(canvas, true, Active(1));
            ArenaPresentation.Mat(canvas, false, Active(0));
            void Side(int relative)
            {
                var seat = relative == 0 ? s.MySeat : 1 - s.MySeat;
                var actions = Prepare(relative);
                ArenaPresentation.Field(canvas, relativePlayers[relative], relative, relative == 1, Active(relative), _preparing,
                    _selectedSkill, _portraits[relative], _life[relative], _actionPools[relative], actions,
                    () => SelectField(seat, FieldZone.Bonus, -1), slot => SelectField(seat, FieldZone.Exchange, slot));
            }
            Side(1);
            Side(0);

            // The rival's hand hangs from the top edge at full card size and shows the same band as ours.
            const float rivalBand = ArenaPresentation.DesktopHandVisible;
            const float rivalFanHeight = BoardCanvas.CardHeight + 60;
            var rivalHand = new HBoxContainer { Name = "RivalHandRow", Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
            rivalHand.AddThemeConstantOverride("separation", 8);
            canvas.Place(rivalHand, new Rect2(340, rivalBand - rivalFanHeight, BoardCanvas.DesignWidth - 680, rivalFanHeight), BoardAnchor.Top);
            BuildOpponentHand(rivalHand, new Vector2(BoardCanvas.CardWidth, BoardCanvas.CardHeight));

            // Turn, stage and the advance button live on the divider, in the colour of whose turn it is.
            var turnAccent = _preparing || s.Winner != null ? BoardArt.Gold : s.CurrentPlayer == s.MySeat ? ArenaPresentation.Mint : ArenaPresentation.Coral;
            var barDetail = _preparing ? PreparationCaption() : s.Winner != null ? resultCaption : $"TURNO {s.Turn:00}";
            if (!_preparing && s.Winner == null && stage != s.Phase) barDetail += " · " + PhaseName(s);
            var (lane, bar) = ArenaPresentation.PhaseBar(canvas, stage, barDetail, turnAccent);
            _combatCenter = lane;
            BuildAdvanceControl(bar);
            BindStageStatus(bar);

            // Choices among cards outside the hand open over the board; the hand itself stays low.
            if (choiceGallery)
            {
                var shade = new ColorRect { Name = "ChoiceShade", Color = new Color(0.01f, 0.02f, 0.05f, 0.74f) };
                shade.GuiInput += ClearContextOnFieldClick;
                canvas.Place(shade, new Rect2(-3000, -3000, 7440, 7024), BoardAnchor.Top);
                var chooser = new PanelContainer { Name = "ChoicePanel" };
                chooser.AddThemeStyleboxOverride("panel", RelickVisuals.Surface(BoardArt.Gold));
                var panelSize = new Vector2(BoardCanvas.DesignWidth - 260, 690);
                if (deckChoices) (deckColumns, panelSize) = FitDeckChoicePanel(panelSize, sections, desktop: true);
                canvas.Place(chooser, new Rect2((BoardCanvas.DesignWidth - panelSize.X) / 2, BoardCanvas.Seam - panelSize.Y / 2, panelSize.X, panelSize.Y));
                // A container can grow during its first measurement and must return to the board's fixed rectangle.
                chooser.MinimumSizeChanged += () => chooser.SetDeferred(Control.PropertyName.Size, panelSize);
                handColumn = new VBoxContainer { Name = "HandColumn" };
                handColumn.AddThemeConstantOverride("separation", 10); chooser.AddChild(handColumn);
            }
            else
            {
                _handLowered = true;
                handColumn = new VBoxContainer { Name = "HandColumn", MouseFilter = MouseFilterEnum.Ignore };
                canvas.Place(handColumn, new Rect2(0, BoardCanvas.DesignHeight - ArenaPresentation.DesktopHandVisible - 8 - ArenaPresentation.DesktopHandHeadroom,
                    BoardCanvas.DesignWidth, 330), BoardAnchor.Bottom);
            }

            // Reading rail: menu, what is resolving, the card under the pointer or, with none, the history.
            var railMargin = new MarginContainer { Name = "ReaderRailMargin" };
            foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" }) railMargin.AddThemeConstantOverride(side, 12);
            columns.AddChild(railMargin);
            var rail = new PanelContainer { Name = "ReaderRail", CustomMinimumSize = new(310, 0) };
            rail.AddThemeStyleboxOverride("panel", RelickVisuals.Surface());
            railMargin.AddChild(rail); _readerRail = rail;
            var inspectorColumn = new VBoxContainer { Name = "InspectorColumn" };
            inspectorColumn.AddThemeConstantOverride("separation", 8); rail.AddChild(inspectorColumn);
            var railHeader = new HBoxContainer { Name = "RailHeader" };
            railHeader.AddThemeConstantOverride("separation", 6); inspectorColumn.AddChild(railHeader);
            BuildComboResponseToggle(railHeader);
            if (_comboResponsesToggle != null) _comboResponsesToggle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            else railHeader.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
            BuildArenaMenu(railHeader);
            BuildRailResolution(inspectorColumn, combatMessage);
            _showcase = new CardShowcase { Name = "CardShowcase", CustomMinimumSize = new(0, 330), Visible = false };
            inspectorColumn.AddChild(_showcase);
            var reading = new VBoxContainer { Name = "RailReading" };
            inspectorColumn.AddChild(reading); _railReading = reading;
            BuildInspector(reading);
            BuildHistoryFeed(inspectorColumn);
        }
        else
        {
            var header = new HBoxContainer { Name = "ArenaHeader" }; _root.AddChild(header);
            var brand = RelickVisuals.Logo(Compact);
            var brandColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            brandColumn.AddThemeConstantOverride("separation", 0); brandColumn.AddChild(brand); header.AddChild(brandColumn);
            _status = ArenaPresentation.Text(Connection.Message, 12, BoardArt.Muted); _status.TooltipText = "Sala " + RoomCode;
            _status.Visible = !Short; brandColumn.AddChild(_status);
            ArenaPresentation.StageIndicator(Short ? _root : header, stage, stageDetail, Short);
            BuildComboResponseToggle(header);
            BuildAdvanceControl(header);
            BuildArenaMenu(header);
            if (Compact && !Short) ArenaPresentation.Scoreline(_root, [s.Players[s.MySeat], s.Players[1 - s.MySeat]]);
            var body = ArenaPresentation.Body(_root, Short && Size.X >= 700);
            var boardCompact = Compact || Short;
            var arena = ArenaPresentation.Arena(body.Board, boardCompact);
            _arenaScroll = arena.GetParent() as ScrollContainer;
            _fitArenaScroll = !boardCompact;
            Control field = _arenaScroll ?? (Control)arena;
            field.GuiInput += ClearContextOnFieldClick;
            if (_arenaScroll != null) _arenaScroll.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, boardPosition);
            void Player(int relative)
            {
                var actions = Prepare(relative);
                ArenaPresentation.Player(arena, relativePlayers[relative], relative, relative == 1, Active(relative),
                    _selectedSkill, _portraits[relative], _life[relative], _actionPools[relative], actions, boardCompact, Short && Size.X < 820);
            }
            Player(0);
            var combat = ArenaPresentation.Combat(arena, _preparing, s.Winner != null, combatMessage, relativePlayers, BuildOpponentHand,
                relative => SelectField(relative == 0 ? s.MySeat : 1 - s.MySeat, FieldZone.Bonus, -1),
                relative => SelectField(relative == 0 ? s.MySeat : 1 - s.MySeat, FieldZone.Area, -1),
                (relative, slot) => SelectField(relative == 0 ? s.MySeat : 1 - s.MySeat, FieldZone.Exchange, slot), boardCompact, BuildResolution);
            _combatMessage = (Label)combat.FindChild("CombatMessage", true, false);
            Player(1);
            foreach (var panel in arena.GetChildren().OfType<PanelContainer>()) panel.MouseFilter = MouseFilterEnum.Pass;
            var interaction = new HBoxContainer
            {
                Name = "CardInteractionArea",
                SizeFlagsVertical = Short || choiceGallery ? SizeFlags.ExpandFill : SizeFlags.Fill,
                SizeFlagsStretchRatio = 0.55f
            };
            interaction.AddThemeConstantOverride("separation", Short ? 4 : 12);
            if (choiceGallery)
            {
                choicePanel = BuildDeckChoicePanel(header, sections, out deckColumns);
                choicePanel.AddChild(interaction);
            }
            else body.Controls.AddChild(interaction);
            handColumn = new VBoxContainer { Name = "HandColumn",
                SizeFlagsHorizontal = Short && !choiceGallery ? SizeFlags.ShrinkEnd : SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill };
            var inspectorColumn = new VBoxContainer
            {
                Name = "InspectorColumn",
                CustomMinimumSize = new(choiceGallery ? 0 : Short ? 270 : 480, 0),
                SizeFlagsHorizontal = Short || choiceGallery ? SizeFlags.ExpandFill : SizeFlags.Fill
            };
            if (choiceGallery)
            {
                var reading = new ScrollContainer
                {
                    Name = "ChoiceInspectorScroll", CustomMinimumSize = new(DeckChoiceInspectorWidth, 0),
                    HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
                };
                interaction.AddChild(handColumn); interaction.AddChild(reading);
                reading.AddChild(inspectorColumn);
            }
            else if (Short) { interaction.AddChild(inspectorColumn); interaction.AddChild(handColumn); }
            else { interaction.AddChild(handColumn); interaction.AddChild(inspectorColumn); }
            BuildInspector(inspectorColumn);
            if (_fitArenaScroll)
            {
                // A card being read can change how much room is left for the scrollable board.
                if (!choiceGallery) interaction.MinimumSizeChanged += FitArenaScroll;
                arena.MinimumSizeChanged += FitArenaScroll;
                _root.Resized += FitArenaScroll;
            }
        }
        var galleryTitle = ChoosingRecycling ? "TU MEMORIA · ELIGE PARA RECICLAR"
            : _showReplayMemory ? "TU MEMORIA · ELIGE UN CÓDIGO"
            : ChoosingCorruption ? "MEMORIAS · ELIGE PARA CORROMPER"
            : ChoosingMemoryRecovery ? "DISQUETE · ELIGE DE CUALQUIER MEMORIA"
            : ChoosingResetCards ? "RESETEAR LA MENTE · ELIGE UNA CARTA"
            : DecisionGalleryTitle ?? (ChoosingDeckSearch ? "TU MAZO · ELIGE UNA NO-HABILIDAD" : null);
        _hand = _choosingOpeningSkill ? ArenaPresentation.InitialSkills(handColumn, _availableInitialSkills, _initialSkillCandidate,
            SelectInitialSkillCandidate, Short, choiceGallery ? 0 : railColumns, desktop) : ArenaPresentation.Hand(handColumn, SelectableCards.Select(id => id!).ToArray(), index => _selected.Contains(index), SelectCard, _preparing, Short || choiceGallery && Compact,
                galleryTitle, choiceGallery ? 0 : railColumns,
                useFan: !ChoosingRecycling && !ChoosingEffectCards, desktop: desktop,
                sections: sections, ownerOf: ChoosingEffectCards || ChoosingRecycling ? null : index => HandCardOwner(s.MySeat, index),
                viewerSeat: s.MySeat, stackColumns: desktop ? 7 : 0,
                relikario: s.Players[s.MySeat].RelikarioId, fittedColumns: deckColumns);
        if (choiceGallery)
        {
            // The chooser scrolls on its own and carries the decision that confirms it.
            _hand.SizeFlagsVertical = SizeFlags.ExpandFill;
            _hand.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
            if (!_choosingOpeningSkill && (deckChoices || !desktop))
            {
                _hand.CustomMinimumSize = Vector2.Zero;
                _hand.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
                _hand.FollowFocus = true;
            }
        }
        BuildDecisionPrompt(choiceGallery ? choicePanel ?? handColumn : null);
        BuildResultPanel();
        if (choiceGallery && (ChoosingMemoryRecovery || deckChoices))
        {
            _movesScroll.CustomMinimumSize = new(0, 60);
            _moves.Alignment = FlowContainer.AlignmentMode.End;
        }
        _hand.SetDeferred(ScrollContainer.PropertyName.ScrollHorizontal, handPosition);
        _hand.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, handVerticalPosition);
        _handCards.Clear(); _handCards.AddRange(_hand.FindChildren("HandCard*", "Button", true, false).OfType<HandCard>());
        _initialSkillCards.Clear(); _initialSkillCards.AddRange(_hand.FindChildren("InitialSkillCard*", "Button", true, false).OfType<HandCard>());
        _ownSkills.Clear(); _ownSkills.AddRange(_root.FindChildren("Skill0_*", "Button", true, false).OfType<SkillTile>());
        _evolveButton = _root.FindChild("EvolveRelikario", true, false) as Button;
        for (var relative = 0; relative < 2; relative++)
        {
            _equipmentZones[relative] = _root.FindChild("EquipmentZone" + relative, true, false) as ZoneCardButton;
            _coverZones[relative] = _root.FindChild("EquipmentCoverZone" + relative, true, false) as ZoneCardButton;
            _exchangeTracks[relative] = _root.FindChild("BonusTrack" + relative, true, false) as ExchangeTrack;
        }
        _opponentCards.Clear(); _opponentCards.AddRange(_root.FindChildren("OpponentHandCard*", "Button", true, false).OfType<OpponentHandCard>());
        PlaceZahiras(s);
        MoveChild(_cardActions, GetChildCount() - 1);
        MoveChild(_diceOverlay, GetChildCount() - 1);
        if (_evolution.Visible) MoveChild(_evolution, GetChildCount() - 1);
        BuildMoves(); BindInspectorPreviews();
        FitArenaScroll();
        UpdateResultPresentation();
        BoardBuildCount++;
    }

    private float DeckChoiceInspectorWidth => Compact ? 240 : 270;

    private (int Columns, Vector2 Size) FitDeckChoicePanel(Vector2 available, IReadOnlyList<ArenaPresentation.HandSection>? sections, bool desktop)
    {
        var counts = sections?.Select(section => section.Indices.Count).ToArray() ?? [_choosingOpeningSkill ? _availableInitialSkills.Length : SelectableCards.Count];
        var card = _choosingOpeningSkill ? new Vector2(177, 133) * (Short ? 0.6f : 1.3f) :
            new Vector2(126, 184) * ArenaPresentation.DeckChoiceCardScale(desktop, Short || Compact);
        const float gap = ArenaPresentation.DeckChoiceGap;
        // Include the surface padding, a vertical scrollbar and room for the side inspector when shown.
        var horizontalChrome = 24 + 16 + (desktop ? 0 : DeckChoiceInspectorWidth + (Short ? 4 : 12));
        var maxColumns = Math.Max(1, (int)((available.X - horizontalChrome + gap) / (card.X + gap)));
        var columns = Math.Clamp(counts.Max(), 1, maxColumns);
        var width = Math.Clamp(columns * card.X + (columns - 1) * gap + horizontalChrome,
            Math.Min(available.X, desktop ? 340 : horizontalChrome + 180), available.X);

        // Each source starts on its own row. Reserve room for the wrapped title, source labels and fixed footer.
        var rows = counts.Select(count => Math.Max(1, (count + columns - 1) / columns)).ToArray();
        var galleryHeight = rows.Sum(rowCount => rowCount * card.Y + (rowCount - 1) * gap);
        if (sections != null) galleryHeight += sections.Count * 31 + (sections.Count - 1) * 16;
        var height = Math.Min(available.Y, galleryHeight + (desktop ? 188 : 156));
        return (columns, new(width, height));
    }

    private VBoxContainer BuildDeckChoicePanel(Control header, IReadOnlyList<ArenaPresentation.HandSection>? sections, out int columns)
    {
        // A sibling of the board keeps the chooser independent of its minimum size.
        var shade = new ColorRect { Name = "ChoiceShade", Color = new Color(0.01f, 0.02f, 0.05f, 0.74f) };
        shade.GuiInput += ClearContextOnFieldClick;
        _deckChoiceShade = shade;
        AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        // Keep the arena menu reachable while a gallery is open, including during preparation.
        shade.OffsetTop = _root!.OffsetTop + header.GetCombinedMinimumSize().Y + 6;
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        shade.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ArenaViewport.ApplyInsets(center, Compact ? 10 : 24, 12);
        var available = Size + new Vector2(center.OffsetRight - center.OffsetLeft, center.OffsetBottom - center.OffsetTop - shade.OffsetTop);
        var layout = FitDeckChoicePanel(available, sections, desktop: false);
        columns = layout.Columns;
        var panel = new PanelContainer { Name = "ChoicePanel", CustomMinimumSize = layout.Size };
        panel.AddThemeStyleboxOverride("panel", RelickVisuals.Surface(BoardArt.Gold));
        center.AddChild(panel);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        panel.AddChild(content);
        return content;
    }

    private void BuildOpponentHand(VBoxContainer parent) => BuildOpponentHand((BoxContainer)parent);

    private void BuildOpponentHand(BoxContainer parent, Vector2? boardFace = null)
    {
        var opponent = _snapshot!.Players[1 - _snapshot.MySeat];
        var hand = opponent.Hand;
        var strip = parent is HBoxContainer;
        // The board fan carries its own count beside its cards; the stacked layouts keep it first in the strip.
        if (boardFace == null) ArenaPresentation.HandCounter(parent, opponent.HandCount, enemy: true);
        var face = boardFace ?? (strip ? new Vector2(43, 60.8f) : new Vector2(54, 76.4f));
        Control row;
        if (boardFace != null)
        {
            // The rival's hand mirrors ours: same card size, tilt and visible band, hanging from the top edge.
            row = new HandFan { Name = "OpponentHandFan", Mirrored = true, Headroom = 0, MaximumAngle = 3, Spread = true,
                VisibleBand = ArenaPresentation.DesktopHandVisible,
                SizeFlagsVertical = SizeFlags.Fill };
        }
        else
        {
            var scroll = new ScrollContainer { Name = "OpponentHandScroll", CustomMinimumSize = new(0, strip ? 62 : 90), VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
                HorizontalScrollMode = strip && hand.Length <= 16 ? ScrollContainer.ScrollMode.Disabled : ScrollContainer.ScrollMode.Auto,
                SizeFlagsHorizontal = strip && hand.Length > 16 ? SizeFlags.ExpandFill : SizeFlags.Fill };
            // Held cards overlap on the board strip, as a hand does.
            var box = new HBoxContainer(); box.AddThemeConstantOverride("separation", strip ? -10 : 4); scroll.AddChild(box); parent.AddChild(scroll);
            row = box;
        }
        for (var i = 0; i < hand.Length; i++)
        {
            var id = hand[i];
            var owner = HandCardOwner(1 - _snapshot.MySeat, i);
            var card = new OpponentHandCard { Name = $"OpponentHandCard{i}", PublicCard = id == null ? null : CardCatalog.Get(id), Sleeve = opponent.Sleeve,
                OriginalOwner = owner, RelikarioId = opponent.RelikarioId, ViewerSeat = _snapshot.MySeat, FaceSize = face };
            var index = i;
            card.Pressed += () => SelectField(1 - _snapshot!.MySeat, FieldZone.Hand, index); row.AddChild(card);
        }
        if (boardFace == null) return;
        // Only the band below the top edge is on screen: the count sits in it, past the last card.
        ((HandFan)row).Counter = ArenaPresentation.HandCounter(row, opponent.HandCount, enemy: true);
        // The fan lays its cards out on entering the tree, so it joins the board once it holds them.
        parent.AddChild(row);
    }

    private CardOwner? HandCardOwner(int holder, int index)
    {
        var s = _snapshot!;
        var owners = holder == s.MySeat && _handDeparture != null ? _handDeparture.Owners : s.Players[holder].HandOwners;
        return owners is { } seats && index >= 0 && index < seats.Length && seats[index] is int owner
            ? new CardOwner(owner, s.Players[owner].Name, s.Players[owner].RelikarioId) : null;
    }

    private string HandArtworkRelikario(int holder, int index) =>
        HandCardOwner(holder, index)?.RelikarioId ?? _snapshot!.Players[holder].RelikarioId;

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
        if (!ChoosingEffectCards && !ChoosingRecycling && _snapshot!.Options.Any(o => o.Move.Kind == DuelMoveKind.Destroy && o.Move.Target == _snapshot.MySeat && o.Move.Value == (int)FieldZone.Hand ||
            o.Move.Kind == DuelMoveKind.Choose && o.Field is { Zone: FieldZone.Hand } own && own.Owner == _snapshot.MySeat))
        { SelectField(_snapshot.MySeat, FieldZone.Hand, -1); return; }
        _choosingAttackSkill = false;
        _fieldSelection = null;
        _skillContext = _skillContext && _snapshot!.Options.Any(o => o.Move.Kind == DuelMoveKind.DestinyDefense && o.Move.Index == _selectedSkill);
        var defense = _snapshot!.Options.Any(o => o.RequiredDiscards > 0);
        if (!defense && !_selected.Contains(index)) _selected.Clear();
        if (!_selected.Add(index)) _selected.Remove(index);
        RefreshSelection();
    }
    private void RefreshSelection()
    {
        _inspectorPreview = null; _relikarioPreview = null;
        foreach (var card in _handCards)
        { card.Chosen = _selected.Contains(int.Parse(card.Name.ToString()[8..])); card.QueueRedraw(); }
        BuildMoves();
    }
    private void BuildMoves()
    {
        foreach (var node in new[] { (Node)_moves, _cardActions })
            foreach (var child in node.GetChildren()) { node.RemoveChild(child); child.QueueFree(); }
        _cardActionAnchor = null;
        _cardActions.Hide();
        _moveButtons.Clear(); _actionMenus.Clear();
        if (_advanceButton != null) _moveButtons.Add(_advanceButton);
        if (_preparing)
        {
            BuildPreparationMoves(); _decisionHint = _selection.Text;
            RefreshDecisionPrompt(); RefreshInspector(); return;
        }
        RefreshBoardTargets(); BuildContextMoves(); RefreshPlayableHand();
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
        ClearRejection(); RememberPlayAnchor(state.Options[index]);
        if (state.Options[index].Move.Kind == DuelMoveKind.Concede) ConfirmConcede(index, state);
        else Connection.Play(index, state.Options[index].RequiredDiscards > 0 ? _selected : null, state.Revision, RoomCode, state);
    }

    private void ConfirmConcede(int index, DuelSnapshot state)
    {
        if (GetChildren().OfType<ConfirmationDialog>().Any(d => d.Name == "ConcedeDialog")) return;
        var dialog = new ConfirmationDialog { Name = "ConcedeDialog", Title = "Abandonar duelo",
            DialogText = "Tu rival ganará esta partida. ¿Quieres abandonar?", OkButtonText = "Abandonar", CancelButtonText = "Cancelar" };
        dialog.Confirmed += () => { Connection.Play(index, expectedRevision: state.Revision, expectedRoom: RoomCode, expectedSnapshot: state); dialog.QueueFree(); };
        dialog.Canceled += () => dialog.QueueFree(); AddChild(dialog);
        foreach (var button in new[] { dialog.GetOkButton(), dialog.GetCancelButton() }) button.CustomMinimumSize = new(132, 48);
        // A short question needs only its own size, centred over the duel.
        dialog.PopupCentered();
    }

    private void ShowCards(string title, IReadOnlyList<string?>? ids, int hiddenCount = 0, Action<int>? select = null,
        Func<int, string?>? relikarioOf = null)
    {
        if (GetChildren().OfType<Window>().Any(w => w.Visible)) return;
        var dialog = new AcceptDialog { Name = "CardCollectionDialog", Title = title, OkButtonText = "Volver al duelo" };
        if (ids is { Count: > 0 })
        {
            var view = new CardCollectionView { Cards = ids.ToArray(), ShortScreen = Short, RelikarioOf = relikarioOf,
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

    private static string PhaseName(DuelSnapshot s) => s.Phase == Phase.Decision && s.Decision is { } d ? d.Title.ToUpperInvariant() : PhaseName(s.Phase);
    private static string PhaseName(Phase phase) => phase switch
    {
        Phase.Preparation => "PREPARACIÓN", Phase.Draw => "ROBO", Phase.Combat => "COMBATE", Phase.Actions => "ACCIONES", Phase.Discard => "DESCARTE", Phase.Final => "DESCARTE FINAL", Phase.Finished => "FINAL",
        Phase.AttackRerollChoice or Phase.DieRerollChoice => "DECISIÓN DEL DADO", Phase.DefenseChoice => "DEFENSA",
        Phase.TrapCraftChoice => "TRAMPA",
        Phase.ZahiraTarget => "ZAHIRA FLAIR",
        Phase.CodeResponse or Phase.ErrorResponse or Phase.EffectDamageChoice => "RESPUESTA", Phase.GoldenTotemChoice or Phase.GoldenTotemResolving => "TÓTEM DE ORO", Phase.TotemChoice => "TÓTEM DE SALVACIÓN", Phase.DestructionResponse => "PROTEGER CARTA", Phase.RecycleChoice => "RECICLAJE", Phase.DeckSearchChoice => "BUSCAR EN EL MAZO", Phase.MemoryRecoveryChoice => "DISQUETE", Phase.TurnBuffDiscard => "DESCARTAR COSTE", Phase.MindResetChoice => "RESETEAR LA MENTE", Phase.SniperChoice => "MIRA DE SNIPER", Phase.BlueEggDrawChoice => "ROBO OPCIONAL", _ => "RESOLVER EFECTO"
    };
}
