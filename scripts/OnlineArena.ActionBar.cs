using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    private Label _contextTitle = null!, _contextCost = null!;
    private CardInspector _inspector = null!;
    private HBoxContainer _turnControls = null!;
    private Button? _advanceButton;
    private readonly List<MenuButton> _actionMenus = [];
    private bool _skillContext, _showReplayMemory;
    private int? _pointsTarget;
    private (int Owner, FieldZone Zone, int Index)? _fieldSelection;

    private void ResetInteraction()
    {
        _selected.Clear(); _selectedSkill = -1; _skillContext = false;
        _fieldSelection = null; _pointsTarget = null; _showReplayMemory = false;
    }

    private void BuildActionArea(VBoxContainer parent)
    {
        var dock = new VBoxContainer();
        dock.Name = "ActionDock"; dock.AddThemeConstantOverride("separation", Short ? 4 : 8); parent.AddChild(dock);
        _inspector = new CardInspector { Name = "ContextActionBar", ShortScreen = Short }; dock.AddChild(_inspector);
        _contextTitle = _inspector.TitleLabel; _contextCost = _inspector.CostLabel; _selection = _inspector.HintLabel;
        var actions = new HBoxContainer { Name = "InspectorActions" }; actions.AddThemeConstantOverride("separation", 6); dock.AddChild(actions);
        var scroll = new ScrollContainer { Name = "MovesScroll", CustomMinimumSize = new(0, 50),
            SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        actions.AddChild(scroll);
        _moves = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _moves.AddThemeConstantOverride("h_separation", 6); _moves.AddThemeConstantOverride("v_separation", 6); scroll.AddChild(_moves);
        _turnControls = new HBoxContainer { Name = "TurnControls" };
        _turnControls.AddThemeConstantOverride("separation", 6); actions.AddChild(_turnControls);
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

    private Button AddOption(Node parent, int index, string? label = null)
    {
        var s = _snapshot!; var option = s.Options[index];
        var button = ArenaPresentation.Button(parent, label ?? ArenaActionText.Label(option, s), () => PlayOption(index, s));
        button.Name = "DuelOption" + index; button.AddThemeFontSizeOverride("font_size", Short ? 13 : 15);
        button.AutowrapMode = TextServer.AutowrapMode.Off;
        button.CustomMinimumSize = new(Math.Clamp(ThemeDB.FallbackFont.GetStringSize(button.Text, HorizontalAlignment.Left, -1, Short ? 13 : 15).X + 30, 64, Short ? 240 : 320), 48);
        button.TooltipText = ArenaActionText.Cost(option, s);
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

    private void BuildTurnControls()
    {
        var s = _snapshot!;
        var relikia = s.Options.Select((o, i) => (o, i)).Where(c => c.o.Move.Kind == DuelMoveKind.Relikia).ToArray();
        if (relikia.Length > 0)
        {
            var menu = new MenuButton { Name = "RelikiaActions", Text = "Relikia", CustomMinimumSize = new(0, 48) };
            _turnControls.AddChild(menu); _actionMenus.Add(menu); var popup = menu.GetPopup(); ArenaViewport.TouchMenu(popup);
            foreach (var c in relikia.Where(c => c.o.Move.Value != (int)RelikiaEffect.ReplayCode))
                popup.AddItem(ArenaActionText.Label(c.o, s) + " · " + ArenaActionText.Cost(c.o, s), c.i);
            if (relikia.Any(c => c.o.Move.Value == (int)RelikiaEffect.ReplayCode)) popup.AddItem("Activar desde memoria · 2 acciones + 400 PR", s.Options.Count);
            popup.IdPressed += id =>
            {
                if ((int)id == s.Options.Count)
                { _showReplayMemory = true; _selected.Clear(); _fieldSelection = null; _skillContext = false; Render(); }
                else PlayOption((int)id, s);
            };
        }
        _turnControls.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var advance = s.Options.FindIndex(o => o.Move.Kind == DuelMoveKind.Advance);
        _advanceButton = advance < 0 ? null : AddOption(_turnControls, advance);
        if (_advanceButton != null) _advanceButton.Name = "AdvanceStage";
        _turnControls.Visible = !_preparing && (advance >= 0 || relikia.Length > 0);
    }

    private void BuildContextMoves()
    {
        var s = _snapshot!;
        var indices = Enumerable.Range(0, s.Options.Count).Where(i => s.Options[i].Move.Kind is not (DuelMoveKind.Advance or DuelMoveKind.Concede or DuelMoveKind.Evolve)).ToList();
        // Responses must remain visible even with no hand card (or another card) selected.
        if (s.Phase == Phase.ErrorResponse)
            indices = indices.Where(i => s.Options[i].Move.Kind is DuelMoveKind.PlayCode or DuelMoveKind.PassError).ToList();
        else if (ChoosingEffectCards)
            indices = indices.Where(i => _selected.Count == 1 && ReferenceEquals(_deckSearchOptions[_selected.Single()], s.Options[i])).ToList();
        else if (_fieldSelection is { } field)
            indices = indices.Where(i => MatchesField(s.Options[i].Move, field)).ToList();
        else
        {
            indices.RemoveAll(i => s.Options[i].Move.Kind is DuelMoveKind.Relikia or DuelMoveKind.FieldCode or DuelMoveKind.Destroy or DuelMoveKind.Magnet);
            indices.RemoveAll(i => IsHandAction(s.Options[i].Move.Kind) && !_selected.Contains(s.Options[i].Move.Index));
            if (_selected.Count > 1 || _skillContext) indices.RemoveAll(i => IsHandAction(s.Options[i].Move.Kind));
            if (!_skillContext && indices.Any(i => IsHandAction(s.Options[i].Move.Kind)))
                indices.RemoveAll(i => !IsHandAction(s.Options[i].Move.Kind));
            indices.RemoveAll(i => s.Options[i].Move.Kind == DuelMoveKind.Attack && s.Options[i].Move.Value != _selectedSkill);
            if (_skillContext)
                indices = indices.Where(i => s.Options[i].Move.Kind is DuelMoveKind.DestinyDefense or DuelMoveKind.ProtectDestruction && s.Options[i].Move.Index == _selectedSkill).ToList();
            else indices.RemoveAll(i => s.Options[i].Move.Kind is DuelMoveKind.DestinyDefense || s.Options[i].Move.Kind == DuelMoveKind.ProtectDestruction && s.Options[i].Move.Index >= 0);
            indices.RemoveAll(i => s.Options[i].Move.Kind == DuelMoveKind.GoldenTotem && s.Options[i].RequiredDiscards != _selected.Count);
            if (s.Phase == Phase.PointsChoice)
                indices = _pointsTarget.HasValue ? indices.Where(i => s.Options[i].Move.Target == _pointsTarget).ToList() : [];
        }
        // A pending die has one principal action and one menu of real modifiers.
        var die = indices.Where(i => s.Options[i].Move.Kind is DuelMoveKind.AttackDie or DuelMoveKind.EffectDie && !s.Options[i].Move.Flag).ToArray();
        if (die.Length > 0)
        {
            AddOption(_moves, die[0]); AddChoiceMenu(_moves, "Modificar…", indices.Except(die));
        }
        else foreach (var index in indices) AddOption(_moves, index);
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
        var inspect = SelectedCardId();
        if (inspect != null) AddLocalButton(_moves, "ⓘ", () => ShowCards("Carta", [inspect]), "Leer carta");
        if (_selected.Count > 0 || _fieldSelection != null || _skillContext || _showReplayMemory || _pointsTarget != null)
            AddLocalButton(_moves, "×", ClearContext, "Limpiar selección");
        var main = indices.FirstOrDefault(i => s.Options[i].Move.Kind is DuelMoveKind.PlayCode or DuelMoveKind.Equip or DuelMoveKind.Learn or DuelMoveKind.Attack or
            DuelMoveKind.Recycle or DuelMoveKind.RecoverMemory or DuelMoveKind.SearchDeck or DuelMoveKind.GoldenTotem or DuelMoveKind.TurnBuffDiscard or DuelMoveKind.DestinyDefense, -1);
        if (main >= 0)
        {
            var primary = _moveButtons.LastOrDefault(b => (int)b.GetMeta("option_index", -1) == main);
            if (primary != null)
            {
                primary.AddThemeStyleboxOverride("normal", ArenaPresentation.Box("5ed6b3", ArenaPresentation.Mint, 8));
                primary.AddThemeStyleboxOverride("hover", ArenaPresentation.Box("7ae6c4", ArenaPresentation.Mint, 8));
                primary.AddThemeStyleboxOverride("pressed", ArenaPresentation.Box("3fb18f", ArenaPresentation.Mint, 8));
                primary.AddThemeColorOverride("font_color", new("102c2a")); primary.AddThemeColorOverride("font_hover_color", new("102c2a"));
                primary.AddThemeColorOverride("font_pressed_color", new("102c2a"));
            }
        }
        _contextTitle.Text = ContextTitle(); _contextTitle.TooltipText = _contextTitle.Text;
        var costs = indices.Select(i => ArenaActionText.Cost(s.Options[i], s)).Where(v => v.Length > 0).Distinct().ToArray();
        _contextCost.Text = main >= 0 ? (costs.Length > 1 ? ArenaActionText.Label(s.Options[main], s) + ": " : "") + ArenaActionText.Cost(s.Options[main], s)
            : costs.Length == 1 ? costs[0] : "";
        if (_contextCost.Text.Length == 0 && !_skillContext && _fieldSelection == null && _selected.Count == 1 &&
            s.Phase is Phase.Draw or Phase.Combat or Phase.Actions && SelectedCardId() is { } selected)
            _contextCost.Text = ArenaActionText.CardCost(CardCatalog.Get(selected));
        _contextCost.Visible = _contextCost.Text.Length > 0; _contextCost.TooltipText = _contextCost.Text;
        _selection.Text = ContextHint(); _selection.TooltipText = _selection.Text;
        RefreshInspector();
    }

    private Button AddLocalButton(Node parent, string label, Action pressed, string? hint = null)
    {
        var button = ArenaPresentation.Button(parent, label, pressed); button.TooltipText = hint ?? label;
        if (label is "ⓘ" or "×") button.CustomMinimumSize = new(48, 48);
        button.AddThemeFontSizeOverride("font_size", Short ? 13 : 15); return button;
    }

    private void ClearContext()
    {
        var gallery = _showReplayMemory; ResetInteraction();
        if (gallery) Render(); else RefreshSelection();
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

    private void RefreshInspector()
    {
        var s = _snapshot!;
        var id = SelectedCardId();
        // During mandatory responses the action context is the activation, not a discard candidate.
        if (!_preparing && s.DecisionPlayer == s.MySeat)
            id = s.Phase switch
            {
                Phase.ErrorResponse => CardCatalog.LoadError.Id,
                Phase.GoldenTotemChoice => CardCatalog.GoldenTotem.Id,
                Phase.TurnBuffDiscard => s.DiscardActivationCard ?? CardCatalog.PlumberStar.Id,
                _ => id
            };
        var card = id == null ? null : CardCatalog.Get(id);
        int? counters = null;
        if (_fieldSelection is { Zone: FieldZone.Skill } field)
            counters = s.Players[field.Owner].Counters.ElementAtOrDefault(field.Index);
        else if (_skillContext && _selectedSkill >= 0)
            counters = s.Players[s.MySeat].Counters.ElementAtOrDefault(_selectedSkill);
        var title = _preparing ? card?.Name ?? (_choosingOpeningSkill ? "Elige una habilidad" : "Mano inicial") : ContextTitle();
        _inspector.Present(card, title, _preparing ? "" : _contextCost.Text, _selection.Text, counters);
    }

    private string ContextTitle()
    {
        var s = _snapshot!;
        if (s.Phase == Phase.TurnBuffDiscard && s.DecisionPlayer == s.MySeat) return CardCatalog.Get(s.DiscardActivationCard ?? CardCatalog.PlumberStar.Id).Name;
        if (s.Phase == Phase.GoldenTotemChoice && s.DecisionPlayer == s.MySeat) return "Tótem de Oro";
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
            _ => ChoosingEffectCards ? "Elige una carta" : _fieldSelection?.Zone == FieldZone.Hand ? _fieldSelection.Value.Owner == s.MySeat ? "Mi mano" : "Mano rival" : "Tu mano"
        };
    }

    private string ContextHint()
    {
        var s = _snapshot!; var myTurn = s.DecisionPlayer == s.MySeat;
        if (s.Winner != null) return "El duelo terminó.";
        if (s.Phase == Phase.ErrorResponse && myTurn) return "Anula la activación o deja que se resuelva.";
        if (ChoosingRecycling) return $"Selecciona {_selected.Count}/{s.Options.First(o => o.Move.Kind == DuelMoveKind.Recycle).RequiredDiscards} cartas para reciclar.";
        if (s.Phase == Phase.GoldenTotemChoice && myTurn) return $"Elige hasta dos Tótems · {_selected.Count}/2 seleccionados.";
        if (s.Phase == Phase.TurnBuffDiscard && myTurn) return $"Faltan {s.ActivationDiscardsRemaining} descarte(s) · " + (_selected.Count == 1 ? CardCatalog.Get(SelectableCards[_selected.Single()]!).Name : "elige una carta");
        if (s.Phase == Phase.Discard && myTurn) return $"Descarta {s.DiscardsRemaining} carta(s) para resolver el efecto.";
        if (s.Phase == Phase.Final && myTurn) return s.Players[s.MySeat].HandCount > GameEngine.MaxHandSize
            ? $"Descarta {s.Players[s.MySeat].HandCount - GameEngine.MaxHandSize} carta(s) para terminar el turno." : "Puedes finalizar el turno o usar un COMBO.";
        if (ChoosingEffectCards) return _selected.Count == 1 ? ChoiceOrigin(_deckSearchOptions[_selected.Single()]) : "Elige una carta y confirma.";
        if (s.Phase == Phase.DefenseChoice && myTurn)
        {
            var defense = s.Options.FirstOrDefault(o => o.Move.Kind == DuelMoveKind.DestinyDefense && o.Move.Index == _selectedSkill);
            if (defense != null) return $"Descartes {_selected.Count}/{defense.RequiredDiscards} · confirma para defender.";
        }
        if (s.Phase == Phase.DestroyChoice && _fieldSelection == null) return "Elige un objetivo en el tablero.";
        if (s.Phase == Phase.DestroyChoice && _fieldSelection?.Zone == FieldZone.Hand) return "Se destruirá una carta al azar de esa mano.";
        if (s.Phase == Phase.MagnetChoice && _fieldSelection == null) return "Elige una carta de la mano rival o su Bonus.";
        if (_selected.Count == 1 && s.Players[s.MySeat].Hand.ElementAtOrDefault(_selected.Single()) == CardCatalog.Attack.Id && !ChoosingEffectCards)
            return _selectedSkill < 0 ? "Elige una habilidad iluminada." : "Usando " + CardCatalog.Get(s.Players[s.MySeat].Skills[_selectedSkill]).Name;
        if (SelectedCardId() != null) return _moveButtons.Any(b => b.GetParent() == _moves) ? "" : "Sin acciones disponibles para esta carta ahora.";
        return myTurn ? "Elige una carta para ver sus acciones." : "Esperando al rival · puedes seleccionar un COMBO.";
    }

    private string ChoiceOrigin(DuelOption option) => option.Move.Kind switch
    {
        DuelMoveKind.RecoverMemory or DuelMoveKind.MindResetMemory or DuelMoveKind.Corrupt => "Memoria de " + _snapshot!.Players[option.Move.Target].Name,
        DuelMoveKind.Relikia => "Tu memoria · la copia se corromperá",
        _ => "Confirma la carta seleccionada."
    };
}
