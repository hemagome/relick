using Godot;
using Relick.Core;
using Relick.Network;

namespace Relick;

// Network adapter: the shared presentation only sees the caller's authorized snapshot.
public partial class OnlineArena : Control
{
    public LobbyConnection Connection { get; set; } = null!;
    public string RoomCode { get; set; } = "";
    public event Action? ReconnectRequested;
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
    private readonly DiceFace _die = new() { Name = "AnimatedDice", CustomMinimumSize = new(260, 210) };
    private CenterContainer _diceOverlay = null!;
    private Label _diceCaption = null!;
    private DuelSnapshot? _snapshot;
    private bool _needsBaseline = true;
    private ulong _connectionEpoch;
    private bool _layoutQueued;
    private int _selectedSkill;
    private bool? _lastDisabled;
    private readonly List<HandCard> _handCards = [];
    private readonly List<HandCard> _initialSkillCards = [];
    private readonly List<SkillTile> _ownSkills = [];
    private Button? _evolveButton;
    private ScrollContainer? _hand;
    private ScrollContainer? _arenaScroll;
    internal int BoardBuildCount { get; private set; }
    internal int PlayedVisualRevisions { get; private set; }
    public bool IsAnimating => _die.IsBusy || _flights.Count > 0 || _flight != null;
    private bool Compact => ArenaViewport.Compact(this);
    private bool Short => ArenaViewport.Short(this);
    private bool ChoosingRecycling => _snapshot is { Phase: Phase.RecycleChoice } s && s.DecisionPlayer == s.MySeat;
    private bool ChoosingDeckSearch => _snapshot is { Phase: Phase.DeckSearchChoice } s && s.DecisionPlayer == s.MySeat;
    private bool ChoosingResetCards => _snapshot is { Phase: Phase.MindResetChoice } s && s.DecisionPlayer == s.MySeat && s.Options.Any(o => o.CardId != null);
    private bool ChoosingEffectCards => ChoosingDeckSearch || ChoosingResetCards;
    private DuelOption[] _deckSearchOptions = [];
    private IReadOnlyList<string?> SelectableCards => ChoosingRecycling
        ? _snapshot!.Players[_snapshot.MySeat].Memory : ChoosingEffectCards
            ? _deckSearchOptions.Select(o => o.CardId).ToArray() : _snapshot!.Players[_snapshot.MySeat].Hand;

    public override void _Ready()
    {
        Name = "OnlineArena"; SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = new ArenaBackground { Name = "ArenaBackdrop" }; AddChild(background);
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _diceOverlay = new CenterContainer { Name = "DiceRollOverlay", Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_diceOverlay); _diceOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel = ArenaPresentation.Panel(_diceOverlay, "111f2c", new("927b54"));
        var column = new VBoxContainer(); panel.AddChild(column);
        column.AddChild(ArenaPresentation.Text("LANZANDO EL DADO", 12, BoardArt.Gold)); column.AddChild(_die);
        _diceCaption = ArenaPresentation.Text("", 13); _diceCaption.AutowrapMode = TextServer.AutowrapMode.WordSmart; column.AddChild(_diceCaption);
        _die.RollFinished += PlayPendingPortraits;
        Resized += QueueLayout;
    }

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
        var snapshot = Connection.Duel;
        if (snapshot == null) UpdatePreparation();
        if (Connection.Ready && snapshot != null && (_needsBaseline || _preparing || _snapshot?.Revision != snapshot.Revision))
        {
            var previous = _snapshot;
            var fromPreparation = _preparing;
            var openingFlights = fromPreparation && !_needsBaseline ? CaptureMulliganOrigins(snapshot) : null;
            var animate = !_needsBaseline && previous != null && previous.Revision + 1 == snapshot.Revision;
            var origins = CaptureOrigins(snapshot, previous);
            if (animate && IsAnimating) { ResetAnimations(); _die.Value = snapshot.Die; }
            _snapshot = snapshot; _preparing = false; _choosingOpeningSkill = false; _selected.Clear();
            if (!animate)
            {
                ResetAnimations(); _die.Value = snapshot.Die;
                for (var relative = 0; relative < 2; relative++)
                {
                    var p = snapshot.Players[relative == 0 ? snapshot.MySeat : 1 - snapshot.MySeat];
                    _life[relative].SetHealth(p.Health, p.MaxHealth, false);
                }
            }
            _needsBaseline = false; Render();
            if (openingFlights != null) AnimateOpening(snapshot, openingFlights);
            else if (animate) Animate(snapshot.Visuals, origins);
        }
        if (_root == null) return;
        if (_status.Text != Connection.Message) _status.Text = Connection.Message;
        UpdateFlights(delta);
        _diceOverlay.Visible = _die.IsPresented;
        _diceOverlay.Modulate = new Color(1, 1, 1, _die.PresentationOpacity);
        _combatMessage.Visible = !_die.IsBusy && _combatMessage.Text.Length > 0;
        if (_die.IsPresented) _diceCaption.Text = _die.RollCaption;
        UpdateEnabled();
    }

    private void Render()
    {
        var handPosition = _hand?.ScrollHorizontal ?? 0;
        var boardPosition = _arenaScroll?.ScrollVertical ?? 0;
        foreach (var node in _portraits.Cast<Node>().Concat(_life)) node.GetParent()?.RemoveChild(node);
        if (_root != null) { RemoveChild(_root); _root.QueueFree(); }
        _root = new VBoxContainer { Name = "DuelInterface" }; AddChild(_root);
        _root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); ArenaViewport.ApplyInsets(_root, Compact ? 10 : 24, Compact ? 8 : 16);
        _root.AddThemeConstantOverride("separation", Short ? 4 : 8);
        var s = _snapshot!;
        _deckSearchOptions = s.Options.Where(o => o.CardId != null && o.Move.Kind is DuelMoveKind.SearchDeck or DuelMoveKind.MindResetMemory or DuelMoveKind.MindResetHand).ToArray();
        _selectedSkill = Math.Clamp(_selectedSkill, 0, Math.Max(0, s.Players[s.MySeat].Skills.Length - 1));
        var header = new HBoxContainer(); _root.AddChild(header);
        var brand = ArenaPresentation.Text("R E L I C K", Compact ? 21 : 29, BoardArt.Gold);
        var brandColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        brandColumn.AddThemeConstantOverride("separation", 0); brandColumn.AddChild(brand); header.AddChild(brandColumn);
        _status = ArenaPresentation.Text(Connection.Message, 12, BoardArt.Muted); _status.TooltipText = "Sala " + RoomCode;
        _status.Visible = !Short; brandColumn.AddChild(_status);
        if (Compact)
        {
            var menu = new MenuButton { Text = "Menú", CustomMinimumSize = new(70, 48) }; header.AddChild(menu);
            var popup = menu.GetPopup(); ArenaViewport.TouchMenu(popup); popup.AddItem("Reconectar", 0);
            if (s.Winner != null || _preparing) popup.AddItem("Cerrar sala", 2);
            popup.IdPressed += id => { if (id == 0) ReconnectRequested?.Invoke(); else Connection.Leave(); };
        }
        else
        {
            ArenaPresentation.Button(header, "Reconectar", () => ReconnectRequested?.Invoke());
            if (s.Winner != null || _preparing) ArenaPresentation.Button(header, "Cerrar sala", Connection.Leave);
        }
        var phase = s.Winner != null ? s.Winner == -1 ? "EMPATE" : s.Winner == s.MySeat ? "¡VICTORIA!" : "DERROTA" :
            $"TURNO {s.Turn:00} · {PhaseName(s.Phase)} · {(s.DecisionPlayer == s.MySeat ? "TU DECISIÓN" : "RIVAL")}";
        var phaseCaption = _preparing ? PreparationCaption() : s.Winner != null ? phase : $"{PhaseName(s.Phase)} · {(s.DecisionPlayer == s.MySeat ? "TU DECISIÓN" : "RIVAL")}";
        if (Short) phaseCaption += $" · TÚ {s.Players[s.MySeat].Health}/{s.Players[s.MySeat].MaxHealth} · RIVAL {s.Players[1 - s.MySeat].Health}/{s.Players[1 - s.MySeat].MaxHealth}";
        ArenaPresentation.Phases(_root, s.Stage ?? s.Phase, phaseCaption, s.Turn, _preparing || s.CurrentPlayer == s.MySeat, Compact);
        if (Compact && !Short) ArenaPresentation.Scoreline(_root, [s.Players[s.MySeat], s.Players[1 - s.MySeat]]);
        var body = ArenaPresentation.Body(_root, Short && Size.X >= 700);
        var arena = ArenaPresentation.Arena(body.Board, Compact);
        _arenaScroll = arena.GetParent() as ScrollContainer;
        if (_arenaScroll != null) _arenaScroll.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, boardPosition);
        var relativePlayers = new[] { s.Players[s.MySeat], s.Players[1 - s.MySeat] };
        void Player(int relative)
        {
            var seat = relative == 0 ? s.MySeat : 1 - s.MySeat;
            var player = relativePlayers[relative];
            _portraits[relative].SetHealth(player.Health);
            ArenaPresentation.Player(arena, player, relative, relative == 1, !_preparing && s.CurrentPlayer == seat && s.Winner == null,
                _selectedSkill, _portraits[relative], _life[relative], new ArenaPlayerActions
                {
                    Memory = corrupt => ShowCards(corrupt ? "Memoria corrupta" : "Memoria", corrupt ? null : player.Memory, player.CorruptedCount),
                    Attached = () => ShowCards("Sobre Yuno", player.Attached),
                    Equipment = () => ShowCards(player.EquipmentDie > 0 ? $"Equipo · D6 guardado: {player.EquipmentDie}" : "Equipo", player.Equipment == null ? [] : [player.Equipment]),
                    Skill = slot =>
                    {
                        if (slot >= player.Skills.Length) return;
                        if (relative == 1) ShowCards("Habilidad rival", [player.Skills[slot]]);
                        else { _selectedSkill = slot; RefreshSelection(); }
                    },
                    InspectSkill = slot => ShowCards("Habilidad", [player.Skills[slot]]),
                    Evolve = () => SendKind(DuelMoveKind.Evolve), CanEvolve = s.Options.Any(o => o.Move.Kind == DuelMoveKind.Evolve) && !Connection.PlayPending && !IsAnimating
                }, Compact);
        }
        Player(0);
        var combatMessage = _preparing ? PreparationMessage() : s.Winner != null ? phase : s.LastCombat;
        var combat = ArenaPresentation.Combat(arena, _preparing, s.Winner != null, combatMessage, relativePlayers, BuildOpponentHand,
            relative => { if (relativePlayers[relative].Bonus is { } id) ShowCards("Bonus", [id]); },
            (relative, slot) => { if (slot < relativePlayers[relative].Exchange.Length) ShowCards("Canje", [relativePlayers[relative].Exchange[slot]]); }, Compact);
        _combatMessage = (Label)combat.FindChild("CombatMessage", true, false);
        Player(1);
        _selection = ArenaPresentation.Text("", 12, BoardArt.Muted); _selection.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        if (Short) _selection.MaxLinesVisible = 1;
        body.Controls.AddChild(_selection);
        var movesScroll = new ScrollContainer { Name = "MovesScroll", CustomMinimumSize = new(0, Short ? 56 : Compact ? 106 : 56), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        body.Controls.AddChild(movesScroll);
        _moves = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _moves.AddThemeConstantOverride("h_separation", 8); _moves.AddThemeConstantOverride("v_separation", 6); movesScroll.AddChild(_moves);
        _hand = _choosingOpeningSkill ? ArenaPresentation.InitialSkills(body.Controls, _availableInitialSkills, _initialSkillCandidate,
            SelectInitialSkillCandidate, Short) : ArenaPresentation.Hand(body.Controls, SelectableCards.Select(id => id!).ToArray(), index => _selected.Contains(index), SelectCard, _preparing, Short,
                ChoosingRecycling ? "TU MEMORIA · ELIGE PARA RECICLAR" : ChoosingResetCards ? "RESETEAR LA MENTE · ELIGE UNA CARTA" : ChoosingDeckSearch ? "TU MAZO · ELIGE UNA NO-HABILIDAD" : "TU MANO");
        _hand.SetDeferred(ScrollContainer.PropertyName.ScrollHorizontal, handPosition);
        _handCards.Clear(); _handCards.AddRange(_hand.FindChildren("HandCard*", "Button", true, false).OfType<HandCard>());
        _initialSkillCards.Clear(); _initialSkillCards.AddRange(_hand.FindChildren("InitialSkillCard*", "Button", true, false).OfType<HandCard>());
        _ownSkills.Clear(); _ownSkills.AddRange(_root.FindChildren("Skill0_*", "Button", true, false).OfType<SkillTile>());
        _evolveButton = _root.FindChild("EvolveRelikario", true, false) as Button;
        BuildMoves(); MoveChild(_diceOverlay, GetChildCount() - 1);
        BoardBuildCount++;
    }

    private void BuildOpponentHand(VBoxContainer parent)
    {
        var hand = _snapshot!.Players[1 - _snapshot.MySeat].Hand;
        parent.AddChild(ArenaPresentation.Text($"MANO DEL RIVAL · {hand.Length}", 10, ArenaPresentation.Coral));
        var scroll = new ScrollContainer { Name = "OpponentHandScroll", CustomMinimumSize = new(0, 90), VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 4); scroll.AddChild(row); parent.AddChild(scroll);
        for (var i = 0; i < hand.Length; i++)
        {
            var id = hand[i];
            var card = new OpponentHandCard { Name = $"OpponentHandCard{i}", PublicCard = id == null ? null : CardCatalog.Get(id), FaceSize = new(54, 76.4f) };
            card.Pressed += () => ShowCards("Mano del rival", hand, hand.Length); row.AddChild(card);
        }
    }

    private void SelectCard(int index)
    {
        if (_preparing)
        {
            if (!CanChooseOpening || IsAnimating || index < 0 || index >= _openingCards.Length) return;
            var id = _openingCards[index].Id;
            if (!_openingReturns.Add(id)) _openingReturns.Remove(id);
            SyncOpeningSelection(); RefreshSelection(); return;
        }
        var defense = _snapshot!.Options.Any(o => o.RequiredDiscards > 0);
        if (!defense && !_selected.Contains(index)) _selected.Clear();
        if (!_selected.Add(index)) _selected.Remove(index);
        RefreshSelection();
    }
    private void RefreshSelection()
    {
        foreach (var card in _handCards)
        { card.Chosen = _selected.Contains(int.Parse(card.Name.ToString()[8..])); card.QueueRedraw(); }
        foreach (var skill in _ownSkills)
        { skill.Chosen = skill.Skill != null && skill.Slot == _selectedSkill; skill.QueueRedraw(); }
        BuildMoves();
    }
    private void BuildMoves()
    {
        foreach (var child in _moves.GetChildren()) { _moves.RemoveChild(child); child.QueueFree(); }
        _moveButtons.Clear();
        var s = _snapshot!;
        if (_preparing) { BuildPreparationMoves(); return; }
        var defense = s.Options.Any(o => o.RequiredDiscards > 0);
        _selection.Text = s.Winner != null ? "El duelo terminó." : _selected.Count > 0
            ? ChoosingRecycling ? $"Reciclaje: {_selected.Count}/{s.Options.First(o => o.Move.Kind == DuelMoveKind.Recycle).RequiredDiscards} cartas seleccionadas."
                : string.Join(" · ", _selected.Select(i => CardCatalog.Get(SelectableCards[i]!).Name))
            : ChoosingRecycling ? "Elige las cartas de tu memoria que volverán al mazo."
                : ChoosingResetCards ? "Elige una carta y confirma su selección para Resetear la Mente."
                : ChoosingDeckSearch ? "Elige una carta del mazo para añadirla a tu mano."
                : s.Phase == Phase.Final && s.DecisionPlayer == s.MySeat ? $"Descarta {s.Players[s.MySeat].HandCount - GameEngine.MaxHandSize} carta(s) para terminar con un máximo de {GameEngine.MaxHandSize}."
                : s.DecisionPlayer == s.MySeat ? "Toca una carta o una habilidad y elige tu jugada." : "Esperando al rival. Puedes responder si tienes un COMBO disponible.";
        void PreviewSelectedCard()
        {
            var card = SelectableCards[_selected.Single()]!;
            ArenaPresentation.Button(_moves, "Ver carta", () => ShowCards("Tu carta", [card]));
        }
        if (_selected.Count == 1 && !ChoosingEffectCards) PreviewSelectedCard();
        if (!ChoosingEffectCards && _selectedSkill < s.Players[s.MySeat].Skills.Length)
            ArenaPresentation.Button(_moves, "Ver habilidad", () => ShowCards("Tu habilidad", [s.Players[s.MySeat].Skills[_selectedSkill]]));
        for (var i = 0; i < s.Options.Count; i++)
        {
            var optionIndex = i; var option = s.Options[i];
            var handMove = option.Move.Kind is DuelMoveKind.Learn or DuelMoveKind.Attack or DuelMoveKind.PlayCode or DuelMoveKind.Equip or DuelMoveKind.Exchange or DuelMoveKind.Discard or DuelMoveKind.Placement;
            if (!defense && handMove && !_selected.Contains(option.Move.Index)) continue;
            if (option.Move.Kind == DuelMoveKind.Attack && option.Move.Value != _selectedSkill) continue;
            if (ChoosingEffectCards && option.CardId != null &&
                (_selected.Count != 1 || !ReferenceEquals(_deckSearchOptions[_selected.Single()], option))) continue;
            if (ChoosingEffectCards && option.Move.Kind == DuelMoveKind.Concede && _selected.Count == 1) PreviewSelectedCard();
            var button = ArenaPresentation.Button(_moves, option.Label, () => PlayOption(optionIndex, s));
            if (Compact) { button.AutowrapMode = TextServer.AutowrapMode.WordSmart; button.CustomMinimumSize = new(Short && Size.X >= 700 ? 280 : Math.Max(120, Size.X - 44), 48); }
            button.SetMeta("discards", option.RequiredDiscards); _moveButtons.Add(button);
        }
        if (s.Winner != null) ArenaPresentation.Button(_moves, "Cerrar sala", Connection.Leave);
        _lastDisabled = null; UpdateEnabled();
    }
    private void UpdateEnabled()
    {
        var disabled = _preparing ? !(_choosingOpeningSkill ? CanChooseInitialSkill : CanChooseOpening) || IsAnimating : !Connection.Ready || Connection.PlayPending || IsAnimating;
        if (_lastDisabled == disabled) return;
        _lastDisabled = disabled;
        foreach (var button in _moveButtons) button.Disabled = disabled || (int)button.GetMeta("discards") > 0 && (int)button.GetMeta("discards") != _selected.Count;
        if (_evolveButton is Button evolve)
            evolve.Disabled = disabled || !_snapshot!.Options.Any(o => o.Move.Kind == DuelMoveKind.Evolve);
        if (_choosingOpeningSkill && InitialSkillConfirmButton != null) InitialSkillConfirmButton.Disabled |= _initialSkillCandidate == null;
        if (_choosingOpeningSkill && _hand != null)
            foreach (var card in _initialSkillCards) card.Disabled = disabled;
        if (_hand != null) foreach (var card in _handCards) card.Disabled = disabled;
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
            dialog.Confirmed += () => { Connection.Play(index, expectedRevision: state.Revision, expectedRoom: RoomCode); dialog.QueueFree(); };
            dialog.Canceled += () => dialog.QueueFree(); AddChild(dialog); ArenaViewport.FitDialog(dialog, Size);
        }
        else Connection.Play(index, state.Options[index].RequiredDiscards > 0 ? _selected : null, state.Revision, RoomCode);
    }

    private void ShowCards(string title, IReadOnlyList<string?>? ids, int hiddenCount = 0)
    {
        if (GetChildren().OfType<Window>().Any(w => w.Visible)) return;
        var dialog = new AcceptDialog { Title = title, OkButtonText = "Volver al duelo" };
        var scroll = new ScrollContainer { CustomMinimumSize = new(Math.Min(620, Size.X - 48), Math.Min(390, Size.Y - 110)), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; scroll.AddChild(list);
        if (ids == null) list.AddChild(ArenaPresentation.Text($"{hiddenCount} cartas corruptas boca abajo. Su identidad permanece oculta."));
        else foreach (var id in ids)
        {
            if (id == null) { list.AddChild(ArenaPresentation.Text("Carta oculta · mano del rival")); continue; }
            var card = CardCatalog.Get(id);
            list.AddChild(new HandCard { Card = card, Disabled = true });
            var text = ArenaPresentation.Text(card.Name + "\n" + card.Text); text.AutowrapMode = TextServer.AutowrapMode.WordSmart; list.AddChild(text);
        }
        if (ids?.Count == 0) list.AddChild(ArenaPresentation.Text("No hay cartas en esta zona."));
        dialog.AddChild(scroll); dialog.Confirmed += () => dialog.QueueFree(); dialog.Canceled += () => dialog.QueueFree();
        AddChild(dialog); ArenaViewport.FitDialog(dialog, Size);
    }

    private static string PhaseName(Phase phase) => phase switch
    {
        Phase.Preparation => "PREPARACIÓN", Phase.Combat => "COMBATE", Phase.Actions => "ACCIONES", Phase.Discard => "DESCARTE", Phase.Final => "DESCARTE FINAL", Phase.Finished => "FINAL",
        Phase.AttackRerollChoice or Phase.DieRerollChoice => "DECISIÓN DEL DADO", Phase.DefenseChoice => "DEFENSA",
        Phase.CodeResponse or Phase.ErrorResponse => "RESPUESTA", Phase.RecycleChoice => "RECICLAJE", Phase.DeckSearchChoice => "BUSCAR EN EL MAZO", Phase.MindResetChoice => "RESETEAR LA MENTE", Phase.SniperChoice => "MIRA DE SNIPER", _ => "RESOLVER EFECTO"
    };
}
