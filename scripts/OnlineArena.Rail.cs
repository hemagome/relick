using Godot;
using Relick.Core;

namespace Relick;

// The desktop rail adapts to what it costs. Where height limits the board, the room beside it is free and holds a
// docked reader; where width does, only the strip of plays stays and the reader floats over a side column.
public partial class OnlineArena
{
    internal const float StripWidth = 72;
    /// <summary>Room beside a full-scale board from which the reader docks.</summary>
    private const float DockFrom = 320;
    private const float DockedRailMaximum = 420;
    private const float StripPadding = 6, DockedMargin = 8, NarrowMargin = 6;
    // Design units: the column of each seat's Bonus, Área and canje, from under the far edge to above the hand.
    private const float SideColumn = 302, ReaderTop = 16, ReaderBottom = 752, ReaderArt = 264, ReaderArtMinimum = 170;
    // A gallery of one card still shrinks, down to what the reader needs; a long text scrolls there.
    private const float GalleryReaderWidth = 280, GalleryReaderMinimum = 520, GalleryReaderArt = 176;

    private bool _railDocked, _readerInGallery, _historyOpen, _readerReading, _readerPositionQueued;
    private PanelContainer? _floatingReader, _historyDrawer;
    private BoardCanvas? _railCanvas;
    private Control? _lastPlay, _readerPane, _galleryReaderEmpty;
    private readonly List<PlayTile> _playTiles = [];
    private readonly List<Control> _unsettledPlay = [];

    /// <summary>The preview follows the pointer; one opened from the history stays until something replaces it.</summary>
    private bool _previewPinned;
    private ulong _previewSince;
    private ulong? _previewLeftAt;
    private Vector2? _previewAnchor;
    private Vector2 _pointer;

    internal const ulong ReaderDelayMilliseconds = 180;
    /// <summary>Floating readers leave quickly; a docked one waits for the pointer to travel to it.</summary>
    internal const ulong FloatingGraceMilliseconds = 250, DockedGraceMilliseconds = 700;

    internal bool RailDocked => _railDocked;

    /// <summary>The width a full-scale board leaves free. A rail inside it costs the cards nothing.</summary>
    private float FreeBesideBoard => Size.X - Size.Y * BoardCanvas.DesignWidth / BoardCanvas.DesignHeight;

    private void ClearRail()
    {
        foreach (var overlay in new Control?[] { _floatingReader, _historyDrawer })
            if (overlay != null && IsInstanceValid(overlay)) { overlay.GetParent()?.RemoveChild(overlay); overlay.QueueFree(); }
        _floatingReader = _historyDrawer = null;
        _historyButton = null; _railCanvas = null;
        _lastPlay = _readerPane = _galleryReaderEmpty = null;
        _railDocked = _readerInGallery = false;
        _playTiles.Clear(); _unsettledPlay.Clear();
        ForgetPreview();
    }

    /// <summary>Decided before the board is built: a narrow rail makes the galleries keep room for the reader.</summary>
    private void ChooseRailMode(bool gallery)
    {
        _railDocked = FreeBesideBoard >= DockFrom;
        _readerInGallery = gallery && !_railDocked;
    }

    private void BuildRail(HBoxContainer columns, string message, bool gallery, VBoxContainer? gallerySide)
    {
        var margin = _railDocked ? DockedMargin : NarrowMargin;
        var railMargin = new MarginContainer { Name = "ReaderRailMargin" };
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" }) railMargin.AddThemeConstantOverride(side, (int)margin);
        columns.AddChild(railMargin);
        _railCanvas = columns.GetNode<BoardCanvas>("DuelArena");
        var width = _railDocked ? MathF.Floor(Math.Min(FreeBesideBoard - 2 * margin, DockedRailMaximum)) : StripWidth + 2 * StripPadding;
        var rail = new PanelContainer { Name = "ReaderRail", CustomMinimumSize = new(width, 0) };
        var surface = RelickVisuals.Frame(RelickVisuals.Playmat.Surface, RelickVisuals.Playmat.Frame, 8, 1);
        surface.ContentMarginLeft = surface.ContentMarginRight = surface.ContentMarginTop = surface.ContentMarginBottom = StripPadding;
        rail.AddThemeStyleboxOverride("panel", surface);
        railMargin.AddChild(rail); _readerRail = rail;
        var split = new HBoxContainer { Name = "RailColumns", MouseFilter = MouseFilterEnum.Ignore };
        split.AddThemeConstantOverride("separation", 6); rail.AddChild(split);

        VBoxContainer reader;
        if (_railDocked)
        {
            reader = new VBoxContainer { Name = "InspectorColumn", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            split.AddChild(reader);
        }
        else if (gallerySide != null) reader = gallerySide;
        else
        {
            _floatingReader = new PanelContainer { Name = "FloatingReader", Visible = false };
            var style = RelickVisuals.Glass(RelickVisuals.Playmat.Raised, RelickVisuals.Playmat.Surface,
                RelickVisuals.Playmat.Frame.Lightened(0.2f), 9, 8, new Color(0, 0, 0, 0.45f));
            style.ContentMarginLeft = style.ContentMarginRight = style.ContentMarginTop = style.ContentMarginBottom = 5;
            _floatingReader.AddThemeStyleboxOverride("panel", style);
            AddOverBoard(_floatingReader);
            reader = new VBoxContainer { Name = "InspectorColumn" };
            _floatingReader.AddChild(reader);
            _floatingReader.MinimumSizeChanged += QueueReaderPosition;
        }
        reader.AddThemeConstantOverride("separation", 6);
        _readerPane = reader;
        var strip = new VBoxContainer { Name = "RailStrip", CustomMinimumSize = new(StripWidth, 0) };
        strip.AddThemeConstantOverride("separation", 3); split.AddChild(strip);

        var header = new VBoxContainer { Name = "RailHeader" };
        header.AddThemeConstantOverride("separation", 0); strip.AddChild(header);
        BuildArenaMenu(header);
        // From the corner inwards: menu, sound and, last, conceding.
        var symbols = header.GetChildren().OfType<Control>().ToArray();
        for (var index = 0; index < symbols.Length; index++)
        {
            header.MoveChild(symbols[index], symbols.Length - 1 - index);
            symbols[index].SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        }
        BuildComboResponseToggle(strip);

        // A gallery dims the board and its turn plate; the clock goes where the card is read.
        if (gallery) AddClockLabel(reader, 16).HorizontalAlignment = HorizontalAlignment.Center;
        BuildRailResolution(reader, message);
        if (_readerInGallery)
        {
            var empty = ArenaPresentation.Text("Señala una carta para leerla.", 14, BoardArt.Muted);
            empty.AutowrapMode = TextServer.AutowrapMode.WordSmart; empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.VerticalAlignment = VerticalAlignment.Center; empty.SizeFlagsVertical = SizeFlags.ExpandFill;
            reader.AddChild(empty); _galleryReaderEmpty = empty;
        }
        _showcase = new CardShowcase { Name = "CardShowcase", Visible = false,
            CustomMinimumSize = new(0, _railDocked ? 300 : _readerInGallery ? GalleryReaderArt : ReaderArt) };
        reader.AddChild(_showcase);
        var reading = new VBoxContainer { Name = "RailReading" };
        reader.AddChild(reading); _railReading = reading;
        BuildInspector(reading);
        if (_floatingReader != null) _inspector.DescriptionLabel.MinimumSizeChanged += QueueReaderPosition;
        if (_railDocked)
        {
            BuildLastPlay(reader);
            BuildHistoryFeed(reader);
        }
        else
        {
            _historyDrawer = new PanelContainer { Name = "HistoryDrawer", Visible = false };
            var style = RelickVisuals.Frame(RelickVisuals.Playmat.Surface, BoardArt.Tint(BoardArt.Gold, 0.7f), 8, 1);
            _historyDrawer.AddThemeStyleboxOverride("panel", style);
            AddOverBoard(_historyDrawer);
            var content = new VBoxContainer(); _historyDrawer.AddChild(content);
            BuildHistoryFeed(content);
        }
        BuildStripPlays(strip);
    }

    // Right above the board and below every overlay that is added or raised after it.
    private void AddOverBoard(Control overlay)
    {
        AddChild(overlay);
        MoveChild(overlay, _root!.GetIndex() + 1);
    }

    private static bool IsPlay(DuelHistoryEntry[] group) => group.Any(entry => !entry.Routine || CardCatalog.All.ContainsKey(entry.CardId) ||
        entry.Events.Any(e => e.Kind is DuelHistoryEventKind.Damage or DuelHistoryEventKind.Heal));

    private List<DuelHistoryEntry[]> RecentGroups() =>
        _preparing || _snapshot?.History is not { Count: > 0 } entries ? []
            : HistoryGroups(entries.OrderByDescending(entry => entry.Id).Take(DuelHistory.Capacity));

    private void BuildStripPlays(VBoxContainer strip)
    {
        _historyButton = new Button { Name = "DuelHistoryButton", Text = "Historial", ToggleMode = true,
            CustomMinimumSize = new(0, 48), Visible = !_preparing && !_readerInGallery, MouseDefaultCursorShape = CursorShape.PointingHand };
        _historyButton.AddThemeFontSizeOverride("font_size", 11);
        StyleBoxFlat Plate(Color fill, Color edge)
        {
            var style = RelickVisuals.Frame(fill, edge, 5, 1);
            style.ContentMarginLeft = style.ContentMarginRight = 3;
            return style;
        }
        var resting = Plate(RelickVisuals.Playmat.Raised, RelickVisuals.Playmat.Frame);
        var open = Plate(RelickVisuals.Playmat.Raised, BoardArt.Gold);
        foreach (var state in new[] { "normal", "hover", "disabled" }) _historyButton.AddThemeStyleboxOverride(state, resting);
        foreach (var state in new[] { "pressed", "hover_pressed" }) _historyButton.AddThemeStyleboxOverride(state, open);
        _historyButton.AddThemeStyleboxOverride("focus", Plate(Colors.Transparent, RelickVisuals.State.Focus));
        _historyButton.SetPressedNoSignal(_historyOpen);
        _historyButton.Pressed += () => SetHistoryOpen(!_historyOpen);
        strip.AddChild(_historyButton);

        var scroll = new ScrollContainer { Name = "PlayStripScroll", SizeFlagsVertical = SizeFlags.ExpandFill, FollowFocus = true,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever };
        strip.AddChild(scroll);
        var plays = new VBoxContainer { Name = "PlayStrip", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        plays.AddThemeConstantOverride("separation", 3); scroll.AddChild(plays);
        var groups = RecentGroups(); var s = _snapshot!;
        var turn = -1;
        for (var index = 0; index < groups.Count; index++)
        {
            var group = groups[index];
            // "Ana avanza de fase." stays in the full history; the strip keeps the plays.
            if (!IsPlay(group)) continue;
            var head = group.FirstOrDefault(entry => !entry.Routine) ?? group[0];
            if (head.Turn != turn)
            {
                turn = head.Turn;
                var mark = ArenaPresentation.Text($"T{turn}", 11, BoardArt.Muted);
                mark.HorizontalAlignment = HorizontalAlignment.Center; plays.AddChild(mark);
            }
            var source = group.Select(entry => entry.CardId).FirstOrDefault(CardCatalog.All.ContainsKey);
            var known = head.SourceActor is 0 or 1;
            var mine = known && head.SourceActor == s.MySeat;
            var (chips, lines) = DuelHistoryText.Summary(group, HistoryPlayer);
            // Lives first, then PR, cards and actions; a die only when nothing else came of it.
            var figure = chips.OrderBy(chip => chip.Kind switch
            {
                DuelHistoryChipKind.Health => 0, DuelHistoryChipKind.Pr => 1, DuelHistoryChipKind.Draw => 2,
                DuelHistoryChipKind.Actions => 3, _ => 4
            }).FirstOrDefault();
            if (figure?.Kind == DuelHistoryChipKind.Die) figure = chips.Last(chip => chip.Kind == DuelHistoryChipKind.Die);
            var tile = new PlayTile
            {
                Name = $"PlayTile{group[^1].Id}", Mine = mine, Newest = _playTiles.Count == 0,
                Accent = !known ? BoardArt.Muted : mine ? ArenaPresentation.Mint : ArenaPresentation.Coral,
                Card = source == null ? null : CardCatalog.Get(source),
                Relikario = known ? s.Players[head.SourceActor].RelikarioId : null,
                Canceled = group[^1].Status == DuelResolutionStatus.Canceled,
                Figure = figure,
                FigureColor = figure == null || figure.Player < 0 ? BoardArt.Gold : figure.Player == s.MySeat ? ArenaPresentation.Mint : ArenaPresentation.Coral,
                Action = head.Text,
                Outcome = string.Join("\n", group.Where(entry => entry != head && !entry.Routine).Select(entry => "↳ " + entry.Text)
                    .Concat(lines).Concat(chips.Select(chip => chip.Description)))
            };
            // Only the newest resolution can still be on its way to the board.
            tile.Latest = index == 0;
            if (tile.Latest) tile.Settled = !IsAnimating;
            tile.Pressed += () =>
            {
                if (tile.Card is { } card) ReadHistoryCard(card, tile.Relikario, tile.GetGlobalRect().GetCenter());
                else SetHistoryOpen(true);
            };
            plays.AddChild(tile); _playTiles.Add(tile);
        }
    }

    /// <summary>With nothing to read, the docked reader shows the last play instead of an empty panel.</summary>
    private void BuildLastPlay(VBoxContainer parent)
    {
        var column = new VBoxContainer { Name = "LastPlay", Visible = false };
        column.AddThemeConstantOverride("separation", 4); parent.AddChild(column); _lastPlay = column;
        var heading = ArenaPresentation.Heading("ÚLTIMA JUGADA", 12, BoardArt.Gold); column.AddChild(heading);
        var latest = RecentGroups().FirstOrDefault(IsPlay);
        if (latest != null && latest.Select(entry => entry.CardId).FirstOrDefault(CardCatalog.All.ContainsKey) is { } id)
        {
            var actor = (latest.FirstOrDefault(entry => !entry.Routine) ?? latest[0]).SourceActor;
            var art = new CardShowcase { Name = "LastPlayCard", CustomMinimumSize = new(0, 300) };
            column.AddChild(art);
            art.ReducedMotion = true;
            art.Present(CardCatalog.Get(id), actor is 0 or 1 ? _snapshot!.Players[actor].RelikarioId : null);
        }
        var entryList = new VBoxContainer { Name = "LastPlayEntry" }; column.AddChild(entryList);
        FillHistory(entryList, "LastPlay", false, "Aún no hay jugadas.", latest ?? []);
    }

    private void SetHistoryOpen(bool open)
    {
        _historyOpen = open && !IsAnimating;
        _historyButton?.SetPressedNoSignal(_historyOpen);
        // Opening the history is asking for it: a card that was only being hovered gives way.
        if (_historyOpen && !HasContextSelection && (_inspectorPreview != null || _relikarioPreview != null)) DropPreview();
        SyncRail();
    }

    private void ForgetPreview() { _previewPinned = false; _previewLeftAt = null; _previewAnchor = null; _previewSince = 0; }

    private void DropPreview()
    {
        _inspectorPreview = null; _relikarioPreview = null;
        ForgetPreview();
        RefreshInspector();
    }

    private void TrackPreview(Control source)
    {
        if (_previewSince == 0 || _floatingReader is { Visible: false }) _previewSince = Math.Max(1, Time.GetTicksMsec());
        _previewPinned = false; _previewLeftAt = null;
        _previewAnchor = ProjectedTable.Of(source) is { } table ? table.ScreenPoint(source, source.Size / 2) : source.GetGlobalRect().GetCenter();
    }

    private void PreviewLeft()
    {
        if (_showcase != null && !_previewPinned && (_inspectorPreview != null || _relikarioPreview != null)) _previewLeftAt = Time.GetTicksMsec();
    }

    /// <summary>A hover is only looking: its reader never takes the pointer from the board below it.</summary>
    private bool HoverReading => !_previewPinned && !HasContextSelection && _relikiaPreview == null &&
        (_inspectorPreview != null || _relikarioPreview != null);

    /// <summary>The card the player must answer, opened by itself.</summary>
    private string? RespondingToCard()
    {
        if (_showcase == null || _preparing || _snapshot is not { Resolution: { Status: DuelResolutionStatus.Response } live } s ||
            s.DecisionPlayer != s.MySeat) return null;
        var id = live.Responses.LastOrDefault()?.CardId ?? live.CardId;
        return CardCatalog.All.ContainsKey(id) ? id : null;
    }

    private string RespondingArtwork()
    {
        var s = _snapshot!; var live = s.Resolution!;
        var actor = live.Responses.LastOrDefault()?.Actor ?? live.Actor;
        return s.Players[actor is 0 or 1 ? actor : s.MySeat].RelikarioId;
    }

    /// <summary>Called every frame and whenever what is read changes.</summary>
    private void SyncRail()
    {
        if (_showcase == null || _readerPane == null || _snapshot == null) return;
        var settled = !IsAnimating;
        var gallery = _readerInGallery;
        var history = _historyOpen && !_preparing;
        foreach (var tile in _playTiles)
            if (tile.Latest) tile.Settled = settled;
        foreach (var panel in _unsettledPlay)
            if (IsInstanceValid(panel)) panel.Visible = settled;
        _showcase.Visible = _readerReading;
        _railReading!.Visible = _readerReading;
        _railReading.SizeFlagsVertical = _readerReading ? SizeFlags.ExpandFill : SizeFlags.Fill;
        if (_galleryReaderEmpty != null) _galleryReaderEmpty.Visible = !_readerReading;
        if (_railDocked)
        {
            if (_historyFeed != null) _historyFeed.Visible = history && !_readerReading;
            if (_lastPlay != null) _lastPlay.Visible = !_preparing && !history && !_readerReading && settled;
            return;
        }
        if (_historyFeed != null) _historyFeed.Visible = history;
        if (_historyDrawer is { } drawer && _readerRail != null)
        {
            var width = 312f;
            var rail = _readerRail.GetGlobalRect();
            var rect = new Rect2(rail.Position.X - GlobalPosition.X - width - 4, NarrowMargin, width, Size.Y - 2 * NarrowMargin);
            drawer.Position = rect.Position; drawer.Size = rect.Size;
            // It never hides something the player has to press.
            drawer.Visible = history && !gallery && !ActionableUnder(new Rect2(rect.Position + GlobalPosition, rect.Size), includePlayable: false);
        }
        PositionReader();
    }

    private void QueueReaderPosition()
    {
        if (_readerPositionQueued) return;
        _readerPositionQueued = true;
        Callable.From(() => { _readerPositionQueued = false; if (IsInsideTree()) PositionReader(); }).CallDeferred();
    }

    private bool ActionableUnder(Rect2 area, bool includePlayable)
    {
        foreach (var target in _pulsingTargets)
            if (IsInstanceValid(target) && target.IsVisibleInTree() && ZoneRect(target).Grow(-2).Intersects(area)) return true;
        if (!includePlayable) return false;
        foreach (var track in _exchangeTracks)
        {
            if (track == null || !IsInstanceValid(track)) continue;
            foreach (var zone in track.ExchangeButtons.Append(track.BonusButton).Append(track.AreaButton))
                if (zone is { Playable: true } && zone.IsVisibleInTree() && ZoneRect(zone).Grow(-2).Intersects(area)) return true;
        }
        return false;
    }

    private Vector2? ReaderAnchor()
    {
        if (_previewAnchor is { } anchor && (_inspectorPreview != null || _relikarioPreview != null)) return anchor;
        Control? selected = null;
        if (_fieldSelection is { } field) selected = FieldActionAnchor(field);
        else if (_skillContext && _selectedSkill >= 0) selected = _ownSkills.FirstOrDefault(tile => tile.SkillIndex == _selectedSkill && tile.Skill != null);
        else if (_selected.Count == 1) selected = _handCards.ElementAtOrDefault(_selected.Single());
        return selected is { } node && node.IsInsideTree() ? ZoneRect(node).GetCenter() : null;
    }

    /// <summary>
    /// Over the right column unless the card being read, the history or something to press is there; then over
    /// the left one. With something to press in both, it stays out of the way.
    /// </summary>
    private void PositionReader()
    {
        if (_floatingReader is not { } reader || !IsInstanceValid(reader) || _showcase == null) return;
        if (!_readerReading || _railCanvas is not { } canvas || canvas.Unit <= 0)
        { reader.Visible = false; return; }
        var hover = HoverReading;
        if (hover && !reader.Visible && Time.GetTicksMsec() < _previewSince + ReaderDelayMilliseconds) return;

        var unit = canvas.Unit; var board = canvas.GetGlobalRect();
        var left = board.Position.X + Math.Max(0, board.Size.X - BoardCanvas.DesignWidth * unit) / 2;
        var middle = board.Position.Y + Math.Max(0, board.Size.Y - BoardCanvas.DesignHeight * unit) / 2;
        var width = SideColumn * unit;
        var room = (ReaderBottom - ReaderTop) * unit;

        // The panel takes the height of the whole text; the art gives way before the text has to scroll.
        var reading = _inspector.ReadingViewport;
        var hidden = reading.Visible ? Math.Max(0, _inspector.DescriptionLabel.GetCombinedMinimumSize().Y - reading.CustomMinimumSize.Y) : 0;
        var besideArt = reader.GetCombinedMinimumSize().Y + hidden - _showcase.CustomMinimumSize.Y;
        var art = MathF.Round(Math.Clamp(room - besideArt, ReaderArtMinimum, ReaderArt));
        if (!Mathf.IsEqualApprox(_showcase.CustomMinimumSize.Y, art)) _showcase.CustomMinimumSize = new(0, art);
        var height = Math.Min(room, besideArt + art);
        var y = Math.Max(middle + ReaderTop * unit, Math.Min(middle + BoardCanvas.Seam * unit - height / 2, middle + ReaderBottom * unit - height));

        var rightX = left + (BoardCanvas.DesignWidth - SideColumn) * unit;
        var preferLeft = _historyDrawer is { Visible: true } || ReaderAnchor() is { } at && at.X >= rightX - 8 * unit;
        Rect2? chosen = null;
        foreach (var x in preferLeft ? new[] { left, rightX } : new[] { rightX, left })
        {
            var area = new Rect2(x, y, width, height);
            if (_historyDrawer is { Visible: true } drawer && drawer.GetGlobalRect().Intersects(area)) continue;
            if (hover && (area.HasPoint(_pointer) || ReaderAnchor() is { } source && area.HasPoint(source))) continue;
            if (ActionableUnder(area, includePlayable: !HasContextSelection)) continue;
            chosen = area; break;
        }
        reader.Visible = chosen != null;
        if (chosen is not { } rect) return;
        reader.Position = rect.Position - GlobalPosition; reader.Size = rect.Size;
        var filter = hover ? MouseBehaviorRecursiveEnum.Disabled : MouseBehaviorRecursiveEnum.Inherited;
        if (reader.MouseBehaviorRecursive != filter) reader.MouseBehaviorRecursive = filter;
    }

    /// <summary>A hover ends when the pointer leaves, so the last play and the history are never left hidden.</summary>
    private void UpdateRail()
    {
        if (_showcase == null) return;
        if (HoverReading)
        {
            var pointer = _pointer;
            if (_floatingReader is { Visible: true } reader && reader.GetGlobalRect().HasPoint(pointer)) { DropPreview(); return; }
            if (_previewLeftAt is { } left)
            {
                if (_floatingReader == null && _readerPane is { } pane && pane.IsVisibleInTree() && pane.GetGlobalRect().HasPoint(pointer))
                    _previewLeftAt = Time.GetTicksMsec();
                else if (Time.GetTicksMsec() - left >= (_floatingReader == null ? DockedGraceMilliseconds : FloatingGraceMilliseconds))
                { DropPreview(); return; }
            }
        }
        SyncRail();
    }
}
