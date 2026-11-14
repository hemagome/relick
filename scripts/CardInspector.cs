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
                CodeType.Character => "Código Personaje", CodeType.Object => "Código Objeto", CodeType.Play => "Código Jugada",
                CodeType.Trap when card.Id == CardCatalog.SuperMagnet.Id => "Código Trampa · Equipo",
                CodeType.Trap => "Código Trampa", _ => "Código"
            },
            CardKind.Attack when card.CodeType == CodeType.Trap => "Código Trampa · Ataque",
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

/// <summary>
/// What an attack with one skill copy can do, read from public card data. Responses, defences and
/// equipment bonuses are left out on purpose: they depend on information the viewer may not have.
/// </summary>
internal sealed record AttackOutlook(int Normal, int Critical, bool RandomNormal = false, int DieBonus = 0,
    int DamageMultiplier = 1, string? Caption = null)
{
    public static AttackOutlook For(CardDefinition skill, int counters, int dieBonus = 0, int multiplier = 1, string? caption = null) =>
        new(skill.NormalDamageAt(counters), skill.CriticalDamageAt(counters), skill.Id == CardCatalog.Magnet.Id, dieBonus,
            Math.Max(1, multiplier), caption);

    /// <summary>Damage for each final D6 face, using the engine's own formula.</summary>
    public string Damage(int die)
    {
        if (die < 4) return "—";
        if (RandomNormal && die < 6) return die == 4 ? "½·1D6" : "1D6";
        var amount = GameEngine.DamageFor(die, Normal, Critical) * DamageMultiplier;
        return amount.ToString();
    }

    public string Notes
    {
        get
        {
            var notes = new List<string>();
            if (DieBonus > 0) notes.Add($"Lv 2: +{DieBonus} al dado salvo con 1.");
            if (DamageMultiplier > 1) notes.Add($"Estrella del Fontanero: daño ×{DamageMultiplier}.");
            notes.Add("Antes de respuestas, defensas y bonus de equipo.");
            return string.Join(" ", notes);
        }
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
    internal Label AlertLabel { get; private set; } = null!;
    internal VBoxContainer Outlook { get; private set; } = null!;
    internal Label OutlookCaption { get; private set; } = null!;
    internal Label OutlookNotes { get; private set; } = null!;
    private PanelContainer _alert = null!;
    private readonly List<Label> _outlookValues = [];
    internal IReadOnlyList<Label> OutlookValues => _outlookValues;
    private readonly List<Label> _attributeValues = [];
    private ScrollContainer _reading = null!;
    private HBoxContainer _cost = null!;
    private string? _cardId;
    private string _lastHint = "";
    internal bool HasCard { get; private set; }
    internal ScrollContainer ReadingViewport => _reading;

    public override void _Ready()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://scripts/card_inspector_acrylic.gdshader") };
        var style = RelickVisuals.Frame(RelickVisuals.Playmat.Raised, RelickVisuals.Playmat.Frame.Lightened(0.12f));
        style.ContentMarginLeft = style.ContentMarginRight = ShortScreen ? 10 : 16;
        style.ContentMarginTop = style.ContentMarginBottom = ShortScreen ? 4 : 8;
        AddThemeStyleboxOverride("panel", style);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", ShortScreen ? 3 : 6); AddChild(column);
        // A refused play is told here, where the player is already looking, not in the header status line.
        _alert = new PanelContainer { Name = "PlayRejection", Visible = false };
        var alertStyle = RelickVisuals.Frame(BoardArt.Tint(RelickVisuals.State.Danger, 0.16f), BoardArt.Tint(RelickVisuals.State.Danger, 0.85f), 6, 2);
        alertStyle.ContentMarginTop = alertStyle.ContentMarginBottom = 6; alertStyle.ContentMarginLeft = 8;
        _alert.AddThemeStyleboxOverride("panel", alertStyle); column.AddChild(_alert);
        var alertRow = new HBoxContainer(); alertRow.AddThemeConstantOverride("separation", 8); _alert.AddChild(alertRow);
        alertRow.AddChild(new IconView { Icon = GameIcon.Alert, Color = RelickVisuals.State.Danger,
            CustomMinimumSize = new(20, 20), SizeFlagsVertical = SizeFlags.ShrinkCenter });
        AlertLabel = Wrapped("PlayRejectionText", ShortScreen ? 13 : 14, BoardArt.Ink); alertRow.AddChild(AlertLabel);
        // Decisions and card identity remain visible while only the description scrolls.
        HintLabel = Wrapped("ContextHint", ShortScreen ? 13 : 14, BoardArt.Ink); column.AddChild(HintLabel);
        var header = new VBoxContainer { Name = "InspectorHeader" };
        header.AddThemeConstantOverride("separation", ShortScreen ? 2 : 6); column.AddChild(header);
        ClassificationLabel = ArenaPresentation.Text("", 12, ArenaPresentation.Mint);
        ClassificationLabel.Name = "InspectorClassification"; ClassificationLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart; header.AddChild(ClassificationLabel);
        TitleLabel = Wrapped("ContextCardName", ShortScreen ? 17 : 20); header.AddChild(TitleLabel);
        Attributes = new HBoxContainer { Name = "InspectorAttributes" }; Attributes.AddThemeConstantOverride("separation", 12); header.AddChild(Attributes);
        foreach (var caption in new[] { "Daño normal", "Daño crítico", "Premio" })
        {
            var attribute = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            attribute.AddThemeConstantOverride("separation", 2); Attributes.AddChild(attribute);
            attribute.AddChild(ArenaPresentation.Text(caption, 11, BoardArt.Muted));
            var value = ArenaPresentation.Text("", ShortScreen ? 17 : 23, BoardArt.Gold);
            attribute.AddChild(value); _attributeValues.Add(value);
        }
        CounterLabel = Wrapped("InspectorCounters", 12, BoardArt.Gold); header.AddChild(CounterLabel);
        BuildOutlook(header);
        _reading = new ScrollContainer { Name = "InspectorReading",
            CustomMinimumSize = new(0, ReadingHeight > 0 ? ReadingHeight : ShortScreen ? 40 : 64),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true, SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddChild(_reading);
        DescriptionLabel = Wrapped("InspectorDescription", 16); _reading.AddChild(DescriptionLabel);
        _cost = new HBoxContainer { Name = "InspectorCost" }; _cost.AddThemeConstantOverride("separation", 12); column.AddChild(_cost);
        _cost.AddChild(ArenaPresentation.Text("Coste", 12, BoardArt.Muted));
        CostLabel = Wrapped("ContextCost", 13, BoardArt.Gold); CostLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        CostLabel.HorizontalAlignment = HorizontalAlignment.Right; _cost.AddChild(CostLabel);
    }

    /// <summary>Final D6 face → result, in the order printed by the rulebook: 1–3 miss, 4 reduced, 5 normal, 6 critical.</summary>
    private void BuildOutlook(VBoxContainer header)
    {
        Outlook = new VBoxContainer { Name = "AttackOutlook", Visible = false };
        Outlook.AddThemeConstantOverride("separation", 2); header.AddChild(Outlook);
        OutlookCaption = ArenaPresentation.Text("", 12, BoardArt.Muted); OutlookCaption.Name = "AttackOutlookCaption";
        OutlookCaption.AutowrapMode = TextServer.AutowrapMode.WordSmart; Outlook.AddChild(OutlookCaption);
        var faces = new HBoxContainer { Name = "AttackOutlookFaces" }; faces.AddThemeConstantOverride("separation", 4); Outlook.AddChild(faces);
        foreach (var (die, result, color) in new[] { ("1–3", "Fallo", BoardArt.Muted), ("4", "Reducido", BoardArt.Ink), ("5", "Normal", BoardArt.Ink), ("6", "Crítico", BoardArt.Gold) })
        {
            var cell = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var style = RelickVisuals.Frame(BoardArt.Tint(RelickVisuals.Playmat.Background, 0.55f), BoardArt.Tint(color, 0.45f), 4);
            style.ContentMarginLeft = style.ContentMarginRight = 4; style.ContentMarginTop = style.ContentMarginBottom = 3;
            cell.AddThemeStyleboxOverride("panel", style); faces.AddChild(cell);
            var stack = new VBoxContainer(); stack.AddThemeConstantOverride("separation", 0); cell.AddChild(stack);
            var face = ArenaPresentation.Text($"D6 {die}", 12, BoardArt.Muted); face.HorizontalAlignment = HorizontalAlignment.Center; stack.AddChild(face);
            var value = ArenaPresentation.Text("", ShortScreen ? 15 : 18, color); value.HorizontalAlignment = HorizontalAlignment.Center;
            value.AddThemeFontOverride("font", RelickVisuals.Strong); stack.AddChild(value); _outlookValues.Add(value);
            var name = ArenaPresentation.Text(result, 12, BoardArt.Tint(color, 0.85f)); name.HorizontalAlignment = HorizontalAlignment.Center; stack.AddChild(name);
        }
        OutlookNotes = ArenaPresentation.Text("", 12, BoardArt.Muted); OutlookNotes.Name = "AttackOutlookNotes";
        OutlookNotes.AutowrapMode = TextServer.AutowrapMode.WordSmart; Outlook.AddChild(OutlookNotes);
    }

    internal void PresentOutlook(AttackOutlook? outlook)
    {
        // Short screens keep their room for the decision buttons; the attributes row already gives normal and critical.
        if (ShortScreen) outlook = null;
        Outlook.Visible = outlook != null;
        if (outlook == null) return;
        OutlookCaption.Text = outlook.Caption ?? "Resultado del ataque según el dado";
        for (var face = 0; face < _outlookValues.Count; face++) _outlookValues[face].Text = outlook.Damage(face + 3);
        OutlookNotes.Text = outlook.Notes;
    }

    /// <summary>Shows or clears the refused-play notice; unchanged text does no work, so it may be called every frame.</summary>
    internal void SetAlert(string text)
    {
        if (AlertLabel.Text == text && _alert.Visible == text.Length > 0) return;
        AlertLabel.Text = text; AlertLabel.TooltipText = text; _alert.Visible = text.Length > 0;
    }

    private static Label Wrapped(string name, int size, Color? color = null)
    {
        var label = ArenaPresentation.Text("", size, color); label.Name = name;
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart; label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return label;
    }

    internal void SetHint(string hint)
    {
        if (_lastHint != hint) _reading.ScrollVertical = 0;
        _lastHint = hint;
        HintLabel.Text = hint; HintLabel.Visible = hint.Length > 0; HintLabel.TooltipText = hint;
        TitleLabel.Visible = TitleLabel.Text.Length > 0 && (HasCard || hint.Length == 0);
    }

    internal void Present(CardDefinition? card, string title, string cost = "", string hint = "", int? counters = null, AttackOutlook? outlook = null)
    {
        PresentOutlook(outlook);
        if (_cardId != card?.Id) _reading.ScrollVertical = 0;
        _cardId = card?.Id; HasCard = card != null;
        _reading.Visible = HasCard;
        SizeFlagsVertical = HasCard ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin;
        TitleLabel.Text = title; TitleLabel.TooltipText = title;
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
        var counterLimit = card?.Id switch
        {
            var id when id == CardCatalog.Destiny.Id => 2,
            var id when id == CardCatalog.SuperMagnet.Id => CardCatalog.SuperMagnetCounterLimit,
            _ => 0
        };
        CounterLabel.Text = counters.HasValue && counterLimit > 0 ? $"Contadores: {counters}/{counterLimit}" : "";
        CounterLabel.Visible = CounterLabel.Text.Length > 0;
        CostLabel.Text = cost; _cost.Visible = cost.Length > 0;
        SetHint(hint);
    }
}
