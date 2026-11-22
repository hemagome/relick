using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    private Label _contextTitle = null!, _contextCost = null!;
    private CardInspector _inspector = null!;
    private HBoxContainer _decisionBar = null!;
    private ScrollContainer _movesScroll = null!;
    private VBoxContainer _actionDock = null!;
    private AdvancePhaseButton? _advanceButton;
    private HBoxContainer? _passResponseSlot;
    private string _decisionHint = "";
    private string _selectionCost = "";
    private readonly List<MenuButton> _actionMenus = [];
    private bool _skillContext, _showReplayMemory;
    private (int Owner, FieldZone Zone, int Index)? _fieldSelection;
    private (CardDefinition Card, int? Counters)? _inspectorPreview;
    private (int Seat, RelikarioDefinition Form, bool Evolution)? _relikarioPreview;
    private Vector2? _fieldPress;

    private void ResetInteraction()
    {
        _decisionMinimized = false;
        _pausedGallery = null;
        ClearSelection();
    }

    private void ClearSelection()
    {
        _selected.Clear(); _selectedSkill = -1; _skillContext = false;
        _choosingAttackSkill = false;
        _fieldSelection = null; _showReplayMemory = false;
        _inspectorPreview = null; _relikarioPreview = null;
    }

    private void BuildInspector(VBoxContainer parent)
    {
        var dock = new VBoxContainer(); _actionDock = dock;
        dock.Name = "ActionDock"; dock.AddThemeConstantOverride("separation", Short ? 4 : 8); parent.AddChild(dock);
        _inspector = new CardInspector { Name = "ContextActionBar", ShortScreen = Short }; dock.AddChild(_inspector);
        _contextTitle = _inspector.TitleLabel; _contextCost = _inspector.CostLabel;
    }

    private void BuildDecisionButtons(VBoxContainer parent)
    {
        var actions = new HBoxContainer { Name = "DecisionBar", MouseFilter = MouseFilterEnum.Ignore };
        _decisionBar = actions; parent.AddChild(actions);
        var scroll = new ScrollContainer { Name = "MovesScroll", CustomMinimumSize = new(0, 50),
            SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _movesScroll = scroll; actions.AddChild(scroll);
        _moves = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = FlowContainer.AlignmentMode.Center };
        _moves.AddThemeConstantOverride("h_separation", 6); _moves.AddThemeConstantOverride("v_separation", 6); scroll.AddChild(_moves);
        _moves.MinimumSizeChanged += FitMovesScroll;
    }

    // Buttons that wrap onto a second row (e.g. pay or skip Huevo Azul's draw) stay visible instead of
    // hiding behind the scrollbar; longer lists still scroll once they pass the cap.
    private void FitMovesScroll()
    {
        var floor = _decisionPopup == null && (ChoosingMemoryRecovery || ChoosingDeckGallery) ? 60 : 50;
        var cap = Short ? 104 : 168;
        var height = Mathf.Clamp(_moves.GetCombinedMinimumSize().Y, floor, Math.Max(floor, cap));
        if (!Mathf.IsEqualApprox(_movesScroll.CustomMinimumSize.Y, height)) _movesScroll.CustomMinimumSize = new(0, height);
    }

    private static bool IsHandAction(DuelMoveKind kind) => kind is DuelMoveKind.Learn or DuelMoveKind.Attack or DuelMoveKind.PlayCode or
        DuelMoveKind.Equip or DuelMoveKind.ActivateArea or DuelMoveKind.Exchange or DuelMoveKind.RevealGhostDog or DuelMoveKind.Discard or DuelMoveKind.Placement or DuelMoveKind.TurnBuffDiscard;

    private string? OptionCardId(DuelOption option)
    {
        if (option.CardId != null) return option.CardId;
        var s = _snapshot!; var m = option.Move;
        if (m.Kind == DuelMoveKind.Corrupt) return s.Players[m.Target].Memory.ElementAtOrDefault(m.Index);
        if (m.Kind == DuelMoveKind.Relikia && m.Value == (int)RelikiaEffect.ReplayCode) return s.Players[s.MySeat].Memory.ElementAtOrDefault(m.Index);
        return null;
    }

    private Button AddOption(Node parent, int index, string? label = null, bool primary = false)
    {
        var s = _snapshot!; var option = s.Options[index];
        Button button = parent == _cardActions ? new CardActionButton { Symbol = CardActionSymbol(option.Move) } :
            option.Move.Kind == DuelMoveKind.Advance ? new AdvancePhaseButton() : new Button();
        button.Text = label ?? ArenaActionText.Label(option, s);
        button.Pressed += () => PlayOption(index, s); parent.AddChild(button);
        button.Name = "DuelOption" + index;
        if (button is CardActionButton cardAction) cardAction.Style(primary, Short);
        else if (button is AdvancePhaseButton advance) advance.Style(s.Phase switch
        {
            Phase.Draw => s.ExtraDrawStages > 0 ? Phase.Draw : Phase.Combat,
            Phase.Combat => s.ExtraCombatStages > 0 ? Phase.Combat : Phase.Actions,
            Phase.Actions when s.ExtraActionStages > 0 => Phase.Actions,
            _ => Phase.Final
        }, Short);
        else
        {
            var fontSize = Short ? 13 : 15;
            button.MouseDefaultCursorShape = CursorShape.PointingHand;
            button.AddThemeFontSizeOverride("font_size", fontSize);
            var lines = button.Text.Split('\n');
            var width = lines.Max(line => ThemeDB.FallbackFont.GetStringSize(line, HorizontalAlignment.Left, -1, fontSize).X);
            button.CustomMinimumSize = new(Math.Clamp(width + 30, 64, Short ? 240 : 320), 48);
            if (lines.Length > 1) button.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            else button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            if (primary) RelickVisuals.Primary(button);
        }
        var cost = ArenaActionText.Cost(option, s);
        button.TooltipText = button is CardActionButton && cost.Length > 0 ? button.Text + " · " + cost : cost;
        if (option.Move.Kind == DuelMoveKind.RevealGhostDog)
            button.TooltipText += "\nSe activa gratis al inicio de tu próxima etapa de acciones si sigue en tu mano.";
        if (s.Phase == Phase.ErrorResponse && option.Move.Kind == DuelMoveKind.PlayCode && ArenaActionText.HandCard(option, s)?.Id == CardCatalog.CopyHelmet.Id)
            button.TooltipText += ArenaActionText.SingleCopyTargetName(s) is { } target ? $"\nCopiar {target}" : "\nElige qué carta de la RAM copiar.";
        button.SetMeta("option_index", index); button.SetMeta("discards", option.RequiredDiscards);
        _moveButtons.Add(button); return button;
    }

    private void AddChoiceMenu(Node parent, string label, IEnumerable<int> indices, bool chooseSource = false)
    {
        var s = _snapshot!; var entries = indices.ToArray(); if (entries.Length == 0) return;
        var menu = new MenuButton { Text = label, CustomMinimumSize = new(0, 48) };
        menu.AddThemeFontSizeOverride("font_size", Short ? 13 : 15);
        parent.AddChild(menu); _actionMenus.Add(menu);
        menu.SetMeta("choose_source", chooseSource);
        var popup = menu.GetPopup(); ArenaViewport.TouchMenu(popup);
        foreach (var i in entries)
        {
            var o = s.Options[i]; var caption = ArenaActionText.Label(o, s); var cost = ArenaActionText.Cost(o, s);
            if (o.Move.Kind == DuelMoveKind.FieldCode)
                caption = "Jessica Lee · " + (o.Move.Value == (int)FieldZone.Bonus ? "Bonus" : "Canje");
            if (o.Move.Kind == DuelMoveKind.Relikia) caption = RelikiaEffects.Get((RelikiaEffect)o.Move.Value).Name;
            if (o.Move.Kind == DuelMoveKind.DestinyDefense) caption = "Destiny Shuffle";
            if (o.Move.Kind == DuelMoveKind.ProtectDestruction) caption = "Reverse Shuffle";
            if (cost.Length > 0) caption += " · " + cost;
            popup.AddItem(caption, i);
        }
        popup.IdPressed += id =>
        {
            var index = (int)id;
            if (chooseSource)
            {
                var m = s.Options[index].Move;
                if (m.Kind is DuelMoveKind.DestinyDefense or DuelMoveKind.ProtectDestruction)
                { _selectedSkill = m.Index; _skillContext = true; _fieldSelection = null; RefreshSelection(); return; }
            }
            PlayOption(index, s);
        };
    }

    private void BuildTurnControl(Node header)
    {
        var s = _snapshot!;
        _advanceButton = null;
        _passResponseSlot = null;
        AddTurnButton(header);
        ArmTurnGuard();
    }

    private void AddTurnButton(Node header)
    {
        var s = _snapshot!;
        if (_preparing) return;
        if (s.Phase == Phase.ErrorResponse && s.DecisionPlayer == s.MySeat)
        {
            _passResponseSlot = new HBoxContainer { Name = "ResponseControls", MouseFilter = MouseFilterEnum.Ignore };
            header.AddChild(_passResponseSlot);
            return;
        }
        var advance = s.Options.FindIndex(o => o.Move.Kind == DuelMoveKind.Advance);
        if (advance < 0) return;
        _advanceButton = (AdvancePhaseButton)AddOption(header, advance);
        _advanceButton.Name = "AdvanceStage";
        _advanceButton.TooltipText = (AdvanceSuggested(s)
            ? "No quedan jugadas disponibles. " + ArenaGuidance.NextStep(s) : "Avanza cuando termines tus jugadas de esta fase.") + ShortcutHint;
    }

    private static string ShortcutHint => OS.HasFeature("mobile") ? "" : "\nAtajo: Espacio.";

    private static bool IsComboActivation(DuelOption option, DuelSnapshot s) =>
        option.Move.Kind == DuelMoveKind.PlayCode && ArenaActionText.HandCard(option, s)?.IsCombo == true;

    private void BuildContextMoves()
    {
        var s = _snapshot!;
        var indices = Enumerable.Range(0, s.Options.Count).Where(i => s.Options[i].Move.Kind is not
            (DuelMoveKind.Advance or DuelMoveKind.Concede or DuelMoveKind.Evolve or DuelMoveKind.DestinyPickMemory or
                DuelMoveKind.PrepareD10 or DuelMoveKind.PassError)).ToList();
        bool IsCombo(int index) => IsComboActivation(s.Options[index], s);
        var selectedCombo = ShowCardActivations ? indices.FirstOrDefault(i => IsCombo(i) &&
            (_fieldSelection is { } source ? MatchesField(s.Options[i], source) :
                !_skillContext && _selected.Count == 1 && s.Options[i].Field == null && _selected.Contains(s.Options[i].Move.Index)), -1) : -1;
        indices.RemoveAll(IsCombo);
        var cardAnchoring = !NeedsDecisionPrompt(s);
        if (ViewingBoardDuringGallery)
            indices.Clear();
        else if (s.Phase == Phase.ErrorResponse)
        {
            indices.Clear();
            var pass = s.Options.FindIndex(o => o.Move.Kind == DuelMoveKind.PassError);
            if (_passResponseSlot != null && pass >= 0)
            {
                var button = AddOption(_passResponseSlot, pass);
                button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                button.TooltipText = (s.ErrorAfterDie ? $"Dado final: {s.Die}. " : "") +
                    "Selecciona un COMBO iluminado para responder o deja que se resuelva la activación." + ShortcutHint;
            }
        }
        else if (s.Phase == Phase.TrapCraftChoice)
            indices = indices.Where(i => s.Options[i].Move.Kind == DuelMoveKind.TrapCraft).ToList();
        else if (Deciding == DecisionKind.Confirm)
            indices = indices.Where(i => s.Options[i].Move.Kind == DuelMoveKind.Choose).ToList();
        else if (ChoosingEffectCards)
            indices = indices.Where(i => _selected.Count == 1 && ReferenceEquals(_deckSearchOptions[_selected.Single()], s.Options[i]) ||
                Deciding == DecisionKind.ChooseCard && s.Options[i].Move.Kind == DuelMoveKind.Choose && s.Options[i].CardId == null).ToList();
        else if (_fieldSelection is { } field && (selectedCombo < 0 || cardAnchoring))
            indices = indices.Where(i => MatchesField(s.Options[i], field)).ToList();
        else
        {
            indices.RemoveAll(i => s.Options[i].Move.Kind is DuelMoveKind.Relikia or DuelMoveKind.FieldCode or DuelMoveKind.Magnet or DuelMoveKind.UsePurpleRing ||
                s.Options[i].Move.Kind == DuelMoveKind.Choose && s.Options[i].Field is { } target &&
                !(selectedCombo >= 0 && target.Zone == FieldZone.Hand && target.Owner == s.MySeat));
            indices.RemoveAll(i => IsHandAction(s.Options[i].Move.Kind) && !_selected.Contains(s.Options[i].Move.Index));
            if (_selected.Count > 1 || _skillContext) indices.RemoveAll(i => IsHandAction(s.Options[i].Move.Kind));
            if (!_skillContext && indices.Any(i => IsHandAction(s.Options[i].Move.Kind)))
                indices.RemoveAll(i => !IsHandAction(s.Options[i].Move.Kind));
            indices.RemoveAll(i => s.Options[i].Move.Kind == DuelMoveKind.Attack &&
                (_choosingAttackSkill || s.Options[i].Move.Value != _selectedSkill));
            if (_skillContext)
                indices = indices.Where(i => s.Options[i].Move.Kind is DuelMoveKind.DestinyDefense or DuelMoveKind.ProtectDestruction && s.Options[i].Move.Index == _selectedSkill).ToList();
            else indices.RemoveAll(i => s.Options[i].Move.Kind is DuelMoveKind.DestinyDefense || s.Options[i].Move.Kind == DuelMoveKind.ProtectDestruction && s.Options[i].Move.Index >= 0);
        }
        var chooseAttackSkill = !_choosingAttackSkill && _selectedSkill < 0 && SelectedAttacks().Any();
        var handActions = cardAnchoring && !ChoosingEffectCards && !ChoosingRecycling &&
            !_skillContext && _fieldSelection == null && _selected.Count == 1;
        if (handActions)
            _cardActionAnchor = _handCards.ElementAtOrDefault(_selected.Single());
        else if (cardAnchoring && _fieldSelection is { } target)
            _cardActionAnchor = FieldActionAnchor(target);

        // A pending die has one principal action and one menu of real modifiers.
        var die = indices.Where(i => s.Options[i].Move.Kind is DuelMoveKind.AttackDie or DuelMoveKind.EffectDie && !s.Options[i].Move.Flag).ToArray();
        var main = indices.FirstOrDefault(i => s.Options[i].Move.Kind is DuelMoveKind.PlayCode or DuelMoveKind.ActivateArea or DuelMoveKind.Equip or DuelMoveKind.Learn or DuelMoveKind.Attack or
            DuelMoveKind.Recycle or DuelMoveKind.TurnBuffDiscard or DuelMoveKind.DestinyDefense ||
            s.Options[i].Move.Kind == DuelMoveKind.RelikarioSearch ||
            s.Options[i].Move.Kind is DuelMoveKind.TrapCraft && s.Options[i].Move.Flag ||
            s.Options[i].Move.Kind == DuelMoveKind.Choose && (s.Options[i].Primary || Deciding == DecisionKind.ChooseCard && s.Options[i].CardId != null), -1);
        if (main < 0 && die.Length > 0) main = die[0];
        if (ChoosingDeckGallery || _showcase != null && ChoosingMemoryRecovery)
        {
            if (ChoosingDeckGallery)
                foreach (var index in indices.Where(i => s.Options[i].CardId == null))
                    AddOption(_moves, index);
            var includesDeck = _deckSearchOptions.Any(o => EffectCardOwner(o) == null);
            var confirm = main >= 0 ? AddOption(_moves, main, primary: true) : new Button
            {
                Text = ChoosingDeckGallery && _deckSearchOptions.FirstOrDefault() is { } choice
                    ? ArenaActionText.Label(choice, s) : includesDeck ? "Añadir a la mano" : "Recuperar",
                Disabled = true,
                TooltipText = includesDeck ? "Selecciona una carta." : "Selecciona una carta de la memoria."
            };
            if (main < 0)
            {
                _moves.AddChild(confirm);
                RelickVisuals.Primary(confirm);
            }
            confirm.CustomMinimumSize = new(192, 56);
            confirm.AddThemeFontSizeOverride("font_size", 18);
        }
        else if (die.Length > 0)
        {
            AddOption(_moves, die[0], primary: die[0] == main);
            AddChoiceMenu(_moves, "Modificar…", indices.Except(die).Where(i => ArenaGuidance.IsUsefulDieModifier(s.Options[i], s)));
        }
        else
        {
            if (chooseAttackSkill && _cardActionAnchor != null) AddAttackSkillChoice();
            foreach (var index in indices)
            {
                var option = s.Options[index];
                var besideCard = _cardActionAnchor != null && (handActions ? IsHandAction(option.Move.Kind) : _fieldSelection != null);
                AddOption(besideCard ? _cardActions : _moves, index, primary: index == main && (!besideCard || selectedCombo < 0));
            }
        }
        if (!_skillContext && _fieldSelection == null && !ChoosingEffectCards && !ViewingBoardDuringGallery &&
            !indices.Any(i => IsHandAction(s.Options[i].Move.Kind)))
        {
            AddChoiceMenu(_moves, s.Phase == Phase.DestructionResponse ? "Sustituir…" : "Defender…",
                s.Options.Select((o, i) => (o, i)).Where(c => c.o.Move.Kind == DuelMoveKind.DestinyDefense || c.o.Move.Kind == DuelMoveKind.ProtectDestruction && c.o.Move.Index >= 0).Select(c => c.i), true);
        }
        if (selectedCombo >= 0)
        {
            _cardActionAnchor = _fieldSelection is { } source
                ? FieldActionAnchor(source) : _handCards.ElementAtOrDefault(s.Options[selectedCombo].Move.Index);
            if (_cardActionAnchor != null)
                _cardActions.MoveChild(AddOption(_cardActions, selectedCombo, primary: true), 0);
        }
        PositionCardActions();
        var costs = indices.Select(i => ArenaActionText.Cost(s.Options[i], s)).Where(v => v.Length > 0).Distinct().ToArray();
        _selectionCost = main >= 0 ? (costs.Length > 1 ? ArenaActionText.Label(s.Options[main], s) + ": " : "") + ArenaActionText.Cost(s.Options[main], s)
            : costs.Length == 1 ? costs[0] : "";
        if (selectedCombo >= 0) _selectionCost = ArenaActionText.Cost(s.Options[selectedCombo], s);
        if (_selectionCost.Length == 0 && !_skillContext && _fieldSelection == null && _selected.Count == 1 &&
            s.Phase.IsStageBeforeFinal() && SelectedCardId() is { } selected)
            _selectionCost = ArenaActionText.CardCost(CardCatalog.Get(selected));
        _selection.Text = ViewingBoardDuringGallery || selectedCombo >= 0 && !cardAnchoring ? ArenaGuidance.Instruction(s) : ContextHint();
        _selection.TooltipText = _selection.Text;
        _decisionHint = _selection.Text;
        RefreshDecisionPrompt();
        RefreshInspector();
    }

    private Button AddLocalButton(Node parent, string label, Action pressed)
    {
        var button = ArenaPresentation.Button(parent, label, pressed); button.TooltipText = label;
        button.AddThemeFontSizeOverride("font_size", Short ? 13 : 15); return button;
    }

    private void ClearContext()
    {
        var gallery = _showReplayMemory;
        if (ViewingBoardDuringGallery) ClearSelection(); else ResetInteraction();
        if (gallery) Render(); else RefreshSelection();
    }

    private void ClearContextOnFieldClick(InputEvent input)
    {
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left } click) return;
        var point = GetViewport().GetMousePosition();
        if (click.Pressed)
        {
            _fieldPress = point;
            return;
        }
        var start = _fieldPress;
        _fieldPress = null;
        if (start == null || start.Value.DistanceSquaredTo(point) > 36 || IsAnimating) return;

        // A fanned card stays raised while it owns keyboard focus.
        if (GetViewport().GuiGetFocusOwner() is HandCard card && _handCards.Contains(card)) card.ReleaseFocus();

        if (_preparing)
        {
            _inspectorPreview = null; _relikarioPreview = null;
            _openingInspection = null;
            RefreshInspector();
        }
        else if (_inspectorPreview != null || _relikarioPreview != null || HasContextSelection || _showReplayMemory) ClearContext();
        else return;
        GetViewport().SetInputAsHandled();
    }

    private bool HasContextSelection => _selected.Count > 0 || _selectedSkill >= 0 || _fieldSelection != null ||
        _preparing && SelectedCardId() != null;

    /// <summary>Hovering a Relikario portrait reads its card and effect in the inspector, as hovering a card does.</summary>
    private void BindRelikarioPreviews()
    {
        for (var relative = 0; relative < _portraits.Length; relative++)
        {
            var portrait = _portraits[relative]; var index = relative;
            portrait.MouseEntered += () =>
            {
                if (_snapshot is { } s && !_preparing && !HasContextSelection)
                    PreviewRelikario(index == 0 ? s.MySeat : 1 - s.MySeat);
            };
        }
    }

    private void PreviewRelikario(int seat, bool evolution = false)
    {
        if (IsAnimating ||
            RelikarioCatalog.Find(_snapshot!.Players[seat].RelikarioId) is not { } current) return;
        var form = evolution && current.Level == 1 ? RelikarioCatalog.EvolutionFor(current) : current;
        if (form == null || _relikarioPreview == (seat, form, evolution)) return;
        _inspectorPreview = null;
        _relikarioPreview = (seat, form, evolution);
        RefreshInspector();
    }

    private void BindInspectorPreviews()
    {
        // A disabled evolution button still receives pointer hover and can be read.
        if (_evolveButton != null) _evolveButton.MouseEntered += () => PreviewRelikario(_snapshot!.MySeat, evolution: true);
        var buttons = _root!.FindChildren("*", "Button", true, false).OfType<Button>();
        if (_deckChoiceShade != null) buttons = buttons.Concat(_handCards).Concat(_initialSkillCards);
        foreach (var control in buttons)
        {
            var card = control switch
            {
                HandCard hand when hand.MouseFilter != MouseFilterEnum.Ignore => hand.Card,
                OpponentHandCard opponent => opponent.PublicCard,
                SkillTile tile => tile.Skill,
                ZoneCardButton zone => zone.Card,
                _ => null
            };
            if (card == null && control is SkillTile or ZoneCardButton)
                control.GuiInput += ClearContextOnFieldClick;
            if (card == null && control is not OpponentHandCard) continue;
            int? counters = control switch
            {
                SkillTile skill => skill.Counters,
                ZoneCardButton zone => zone.PresentedCounters?.Count,
                _ => null
            };
            var owner = SkillTileOwner(control);
            var relikario = control switch
            {
                HandCard hand => hand.ArtworkRelikario,
                OpponentHandCard opponent => opponent.ArtworkRelikario,
                ZoneCardButton zone => zone.RelikarioId,
                _ => null
            };
            var tooltip = control.TooltipText;
            control.MouseEntered += () =>
            {
                // Choosing the skill of an Attack is exactly when its damage table is worth reading.
                var choosingSkill = _choosingAttackSkill && control is SkillTile;
                if (IsAnimating || HasContextSelection && !choosingSkill) return;
                if (card != null) control.TooltipText = "";
                (CardDefinition, int?)? preview = card == null ? null : (card, counters);
                if (_inspectorPreview == preview && _inspectorPreviewOwner == owner && _inspectorPreviewRelikario == relikario && _relikarioPreview == null) return;
                _inspectorPreview = preview; _inspectorPreviewOwner = owner; _inspectorPreviewRelikario = relikario; _relikarioPreview = null;
                RefreshInspector();
            };
            if (card != null) control.MouseExited += () =>
            {
                control.TooltipText = tooltip;
                if (!_choosingAttackSkill || _inspectorPreview == null) return;
                _inspectorPreview = null; RefreshInspector();
            };
        }
    }

    private string? SelectedCardId()
    {
        if (_choosingOpeningSkill) return _initialSkillCandidate;
        if (_preparing) return _openingInspection;
        if (ChoosingEffectCards || ChoosingRecycling) return _selected.Count == 1 ? SelectableCards.ElementAtOrDefault(_selected.Single()) : null;
        if (_fieldSelection is { } field) return FieldCardId(field);
        if (_skillContext && _selectedSkill >= 0) return _snapshot!.Players[_snapshot.MySeat].Skills.ElementAtOrDefault(_selectedSkill);
        return _selected.Count == 1 ? SelectableCards.ElementAtOrDefault(_selected.Single()) : null;
    }

    private string? InspectorActivationId() =>
        !_preparing && _snapshot is { } s && s.DecisionPlayer == s.MySeat
            ? s.Phase switch
            {
                Phase.TrapCraftChoice => s.TrapCardId,
                Phase.Decision when s.Decision?.Source == CardCatalog.Robotize.Id =>
                    s.Options.FirstOrDefault(o => o.Move.Kind == DuelMoveKind.Choose)?.CardId,
                Phase.Decision when s.Decision is { } decision && CardCatalog.All.ContainsKey(decision.Source) => decision.Source,
                Phase.TurnBuffDiscard => s.DiscardActivationCard ?? CardCatalog.PlumberStar.Id,
                _ => null
            } : null;

    private string SelectedArtworkRelikario()
    {
        var s = _snapshot!;
        if (_fieldSelection is { } field)
            return field.Zone == FieldZone.Hand ? HandArtworkRelikario(field.Owner, field.Index) : s.Players[field.Owner].RelikarioId;
        if (_selected.Count == 1 && !_preparing && !ChoosingRecycling)
        {
            var index = _selected.Single();
            if (!ChoosingEffectCards) return HandArtworkRelikario(s.MySeat, index);
            var option = _deckSearchOptions.ElementAtOrDefault(index);
            if (option != null && EffectCardOwner(option) is int owner) return s.Players[owner].RelikarioId;
        }
        return s.Players[s.MySeat].RelikarioId;
    }

    private void RefreshInspector()
    {
        var s = _snapshot!;
        var selected = SelectedCardId();
        var activation = selected == null ? InspectorActivationId() : null;
        var id = selected ?? activation;
        var card = id == null ? null : CardCatalog.Get(id);
        var artworkRelikario = SelectedArtworkRelikario();
        int? counters = null;
        if (_fieldSelection is { Zone: FieldZone.Skill } field)
            counters = s.Players[field.Owner].Counters.ElementAtOrDefault(field.Index);
        else if (activation == null && _fieldSelection is { Zone: FieldZone.Equipment } equipment)
            counters = s.Players[equipment.Owner].EquipmentCounters;
        else if (_skillContext && _selectedSkill >= 0)
            counters = s.Players[s.MySeat].Counters.ElementAtOrDefault(_selectedSkill);
        var title = card?.Name ?? "";
        var cost = _preparing ? "" : s.Phase == Phase.TrapCraftChoice && activation != null ? ArenaActionText.CardCost(card!) : _selectionCost;
        var fromPreview = false;
        if (_inspectorPreview is { } preview &&
            (preview.Card != card || preview.Counters != counters || card?.Id == CardCatalog.Attack.Id && _inspectorPreviewRelikario != artworkRelikario))
        {
            card = preview.Card; title = card.Name; counters = preview.Counters; fromPreview = true;
            // While an Attack waits for its skill, the skill being read is what the Attack would use.
            cost = _preparing ? "" : _choosingAttackSkill ? ArenaActionText.CardCost(CardCatalog.Attack) : ArenaActionText.CardCost(card);
        }
        var relikia = _showcase != null && activation == null ? _relikiaPreview : null;
        var relikario = activation == null && relikia == null && !fromPreview ? _relikarioPreview : null;
        if (relikia is { } effectId)
        {
            // The effect's own sheet explains it, so the menu only has to name it.
            var effect = RelikiaEffects.Get(effectId);
            cost = $"{effect.Cost} PR · {effect.ActionLabel}";
            _inspector.Present(null, effect.Name, cost);
            _showcase!.PresentSheet(effectId.ToString(), RelikiaSheet(effectId));
        }
        else if (relikario is { } previewForm)
        {
            var form = previewForm.Form;
            var artwork = previewForm.Evolution || _showcase != null ? RelikarioArt.Card(form.Id, form.Level) : null;
            _inspector.PresentRelikario(form, s.Players[previewForm.Seat].Name, _showcase == null ? artwork : null);
            _showcase?.PresentSheet("relikario:" + form.Id, artwork);
        }
        else
        {
            var owner = card?.Kind == CardKind.Skill ? InspectorSkillOwner(fromPreview) : null;
            // Deciding a Trap shows the two exchanged cards that met its recipe.
            var recipe = !fromPreview && card?.Id == s.TrapCardId && s.Phase == Phase.TrapCraftChoice ? s.TrapIngredients : null;
            _inspector.Present(card, card != null ? title : "", card != null ? cost : "", counters: counters,
                outlook: InspectorOutlook(card, counters, owner), recipe: recipe);
            _showcase?.Present(card, fromPreview ? _inspectorPreviewRelikario : artworkRelikario);
        }
        _contextCost.TooltipText = cost;
        var reading = card != null || relikia != null || relikario != null;
        _actionDock.SizeFlagsVertical = reading ? SizeFlags.ExpandFill : SizeFlags.Fill;
        if (_showcase != null)
        {
            // With no card to read, the rail gives its height to the history.
            _showcase.Visible = reading;
            if (_historyFeed != null) _historyFeed.Visible = !reading && !_preparing;
            if (_railReading != null) _railReading.SizeFlagsVertical = reading || _historyFeed is not { Visible: true } ? SizeFlags.ExpandFill : SizeFlags.Fill;
        }
        if (_showcase == null && _deckChoiceShade == null)
        {
            var interaction = (Control)_actionDock.GetParent().GetParent();
            // The scrollable board and inspector share a column at medium sizes.
            interaction.SizeFlagsVertical = Short || (card != null && _arenaScroll == null) ? SizeFlags.ExpandFill : SizeFlags.Fill;
        }
    }

    private string ContextTitle()
    {
        var s = _snapshot!;
        if (s.Phase == Phase.TurnBuffDiscard && s.DecisionPlayer == s.MySeat) return CardCatalog.Get(s.DiscardActivationCard ?? CardCatalog.PlumberStar.Id).Name;
        // A COMBO has its own card menu, so the popup retains the pending effect's title.
        if (s.Phase == Phase.Decision && s.Decision is { } decision &&
            (Deciding == DecisionKind.Confirm || _cardActionAnchor != null || _fieldSelection == null && _selected.Count == 0))
            return decision.Title;
        if (s.Phase is Phase.TrapCraftChoice && InspectorActivationId() is { } trap)
            return CardCatalog.Get(trap).Name;
        if (s.Winner != null) return "Duelo terminado";
        return s.Phase switch
        {
            Phase.AttackRerollChoice or Phase.DieRerollChoice => $"Tu dado · {s.Die}",
            Phase.TurnBuffDiscard => CardCatalog.Get(s.DiscardActivationCard ?? CardCatalog.PlumberStar.Id).Name,
            Phase.SniperChoice => "Mira de Sniper",
            Phase.DefenseChoice or Phase.EffectDamageChoice => "Defensa", Phase.DestructionResponse => "Proteger carta",
            Phase.CodeResponse => "Respuesta", Phase.RecycleChoice => "Reciclar memoria",
            Phase.MagnetChoice => "Crítico de Magnet",
            _ when SelectedCardId() is { } id => CardCatalog.Get(id).Name,
            _ when _selected.Count > 1 => $"{_selected.Count} cartas seleccionadas",
            _ => ChoosingEffectCards ? "Elige una carta" : _fieldSelection?.Zone == FieldZone.Hand ? _fieldSelection.Value.Owner == s.MySeat ? "Mi mano" : "Mano rival" : s.DecisionPlayer != s.MySeat ? "Decisión del rival" : s.Phase switch
            { Phase.Combat => "Tu combate", Phase.Actions => "Tus acciones", Phase.Draw => "Fase de robo", Phase.Final => "Cierre del turno", _ => "Tu decisión" }
        };
    }

    private string ContextHint()
    {
        var s = _snapshot!; var myTurn = s.DecisionPlayer == s.MySeat;
        if (s.Winner != null) return "El duelo terminó.";
        if (s.Phase == Phase.ErrorResponse) return "";
        if (s.Phase is Phase.TrapCraftChoice) return ArenaGuidance.Instruction(s);
        if (myTurn && s.Decision?.Source == CardCatalog.Robotize.Id &&
            s.Options.FirstOrDefault(o => o.Move.Kind == DuelMoveKind.Choose)?.CardId is { } top)
            return $"Elige qué hacer con {CardCatalog.Get(top).Name}, la carta del tope de tu mazo.";
        if (Deciding == DecisionKind.ChooseTarget && _fieldSelection is { Zone: FieldZone.Hand, Index: < 0 }) return "Se destruirá una carta al azar de esa mano.";
        if (Deciding is DecisionKind.Confirm or DecisionKind.ChooseTarget && _fieldSelection == null) return ArenaGuidance.Instruction(s);
        if (ChoosingRecycling) return $"Reciclaje: {_selected.Count}/{s.Options.First(o => o.Move.Kind == DuelMoveKind.Recycle).RequiredDiscards} cartas elegidas. " + (_selected.Count == s.Options.First(o => o.Move.Kind == DuelMoveKind.Recycle).RequiredDiscards ? "Confirma para reciclar." : "Elige las cartas que faltan.");
        if (s.Phase == Phase.TurnBuffDiscard && myTurn) return $"Descarta {s.ActivationDiscardsRemaining} carta(s) para completar la activación · " + (_selected.Count == 1 ? CardCatalog.Get(SelectableCards[_selected.Single()]!).Name : "elige una carta");
        if (s.Phase == Phase.Discard && myTurn) return $"Descarta {s.DiscardsRemaining} carta(s) para resolver el efecto.";
        if (s.Phase == Phase.Final && myTurn) return s.Players[s.MySeat].HandCount > GameEngine.MaxHandSize
            ? $"Descarta {s.Players[s.MySeat].HandCount - GameEngine.MaxHandSize} carta(s) para terminar el turno." : ArenaGuidance.Instruction(s);
        if (ChoosingCopyTarget) return _selected.Count == 1
            ? $"Vas a copiar {_deckSearchOptions[_selected.Single()].Label}." : "Selecciona un Objeto o Personaje de la RAM.";
        if (ChoosingEffectCards) return _selected.Count == 1 ? ChoiceOrigin(_deckSearchOptions[_selected.Single()]) : "Elige una carta y confirma.";
        if (s.Phase == Phase.DefenseChoice && myTurn)
        {
            var defense = s.Options.FirstOrDefault(o => o.Move.Kind == DuelMoveKind.DestinyDefense && o.Move.Index == _selectedSkill);
            if (defense != null) return $"Descartes {_selected.Count}/{defense.RequiredDiscards} · confirma para defender.";
        }
        if (s.Phase == Phase.MagnetChoice && myTurn && _fieldSelection == null) return "Elige una carta de la mano rival o su Bonus.";
        if (_selected.Count == 1 && !ChoosingEffectCards && !ChoosingRecycling && !_skillContext && _fieldSelection == null)
        {
            var index = _selected.Single();
            var handOptions = s.Options.Where(o => IsHandAction(o.Move.Kind) && o.Move.Index == index).ToArray();
            if (handOptions.Length == 0) return ArenaGuidance.UnavailableCard(s, index);
            if (handOptions.Any(o => o.Move.Kind == DuelMoveKind.Attack))
                return _choosingAttackSkill ? "Elige una habilidad iluminada para atacar." :
                    _selectedSkill < 0 ? "Pulsa Atacar para elegir una habilidad." :
                    "Ataca con " + CardCatalog.Get(s.Players[s.MySeat].Skills[_selectedSkill]).Name + ".";
            return "";
        }
        if (SelectedCardId() != null)
            return _moveButtons.Any(b => b.GetParent() == _moves || b.GetParent() == _cardActions) ? "" :
                _fieldSelection != null || _skillContext ? ArenaGuidance.Instruction(s) : "Esta carta no tiene una activación disponible ahora.";
        return ArenaGuidance.Instruction(s);
    }

    private void RefreshRuntimeGuidance()
    {
        if (_preparing || _snapshot == null || _relikiaPreview != null && _showcase != null) return;
        var text = _snapshot.Winner != null ? _decisionHint : !Connection.Ready
            ? "Conexión interrumpida. Reconecta para continuar."
            : Connection.PlayPending ? "Enviando tu decisión…"
            : IsAnimating ? DiceBusy ? "Resolviendo la tirada…" : "Resolviendo la jugada…"
            : _decisionHint;
        SetDecisionHint(text);
    }

    private DuelSnapshot? _advanceSuggestionSource;
    private bool _advanceSuggested;

    // Asked every frame: the answer only changes with the snapshot.
    private bool AdvanceSuggested(DuelSnapshot snapshot)
    {
        if (!ReferenceEquals(_advanceSuggestionSource, snapshot))
        {
            _advanceSuggestionSource = snapshot;
            _advanceSuggested = ArenaGuidance.SuggestAdvance(snapshot);
        }
        return _advanceSuggested;
    }

    private void RefreshAdvanceInvitation(bool blocked)
        => _advanceButton?.SetInvitation(!blocked && !_preparing && _snapshot != null && AdvanceSuggested(_snapshot));


    private string ChoiceOrigin(DuelOption option) => option.Move.Kind switch
    {
        DuelMoveKind.Corrupt => "Memoria de " + _snapshot!.Players[option.Move.Target].Name,
        DuelMoveKind.Choose when option.Owner is int owner => "Memoria de " + _snapshot!.Players[owner].Name,
        DuelMoveKind.Relikia => "Tu memoria · la copia se corromperá",
        _ => "Confirma la carta seleccionada."
    };
}
