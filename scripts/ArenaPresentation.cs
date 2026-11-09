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
            arena = new HBoxContainer { Name = "DuelArena", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            root.AddChild(arena);
        }
        arena.AddThemeConstantOverride("separation", compact ? 8 : 12);
        return arena;
    }

    public static void Player(Node arena, DuelPlayerView owner, int index, bool enemy, bool active,
        int selectedSkill, YunoPortrait portrait, LifeMeter life, ArenaPlayerActions actions, bool compact)
    {
        var accent = enemy ? Coral : Mint;
        var panel = Panel(arena, RelickVisuals.Playmat.Surface.ToHtml(), RelickVisuals.Playmat.Frame);
        panel.Name = enemy ? "EnemyRelikarioPanel" : "OwnRelikarioPanel";
        panel.CustomMinimumSize = new(compact ? 0 : 320, 0);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 6); panel.AddChild(column);
        var header = new HBoxContainer(); column.AddChild(header);
        var names = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        names.AddThemeConstantOverride("separation", 0);
        var ownerName = Text((active ? "» " : "") + (enemy ? "ADVERSARIO" : "TU RELIKARIO") + " · " + owner.Name, 9, accent);
        ownerName.Name = $"PlayerName{index}"; ownerName.ClipText = compact; ownerName.TooltipText = owner.Name; names.AddChild(ownerName);
        var name = Text($"{RelikarioCatalog.Yuno.Name} · Lv {owner.Level}", compact ? 15 : 18);
        name.ClipText = compact; name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        name.Name = $"RelikarioName{index}"; name.TooltipText = $"{owner.Name} · Vida máxima: {owner.MaxHealth}";
        names.AddChild(name); header.AddChild(names);
        var dexterity = Text($"DEST {owner.Dexterity}", compact ? 11 : 12, accent);
        dexterity.Name = $"Dexterity{index}";
        dexterity.TooltipText = "Destreza: gasta una ficha para sumar +1 a uno de tus dados. No se puede usar con 1 ni para alcanzar 6.";
        header.AddChild(dexterity);
        if (owner.StarEffects > 0)
        {
            var star = Text($"★ ×{1 << owner.StarEffects}", compact ? 11 : 12, BoardArt.Gold);
            star.Name = $"StarEffects{index}";
            star.TooltipText = "Estrella del Fontanero: no recibe daño y multiplica su daño hasta el final de este turno.";
            header.AddChild(star);
        }
        header.AddChild(new ResourceBadge { Amount = owner.Pr });
        if (!enemy)
        {
            var evolve = Button(header, owner.Level == 1 ? "Lv 2 ↑" : "Lv 2 ✓", actions.Evolve, !actions.CanEvolve);
            evolve.Name = "EvolveRelikario"; evolve.AddThemeFontSizeOverride("font_size", 12); evolve.TooltipText = actions.EvolutionHint;
        }
        life.SetHealth(owner.Health, owner.MaxHealth); column.AddChild(life);
        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 6);
        var zones = new VBoxContainer { CustomMinimumSize = new(70, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        zones.AddThemeConstantOverride("separation", 6);
        zones.AddChild(new DeckStack { Name = $"DeckStack{index}", Amount = owner.DeckCount, Accent = accent, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter });
        var memories = new HBoxContainer(); memories.AddThemeConstantOverride("separation", 6);
        var memory = new MemoryZoneButton { Name = $"MemoryButton{index}", Amount = owner.Memory.Length, Accent = accent,
            TopCard = owner.Memory.Length == 0 ? null : CardCatalog.Get(owner.Memory[^1]) };
        memory.Pressed += () => actions.Memory(false); memories.AddChild(memory);
        var corrupted = new MemoryZoneButton { Name = $"CorruptedMemoryButton{index}", Amount = owner.CorruptedCount, Corrupted = true };
        corrupted.Pressed += () => actions.Memory(true); memories.AddChild(corrupted); zones.AddChild(memories);
        var permanent = new VBoxContainer { CustomMinimumSize = new(92, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        permanent.AddThemeConstantOverride("separation", 6);
        var attached = new AttachmentStack { Name = $"AttachmentStack{index}", Amount = owner.Attached.Length };
        attached.Pressed += actions.Attached; permanent.AddChild(attached);
        if (owner.EquipmentCovers is { Length: > 0 } covers)
        {
            var cover = new ZoneCardButton { Name = $"EquipmentCoverZone{index}", Caption = $"ORO ×{covers.Length}",
                Card = CardCatalog.Get(covers[^1]), Accent = BoardArt.Gold,
                TooltipText = "Tótem de Oro: se destruye en lugar de un Tótem de Salvación equipado." };
            cover.Pressed += actions.EquipmentCovers; permanent.AddChild(cover);
        }
        var equipment = new ZoneCardButton { Name = $"EquipmentZone{index}", Caption = owner.Equipment == null ? "EQUIPO 0/1" : owner.EquipmentStackedCopies > 0 ? $"EQUIPO ×{owner.EquipmentStackedCopies + 1}" : owner.Equipment == CardCatalog.Sniper.Id ? $"EQUIPO · {owner.EquipmentCounters}/3" : owner.EquipmentDie > 0 ? $"EQUIPO · D6: {owner.EquipmentDie}" : "EQUIPO 1/1",
            Card = owner.Equipment == null ? null : CardCatalog.Get(owner.Equipment), EmptyCaption = "Sin equipo", Accent = new("81c8ef") };
        equipment.Pressed += actions.Equipment; permanent.AddChild(equipment);
        body.AddChild(enemy ? zones : permanent); body.AddChild(portrait); body.AddChild(enemy ? permanent : zones); column.AddChild(body);
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
        Action<int, int> exchangePressed, bool compact)
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

    public static ScrollContainer InitialSkills(Node parent, IReadOnlyList<string> skills, string? chosen, Action<string> selected, bool shortScreen)
    {
        parent.AddChild(Text("ELIGE TU HABILIDAD INICIAL", 12, BoardArt.Gold));
        var scroll = new ScrollContainer { Name = "InitialSkillScroll", CustomMinimumSize = new(0, shortScreen ? 120 : 196),
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 12); scroll.AddChild(row); parent.AddChild(scroll);
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
        Action<int> pressed, bool preparing, bool shortScreen, string title = "TU MANO")
    {
        parent.AddChild(Text($"{title}  ·  {hand.Count} CARTAS", 10));
        var scroll = new ScrollContainer { Name = "HandScroll", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new(0, shortScreen ? 120 : 196), VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, ScrollDeadzone = 12 };
        var cards = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        cards.AddThemeConstantOverride("separation", 10);
        for (var i = 0; i < hand.Count; i++)
        {
            var slot = i;
            var card = new HandCard { Name = $"HandCard{i}", Card = CardCatalog.Get(hand[i]), Chosen = selected(i), Mulligan = preparing,
                PresentationScale = shortScreen ? 0.6f : 1 };
            card.Pressed += () => pressed(slot); cards.AddChild(card);
        }
        if (hand.Count == 0) cards.AddChild(Text(preparing ? "Sincronizando tu mano inicial…" : "Mano vacía · robarás 3 cartas al empezar tu turno.", 14, BoardArt.Muted));
        scroll.AddChild(cards); parent.AddChild(scroll); return scroll;
    }

    public static void Scoreline(Node parent, DuelPlayerView[] players)
    {
        var text = Text($"TÚ {players[0].Health}/{players[0].MaxHealth} · {players[0].Pr} PR     RIVAL {players[1].Health}/{players[1].MaxHealth} · {players[1].Pr} PR", 12, BoardArt.Muted);
        text.Name = "DuelScoreline"; text.AutowrapMode = TextServer.AutowrapMode.WordSmart; parent.AddChild(text);
    }

    public static void Phases(Node parent, Phase stage, string caption, int turn, bool ownTurn, bool compact)
    {
        if (compact)
        {
            var label = Text(stage == Phase.Preparation ? caption : $"TURNO {turn:00} · {caption}", 12, ownTurn ? Mint : Coral);
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart; parent.AddChild(label); return;
        }
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8); parent.AddChild(row);
        var pill = new PanelContainer(); pill.AddThemeStyleboxOverride("panel", Box("1b2336", ownTurn ? Mint : Coral, 8));
        pill.AddChild(Text(stage == Phase.Preparation ? "PREPARACIÓN" : stage == Phase.Finished ? "DUELO TERMINADO" : ownTurn ? "TU TURNO" : "TURNO DEL RIVAL", 11, ownTurn ? Mint : Coral)); row.AddChild(pill);
        if (stage != Phase.Preparation) row.AddChild(Text($"TURNO {turn:00}", 12, BoardArt.Muted));
        var current = stage switch { Phase.Preparation or Phase.Draw => 0, Phase.Combat => 1, Phase.Actions => 2, _ => 3 };
        var phases = new[] { (stage == Phase.Preparation ? "Preparar" : "Robo", GameIcon.Cards), ("Combate", GameIcon.Sword), ("Acciones", GameIcon.Bolt), ("Final", GameIcon.Flag) };
        for (var i = 0; i < phases.Length; i++) row.AddChild(new PhaseBadge { Caption = phases[i].Item1, Icon = phases[i].Item2,
            Current = i == current, Complete = i < current, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
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
