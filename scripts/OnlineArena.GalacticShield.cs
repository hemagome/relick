using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    private static string GalacticShieldNodeName(int relative, int slot) => $"GalacticShield{relative}_{slot}";

    private void PlaceGalacticShields(DuelSnapshot snapshot)
    {
        for (var relative = 0; relative < 2; relative++)
        {
            var owner = relative == 0 ? snapshot.MySeat : 1 - snapshot.MySeat;
            var player = snapshot.Players[owner];
            if (player.GalacticShieldCharges is not { Length: > 0 } charges ||
                ZoneNode(relative, CardZone.Attached, -1) is not AttachmentStack anchor) continue;

            var firstSlot = player.Attached.Length > 0 ? 1 : 0;
            var columns = firstSlot + charges.Length;
            for (var i = 0; i < charges.Length; i++)
            {
                var slot = i;
                var shield = new ZoneCardButton
                {
                    Name = GalacticShieldNodeName(relative, slot), Caption = "SOBRE RELIKARIO",
                    Card = CardCatalog.GalacticShield, Accent = new Color("6ee3bd"), SlotWidth = 1,
                    ShieldCharges = charges[slot],
                    GroupHue = anchor.Groups.FirstOrDefault(group => group.Zone == FieldZone.GalacticShield && group.Index == slot) is { Linked: true } group ? group.Hue : null,
                    StackCopies = anchor.RaisedPile ? charges[slot] + 1 : 0
                };
                shield.Pressed += () => SelectField(owner, FieldZone.GalacticShield, slot);
                anchor.AddChild(shield);
                shield.AnchorLeft = (float)(firstSlot + slot) / columns;
                shield.AnchorRight = (float)(firstSlot + slot + 1) / columns;
                shield.AnchorBottom = 1;
                shield.OffsetLeft = shield.OffsetRight = shield.OffsetTop = shield.OffsetBottom = 0;
            }
        }
    }

    private readonly RelikarioGroupPeek _groupPeek = new();

    /// <summary>
    /// Resting the pointer on what lies on a Relikario lays its groups out side by side, each host
    /// with the cards under it, so which card belongs to which reads without opening anything.
    /// </summary>
    private void BindRelikarioPeeks(DuelSnapshot snapshot)
    {
        _groupPeek.Dismiss();
        if (!Desktop) return;
        for (var relative = 0; relative < 2; relative++)
        {
            var owner = relative == 0 ? snapshot.MySeat : 1 - snapshot.MySeat;
            var player = snapshot.Players[owner];
            if (ZoneNode(relative, CardZone.Attached, -1) is not AttachmentStack anchor ||
                !RelikarioGroups.WorthPeeking(anchor.Groups)) continue;
            var title = $"Sobre {RelikarioCatalog.DisplayNameOf(player.RelikarioId)} · {player.Name}";
            void Show()
            {
                if (IsAnimating || !IsInstanceValid(anchor) || !anchor.IsVisibleInTree()) return;
                _groupPeek.Present(anchor.Groups, player.RelikarioId, player.Sleeve, title, Global(anchor, new Rect2(Vector2.Zero, anchor.Size)), GetGlobalRect());
            }
            foreach (var zone in new Control[] { anchor }.Concat(anchor.GetChildren().OfType<ZoneCardButton>()))
            {
                zone.MouseEntered += Show;
                zone.MouseExited += _groupPeek.Dismiss;
                if (zone is BaseButton button) button.Pressed += _groupPeek.Dismiss;
            }
        }
    }
}
