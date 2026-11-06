using Godot;
using Relick.Core;

namespace Relick;

public partial class Main
{
    private void Render()
    {
        if (_root != null)
        {
            _yuno.GetParent()?.RemoveChild(_yuno);
            _enemyYuno.GetParent()?.RemoveChild(_enemyYuno);
            foreach (var meter in _lifeMeters) meter.GetParent()?.RemoveChild(meter);
            if (_handScroll != null) _handScrollPosition = _handScroll.ScrollHorizontal;
            if (_opponentHandScroll != null) _opponentHandScrollPosition = _opponentHandScroll.ScrollHorizontal;
            RemoveChild(_root);
            _root.QueueFree();
        }
        _root = new VBoxContainer { Name = "DuelInterface" };
        _root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _root.OffsetLeft = 24; _root.OffsetRight = -24;
        _root.OffsetTop = 16; _root.OffsetBottom = -14;
        _root.AddThemeConstantOverride("separation", 8);
        AddChild(_root);
        BuildHeader();
        BuildPhases();
        var arena = new HBoxContainer { Name = "DuelArena", SizeFlagsVertical = SizeFlags.ExpandFill };
        _arena = arena;
        arena.AddThemeConstantOverride("separation", 12);
        _root.AddChild(arena);
        BuildPlayer(arena, 0);
        BuildCombat(arena);
        BuildPlayer(arena, 1);
        BuildToolbar(_root);
        BuildHand(_root);
        MoveChild(_diceOverlay, GetChildCount() - 1);
        UpdateDiceOverlay();
        PrepareDrawTargets();
    }

    private void BuildDiceOverlay()
    {
        _diceOverlay = new CenterContainer { Name = "DiceRollOverlay", MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        AddChild(_diceOverlay);
        _diceOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel = new PanelContainer { CustomMinimumSize = new(312, 286), MouseFilter = MouseFilterEnum.Ignore };
        var box = Box("111f2c", "927b54", 18);
        box.ContentMarginLeft = 12; box.ContentMarginRight = 12;
        box.ContentMarginTop = 12; box.ContentMarginBottom = 12;
        panel.AddThemeStyleboxOverride("panel", box);
        _diceOverlay.AddChild(panel);
        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 8);
        panel.AddChild(column);
        _diceTitle = Label("LANZANDO EL DADO", 12, Gold);
        _diceTitle.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(_diceTitle);
        column.AddChild(_die);
        _diceCaption = Label("", 13, Ink);
        _diceCaption.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(_diceCaption);
        IgnoreMouse(panel);
        panel.MouseFilter = MouseFilterEnum.Stop;
    }

    private void UpdateDiceOverlay()
    {
        _diceOverlay.Visible = _die.IsPresented;
        if (!_die.IsPresented) return;
        if (_arena.Size.Y > 0)
        {
            var bounds = _arena.GetGlobalRect();
            _diceOverlay.OffsetTop = bounds.Position.Y - GlobalPosition.Y;
            _diceOverlay.OffsetBottom = bounds.End.Y - GlobalPosition.Y - Size.Y;
        }
        _diceOverlay.Modulate = new Color(1, 1, 1, _die.PresentationOpacity);
        _diceTitle.Text = _die.IsRolling ? "LANZANDO EL DADO" : "RESULTADO DEL DADO";
        _diceCaption.Text = _die.IsRolling ? "El dado está en movimiento…" : $"Cara superior · {_die.Value}" + (_die.RollCaption.Length > 0 ? $"\n{_die.RollCaption}" : "");
    }

    private void BuildHeader()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        var brand = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        brand.AddThemeConstantOverride("separation", 0);
        brand.AddChild(Label("R E L I C K", 29, Gold));
        row.AddChild(brand);
        row.AddChild(Button("Pruebas", ShowAnimationLab, false, 126));
        var errorIndex = _game.State.Players[0].Hand.FindIndex(id => CardCatalog.Get(id).CancelActivation);
        if (errorIndex >= 0 && _game.CanPlayLoadError(0) && _game.CodeUnavailableReason(0, errorIndex) == null && _game.State.Phase != Phase.ErrorResponse)
        {
            var cancel = Button("Error de Carga", () => Execute(_game.PlayCode(0, errorIndex)), false, 126);
            cancel.Name = "InterruptWithLoadError"; row.AddChild(cancel);
        }
        row.AddChild(Button("Registro", ShowHistory, false, 92));
        row.AddChild(Button("Cómo jugar", ShowHelp, false, 116));
        row.AddChild(Button("Nueva partida", ConfirmRestart, false, 135));
        _root.AddChild(row);
    }

    private static PanelContainer Pill(string text, Color color)
    {
        var panel = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        var box = Box("1b2336", color.Darkened(0.6f).ToHtml(false), 8);
        box.ContentMarginLeft = 12; box.ContentMarginRight = 12;
        box.ContentMarginTop = 6; box.ContentMarginBottom = 6;
        panel.AddThemeStyleboxOverride("panel", box);
        panel.AddChild(Label(text, 11, color));
        return panel;
    }

    private void BuildPhases()
    {
        var state = _game.State;
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(Pill(state.Phase == Phase.Preparation ? "PREPARACIÓN" : state.IsOver ? "DUELO TERMINADO" : state.CurrentPlayer == 0 ? "TU TURNO" : "TURNO DEL RIVAL", state.CurrentPlayer == 0 ? Mint : Coral));
        if (state.Phase != Phase.Preparation) row.AddChild(Pill($"TURNO {state.Turn:00}", Muted));
        var phase = state.Phase is Phase.CodeResponse or Phase.PointsChoice or Phase.DestroyChoice or Phase.ErrorResponse ? Phase.Actions : state.Phase == Phase.DieRerollChoice ? state.PendingDieRoll!.ResumePhase : state.Phase is Phase.DefenseChoice or Phase.MagnetChoice or Phase.AttackRerollChoice or Phase.DieRerollChoice or Phase.CodeResponse or Phase.PointsChoice or Phase.DestroyChoice or Phase.ErrorResponse ? Phase.Combat : state.Phase == Phase.Discard ? state.DiscardResumePhase : state.Phase == Phase.RelikiaChoice ? Phase.Actions : state.Phase;
        var current = phase switch { Phase.Preparation => 0, Phase.Combat => 1, Phase.Actions => 2, _ => 3 };
        var phases = new[] { ("Preparar", GameIcon.Cards), ("Combate", GameIcon.Sword), ("Acciones", GameIcon.Bolt), ("Final", GameIcon.Flag) };
        for (var i = 0; i < phases.Length; i++)
            row.AddChild(new PhaseBadge
            {
                Caption = phases[i].Item1, Icon = phases[i].Item2, Current = i == current, Complete = i < current,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            });
        _root.AddChild(row);
    }

    private static PanelContainer BoardPanel(Node parent, string background, Color border)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var box = Box(background, border.ToHtml(false), 16);
        box.ContentMarginTop = 10; box.ContentMarginBottom = 10;
        box.ContentMarginLeft = 12; box.ContentMarginRight = 12;
        panel.AddThemeStyleboxOverride("panel", box);
        parent.AddChild(panel);
        return panel;
    }

    private void BuildPlayer(HBoxContainer arena, int index)
    {
        var state = _game.State;
        var owner = state.Players[index];
        var enemy = index == 1;
        var accent = enemy ? Coral : Mint;
        var active = state.CurrentPlayer == index && state.Phase != Phase.Preparation && !state.IsOver;
        var panel = BoardPanel(arena, enemy ? "1b1b30" : "12252b", active ? accent : accent.Darkened(0.65f));
        panel.Name = enemy ? "EnemyRelikarioPanel" : "OwnRelikarioPanel";
        panel.CustomMinimumSize = new(320, 0);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 6);
        panel.AddChild(col);
        var header = new HBoxContainer();
        var names = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        names.AddThemeConstantOverride("separation", 0);
        names.AddChild(Label(enemy ? "ADVERSARIO" : "TU RELIKARIO", 9, accent));
        var relikarioName = Label($"{owner.Relikario.Name} · Lv {owner.Level}", 18, Ink);
        relikarioName.Name = $"RelikarioName{index}";
        relikarioName.MouseFilter = MouseFilterEnum.Pass;
        relikarioName.TooltipText = $"Vida máxima: {owner.MaxHealth}\n{owner.Relikario.AbilityText}";
        names.AddChild(relikarioName);
        header.AddChild(names);
        header.AddChild(new ResourceBadge { Amount = owner.Relikia });
        if (!enemy)
        {
            var evolve = Button(owner.Level == 1 ? "Lv 2 ↑" : "Lv 2 ✓", () => Execute(_game.Evolve(index)),
                !_game.CanEvolve(index) || _die.IsBusy || CardsInMotion, 64);
            evolve.Name = "EvolveRelikario";
            evolve.AddThemeFontSizeOverride("font_size", 12);
            evolve.TooltipText = "Evolucionar: 1800 PR + haber obtenido un 6 natural durante la partida. Gana 2 vidas; máximo 10.\n" +
                (owner.HasNaturalSix ? "6 natural: cumplido." : "6 natural: pendiente.") + "\n" + (_game.EvolutionUnavailableReason(index) ?? "Evolución disponible.");
            header.AddChild(evolve);
        }
        col.AddChild(header);
        var life = _lifeMeters[index];
        life.SetHealth(owner.Health, owner.MaxHealth);
        col.AddChild(life);
        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 6);
        var zones = new VBoxContainer { CustomMinimumSize = new(70, 0), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        zones.AddThemeConstantOverride("separation", 6);
        zones.AddChild(new DeckStack { Name = $"DeckStack{index}", Amount = owner.Deck.Count, Accent = accent, SizeFlagsHorizontal = SizeFlags.ShrinkCenter });
        var memory = new MemoryZoneButton { Name = $"MemoryButton{index}", Amount = owner.Memory.Count, Accent = accent,
            TopCard = owner.Memory.Count == 0 ? null : CardCatalog.Get(owner.Memory[^1]) };
        memory.Pressed += () => ShowMemory(index);
        var memories = new HBoxContainer();
        memories.AddThemeConstantOverride("separation", 6);
        memories.AddChild(memory);
        var corrupted = new MemoryZoneButton { Name = $"CorruptedMemoryButton{index}", Amount = owner.CorruptedMemory.Count, Corrupted = true };
        corrupted.Pressed += () => ShowMemory(index, true);
        memories.AddChild(corrupted);
        zones.AddChild(memories);
        var permanent = BuildPermanentZones(index);
        body.AddChild(enemy ? zones : permanent);
        body.AddChild(PortraitFor(index));
        body.AddChild(enemy ? permanent : zones);
        col.AddChild(body);
        var skillRow = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        skillRow.AddThemeConstantOverride("h_separation", 6);
        skillRow.AddThemeConstantOverride("v_separation", 6);
        foreach (var slot in new[] { 2, 3, 0, 1 })
        {
            var learned = slot < owner.Skills.Count;
            var skill = new SkillTile
            {
                Name = $"Skill{index}_{slot}", Skill = learned ? CardCatalog.Get(owner.Skills[slot]) : null,
                Slot = slot, Counters = owner.CountersOnSkill(slot), Chosen = !enemy && learned && _selectedSkill == slot, Accent = accent,
                Disabled = enemy && !learned
            };
            skill.Pressed += () =>
            {
                if (enemy && learned) { ShowCardDetails(index, CardCatalog.Get(owner.Skills[slot]), "HABILIDAD RIVAL", $"Espacio {slot + 1} · {owner.CountersOnSkill(slot)}/2 contadores.", "Habilidad del rival"); return; }
                if (enemy) return;
                if (learned) { _selectedSkill = slot; _feedback = ""; }
                else _feedback = "Selecciona una habilidad de tu mano y pulsa Enseñar.";
                Render();
            };
            if (!enemy && learned)
            {
                skill.Ready += () => skill.TooltipText += "\nClic derecho para ampliar.";
                skill.GuiInput += input =>
                {
                    if (input is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
                        ShowCardDetails(index, CardCatalog.Get(owner.Skills[slot]), "TU HABILIDAD", $"Espacio {slot + 1}", "Detalle de habilidad");
                };
            }
            skillRow.AddChild(skill);
        }
        col.AddChild(skillRow);
    }

    private VBoxContainer BuildPermanentZones(int player)
    {
        var state = _game.State.Players[player];
        var column = new VBoxContainer { CustomMinimumSize = new(92, 0), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        column.AddThemeConstantOverride("separation", 6);
        var stack = new AttachmentStack { Name = $"AttachmentStack{player}", Amount = state.AttachedCards.Count };
        stack.Pressed += () => ShowAttachedCards(player);
        column.AddChild(stack);
        var equipment = new ZoneCardButton
        {
            Name = $"EquipmentZone{player}", Caption = state.Equipment == null ? "EQUIPO 0/1" : "EQUIPO 1/1",
            Card = state.Equipment == null ? null : CardCatalog.Get(state.Equipment), EmptyCaption = "Sin equipo", Accent = new Color("81c8ef")
        };
        equipment.Pressed += () => ShowEquipment(player);
        column.AddChild(equipment);
        return column;
    }

    private void BuildCombat(HBoxContainer arena)
    {
        var state = _game.State;
        var panel = BoardPanel(arena, "111a2c", new Color("4b4350"));
        panel.Name = "CombatCenter";
        panel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        panel.CustomMinimumSize = new(312, 0);
        panel.SizeFlagsStretchRatio = 1.05f;
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 4);
        panel.AddChild(col);
        BuildOpponentHand(col);
        col.AddChild(new HSeparator { Modulate = new Color("38415a") });
        var title = Label(state.IsOver ? "RESULTADO DEL DUELO" : "RESOLUCIÓN DE COMBATE", 10, Gold);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.Visible = state.Phase != Phase.Preparation;
        col.AddChild(title);
        var message = state.IsOver ? (state.Winner == -1 ? "Empate" : state.Winner == 0 ? "¡Victoria!" : "Derrota") : state.Phase == Phase.Preparation ? "" : state.LastCombat;
        if (_die.IsBusy) message = "";
        else if (_dicePreview) message = $"Resultado de prueba: {_die.Value}";
        var status = Label(message, 20, Ink);
        status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        status.HorizontalAlignment = HorizontalAlignment.Center;
        status.CustomMinimumSize = new(0, 32);
        status.Visible = message.Length > 0;
        col.AddChild(status);
        var divider = new HSeparator();
        divider.Modulate = new Color("38415a");
        col.AddChild(divider);
        var exchangeTitle = Label("CANJE  →  BONUS", 9, Muted);
        exchangeTitle.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(exchangeTitle);
        foreach (var index in new[] { 1, 0 })
        {
            var owner = state.Players[index];
            var bonus = new ExchangeTrack
            {
                Name = $"BonusTrack{index}",
                OwnerCaption = index == 0 ? "TÚ" : "RIVAL", ExchangeCards = owner.Exchange.Select(CardCatalog.Get).ToArray(),
                Preparing = state.Phase == Phase.Preparation, BonusCard = owner.Bonus == null ? null : CardCatalog.Get(owner.Bonus),
                Accent = index == 0 ? Mint : Coral
            };
            bonus.BonusInspected += () => ShowBonus(index);
            bonus.ExchangeInspected += slot => ShowExchange(index, slot);
            col.AddChild(bonus);
        }
    }

    private void BuildToolbar(VBoxContainer board)
    {
        var state = _game.State;
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 4);
        var hintText = Hint();
        if (!_die.IsBusy && state.DecisionPlayer == 0 && (state.Phase is Phase.Combat or Phase.Actions) &&
            _selectedCard >= 0 && _selectedCard < state.Players[0].Hand.Count)
        {
            var selected = CardCatalog.Get(state.Players[0].Hand[_selectedCard]);
            hintText = $"{selected.Name.Replace(" de práctica", "")}: {selected.Text}";
        }
        var hint = Label(_feedback.Length > 0 ? _feedback : hintText, 12, _feedback.Length > 0 ? Coral : Muted);
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        hint.CustomMinimumSize = new(0, 18);
        hint.Visible = hint.Text.Length > 0;
        column.AddChild(hint);
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        var myTurn = state.DecisionPlayer == 0 && !state.IsOver && !_die.IsBusy;
        if (state.Phase == Phase.Preparation)
            actions.AddChild(PrimaryButton(_mulligan.Count == 0 ? "Conservar mano y empezar  →" : $"Cambiar {_mulligan.Count} cartas y empezar  →", () =>
            {
                Execute(_game.Start(_mulligan));
                _mulligan.Clear();
            }, _die.IsBusy));
        else if (state.IsOver) actions.AddChild(PrimaryButton("Volver a jugar  →", NewGame, _die.IsBusy));
        else
        {
            actions.AddChild(PrimaryButton(SelectedVerb(), UseSelected, !myTurn || !CanUseSelected()));
            actions.AddChild(Button("Canjear carta", () => Execute(_game.Exchange(0, _selectedCard)),
                !myTurn || state.Phase != Phase.Actions || state.ActionsLeft == 0 || _selectedCard < 0 || state.Players[0].Bonus == null, 154));
            var relikia = Button(state.Phase == Phase.RelikiaChoice ? "Resolver Relikia" : "Efectos de Relikia", () =>
            {
                if (state.Phase == Phase.RelikiaChoice) ShowPendingRelikiaChoice();
                else ShowRelikiaEffects();
            }, !myTurn || state.Phase is not (Phase.Actions or Phase.RelikiaChoice), 166);
            relikia.Name = "RelikiaEffectsButton";
            actions.AddChild(relikia);
            if (state.Phase == Phase.Actions)
            {
                var charges = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
                for (var i = 0; i < 2; i++) charges.AddChild(new IconView
                {
                    Icon = GameIcon.Gem, Color = i < state.ActionsLeft ? Gold : new Color("39445c"), CustomMinimumSize = new(22, 22), TooltipText = $"{state.ActionsLeft} acciones disponibles"
                });
                actions.AddChild(charges);
            }
            actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
            var advance = new StageAdvanceButton
            {
                Name = "AdvanceStageButton",
                Text = state.Phase == Phase.Combat ? "Ir a acciones  →" : state.Phase is Phase.Discard or Phase.RelikiaChoice or Phase.DefenseChoice or Phase.MagnetChoice or Phase.AttackRerollChoice or Phase.DieRerollChoice or Phase.CodeResponse or Phase.PointsChoice or Phase.DestroyChoice or Phase.ErrorResponse ? "Resolver efecto" : "Terminar turno  →",
                Disabled = !myTurn || state.Phase is Phase.Discard or Phase.RelikiaChoice or Phase.DefenseChoice or Phase.MagnetChoice or Phase.AttackRerollChoice or Phase.DieRerollChoice or Phase.CodeResponse or Phase.PointsChoice or Phase.DestroyChoice or Phase.ErrorResponse,
                Suggested = myTurn && (state.Phase is Phase.Combat or Phase.Actions) && !_game.HasAvailablePlay(0),
                CustomMinimumSize = new Vector2(182, 40)
            };
            if (advance.Suggested) advance.TooltipText = state.Phase == Phase.Combat
                ? "No quedan jugadas en Combate. Continúa a Acciones."
                : "No quedan jugadas en Acciones. Termina tu turno.";
            advance.Pressed += () => Execute(_game.Advance(0));
            actions.AddChild(advance);
        }
        column.AddChild(actions);
        board.AddChild(column);
    }

    private void BuildHand(VBoxContainer board)
    {
        var state = _game.State;
        var header = new HBoxContainer();
        var title = Label($"TU MANO  ·  {state.Players[0].Hand.Count} CARTAS", 10, Ink);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);
        board.AddChild(header);
        var scroll = new ScrollContainer
        {
            Name = "HandScroll", SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new(0, 196),
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, HorizontalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        var cards = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        cards.AddThemeConstantOverride("separation", 10);
        for (var i = 0; i < state.Players[0].Hand.Count; i++)
        {
            var index = i;
            var selected = state.Phase == Phase.Preparation ? _mulligan.Contains(i) : _selectedCard == i;
            var card = new HandCard { Name = $"HandCard{i}", Card = CardCatalog.Get(state.Players[0].Hand[i]), Chosen = selected, Mulligan = state.Phase == Phase.Preparation };
            card.Pressed += () =>
            {
                if (state.Phase == Phase.Preparation) { if (!_mulligan.Add(index)) _mulligan.Remove(index); }
                else _selectedCard = index;
                _feedback = "";
                Render();
            };
            cards.AddChild(card);
        }
        if (state.Players[0].Hand.Count == 0) cards.AddChild(Label("Mano vacía · robarás 3 cartas al empezar tu turno.", 14, Muted));
        scroll.AddChild(cards);
        board.AddChild(scroll);
        _handScroll = scroll;
        scroll.SetDeferred(ScrollContainer.PropertyName.ScrollHorizontal, _handScrollPosition);
    }

    private AcceptDialog? OpenBoardDialog(string title, Control content, Vector2I size)
    {
        if (_helpOpen) return null;
        _helpOpen = true;
        var dialog = new AcceptDialog { Title = title, MinSize = size, OkButtonText = "Volver al duelo" };
        dialog.AddChild(content);
        dialog.Confirmed += () => DismissBoardDialog(dialog);
        dialog.Canceled += () => DismissBoardDialog(dialog);
        AddChild(dialog);
        dialog.PopupCentered();
        return dialog;
    }

    private void DismissBoardDialog(AcceptDialog dialog)
    {
        dialog.Hide();
        dialog.QueueFree();
        _helpOpen = false;
    }

    private void ShowBonus(int player)
    {
        if (_helpOpen || _game.State.Phase == Phase.Preparation || _game.State.Players[player].Bonus is not { } id) return;
        ShowZoneCard(player, CardCatalog.Get(id), true);
    }

    private void ShowExchange(int player, int slot)
    {
        var cards = _game.State.Players[player].Exchange;
        if (_helpOpen || slot < 0 || slot >= cards.Count) return;
        ShowZoneCard(player, CardCatalog.Get(cards[slot]), false, slot);
    }

    private void ShowZoneCard(int player, CardDefinition card, bool bonus, int slot = 0)
    {
        var title = bonus ? (player == 0 ? "Tu carta de Bonus" : "Carta de Bonus del rival")
            : (player == 0 ? $"Tu carta en canje {slot + 1}" : $"Carta del rival en canje {slot + 1}");
        var caption = bonus ? (player == 0 ? "TU BONUS" : "BONUS DEL RIVAL")
            : (player == 0 ? $"TU CANJE · CARTA {slot + 1}" : $"CANJE DEL RIVAL · CARTA {slot + 1}");
        var context = bonus ? "Al completar dos canjes, recibes esta carta y se revela la siguiente del mazo como nuevo Bonus."
            : "Esta carta está en canje. Al completar dos cartas, ambas pasan a memoria y recibes el Bonus.";
        ShowCardDetails(player, card, caption, context, title);
    }

    private void ShowCardDetails(int player, CardDefinition card, string caption, string context, string title, Action? returnToZone = null)
    {
        var row = new HBoxContainer { CustomMinimumSize = new(500, 316) };
        row.AddThemeConstantOverride("separation", 20);
        var stage = new Control { CustomMinimumSize = new(218, 316) };
        stage.AddChild(new HandCard
        {
            Name = "ZoneCardPreview", Card = card, Disabled = true,
            Position = new Vector2(5, 0), Size = new Vector2(126, 184), Scale = new Vector2(1.65f, 1.65f)
        });
        if (card.Kind == CardKind.Skill)
        {
            stage.CustomMinimumSize = new(380, 270);
            var face = (HandCard)stage.GetChild(0);
            face.Position = new(372, 0); face.Rotation = Mathf.Pi / 2; face.Scale = new(2, 2);
        }
        row.AddChild(stage);
        var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        info.AddThemeConstantOverride("separation", 14);
        info.AddChild(Label(caption, 12, player == 0 ? Mint : Coral));
        info.AddChild(Wrapped(card.Name.Replace(" de práctica", ""), 24, Ink, 245));
        info.AddChild(Pill(BoardArt.CardType(card), BoardArt.CardColor(card)));
        info.AddChild(Wrapped(card.Text, 16, Ink, 245));
        info.AddChild(Wrapped(context, 13, Muted, 245));
        row.AddChild(info);
        var dialog = OpenBoardDialog(title, row, new Vector2I(card.Kind == CardKind.Skill ? 720 : 560, 400));
        if (dialog != null && returnToZone != null)
        {
            dialog.OkButtonText = "Volver a memoria";
            dialog.Confirmed += returnToZone;
            dialog.Canceled += returnToZone;
        }
    }

    private void ShowEquipment(int player)
    {
        if (_helpOpen || _game.State.Players[player].Equipment is not { } id) return;
        ShowCardDetails(player, CardCatalog.Get(id), player == 0 ? "TU EQUIPO · 1/1" : "EQUIPO DEL RIVAL · 1/1",
            "Solo puedes tener un equipo. Poner otro envía este a la memoria. Durante tus tiradas puedes destruir el Colgante para usar uno de sus efectos.",
            player == 0 ? "Tu equipo" : "Equipo del rival");
    }

    private void ShowAttachedCards(int player)
    {
        if (_helpOpen) return;
        var cards = _game.State.Players[player].AttachedCards;
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 12);
        content.AddChild(Label($"{cards.Count} carta(s) · sin límite", 13, new Color("b5a2fa")));
        var scroll = new ScrollContainer { Name = "AttachmentList", CustomMinimumSize = new(590, 340), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        content.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 16);
        scroll.AddChild(list);
        for (var index = 0; index < cards.Count; index++)
        {
            var card = CardCatalog.Get(cards[index]);
            var row = new HBoxContainer { Name = $"AttachedCardRow{index}" };
            row.AddThemeConstantOverride("separation", 18);
            row.AddChild(new HandCard { Name = $"AttachedCardPreview{index}", Card = card, Disabled = true });
            var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            info.AddThemeConstantOverride("separation", 10);
            info.AddChild(Wrapped($"{index + 1}. {card.Name.Replace(" de práctica", "")}", 20, Ink, 390));
            info.AddChild(Pill(BoardArt.CardType(card), BoardArt.CardColor(card)));
            info.AddChild(Wrapped(card.Text, 14, Ink, 390));
            row.AddChild(info);
            list.AddChild(row);
        }
        if (cards.Count == 0) list.AddChild(Label("No hay cartas sobre este Relikario.", 15, Muted));
        OpenBoardDialog(player == 0 ? "Cartas sobre tu Relikario" : "Cartas sobre el Relikario rival", content, new Vector2I(650, 445));
    }

    private void ShowAnimationLab()
    {
        var column = new VBoxContainer { CustomMinimumSize = new(480, 150) };
        column.AddThemeConstantOverride("separation", 14);
        column.AddChild(Label("Pruebas visuales · no cambian vidas, cartas ni turnos", 13, Muted));
        foreach (var player in new[] { 0, 1 })
        {
            var row = new HBoxContainer();
            var name = Label(player == 0 ? "Tu Relikario" : "Adversario", 16, player == 0 ? Mint : Coral);
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(name);
            foreach (var (caption, motion) in new[] { ("Reposo", PortraitMotion.Idle), ("Ataque", PortraitMotion.Attack), ("Daño", PortraitMotion.Hit) })
                row.AddChild(Button(caption, () => PreviewPortrait(player, motion), false, 84));
            column.AddChild(row);
        }
        OpenBoardDialog("Pruebas de animación", column, new Vector2I(570, 380));
    }

    private async void VisualSmokeTest()
    {
        try
        {
            _helpOpen = true;
            _die.SetProcess(false);
            _yuno.SetProcess(false);
            _enemyYuno.SetProcess(false);
            foreach (var meter in _lifeMeters) meter.SetProcess(false);
            VerifyLifeMeter();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            foreach (var owner in new[] { 0, 1 })
            {
                var first = (_root.FindChild($"Skill{owner}_0", true, false) as SkillTile)!;
                var costs = first.GetParent().GetChildren().OfType<SkillTile>().Select(tile => GameEngine.SkillSlotCosts[tile.Slot]);
                if (!costs.SequenceEqual(new[] { 500, 1000, 0, 300 }))
                    throw new InvalidOperationException("Las habilidades no están ordenadas 500/1000 arriba y 0/300 abajo.");
                foreach (var tile in first.GetParent().GetChildren().OfType<SkillTile>())
                    if (tile.Size.DistanceTo(SkillTile.CardSize) > 0.1f)
                        throw new InvalidOperationException($"El espacio de habilidad no encaja con una carta horizontal: {tile.Size}.");
            }
            HandCard Hand(int i) => _root.FindChild($"HandCard{i}", true, false) as HandCard
                ?? throw new InvalidOperationException("No aparece la carta de la mano.");
            void Press(string prefix)
            {
                var button = _root.FindChildren("*", "Button", true, false).OfType<Button>()
                    .Single(b => b.Text.StartsWith(prefix));
                if (button.Disabled) throw new InvalidOperationException($"Botón desactivado: {prefix}");
                button.EmitSignal(BaseButton.SignalName.Pressed);
            }
            Hand(0).EmitSignal(BaseButton.SignalName.Pressed);
            if (!_mulligan.Contains(0) || !Hand(0).Chosen) throw new InvalidOperationException("No se ve el cambio inicial seleccionado.");
            Hand(0).EmitSignal(BaseButton.SignalName.Pressed);
            if (_mulligan.Contains(0) || Hand(0).Chosen) throw new InvalidOperationException("No se quitó la selección inicial.");

            BindGame(new GameEngine(42));
            _game.Start(Array.Empty<int>());
            for (var step = 0; step < 30 && _game.State.CurrentPlayer != 0; step++)
                PracticeBot.Step(_game, 1);
            DrainTestDice();
            _yuno.Play(PortraitMotion.Idle);
            _enemyYuno.Play(PortraitMotion.Idle);
            Render();
            foreach (var owner in new[] { 0, 1 })
            {
                var track = _root.FindChild($"BonusTrack{owner}", true, false) as ExchangeTrack;
                var bonusId = _game.State.Players[owner].Bonus;
                if (track?.BonusCard?.Id != bonusId || track!.BonusButton.Disabled != (bonusId == null))
                    throw new InvalidOperationException("El Bonus visible no coincide con la carta del jugador.");
                if (bonusId == null) continue;
                var handBefore = _game.State.Players[owner].Hand.ToArray();
                _helpOpen = false;
                track.BonusButton.EmitSignal(BaseButton.SignalName.Pressed);
                var previewDialog = GetChildren().OfType<AcceptDialog>().Single();
                var preview = previewDialog.FindChild("ZoneCardPreview", true, false) as HandCard;
                if (!previewDialog.Visible || preview?.Card.Id != bonusId ||
                    _game.State.Players[owner].Bonus != bonusId || !handBefore.SequenceEqual(_game.State.Players[owner].Hand))
                    throw new InvalidOperationException("Consultar el Bonus no muestra la carta correcta o cambia la partida.");
                previewDialog.EmitSignal(AcceptDialog.SignalName.Confirmed);
                _helpOpen = true;
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            var player = _game.State.Players[0];
            var learn = player.Hand.FindIndex(id => CardCatalog.Get(id).Kind == CardKind.Skill);
            Hand(learn).EmitSignal(BaseButton.SignalName.Pressed);
            Press("Enseñar");
            if (player.Skills.Count != 1) throw new InvalidOperationException("El control nuevo no permite enseñar.");
            var attack = player.Hand.FindIndex(id => CardCatalog.Get(id).Kind == CardKind.Attack);
            var enemyMeterBeforeAttack = _lifeMeters[1];
            var lifeBeforeAttack = _game.State.Players[1].Health;
            if (!Hand(attack).HasAttackArtwork) throw new InvalidOperationException("La carta de ataque no usa el arte nuevo.");
            Hand(attack).EmitSignal(BaseButton.SignalName.Pressed);
            Press("Atacar");
            KeepTestAttackRoll();
            if (!_game.State.AttackedThisTurn || !_die.IsRolling || _yuno.Motion == PortraitMotion.Attack)
                throw new InvalidOperationException("El ataque no conserva sus efectos y animación.");
            DrainTestDice();
            var enemyLife = _root.FindChild("EnemyLifeMeter", true, false) as LifeMeter;
            if (enemyLife?.Health != _game.State.Players[1].Health)
                throw new InvalidOperationException("El medidor visual no refleja las vidas del rival.");
            if (!ReferenceEquals(enemyLife, enemyMeterBeforeAttack))
                throw new InvalidOperationException("El tablero reemplaza la barra de vida y pierde su animación.");
            if (enemyLife.Health < lifeBeforeAttack)
            {
                if (!enemyLife.IsAnimating) throw new InvalidOperationException("El daño no anima la barra de vida.");
                enemyLife.AdvancePlayback(0.1);
                var intermediate = enemyLife.DisplayedRatio;
                Render();
                if (enemyLife.DisplayedRatio != intermediate || !enemyLife.IsAnimating)
                    throw new InvalidOperationException("Actualizar el tablero interrumpe la animación de vida.");
                enemyLife.AdvancePlayback(1);
            }
            Press("Ir a acciones");
            Hand(0).EmitSignal(BaseButton.SignalName.Pressed);
            Press("Canjear");
            var exchange = _root.FindChildren("*", "Control", true, false).OfType<ExchangeTrack>().Single(e => e.OwnerCaption == "TÚ");
            if (player.Exchange.Count != 1 || exchange.Filled != 1 || _game.State.ActionsLeft != 1)
                throw new InvalidOperationException("El canje visual no refleja el progreso y las acciones.");
            async System.Threading.Tasks.Task InspectExchange(int owner)
            {
                var track = (_root.FindChild($"BonusTrack{owner}", true, false) as ExchangeTrack)!;
                var cards = _game.State.Players[owner].Exchange;
                if (!track.ExchangeCards.Select(c => c.Id).SequenceEqual(cards) ||
                    track.ExchangeButtons[0].Card?.Id != cards[0] || track.ExchangeButtons[0].Disabled ||
                    !track.ExchangeButtons[1].Disabled)
                    throw new InvalidOperationException("Las cartas de canje visibles no coinciden con su zona.");
                var before = System.Text.Json.JsonSerializer.Serialize(_game.State);
                _helpOpen = false;
                track.ExchangeButtons[0].EmitSignal(BaseButton.SignalName.Pressed);
                var dialog = GetChildren().OfType<AcceptDialog>().Single();
                var preview = dialog.FindChild("ZoneCardPreview", true, false) as HandCard;
                if (!dialog.Visible || preview?.Card.Id != cards[0] ||
                    System.Text.Json.JsonSerializer.Serialize(_game.State) != before)
                    throw new InvalidOperationException("La consulta de canje no muestra la carta correcta o altera la partida.");
                dialog.EmitSignal(AcceptDialog.SignalName.Confirmed);
                _helpOpen = true;
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            await InspectExchange(0);
            var claimedBonus = player.Bonus;
            var deckBeforeClaim = player.Deck.ToArray();
            Hand(0).EmitSignal(BaseButton.SignalName.Pressed);
            Press("Canjear");
            exchange = (_root.FindChild("BonusTrack0", true, false) as ExchangeTrack)!;
            if (claimedBonus == null || player.Bonus != deckBeforeClaim[0] || player.Hand.Last() != claimedBonus ||
                exchange.BonusCard?.Id != player.Bonus || exchange.BonusButton.Disabled || exchange.Filled != 0 ||
                exchange.ExchangeButtons.Any(b => b.Card != null || !b.Disabled) ||
                !player.Deck.SequenceEqual(deckBeforeClaim.Skip(1)))
                throw new InvalidOperationException("Al reclamar el Bonus no se muestra la siguiente carta del mazo ni se reinicia el canje.");

            BindGame(new GameEngine(42));
            _game.State.Phase = Phase.Actions; _game.State.CurrentPlayer = 0; _game.State.ActionsLeft = 2;
            player = _game.State.Active;
            player.Hand.Clear(); player.Hand.AddRange(Enumerable.Repeat("attack", 20));
            _selectedCard = -1;
            Render();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var handScroll = (_root.FindChild("HandScroll", true, false) as ScrollContainer)!;
            if (_root.FindChildren("*", "Button", true, false).OfType<HandCard>().Count() != 20 ||
                handScroll.GetHScrollBar().MaxValue <= handScroll.GetHScrollBar().Page)
                throw new InvalidOperationException("La mano grande no muestra todas las cartas mediante desplazamiento.");
            Press("Terminar turno");
            if (player.Hand.Count != 20 || player.Memory.Count != 0 || _game.State.CurrentPlayer != 1 || _game.State.Phase != Phase.Combat)
                throw new InvalidOperationException("La interfaz exige descartar al terminar con una mano grande.");

            BindGame(new GameEngine(42));
            _game.State.Phase = Phase.Combat;
            _game.State.CurrentPlayer = 0;
            player = _game.State.Players[0];
            player.Skills.Clear(); player.Hand.Clear();
            player.Hand.AddRange(new[] { "magnet-shuffle", "destiny-shuffle", "magnet-shuffle", "destiny-shuffle", "attack" });
            player.Relikia = 1800;
            _selectedCard = -1;
            Render();
            for (var i = 0; i < GameEngine.MaxSkills; i++)
            {
                _game.State.LearnedThisTurn = false;
                Hand(0).EmitSignal(BaseButton.SignalName.Pressed);
                Press("Enseñar");
                var tile = _root.FindChild($"Skill0_{i}", true, false) as SkillTile;
                if (tile?.Skill?.Id != player.Skills[i]) throw new InvalidOperationException("El espacio enseñado no se actualizó.");
            }
            var fourth = _root.FindChild("Skill0_3", true, false) as SkillTile;
            fourth!.EmitSignal(BaseButton.SignalName.Pressed);
            Hand(0).EmitSignal(BaseButton.SignalName.Pressed);
            Press("Atacar");
            KeepTestAttackRoll();
            if (_selectedSkill != 3 || player.Relikia < 0 || !_game.State.Log.Any(line => line.StartsWith("Tú usa Relick Destiny Shuffle")))
                throw new InvalidOperationException("No se pudo seleccionar y atacar con la cuarta habilidad.");
            DrainTestDice();

            _game.State.CurrentPlayer = 1; _game.State.Phase = Phase.Actions; _game.State.ActionsLeft = 2;
            var rival = _game.State.Active;
            rival.Hand.Clear(); rival.Hand.Add("lucky-pendant"); rival.Bonus = "lucky-pendant";
            if (!_game.Exchange(1, 0).Success) throw new InvalidOperationException("No se pudo preparar el canje del rival.");
            Render();
            await InspectExchange(1);

            _helpOpen = false;
            ShowHistory();
            var dialog = GetChildren().OfType<AcceptDialog>().Single();
            if (!dialog.Visible || !_helpOpen) throw new InvalidOperationException("No se abrió el registro.");
            dialog.EmitSignal(AcceptDialog.SignalName.Confirmed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            ShowAnimationLab();
            dialog = GetChildren().OfType<AcceptDialog>().Single();
            if (!dialog.Visible || !_helpOpen) throw new InvalidOperationException("No se abrieron las pruebas de animación.");
            dialog.EmitSignal(AcceptDialog.SignalName.Confirmed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            _helpOpen = true;
            NewGame();
            if (_game.State.Players.Any(zone => zone.AttachedCards.Count != 0 || zone.Equipment != null))
                throw new InvalidOperationException("Reiniciar no vacía las zonas de equipo y cartas sobre el Relikario.");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var minimum = _root.GetCombinedMinimumSize();
            var available = GetViewportRect().Size - new Vector2(48, 30);
            if (minimum.X > available.X + 1 || minimum.Y > available.Y + 1)
                throw new InvalidOperationException($"El nuevo tablero excede la ventana: {minimum} / {available}");
            GD.Print("VISUAL_SMOKE_PASS · mano de 20 cartas sin descarte obligatorio, habilidades, vidas, canje, Bonus, pila sin límite y equipo único.");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private void VerifyLifeMeter()
    {
        var meter = _lifeMeters[0];
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        foreach (var maximum in new[] { 1, 5, 18, 1000 })
        {
            meter.SetHealth(maximum, maximum, false);
            Check(meter.DisplayedRatio == 1 && !meter.IsAnimating, "La vida máxima no llena la barra.");
            meter.SetHealth(0, maximum, false);
            Check(meter.DisplayedRatio == 0 && meter.Health == 0, "La barra no queda vacía en cero.");
        }
        foreach (var (health, color) in new[] { (100, "58d69a"), (51, "58d69a"), (50, "f1c75b"), (21, "f1c75b"), (20, "ef6577"), (0, "ef6577") })
        {
            meter.SetHealth(health, 100, false);
            Check(meter.FillColor == new Color(color), "El color no depende del porcentaje de vida.");
        }
        meter.SetHealth(80, 80, false);
        meter.SetHealth(20, 80);
        Check(meter.IsAnimating && meter.DisplayedRatio == 1 && meter.Health == 20, "El daño salta directamente al valor final.");
        meter.AdvancePlayback(0.2);
        var partial = meter.DisplayedRatio;
        Check(partial > 0.25f && partial < 1, "La reducción no tiene un valor intermedio.");
        meter.SetHealth(20, 80);
        Check(meter.DisplayedRatio == partial, "El mismo valor reinicia la barra.");
        meter.SetHealth(80, 80);
        Check(meter.DisplayedRatio == partial, "La curación interrumpe la barra con un salto.");
        meter.AdvancePlayback(0.1);
        Check(meter.DisplayedRatio > partial && meter.DisplayedRatio < 1, "La curación no anima el aumento de vida.");
        meter.AdvancePlayback(1);
        Check(meter.DisplayedRatio == 1 && !meter.IsAnimating, "La curación no llega al valor final.");
        float Sample(int fps)
        {
            meter.SetHealth(100, 100, false);
            meter.SetHealth(0, 100);
            for (var frame = 0; frame < fps / 3; frame++) meter.AdvancePlayback(1.0 / fps);
            return meter.DisplayedRatio;
        }
        Check(Math.Abs(Sample(60) - Sample(120)) < 0.0001f, "La velocidad de la barra depende de los FPS.");
        meter.SetHealth(0, 100, false);
        Check(!meter.IsAnimating && meter.DisplayedRatio == 0, "Reiniciar la partida conserva una transición anterior.");
        foreach (var index in new[] { 0, 1 })
            _lifeMeters[index].SetHealth(_game.State.Players[index].Health, _game.State.Players[index].MaxHealth, false);
    }
}
