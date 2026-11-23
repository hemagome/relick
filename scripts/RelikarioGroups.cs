using Godot;
using Relick.Core;

namespace Relick;

/// <summary>A card lying under its host. A null card is face down.</summary>
internal sealed record CardGroupMember(string? CardId, bool Inverted = false, int TomeSlot = -1);

/// <summary>A card on the Relikario and the cards it keeps under it, drawn in one shared hue.</summary>
internal sealed record CardGroup(string HostId, FieldZone Zone, int Index, IReadOnlyList<CardGroupMember> Members, Color Hue)
{
    internal bool Linked => Members.Count > 0;
    internal string Heading
    {
        get
        {
            var name = CardCatalog.Get(HostId).Name;
            if (!Linked) return name;
            var hidden = Members.Count(member => member.CardId == null);
            var count = Members.Count == 1 ? "1 carta debajo" : $"{Members.Count} cartas debajo";
            return $"{name} · {count}" + (hidden == Members.Count ? " (boca abajo)" : "");
        }
    }
}

/// <summary>
/// Splits what lies on a Relikario into groups, the way a physical table shows them: each card
/// with the ones it put under it. Only groups with cards under them get a hue, so a colour on the
/// board always means "these belong together".
/// </summary>
internal static class RelikarioGroups
{
    internal static readonly Color[] Hues = [new("f5b84a"), new("5fd3f3"), new("f27ab8"), new("a5e05a"), new("c9a2ff")];

    internal static IReadOnlyList<CardGroup> For(DuelPlayerView player)
    {
        var groups = new List<CardGroup>();
        var hue = 0;
        Color Next(bool linked) => linked ? Hues[hue++ % Hues.Length] : Colors.Transparent;
        for (var i = 0; i < player.Attached.Length; i++)
        {
            var members = player.TomeCards?.Where(card => card.HostZone == FieldZone.Attached && card.HostIndex == i)
                .Select(card => new CardGroupMember(card.CardId, card.Inverted, card.Slot)).ToArray() ?? [];
            groups.Add(new(player.Attached[i], FieldZone.Attached, i, members, Next(members.Length > 0)));
        }
        var shields = player.GalacticShieldCharges ?? [];
        for (var slot = 0; slot < shields.Length; slot++)
        {
            var members = Enumerable.Repeat(new CardGroupMember(null), shields[slot]).ToArray();
            groups.Add(new(CardCatalog.GalacticShield.Id, FieldZone.GalacticShield, slot, members, Next(members.Length > 0)));
        }
        return groups;
    }

    /// <summary>Worth a closer look: several cards, or one keeping others under it.</summary>
    internal static bool WorthPeeking(IReadOnlyList<CardGroup> groups) => groups.Count > 1 || groups.Any(group => group.Linked);

    /// <summary>The group of the attached card shown on top of the pile.</summary>
    internal static CardGroup? Top(IReadOnlyList<CardGroup> groups, string? topCardId) =>
        groups.LastOrDefault(group => group.Zone == FieldZone.Attached && group.HostId == topCardId);
}

/// <summary>
/// A look over what lies on a Relikario while the pointer rests on it: every card side by side,
/// each with the cards under it fanned below and framed in the group's hue. It never takes input.
/// </summary>
internal sealed partial class RelikarioGroupPeek : Control
{
    private const float CardW = 84, CardH = 117, MemberW = 60, MemberH = 84, MemberGap = 6, Link = 18, Gap = 14, Pad = 12, Caption = 18, Title = 24;
    private const int ShownMembers = 4;
    private IReadOnlyList<CardGroup> _groups = [];
    private string? _relikario;
    private string? _sleeve;
    private string _title = "";
    private readonly Dictionary<string, Texture2D?> _art = [];

    internal IReadOnlyList<CardGroup> Groups => _groups;

    public RelikarioGroupPeek()
    {
        Name = "RelikarioGroupPeek";
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        ZIndex = 20;
    }

    internal void Present(IReadOnlyList<CardGroup> groups, string? relikario, string? sleeve, string title, Rect2 anchor, Rect2 bounds)
    {
        _groups = groups; _relikario = relikario; _sleeve = sleeve; _title = title;
        var linked = groups.Any(group => group.Linked);
        Size = new Vector2(Pad * 2 + groups.Sum(ColumnWidth) + (groups.Count - 1) * Gap,
            Pad * 2 + Title + CardH + (linked ? Link + MemberH + Caption : 0));
        // Above the pile when it fits, otherwise below it; always inside the duel.
        var x = Mathf.Clamp(anchor.GetCenter().X - Size.X / 2, bounds.Position.X + 8, Math.Max(bounds.Position.X + 8, bounds.End.X - Size.X - 8));
        var above = anchor.Position.Y - Size.Y - 10;
        var y = above >= bounds.Position.Y + 8 ? above : Math.Min(anchor.End.Y + 10, bounds.End.Y - Size.Y - 8);
        GlobalPosition = new Vector2(x, y);
        Visible = true;
        QueueRedraw();
    }

    internal void Dismiss() => Visible = false;

    private Texture2D? Art(string id)
    {
        if (!_art.TryGetValue(id, out var texture))
            _art[id] = texture = BoardArt.LoadCardArtwork(CardCatalog.Get(id), _relikario);
        return texture;
    }

    public override void _Draw()
    {
        if (_groups.Count == 0) return;
        var panel = new Rect2(Vector2.Zero, Size);
        RelickVisuals.Shadow(this, panel, new Color(0, 0, 0, 0.6f), 14, 6);
        DrawStyleBox(RelickVisuals.Frame(BoardArt.Tint(RelickVisuals.Playmat.Surface, 0.97f), BoardArt.Tint(RelickVisuals.Playmat.Frame.Lightened(.2f), .9f), 10, 2), panel);
        BoardArt.CenterText(this, _title, new Vector2(Size.X / 2, Pad + 9), 11, BoardArt.Muted);
        var back = SleeveArt.Back(_sleeve);
        var left = Pad;
        foreach (var group in _groups)
        {
            var width = ColumnWidth(group);
            var top = Pad + Title;
            var host = new Rect2(left + (width - CardW) / 2, top, CardW, CardH);
            if (group.Linked)
            {
                var shown = Math.Min(ShownMembers, group.Members.Count);
                var row = shown * MemberW + (shown - 1) * MemberGap;
                var membersTop = host.End.Y + Link;
                var column = new Rect2(left - 6, top - 6, width + 12, CardH + Link + MemberH + Caption + 8);
                DrawStyleBox(RelickVisuals.Frame(BoardArt.Tint(group.Hue, 0.10f), BoardArt.Tint(group.Hue, 0.95f), 7, 2), column);
                // A small tree: one line down from the host, then a branch to each card it keeps under it.
                var trunk = new Vector2(host.GetCenter().X, host.End.Y);
                var branchY = host.End.Y + Link / 2;
                DrawLine(trunk, new Vector2(trunk.X, branchY), group.Hue, 3);
                var first = left + (width - row) / 2;
                for (var m = 0; m < shown; m++)
                {
                    var member = group.Members[m];
                    var rect = new Rect2(first + m * (MemberW + MemberGap), membersTop, MemberW, MemberH);
                    var x = rect.GetCenter().X;
                    DrawLine(new Vector2(Math.Min(x, trunk.X), branchY), new Vector2(Math.Max(x, trunk.X), branchY), group.Hue, 3);
                    DrawLine(new Vector2(x, branchY), new Vector2(x, membersTop), group.Hue, 3);
                    DrawCard(member.CardId is { } id ? Art(id) : back, rect, member.Inverted);
                }
                var more = group.Members.Count - shown;
                var caption = (group.Members.Count == 1 ? "1 debajo" : $"{group.Members.Count} debajo") + (more > 0 ? $" (+{more})" : "");
                BoardArt.CenterText(this, caption, new Vector2(left + width / 2, membersTop + MemberH + 11), 10, group.Hue);
            }
            DrawCard(Art(group.HostId), host, false);
            left += width + Gap;
        }
    }

    private static float ColumnWidth(CardGroup group)
    {
        var shown = Math.Min(ShownMembers, group.Members.Count);
        return Math.Max(CardW, shown * MemberW + Math.Max(0, shown - 1) * MemberGap);
    }

    private void DrawCard(Texture2D? texture, Rect2 slot, bool inverted)
    {
        RelickVisuals.Shadow(this, new Rect2(slot.Position + new Vector2(0, 2), slot.Size).Grow(-2), new Color(0, 0, 0, .55f), 6);
        if (texture == null) { DrawRect(slot, BoardArt.Tint(RelickVisuals.Playmat.Surface, 1)); return; }
        var rect = BoardArt.FitArtwork(texture, slot);
        if (!inverted) { DrawTextureRect(texture, rect, false); return; }
        DrawSetTransform(rect.GetCenter(), Mathf.Pi);
        DrawTextureRect(texture, new Rect2(-rect.Size / 2, rect.Size), false);
        DrawSetTransform(Vector2.Zero);
    }
}
