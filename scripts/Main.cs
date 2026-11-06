using Godot;
using Relick.Core;

namespace Relick;

public partial class Main : Control
{
    private static readonly Color Ink = new("f1f2f8");
    private static readonly Color Muted = new("92a0bd");
    private static readonly Color Mint = new("5ed6b3");
    private static readonly Color Gold = new("f4c57a");
    private static readonly Color Coral = new("ef7890");
    private GameEngine _game = null!;
    private int _selectedCard = -1;
    private int _selectedSkill;
    private readonly HashSet<int> _mulligan = new();
    private VBoxContainer _root = null!;
    private double _botElapsed;
    private string _feedback = "";
    private ScrollContainer? _handScroll;
    private int _handScrollPosition;
    private ScrollContainer? _opponentHandScroll;
    private int _opponentHandScrollPosition;
    private bool _helpOpen;
    private int _seed;
    private YunoPortrait _yuno = null!;
    private YunoPortrait _enemyYuno = null!;
    private DiceFace _die = null!;
    private LifeMeter[] _lifeMeters = null!;
    private bool _dicePreview;
    private CenterContainer _diceOverlay = null!;
    private Label _diceTitle = null!;
    private Label _diceCaption = null!;
    private HBoxContainer _arena = null!;

    public override void _Ready()
    {
        GetWindow().Title = "Relick · Arena de práctica";
        var theme = new Theme { DefaultFontSize = 16 };
        theme.SetColor("font_color", "Label", Ink);
        theme.SetColor("font_color", "Button", Ink);
        theme.SetColor("font_disabled_color", "Button", new Color("73808c"));
        theme.SetStylebox("normal", "Button", Box("1e2a40", "384763", 10));
        theme.SetStylebox("hover", "Button", Box("2d3c55", "5ed6b3", 10));
        theme.SetStylebox("pressed", "Button", Box("345455", "5ed6b3", 10));
        theme.SetStylebox("disabled", "Button", Box("151d2d", "27334a", 10));
        theme.SetConstant("outline_size", "Label", 0);
        Theme = theme;
        var backdrop = new ArenaBackground { Name = "ArenaBackdrop" };
        AddChild(backdrop);
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _yuno = new YunoPortrait
        {
            Name = "YunoPortrait", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill,
            PortraitHeight = 150, AccentColor = Mint
        };
        _enemyYuno = new YunoPortrait
        {
            Name = "EnemyYunoPortrait", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill,
            PortraitHeight = 150, FlipHorizontal = true, AccentColor = Coral
        };
        _die = new DiceFace { Name = "AnimatedDice", CustomMinimumSize = new Vector2(284, 218) };
        _die.RollFinished += Render;
        BuildDiceOverlay();
        _lifeMeters = new[] { new LifeMeter { Name = "OwnLifeMeter" }, new LifeMeter { Name = "EnemyLifeMeter" } };
        NewGame();
        var args = OS.GetCmdlineUserArgs();
        if (args.Contains("--smoke-test")) CallDeferred(nameof(SmokeTest));
        if (args.Contains("--pile-animation-test")) CallDeferred(nameof(PileAnimationSmokeTest));
        if (args.Contains("--attack-timing-test")) CallDeferred(nameof(AttackTimingSmokeTest));
        if (args.Contains("--animation-test")) CallDeferred(nameof(AnimationSmokeTest));
        if (args.Contains("--dice-test")) CallDeferred(nameof(DiceSmokeTest));
        if (args.Contains("--visual-test")) CallDeferred(nameof(VisualSmokeTest));
        if (args.Contains("--relikia-test")) CallDeferred(nameof(RelikiaSmokeTest));
        if (args.Contains("--opponent-hand-test")) CallDeferred(nameof(OpponentHandSmokeTest));
        if (args.Contains("--load-error-test")) CallDeferred(nameof(LoadErrorSmokeTest));
        if (args.Contains("--combo-test")) CallDeferred(nameof(ComboSmokeTest));
        if (args.Contains("--points-test")) CallDeferred(nameof(PointsSmokeTest));
        if (args.Contains("--extra-life-test")) CallDeferred(nameof(ExtraLifeSmokeTest));
        if (args.Contains("--box-test")) CallDeferred(nameof(BoxSmokeTest));
        if (args.Contains("--senshuni-test")) CallDeferred(nameof(SenshuniSmokeTest));
        if (args.Contains("--equipment-test")) CallDeferred(nameof(EquipmentSmokeTest));
        if (args.Contains("--evolution-test")) CallDeferred(nameof(EvolutionSmokeTest));
        if (args.Contains("--yuno-test")) CallDeferred(nameof(YunoSmokeTest));
        if (args.Contains("--skills-test")) CallDeferred(nameof(SkillsSmokeTest));
        if (args.Contains("--bonus-animation-test")) CallDeferred(nameof(BonusAnimationSmokeTest));
        if (args.Contains("--opening-animation-test")) CallDeferred(nameof(OpeningAnimationSmokeTest));
        if (args.Contains("--draw-test")) CallDeferred(nameof(DrawSmokeTest));
        if (args.Contains("--memory-test")) CallDeferred(nameof(MemorySmokeTest));
        if (args.Any(a => a.StartsWith("--dice-frames="))) CallDeferred(nameof(CaptureDiceFrames));
        if (args.Any(a => a.StartsWith("--screenshot="))) CallDeferred(nameof(CapturePreview));
    }

    private void NewGame()
    {
        _seed = Random.Shared.Next();
        BindGame(new GameEngine(_seed));
        _yuno.Play(PortraitMotion.Idle);
        _enemyYuno.Play(PortraitMotion.Idle);
        _die.Value = 0;
        _dicePreview = false;
        _selectedCard = -1;
        _selectedSkill = 0;
        _mulligan.Clear();
        _feedback = "";
        _botElapsed = 0;
        _handScrollPosition = 0;
        _handScroll = null;
        _opponentHandScrollPosition = 0;
        _opponentHandScroll = null;
        QueueInitialHands();
        Render();
    }

    private void BindGame(GameEngine game)
    {
        ResetDrawAnimations();
        _pendingCombatAnimations.Clear();
        _game = game;
        _game.CardDrawn += QueueCardDraw;
        _game.CardMovedToPile += QueuePileMovement;
        _game.CardReturnedToDeck += QueueCardReturn;
        _game.CardsReturnedToDeck += owner => _drawQueue.Enqueue(new(owner, -1, Shuffle: true, Duration: 0.72));
        _game.BonusPlaced += (owner, id) => _drawQueue.Enqueue(new(owner, -1, BonusDestination: true, CardId: id));
        _game.BonusClaimed += (owner, recipient, slot, id) => _drawQueue.Enqueue(new(recipient, slot, BonusSource: owner, CardId: id));
        _die.Value = 0;
        _dicePreview = false;
        for (var i = 0; i < _lifeMeters.Length; i++)
            _lifeMeters[i].SetHealth(game.State.Players[i].Health, game.State.Players[i].MaxHealth, false);
        _game.DieRolled += value =>
        {
            if (_game.State.Phase == Phase.Preparation) return;
            _dicePreview = false;
            _die.QueueRoll(value, _game.CurrentDieCaption);
        };
        _game.AttackPerformed += actor => _pendingCombatAnimations.Add((actor, PortraitMotion.Attack));
        _game.DamageReceived += (target, _) => _pendingCombatAnimations.Add((target, PortraitMotion.Hit));
    }

    private YunoPortrait PortraitFor(int player) => player == 0 ? _yuno : _enemyYuno;

    private void PreviewPortrait(int player, PortraitMotion motion)
    {
        PortraitFor(player).Play(motion);
        if (motion == PortraitMotion.Attack) PortraitFor(1 - player).Play(PortraitMotion.Hit);
    }

    private void PreviewDice(int value)
    {
        if (_die.IsBusy || _helpOpen) return;
        _dicePreview = true;
        _die.RollTo(value);
        Render();
    }

    public override void _Process(double delta)
    {
        UpdateDiceOverlay();
        UpdateDrawAnimations(delta);
        PlayPendingCombatAnimations();
        var s = _game.State;
        if (s.DecisionPlayer == 0 && !_helpOpen && !_die.IsBusy && !CardsInMotion &&
            s.Phase is Phase.DefenseChoice or Phase.MagnetChoice or Phase.AttackRerollChoice or Phase.DieRerollChoice or Phase.CodeResponse or Phase.PointsChoice or Phase.DestroyChoice or Phase.ErrorResponse) ShowPendingSkillChoice();
        if (s.Phase == Phase.RelikiaChoice && s.DecisionPlayer == 0 && !_helpOpen && !_die.IsBusy && !CardsInMotion)
            ShowPendingRelikiaChoice();
        if (s.IsOver || s.Phase == Phase.Preparation || s.DecisionPlayer != 1 || CardsInMotion || _helpOpen || _yuno.IsActionPlaying || _enemyYuno.IsActionPlaying || _die.IsBusy)
        {
            _botElapsed = 0;
            return;
        }
        _botElapsed += delta;
        if (_botElapsed < 0.8) return;
        _botElapsed = 0;
        var result = PracticeBot.Step(_game, 1);
        if (!result.Success) GD.PushError(result.Message);
        _selectedCard = -1;
        Render();
    }

    private bool CanUseSelected()
    {
        var s = _game.State;
        var p = s.Players[0];
        if (_selectedCard < 0 || _selectedCard >= p.Hand.Count) return false;
        var card = CardCatalog.Get(p.Hand[_selectedCard]);
        if (s.Phase == Phase.Discard) return s.DecisionPlayer == 0 && s.DiscardsRemaining > 0;
        if (card.Kind == CardKind.Code) return _game.CodeUnavailableReason(0, _selectedCard) == null;
        if (s.Phase == Phase.Actions) return s.ActionsLeft > 0 && card.Kind == CardKind.Equipment;
        if (s.Phase != Phase.Combat || s.AttackedThisTurn) return false;
        return card.Kind == CardKind.Attack ? p.Skills.Count > 0
            : card.Kind == CardKind.Skill && !s.LearnedThisTurn && p.Skills.Count < GameEngine.MaxSkills &&
              p.Relikia >= _game.SkillCost(p);
    }

    private string SelectedVerb()
    {
        var s = _game.State;
        if (s.Phase == Phase.Discard) return "Descartar por efecto";
        var p = s.Players[0];
        if (_selectedCard < 0 || _selectedCard >= p.Hand.Count) return "Selecciona una carta";
        return CardCatalog.Get(p.Hand[_selectedCard]).Kind switch
        {
            CardKind.Skill => p.Skills.Count >= GameEngine.MaxSkills ? "Sin espacios disponibles" : $"Enseñar · {_game.SkillCost(p)} PR",
            CardKind.Attack => "Atacar y lanzar dado",
            CardKind.Equipment => "Poner equipo · 1 acción",
            _ => CardCatalog.Get(p.Hand[_selectedCard]).IsCombo ? $"COMBO · {CardCatalog.Get(p.Hand[_selectedCard]).ActionCost} acción(es)" : $"Jugar código · 1 acción{(CardCatalog.Get(p.Hand[_selectedCard]).PrCost > 0 ? " · 200 PR" : "")}"
        };
    }

    private void UseSelected()
    {
        var s = _game.State;
        if (_selectedCard < 0 || _selectedCard >= s.Players[0].Hand.Count) return;
        if (s.Phase == Phase.Discard) { Execute(_game.Discard(0, _selectedCard)); return; }
        var card = CardCatalog.Get(s.Players[0].Hand[_selectedCard]);
        Execute(card.Kind switch
        {
            CardKind.Skill => _game.Learn(0, _selectedCard),
            CardKind.Attack => _game.Attack(0, _selectedCard, _selectedSkill),
            CardKind.Equipment => _game.Equip(0, _selectedCard),
            _ => _game.PlayCode(0, _selectedCard)
        });
    }

    private void Execute(CommandResult result)
    {
        _feedback = result.Success ? "" : result.Message;
        if (result.Success) _selectedCard = -1;
        Render();
    }

    private string Hint()
    {
        if (CardsInMotion) return "Robando cartas…";
        if (_die.IsBusy) return "Espera a que el dado se detenga…";
        var s = _game.State;
        if (s.Phase == Phase.DieRerollChoice) return s.DecisionPlayer == 0 ? "Yuno Lv 2: decide si conservas este dado o lo relanzas." : "El rival decide si relanza su dado con Yuno Lv 2.";
        if (s.Phase == Phase.AttackRerollChoice) return s.DecisionPlayer == 0 ? "Yuno: puedes conservar el dado o relanzarlo una vez." : "El rival decide si relanza su dado con Yuno.";
        if (s.Phase == Phase.ErrorResponse) return "Error de Carga: responde a la activación o deja resolver la cadena.";
        if (s.Phase == Phase.DestroyChoice) return "Huevo Azul Explosivo: elige una no-habilidad del campo para destruir.";
        if (s.Phase == Phase.PointsChoice) return "Aumento de Relick Points: elige un jugador y si aumentas o disminuyes sus PR.";
        if (s.Phase == Phase.CodeResponse) return s.DecisionPlayer == 0 ? "Puedes destruir Caja de Cartón para cancelar el código objeto." : "El rival decide si cancela el código objeto.";
        if (s.Phase == Phase.DefenseChoice) return s.DecisionPlayer == 0 ? "Puedes cancelar el daño con una respuesta disponible." : "El rival decide si cancela el daño.";
        if (s.Phase == Phase.MagnetChoice) return s.DecisionPlayer == 0 ? "Magnet Shuffle: elige una carta de la mano rival o de su Bonus." : "El rival elige una carta por Magnet Shuffle.";
        if (s.IsOver) return "El duelo terminó. Puedes empezar otra partida cuando quieras.";
        if (s.Phase == Phase.Preparation) return "Tienes una habilidad y 4 cartas. Puedes cambiar cualquier selección una sola vez.";
        if (s.Phase == Phase.Discard) return s.DecisionPlayer == 0
            ? $"Relikia o un efecto requiere descartar {s.DiscardsRemaining} carta(s). Tú eliges cuáles de tu mano."
            : $"El rival elige {s.DiscardsRemaining} carta(s) para descartar por efecto.";
        if (s.CurrentPlayer == 1) return "El rival está pensando… puedes consultar tus cartas mientras juega.";
        if (s.Phase == Phase.RelikiaChoice) return "Completa la elección del efecto de Relikia para continuar.";
        if (s.Phase == Phase.Actions) return $"Te quedan {s.ActionsLeft} acciones: juega un código, canjea o usa efectos de Relikia.";
        if (s.AttackedThisTurn) return "Ataque resuelto. Continúa a la etapa de acciones.";
        if (s.Active.Skills.Count == 0) return "Primero selecciona una habilidad de tu mano y pulsa Enseñar.";
        return "Selecciona una carta de ataque. Puedes elegir tu habilidad en el tablero.";
    }

    private void ShowHelp()
    {
        _helpOpen = true;
        var popup = new AcceptDialog
        {
            Title = "Cómo jugar · Demo de práctica",
            DialogText = "OBJETIVO\nLleva al rival a 0 vidas y causa un nuevo daño para ganar. Si se cura tras llegar a 0, volver a 0 lo derrota.\n\nTU TURNO\nRobo automático: 1 carta, o 3 si tu mano está vacía.\nCombate: enseña una habilidad y juega un ataque.\nAcciones: juega códigos, canjea cartas o usa efectos de Relikia (hasta 2 acciones).\nFinal: termina tu turno. No hay límite de cartas en mano; solo se descarta si un efecto de carta lo indica.\n\nYUNO LV 1\nVida máxima: 8. Una vez por turno puedes relanzar tu dado de ataque si no salió 1. Un 6 en ese relanzamiento se convierte en 5. No afecta al dado de daño de Magnet ni a otras tiradas.\n\nEVOLUCIÓN A LV 2\nConsigue un 6 natural durante la partida (sin relanzar ni modificar). La condición se conserva. En Combate o Acciones paga 1800 PR: ganas 2 vidas y tu máximo sube a 10. En Lv 2 puedes relanzar un dado propio hasta 2 veces, una cadena por turno. Un 1 detiene la cadena y un 6 conserva su valor.\n\nDADO\n1–3: fallo · 4: mitad del daño, redondeada hacia arriba.\n5: daño normal · 6: daño crítico. Un acierto otorga PR.\n\nEN ESTA DEMO\nMazos de 20 cartas y relikarios de práctica con 8 vidas.\nCuatro espacios de habilidad: 0 / 300 / 500 / 1000 PR.\nEl primer reciclaje de memoria se elige automáticamente.\nEquipo: poner cuesta 1 acción. Solo uno a la vez; el anterior va a memoria al sustituirlo. Colgante permite destruirlo para relanzar o sumar +1/+2 a un dado relanzado, sin alcanzar 6 por suma al atacar. Senshuni roba 2 cartas por 1 acción. Caja de Cartón se destruye para cancelar daño de un ataque o la activación de un código objeto; Senshuni es de personaje y no puede ser cancelada por la Caja. Vida Extra es objeto: lanza D6 y cura 1/2/3/3/3/4 vidas según la cara, sin superar tu máximo. La Caja puede cancelarla antes de lanzar.\nRelikia: 100 PR cambia Bonus; 300 PR roba y coloca; 400 PR activa un código de memoria y lo corrompe (2 acciones); 500 PR roba Bonus y lanza D6.\nYuno puede evolucionar a Lv 2. Sin áreas. Los COMBO pueden usarse fuera de Acciones y en turno enemigo si cumplen requisitos. Cada jugador tiene 2 acciones por turno de juego. Re-lanza es gratis: relanza un dado propio y roba 1 carta si sale 1–3. Error de Carga cuesta 1 acción y anula un código o la colocación de un equipo. Otro Error puede negarlo. Las activaciones anuladas recuperan todas sus acciones y PR; el equipo anterior se conserva si se niega el nuevo. Huevo Azul cuesta 200 PR y 1 acción para destruir una no-habilidad del campo.\nHabilidades: Magnet Shuffle y Destiny Shuffle, con sus efectos impresos. Se pueden enseñar copias repetidas (una habilidad por turno, antes de atacar). El catálogo solo contiene las cartas reales aportadas.",
            MinSize = new Vector2I(650, 520)
        };
        popup.Confirmed += () => { _helpOpen = false; popup.QueueFree(); };
        popup.Canceled += () => { _helpOpen = false; popup.QueueFree(); };
        AddChild(popup);
        popup.PopupCentered();
    }

    private void ConfirmRestart()
    {
        if (_game.State.Phase == Phase.Preparation || _game.State.IsOver) { NewGame(); return; }
        _helpOpen = true;
        var popup = new ConfirmationDialog
        {
            Title = "Nueva partida", DialogText = "¿Empezar un nuevo duelo? Se perderá la partida actual.",
            OkButtonText = "Nueva partida", CancelButtonText = "Continuar duelo"
        };
        popup.Confirmed += () => { _helpOpen = false; popup.QueueFree(); NewGame(); };
        popup.Canceled += () => { _helpOpen = false; popup.QueueFree(); };
        AddChild(popup);
        popup.PopupCentered();
    }

    private static string PhaseName(Phase phase) => phase switch
    {
        Phase.Preparation => "Preparación", Phase.Combat => "Combate", Phase.Actions => "Acciones",
        Phase.DieRerollChoice => "Habilidad de Yuno Lv 2",
        Phase.AttackRerollChoice => "Habilidad de Yuno",
        Phase.ErrorResponse => "Cadena de respuestas",
        Phase.DestroyChoice => "Destruir una carta del campo",
        Phase.PointsChoice => "Elegir cambio de PR",
        Phase.CodeResponse => "Respuesta a código objeto", Phase.DefenseChoice => "Respuesta al ataque", Phase.MagnetChoice => "Efecto de Magnet",
        Phase.Discard => "Descarte por efecto", Phase.RelikiaChoice => "Efecto de Relikia", _ => "Fin del duelo"
    };

    private static StyleBoxFlat Box(string bg, string border, int radius, int width = 1) => new()
    {
        BgColor = new Color(bg), BorderColor = new Color(border),
        BorderWidthLeft = width, BorderWidthTop = width, BorderWidthRight = width, BorderWidthBottom = width,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 12, ContentMarginBottom = 12
    };

    private static Label Label(string text, int size, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static Label Wrapped(string text, int size, Color color, float width)
    {
        var label = Label(text, size, color);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(width, 0);
        return label;
    }

    private static PanelContainer Panel(Node parent, string bg, string border)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", Box(bg, border, 9));
        parent.AddChild(panel);
        return panel;
    }

    private static Button Button(string text, Action pressed, bool disabled = false, float width = 0)
    {
        var button = new Button
        {
            Text = text, Disabled = disabled, CustomMinimumSize = new Vector2(width, 40),
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        button.Pressed += pressed;
        return button;
    }

    private static Button PrimaryButton(string text, Action pressed, bool disabled = false)
    {
        var button = Button(text, pressed, disabled, 225);
        button.AddThemeStyleboxOverride("normal", Box("75e6bd", "75e6bd", 7));
        button.AddThemeStyleboxOverride("hover", Box("98f4d0", "98f4d0", 7));
        button.AddThemeStyleboxOverride("pressed", Box("58b998", "58b998", 7));
        button.AddThemeColorOverride("font_color", new Color("102b25"));
        button.AddThemeColorOverride("font_hover_color", new Color("102b25"));
        button.AddThemeColorOverride("font_pressed_color", new Color("102b25"));
        return button;
    }

    private static void IgnoreMouse(Control node)
    {
        node.MouseFilter = MouseFilterEnum.Ignore;
        foreach (var child in node.GetChildren()) if (child is Control control) IgnoreMouse(control);
    }

    private async void CapturePreview()
    {
        try
        {
            var args = OS.GetCmdlineUserArgs();
            var path = args.First(a => a.StartsWith("--screenshot="))["--screenshot=".Length..];
            if (args.Contains("--demo-turn") || args.Contains("--demo-start"))
            {
                BindGame(new GameEngine(42));
                _game.Start(Array.Empty<int>());
                for (var i = 0; args.Contains("--demo-turn") && i < 100 && !_game.State.IsOver; i++)
                {
                    if (_game.State.Turn >= 5 && _game.State.CurrentPlayer == 0) break;
                    PracticeBot.Step(_game, _game.State.DecisionPlayer);
                }
                _die.Value = _game.State.LastDie;
                if (args.Contains("--demo-select") && _game.State.Players[0].Hand.Count > 0) _selectedCard = 0;
                Render();
            }
            // Pause bot choices while the rendered frame is captured.
            _helpOpen = true;
            var diceValue = args.FirstOrDefault(a => a.StartsWith("--dice-value="))?["--dice-value=".Length..];
            if (int.TryParse(diceValue, out var fixedValue))
            {
                _die.SetProcess(false);
                _die.Value = fixedValue;
            }
            if (args.Contains("--dice-roll"))
            {
                _dicePreview = true;
                _die.SetProcess(false);
                _die.RollTo(6);
                _die.AdvancePlayback(0.43);
                Render();
            }
            var animation = args.FirstOrDefault(a => a.StartsWith("--portrait="))?["--portrait=".Length..];
            if (Enum.TryParse<PortraitMotion>(animation, true, out var motion))
            {
                _yuno.SetProcess(false);
                _yuno.Play(motion);
                _yuno.AdvancePlayback(_yuno.DurationSeconds * (motion == PortraitMotion.Idle ? 0.25 : 0.42));
            }
            var enemyAnimation = args.FirstOrDefault(a => a.StartsWith("--enemy-portrait="))?["--enemy-portrait=".Length..];
            if (Enum.TryParse<PortraitMotion>(enemyAnimation, true, out var enemyMotion))
            {
                _enemyYuno.SetProcess(false);
                _enemyYuno.Play(enemyMotion);
                _enemyYuno.AdvancePlayback(_enemyYuno.DurationSeconds * (enemyMotion == PortraitMotion.Idle ? 0.25 : 0.42));
            }
            if (args.Contains("--bonus-preview"))
            {
                _helpOpen = false;
                ShowBonus(0);
            }
            if (args.Contains("--attachments-preview"))
            {
                _helpOpen = false;
                ShowAttachedCards(0);
            }
            if (args.Contains("--relikia-preview"))
            {
                _game.State.Phase = Phase.Actions;
                _game.State.CurrentPlayer = 0;
                _game.State.ActionsLeft = 2;
                _game.State.Active.Relikia = 1000;
                _game.State.Active.Memory.AddRange(new[] { "lucky-pendant", "lucky-pendant" });
                Render();
                _helpOpen = false;
                ShowRelikiaEffects();
            }
            if (args.Contains("--glow-preview"))
            {
                _game.State.Phase = Phase.Actions;
                _game.State.CurrentPlayer = 0;
                _game.State.ActionsLeft = 0;
                _game.State.Active.Relikia = 0;
                Render();
            }
            if (args.Contains("--opponent-reveal-preview"))
            {
                _game.State.Players[1].RevealHandCard(0);
                Render();
            }
            if (args.Contains("--skills-preview"))
            {
                foreach (var player in _game.State.Players)
                {
                    player.Skills.Clear();
                    player.Skills.AddRange(new[] { "magnet-shuffle", "destiny-shuffle", "magnet-shuffle", "destiny-shuffle" });
                }
                Render();
            }
            if (args.Contains("--memory-zones-preview") || args.Contains("--memory-preview") || args.Contains("--corrupted-memory-preview"))
            {
                _game.State.CurrentPlayer = 0;
                _game.State.Phase = Phase.Actions;
                _game.State.ActionsLeft = 2;
                var own = _game.State.Players[0];
                own.Memory.Clear(); own.Memory.AddRange(new[] { "attack", "lucky-pendant", "lucky-pendant", "magnet-shuffle", "attack" });
                own.CorruptedMemory.Clear(); own.CorruptedMemory.AddRange(new[] { "lucky-pendant", "lucky-pendant" });
                var rival = _game.State.Players[1];
                rival.Memory.Clear(); rival.Memory.AddRange(new[] { "attack", "destiny-shuffle", "lucky-pendant" });
                rival.CorruptedMemory.Clear(); rival.CorruptedMemory.Add("lucky-pendant");
                Render();
                if (!args.Contains("--memory-zones-preview"))
                {
                    _helpOpen = false;
                    ShowMemory(0, args.Contains("--corrupted-memory-preview"));
                }
            }
            foreach (var meter in _lifeMeters)
            {
                meter.AdvancePlayback(2);
                meter.SetProcess(false);
            }
            if (args.Contains("--life-preview"))
            {
                _lifeMeters[0].SetHealth(3, 8, false);
                _lifeMeters[1].SetHealth(12, 80, false);
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var error = GetViewport().GetTexture().GetImage().SavePng(path);
            if (error != Error.Ok) throw new InvalidOperationException(error.ToString());
            GD.Print($"PREVIEW_SAVED {path} · layout {_root.GetCombinedMinimumSize()} / {_root.Size}");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private async void CaptureDiceFrames()
    {
        try
        {
            _helpOpen = true;
            _die.SetProcess(false);
            var directory = OS.GetCmdlineUserArgs().First(a => a.StartsWith("--dice-frames="))["--dice-frames=".Length..];
            var error = DirAccess.MakeDirRecursiveAbsolute(directory);
            if (error != Error.Ok) throw new InvalidOperationException(error.ToString());
            _die.RollTo(6);
            // Allow the off-screen 3D viewport to render before capturing its first frame.
            for (var warmup = 0; warmup < 2; warmup++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            }
            var frames = (int)Math.Ceiling((DiceFace.RollSeconds + DiceFace.ResultSeconds) * 60);
            for (var frame = 0; frame < frames; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var image = _die.Capture3DFrame();
                error = image.SavePng($"{directory}/dice-{frame:000}.png");
                if (error != Error.Ok) throw new InvalidOperationException(error.ToString());
                _die.AdvancePlayback(1.0 / 60);
            }
            GD.Print($"DICE_FRAMES_SAVED {directory} · {frames} frames at 60 fps.");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private async void DiceSmokeTest()
    {
        try
        {
            _helpOpen = true;
            _die.SetProcess(false);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!_die.Is3DReady) throw new InvalidOperationException("No se creó la escena 3D del dado.");
            if (_die.IsPresented || _diceOverlay.Visible) throw new InvalidOperationException("El dado aparece en reposo.");
            var finished = 0;
            _die.RollFinished += () => finished++;
            foreach (var rate in new[] { 60, 120 })
            {
                for (var value = 1; value <= 6; value++)
                {
                    _die.RollTo(value);
                    for (var i = 0; i < rate / 2; i++)
                    {
                        _die.AdvancePlayback(1.0 / rate);
                        if (_die.LowestPointHeight < -0.6801f)
                            throw new InvalidOperationException("El dado atraviesa la mesa al girar.");
                    }
                    if (!_die.IsRolling || _die.DisplayedValue < 1 || _die.DisplayedValue > 6)
                        throw new InvalidOperationException("El dado no mostró caras válidas durante el lanzamiento.");
                    var face = _die.DisplayedValue;
                    Render();
                    if (!_die.IsRolling || _die.Value != value || _die.DisplayedValue != face || !_diceOverlay.Visible)
                        throw new InvalidOperationException("Actualizar el tablero reinició el lanzamiento.");
                    for (var i = 0; i < rate; i++) _die.AdvancePlayback(1.0 / rate);
                    if (_die.IsRolling || _die.DisplayedValue != value || _die.TopFaceValue != value || _die.ResultUpAlignment < 0.9999f)
                        throw new InvalidOperationException($"La cara superior no muestra {value}, a {rate} fps.");
                    if (!_die.IsPresented) throw new InvalidOperationException("El resultado desaparece antes de poder leerlo.");
                    _die.AdvancePlayback(DiceFace.ResultSeconds);
                    UpdateDiceOverlay();
                    if (_die.IsPresented || _diceOverlay.Visible)
                        throw new InvalidOperationException("El dado permanece en pantalla tras mostrar el resultado.");
                }
            }
            if (finished != 12) throw new InvalidOperationException("Final de lanzamiento duplicado o ausente.");
            _die.RollTo(6);
            var initialHeight = _die.HeightAboveTable;
            _die.AdvancePlayback(0.34);
            if (_die.HeightAboveTable > 0.0001f) throw new InvalidOperationException("El dado no toca la mesa en el primer impacto.");
            _die.AdvancePlayback(0.21);
            var firstBounce = _die.HeightAboveTable;
            _die.AdvancePlayback(0.21 + 0.13);
            var secondBounce = _die.HeightAboveTable;
            _die.AdvancePlayback(0.13 + 0.08);
            var thirdBounce = _die.HeightAboveTable;
            if (!(initialHeight > firstBounce && firstBounce > secondBounce && secondBounce > thirdBounce && thirdBounce > 0))
                throw new InvalidOperationException("Los rebotes no pierden altura progresivamente.");
            _die.AdvancePlayback(3);
            var before = System.Text.Json.JsonSerializer.Serialize(_game.State);
            _helpOpen = false;
            PreviewDice(4);
            _helpOpen = true;
            _die.AdvancePlayback(2);
            if (System.Text.Json.JsonSerializer.Serialize(_game.State) != before)
                throw new InvalidOperationException("Probar el dado modificó la partida.");

            BindGame(new GameEngine(42));
            _game.Start(Array.Empty<int>());
            Render();
            if (_die.IsPresented || _diceOverlay.Visible)
                throw new InvalidOperationException("El sorteo inicial deja el dado visible sin un ataque o efecto.");
            var attacks = 0;
            _game.AttackPerformed += _ =>
            {
                attacks++;
                if (_game.State.LastAttackDie < 4 || _game.State.PendingAttackRoll != null)
                    throw new InvalidOperationException("El ataque animó un resultado fallido o sin confirmar.");
            };
            for (var i = 0; i < 1500 && !_game.State.IsOver; i++)
            {
                PracticeBot.Step(_game, _game.State.DecisionPlayer);
                DrainTestDice();
            }
            if (attacks == 0) throw new InvalidOperationException("No se comprobaron ataques reales.");
            BindGame(new GameEngine(42));
            _game.State.Phase = Phase.Actions; _game.State.CurrentPlayer = 0; _game.State.ActionsLeft = 2;
            var player = _game.State.Active;
            player.Hand.Clear(); player.Hand.Add("lucky-pendant");
            player.Deck.Clear(); player.Deck.Add("attack");
            player.Memory.Clear(); player.Memory.AddRange(new[] { "lucky-pendant", "attack", "attack" });
            player.Relikia = 300;
            if (!_game.UseRelikiaEffect(0, RelikiaEffect.DrawAndPlace).Success || !_die.IsRolling || _die.Value != _game.State.LastDie)
                throw new InvalidOperationException("La tirada de reciclaje causada por una carta no muestra el dado.");
            _die.RollTo(6);
            NewGame();
            if (_die.IsRolling || _die.Value != 0) throw new InvalidOperationException("Reiniciar no canceló el lanzamiento.");
            GD.Print($"DICE_SMOKE_PASS · dado temporal, rebotes decrecientes, contacto con mesa, 6 caras superiores a 60/120 fps, continuidad, reinicio y {attacks} ataques reales.");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    // Explicit integration check: real textures, timing, UI rebuilds and combat events.
    private async void AnimationSmokeTest()
    {
        try
        {
            _helpOpen = true;
            _yuno.SetProcess(false);
            _enemyYuno.SetProcess(false);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!_yuno.AssetsReady || !_enemyYuno.AssetsReady)
                throw new InvalidOperationException("No se cargaron las animaciones de ambos relikarios.");
            if (_yuno.FlipHorizontal || !_enemyYuno.FlipHorizontal)
                throw new InvalidOperationException("Los relikarios no están orientados en sentidos opuestos.");
            _yuno.Play(PortraitMotion.Attack);
            _enemyYuno.Play(PortraitMotion.Hit);
            _yuno.AdvancePlayback(_yuno.DurationSeconds * 0.25);
            _enemyYuno.AdvancePlayback(_enemyYuno.DurationSeconds * 0.5);
            var ownFrame = _yuno.CurrentFrame;
            var enemyFrame = _enemyYuno.CurrentFrame;
            Render();
            if (_yuno.CurrentFrame != ownFrame || _enemyYuno.CurrentFrame != enemyFrame)
                throw new InvalidOperationException("Actualizar la interfaz reinició alguno de los relikarios.");
            _yuno.AdvancePlayback(_yuno.DurationSeconds + 0.01);
            if (_enemyYuno.Motion != PortraitMotion.Hit || _enemyYuno.CurrentFrame != enemyFrame)
                throw new InvalidOperationException("Los relikarios comparten su reloj de animación.");
            foreach (var portrait in new[] { _yuno, _enemyYuno })
            foreach (var motion in new[] { PortraitMotion.Attack, PortraitMotion.Hit })
            {
                portrait.Play(motion);
                var duration = portrait.DurationSeconds;
                portrait.AdvancePlayback(duration * 0.5);
                var frame = portrait.CurrentFrame;
                Render();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (portrait.Motion != motion || portrait.CurrentFrame != frame)
                    throw new InvalidOperationException("Actualizar la interfaz reinició la animación.");
                portrait.AdvancePlayback(duration * 0.5 + 0.01);
                if (portrait.Motion != PortraitMotion.Idle) throw new InvalidOperationException("La acción no volvió a reposo.");
            }
            foreach (var portrait in new[] { _yuno, _enemyYuno })
            foreach (var (motion, expectedFrame) in new[] { (PortraitMotion.Idle, 16), (PortraitMotion.Attack, 23), (PortraitMotion.Hit, 35) })
            foreach (var rate in new[] { 60, 120 })
            {
                portrait.Play(motion);
                for (var i = 0; i < rate / 2; i++) portrait.AdvancePlayback(1.0 / rate);
                if (portrait.Motion != motion || portrait.CurrentFrame != expectedFrame)
                    throw new InvalidOperationException($"Velocidad incorrecta para {motion} a {rate} fps.");
            }
            _yuno.Play(PortraitMotion.Idle);
            _yuno.AdvancePlayback(_yuno.DurationSeconds * 2.5);
            if (_yuno.Motion != PortraitMotion.Idle || _yuno.CurrentFrame != 32)
                throw new InvalidOperationException("El reposo no repite con la nueva duración.");

            var beforePreview = System.Text.Json.JsonSerializer.Serialize(_game.State);
            PreviewPortrait(1, PortraitMotion.Attack);
            if (_enemyYuno.Motion != PortraitMotion.Attack || _yuno.Motion != PortraitMotion.Hit)
                throw new InvalidOperationException("La vista previa del rival no produjo la reacción correcta.");
            PreviewPortrait(0, PortraitMotion.Attack);
            if (_yuno.Motion != PortraitMotion.Attack || _enemyYuno.Motion != PortraitMotion.Hit ||
                beforePreview != System.Text.Json.JsonSerializer.Serialize(_game.State))
                throw new InvalidOperationException("La vista previa alteró la partida o el relikario equivocado.");

            var attacks = new int[2];
            var hits = new int[2];
            void VerifyAttack(int actor)
            {
                attacks[actor]++;
                if (_pendingCombatAnimations.Last() != (actor, PortraitMotion.Attack))
                    throw new InvalidOperationException("El ataque no encoló al relikario correcto.");
            }
            void VerifyDamage(int target, int amount)
            {
                hits[target]++;
                if (_pendingCombatAnimations.Last() != (target, PortraitMotion.Hit))
                    throw new InvalidOperationException("El daño no encoló al relikario correcto.");
            }
            var checkRandom = new SkillsTestRandom();
            BindGame(new GameEngine(checkRandom));
            _game.AttackPerformed += VerifyAttack; _game.DamageReceived += VerifyDamage;
            if (_game.Attack(0, 0, 0).Success || attacks.Sum() != 0 || hits.Sum() != 0)
                throw new InvalidOperationException("Una jugada inválida produjo una animación.");
            // Guarantee both event directions independently of the bot's changing strategy.
            for (var actor = 0; actor < 2; actor++)
            {
                _game.State.Phase = Phase.Combat; _game.State.CurrentPlayer = actor; _game.State.AttackedThisTurn = false;
                foreach (var player in _game.State.Players) { player.Hand.Clear(); player.Skills.Clear(); }
                _game.State.Active.Hand.Add("attack"); _game.State.Active.Skills.Add(CardCatalog.Destiny.Id);
                _yuno.Play(PortraitMotion.Idle); _enemyYuno.Play(PortraitMotion.Idle);
                checkRandom.Enqueue(5);
                if (!_game.Attack(actor, 0, 0).Success || !_game.ResolveAttackReroll(actor, false).Success)
                    throw new InvalidOperationException("No se pudo verificar el ataque de ambos relikarios.");
            }
            BindGame(new GameEngine(42));
            _game.AttackPerformed += VerifyAttack; _game.DamageReceived += VerifyDamage;
            _game.Start(Array.Empty<int>());
            for (var i = 0; i < 1500 && !_game.State.IsOver; i++)
            {
                _yuno.Play(PortraitMotion.Idle);
                _enemyYuno.Play(PortraitMotion.Idle);
                var result = PracticeBot.Step(_game, _game.State.DecisionPlayer);
                if (!result.Success) throw new InvalidOperationException(result.Message);
            }
            if (attacks.Any(count => count == 0) || hits.Any(count => count == 0))
                throw new InvalidOperationException("El duelo no verificó ataque y daño de ambos relikarios.");
            NewGame();
            if (_yuno.Motion != PortraitMotion.Idle || _enemyYuno.Motion != PortraitMotion.Idle)
                throw new InvalidOperationException("Reinicio sin reposo en ambos relikarios.");
            GD.Print($"ANIMATION_SMOKE_PASS · 2 relikarios independientes, 60/120 fps, continuidad, vistas previas sin mutaciones, ataques {attacks[0]}/{attacks[1]} e impactos {hits[0]}/{hits[1]} reales.");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    // Integration check, invoked explicitly; it runs the real scene and real rules.
    private async void SmokeTest()
    {
        try
        {
            _helpOpen = true;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Execute(_game.Start(Array.Empty<int>()));
            var steps = 0;
            while (!_game.State.IsOver && steps++ < 1500)
            {
                var result = PracticeBot.Step(_game, _game.State.DecisionPlayer);
                if (!result.Success) throw new InvalidOperationException(result.Message);
                Render();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            if (!_game.State.IsOver) throw new InvalidOperationException("El duelo no terminó.");
            NewGame();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var minimum = _root.GetCombinedMinimumSize();
            if (minimum.X > _root.Size.X + 1 || minimum.Y > _root.Size.Y + 1)
                throw new InvalidOperationException($"La interfaz excede la ventana: {minimum} / {_root.Size}");
            GD.Print($"UI_SMOKE_PASS · duelo completo en {steps} decisiones y reinicio correcto.");
            GetTree().Quit();
        }
        catch (Exception e)
        {
            GD.PushError(e.ToString());
            GetTree().Quit(1);
        }
    }
}
