using Godot;
using Relick.Core;

namespace Relick;

// Text and attributes come from the same catalogue as the cards; no rules are run here.
internal static class CardInspectorText
{
    public static string Classification(CardDefinition card)
    {
        var type = card.Kind switch
        {
            CardKind.Skill => card.Id == CardCatalog.Magnet.Id || card.Id == CardCatalog.Destiny.Id ? "Habilidad · Yuno" : "Habilidad",
            CardKind.Code => card.CodeType switch
            {
                CodeType.Character => "Código Personaje", CodeType.Object => "Código Objeto", CodeType.Play => "Código Jugada", _ => "Código"
            },
            CardKind.Equipment => "Equipo", CardKind.Attack => "Ataque", _ => BoardArt.CardType(card)
        };
        return card.IsCombo ? type + " · COMBO" : type;
    }

    public static string Description(CardDefinition card)
    {
        var text = card.Text;
        if (card.Kind == CardKind.Skill)
        {
            var normal = card.Id == CardCatalog.Magnet.Id ? "1D6" : card.NormalDamage.ToString();
            var attributes = $"Daño normal: {normal} · Crítico: {card.CriticalDamage} · Premio: {card.Reward} PR. ";
            if (text.StartsWith(attributes, StringComparison.Ordinal)) text = text[attributes.Length..];
        }
        if (card.IsCombo && text.StartsWith("COMBO. ", StringComparison.Ordinal)) text = text[7..];
        return text;
    }
}

public partial class CardInspector : PanelContainer
{
    public bool ShortScreen { get; init; }
    public int ReadingHeight { get; init; }
    internal Label TitleLabel { get; private set; } = null!;
    internal Label CostLabel { get; private set; } = null!;
    internal Label HintLabel { get; private set; } = null!;
    internal Label ClassificationLabel { get; private set; } = null!;
    internal Label DescriptionLabel { get; private set; } = null!;
    internal Label CounterLabel { get; private set; } = null!;
    internal HBoxContainer Attributes { get; private set; } = null!;
    private readonly List<Label> _attributeValues = [];
    private ScrollContainer _reading = null!;
    private HBoxContainer _cost = null!;
    private string? _cardId;

    public override void _Ready()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var style = RelickVisuals.Frame(RelickVisuals.Playmat.Raised, RelickVisuals.Playmat.Frame);
        style.ContentMarginLeft = style.ContentMarginRight = ShortScreen ? 10 : 16;
        style.ContentMarginTop = style.ContentMarginBottom = ShortScreen ? 4 : 8;
        AddThemeStyleboxOverride("panel", style);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", ShortScreen ? 2 : 6); AddChild(column);
        _reading = new ScrollContainer { Name = "InspectorReading", CustomMinimumSize = new(0, ReadingHeight > 0 ? ReadingHeight : ShortScreen ? 76 : 176),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true, SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddChild(_reading);
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", ShortScreen ? 2 : 6); _reading.AddChild(content);
        // Pending choices must stay ahead of a long card description, including on phones.
        HintLabel = Wrapped("ContextHint", 12, BoardArt.Muted); content.AddChild(HintLabel);
        ClassificationLabel = ArenaPresentation.Text("", 12, ArenaPresentation.Mint);
        ClassificationLabel.Name = "InspectorClassification"; ClassificationLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart; content.AddChild(ClassificationLabel);
        TitleLabel = Wrapped("ContextCardName", ShortScreen ? 17 : 20); content.AddChild(TitleLabel);
        Attributes = new HBoxContainer { Name = "InspectorAttributes" }; Attributes.AddThemeConstantOverride("separation", 12); content.AddChild(Attributes);
        foreach (var caption in new[] { "Daño normal", "Daño crítico", "Premio" })
        {
            var attribute = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            attribute.AddThemeConstantOverride("separation", 2); Attributes.AddChild(attribute);
            attribute.AddChild(ArenaPresentation.Text(caption, 11, BoardArt.Muted));
            var value = ArenaPresentation.Text("", ShortScreen ? 19 : 23, BoardArt.Gold);
            attribute.AddChild(value); _attributeValues.Add(value);
        }
        DescriptionLabel = Wrapped("InspectorDescription", 16); content.AddChild(DescriptionLabel);
        CounterLabel = Wrapped("InspectorCounters", 12, BoardArt.Gold); content.AddChild(CounterLabel);
        _cost = new HBoxContainer { Name = "InspectorCost" }; _cost.AddThemeConstantOverride("separation", 12); column.AddChild(_cost);
        _cost.AddChild(ArenaPresentation.Text("Coste", 12, BoardArt.Muted));
        CostLabel = Wrapped("ContextCost", 13, BoardArt.Gold); CostLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        CostLabel.HorizontalAlignment = HorizontalAlignment.Right; _cost.AddChild(CostLabel);
    }

    private static Label Wrapped(string name, int size, Color? color = null)
    {
        var label = ArenaPresentation.Text("", size, color); label.Name = name;
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart; label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return label;
    }

    internal void Present(CardDefinition? card, string title, string cost = "", string hint = "", int? counters = null)
    {
        if (_cardId != card?.Id) _reading.ScrollVertical = 0;
        _cardId = card?.Id;
        TitleLabel.Text = title; TitleLabel.Visible = title.Length > 0;
        ClassificationLabel.Text = card == null ? "" : CardInspectorText.Classification(card);
        ClassificationLabel.Visible = card != null;
        DescriptionLabel.Text = card == null ? "" : CardInspectorText.Description(card); DescriptionLabel.Visible = card != null;
        Attributes.Visible = card?.Kind == CardKind.Skill;
        if (card?.Kind == CardKind.Skill)
        {
            _attributeValues[0].Text = card.Id == CardCatalog.Magnet.Id ? "1D6" : card.NormalDamageAt(counters ?? 0).ToString();
            _attributeValues[1].Text = card.CriticalDamageAt(counters ?? 0).ToString();
            _attributeValues[2].Text = card.Reward + " PR";
        }
        CounterLabel.Text = counters.HasValue && card?.Id == CardCatalog.Destiny.Id ? $"Contadores: {counters}/2" : "";
        CounterLabel.Visible = CounterLabel.Text.Length > 0;
        CostLabel.Text = cost; _cost.Visible = cost.Length > 0;
        HintLabel.Text = hint; HintLabel.Visible = hint.Length > 0;
    }
}
