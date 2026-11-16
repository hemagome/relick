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
    private string _decisionHint = "";
    private string _selectionCost = "";
    private readonly List<MenuButton> _actionMenus = [];
    private bool _skillContext, _showReplayMemory;
    private int? _pointsTarget;
    private (int Owner, FieldZone Zone, int Index)? _fieldSelection;
    private (CardDefinition Card, int? Counters)? _inspectorPreview;
    private Vector2? _fieldPress;

    private void ResetInteraction()
    {
        _selected.Clear(); _selectedSkill = -1; _skillContext = false;
        _choosingAttackSkill = false;
        _fieldSelection = null; _pointsTarget = null; _showReplayMemory = false;
        _inspectorPreview = null;
    }

    private void BuildActionArea(VBoxContainer parent)
    {
        var dock = new VBoxContainer(); _actionDock = dock;
        dock.Name = "ActionDock"; dock.AddThemeConstantOverride("separation", Short ? 4 : 8); parent.AddChild(dock);
        _inspector = new CardInspector { Name = "ContextActionBar", ShortScreen = Short }; dock.AddChild(_inspector);
        _contextTitle = _inspector.TitleLabel; _contextCost = _inspector.CostLabel; _selection = _inspector.HintLabel;
        var actions = new HBoxContainer { Name = "DecisionBar" }; _decisionBar = actions; actions.AddThemeConstantOverride("separation", 6); dock.AddChild(actions);
        var scroll = new ScrollContainer { Name = "MovesScroll", CustomMinimumSize = new(0, 50),
            SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _movesScroll = scroll; actions.AddChild(scroll);
        _moves = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _moves.AddThemeConstantOverride("h_separation", 6); _moves.AddThemeConstantOverride("v_separation", 6); scroll.AddChild(_moves);
        _moves.MinimumSizeChanged += FitMovesScroll;
    }

    // Buttons that wrap onto a second row (e.g. pay or skip Huevo Azul's draw) stay visible instead of
    // hiding behind the scrollbar; longer lists still scroll once they pass the cap.
    private void FitMovesScroll()
    {
        var floor = ChoosingMemoryRecovery ? 60 : 50;
        var cap = Short ? 104 : 168;
        var height = Mathf.Clamp(_moves.GetCombinedMinimumSize().Y, floor, Math.Max(floor, cap));
        if (!Mathf.IsEqualApprox(_movesScroll.CustomMinimumSize.Y, height)) _movesScroll.CustomMinimumSize = new(0, height);
    }

    private static bool IsHandAction(DuelMoveKind kind) => kind is DuelMoveKind.Learn or DuelMoveKind.Attack or DuelMoveKind.PlayCode or
        DuelMoveKind.Equip or DuelMoveKind.Exchange or DuelMoveKind.Discard or DuelMoveKind.Placement or DuelMoveKind.TurnBuffDiscard;

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
            button.CustomMinimumSize = new(Math.Clamp(ThemeDB.FallbackFont.GetStringSize(button.Text, HorizontalAlignment.Left, -1, fontSize).X + 30, 64, Short ? 240 : 320), 48);
            if (primary) RelickVisuals.Primary(button);
        }
        var cost = ArenaActionText.Cost(option, s);
        button.TooltipText = button is CardActionButton && cost.Length > 0 ? button.Text + " · " + cost : cost;
        button.SetMeta("option_index", index); button.SetMeta("discards", option.RequiredDiscards);
        button.SetMeta("golden_totem", option.Move.Kind == DuelMoveKind.GoldenTotem);
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

    private void BuildAdvanceControl(Node header)
    {
        var s = _snapshot!;
        _advanceButton = null;
        if (_preparing) return;
        var advance = s.Options.FindIndex(o => o.Move.Kind == DuelMoveKind.Advance);
        if (advance < 0)
        {
            // On the board the bar keeps the button's room, so it does not shrink and shift on the rival's turn.
            if (header.Name == "TurnDock" && s.Winner == null)
                header.AddChild(new Control { Name = "AdvancePlaceholder", CustomMinimumSize = new(192, 48), MouseFilter = MouseFilterEnum.Ignore });
            return;
        }
        _advanceButton = (AdvancePhaseButton)AddOption(header, advance);
        _advanceButton.Name = "AdvanceStage";
        _advanceButton.TooltipText = AdvanceSuggested(s)
            ? "No quedan jugadas disponibles. " + ArenaGuidance.NextStep(s) : "Avanza cuando termines tus jugadas de esta fase.";
    }

    private void BuildContextMoves()
    {
        var s = _snapshot!;
        var indices = Enumerable.Range(0, s.Options.Count).Where(i => s.Options[i].Move.Kind is not (DuelMoveKind.Advance or DuelMoveKind.Concede or DuelMoveKind.Evolve)).ToList();
        // Responses must remain visible even with no hand card (or another card) selected.
        if (s.Phase == Phase.ErrorResponse)
            indices = indices.Where(i => s.Options[i].Move.Kind is DuelMoveKind.PlayCode or DuelMoveKind.PassError).ToList();
        else if (s.Phase == Phase.TrapCraftChoice)
            indices = indices.Where(i => s.Options[i].Move.Kind == DuelMoveKind.TrapCraft).ToList();
        else if (s.Phase == Phase.ZahiraTarget)
            indices = indices.Where(i => s.Options[i].Move.Kind == DuelMoveKind.ZahiraTarget).ToList();
        else if (s.Phase == Phase.BlueEggDrawChoice)
            indices = indices.Where(i => s.Options[i].Move.Kind == DuelMoveKind.BlueEggDraw).ToList();
        else if (Deciding == DecisionKind.Confirm)
            indices = indices.Where(i => s.Options[i].Move.Kind == DuelMoveKind.Choose).ToList();
        else if (ChoosingEffectCards)
            indices = indices.Where(i => _selected.Count == 1 && ReferenceEquals(_deckSearchOptions[_selected.Single()], s.Options[i])).ToList();
        else if (_fieldSelection is { } field)
            indices = indices.Where(i => MatchesField(s.Options[i], field)).ToList();
        else
        {
            indices.RemoveAll(i => s.Options[i].Move.Kind is DuelMoveKind.Relikia or DuelMoveKind.FieldCode or DuelMoveKind.Destroy or DuelMoveKind.Magnet ||
                s.Options[i].Move.Kind == DuelMoveKind.Choose && s.Options[i].Field != null);
            indices.RemoveAll(i => IsHandAction(s.Options[i].Move.Kind) && !_selected.Contains(s.Options[i].Move.Index));
            if (_selected.Count > 1 || _skillContext) indices.RemoveAll(i => IsHandAction(s.Options[i].Move.Kind));
            if (!_skillContext && indices.Any(i => IsHandAction(s.Options[i].Move.Kind)))
                indices.RemoveAll(i => !IsHandAction(s.Options[i].Move.Kind));
            indices.RemoveAll(i => s.Options[i].Move.Kind == DuelMoveKind.Attack &&
                (_choosingAttackSkill || s.Options[i].Move.Value != _selectedSkill));
            if (_skillContext)
                indices = indices.Where(i => s.Options[i].Move.Kind is DuelMoveKind.DestinyDefense or DuelMoveKind.ProtectDestruction && s.Options[i].Move.Index == _selectedSkill).ToList();
            else indices.RemoveAll(i => s.Options[i].Move.Kind is DuelMoveKind.DestinyDefense || s.Options[i].Move.Kind == DuelMoveKind.ProtectDestruction && s.Options[i].Move.Index >= 0);
            indices.RemoveAll(i => s.Options[i].Move.Kind == DuelMoveKind.GoldenTotem && s.Options[i].RequiredDiscards != _selected.Count);
            if (s.Phase == Phase.PointsChoice)
                indices = _pointsTarget.HasValue ? indices.Where(i => s.Options[i].Move.Target == _pointsTarget).ToList() : [];
        }
        var chooseAttackSkill = !_choosingAttackSkill && _selectedSkill < 0 && SelectedAttacks().Any();
        var cardAnchoring = s.Phase is not (Phase.ErrorResponse or Phase.TrapCraftChoice or Phase.ZahiraTarget or Phase.BlueEggDrawChoice) && Deciding != DecisionKind.Confirm;
        var handActions = cardAnchoring && !ChoosingEffectCards && !ChoosingRecycling &&
            !_skillContext && _fieldSelection == null && _selected.Count == 1;
        if (handActions)
            _cardActionAnchor = _handCards.ElementAtOrDefault(_selected.Single());
        else if (cardAnchoring && _fieldSelection is { } target)
            _cardActionAnchor = FieldActionAnchor(target);
        else if (_skillContext && indices.Any(i => s.Options[i].Move.Kind == DuelMoveKind.ProtectDestruction))
            _cardActionAnchor = _ownSkills.FirstOrDefault(tile => tile.SkillIndex == _selectedSkill && tile.Skill != null);

        // A pending die has one principal action and one menu of real modifiers.
        var die = indices.Where(i => s.Options[i].Move.Kind is DuelMoveKind.AttackDie or DuelMoveKind.EffectDie && !s.Options[i].Move.Flag).ToArray();
        var main = indices.FirstOrDefault(i => s.Options[i].Move.Kind is DuelMoveKind.PlayCode or DuelMoveKind.Equip or DuelMoveKind.Learn or DuelMoveKind.Attack or
            DuelMoveKind.Recycle or DuelMoveKind.RecoverMemory or DuelMoveKind.SearchDeck or DuelMoveKind.GoldenTotem or DuelMoveKind.TurnBuffDiscard or DuelMoveKind.DestinyDefense ||
            s.Options[i].Move.Kind is DuelMoveKind.TrapCraft or DuelMoveKind.BlueEggDraw && s.Options[i].Move.Flag ||
            s.Options[i].Move.Kind == DuelMoveKind.Choose && (s.Options[i].Primary || Deciding == DecisionKind.ChooseCard), -1);
        if (main < 0 && die.Length > 0) main = die[0];
        if (_showcase != null && ChoosingMemoryRecovery)
        {
            var confirm = main >= 0 ? AddOption(_moves, main, primary: true) : new Button
            {
                Text = "Recuperar", Disabled = true, TooltipText = "Selecciona una carta de la memoria."
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
            AddChoiceMenu(_moves, "Modificar…", indices.Except(die));
        }
        else
        {
            if (chooseAttackSkill && _cardActionAnchor != null) AddAttackSkillChoice();
            foreach (var index in indices)
            {
                var option = s.Options[index];
                var besideCard = _cardActionAnchor != null && (handActions ? IsHandAction(option.Move.Kind) :
                    _fieldSelection != null || option.Move.Kind == DuelMoveKind.ProtectDestruction);
                AddOption(besideCard ? _cardActions : _moves, index, primary: index == main);
            }
        }
        if (!_skillContext && _fieldSelection == null && !ChoosingEffectCards && !indices.Any(i => IsHandAction(s.Options[i].Move.Kind)))
        {
            AddChoiceMenu(_moves, s.Phase == Phase.DestructionResponse ? "Sustituir…" : "Defender…",
                s.Options.Select((o, i) => (o, i)).Where(c => c.o.Move.Kind == DuelMoveKind.DestinyDefense || c.o.Move.Kind == DuelMoveKind.ProtectDestruction && c.o.Move.Index >= 0).Select(c => c.i), true);
        }
        if (s.Phase == Phase.PointsChoice && _pointsTarget == null)
            foreach (var player in s.Options.Where(o => o.Move.Kind == DuelMoveKind.Points).Select(o => o.Move.Target).Distinct().OrderBy(p => p != s.MySeat))
            {
                var seat = player;
                AddLocalButton(_moves, seat == s.MySeat ? "Mis PR" : "PR del rival", () => { _pointsTarget = seat; BuildMoves(); });
            }
        PositionCardActions();
        var costs = indices.Select(i => ArenaActionText.Cost(s.Options[i], s)).Where(v => v.Length > 0).Distinct().ToArray();
        _selectionCost = main >= 0 ? (costs.Length > 1 ? ArenaActionText.Label(s.Options[main], s) + ": " : "") + ArenaActionText.Cost(s.Options[main], s)
            : costs.Length == 1 ? costs[0] : "";
        if (_selectionCost.Length == 0 && !_skillContext && _fieldSelection == null && _selected.Count == 1 &&
            s.Phase is Phase.Draw or Phase.Combat or Phase.Actions && SelectedCardId() is { } selected)
            _selectionCost = ArenaActionText.CardCost(CardCatalog.Get(selected));
        _selection.Text = ContextHint(); _selection.TooltipText = _selection.Text;
        RefreshInspector();
        _decisionHint = _selection.Text;
    }

    private Button AddLocalButton(Node parent, string label, Action pressed)
    {
        var button = ArenaPresentation.Button(parent, label, pressed); button.TooltipText = label;
        button.AddThemeFontSizeOverride("font_size", Short ? 13 : 15); return button;
    }

    private void ClearContext()
    {
        var gallery = _showReplayMemory; ResetInteraction();
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
        if (_preparing)
        {
            _inspectorPreview = null;
            _openingInspection = null;
            RefreshInspector();
        }
        else if (_inspectorPreview != null || HasContextSelection || _showReplayMemory) ClearContext();
        else return;
        GetViewport().SetInputAsHandled();
    }

    private bool HasContextSelection => _selected.Count > 0 || _selectedSkill >= 0 || _fieldSelection != null ||
        _pointsTarget != null || _preparing && SelectedCardId() != null;

    private void BindInspectorPreviews()
    {
        foreach (var control in _root!.FindChildren("*", "Button", true, false).OfType<Button>())
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
            int? counters = control is SkillTile skill ? skill.Counters : null;
            var owner = SkillTileOwner(control);
            var tooltip = control.TooltipText;
            control.MouseEntered += () =>
            {
                // Choosing the skill of an Attack is exactly when its damage table is worth reading.
                var choosingSkill = _choosingAttackSkill && control is SkillTile;
                if (IsAnimating || HasContextSelection && !choosingSkill || InspectorActivationId() != null) return;
                if (card != null) control.TooltipText = "";
                (CardDefinition, int?)? preview = card == null ? null : (card, counters);
                if (_inspectorPreview == preview && _inspectorPreviewOwner == owner) return;
                _inspectorPreview = preview; _inspectorPreviewOwner = owner;
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
                Phase.ErrorResponse => CardCatalog.LoadError.Id,
                Phase.GoldenTotemChoice => CardCatalog.GoldenTotem.Id,
                Phase.TrapCraftChoice => s.TrapCardId,
                Phase.ZahiraTarget => CardCatalog.ZahiraFlair.Id,
                Phase.BlueEggDrawChoice => CardCatalog.BlueEgg.Id,
                Phase.Decision => s.Decision?.Source,
                Phase.TurnBuffDiscard => s.DiscardActivationCard ?? CardCatalog.PlumberStar.Id,
                _ => null
            } : null;

    private void RefreshInspector()
    {
        var s = _snapshot!;
        var activation = InspectorActivationId();
        var id = activation ?? SelectedCardId();
        var card = id == null ? null : CardCatalog.Get(id);
        int? counters = null;
        if (_fieldSelection is { Zone: FieldZone.Skill } field)
            counters = s.Players[field.Owner].Counters.ElementAtOrDefault(field.Index);
        else if (activation == null && _fieldSelection is { Zone: FieldZone.Equipment } equipment && card?.Id == CardCatalog.SuperMagnet.Id)
            counters = s.Players[equipment.Owner].EquipmentCounters;
        else if (_skillContext && _selectedSkill >= 0)
            counters = s.Players[s.MySeat].Counters.ElementAtOrDefault(_selectedSkill);
        var title = _preparing ? card?.Name ?? (_choosingOpeningSkill ? "Elige una habilidad" : "Mano inicial") : ContextTitle();
        var cost = _preparing ? "" : s.Phase == Phase.TrapCraftChoice && activation != null ? ArenaActionText.CardCost(card!) : _selectionCost;
        var fromPreview = false;
        if (activation == null && _inspectorPreview is { } preview && (preview.Card != card || preview.Counters != counters))
        {
            card = preview.Card; title = card.Name; counters = preview.Counters; fromPreview = true;
            // While an Attack waits for its skill, the skill being read is what the Attack would use.
            cost = _preparing ? "" : _choosingAttackSkill ? ArenaActionText.CardCost(CardCatalog.Attack) : ArenaActionText.CardCost(card);
        }
        var relikia = _showcase != null && activation == null ? _relikiaPreview : null;
        if (relikia is { } effectId)
        {
            // The effect's own sheet explains it, so the menu only has to name it.
            var effect = RelikiaEffects.Get(effectId);
            cost = $"{effect.Cost} PR · {effect.ActionLabel}";
            _inspector.Present(null, effect.Name, cost);
            _showcase!.PresentSheet(effectId.ToString(), RelikiaSheet(effectId));
        }
        else
        {
            var owner = card?.Kind == CardKind.Skill ? InspectorSkillOwner(fromPreview) : null;
            // Deciding a Trap shows the two exchanged cards that met its recipe.
            var recipe = activation != null && s.Phase == Phase.TrapCraftChoice ? s.TrapIngredients : null;
            _inspector.Present(card, title, cost, _selection.Text, counters, InspectorOutlook(card, counters, owner), recipe);
            _showcase?.Present(card);
        }
        _contextCost.TooltipText = cost;
        var reading = card != null || relikia != null;
        _actionDock.SizeFlagsVertical = reading ? SizeFlags.ExpandFill : SizeFlags.Fill;
        if (_showcase != null)
        {
            // With no card to read, the rail gives its height to the history.
            _showcase.Visible = reading;
            if (_historyFeed != null) _historyFeed.Visible = !reading && !_preparing;
            if (_railReading != null) _railReading.SizeFlagsVertical = reading || _historyFeed is not { Visible: true } ? SizeFlags.ExpandFill : SizeFlags.Fill;
        }
        if (_showcase == null)
        {
            var interaction = (Control)_actionDock.GetParent().GetParent();
            // The scrollable board and inspector share a column at medium sizes.
            interaction.SizeFlagsVertical = Short || (card != null && _arenaScroll == null) ? SizeFlags.ExpandFill : SizeFlags.Fill;
        }
        var hasDecisions = _moves.GetChildCount() > 0;
        _movesScroll.Visible = hasDecisions;
        _movesScroll.MouseFilter = hasDecisions ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        _decisionBar.Visible = hasDecisions;
    }

    private string ContextTitle()
    {
        var s = _snapshot!;
        if (s.Phase == Phase.TurnBuffDiscard && s.DecisionPlayer == s.MySeat) return CardCatalog.Get(s.DiscardActivationCard ?? CardCatalog.PlumberStar.Id).Name;
        if (s.Phase == Phase.GoldenTotemChoice && s.DecisionPlayer == s.MySeat) return "Tótem de Oro";
        if (s.Phase == Phase.BlueEggDrawChoice) return CardCatalog.BlueEgg.Name;
        // The card that asks names the decision until the player picks what to answer with.
        if (s.Phase == Phase.Decision && s.Decision is { } decision && (Deciding == DecisionKind.Confirm || _fieldSelection == null && _selected.Count == 0))
            return decision.Title;
        if (s.Phase is Phase.TrapCraftChoice or Phase.ZahiraTarget && InspectorActivationId() is { } trap)
            return CardCatalog.Get(trap).Name;
        if (s.Phase == Phase.ErrorResponse && s.DecisionPlayer == s.MySeat) return CardCatalog.LoadError.Name;
        var id = SelectedCardId(); if (id != null) return CardCatalog.Get(id).Name;
        if (s.Winner != null) return "Duelo terminado";
        if (_selected.Count > 1) return $"{_selected.Count} cartas seleccionadas";
        return s.Phase switch
        {
            Phase.AttackRerollChoice or Phase.DieRerollChoice => $"Tu dado · {s.Die}",
            Phase.GoldenTotemChoice => "Tótem de Oro", Phase.TurnBuffDiscard => CardCatalog.Get(s.DiscardActivationCard ?? CardCatalog.PlumberStar.Id).Name,
            Phase.TotemChoice => "Tótem de Salvación", Phase.SniperChoice => "Mira de Sniper",
            Phase.DefenseChoice or Phase.EffectDamageChoice => "Defensa", Phase.DestructionResponse => "Proteger carta",
            Phase.CodeResponse or Phase.ErrorResponse => "Respuesta", Phase.RecycleChoice => "Reciclar memoria",
            Phase.PointsChoice => _pointsTarget is int target ? s.Players[target].Name : "Relick Points",
            Phase.MagnetChoice => "Crítico de Magnet",
            _ => ChoosingEffectCards ? "Elige una carta" : _fieldSelection?.Zone == FieldZone.Hand ? _fieldSelection.Value.Owner == s.MySeat ? "Mi mano" : "Mano rival" : s.DecisionPlayer != s.MySeat ? "Decisión del rival" : s.Phase switch
            { Phase.Combat => "Tu combate", Phase.Actions => "Tus acciones", Phase.Draw => "Fase de robo", Phase.Final => "Cierre del turno", _ => "Tu decisión" }
        };
    }

    private string ContextHint()
    {
        var s = _snapshot!; var myTurn = s.DecisionPlayer == s.MySeat;
        if (s.Winner != null) return "El duelo terminó.";
        if (s.Phase == Phase.ErrorResponse && myTurn) return "Anula la activación o deja que se resuelva.";
        if (s.Phase is Phase.TrapCraftChoice or Phase.ZahiraTarget or Phase.BlueEggDrawChoice) return ArenaGuidance.Instruction(s);
        if (Deciding == DecisionKind.ChooseTarget && _fieldSelection?.Zone == FieldZone.Hand) return "Se destruirá una carta al azar de esa mano.";
        if (Deciding is DecisionKind.Confirm or DecisionKind.ChooseTarget && _fieldSelection == null) return ArenaGuidance.Instruction(s);
        if (ChoosingRecycling) return $"Reciclaje: {_selected.Count}/{s.Options.First(o => o.Move.Kind == DuelMoveKind.Recycle).RequiredDiscards} cartas elegidas. " + (_selected.Count == s.Options.First(o => o.Move.Kind == DuelMoveKind.Recycle).RequiredDiscards ? "Confirma para reciclar." : "Elige las cartas que faltan.");
        if (s.Phase == Phase.GoldenTotemChoice && myTurn) return $"Elige hasta dos Tótems · {_selected.Count}/2 seleccionados.";
        if (s.Phase == Phase.TurnBuffDiscard && myTurn) return $"Descarta {s.ActivationDiscardsRemaining} carta(s) para completar la activación · " + (_selected.Count == 1 ? CardCatalog.Get(SelectableCards[_selected.Single()]!).Name : "elige una carta");
        if (s.Phase == Phase.Discard && myTurn) return $"Descarta {s.DiscardsRemaining} carta(s) para resolver el efecto.";
        if (s.Phase == Phase.Final && myTurn) return s.Players[s.MySeat].HandCount > GameEngine.MaxHandSize
            ? $"Descarta {s.Players[s.MySeat].HandCount - GameEngine.MaxHandSize} carta(s) para terminar el turno." : ArenaGuidance.Instruction(s);
        if (ChoosingEffectCards) return _selected.Count == 1 ? ChoiceOrigin(_deckSearchOptions[_selected.Single()]) : "Elige una carta y confirma.";
        if (s.Phase == Phase.DefenseChoice && myTurn)
        {
            var defense = s.Options.FirstOrDefault(o => o.Move.Kind == DuelMoveKind.DestinyDefense && o.Move.Index == _selectedSkill);
            if (defense != null) return $"Descartes {_selected.Count}/{defense.RequiredDiscards} · confirma para defender.";
        }
        if (s.Phase == Phase.DestroyChoice && myTurn && _fieldSelection == null) return "Elige un objetivo en el tablero.";
        if (s.Phase == Phase.DestroyChoice && myTurn && _fieldSelection?.Zone == FieldZone.Hand) return "Se destruirá una carta al azar de esa mano.";
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
            : IsAnimating ? _die.IsBusy ? "Resolviendo el dado…" : "Resolviendo la jugada…"
            : _decisionHint;
        _inspector.SetHint(text);
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
        DuelMoveKind.RecoverMemory or DuelMoveKind.MindResetMemory or DuelMoveKind.Corrupt => "Memoria de " + _snapshot!.Players[option.Move.Target].Name,
        DuelMoveKind.Relikia => "Tu memoria · la copia se corromperá",
        _ => "Confirma la carta seleccionada."
    };
}
