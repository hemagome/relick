using Godot;
using Relick.Core;

namespace Relick;

// A snapshot of authorized cards: selection only changes this viewer's shared inspector.
public partial class CardCollectionView : HBoxContainer
{
    public IReadOnlyList<string?> Cards { get; init; } = [];
    public Func<int, string?>? RelikarioOf { get; init; }
    public bool ShortScreen { get; init; }
    public float InspectorWidth { get; init; } = 340;
    public event Action<int>? CardPressed;
    internal int SelectedIndex { get; private set; } = -1;
    internal CardInspector Inspector { get; private set; } = null!;
    internal ScrollContainer Gallery { get; private set; } = null!;
    private GridContainer _grid = null!;
    private readonly List<Button> _cards = [];

    public override void _Ready()
    {
        Name = "CardCollectionView";
        SizeFlagsHorizontal = SizeFlags.ExpandFill; SizeFlagsVertical = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 12);
        Gallery = new ScrollContainer { Name = "CollectionGallery", SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill, FollowFocus = true,
            // Allow the viewport to shrink before recalculating the grid's column count.
            HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever };
        AddChild(Gallery);
        _grid = new GridContainer { Name = "CollectionCards", Columns = 1, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _grid.AddThemeConstantOverride("h_separation", 10); _grid.AddThemeConstantOverride("v_separation", 10);
        Gallery.AddChild(_grid);
        Inspector = new CardInspector { Name = "CollectionInspector", ShortScreen = ShortScreen,
            CustomMinimumSize = new(InspectorWidth, 0) };
        AddChild(Inspector);
        Inspector.SizeFlagsHorizontal = SizeFlags.Fill;
        var scale = ShortScreen ? 0.6f : 1f;
        for (var index = 0; index < Cards.Count; index++)
        {
            var slot = index;
            Button item = Cards[index] is string id
                ? new HandCard { Card = CardCatalog.Get(id), PresentationScale = scale, RelikarioId = RelikarioOf?.Invoke(index) }
                : new OpponentHandCard { FaceSize = new Vector2(126, 184) * scale };
            item.Name = "CollectionCard" + index;
            item.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            item.Pressed += () =>
            {
                Inspect(slot);
                if (Cards[slot] != null) CardPressed?.Invoke(slot);
            };
            item.FocusEntered += () => Inspect(slot);
            item.MouseEntered += () => Inspect(slot);
            _cards.Add(item); _grid.AddChild(item);
            if (Cards[index] is string visibleId) item.TooltipText = CardCatalog.Get(visibleId).Name;
        }
        Gallery.Resized += () => CallDeferred(nameof(UpdateColumns));
        UpdateColumns();
        // Present text after the dialog has assigned its reading width, avoiding
        // a transient one-character-wide wrap expanding the native window.
        CallDeferred(nameof(InspectInitialCard));
    }

    private void InspectInitialCard()
    {
        var firstVisible = Enumerable.Range(0, Cards.Count).FirstOrDefault(i => Cards[i] != null, -1);
        if (Cards.Count > 0) Inspect(firstVisible >= 0 ? firstVisible : 0);
    }

    private void UpdateColumns()
    {
        var width = Gallery.Size.X - 18; // Reserve the vertical scrollbar, even before it appears.
        var cardWidth = ShortScreen ? 75.6f : 126;
        _grid.Columns = Math.Clamp((int)((width + 10) / (cardWidth + 10)), 1, 6);
    }

    private void Inspect(int index)
    {
        if (index < 0 || index >= Cards.Count) return;
        SelectedIndex = index;
        for (var i = 0; i < _cards.Count; i++)
        {
            if (_cards[i] is HandCard face) face.Chosen = i == index;
            if (_cards[i] is OpponentHandCard back) back.Chosen = i == index;
            _cards[i].QueueRedraw();
        }
        if (Cards[index] is string id)
        {
            var card = CardCatalog.Get(id);
            Inspector.Present(card, card.Name, ArenaActionText.CardCost(card));
        }
        else Inspector.Present(null, "Carta oculta", hint: "La identidad de esta carta permanece oculta.");
        Inspector.SizeFlagsVertical = SizeFlags.ExpandFill;
    }
}
