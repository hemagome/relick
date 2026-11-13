using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    private Button? _historyButton;
    private Label? _resolutionStage, _resolutionSource, _resolutionResponses, _resolutionMessage;
    private AcceptDialog? _historyDialog;

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
        _historyButton = new Button { Name = "DuelHistoryButton", Text = "Historial", CustomMinimumSize = new(0, 48), Visible = !_preparing, MouseDefaultCursorShape = CursorShape.PointingHand };
        _historyButton.AddThemeFontSizeOverride("font_size", 12);
        _historyButton.Pressed += ShowDuelHistory; row.AddChild(_historyButton);
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
        _historyButton!.Visible = !_preparing && !_inlineHistory;
        _historyButton.Disabled = IsAnimating;
        _historyButton.TooltipText = IsAnimating ? "El historial estará disponible al terminar la resolución visual." : $"Últimas {DuelHistory.Capacity} jugadas públicas.";
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
                _resolutionMessage.Text = (live == null ? latest.Text + "\n" : "") + string.Join("\n", latest.Details.Take(2));
                _resolutionMessage.TooltipText = latest.Text + "\n" + string.Join("\n", latest.Details);
                _resolutionMessage.MaxLinesVisible = 3;
                _resolutionMessage.CustomMinimumSize = Vector2.Zero;
            }
        }
        // Preserve legacy-server and preparation messages, including the initiative presentation.
        _resolutionMessage.Visible = !IsAnimating && _resolutionMessage.Text.Length > 0;
        if (_resolutionPanel != null)
            _resolutionPanel.Visible = _resolutionStage!.Visible || _resolutionSource.Visible || _resolutionResponses.Visible || _resolutionMessage.Visible;
    }

    /// <summary>On the desktop rail, what is resolving now stays above the card or the history.</summary>
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

    /// <summary>The public plays, newest first. The rail shows them whenever no card is being read.</summary>
    private void BuildHistoryFeed(VBoxContainer parent)
    {
        var s = _snapshot!;
        var feed = new VBoxContainer { Name = "HistoryFeed", SizeFlagsVertical = SizeFlags.ExpandFill, Visible = !_preparing };
        feed.AddThemeConstantOverride("separation", 4); parent.AddChild(feed); _historyFeed = feed;
        var heading = ArenaPresentation.Text("HISTORIAL", 12, BoardArt.Gold);
        heading.AddThemeFontOverride("font", RelickVisuals.Display); feed.AddChild(heading);
        var scroll = new ScrollContainer { Name = "HistoryFeedScroll", SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        feed.AddChild(scroll);
        var list = new VBoxContainer { Name = "HistoryFeedEntries", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 6); scroll.AddChild(list);
        var entries = _preparing ? [] : s.History?.OrderByDescending(e => e.Id).Take(DuelHistory.Capacity).ToArray() ?? [];
        if (entries.Length == 0)
        {
            var empty = ArenaPresentation.Text("Las jugadas públicas aparecerán aquí.", 13, BoardArt.Muted);
            empty.AutowrapMode = TextServer.AutowrapMode.WordSmart; list.AddChild(empty);
        }
        var turn = -1;
        foreach (var entry in entries)
        {
            if (entry.Turn != turn)
            {
                turn = entry.Turn;
                var mark = ArenaPresentation.Text($"TURNO {entry.Turn:00}", 10, BoardArt.Muted);
                mark.Name = "HistoryFeedTurn" + entry.Turn; list.AddChild(mark);
            }
            // The edge takes the colour of whoever played, as the fields do.
            var mine = entry.SourceActor == s.MySeat;
            var accent = entry.SourceActor is 0 or 1 ? mine ? ArenaPresentation.Mint : ArenaPresentation.Coral : BoardArt.Muted;
            var panel = new PanelContainer { Name = "HistoryFeedEntry" + entry.Id, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var style = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.035f), BorderColor = accent, BorderWidthLeft = 3,
                ContentMarginLeft = 9, ContentMarginRight = 6, ContentMarginTop = 5, ContentMarginBottom = 5 };
            panel.AddThemeStyleboxOverride("panel", style); list.AddChild(panel);
            var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; column.AddThemeConstantOverride("separation", 1); panel.AddChild(column);
            var action = ArenaPresentation.Text(entry.Text, 13); action.AutowrapMode = TextServer.AutowrapMode.WordSmart; column.AddChild(action);
            if (entry.Details.Length > 0)
            {
                var detail = ArenaPresentation.Text(string.Join("\n", entry.Details), 12, BoardArt.Muted);
                detail.AutowrapMode = TextServer.AutowrapMode.WordSmart; column.AddChild(detail);
            }
        }
    }

    private void ShowDuelHistory()
    {
        if (_snapshot == null || _preparing || IsAnimating || GetChildren().OfType<Window>().Any(w => w.Visible)) return;
        var entries = _snapshot.History?.OrderByDescending(e => e.Id).Take(DuelHistory.Capacity).ToArray() ?? [];
        var dialog = new AcceptDialog { Name = "DuelHistoryDialog", Title = "Historial de jugadas", OkButtonText = "Volver al duelo" };
        var scroll = new ScrollContainer { Name = "HistoryReading", CustomMinimumSize = new(Math.Min(660, Size.X - 64), Math.Min(420, Size.Y - 130)),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var list = new VBoxContainer { Name = "HistoryEntries", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 10); scroll.AddChild(list); dialog.AddChild(scroll); AddChild(dialog);
        if (entries.Length == 0)
        {
            var empty = ArenaPresentation.Text("Las nuevas jugadas públicas aparecerán aquí.", 15, BoardArt.Muted);
            empty.AutowrapMode = TextServer.AutowrapMode.WordSmart; list.AddChild(empty);
        }
        foreach (var entry in entries)
        {
            var panel = new PanelContainer { Name = "HistoryEntry" + entry.Id, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            panel.AddThemeStyleboxOverride("panel", RelickVisuals.Frame(RelickVisuals.Playmat.Surface, RelickVisuals.Playmat.Frame)); list.AddChild(panel);
            var row = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; panel.AddChild(row);
            row.AddChild(ArenaPresentation.Text($"TURNO {entry.Turn:00} · {ResolutionStatus(entry.Status)}", 11, BoardArt.Gold));
            var action = ArenaPresentation.Text(entry.Text, 15); action.AutowrapMode = TextServer.AutowrapMode.WordSmart; row.AddChild(action);
            if (entry.Details.Length > 0)
            {
                var detail = ArenaPresentation.Text(string.Join("\n", entry.Details), 14, BoardArt.Muted);
                detail.AutowrapMode = TextServer.AutowrapMode.WordSmart; row.AddChild(detail);
            }
        }
        _historyDialog = dialog;
        void Close() { _historyDialog = null; dialog.QueueFree(); }
        dialog.Confirmed += Close; dialog.Canceled += Close;
        ArenaViewport.FitDialog(dialog, Size);
    }
}
