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
}
