using Godot;
using Relick.Core;

namespace Relick;

// A snapshot of authorized cards: selection only changes this viewer's shared inspector.
public partial class CardCollectionView : HBoxContainer
{
    public IReadOnlyList<string?> Cards { get; init; } = [];
    public Func<int, string?>? RelikarioOf { get; init; }
    public bool ShortScreen { get; init; }
    public Func<int, bool>? Inverted { get; init; }
    public int? FaceDownStart { get; init; }
    public string? Sleeve { get; init; }
    public float InspectorWidth { get; init; } = 340;
    public event Action<int>? CardPressed;
    public event Action<int>? SelectionChanged;
    internal int SelectedIndex { get; private set; } = -1;
    internal CardInspector Inspector { get; private set; } = null!;
    /// <summary>The large card art above the inspector, as on the duel's reading rail; absent on short screens.</summary>
    internal CardShowcase? Showcase { get; private set; }
    internal ScrollContainer Gallery { get; private set; } = null!;
    private readonly List<GridContainer> _grids = [];
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
        var sections = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        sections.AddThemeConstantOverride("separation", 8);
        Gallery.AddChild(sections);
        var reading = new VBoxContainer { Name = "CollectionReading", CustomMinimumSize = new(InspectorWidth, 0),
            SizeFlagsHorizontal = SizeFlags.Fill, SizeFlagsVertical = SizeFlags.ExpandFill };
        reading.AddThemeConstantOverride("separation", 8);
        AddChild(reading);
        if (!ShortScreen)
        {
            Showcase = new CardShowcase { Name = "CollectionShowcase", CustomMinimumSize = new(0, 200),
                SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1.1f };
            reading.AddChild(Showcase);
        }
        Inspector = new CardInspector { Name = "CollectionInspector", ShortScreen = ShortScreen };
        reading.AddChild(Inspector);
        Inspector.SizeFlagsHorizontal = SizeFlags.Fill;
        var scale = ShortScreen ? 0.6f : 1f;
        GridContainer? grid = null;
        for (var index = 0; index < Cards.Count; index++)
        {
            if (index == 0 || index == FaceDownStart)
            {
                var faceDown = index == FaceDownStart;
                if (FaceDownStart.HasValue)
                {
                    var heading = ArenaPresentation.Text(faceDown ? "Fondo · Boca abajo" : "Boca arriba", 14, BoardArt.Muted);
                    heading.Name = faceDown ? "FaceDownMemoryHeading" : "FaceUpMemoryHeading";
                    sections.AddChild(heading);
                }
                grid = new GridContainer { Name = faceDown ? "FaceDownMemoryCards" : "CollectionCards", Columns = 1 };
                grid.AddThemeConstantOverride("h_separation", 10); grid.AddThemeConstantOverride("v_separation", 10);
                sections.AddChild(grid);
                _grids.Add(grid);
            }
            var slot = index;
            Button item = Cards[index] is string id
                ? new HandCard { Card = CardCatalog.Get(id), PresentationScale = scale, RelikarioId = RelikarioOf?.Invoke(index) }
                : new OpponentHandCard { FaceSize = new Vector2(126, 184) * scale, Sleeve = Sleeve };
            item.Name = "CollectionCard" + index;
            item.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            item.Pressed += () =>
            {
                Inspect(slot);
                if (Cards[slot] != null) CardPressed?.Invoke(slot);
            };
            item.FocusEntered += () => Inspect(slot);
            item.MouseEntered += () => Inspect(slot);
            _cards.Add(item); grid!.AddChild(item);
            if (Cards[index] is string visibleId)
                item.TooltipText = CardCatalog.Get(visibleId).Name + (Inverted?.Invoke(index) == true ? " · Invertida" : "");
            if (Inverted?.Invoke(index) == true)
            {
                item.Resized += () => item.PivotOffset = item.Size / 2;
                item.RotationDegrees = 180;
            }
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
        var columns = Math.Clamp((int)((width + 10) / (cardWidth + 10)), 1, 6);
        foreach (var grid in _grids) grid.Columns = columns;
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
            Inspector.Present(card, card.Name, ArenaActionText.CardCost(card),
                hint: Inverted?.Invoke(index) == true ? "Invertida sobre Tomo de Magia." : "");
            Showcase?.Present(card, RelikarioOf?.Invoke(index));
        }
        else
        {
            Inspector.Present(null, "Carta oculta", hint: "La identidad de esta carta permanece oculta.");
            Showcase?.Present(null);
        }
        Inspector.SizeFlagsVertical = SizeFlags.ExpandFill;
        SelectionChanged?.Invoke(index);
    }
}
