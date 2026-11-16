using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    private readonly HBoxContainer _cardActions = new()
    {
        Name = "CardActions", MouseFilter = MouseFilterEnum.Ignore, ZIndex = 3, Visible = false
    };
    private Control? _cardActionAnchor;
    private bool _choosingAttackSkill;

    private IEnumerable<(DuelOption Option, int Index)> SelectedAttacks()
    {
        if (_selected.Count != 1 || _fieldSelection != null || _skillContext || ChoosingEffectCards || ChoosingRecycling)
            return [];
        var handIndex = _selected.Single();
        return _snapshot!.Options.Select((option, index) => (Option: option, Index: index))
            .Where(entry => entry.Option.Move.Kind == DuelMoveKind.Attack && entry.Option.Move.Index == handIndex);
    }

    private Control? FieldActionAnchor((int Owner, FieldZone Zone, int Index) field)
    {
        var relative = field.Owner == _snapshot!.MySeat ? 0 : 1;
        return field.Zone switch
        {
            FieldZone.Equipment => _root!.FindChild("EquipmentZone" + relative, true, false) as Control,
            FieldZone.EquipmentCover => _root!.FindChild("EquipmentCoverZone" + relative, true, false) as Control,
            FieldZone.Attached => _root!.FindChild("AttachmentStack" + relative, true, false) as Control,
            FieldZone.Bonus when _root!.FindChild("BonusTrack" + relative, true, false) is ExchangeTrack track => track.BonusButton,
            FieldZone.Exchange when _root!.FindChild("BonusTrack" + relative, true, false) is ExchangeTrack track =>
                track.ExchangeButtons.ElementAtOrDefault(field.Index),
            FieldZone.Hand when relative == 1 && field.Index >= 0 =>
                _root!.FindChild("OpponentHandCard" + field.Index, true, false) as Control,
            _ => null
        };
    }

    private static GameIcon CardActionSymbol(DuelMove move) => move.Kind switch
    {
        DuelMoveKind.Attack or DuelMoveKind.Destroy or DuelMoveKind.Choose => GameIcon.Sword,
        DuelMoveKind.Equip or DuelMoveKind.BoxDefense or DuelMoveKind.CodeResponse or DuelMoveKind.ProtectDestruction => GameIcon.Shield,
        DuelMoveKind.Exchange => GameIcon.Exchange,
        DuelMoveKind.Placement when move.Value == (int)CardDestination.Exchange => GameIcon.Exchange,
        DuelMoveKind.Discard or DuelMoveKind.TurnBuffDiscard => GameIcon.Memory,
        DuelMoveKind.Placement or DuelMoveKind.Magnet => GameIcon.Cards,
        DuelMoveKind.Pc or DuelMoveKind.Pendant => GameIcon.Reload,
        _ => GameIcon.Bolt
    };

    private void AddAttackSkillChoice()
    {
        var button = new CardActionButton { Name = "ChooseAttackSkill", Text = "Atacar", Symbol = GameIcon.Sword,
            TooltipText = "Consume 1 Ataque · Elige la habilidad", AccessibilityName = "Atacar: elegir habilidad" };
        button.Style(primary: true, shortScreen: Short);
        button.SetMeta("discards", 0);
        button.Pressed += () =>
        {
            if (Connection.PlayPending || IsAnimating) return;
            _choosingAttackSkill = true;
            BuildMoves();
        };
        _cardActions.AddChild(button);
        _moveButtons.Add(button);
    }

    private void PositionCardActions()
    {
        if (_cardActionAnchor is not { } anchor || !anchor.IsVisibleInTree() || _cardActions.GetChildCount() == 0)
        {
            _cardActions.Hide();
            return;
        }

        // The fan rotates the cards. Keep the controls level and outside the scroll clips.
        var toArena = GetGlobalTransform().AffineInverse();
        Rect2 Bounds(Control control) => (toArena * control.GetGlobalTransform()) * new Rect2(Vector2.Zero, control.Size);
        var area = Bounds(_root!);
        var visible = Bounds(anchor).Intersection(area);
        for (var parent = anchor.GetParentControl(); parent != null && parent != this; parent = parent.GetParentControl())
            if (parent.ClipContents) visible = visible.Intersection(Bounds(parent));
        if (!visible.HasArea())
        {
            _cardActions.Hide();
            return;
        }
        // A raised hand card is drawn above its hit area; its actions follow the card that is seen.
        if (anchor is HandCard { InFan: true } raised)
        {
            var lift = raised.DrawnLift * anchor.GetGlobalTransform().Scale.Y;
            visible = new Rect2(visible.Position - new Vector2(0, lift), visible.Size + new Vector2(0, lift));
        }

        const float gap = 5;
        var size = _cardActions.GetCombinedMinimumSize();
        var x = Mathf.Clamp(visible.GetCenter().X - size.X / 2, area.Position.X, Math.Max(area.Position.X, area.End.X - size.X));
        var above = visible.Position.Y - size.Y - gap;
        var y = above >= area.Position.Y ? above : Math.Min(visible.End.Y + gap, area.End.Y - size.Y);
        _cardActions.Position = new Vector2(x, y).Round();
        if (_cardActions.Size != size) _cardActions.Size = size;
        _cardActions.Show();
    }
}

internal partial class CardActionButton : Button
{
    internal GameIcon Symbol { get; init; }

    internal void Style(bool primary, bool shortScreen)
    {
        var surface = RelickVisuals.Playmat;
        var accent = primary ? RelickVisuals.State.Target : surface.Frame.Lightened(0.2f);
        var fill = primary ? surface.Raised.Lerp(accent, 0.14f) : surface.Raised;
        void Frame(string state, Color background, Color border)
        {
            var box = RelickVisuals.Frame(background, border, 7);
            box.ContentMarginLeft = 31;
            box.ContentMarginRight = 10;
            AddThemeStyleboxOverride(state, box);
        }
        Frame("normal", fill, accent);
        Frame("hover", fill.Lightened(0.12f), RelickVisuals.State.Focus);
        Frame("pressed", fill.Darkened(0.15f), accent);
        Frame("disabled", surface.Surface, surface.Frame);
        Alignment = HorizontalAlignment.Left;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        CustomMinimumSize = new(48, 48);
        AddThemeFontSizeOverride("font_size", shortScreen ? 13 : 15);
    }

    public override void _Draw() => BoardArt.Icon(this, Symbol, new(16, Size.Y / 2), 16,
        GetThemeColor(Disabled ? "font_disabled_color" : "font_color"));
}
