using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    private PanelContainer? _decisionPopup;
    private Label _decisionTitle = null!, _decisionRejection = null!;
    private Button? _decisionToggle;
    private bool _decisionMinimized;
    private (int[] Selection, Vector2I Scroll)? _pausedGallery;
    private bool ViewingBoardDuringGallery => _pausedGallery.HasValue;

    private bool NeedsDecisionPrompt(DuelSnapshot s) => _choosingAttackSkill || ChoosingEffectCards ||
        s.DecisionPlayer == s.MySeat && (!(s.Phase.IsTurnStage() || s.Phase is Phase.Finished or Phase.ErrorResponse) ||
            s.Options.Any(o => o.Move.Kind == DuelMoveKind.Discard));

    private void BuildDecisionPrompt(VBoxContainer? gallery)
    {
        _decisionToggle = null;
        var content = gallery;
        if (content == null)
        {
            var popup = new PanelContainer { Name = "DuelDecisionPrompt", MouseFilter = MouseFilterEnum.Ignore, ZIndex = 19, Visible = false };
            var style = RelickVisuals.Glass(new Color("1a263d"), new Color("0c1323"), BoardArt.Gold, 9, 9,
                BoardArt.Tint(BoardArt.Gold, 0.28f));
            style.ContentMarginLeft = style.ContentMarginRight = 16;
            style.ContentMarginTop = style.ContentMarginBottom = Short ? 8 : 12;
            popup.AddThemeStyleboxOverride("panel", style);
            AddChild(popup); _decisionPopup = popup;
            popup.MinimumSizeChanged += PositionDecisionPrompt;
            content = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            content.AddThemeConstantOverride("separation", 6); popup.AddChild(content);
        }

        var header = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        header.AddThemeConstantOverride("separation", 10); content.AddChild(header);
        _decisionTitle = DecisionText("DecisionTitle", Short ? 17 : 21, BoardArt.Gold);
        _decisionTitle.AddThemeFontOverride("font", RelickVisuals.Display);
        header.AddChild(_decisionTitle);
        header.Visible = gallery == null;
        if (gallery == null) BuildDecisionToggle(header);

        _selection = DecisionText("ContextHint", Short ? 13 : 15, BoardArt.Ink);
        content.AddChild(_selection);
        if (gallery != null)
        {
            // Changing the selected copy must not move the gallery or its confirmation.
            _selection.AutowrapMode = TextServer.AutowrapMode.Off;
            _selection.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        }
        _decisionRejection = DecisionText("PlayRejectionText", Short ? 13 : 15, RelickVisuals.State.Danger.Lightened(0.35f));
        _decisionRejection.MaxLinesVisible = 3;
        _decisionRejection.Hide(); content.AddChild(_decisionRejection);
        BuildDecisionButtons(content);
        if (gallery != null && ChoosingDeckGallery)
        {
            BuildDecisionToggle(_decisionBar);
            _decisionBar.MoveChild(_decisionToggle!, 0);
        }
    }

    private void BuildDecisionToggle(Node parent)
    {
        _decisionToggle = ArenaPresentation.Button(parent, "Ver tablero", ToggleDecisionView);
        _decisionToggle.Name = "ToggleDecisionPrompt";
        _decisionToggle.AddThemeFontSizeOverride("font_size", 13);
    }

    private void ToggleDecisionView()
    {
        if (_pausedGallery is { } paused)
        {
            _pausedGallery = null;
            ClearSelection();
            _selected.UnionWith(paused.Selection);
            Render(handScroll: paused.Scroll);
        }
        else if (ChoosingDeckGallery)
        {
            if (IsAnimating || Connection.PlayPending) return;
            _pausedGallery = (_selected.ToArray(), new(_hand!.ScrollHorizontal, _hand.ScrollVertical));
            ClearSelection();
            Render(handScroll: Vector2I.Zero);
        }
        else
        {
            _decisionMinimized = !_decisionMinimized;
            RefreshDecisionPrompt();
            return;
        }
        _decisionToggle?.GrabFocus();
    }

    private static Label DecisionText(string name, int size, Color color)
    {
        var label = ArenaPresentation.Text("", size, color);
        label.Name = name; label.MouseFilter = MouseFilterEnum.Ignore;
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return label;
    }

    private void SetDecisionHint(string text)
    {
        if (_selection.Text == text) return;
        _selection.Text = text;
        _selection.TooltipText = text;
        RefreshDecisionPrompt();
    }

    private void RefreshDecisionPrompt()
    {
        var s = _snapshot!;
        var rejection = ShowingRejection ? _rejectionText : "";
        if (_decisionRejection.Text != rejection)
        {
            _decisionRejection.Text = rejection;
            _decisionRejection.TooltipText = rejection;
        }
        var hasButtons = _moves.GetChildCount() > 0;
        if (_decisionPopup == null)
        {
            _selection.Visible = !_choosingOpeningSkill || _selection.Text.Length > 0;
            _decisionRejection.Visible = rejection.Length > 0;
            _decisionBar.Visible = hasButtons || _decisionToggle != null;
            if (_decisionToggle != null) _decisionToggle.Disabled = IsAnimating || Connection.PlayPending;
            return;
        }

        var choosing = _preparing || NeedsDecisionPrompt(s) || hasButtons;
        var active = s.Winner == null && (!IsAnimating || rejection.Length > 0);
        var showHint = _decisionHint.Length > 0 && _selected.Count == 1 && _fieldSelection == null && !_skillContext && !_cardActions.Visible;
        _decisionPopup.Visible = active && (choosing || showHint || rejection.Length > 0);
        if (!_decisionPopup.Visible) return;

        var folded = ViewingBoardDuringGallery || _decisionMinimized && choosing && rejection.Length == 0;
        _decisionToggle!.Visible = choosing;
        _decisionToggle.Text = folded ? "Volver a la decisión" : "Ver tablero";
        var title = rejection.Length > 0 ? "Jugada rechazada" : _preparing ? "Mano inicial" : _choosingAttackSkill ? "Elige una habilidad" :
            ViewingBoardDuringGallery ? s.Decision!.Title :
            choosing ? ContextTitle() : SelectedCardId() is { } card ? CardCatalog.Get(card).Name : "";
        if (_decisionTitle.Text != title) _decisionTitle.Text = title;
        _selection.Visible = !folded && _selection.Text.Length > 0;
        _decisionRejection.Visible = rejection.Length > 0;
        _decisionBar.Visible = !folded && hasButtons;
        _decisionPopup.CustomMinimumSize = new(folded ? 320 : Math.Min(Size.X - 32, Short ? 424 : 528), 0);
        PositionDecisionPrompt();
    }

    private void PositionDecisionPrompt()
    {
        if (_decisionPopup is not { Visible: true } popup) return;
        var size = popup.GetCombinedMinimumSize();
        if (popup.Size != size) popup.Size = size;
        var folded = ViewingBoardDuringGallery || _decisionMinimized && _decisionToggle is { Visible: true } && !ShowingRejection;
        var y = folded ? (Short ? 92 : Compact ? 108 : 20) : (Size.Y - size.Y) / 2;
        popup.Position = new Vector2((Size.X - size.X) / 2, Math.Max(8, y)).Round();
    }
}
