using Godot;
using Relick.Core;

namespace Relick;

// Presentation accepts only public/player-authorized data. It never executes rules.
public sealed class ArenaPlayerActions
{
    public Action<bool> Memory { get; init; } = _ => { };
    public Action Attached { get; init; } = () => { };
    public Action Equipment { get; init; } = () => { };
    public Action EquipmentCovers { get; init; } = () => { };
    public Action Zahira { get; init; } = () => { };
    public string? ZahiraCaption { get; init; }
    public Action<int> Skill { get; init; } = _ => { };
    public Action<int> InspectSkill { get; init; } = _ => { };
    public Action Evolve { get; init; } = () => { };
    public bool CanEvolve { get; init; }
    public string EvolutionHint { get; init; } = "Evolucionar cuesta 1800 PR y requiere un 6 natural.";
}

public static class ArenaPresentation
{
    public static readonly Color Mint = RelickVisuals.State.Own;
    public static readonly Color Coral = RelickVisuals.State.Rival;

    public static Label Text(string text, int size = 16, Color? color = null)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? BoardArt.Ink);
        return label;
    }

    public static StyleBoxFlat Box(string background, Color border, int radius = 12)
        => RelickVisuals.Frame(new Color(background), border, Math.Min(radius, 8));

    public static Button Button(Node parent, string text, Action action, bool disabled = false)
    {
        var button = new Button { Text = text, Disabled = disabled, CustomMinimumSize = new(0, 48), MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        button.Pressed += action; parent.AddChild(button); return button;
    }

    public static PanelContainer Panel(Node parent, string background, Color border)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", Box(background, border, 16));
        parent.AddChild(panel); return panel;
    }

    public static BoxContainer Arena(VBoxContainer root, bool compact)
    {
        BoxContainer arena;
        if (compact)
        {
            var scroll = new ScrollContainer { Name = "ArenaScroll", SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            root.AddChild(scroll);
            arena = new VBoxContainer { Name = "DuelArena", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            scroll.AddChild(arena);
        }
        else
        {
            // The inspector can grow below the board without resizing the player panels.
            arena = new HBoxContainer { Name = "DuelArena", SizeFlagsVertical = Control.SizeFlags.Fill };
            root.AddChild(arena);
        }
        arena.AddThemeConstantOverride("separation", compact ? 8 : 12);
        return arena;
    }

    public static void Player(Node arena, DuelPlayerView owner, int index, bool enemy, bool active,
        int selectedSkill, YunoPortrait portrait, LifeMeter life, ActionCrystalPool crystals, ArenaPlayerActions actions,
        bool compact, bool narrow = false)
    {
        var accent = enemy ? Coral : Mint;
        var panel = Panel(arena, RelickVisuals.Playmat.Surface.ToHtml(), RelickVisuals.Playmat.Frame);
        panel.Name = enemy ? "EnemyRelikarioPanel" : "OwnRelikarioPanel";
        panel.CustomMinimumSize = new(compact ? 0 : 320, 0);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 6); panel.AddChild(column);
        var header = new HBoxContainer(); column.AddChild(header);
        var names = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        names.AddThemeConstantOverride("separation", 0);
        var ownerName = Text(active ? $"» {owner.Name}" : owner.Name, 9, accent);
        ownerName.Name = $"PlayerName{index}"; ownerName.ClipText = compact; ownerName.TooltipText = owner.Name; names.AddChild(ownerName);
        var name = Text($"{RelikarioCatalog.Yuno.Name} · Lv {owner.Level}", compact ? 15 : 18);
        name.ClipText = compact; name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        name.Name = $"RelikarioName{index}"; name.TooltipText = $"{owner.Name} · Vida máxima: {owner.MaxHealth}";
        names.AddChild(name); header.AddChild(names);
        var stats = narrow ? new HBoxContainer() : header;
        if (narrow) column.AddChild(stats);
        stats.AddChild(new DexterityBadge { Name = $"Dexterity{index}", Amount = owner.Dexterity, Accent = accent });
        if (owner.StarEffects > 0)
        {
            var star = Text($"★ ×{1 << owner.StarEffects}", compact ? 11 : 12, BoardArt.Gold);
            star.Name = $"StarEffects{index}";
            star.TooltipText = "Estrella del Fontanero: no recibe daño y multiplica su daño hasta el final de este turno.";
            stats.AddChild(star);
        }
        stats.AddChild(new ResourceBadge { Amount = owner.Pr });
        if (!enemy)
        {
            var evolve = Button(stats, owner.Level == 1 ? "Lv 2 ↑" : "Lv 2 ✓", actions.Evolve, !actions.CanEvolve);
            evolve.Name = "EvolveRelikario"; evolve.AddThemeFontSizeOverride("font_size", 12); evolve.TooltipText = actions.EvolutionHint;
        }
        var meters = new HBoxContainer();
        meters.AddThemeConstantOverride("separation", 10);
        life.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        life.SetHealth(owner.Health, owner.MaxHealth);
        crystals.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        meters.AddChild(life);
        meters.AddChild(crystals);
        column.AddChild(meters);
        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 6);
        var zones = new VBoxContainer { CustomMinimumSize = new(70, 0),
            SizeFlagsVertical = compact ? Control.SizeFlags.ShrinkCenter : Control.SizeFlags.ShrinkBegin };
        zones.AddThemeConstantOverride("separation", 6);
        zones.AddChild(new DeckStack { Name = $"DeckStack{index}", Amount = owner.DeckCount, Accent = accent, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter });
        var memories = new HBoxContainer(); memories.AddThemeConstantOverride("separation", 6);
        var memory = new MemoryZoneButton { Name = $"MemoryButton{index}", Amount = owner.Memory.Length, Accent = accent,
            TopCard = owner.Memory.Length == 0 ? null : CardCatalog.Get(owner.Memory[^1]) };
        memory.Pressed += () => actions.Memory(false); memories.AddChild(memory);
        var corrupted = new MemoryZoneButton { Name = $"CorruptedMemoryButton{index}", Amount = owner.CorruptedCount, Corrupted = true };
        corrupted.Pressed += () => actions.Memory(true); memories.AddChild(corrupted);
        if (!narrow) zones.AddChild(memories);
        var permanent = new VBoxContainer { CustomMinimumSize = new(92, 0),
            SizeFlagsVertical = compact ? Control.SizeFlags.ShrinkCenter : Control.SizeFlags.Fill };
        permanent.AddThemeConstantOverride("separation", 6);
        var attached = new AttachmentStack { Name = $"AttachmentStack{index}", Cards = owner.Attached };
        attached.Pressed += actions.Attached; permanent.AddChild(attached);
        if (owner.EquipmentCovers is { Length: > 0 } covers)
        {
            var cover = new ZoneCardButton { Name = $"EquipmentCoverZone{index}", Caption = "ORO",
                Card = CardCatalog.Get(covers[^1]), Accent = BoardArt.Gold, StatusText = covers.Length > 1 ? $"×{covers.Length}" : "" };
            cover.Pressed += actions.EquipmentCovers; permanent.AddChild(cover);
        }
        var equipmentStatus = owner.Equipment switch
        {
            null => "",
            _ when owner.EquipmentStackedCopies > 0 => $"×{owner.EquipmentStackedCopies + 1}",
            _ when owner.Equipment == CardCatalog.SuperMagnet.Id => $"{owner.EquipmentCounters}/{CardCatalog.SuperMagnetCounterLimit}",
            _ when owner.Equipment == CardCatalog.Sniper.Id => $"{owner.EquipmentCounters}/3",
            _ when owner.EquipmentDie > 0 => $"D6: {owner.EquipmentDie}",
            _ => ""
        };
        var equipment = new ZoneCardButton { Name = $"EquipmentZone{index}", Caption = owner.Equipment == null ? "EQUIPO 0/1" : "EQUIPO", StatusText = equipmentStatus,
            Card = owner.Equipment == null ? null : CardCatalog.Get(owner.Equipment), Accent = new("81c8ef"),
            Compact = compact, LargePreview = true, SizeFlagsVertical = compact ? Control.SizeFlags.Fill : Control.SizeFlags.ExpandFill };
        equipment.Pressed += actions.Equipment; permanent.AddChild(equipment);
        portrait.SizeFlagsVertical = narrow ? Control.SizeFlags.ShrinkBegin : Control.SizeFlags.ExpandFill;
        body.AddChild(enemy ? zones : permanent); body.AddChild(portrait); body.AddChild(enemy ? permanent : zones); column.AddChild(body);
        if (narrow) column.AddChild(memories);
        if (actions.ZahiraCaption is { } zahiraCaption)
        {
            var zahira = Button(column, zahiraCaption, actions.Zahira);
            zahira.Name = $"Zahira{index}";
            zahira.ClipText = true;
            zahira.TooltipText = zahiraCaption;
            zahira.Icon = BoardArt.LoadCardArtwork(CardCatalog.ZahiraFlair);
            zahira.AddThemeConstantOverride("icon_max_width", 24);
        }
        var skills = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        skills.AddThemeConstantOverride("h_separation", 6); skills.AddThemeConstantOverride("v_separation", 6);
        foreach (var slot in new[] { 2, 3, 0, 1 })
        {
            var learnedIndex = owner.SkillIndexAtPosition(slot);
            var learned = learnedIndex >= 0;
            var skill = new SkillTile { Name = $"Skill{index}_{slot}", Skill = learned ? CardCatalog.Get(owner.Skills[learnedIndex]) : null,
                Slot = slot, SkillIndex = learnedIndex, Counters = learnedIndex >= 0 && learnedIndex < owner.Counters.Length ? owner.Counters[learnedIndex] : 0,
                Chosen = !enemy && learned && selectedSkill == learnedIndex, Accent = accent, Disabled = enemy && !learned,
                PresentationScale = compact ? 0.7f : 1 };
            skill.Pressed += () => { if (learned) actions.Skill(learnedIndex); };
            skill.GuiInput += input =>
            {
                if (learned && input is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }) actions.InspectSkill(learnedIndex);
            };
            skills.AddChild(skill);
        }
        column.AddChild(skills);
    }

    public static PanelContainer Combat(Node arena, bool preparing, bool over, string message,
        DuelPlayerView[] players, Action<VBoxContainer> opponentHand, Action<int> bonusPressed,
        Action<int, int> exchangePressed, bool compact, Action<VBoxContainer, Label, Label>? resolution = null)
    {
        var panel = Panel(arena, RelickVisuals.Playmat.Surface.ToHtml(), RelickVisuals.Playmat.Frame); panel.Name = "CombatCenter";
        panel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        panel.CustomMinimumSize = new(compact ? 0 : 312, 0); panel.SizeFlagsStretchRatio = 1.05f;
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 4); panel.AddChild(column);
        opponentHand(column); column.AddChild(new HSeparator { Modulate = new("38415a") });
        var title = Text(over ? "RESULTADO DEL DUELO" : "RESOLUCIÓN DE COMBATE", 10, BoardArt.Gold);
        title.HorizontalAlignment = HorizontalAlignment.Center; title.Visible = !preparing; column.AddChild(title);
        var status = Text(message, 20); status.Name = "CombatMessage";
        status.AutowrapMode = TextServer.AutowrapMode.WordSmart; status.HorizontalAlignment = HorizontalAlignment.Center;
        status.CustomMinimumSize = new(0, 32); status.Visible = message.Length > 0; column.AddChild(status);
        resolution?.Invoke(column, title, status);
        column.AddChild(new HSeparator { Modulate = new("38415a") });
        var caption = Text("CANJE  →  BONUS", 9, BoardArt.Muted); caption.HorizontalAlignment = HorizontalAlignment.Center; column.AddChild(caption);
        foreach (var index in new[] { 1, 0 })
        {
            var owner = players[index];
            var track = new ExchangeTrack { Name = $"BonusTrack{index}", OwnerCaption = index == 0 ? "TÚ" : "RIVAL",
                Compact = compact,
                ExchangeCards = owner.Exchange.Select(CardCatalog.Get).ToArray(), Preparing = preparing,
                BonusEffectUsed = owner.BonusEffectUsed, ExchangeEffectsUsed = owner.ExchangeEffectsUsed ?? Array.Empty<int>(),
                BonusCard = owner.Bonus == null ? null : CardCatalog.Get(owner.Bonus), Accent = index == 0 ? Mint : Coral };
            track.BonusInspected += () => bonusPressed(index); track.ExchangeInspected += slot => exchangePressed(index, slot); column.AddChild(track);
        }
        return panel;
    }

    public static ScrollContainer InitialSkills(Node parent, IReadOnlyList<string> skills, string? chosen, Action<string> selected, bool shortScreen, int railColumns = 0)
    {
        parent.AddChild(HandHeading("ELIGE TU HABILIDAD INICIAL", skills.Count, railColumns));
        var scroll = CardGallery("InitialSkillScroll", shortScreen, railColumns);
        var row = GalleryContents(railColumns, 12); scroll.AddChild(row); parent.AddChild(scroll);
        for (var i = 0; i < skills.Count; i++)
        {
            var id = skills[i];
            var card = new HandCard { Name = "InitialSkillCard" + i, Card = CardCatalog.Get(id), Chosen = id == chosen,
                PresentationScale = shortScreen ? 0.6f : 1 };
            card.Pressed += () => selected(id); row.AddChild(card);
        }
        return scroll;
    }

    public static ScrollContainer Hand(Node parent, IReadOnlyList<string> hand, Func<int, bool> selected,
        Action<int> pressed, bool preparing, bool shortScreen, string? title = null, int railColumns = 0, bool useFan = true)
    {
        if (title == null) HandCounter(parent, hand.Count, enemy: false);
        else parent.AddChild(HandHeading(title, hand.Count, railColumns));
        var scroll = CardGallery("HandScroll", shortScreen, railColumns);
        var fanned = useFan && railColumns == 0 && hand.Count > 0;
        scroll.FollowFocus = fanned;
        Control cards = fanned ? new HandFan() : GalleryContents(railColumns, 10);
        for (var i = 0; i < hand.Count; i++)
        {
            var slot = i;
            var card = new HandCard { Name = $"HandCard{i}", Card = CardCatalog.Get(hand[i]), Chosen = selected(i), Mulligan = preparing,
                InFan = fanned, PresentationScale = fanned ? HandFan.CardScale : shortScreen ? 0.6f : 1 };
            card.Pressed += () => pressed(slot); cards.AddChild(card);
        }
        if (hand.Count == 0 && railColumns == 0)
        {
            var hint = preparing ? "Sincronizando tu mano inicial…" : "Robarás 3 cartas al empezar tu turno.";
            cards.AddChild(Text(hint, 14, BoardArt.Muted));
        }
        scroll.AddChild(cards); parent.AddChild(scroll); return scroll;
    }

    public static void HandCounter(Node parent, int count, bool enemy)
    {
        var description = $"{(enemy ? "Mano rival" : "Tu mano")} · {count} {(count == 1 ? "carta" : "cartas")}";
        var row = new HBoxContainer
        {
            Name = enemy ? "OpponentHandCounter" : "OwnHandCounter",
            SizeFlagsHorizontal = enemy ? Control.SizeFlags.ShrinkBegin : Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Pass,
            TooltipText = description
        };
        row.AddThemeConstantOverride("separation", 5);
        row.AddChild(new IconView { Icon = GameIcon.Cards, Color = enemy ? Coral : Mint, CustomMinimumSize = new(16, 16) });
        var amount = Text(count.ToString(), 13);
        amount.AccessibilityName = description;
        amount.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(amount);
        parent.AddChild(row);
    }

    private static Label HandHeading(string title, int count, int railColumns)
    {
        var caption = railColumns > 0 ? (title == "ELIGE TU HABILIDAD INICIAL" ? "HABILIDAD INICIAL" : title.Split(" · ")[0]) + $"\n{count} CARTAS" : $"{title}  ·  {count} CARTAS";
        var heading = Text(caption, 10); heading.TooltipText = title;
        heading.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return heading;
    }

    private static ScrollContainer CardGallery(string name, bool shortScreen, int railColumns) => new()
    {
        Name = name, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        SizeFlagsVertical = railColumns > 0 ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill,
        CustomMinimumSize = new(railColumns > 0 ? railColumns * 75.6f + (railColumns - 1) * 12 + 16 : 0, shortScreen ? 120 : 196),
        VerticalScrollMode = railColumns > 0 ? ScrollContainer.ScrollMode.Auto : ScrollContainer.ScrollMode.Disabled,
        HorizontalScrollMode = railColumns > 0 ? ScrollContainer.ScrollMode.Disabled : ScrollContainer.ScrollMode.Auto, ScrollDeadzone = 12
    };

    private static Container GalleryContents(int railColumns, int separation)
    {
        if (railColumns > 0)
        {
            var grid = new GridContainer { Columns = railColumns, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            grid.AddThemeConstantOverride("h_separation", separation); grid.AddThemeConstantOverride("v_separation", separation);
            return grid;
        }
        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", separation); return row;
    }

    public static void Scoreline(Node parent, DuelPlayerView[] players)
    {
        var text = Text($"TÚ {players[0].Health}/{players[0].MaxHealth} · {players[0].Pr} PR     RIVAL {players[1].Health}/{players[1].MaxHealth} · {players[1].Pr} PR", 12, BoardArt.Muted);
        text.Name = "DuelScoreline"; text.AutowrapMode = TextServer.AutowrapMode.WordSmart; parent.AddChild(text);
    }

    public static void StageIndicator(Node parent, Phase stage, string detail, bool shortScreen)
    {
        var style = StageAppearance.For(stage);
        var panel = new PanelContainer { Name = "StageIndicator", MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        var frame = RelickVisuals.Frame(RelickVisuals.Playmat.Raised, BoardArt.Tint(style.Accent, 0.65f));
        frame.ContentMarginTop = frame.ContentMarginBottom = shortScreen ? 4 : 5;
        panel.AddThemeStyleboxOverride("panel", frame);
        parent.AddChild(panel);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 8);
        panel.AddChild(row);
        row.AddChild(new IconView { Icon = style.Icon, Color = style.Accent, CustomMinimumSize = Vector2.One * (shortScreen ? 16 : 23),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
        BoxContainer labels = shortScreen ? new HBoxContainer() : new VBoxContainer();
        labels.MouseFilter = Control.MouseFilterEnum.Ignore;
        labels.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        labels.AddThemeConstantOverride("separation", shortScreen ? 9 : 0);
        row.AddChild(labels);
        var title = Text(style.Title, shortScreen ? 13 : 16, style.Accent);
        title.Name = "CurrentStageTitle";
        labels.AddChild(title);
        var context = Text(detail, shortScreen ? 11 : 10, BoardArt.Ink);
        context.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        if (shortScreen) context.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        labels.AddChild(context);
    }

    public static (VBoxContainer Board, VBoxContainer Controls) Body(VBoxContainer root, bool split)
    {
        if (!split) return (root, root);
        var row = new HBoxContainer { Name = "ArenaColumns", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 10); root.AddChild(row);
        var board = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1.3f };
        var controls = new VBoxContainer { Name = "ArenaControls", CustomMinimumSize = new(300, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        controls.AddThemeConstantOverride("separation", 6); row.AddChild(board); row.AddChild(controls);
        return (board, controls);
    }
}
