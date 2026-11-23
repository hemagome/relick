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
            CardKind.Skill when card.Special => "Habilidad especial · Universal",
            CardKind.Skill => RelikarioCatalog.Find(card.Relikario) is { } affinity ? $"Habilidad · {affinity.DisplayName}" : "Habilidad",
            CardKind.Code => card.CodeType switch
            {
                CodeType.Character => "Código Personaje", CodeType.Object => "Código Objeto", CodeType.Play => "Código Jugada",
                CodeType.Trap when card.Id == CardCatalog.SuperMagnet.Id => "Código Trampa · Equipo",
                CodeType.Trap => "Código Trampa", _ => "Código"
            },
            CardKind.Attack when card.CodeType == CodeType.Trap => "Código Trampa · Ataque",
            CardKind.Equipment => card.LightEquipment ? "Equipo ligero" : "Equipo",
            CardKind.Attack => "Ataque", _ => BoardArt.CardType(card)
        };
        return card.IsCombo ? type + " · COMBO" : type;
    }

    public static string SkillStats(CardDefinition card) =>
        $"Daño {card.NormalDamageText()} · Crítico {card.CriticalDamageText()} · {card.RewardText}";

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
    int DamageMultiplier = 1, string? Caption = null, string? SkillId = null, bool BonusIncludesOne = false)
{
    public static AttackOutlook For(CardDefinition skill, int counters, int dieBonus = 0, int multiplier = 1,
        string? caption = null, bool bonusIncludesOne = false) =>
        new(skill.NormalDamageAt(counters), skill.CriticalDamageAt(counters), skill.Id == CardCatalog.Magnet.Id, dieBonus,
            Math.Max(1, multiplier), caption, skill.Id, bonusIncludesOne);

    /// <summary>Damage for each final D6 face, using the engine's own formula.</summary>
    public string Damage(int die)
    {
        if (die < 4) return "—";
        if (RandomNormal && die < 6) return die == 4 ? "½·1D6" : "1D6";
        string Range(int min, int max) => $"{min * DamageMultiplier}–{max * DamageMultiplier}";
        if (SkillId == CardCatalog.TurboSword.Id)
            return die switch { 4 => Range(1, 3), 5 => Range(2, 5), _ => Range(4, 7) };
        if (SkillId == CardCatalog.TurboOverlap.Id && die == 6) return Range(1, 6);
        if (SkillId == CardCatalog.TurboAttack.Id && die < 6) return die == 4 ? Range(1, 2) : Range(2, 3);
        if (SkillId == CardCatalog.RollingShuffle.Id && die == 6) return Range(2, 7);
        var amount = GameEngine.DamageFor(die, Normal, Critical) * DamageMultiplier;
        if (SkillId == CardCatalog.WildAim.Id)
            return die == 6 ? $"≥{amount}" : Range(GameEngine.DamageFor(die, Normal, Critical),
                GameEngine.DamageFor(die, CardCatalog.WildAim.MaxNormalDamage, Critical));
        if (SkillId == CardCatalog.MultiDrive.Id || SkillId == CardCatalog.WildDrive.Id && die == 6)
            return $"≥{amount}";
        return amount.ToString();
    }

    public string Notes
    {
        get
        {
            var notes = new List<string>();
            if (DieBonus > 0) notes.Add($"Lv 2: +{DieBonus} al dado" + (BonusIncludesOne ? "." : " salvo con 1."));
            if (SkillId == CardCatalog.TurboOverlap.Id) notes.Add("Crítico: lanza dos dados y elige uno.");
            if (DamageMultiplier > 1) notes.Add($"Estrella del Fontanero: daño ×{DamageMultiplier}.");
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
    internal CardCounterBadge CounterBadge { get; private set; } = null!;
    internal HBoxContainer Attributes { get; private set; } = null!;
    internal VBoxContainer Outlook { get; private set; } = null!;
    internal HBoxContainer Recipe { get; private set; } = null!;
    internal Label RecipeNames { get; private set; } = null!;
    private readonly List<TextureRect> _recipeCards = [];
    internal Label OutlookCaption { get; private set; } = null!;
    internal Label OutlookNotes { get; private set; } = null!;
    private readonly List<Label> _outlookValues = [];
    internal IReadOnlyList<Label> OutlookValues => _outlookValues;
    private readonly List<Label> _attributeValues = [];
    private readonly List<Control> _attributeColumns = [];
    private ScrollContainer _reading = null!;
    private TextureRect _relikarioArtwork = null!;
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
        HintLabel = Wrapped("ContextHint", ShortScreen ? 13 : 14, BoardArt.Gold); column.AddChild(HintLabel);
        var header = new VBoxContainer { Name = "InspectorHeader" };
        header.AddThemeConstantOverride("separation", ShortScreen ? 2 : 6); column.AddChild(header);
        ClassificationLabel = ArenaPresentation.Text("", 12, ArenaPresentation.Mint);
        ClassificationLabel.Name = "InspectorClassification"; ClassificationLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart; header.AddChild(ClassificationLabel);
        TitleLabel = Wrapped("ContextCardName", ShortScreen ? 17 : 20); header.AddChild(TitleLabel);
        Attributes = new HBoxContainer { Name = "InspectorAttributes" }; Attributes.AddThemeConstantOverride("separation", 12); header.AddChild(Attributes);
        foreach (var caption in new[] { "Daño normal", "Daño crítico", "Premio" })
        {
            var attribute = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            attribute.AddThemeConstantOverride("separation", 2); Attributes.AddChild(attribute); _attributeColumns.Add(attribute);
            attribute.AddChild(ArenaPresentation.Text(caption, 11, BoardArt.Muted));
            var value = ArenaPresentation.Text("", ShortScreen ? 17 : 23, BoardArt.Gold);
            attribute.AddChild(value); _attributeValues.Add(value);
        }
        CounterBadge = new CardCounterBadge { Name = "InspectorCounters", SizeFlagsHorizontal = SizeFlags.ShrinkBegin, MouseFilter = MouseFilterEnum.Pass };
        header.AddChild(CounterBadge);
        BuildRecipe(header);
        BuildOutlook(header);
        _reading = new ScrollContainer { Name = "InspectorReading",
            CustomMinimumSize = new(0, ReadingHeight > 0 ? ReadingHeight : ShortScreen ? 40 : 64),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true, SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddChild(_reading);
        var readingContent = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        readingContent.AddThemeConstantOverride("separation", 8);
        _reading.AddChild(readingContent);
        _relikarioArtwork = new TextureRect
        {
            Name = "InspectorRelikarioArtwork", Visible = false, MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = ShortScreen ? new(55, 78) : new(76, 108),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        readingContent.AddChild(_relikarioArtwork);
        DescriptionLabel = Wrapped("InspectorDescription", 16); readingContent.AddChild(DescriptionLabel);
        _cost = new HBoxContainer { Name = "InspectorCost" }; _cost.AddThemeConstantOverride("separation", 12); column.AddChild(_cost);
        _cost.AddChild(ArenaPresentation.Text("Coste", 12, BoardArt.Muted));
        CostLabel = Wrapped("ContextCost", 13, BoardArt.Gold); CostLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        CostLabel.HorizontalAlignment = HorizontalAlignment.Right; _cost.AddChild(CostLabel);
    }

    /// <summary>Final D6 face → result, for the faces that hit: 4 reduced, 5 normal, 6 critical.</summary>
    private void BuildOutlook(VBoxContainer header)
    {
        Outlook = new VBoxContainer { Name = "AttackOutlook", Visible = false };
        Outlook.AddThemeConstantOverride("separation", 2); header.AddChild(Outlook);
        OutlookCaption = ArenaPresentation.Text("", 12, BoardArt.Muted); OutlookCaption.Name = "AttackOutlookCaption";
        OutlookCaption.AutowrapMode = TextServer.AutowrapMode.WordSmart; Outlook.AddChild(OutlookCaption);
        var faces = new HBoxContainer { Name = "AttackOutlookFaces" }; faces.AddThemeConstantOverride("separation", 4); Outlook.AddChild(faces);
        foreach (var (die, result, color) in new[] { ("4", "Reducido", BoardArt.Ink), ("5", "Normal", BoardArt.Ink), ("6", "Crítico", BoardArt.Gold) })
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

    /// <summary>The two exchanged cards that met a Trap recipe, side by side: the creation's visible cause.</summary>
    private void BuildRecipe(VBoxContainer header)
    {
        Recipe = new HBoxContainer { Name = "TrapRecipe", Visible = false }; Recipe.AddThemeConstantOverride("separation", 6); header.AddChild(Recipe);
        for (var i = 0; i < 2; i++)
        {
            if (i == 1) Recipe.AddChild(ArenaPresentation.Text("+", 18, BoardArt.Muted));
            var card = new TextureRect { Name = "TrapIngredient" + i, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new(40, 56), MouseFilter = MouseFilterEnum.Pass };
            Recipe.AddChild(card); _recipeCards.Add(card);
        }
        RecipeNames = ArenaPresentation.Text("", 13, BoardArt.Muted); RecipeNames.Name = "TrapRecipeNames";
        RecipeNames.AutowrapMode = TextServer.AutowrapMode.WordSmart; RecipeNames.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        RecipeNames.VerticalAlignment = VerticalAlignment.Center; Recipe.AddChild(RecipeNames);
    }

    internal void PresentRecipe(IReadOnlyList<string>? ingredients)
    {
        var cards = ingredients?.Where(CardCatalog.All.ContainsKey).Select(CardCatalog.Get).Take(2).ToArray() ?? [];
        Recipe.Visible = cards.Length == 2;
        if (!Recipe.Visible) return;
        for (var i = 0; i < 2; i++) { _recipeCards[i].Texture = BoardArt.LoadCardArtwork(cards[i]); _recipeCards[i].TooltipText = cards[i].Name; }
        RecipeNames.Text = $"Canjeaste {cards[0].Name} y {cards[1].Name}.";
    }

    internal void PresentOutlook(AttackOutlook? outlook)
    {
        // Short screens keep their room for the decision buttons; the attributes row already gives normal and critical.
        if (ShortScreen) outlook = null;
        Outlook.Visible = outlook != null;
        // The D6 table already gives normal (5) and critical (6) damage: the attributes keep only the reward.
        _attributeColumns[0].Visible = _attributeColumns[1].Visible = outlook == null;
        if (outlook == null) return;
        OutlookCaption.Text = outlook.Caption ?? "Resultado del ataque según el dado";
        for (var face = 0; face < _outlookValues.Count; face++) _outlookValues[face].Text = outlook.Damage(face + 4);
        OutlookNotes.Text = outlook.Notes;
        OutlookNotes.Visible = OutlookNotes.Text.Length > 0;
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

    /// <summary>A Relikario is not a card in the catalog: its sheet goes to the showcase and its effect is read here.</summary>
    internal void PresentRelikario(RelikarioDefinition relikario, string ownerName, Texture2D? artwork = null)
    {
        Present(null, $"{relikario.Name} · Lv {relikario.Level}");
        _cardId = "relikario:" + relikario.Id; HasCard = true;
        _reading.Visible = true; SizeFlagsVertical = SizeFlags.ExpandFill;
        ClassificationLabel.Text = $"RELIKARIO DE {ownerName.ToUpperInvariant()} · {relikario.MaxHealth} VIDAS"; ClassificationLabel.Visible = true;
        _relikarioArtwork.Texture = artwork; _relikarioArtwork.Visible = artwork != null;
        var description = relikario.AbilityText;
        if (relikario.EvolutionHealing > 0)
            description = $"Al evolucionar, recupera hasta {relikario.EvolutionHealing} vidas.\n\n{description}";
        if (relikario.AttackDieBonusExceptOne > 0)
            description += $"\n\nSuma +{relikario.AttackDieBonusExceptOne} al D6 de ataque si el resultado es distinto de 1.";
        DescriptionLabel.Text = description; DescriptionLabel.Visible = true;
    }

    internal void Present(CardDefinition? card, string title, string cost = "", string hint = "", int? counters = null, AttackOutlook? outlook = null,
        IReadOnlyList<string>? recipe = null)
    {
        PresentOutlook(outlook);
        PresentRecipe(recipe);
        if (_cardId != card?.Id) _reading.ScrollVertical = 0;
        _cardId = card?.Id; HasCard = card != null;
        _reading.Visible = HasCard;
        _relikarioArtwork.Hide(); _relikarioArtwork.Texture = null;
        SizeFlagsVertical = HasCard ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin;
        TitleLabel.Text = title; TitleLabel.TooltipText = title;
        ClassificationLabel.Text = card == null ? "" : CardInspectorText.Classification(card);
        ClassificationLabel.Visible = card != null;
        DescriptionLabel.Text = card == null ? "" : CardInspectorText.Description(card); DescriptionLabel.Visible = card != null;
        Attributes.Visible = card?.Kind == CardKind.Skill;
        if (card?.Kind == CardKind.Skill)
        {
            _attributeValues[0].Text = card.NormalDamageText(counters ?? 0);
            _attributeValues[1].Text = card.CriticalDamageText(counters ?? 0);
            _attributeValues[2].Text = card.RewardText;
        }
        CounterBadge.Present(CardCounters.For(card, counters));
        CostLabel.Text = cost; _cost.Visible = cost.Length > 0;
        SetHint(hint);
    }
}
