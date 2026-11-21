using Godot;
using Relick.Core;

namespace Relick;

// Presentation accepts only public/player-authorized data. It never executes rules.
public sealed class ArenaPlayerActions
{
    public Action? Area { get; init; }
    public Action<bool> Memory { get; init; } = _ => { };
    public Action Attached { get; init; } = () => { };
    public Action Equipment { get; init; } = () => { };
    public Action LightEquipment { get; init; } = () => { };
    public Action EquipmentCovers { get; init; } = () => { };
    public Action<int> Skill { get; init; } = _ => { };
    public Action<int> InspectSkill { get; init; } = _ => { };
    public Action Evolve { get; init; } = () => { };
    public bool CanEvolve { get; init; }
    /// <summary>The server offers teaching a skill now; the next free space is lit.</summary>
    public bool CanLearn { get; init; }
    public string EvolutionHint { get; init; } = "Evolucionar cuesta 1800 PR y requiere un 6 natural.";
    public Action<ResourceBadge>? Relikia { get; init; }
    /// <summary>Opens this player's public profile; null when the server has no profiles.</summary>
    public Action? Profile { get; init; }
}

public static class ArenaPresentation
{
    public static readonly Color Mint = RelickVisuals.State.Own;
    public static readonly Color Coral = RelickVisuals.State.Rival;

    /// <summary>Cards selected from one memory, rendered as its own labelled block.</summary>
    public sealed record HandSection(string Title, Color Accent, CardOwner Owner, IReadOnlyList<int> Indices, bool FromDeck = false);

    public static Label Text(string text, int size = 16, Color? color = null)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? BoardArt.Ink);
        return label;
    }

    /// <summary>
    /// Once evolution is affordable, the Lv 2 button turns gold and breathes so the player notices the chance.
    /// The tween belongs to the button, so it stops when a rebuild frees it. Reduced motion keeps only the gold.
    /// </summary>
    internal static void InviteEvolution(Button evolve)
    {
        evolve.Name = "EvolveRelikario";
        RelickVisuals.Primary(evolve, BoardArt.Gold);
        evolve.SetMeta("evolution_invite", true);
        if (DisplayServer.AccessibilityShouldReduceAnimation() == 1 ||
            ProjectSettings.GetSetting("relick/accessibility/reduced_motion", false).AsBool()) return;
        void Center() => evolve.PivotOffset = evolve.Size / 2;
        evolve.Resized += Center; Center();
        var pulse = evolve.CreateTween().SetLoops().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        pulse.TweenProperty(evolve, "scale", new Vector2(1.08f, 1.08f), 0.55);
        pulse.Parallel().TweenProperty(evolve, "self_modulate", new Color(1.25f, 1.2f, 0.9f), 0.55);
        pulse.TweenProperty(evolve, "scale", Vector2.One, 0.55);
        pulse.Parallel().TweenProperty(evolve, "self_modulate", Colors.White, 0.55);
        pulse.TweenInterval(0.5);
    }

    public static Button Button(Node parent, string text, Action action, bool disabled = false)
    {
        var button = new Button { Text = text, Disabled = disabled, CustomMinimumSize = new(0, 48), MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        button.Pressed += action; parent.AddChild(button); return button;
    }

    public static PanelContainer Panel(Node parent, string background, Color border)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var fill = new Color(background);
        panel.AddThemeStyleboxOverride("panel", RelickVisuals.Glass(BoardArt.Tint(fill.Lightened(0.06f), 0.90f), BoardArt.Tint(fill.Darkened(0.2f), 0.90f),
            border, 10, 8, new Color(0, 0, 0, 0.4f)));
        parent.AddChild(panel); return panel;
    }

    /// <summary>Lets a player's name open their profile card with a click or a tap.</summary>
    private static void OpensProfile(Label name, string playerName, Action? profile)
    {
        if (profile == null) return;
        name.MouseFilter = Control.MouseFilterEnum.Stop;
        name.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        name.TooltipText = $"{playerName} · Ver perfil";
        name.GuiInput += input =>
        {
            if (input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;
            name.AcceptEvent();
            profile();
        };
    }

    private static Label Heading(string text, int size, Color color)
    {
        var label = Text(text, size, color);
        label.AddThemeFontOverride("font", RelickVisuals.Display);
        return label;
    }

    /// <summary>Translucent half of the playmat; both are laid before any zone so shared zones sit above them.</summary>
    public static PanelContainer Mat(BoardCanvas canvas, bool enemy, bool active)
    {
        var mat = new PanelContainer { Name = enemy ? "EnemyRelikarioPanel" : "OwnRelikarioPanel", MouseFilter = Control.MouseFilterEnum.Ignore };
        mat.AddThemeStyleboxOverride("panel", RelickVisuals.Field(enemy ? Coral : Mint, active));
        return canvas.PlaceField(mat, enemy, 0, 14, BoardCanvas.DesignWidth, 410);
    }

    /// <summary>
    /// One duelist's half of the desktop board, with every card at the same size. From the outer
    /// edge inward: canje under the Bonus and Área frame that straddles the divider, the Relikario
    /// over its equipment, the four skills in a 2×2 block, the memories and the deck. The rival's
    /// half is the same field turned around.
    /// </summary>
    public static void Field(BoardCanvas canvas, DuelPlayerView owner, int index, bool enemy, bool active, bool preparing,
        int selectedSkill, RelikarioPortrait portrait, LifeMeter life, ActionCrystalPool crystals, ArenaPlayerActions actions,
        Action bonusPressed, Action<int> exchangePressed)
    {
        const float W = BoardCanvas.CardWidth, H = BoardCanvas.CardHeight;
        const float top = 21, bottom = top + H + 10;
        var accent = enemy ? Coral : Mint;
        T Put<T>(T node, float x, float fromSeam, float width, float height) where T : Control
            => canvas.PlaceField(node, enemy, x, fromSeam, width, height);

        // Bonus and Área share one frame, half on each field, next to their owner's canje.
        var frame = new Panel { Name = $"BonusFrame{index}", MouseFilter = Control.MouseFilterEnum.Ignore };
        frame.AddThemeStyleboxOverride("panel", RelickVisuals.Frame(BoardArt.Tint(RelickVisuals.Playmat.Surface, 0.94f), BoardArt.Tint(accent, 0.8f), 10, 2));
        Put(frame, 0, -112.5f, 302, 225);
        var track = new ExchangeTrack { Name = $"BonusTrack{index}", Detached = true, OwnerCaption = index == 0 ? "TÚ" : "RIVAL",
            ExchangeCards = owner.Exchange.Select(CardCatalog.Get).ToArray(), Preparing = preparing, RelikarioId = owner.RelikarioId,
            BonusEffectUsed = owner.BonusEffectUsed, ExchangeEffectsUsed = owner.ExchangeEffectsUsed ?? Array.Empty<int>(),
            ShieldCharges = owner.MovedShieldCharges,
            BonusCard = owner.Bonus == null ? null : CardCatalog.Get(owner.Bonus), Accent = accent,
            AreaCard = owner.Area == null ? null : CardCatalog.Get(owner.Area), AreaSide = owner.AreaSide };
        track.BonusInspected += bonusPressed; track.ExchangeInspected += exchangePressed;
        track.AreaInspected += actions.Area;
        track.Build();
        canvas.Place(track, new Rect2(), BoardAnchor.Top);
        Put(track.AreaButton!, 8, -H / 2, W, H);
        Put(track.BonusButton, 156, -H / 2, W, H);
        for (var slot = 0; slot < track.ExchangeButtons.Length; slot++)
            Put(track.ExchangeButtons[slot], (302 - H) / 2, 131 + slot * (W + 10), H, W);

        // Relikario: portrait beside its name, lives, actions, Destreza and PR.
        var hero = new HBoxContainer { Name = $"Hero{index}", MouseFilter = Control.MouseFilterEnum.Ignore };
        hero.AddThemeConstantOverride("separation", 4);
        var stand = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        stand.AddThemeConstantOverride("separation", 2);
        var info = new VBoxContainer { CustomMinimumSize = new(118, 0), Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        info.AddThemeConstantOverride("separation", 3);
        if (enemy) { hero.AddChild(info); hero.AddChild(stand); } else { hero.AddChild(stand); hero.AddChild(info); }
        portrait.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        portrait.CustomMinimumSize = new(0, 96);
        if (canvas.TableView)
            stand.AddChild(new Control { CustomMinimumSize = portrait.CustomMinimumSize, SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore });
        else stand.AddChild(portrait);
        // Whose turn it is lives in the phase bar's status cell; the name is only the name.
        var ownerName = Heading(owner.Name, 17, accent);
        ownerName.Name = $"PlayerName{index}"; ownerName.ClipText = true; ownerName.TooltipText = owner.Name; info.AddChild(ownerName);
        OpensProfile(ownerName, owner.Name, actions.Profile);
        var name = Text($"{RelikarioCatalog.NameOf(owner.RelikarioId)} · Lv {owner.Level}", 11, BoardArt.Muted);
        name.ClipText = true; name.Name = $"RelikarioName{index}"; name.TooltipText = $"{owner.Name} · {RelikarioCatalog.NameOf(owner.RelikarioId)} · Vida máxima: {owner.MaxHealth}";
        info.AddChild(name);
        life.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        life.ReachedZero = owner.HasReachedZero;
        life.SetHealth(owner.Health, owner.MaxHealth);
        info.AddChild(life);
        var stats = new HBoxContainer(); stats.AddThemeConstantOverride("separation", 6); info.AddChild(stats);
        crystals.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        stats.AddChild(crystals);
        stats.AddChild(new DexterityBadge { Name = $"Dexterity{index}", Amount = owner.Dexterity, Accent = accent });
        if (owner.StarEffects > 0)
        {
            var star = Text($"★×{1 << owner.StarEffects}", 12, BoardArt.Gold);
            star.Name = $"StarEffects{index}";
            star.TooltipText = "Estrella del Fontanero: no recibe daño y multiplica su daño hasta el final de este turno.";
            stats.AddChild(star);
        }
        var resources = new ResourceBadge { Name = $"ResourceBadge{index}", Amount = owner.Pr, SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
        if (!enemy && actions.Relikia is { } relikia) resources.Activated = () => relikia(resources);
        info.AddChild(resources);
        if (!enemy)
        {
            var evolve = Button(stand, owner.Level == 1 ? "Lv 2 ↑" : "Lv 2 ✓", actions.Evolve, !actions.CanEvolve);
            evolve.Name = "EvolveRelikario"; evolve.AddThemeFontSizeOverride("font_size", 12); evolve.TooltipText = actions.EvolutionHint;
            evolve.CustomMinimumSize = new(0, 28); evolve.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            if (actions.CanEvolve && owner.Level == 1) InviteEvolution(evolve);
        }
        canvas.PlaceTableHud(hero, BoardCanvas.FieldRect(enemy, 306, top, 286, H));
        if (canvas.TableView) canvas.PlaceFigure(portrait, enemy);

        var equipmentStatus = owner.Equipment switch
        {
            null => "",
            _ when owner.EquipmentStackedCopies > 0 => $"×{owner.EquipmentStackedCopies + 1}",
            _ when owner.Equipment == CardCatalog.SuperMagnet.Id => $"{owner.EquipmentCounters}/{CardCatalog.SuperMagnetCounterLimit}",
            _ when owner.Equipment == CardCatalog.Sniper.Id => $"{owner.EquipmentCounters}/3",
            _ when owner.EquipmentDie > 0 => $"D6: {owner.EquipmentDie}",
            _ => ""
        };
        var equipment = new ZoneCardButton { Name = $"EquipmentZone{index}", Caption = "EQUIPO", StatusText = equipmentStatus,
            Card = owner.Equipment == null ? null : CardCatalog.Get(owner.Equipment), Accent = new("7cc8ff"), LargePreview = true, SlotWidth = W,
            StackCopies = canvas.TableView ? owner.EquipmentStackedCopies + 1 : 0 };
        equipment.Pressed += actions.Equipment; Put(equipment, 306, bottom, W, H);
        if (LightEquipmentZone(owner, index, actions.LightEquipment, false, W, canvas.TableView) is { } lights)
            Put(lights, 1150, top, W, H);
        var attached = new AttachmentStack { Name = $"AttachmentStack{index}", Cards = owner.Attached, RelikarioId = owner.RelikarioId,
            TomeCardCount = owner.TomeCards?.Count(card => card.HostZone == FieldZone.Attached) ?? 0,
            SlotWidth = W, Board = true, RaisedPile = canvas.TableView, ShieldCount = owner.GalacticShieldCharges?.Length ?? 0 };
        attached.Pressed += actions.Attached; Put(attached, 306 + W + 10, bottom, W, H);
        if (owner.EquipmentCovers is { Length: > 0 } covers)
        {
            // Gold lies over the equipment it protects, leaving a strip of that card in sight.
            var cover = new ZoneCardButton { Name = $"EquipmentCoverZone{index}", Caption = "ORO", SlotWidth = W,
                Card = CardCatalog.Get(covers[^1]), Accent = BoardArt.Gold, StatusText = covers.Length > 1 ? $"×{covers.Length}" : "",
                StackCopies = canvas.TableView ? covers.Length : 0 };
            cover.Pressed += actions.EquipmentCovers; Put(cover, 306 + 42, bottom, W, H);
        }

        // Skills: 500 and 1000 PR above, 0 and 300 PR below.
        var skills = new (int Slot, float X, float Y)[] { (2, 596, 76), (3, 596 + H + 10, 76), (0, 596, 76 + W + 10), (1, 596 + H + 10, 76 + W + 10) };
        var nextLesson = NextLessonSlot(owner, !enemy && actions.CanLearn);
        foreach (var (slot, x, y) in skills)
        {
            var learnedIndex = owner.SkillIndexAtPosition(slot);
            var learned = learnedIndex >= 0;
            var skill = new SkillTile { Name = $"Skill{index}_{slot}", Skill = learned ? CardCatalog.Get(owner.Skills[learnedIndex]) : null, NextLesson = slot == nextLesson,
                Slot = slot, SkillIndex = learnedIndex, Counters = learnedIndex >= 0 && learnedIndex < owner.Counters.Length ? owner.Counters[learnedIndex] : 0,
                Chosen = !enemy && learned && selectedSkill == learnedIndex, Accent = accent, Disabled = enemy && !learned,
                PresentationScale = H / SkillTile.CardSize.X };
            skill.Pressed += () => { if (learned) actions.Skill(learnedIndex); };
            skill.GuiInput += input =>
            {
                if (learned && input is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }) actions.InspectSkill(learnedIndex);
            };
            Put(skill, x, y, H, W);
        }

        var memory = new MemoryZoneButton { Name = $"MemoryButton{index}", Cards = owner.Memory, Accent = accent, Board = true,
            RaisedPile = canvas.TableView, RelikarioId = owner.RelikarioId };
        memory.Pressed += () => actions.Memory(false); Put(memory, 996, top, W, H);
        var corrupted = new MemoryZoneButton { Name = $"CorruptedMemoryButton{index}", CorruptedCount = owner.CorruptedCount,
            Corrupted = true, Board = true, RaisedPile = canvas.TableView, Sleeve = owner.Sleeve };
        corrupted.Pressed += () => actions.Memory(true); Put(corrupted, 996, bottom, W, H);
        // Center each deck vertically in the outer column, just beyond the opposing Bonus/Área frame.
        var deckRow = canvas.TableView ? (top + bottom) / 2 : bottom;
        var deckInset = canvas.TableView ? 10 : 0;
        Put(new DeckStack { Name = $"DeckStack{index}", Amount = owner.DeckCount, Accent = accent, Board = true,
            RaisedPile = canvas.TableView, Sleeve = owner.Sleeve }, BoardCanvas.DesignWidth - W - deckInset, deckRow, W, H);
    }

    /// <summary>
    /// The bar over the divider: turn, the four stages and, for whoever may advance, the button.
    /// Its colour is the colour of the duelist whose turn it is.
    /// </summary>
    public static (Control Lane, HBoxContainer Row) PhaseBar(BoardCanvas canvas, Phase stage, string detail, Color accent)
    {
        var lane = new CenterContainer { Name = "CombatCenter", MouseFilter = Control.MouseFilterEnum.Ignore };
        canvas.PlaceTableHud(lane, new Rect2(306, BoardCanvas.Seam - 30, 828, 60));
        var panel = new PanelContainer { Name = "StageIndicator", MouseFilter = Control.MouseFilterEnum.Ignore };
        var frame = RelickVisuals.Glass(RelickVisuals.Playmat.Raised.Lerp(accent, 0.14f), RelickVisuals.Playmat.Surface,
            BoardArt.Tint(accent, 0.9f), 10, 8, BoardArt.Tint(accent, 0.30f));
        frame.ContentMarginLeft = frame.ContentMarginRight = 18; frame.ContentMarginTop = frame.ContentMarginBottom = 4;
        panel.AddThemeStyleboxOverride("panel", frame);
        lane.AddChild(panel);
        var row = new HBoxContainer { Name = "TurnDock", CustomMinimumSize = new(0, 48), MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 8); panel.AddChild(row);
        static Label Still(Label label)
        {
            label.MouseFilter = Control.MouseFilterEnum.Ignore; label.VerticalAlignment = VerticalAlignment.Center;
            label.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter; return label;
        }
        // Every cell keeps its width from stage to stage, so the advance button never moves.
        // The status cell says in words whose turn or decision it is; colour only repeats it.
        var status = new HBoxContainer { Name = "StageStatusCell", CustomMinimumSize = new(StatusCellWidth, 0),
            MouseFilter = Control.MouseFilterEnum.Pass, TooltipText = detail };
        status.AddThemeConstantOverride("separation", 7); row.AddChild(status);
        status.AddChild(new IconView { Name = "StageStatusIcon", Icon = GameIcon.Bolt, Color = accent,
            CustomMinimumSize = new(20, 20), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
        var lines = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        lines.AddThemeConstantOverride("separation", -2); status.AddChild(lines);
        var headline = Still(Heading("", 15, accent));
        headline.Name = "StageStatus"; headline.ClipText = true; lines.AddChild(headline);
        var context = Still(Text(detail, 12, BoardArt.Ink));
        context.Name = "StageDetail"; context.ClipText = true; context.TooltipText = detail;
        lines.AddChild(context);
        var stages = new[] { Phase.Draw, Phase.Combat, Phase.Actions, Phase.Final };
        var widths = new[] { 66f, 112, 116, 70 };
        var current = Array.IndexOf(stages, stage);
        if (current < 0)
        {
            var only = Still(Heading(StageAppearance.For(stage).Title, 16, accent));
            only.Name = "CurrentStageTitle"; only.CustomMinimumSize = new(widths.Sum() + 8 * (widths.Length - 1), 0);
            only.HorizontalAlignment = HorizontalAlignment.Center;
            row.AddChild(only);
            return (lane, row);
        }
        for (var i = 0; i < stages.Length; i++)
        {
            var title = StageAppearance.For(stages[i]).Title;
            var cell = new HBoxContainer { CustomMinimumSize = new(widths[i], 0), Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
            cell.AddThemeConstantOverride("separation", 5); row.AddChild(cell);
            if (i < current) cell.AddChild(Still(Text(title + " ✓", 12, BoardArt.Tint(accent, 0.75f))));
            else if (i > current) cell.AddChild(Still(Text(title, 12, BoardArt.Tint(BoardArt.Muted, 0.6f))));
            else
            {
                var now = Still(Heading(title, 16, accent)); now.Name = "CurrentStageTitle"; cell.AddChild(now);
            }
        }
        return (lane, row);
    }

    /// <summary>Width of the desktop status cell; the bar must still fit its 828-unit lane.</summary>
    internal const float StatusCellWidth = 180;

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
            // The inspector can grow below the board without resizing the player panels. When the
            // window is short the board scrolls instead, so the hand and its decisions stay on screen;
            // OnlineArena sizes this scroll to whatever height the rest of the screen leaves.
            var scroll = new ScrollContainer { Name = "ArenaScroll", SizeFlagsVertical = Control.SizeFlags.Fill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            root.AddChild(scroll);
            arena = new HBoxContainer { Name = "DuelArena", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.Fill };
            scroll.AddChild(arena);
        }
        arena.AddThemeConstantOverride("separation", compact ? 8 : 12);
        return arena;
    }

    public static void Player(Node arena, DuelPlayerView owner, int index, bool enemy, bool active,
        int selectedSkill, RelikarioPortrait portrait, LifeMeter life, ActionCrystalPool crystals, ArenaPlayerActions actions,
        bool compact, bool narrow = false)
    {
        var accent = enemy ? Coral : Mint;
        var panel = Panel(arena, RelickVisuals.Playmat.Surface.ToHtml(), RelickVisuals.Playmat.Frame);
        panel.AddThemeStyleboxOverride("panel", RelickVisuals.Field(accent, active));
        panel.Name = enemy ? "EnemyRelikarioPanel" : "OwnRelikarioPanel";
        panel.CustomMinimumSize = new(compact ? 0 : 320, 0);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 6); panel.AddChild(column);
        var header = new HBoxContainer(); column.AddChild(header);
        var names = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        names.AddThemeConstantOverride("separation", 0);
        var ownerName = Text(owner.Name, 9, accent);
        ownerName.Name = $"PlayerName{index}"; ownerName.ClipText = compact; ownerName.TooltipText = owner.Name; names.AddChild(ownerName);
        OpensProfile(ownerName, owner.Name, actions.Profile);
        var name = Text($"{RelikarioCatalog.NameOf(owner.RelikarioId)} · Lv {owner.Level}", compact ? 15 : 18);
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
        var resources = new ResourceBadge { Name = $"ResourceBadge{index}", Amount = owner.Pr };
        if (!enemy && actions.Relikia is { } relikia) resources.Activated = () => relikia(resources);
        stats.AddChild(resources);
        if (!enemy)
        {
            var evolve = Button(stats, owner.Level == 1 ? "Lv 2 ↑" : "Lv 2 ✓", actions.Evolve, !actions.CanEvolve);
            evolve.Name = "EvolveRelikario"; evolve.AddThemeFontSizeOverride("font_size", 12); evolve.TooltipText = actions.EvolutionHint;
            if (actions.CanEvolve && owner.Level == 1) InviteEvolution(evolve);
        }
        var meters = new HBoxContainer();
        meters.AddThemeConstantOverride("separation", 10);
        life.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        life.ReachedZero = owner.HasReachedZero;
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
        zones.AddChild(new DeckStack { Name = $"DeckStack{index}", Amount = owner.DeckCount, Accent = accent, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter, Sleeve = owner.Sleeve });
        var memories = new HBoxContainer(); memories.AddThemeConstantOverride("separation", 6);
        var memory = new MemoryZoneButton { Name = $"MemoryButton{index}", Cards = owner.Memory, Accent = accent, RelikarioId = owner.RelikarioId };
        memory.Pressed += () => actions.Memory(false); memories.AddChild(memory);
        var corrupted = new MemoryZoneButton { Name = $"CorruptedMemoryButton{index}", CorruptedCount = owner.CorruptedCount, Corrupted = true, Sleeve = owner.Sleeve };
        corrupted.Pressed += () => actions.Memory(true); memories.AddChild(corrupted);
        if (!narrow) zones.AddChild(memories);
        var permanent = new VBoxContainer { CustomMinimumSize = new(92, 0),
            SizeFlagsVertical = compact ? Control.SizeFlags.ShrinkCenter : Control.SizeFlags.Fill };
        permanent.AddThemeConstantOverride("separation", 6);
        var attached = new AttachmentStack { Name = $"AttachmentStack{index}", Cards = owner.Attached, RelikarioId = owner.RelikarioId,
            TomeCardCount = owner.TomeCards?.Count(card => card.HostZone == FieldZone.Attached) ?? 0,
            ShieldCount = owner.GalacticShieldCharges?.Length ?? 0 };
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
        var equipment = new ZoneCardButton { Name = $"EquipmentZone{index}", Caption = "EQUIPO", StatusText = equipmentStatus,
            Card = owner.Equipment == null ? null : CardCatalog.Get(owner.Equipment), Accent = new("81c8ef"),
            Compact = compact, LargePreview = true, SizeFlagsVertical = compact ? Control.SizeFlags.Fill : Control.SizeFlags.ExpandFill };
        equipment.Pressed += actions.Equipment; permanent.AddChild(equipment);
        if (LightEquipmentZone(owner, index, actions.LightEquipment, compact) is { } lights)
            permanent.AddChild(lights);
        portrait.SizeFlagsVertical = narrow ? Control.SizeFlags.ShrinkBegin : Control.SizeFlags.ExpandFill;
        portrait.CustomMinimumSize = new(0, portrait.PortraitHeight);
        body.AddChild(enemy ? zones : permanent); body.AddChild(portrait); body.AddChild(enemy ? permanent : zones); column.AddChild(body);
        if (narrow) column.AddChild(memories);
        var skills = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        skills.AddThemeConstantOverride("h_separation", 6); skills.AddThemeConstantOverride("v_separation", 6);
        var nextLesson = NextLessonSlot(owner, !enemy && actions.CanLearn);
        foreach (var slot in new[] { 2, 3, 0, 1 })
        {
            var learnedIndex = owner.SkillIndexAtPosition(slot);
            var learned = learnedIndex >= 0;
            var skill = new SkillTile { Name = $"Skill{index}_{slot}", Skill = learned ? CardCatalog.Get(owner.Skills[learnedIndex]) : null, NextLesson = slot == nextLesson,
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

    private static ZoneCardButton? LightEquipmentZone(DuelPlayerView owner, int index, Action inspect, bool compact, float width = 0, bool raisedPile = false)
    {
        if (owner.LightEquipment is not { Length: > 0 } cards) return null;
        var zone = new ZoneCardButton
        {
            Name = $"LightEquipmentZone{index}", Caption = "EQUIPOS LIGEROS", Card = CardCatalog.Get(cards[^1]),
            StatusText = cards.Length == 1 ? "LIGERO" : $"×{cards.Length}", Accent = new("7cc8ff"),
            SlotWidth = width, Compact = compact, StackCopies = raisedPile ? cards.Length : 0
        };
        zone.Pressed += inspect;
        return zone;
    }

    /// <summary>Skills are taught in the first free space, in cost order; -1 when teaching is not offered.</summary>
    internal static int NextLessonSlot(DuelPlayerView owner, bool canLearn) =>
        canLearn ? Enumerable.Range(0, GameEngine.MaxSkills).FirstOrDefault(slot => owner.SkillIndexAtPosition(slot) < 0, -1) : -1;

    public static PanelContainer Combat(Node arena, bool preparing, bool over, string message,
        DuelPlayerView[] players, Action<VBoxContainer> opponentHand, Action<int> bonusPressed, Action<int> areaPressed,
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
                Compact = compact, RelikarioId = owner.RelikarioId,
                ExchangeCards = owner.Exchange.Select(CardCatalog.Get).ToArray(), Preparing = preparing,
                BonusEffectUsed = owner.BonusEffectUsed, ExchangeEffectsUsed = owner.ExchangeEffectsUsed ?? Array.Empty<int>(),
                ShieldCharges = owner.MovedShieldCharges,
                BonusCard = owner.Bonus == null ? null : CardCatalog.Get(owner.Bonus), Accent = index == 0 ? Mint : Coral,
                AreaCard = owner.Area == null ? null : CardCatalog.Get(owner.Area), AreaSide = owner.AreaSide };
            track.BonusInspected += () => bonusPressed(index); track.AreaInspected += () => areaPressed(index);
            track.ExchangeInspected += slot => exchangePressed(index, slot); column.AddChild(track);
        }
        return panel;
    }

    public static ScrollContainer InitialSkills(Node parent, IReadOnlyList<string> skills, string? chosen, Action<string> selected, bool shortScreen,
        int railColumns = 0, bool desktop = false)
    {
        // The cards speak for their own number; the heading only names the decision.
        var heading = Text(railColumns > 0 ? "HABILIDAD INICIAL" : "ELIGE TU HABILIDAD INICIAL", shortScreen ? 14 : desktop ? 30 : 20, BoardArt.Gold);
        heading.Name = "InitialSkillHeading";
        heading.AddThemeFontOverride("font", RelickVisuals.Display);
        heading.HorizontalAlignment = HorizontalAlignment.Center;
        // On the board a wrapped heading would size the fixed panel from its zero-width first frame, and it never shrinks back.
        if (!desktop) heading.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        parent.AddChild(heading);
        var scroll = CardGallery("InitialSkillScroll", shortScreen, railColumns);
        Container row;
        if (desktop)
        {
            // Over the board there is room for three large landscape cards per row; more wrap
            // into a fixed grid, whose size is known on the first frame, and the panel scrolls.
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
            var grid = new GridContainer { Columns = Math.Clamp(skills.Count, 1, 3) };
            grid.AddThemeConstantOverride("h_separation", 24); grid.AddThemeConstantOverride("v_separation", 18);
            row = grid;
            var middle = new CenterContainer { Name = "InitialSkillMiddle",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            middle.AddChild(row); scroll.AddChild(middle);
        }
        else { row = GalleryContents(railColumns, 12); scroll.AddChild(row); }
        parent.AddChild(scroll);
        var scale = shortScreen ? 0.6f : desktop ? 2f : 1.3f;
        for (var i = 0; i < skills.Count; i++)
        {
            var id = skills[i];
            var card = new HandCard { Name = "InitialSkillCard" + i, Card = CardCatalog.Get(id), Chosen = id == chosen,
                PresentationScale = scale, Landscape = true };
            card.Pressed += () => selected(id); row.AddChild(card);
        }
        return scroll;
    }

    public static ScrollContainer Hand(Node parent, IReadOnlyList<string> hand, Func<int, bool> selected,
        Action<int> pressed, bool preparing, bool shortScreen, string? title = null, int railColumns = 0, bool useFan = true,
        bool desktop = false, IReadOnlyList<HandSection>? sections = null, Func<int, CardOwner?>? ownerOf = null,
        int viewerSeat = -1, int stackColumns = 0, string? relikario = null, int fittedColumns = 0)
    {
        var grouped = sections is { Count: > 0 };
        var fitted = fittedColumns > 0;
        var fanned = useFan && !grouped && railColumns == 0 && hand.Count > 0;
        var raised = desktop && fanned;
        Control? balance = null;
        if (desktop && title == null && !raised)
        {
            // An empty board hand keeps the count beside its note, leaving the full height to the band.
            var row = new HBoxContainer { Name = "HandRow", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            row.AddThemeConstantOverride("separation", 8); parent.AddChild(row);
            parent = row; row.MouseFilter = Control.MouseFilterEnum.Ignore;
            balance = new Control { CustomMinimumSize = new(44, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        }
        // On the board the fan carries its own count beside its last card.
        if (title == null && !raised) HandCounter(parent, hand.Count, enemy: false);
        else if (title != null)
        {
            // Labelled memory blocks carry their own counts; the total would only repeat their sum.
            var heading = grouped ? Text(title, 10) : HandHeading(title, hand.Count, railColumns);
            heading.TooltipText = title;
            if (fitted) heading.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            if (desktop) { heading.AddThemeFontSizeOverride("font_size", 16); heading.AddThemeFontOverride("font", RelickVisuals.Display); heading.AddThemeColorOverride("font_color", BoardArt.Gold); }
            parent.AddChild(heading);
        }
        var scroll = CardGallery("HandScroll", shortScreen, railColumns);
        scroll.FollowFocus = fanned;
        if (fitted)
        {
            scroll.CustomMinimumSize = Vector2.Zero;
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
            scroll.FollowFocus = true;
        }
        Control cards;
        if (grouped)
        {
            cards = SectionedContents(hand, sections!, selected, pressed, shortScreen, railColumns, viewerSeat,
                fitted ? fittedColumns : stackColumns, fitted ? DeckChoiceCardScale(desktop, shortScreen) : null);
            // The section heading adds one text line above the cards.
            if (!fitted) scroll.CustomMinimumSize = new(scroll.CustomMinimumSize.X, (shortScreen ? 120 : 196) + 16);
        }
        else
        {
            cards = fanned ? new HandFan { Headroom = raised ? DesktopHandHeadroom : 4, MaximumAngle = raised ? 3 : 8, Spread = raised }
                : GalleryContents(fitted ? fittedColumns : railColumns, DeckChoiceGap);
            for (var i = 0; i < hand.Count; i++)
            {
                var slot = i;
                var card = new HandCard
                {
                    Name = $"HandCard{i}", Card = CardCatalog.Get(hand[i]), Chosen = selected(i), Mulligan = preparing,
                    InFan = fanned, Lift = raised ? DesktopHandLift : 9, OriginalOwner = ownerOf?.Invoke(i), RelikarioId = relikario,
                    ViewerSeat = viewerSeat >= 0 ? viewerSeat : null,
                    PresentationScale = fanned ? raised ? DesktopHandScale : HandFan.CardScale : DeckChoiceCardScale(desktop, shortScreen)
                };
                card.Pressed += () => pressed(slot); cards.AddChild(card);
            }
            if (raised && title == null && cards is HandFan fan) fan.Counter = HandCounter(fan, hand.Count, enemy: false);
            if (fitted)
            {
                var center = new CenterContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                center.AddChild(cards);
                cards = center;
            }
        }
        if (hand.Count == 0 && railColumns == 0)
        {
            var hint = preparing ? "Sincronizando tu mano inicial…" : "Robarás 3 cartas al empezar tu turno.";
            var empty = Text(hint, 14, BoardArt.Muted);
            // The lowered hand only shows a band at the bottom edge; the note sits inside it.
            if (desktop && title == null) { empty.CustomMinimumSize = new(0, DesktopHandHeadroom + 44); empty.VerticalAlignment = VerticalAlignment.Bottom; }
            cards.AddChild(empty);
        }
        // The lowered hand overlaps the field's last row: only the cards themselves take the pointer.
        if (raised) scroll.MouseFilter = Control.MouseFilterEnum.Ignore;
        scroll.AddChild(cards); parent.AddChild(scroll);
        if (balance != null) parent.AddChild(balance);
        return scroll;
    }

    private static Control SectionedContents(IReadOnlyList<string> hand, IReadOnlyList<HandSection> sections,
        Func<int, bool> selected, Action<int> pressed, bool shortScreen, int railColumns, int viewerSeat, int stackColumns, float? fittedScale)
    {
        // The desktop chooser stacks one memory over the other, each as a grid of full-size cards.
        var columns = railColumns > 0 ? railColumns : stackColumns;
        Container stack = columns > 0
            ? new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }
            : new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        stack.AddThemeConstantOverride("separation", columns > 0 ? 16 : 24);
        foreach (var section in sections)
        {
            if (section.Indices.Count == 0) continue;
            var suffix = section.FromDeck ? "Deck" : section.Owner.Seat.ToString();
            var block = new VBoxContainer { Name = $"HandSection{suffix}", SizeFlagsHorizontal = columns > 0 ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkBegin };
            block.AddThemeConstantOverride("separation", 4);
            var count = section.Indices.Count;
            var heading = Heading($"{section.Title}  ·  {count} {(count == 1 ? "CARTA" : "CARTAS")}", stackColumns > 0 && railColumns == 0 ? 13 : 10, section.Accent);
            heading.Name = $"HandSectionTitle{suffix}";
            heading.TooltipText = $"{section.Title} · {section.Indices.Count} cartas";
            block.AddChild(heading);
            var rule = new ColorRect { Color = BoardArt.Tint(section.Accent, 0.65f), CustomMinimumSize = new(0, 2), MouseFilter = Control.MouseFilterEnum.Ignore };
            block.AddChild(rule);
            var cards = GalleryContents(columns, DeckChoiceGap);
            foreach (var i in section.Indices)
            {
                var slot = i;
                var card = new HandCard
                {
                    Name = $"HandCard{i}", Card = CardCatalog.Get(hand[i]), Chosen = selected(i), InFan = false, Lift = 9,
                    OriginalOwner = section.Owner, ViewerSeat = viewerSeat >= 0 ? viewerSeat : null, ShowOwnerBadge = false,
                    PresentationScale = fittedScale ?? (shortScreen ? 0.6f : railColumns > 0 ? 0.9f : DesktopHandScale)
                };
                card.Pressed += () => pressed(slot); cards.AddChild(card);
            }
            block.AddChild(cards); stack.AddChild(block);
        }
        return stack;
    }

    // Hand cards share the board's card footprint. At rest only the effect box is below the
    // screen edge; under the pointer a card rises until it shows whole.
    internal const float DesktopHandScale = BoardCanvas.CardWidth / 126f;
    internal const int DeckChoiceGap = 10;
    internal static float DeckChoiceCardScale(bool desktop, bool shortScreen) => shortScreen ? 0.6f : desktop ? DesktopHandScale : 1;
    internal const float DesktopHandVisible = 138;
    internal const float DesktopHandLift = 58;
    internal const float DesktopHandHeadroom = 88;

    public static Control HandCounter(Node parent, int count, bool enemy)
    {
        var description = $"{(enemy ? "Mano rival" : "Tu mano")} · {count} {(count == 1 ? "carta" : "cartas")}";
        var row = new HBoxContainer
        {
            Name = enemy ? "OpponentHandCounter" : "OwnHandCounter",
            SizeFlagsHorizontal = enemy || parent is HBoxContainer ? Control.SizeFlags.ShrinkBegin : Control.SizeFlags.ShrinkCenter,
            SizeFlagsVertical = parent is HBoxContainer ? Control.SizeFlags.ShrinkCenter : Control.SizeFlags.Fill,
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
        return row;
    }

    private static Label HandHeading(string title, int count, int railColumns)
    {
        var amount = $"{count} {(count == 1 ? "CARTA" : "CARTAS")}";
        var caption = railColumns > 0 ? title.Split(" · ")[0] + $"\n{amount}" : $"{title}  ·  {amount}";
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

    public static void StageIndicator(Node parent, Phase stage, string detail, bool shortScreen, bool wrap = false)
    {
        var style = StageAppearance.For(stage);
        var panel = new PanelContainer { Name = "StageIndicator", MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        var frame = RelickVisuals.Glass(RelickVisuals.Playmat.Raised.Lerp(style.Accent, 0.16f), RelickVisuals.Playmat.Surface,
            BoardArt.Tint(style.Accent, 0.75f), 8, 6, BoardArt.Tint(style.Accent, 0.22f));
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
        title.AddThemeFontOverride("font", RelickVisuals.Display);
        title.Name = "CurrentStageTitle";
        labels.AddChild(title);
        var context = Text(detail, shortScreen ? 11 : 10, BoardArt.Ink);
        context.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        if (shortScreen || wrap) context.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        if (wrap) { title.AddThemeFontSizeOverride("font_size", 20); context.AddThemeFontSizeOverride("font_size", 12); context.AddThemeColorOverride("font_color", BoardArt.Muted); }
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
