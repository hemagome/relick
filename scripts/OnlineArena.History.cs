using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    private Button? _historyButton;
    private Label? _resolutionStage, _resolutionSource, _resolutionResponses, _resolutionMessage;
    private AcceptDialog? _historyDialog;
    /// <summary>The newest play the rail has shown; a newer one lights up once. Zero until the first board.</summary>
    private ulong _historySeen;
    /// <summary>The play glowing now and when it started, so a rebuilt rail carries the glow on instead of losing it.</summary>
    private (ulong Id, ulong StartMs) _historyGlow;
    private const double HistoryGlowSeconds = 1.4;

    private static string ResolutionStatus(DuelResolutionStatus status) => status switch
    {
        DuelResolutionStatus.Response => "RESPUESTA", DuelResolutionStatus.Resolving => "RESOLVIENDO",
        DuelResolutionStatus.Canceled => "ACTIVACIÓN ANULADA", _ => "RESULTADO"
    };
    private string HistoryPlayer(int seat) => seat is 0 or 1 ? _snapshot!.Players[seat].Name : "Jugador";
    private static string HistoryCard(string id) => CardCatalog.All.TryGetValue(id, out var card) ? card.Name : "";

    private void BuildResolution(VBoxContainer column, Label title, Label message)
    {
        _resolutionStage = title; _resolutionMessage = message;
        message.AddThemeFontSizeOverride("font_size", Compact || Short ? 13 : 14);
        message.MaxLinesVisible = 3; message.CustomMinimumSize = Vector2.Zero;
        var position = title.GetIndex(); column.RemoveChild(title);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6); column.AddChild(row); column.MoveChild(row, position);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.HorizontalAlignment = HorizontalAlignment.Left; title.VerticalAlignment = VerticalAlignment.Center;
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        row.AddChild(title);
        // On desktop the button belongs to the strip of plays and opens the history beside it.
        if (!_inlineHistory)
        {
            _historyButton = new Button { Name = "DuelHistoryButton", Text = "Historial", CustomMinimumSize = new(0, 48), Visible = !_preparing, MouseDefaultCursorShape = CursorShape.PointingHand };
            _historyButton.AddThemeFontSizeOverride("font_size", 12);
            _historyButton.Pressed += ShowDuelHistory; row.AddChild(_historyButton);
        }
        _resolutionSource = ArenaPresentation.Text("", 17);
        _resolutionSource.Name = "ResolutionSource"; _resolutionSource.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _resolutionSource.MaxLinesVisible = 2; _resolutionSource.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(_resolutionSource); column.MoveChild(_resolutionSource, row.GetIndex() + 1);
        _resolutionResponses = ArenaPresentation.Text("", 12, RelickVisuals.State.Target);
        _resolutionResponses.Name = "ResolutionResponses"; _resolutionResponses.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _resolutionResponses.MaxLinesVisible = 2;
        column.AddChild(_resolutionResponses); column.MoveChild(_resolutionResponses, _resolutionSource.GetIndex() + 1);
        UpdateResolutionPresentation();
    }

    private void UpdateResolutionPresentation()
    {
        if (_resolutionMessage == null || _snapshot == null) return;
        var s = _snapshot; var latest = s.History?.LastOrDefault(); var live = s.Resolution;
        if (_historyButton != null && IsInstanceValid(_historyButton))
        {
            _historyButton.Visible = !_preparing && !_readerInGallery;
            _historyButton.Disabled = IsAnimating;
            _historyButton.TooltipText = IsAnimating ? "El historial estará disponible al terminar la resolución visual." : $"Últimas {DuelHistory.Capacity} jugadas públicas.";
        }
        if (_arenaMenu != null)
        {
            var popup = _arenaMenu.GetPopup(); var item = popup.GetItemIndex(3);
            if (item >= 0) popup.SetItemDisabled(item, IsAnimating);
        }
        var hasPublicResolution = !_preparing && (latest != null || live != null);
        _resolutionSource!.Visible = hasPublicResolution;
        _resolutionResponses!.Visible = hasPublicResolution && live?.Responses.Count > 0;
        if (hasPublicResolution)
        {
            var status = live?.Status ?? latest!.Status;
            _resolutionStage!.Text = IsAnimating ? "RESOLVIENDO" : ResolutionStatus(status) + (live != null ? " · " + HistoryPlayer(live.DecisionPlayer) : "");
            if (!IsAnimating && s.Winner != null) _resolutionStage.Text = s.Winner == -1 ? "EMPATE" : "VICTORIA · " + HistoryPlayer(s.Winner.Value);
            var card = HistoryCard(live?.CardId ?? latest!.CardId);
            _resolutionSource.Text = card.Length > 0 ? HistoryPlayer(live?.Actor ?? latest!.SourceActor) + " · " + card : live != null ? latest?.Text ?? "" : "";
            _resolutionSource.Visible = _resolutionSource.Text.Length > 0;
            _resolutionSource.TooltipText = _resolutionSource.Text;
            _resolutionResponses.Text = live == null ? "" : string.Join("\n", live.Responses.TakeLast(2).Select(r => "↳ " + HistoryPlayer(r.Actor) + " · " + HistoryCard(r.CardId)));
            _resolutionResponses.TooltipText = live == null ? "" : string.Join("\n", live.Responses.Select(r => HistoryPlayer(r.Actor) + " · " + HistoryCard(r.CardId)));
            if (latest != null)
            {
                var lines = DuelHistoryText.Lines(latest, HistoryPlayer);
                _resolutionMessage.Text = (live == null ? latest.Text + "\n" : "") + string.Join("\n", lines.Take(2));
                _resolutionMessage.TooltipText = latest.Text + "\n" + string.Join("\n", lines);
                _resolutionMessage.MaxLinesVisible = 3;
                _resolutionMessage.CustomMinimumSize = Vector2.Zero;
            }
        }
        // A message that restates the source line ("Ana · Disquete · …") only keeps what it adds.
        if (_resolutionSource.Visible && _resolutionSource.Text.Length > 0 && _resolutionMessage.Text.Contains(_resolutionSource.Text + " · "))
        {
            var prefix = _resolutionSource.Text + " · ";
            _resolutionMessage.Text = string.Join("\n", _resolutionMessage.Text.Split('\n').Select(line => line.StartsWith(prefix) ? line[prefix.Length..] : line));
        }
        // Preserve legacy-server and preparation messages, including the initiative presentation.
        _resolutionMessage.Visible = !IsAnimating && _resolutionMessage.Text.Length > 0;
        // The heading only appears with something to head: an idle duel shows no empty "resolution".
        _resolutionStage!.Visible = !_preparing && (hasPublicResolution || _resolutionMessage.Visible);
        // Once settled, the strip and the last play tell the result; repeating it here said the same thing twice.
        if (_resolutionPanel != null)
            _resolutionPanel.Visible = (live != null || latest == null || s.Winner != null) && (_resolutionStage!.Visible || _resolutionSource.Visible || _resolutionResponses.Visible || _resolutionMessage.Visible);
    }

    /// <summary>On desktop, what is resolving now heads the reader.</summary>
    private void BuildRailResolution(VBoxContainer parent, string message)
    {
        var panel = new PanelContainer { Name = "ResolutionPanel", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var style = RelickVisuals.Frame(BoardArt.Tint(BoardArt.Gold, 0.10f), BoardArt.Tint(BoardArt.Gold, 0.55f));
        style.ContentMarginLeft = style.ContentMarginRight = 10; style.ContentMarginTop = style.ContentMarginBottom = 6;
        panel.AddThemeStyleboxOverride("panel", style);
        parent.AddChild(panel); _resolutionPanel = panel;
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 2); panel.AddChild(column);
        var title = ArenaPresentation.Text(_snapshot!.Winner != null ? "RESULTADO DEL DUELO" : "RESOLUCIÓN DE COMBATE", 11, BoardArt.Gold);
        title.AddThemeFontOverride("font", RelickVisuals.Display);
        title.Visible = !_preparing; column.AddChild(title);
        var status = ArenaPresentation.Text(message, 13); status.Name = "CombatMessage";
        status.AutowrapMode = TextServer.AutowrapMode.WordSmart; status.Visible = message.Length > 0; column.AddChild(status);
        _inlineHistory = true;
        BuildResolution(column, title, status);
        _resolutionSource!.HorizontalAlignment = HorizontalAlignment.Left;
        _resolutionSource.AddThemeFontSizeOverride("font_size", 15);
        _combatMessage = status;
    }

    /// <summary>The public plays, newest first: in the docked reader or the drawer, when asked for.</summary>
    private void BuildHistoryFeed(VBoxContainer parent)
    {
        var feed = new VBoxContainer { Name = "HistoryFeed", SizeFlagsVertical = SizeFlags.ExpandFill, Visible = false };
        feed.AddThemeConstantOverride("separation", 4); parent.AddChild(feed); _historyFeed = feed;
        var heading = ArenaPresentation.Text("HISTORIAL", 12, BoardArt.Gold);
        heading.AddThemeFontOverride("font", RelickVisuals.Display); feed.AddChild(heading);
        var scroll = new ScrollContainer { Name = "HistoryFeedScroll", SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        feed.AddChild(scroll);
        var list = new VBoxContainer { Name = "HistoryFeedEntries", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 6); scroll.AddChild(list);
        FillHistory(list, "HistoryFeed", false, "Aún no hay jugadas.");
    }

    private void ShowDuelHistory()
    {
        if (_snapshot == null || _preparing || IsAnimating || GetChildren().OfType<Window>().Any(w => w.Visible)) return;
        var dialog = new AcceptDialog { Name = "DuelHistoryDialog", Title = "Historial de jugadas", OkButtonText = "Volver al duelo" };
        var scroll = new ScrollContainer { Name = "HistoryReading", CustomMinimumSize = new(Math.Min(660, Size.X - 64), Math.Min(420, Size.Y - 130)),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var list = new VBoxContainer { Name = "HistoryEntries", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 8); scroll.AddChild(list); dialog.AddChild(scroll); AddChild(dialog);
        FillHistory(list, "History", true, "Las nuevas jugadas públicas aparecerán aquí.");
        _historyDialog = dialog;
        void Close() { _historyDialog = null; dialog.QueueFree(); }
        dialog.Confirmed += Close; dialog.Canceled += Close;
        ArenaViewport.FitDialog(dialog, Size);
    }

    private static string StageName(Phase stage) => stage switch
    {
        Phase.Draw => "ROBO", Phase.Combat => "COMBATE", Phase.Actions => "ACCIONES", Phase.Final => "FINAL", _ => ""
    };

    /// <summary>Consecutive entries of one resolution read as one play, newest play first; inside it, in order.</summary>
    internal static List<DuelHistoryEntry[]> HistoryGroups(IEnumerable<DuelHistoryEntry> entries)
    {
        var groups = new List<DuelHistoryEntry[]>(); var current = new List<DuelHistoryEntry>();
        foreach (var entry in entries.OrderBy(e => e.Id))
        {
            if (current.Count > 0 && current[0].Group != entry.Group) { groups.Add([.. current]); current.Clear(); }
            current.Add(entry);
        }
        if (current.Count > 0) groups.Add([.. current]);
        groups.Reverse();
        return groups;
    }

    /// <summary>One list for the rail and the dialog: turn and stage marks, one card per resolution, and formalities
    /// that settled nothing as a quiet line.</summary>
    private void FillHistory(VBoxContainer list, string prefix, bool large, string emptyText, DuelHistoryEntry[]? only = null)
    {
        var s = _snapshot!;
        var entries = only ?? (_preparing ? [] : s.History?.OrderByDescending(e => e.Id).Take(DuelHistory.Capacity).ToArray() ?? []);
        if (entries.Length == 0)
        {
            var empty = ArenaPresentation.Text(emptyText, large ? 15 : 13, BoardArt.Muted);
            empty.AutowrapMode = TextServer.AutowrapMode.WordSmart; list.AddChild(empty);
            return;
        }
        int body = large ? 15 : 13, small = large ? 14 : 12;
        var feed = prefix == "HistoryFeed"; var newest = entries.Max(e => e.Id);
        // A history that goes back is another duel: it starts a new baseline instead of glowing.
        if (feed && newest < _historySeen) { _historySeen = 0; _historyGlow = default; }
        (int Turn, Phase Stage)? mark = null;
        foreach (var group in HistoryGroups(entries))
        {
            var head = group.FirstOrDefault(e => !e.Routine) ?? group[0]; var last = group[^1];
            if (mark != (head.Turn, head.Stage))
            {
                mark = (head.Turn, head.Stage);
                var stage = StageName(head.Stage);
                var label = ArenaPresentation.Text($"TURNO {head.Turn:00}" + (stage.Length > 0 ? " · " + stage : ""), large ? 11 : 10, BoardArt.Muted);
                label.Name = $"{prefix}Turn{head.Turn}{head.Stage}"; list.AddChild(label);
            }
            var (chips, outcomes) = DuelHistoryText.Summary(group, HistoryPlayer);
            if (group.All(e => e.Routine) && outcomes.Count == 0 && chips.Count == 0)
            {
                // "Ana avanza de fase." marks the flow without competing with the plays.
                var quiet = ArenaPresentation.Text(string.Join(" ", group.Select(e => e.Text)), small - 1, BoardArt.Muted);
                quiet.Name = $"{prefix}Routine{last.Id}"; quiet.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                list.AddChild(quiet);
                continue;
            }
            // The edge takes the colour of whoever played, as the fields do.
            var accent = head.SourceActor is 0 or 1 ? head.SourceActor == s.MySeat ? ArenaPresentation.Mint : ArenaPresentation.Coral : BoardArt.Muted;
            var panel = new PanelContainer { Name = $"{prefix}Entry{last.Id}", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var style = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.035f), BorderColor = accent, BorderWidthLeft = 3,
                ContentMarginLeft = 9, ContentMarginRight = 6, ContentMarginTop = 5, ContentMarginBottom = 5 };
            panel.AddThemeStyleboxOverride("panel", style); list.AddChild(panel);
            if (feed && last.Id == newest) _unsettledPlay.Add(panel);
            if (feed && _historySeen != 0 && last.Id > _historySeen && !_impacts.ReducedMotion) _historyGlow = (last.Id, Time.GetTicksMsec());
            var glowing = (Time.GetTicksMsec() - _historyGlow.StartMs) / 1000.0;
            if (feed && _historyGlow.Id == last.Id && glowing < HistoryGlowSeconds && !_impacts.ReducedMotion)
            {
                // The play that just arrived glows once in the colour of whoever made it, then settles.
                var settled = style.BgColor;
                var tween = panel.CreateTween();
                tween.TweenMethod(Callable.From<Color>(c => style.BgColor = c), BoardArt.Tint(accent, 0.30f), settled, HistoryGlowSeconds)
                    .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                tween.CustomStep(glowing);
            }
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; row.AddThemeConstantOverride("separation", 8); panel.AddChild(row);
            var source = group.Select(e => e.CardId).FirstOrDefault(id => CardCatalog.All.ContainsKey(id));
            if (source != null)
            {
                var seat = head.SourceActor is 0 or 1 ? head.SourceActor : -1;
                var thumb = new HistoryCardThumb(CardCatalog.Get(source), seat >= 0 ? s.Players[seat].RelikarioId : null, large)
                    { Name = $"{prefix}Card{last.Id}" };
                thumb.Pressed += () => ReadHistoryCard(thumb.Card, thumb.Relikario, thumb.GetGlobalRect().GetCenter());
                // Under "ÚLTIMA JUGADA" the card is already shown large.
                if (prefix == "LastPlay") thumb.Visible = false;
                row.AddChild(thumb);
            }
            var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; column.AddThemeConstantOverride("separation", 1); row.AddChild(column);
            var status = last.Status switch
            {
                DuelResolutionStatus.Canceled => "ANULADA", DuelResolutionStatus.Response or DuelResolutionStatus.Resolving => "EN CURSO", _ => ""
            };
            if (status.Length > 0)
                column.AddChild(ArenaPresentation.Text(status, large ? 11 : 10, last.Status == DuelResolutionStatus.Canceled ? ArenaPresentation.Coral : BoardArt.Gold));
            var action = ArenaPresentation.Text(head.Text, body, head.Routine ? BoardArt.Muted : null);
            action.AutowrapMode = TextServer.AutowrapMode.WordSmart; column.AddChild(action);
            // Responses and decisions inside the resolution hang from the play they answer.
            var follow = group.Where(e => e != head && !e.Routine).Select(e => "↳ " + e.Text).ToArray();
            if (follow.Length > 0)
            {
                var responses = ArenaPresentation.Text(string.Join("\n", follow), small, RelickVisuals.State.Target);
                responses.AutowrapMode = TextServer.AutowrapMode.WordSmart; column.AddChild(responses);
            }
            if (outcomes.Count > 0)
            {
                var detail = ArenaPresentation.Text(string.Join("\n", outcomes), small, BoardArt.Muted);
                detail.AutowrapMode = TextServer.AutowrapMode.WordSmart; column.AddChild(detail);
            }
            if (chips.Count > 0)
            {
                var flow = new HFlowContainer { Name = $"{prefix}Chips{last.Id}", SizeFlagsHorizontal = SizeFlags.ExpandFill };
                flow.AddThemeConstantOverride("h_separation", 4); flow.AddThemeConstantOverride("v_separation", 3);
                foreach (var chip in chips) flow.AddChild(HistoryChip(chip, s.MySeat, large));
                column.AddChild(flow);
            }
        }
        if (feed) _historySeen = Math.Max(_historySeen, newest);
    }

    /// <summary>A figure in the colour of whose it is (the die belongs to nobody); the tooltip says it in full.</summary>
    private static Control HistoryChip(DuelHistoryChip chip, int mySeat, bool large)
    {
        var color = chip.Player < 0 ? BoardArt.Gold : chip.Player == mySeat ? ArenaPresentation.Mint : ArenaPresentation.Coral;
        var panel = new PanelContainer { TooltipText = chip.Description, MouseFilter = MouseFilterEnum.Pass };
        var style = new StyleBoxFlat { BgColor = BoardArt.Tint(color, 0.13f), BorderColor = BoardArt.Tint(color, 0.55f),
            ContentMarginLeft = 5, ContentMarginRight = 6, ContentMarginTop = 1, ContentMarginBottom = 1 };
        style.SetBorderWidthAll(1); style.SetCornerRadiusAll(8);
        panel.AddThemeStyleboxOverride("panel", style);
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; row.AddThemeConstantOverride("separation", 3); panel.AddChild(row);
        GameIcon? icon = chip.Kind switch
        {
            DuelHistoryChipKind.Health => GameIcon.Heart, DuelHistoryChipKind.Actions => GameIcon.Bolt,
            DuelHistoryChipKind.Pr => GameIcon.Gem, DuelHistoryChipKind.Draw => GameIcon.Cards, _ => null
        };
        var size = large ? 13 : 11;
        if (icon is { } symbol)
            row.AddChild(new IconView { Icon = symbol, Color = color, CustomMinimumSize = Vector2.One * size, SizeFlagsVertical = SizeFlags.ShrinkCenter });
        var label = ArenaPresentation.Text(chip.Label, size, color); label.MouseFilter = MouseFilterEnum.Ignore; row.AddChild(label);
        return panel;
    }

    /// <summary>Reading a play's card: the inspector shows it as when hovering it on the board; the dialog yields.</summary>
    private void ReadHistoryCard(CardDefinition card, string? relikario, Vector2 from)
    {
        if (_snapshot == null || IsAnimating) return;
        if (_historyDialog is { } dialog && IsInstanceValid(dialog)) { _historyDialog = null; dialog.QueueFree(); }
        _inspectorPreview = (card, null); _inspectorPreviewOwner = null; _inspectorPreviewRelikario = relikario; _relikarioPreview = null;
        _previewPinned = true; _previewLeftAt = null; _previewAnchor = from;
        RefreshInspector();
    }
}

/// <summary>The card of a play in miniature. Hovering shows it large; pressing reads it in the inspector.</summary>
public partial class HistoryCardThumb : TextureRect
{
    public CardDefinition Card { get; }
    public string? Relikario { get; }
    public event Action? Pressed;

    public HistoryCardThumb() : this(CardCatalog.Attack, null, false) { }
    public HistoryCardThumb(CardDefinition card, string? relikario, bool large)
    {
        Card = card; Relikario = relikario;
        Texture = CardShowcase.Artwork(card, relikario);
        ExpandMode = ExpandModeEnum.IgnoreSize; StretchMode = StretchModeEnum.KeepAspectCentered;
        CustomMinimumSize = large ? new(46, 64) : new(34, 48);
        SizeFlagsVertical = SizeFlags.ShrinkBegin;
        MouseFilter = MouseFilterEnum.Stop; MouseDefaultCursorShape = CursorShape.PointingHand;
        TooltipText = card.Name;
    }

    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } || input is InputEventScreenTouch { Pressed: false })
        { AcceptEvent(); Pressed?.Invoke(); }
    }

    public override GodotObject? _MakeCustomTooltip(string forText)
    {
        if (Texture == null) return null;
        var column = new VBoxContainer();
        column.AddChild(new TextureRect { Texture = Texture, ExpandMode = ExpandModeEnum.IgnoreSize,
            StretchMode = StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new(200, 280) });
        var name = ArenaPresentation.Text(Card.Name, 14); name.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(name);
        return column;
    }
}
