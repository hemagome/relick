using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    private DuelOption? PresentedOption(Button button)
    {
        var index = (int)button.GetMeta("option_index", -1);
        return index >= 0 && index < _snapshot!.Options.Count ? _snapshot.Options[index] : null;
    }
    private Button? PresentedButton(DuelOption option) => _moveButtons.FirstOrDefault(b =>
        (int)b.GetMeta("option_index", -1) == _snapshot!.Options.IndexOf(option));
    private bool PresentsOption(DuelOption option) => PresentedButton(option) != null ||
        _actionMenus.Any(m => m.GetPopup().GetItemIndex(_snapshot!.Options.IndexOf(option)) >= 0);
    private bool PresentsKind(DuelMoveKind kind) => _snapshot!.Options.Any(o => o.Move.Kind == kind && PresentsOption(o));

    internal void PrepareTestOption(int index)
    {
        ClearContext(); var s = _snapshot!; var o = s.Options[index]; var m = o.Move;
        if (m.Kind == DuelMoveKind.Relikia && m.Value == (int)RelikiaEffect.ReplayCode)
        { _showReplayMemory = true; Render(); }
        if (ChoosingEffectCards && _deckSearchOptions.Contains(o)) SelectCard(Array.IndexOf(_deckSearchOptions, o));
        else if (m.Kind == DuelMoveKind.FieldCode) SelectField(s.MySeat, (FieldZone)m.Value, m.Index);
        else if (m.Kind == DuelMoveKind.Destroy) SelectField(m.Target, (FieldZone)m.Value, m.Index);
        else if (m.Kind == DuelMoveKind.Magnet) SelectField(1 - s.MySeat, m.Flag ? FieldZone.Bonus : FieldZone.Hand, m.Index);
        else if (m.Kind == DuelMoveKind.Points) { _pointsTarget = m.Target; BuildMoves(); }
        else if (m.Kind is DuelMoveKind.DestinyDefense or DuelMoveKind.ProtectDestruction && m.Index >= 0) SelectSkill(m.Index);
        if (o.RequiredDiscards > 0)
        {
            var indices = m.Kind == DuelMoveKind.GoldenTotem ? s.Players[s.MySeat].Hand.Select((id, i) => (id, i)).Where(c => c.id == CardCatalog.Totem.Id).Select(c => c.i)
                : Enumerable.Range(0, SelectableCards.Count);
            foreach (var i in indices.Take(o.RequiredDiscards)) SelectCard(i);
        }
        else if (IsHandAction(m.Kind)) { SelectCard(m.Index); if (m.Kind == DuelMoveKind.Attack) SelectSkill(m.Value); }
    }

    internal bool PressTestOption(int index)
    {
        var button = PresentedButton(_snapshot!.Options[index]);
        if (button is { Disabled: false }) { button.EmitSignal(Button.SignalName.Pressed); return true; }
        foreach (var menu in _actionMenus)
        {
            var item = menu.GetPopup().GetItemIndex(index);
            if (item >= 0 && !menu.Disabled && !menu.GetPopup().IsItemDisabled(item))
            { menu.GetPopup().EmitSignal(PopupMenu.SignalName.IdPressed, index); return true; }
        }
        return false;
    }

    internal async Task CheckContextActionBar()
    {
        await CheckCardInspector();
        await CheckLoadErrorChoices();
        await CheckPointsChoices();
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        async Task Layout() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        async Task TapCard(int index)
        {
            var card = _handCards[index]; _hand!.EnsureControlVisible(card); await Layout();
            card.Disabled = false; GuiInputTestDriver.Click(card); await Layout();
        }
        async Task Capture(string name)
        {
            var directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
            if (directory == null || DisplayServer.GetName() == "headless") return;
            System.IO.Directory.CreateDirectory(directory);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, name + ".png"));
        }
        foreach (var actor in new[] { 0, 1 })
        foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
        foreach (var seat in new[] { 0, 1 })
        {
            GetWindow().Size = size; GetWindow().ContentScaleSize = size; await Layout();
            var rng = new SeededRandom(91); var hands = new List<string>[] { [CardCatalog.Attack.Id], [CardCatalog.Attack.Id] };
            hands[actor] = [CardCatalog.Bonfire.Id, CardCatalog.PlumberStar.Id, CardCatalog.Attack.Id, CardCatalog.Senshuni.Id];
            var game = GameEngine.FromOpening(new DuelOpening { Names = ["Ana", "Beto"], StartingSeat = actor, Hands = hands,
                Decks = [Enumerable.Repeat("attack", 30).ToList(), Enumerable.Repeat("attack", 30).ToList()], Bonuses = [null, null] }, rng);
            var p = game.State.Active; p.Bonus = CardCatalog.Jessica.Id; p.Skills.AddRange([CardCatalog.Destiny.Id, CardCatalog.Magnet.Id]);
            p.SkillPositions = new() { [0] = 2, [1] = 0 }; p.Relikia = 500; game.State.Phase = Phase.Actions;
            ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, seat, 310)); await Layout();
            var initialMinimum = _root!.GetCombinedMinimumSize();
            Check(initialMinimum.X <= Size.X + 1 && initialMinimum.Y <= Size.Y + 1,
                $"Barra inicial desborda {size}: mínimo {initialMinimum}, raíz {_root.Size}, mano {_hand!.GetGlobalRect()}.");
            Check(!PresentsKind(DuelMoveKind.Concede) && _arenaMenu!.GetPopup().GetItemIndex(1) >= 0, "Abandonar sigue entre las acciones de cartas.");
            Check(_moveButtons.All(b => b.GetParent() != _moves), "Una mano sin seleccionar muestra acciones ajenas.");
            if (seat == actor)
            {
                var before = game.SaveSnapshot();
                await TapCard(0);
                var equip = _snapshot!.Options.Single(o => o.Move.Kind == DuelMoveKind.Equip && o.Move.Index == 0);
                Check(PresentedButton(equip)?.Text == "Equipar" && _contextTitle.Text == "Hoguera" && _contextCost.Text == "1 acción", "Hoguera pierde el contexto o el coste breve.");
                Check(_moveButtons.Count(b => b.GetParent() == _moves) == 2 && _moves.GetChildren().OfType<Button>().Count() <= 4,
                    "La barra de Hoguera presenta más de sus dos acciones y consultas compactas.");
                Check(_advanceButton?.GetParent() == _turnControls && _turnControls.GetParent() != _moves, "El avance de turno se mezcla con la selección.");
                await Capture($"context-equip-seat{seat}-{size.X}x{size.Y}");
                await TapCard(1);
                Check(_contextCost.Text.Contains("1 acción") && _contextCost.Text.Contains("1 descarte") &&
                    _moveButtons.Any(b => b.GetParent() == _moves && b.Text == "Activar"), "Estrella oculta el coste antes de anunciarla.");
                Check(game.SaveSnapshot() == before, "Seleccionar una acción altera las reglas antes de confirmar.");

                game.State.Phase = Phase.Combat;
                PresentTestSnapshot(DuelSnapshot.For(game, seat, 311)); await Layout(); await TapCard(2);
                Check(_ownSkills.Count(t => t.Targetable) == 2 && !PresentsKind(DuelMoveKind.Attack) && _selection.Text.Contains("habilidad"),
                    "Atacar no pide elegir una habilidad entre los objetivos válidos.");
                Check(_selection.GetGlobalRect().End.Y <= ((Control)_inspector.FindChild("InspectorReading", true, false)).GetGlobalRect().End.Y + 1,
                    "La instrucción de elegir una habilidad queda detrás de la descripción.");
                var skill = _ownSkills.Single(t => t.SkillIndex == 1); _arenaScroll?.EnsureControlVisible(skill); await Layout();
                GuiInputTestDriver.Click(skill); await Layout();
                var attack = _snapshot!.Options.Single(o => o.Move.Kind == DuelMoveKind.Attack && o.Move.Index == 2 && o.Move.Value == 1);
                Check(PresentedButton(attack)?.Text == "Atacar" && skill.Chosen && _contextCost.Text == "Consume 1 Ataque", "El ataque no conserva la habilidad real elegida.");
                await Capture($"context-attack-seat{seat}-{size.X}x{size.Y}");
                ClearContext();
                var bonus = (_root!.FindChild("BonusTrack0", true, false) as ExchangeTrack)!.BonusButton;
                _arenaScroll?.EnsureControlVisible(bonus); await Layout(); GuiInputTestDriver.Click(bonus); await Layout();
                Check(_contextTitle.Text == CardCatalog.Jessica.Name && _moveButtons.Count(b => b.GetParent() == _moves) == 1 && PresentsKind(DuelMoveKind.FieldCode),
                    "Jessica no se activa desde su carta o se mezcla con otras copias.");

                game.State.Phase = Phase.Actions; p.Hand.Add(CardCatalog.BlueEgg.Id); p.Equipment = CardCatalog.Pc.Id;
                Check(game.PlayCode(actor, p.Hand.Count - 1).Success, "No comienza Huevo para probar objetivos.");
                PresentTestSnapshot(DuelSnapshot.For(game, seat, 312)); await Layout();
                Check(!PresentsKind(DuelMoveKind.Destroy), "Se listan todos los objetivos de destrucción como botones.");
                var equipment = (ZoneCardButton)_root!.FindChild("EquipmentZone0", true, false);
                Check(equipment.Targetable, "El equipo propio válido no se ilumina.");
                _arenaScroll?.EnsureControlVisible(equipment); await Layout(); GuiInputTestDriver.Click(equipment); await Layout();
                var destroy = _snapshot!.Options.Single(o => o.Move.Kind == DuelMoveKind.Destroy && o.Move.Target == actor && o.Move.Value == (int)FieldZone.Equipment);
                Check(PresentedButton(destroy)?.Text == "Destruir" && equipment.Chosen, "La selección del equipo apunta a otra carta.");
                Check(game.ResolveDestruction(actor, actor, true).Success, "No termina Huevo en el fixture de objetivos.");
                p.Hand.Add(CardCatalog.TimeBomb.Id); Check(game.PlayCode(actor, p.Hand.Count - 1).Success, "No empieza Bomba en el fixture de mano.");
                PresentTestSnapshot(DuelSnapshot.For(game, seat, 313)); await Layout(); await TapCard(0);
                var ownHand = _snapshot!.Options.Single(o => o.Move.Kind == DuelMoveKind.Destroy && o.Move.Target == actor && o.Move.Value == (int)FieldZone.Hand);
                Check(PresentedButton(ownHand)?.Text == "Destruir" && _contextTitle.Text == "Mi mano" && _selection.Text.Contains("azar"), "Bomba confunde la mano al azar con la carta tocada.");

                var memoryGame = new GameEngine(41); memoryGame.State.Phase = Phase.Actions; memoryGame.State.CurrentPlayer = actor;
                memoryGame.State.Active.Relikia = 500; memoryGame.State.Active.Memory.Add(CardCatalog.Senshuni.Id);
                PresentTestSnapshot(DuelSnapshot.For(memoryGame, seat, 314)); await Layout();
                var replay = _snapshot!.Options.FindIndex(o => o.Move.Kind == DuelMoveKind.Relikia && o.Move.Value == (int)RelikiaEffect.ReplayCode);
                Check(replay >= 0 && _actionMenus.Any(m => m.Name == "RelikiaActions"), "Relikia pierde el acceso a sus opciones.");
                PrepareTestOption(replay); await Layout();
                Check(ChoosingEffectCards && PresentedButton(_snapshot.Options[replay])?.Text == "Activar" && _contextTitle.Text == "Senshuni" &&
                    _contextCost.Text.Contains("2 acciones") && _contextCost.Text.Contains("400 PR"), "La galería de memoria pierde el código o su coste real.");
            }
            else Check(_advanceButton == null, "El rival puede avanzar una etapa ajena.");
            Check(_moveButtons.All(b => !System.Text.RegularExpressions.Regex.IsMatch(b.Text, @"(?i)(carta|espacio)\s+\d+")), "La barra muestra identificadores internos.");
            var minimum = _root!.GetCombinedMinimumSize();
            Check(minimum.X <= _root.Size.X + 1 && minimum.Y <= _root.Size.Y + 1, $"La barra contextual desborda {size}: {minimum}.");
            Check(_hand!.GetGlobalRect().End.Y <= Size.Y + 1, $"La mano se sale en {size}: arena {Size}, raíz {_root.Size}, mínimo {minimum}, mano {_hand.GetGlobalRect()}, galería {_showReplayMemory}.");
        }
        ResetInteraction();
        GD.Print("PVP_CONTEXT_ACTION_BAR_PASS · barra breve, costes, selección real de habilidades y zonas, índice/revisión, menús separados y cuatro tamaños para ambos asientos.");
    }

    private static GameEngine PendingCodeFixture(int actor, string card)
    {
        var hands = new List<string>[] { [CardCatalog.Attack.Id], [CardCatalog.Attack.Id] };
        hands[actor] = [card, CardCatalog.Attack.Id];
        var random = new DuelRandom(); random.Replay([4]);
        var game = GameEngine.FromOpening(new DuelOpening { Names = ["Ana", "Beto"], StartingSeat = actor, Hands = hands,
            Decks = [Enumerable.Repeat("attack", 30).ToList(), Enumerable.Repeat("attack", 30).ToList()], Bonuses = [null, null] }, random);
        foreach (var p in game.State.Players) p.Dexterity = 0;
        game.State.Phase = Phase.Actions;
        return game;
    }

    private void CheckPendingCodeBounds()
    {
        var minimum = _root!.GetCombinedMinimumSize();
        if (!(minimum.X <= _root.Size.X + 1 && minimum.Y <= _root.Size.Y + 1 && _hand!.GetGlobalRect().End.Y <= Size.Y + 1 &&
            _root.GetGlobalRect().End.Y <= Size.Y + 1 && (_advanceButton == null || _advanceButton.GetGlobalRect().End.Y <= Size.Y + 1)))
            throw new Exception($"La decisión de código desborda la arena: mínimo {minimum}, raíz {_root.Size}.");
    }

    private async Task CheckLoadErrorChoices()
    {
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        async Task Layout() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        foreach (var actor in new[] { 0, 1 })
        foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
        {
            GetWindow().Size = size; GetWindow().ContentScaleSize = size; await Layout();
            var error = PendingCodeFixture(actor, CardCatalog.Senshuni.Id);
            foreach (var p in error.State.Players) p.Hand.Add(CardCatalog.LoadError.Id);
            Check(error.PlayCode(actor, 0).Success && error.State.Phase == Phase.ErrorResponse, "No empieza la ventana de Error de Carga.");
            for (var step = 0; step < 2; step++)
            {
                error = GameEngine.FromSnapshot(error.SaveSnapshot(), new SeededRandom(32));
                var responder = error.State.DecisionPlayer;
                foreach (var seat in new[] { 0, 1 })
                {
                    ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(error, seat, (ulong)(321 + step))); await Layout();
                    CheckPendingCodeBounds();
                    if (seat != responder)
                    {
                        Check(!PresentsKind(DuelMoveKind.PlayCode) && !PresentsKind(DuelMoveKind.PassError), "El jugador en espera puede responder con Error.");
                        continue;
                    }
                    void CheckResponse()
                    {
                        Check(PresentsKind(DuelMoveKind.PlayCode) && PresentsKind(DuelMoveKind.PassError),
                            "Error de Carga no ofrece Anular y Dejar resolver sin depender de la selección de mano.");
                        Check(_contextCost.Text == "1 acción", "Error de Carga oculta el coste de anular.");
                    }
                    CheckResponse();
                    foreach (var index in new[] { 0, error.State.Players[seat].Hand.IndexOf(CardCatalog.LoadError.Id) })
                    {
                        var card = _handCards[index]; _hand!.EnsureControlVisible(card); await Layout();
                        card.Disabled = false; GuiInputTestDriver.Click(card); await Layout(); CheckResponse();
                    }
                    var pass = _snapshot!.Options.Single(o => o.Move.Kind == DuelMoveKind.PassError);
                    var copy = GameEngine.FromSnapshot(error.SaveSnapshot(), new SeededRandom(32));
                    Check(pass.Move.Apply(copy, responder).Success && copy.State.Phase == Phase.Actions, "Dejar resolver no cierra la ventana de Error.");
                }
                var counter = DuelOptions.For(error, responder).Single(o => o.Move.Kind == DuelMoveKind.PlayCode);
                Check(counter.Move.Apply(error, responder).Success, "No se puede anular con la opción presentada.");
            }
            Check(error.State.Phase == Phase.Actions && error.State.Players[actor].Hand.Count == 3, "El contraerror no permite resolver Senshuni.");
        }
        ResetInteraction();
        GD.Print("PVP_LOAD_ERROR_CHOICES_PASS · Error y contraerror con ambas respuestas visibles tras recuperar, en cuatro tamaños y ambos jugadores/asientos.");
    }

    private async Task CheckPointsChoices()
    {
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        async Task Layout() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        foreach (var actor in new[] { 0, 1 })
        foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
        {
            GetWindow().Size = size; GetWindow().ContentScaleSize = size; await Layout();
            var points = PendingCodeFixture(actor, CardCatalog.PointsIncrease.Id);
            Check(points.PlayCode(actor, 0).Success && points.State.Phase == Phase.PointsChoice, "No empieza la elección de PR.");
            points = GameEngine.FromSnapshot(points.SaveSnapshot(), new SeededRandom(31));
            foreach (var seat in new[] { 0, 1 })
            {
                ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(points, seat, 320)); await Layout();
                CheckPendingCodeBounds();
                Button[] Targets() => _moves.GetChildren().OfType<Button>().Where(b => b.Text is "Mis PR" or "PR del rival").ToArray();
                if (seat != actor)
                {
                    Check(Targets().Length == 0 && !PresentsKind(DuelMoveKind.Points), "El rival ve el selector de Aumento de Relick Points.");
                    Check(_selection.Text.Contains("rival"), "El rival no ve la espera de la elección de PR.");
                    continue;
                }
                Check(Targets().Length == 2, "El dueño no puede elegir ambos destinos de PR.");
                foreach (var target in new[] { actor, 1 - actor })
                {
                    var before = points.SaveSnapshot();
                    GuiInputTestDriver.Click(Targets().Single(b => b.Text == (target == actor ? "Mis PR" : "PR del rival"))); await Layout();
                    CheckPendingCodeBounds();
                    var options = _snapshot!.Options.Where(o => o.Move.Kind == DuelMoveKind.Points && PresentsOption(o)).ToArray();
                    Check(options.Length == 2 && options.All(o => o.Move.Target == target) && options.Select(o => o.Move.Flag).Distinct().Count() == 2,
                        "La elección de PR muestra otro destino o pierde uno de los signos.");
                    foreach (var option in options)
                    {
                        var copy = GameEngine.FromSnapshot(before, new SeededRandom(31));
                        Check(option.Move.Apply(copy, actor).Success && copy.State.Phase == Phase.Actions &&
                            copy.State.Players[target].Relikia == (option.Move.Flag ? points.State.PendingPoints!.Amount : 0), "El botón de PR resuelve otra opción.");
                    }
                    GuiInputTestDriver.Click(_moves.GetChildren().OfType<Button>().Single(b => b.Text == "×")); await Layout();
                    Check(Targets().Length == 2 && points.SaveSnapshot() == before, "Seleccionar PR altera el duelo o pierde los destinos.");
                }
            }
        }
        ResetInteraction();
        GD.Print("PVP_POINTS_CHOICES_PASS · PR privado con ambos destinos/signos tras recuperar, en cuatro tamaños y ambos jugadores/asientos.");
    }
}
