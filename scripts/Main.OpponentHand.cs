using Godot;
using Relick.Core;

namespace Relick;

public partial class Main
{
    private void BuildOpponentHand(VBoxContainer parent)
    {
        var column = new VBoxContainer { Name = "OpponentHand" };
        column.AddThemeConstantOverride("separation", 4);
        var header = new HBoxContainer();
        var caption = Label("MANO DEL RIVAL", 10, Coral);
        caption.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(caption);
        var expand = new Button
        {
            Name = "InspectOpponentHand", Text = "Ampliar ↗", CustomMinimumSize = new(0, 18),
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        expand.AddThemeFontSizeOverride("font_size", 11);
        foreach (var state in new[] { "normal", "hover", "pressed", "focus" }) expand.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        expand.Pressed += ShowOpponentHand;
        header.AddChild(expand); column.AddChild(header);
        var scroll = new ScrollContainer
        {
            Name = "OpponentHandScroll", CustomMinimumSize = new(0, 90),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        var cards = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        cards.AddThemeConstantOverride("separation", 4);
        for (var i = 0; i < _game.State.Players[1].Hand.Count; i++)
        {
            var card = BuildOpponentCard(i);
            card.Pressed += () => InspectOpponentCard(card.PublicCard);
            cards.AddChild(card);
        }
        if (_game.State.Players[1].Hand.Count == 0) cards.AddChild(Label("Sin cartas en mano", 13, Muted));
        scroll.AddChild(cards); column.AddChild(scroll); parent.AddChild(column);
        _opponentHandScroll = scroll;
        scroll.SetDeferred(ScrollContainer.PropertyName.ScrollHorizontal, _opponentHandScrollPosition);
    }

    private OpponentHandCard BuildOpponentCard(int slot, bool enlarged = false)
    {
        var visibleId = _game.State.Players[1].RevealedHandCard(slot);
        return new OpponentHandCard
        {
            Name = $"OpponentHandCard{slot}", PublicCard = visibleId == null ? null : CardCatalog.Get(visibleId),
            FaceSize = enlarged ? new(108, 153) : new(54, 76.4f)
        };
    }

    private void InspectOpponentCard(CardDefinition? card)
    {
        if (_helpOpen) return;
        if (card == null) ShowOpponentHand();
        else ShowCardDetails(1, card, "CARTA REVELADA · MANO DEL RIVAL", "Esta carta es visible por un efecto. Sigue en la mano de tu adversario.", "Carta revelada del rival");
    }

    private void ShowOpponentHand()
    {
        if (_helpOpen) return;
        var column = new VBoxContainer { CustomMinimumSize = new(590, 0) };
        column.AddThemeConstantOverride("separation", 12);
        column.AddChild(Wrapped("Cada reverso representa una carta. Las cartas reveladas por efectos muestran su frente y se pueden consultar.", 14, Muted, 580));
        var scroll = new ScrollContainer
        {
            Name = "OpponentHandGallery", CustomMinimumSize = new(590, 335),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        var grid = new GridContainer { Columns = 5 };
        grid.AddThemeConstantOverride("h_separation", 10); grid.AddThemeConstantOverride("v_separation", 12);
        AcceptDialog? dialog = null;
        for (var i = 0; i < _game.State.Players[1].Hand.Count; i++)
        {
            var card = BuildOpponentCard(i, true);
            card.Disabled = card.IsFaceDown;
            card.Pressed += () =>
            {
                if (card.PublicCard == null || dialog == null) return;
                dialog.Hide(); dialog.QueueFree(); _helpOpen = false;
                InspectOpponentCard(card.PublicCard);
            };
            grid.AddChild(card);
        }
        if (_game.State.Players[1].Hand.Count == 0) grid.AddChild(Label("El rival no tiene cartas en mano.", 16, Muted));
        scroll.AddChild(grid); column.AddChild(scroll);
        dialog = OpenBoardDialog("Mano del rival", column, new(630, 460));
    }
}
