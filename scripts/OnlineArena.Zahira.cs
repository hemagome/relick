using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    private static string ZahiraNodeName(int relative, int slot) => slot == 0 ? $"Zahira{relative}" : $"Zahira{relative}Copy{slot + 1}";

    private void PlaceZahiras(DuelSnapshot snapshot)
    {
        var traps = new List<(int Owner, int Slot, FieldTarget Host)>();
        for (var owner = 0; owner < snapshot.Players.Length; owner++)
            for (var slot = 0; slot < PlayerState.ZahiraSlots; slot++)
                if (snapshot.Players[owner].Zahira(slot) is { } host) traps.Add((owner, slot, host));
        if (traps.Count == 0) return;

        var art = BoardArt.LoadCardArtwork(CardCatalog.ZahiraFlair);
        Control? Anchor(FieldTarget host)
        {
            var relative = host.Owner == snapshot.MySeat ? 0 : 1;
            return host.Zone switch
            {
                FieldZone.Relikario => ZoneNode(relative, CardZone.Attached, -1),
                FieldZone.Skill => ZoneNode(relative, CardZone.Skill, host.Index),
                _ => FieldActionAnchor((host.Owner, host.Zone, host.Index))
            };
        }

        // A trap on another Zahira uses that trap's face as its anchor.
        foreach (var layer in traps.GroupBy(trap => trap.Host.Zone == FieldZone.Zahira).OrderBy(layer => layer.Key))
        foreach (var stack in layer.GroupBy(trap => Anchor(trap.Host)))
        {
            if (stack.Key is not { } anchor) continue;
            var cards = stack.ToArray();
            for (var i = 0; i < cards.Length; i++)
            {
                var (owner, slot, host) = cards[i];
                var hostPlayer = snapshot.Players[host.Owner];
                var hostName = host.Zone == FieldZone.Relikario ? RelikarioCatalog.NameOf(hostPlayer.RelikarioId) : CardCatalog.Get(host.CardId).Name;
                var caption = $"Zahira Flair de {snapshot.Players[owner].Name} sobre {hostName} · {hostPlayer.Name}";
                TextureRect? buriedHost = null;
                if (host.Zone == FieldZone.Attached && anchor is AttachmentStack attached && attached.TopCardId != host.CardId)
                {
                    // Reveal the face actually being covered if it is under the top card of the stack.
                    buriedHost = new TextureRect
                    {
                        Texture = BoardArt.LoadCardArtwork(CardCatalog.Get(host.CardId), hostPlayer.RelikarioId),
                        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                        MouseFilter = MouseFilterEnum.Ignore
                    };
                    anchor.AddChild(buriedHost);
                }
                var trap = new TextureButton
                {
                    Name = ZahiraNodeName(owner == snapshot.MySeat ? 0 : 1, slot),
                    TextureNormal = art, IgnoreTextureSize = true, StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
                    TooltipText = caption, AccessibilityName = caption, MouseDefaultCursorShape = CursorShape.PointingHand
                };
                trap.Pressed += () => SelectField(owner, FieldZone.Zahira, GameEngine.ZahiraIndex(slot));
                anchor.AddChild(trap);
                var standalone = host.Zone == FieldZone.Relikario && hostPlayer.Attached.Length == 0 &&
                    hostPlayer.GalacticShieldCharges is not { Length: > 0 } && cards.Length == 1;
                var behindCover = Desktop && host.Zone == FieldZone.Equipment && hostPlayer.EquipmentCovers is { Length: > 0 };
                var position = i;
                void Arrange()
                {
                    var bounds = anchor switch
                    {
                        AttachmentStack pile when pile.Cards.Count > 0 => pile.CardRect,
                        ZoneCardButton zone => zone.CardRect,
                        _ => new Rect2(Vector2.Zero, anchor.Size).Grow(-4)
                    };
                    if (bounds.Size.X <= 0 || bounds.Size.Y <= 0) return;
                    if (behindCover) bounds.Size = new Vector2(Math.Min(38, bounds.Size.X), bounds.Size.Y);
                    if (cards.Length > 1)
                    {
                        var widthPerCard = bounds.Size.X / cards.Length;
                        bounds.Position += new Vector2(widthPerCard * position, 0);
                        bounds.Size = new Vector2(widthPerCard, bounds.Size.Y);
                    }
                    var width = behindCover || cards.Length > 1 ? bounds.Size.X : bounds.Size.X * .58f;
                    var area = standalone ? bounds : new Rect2(Vector2.Zero, new Vector2(width, bounds.Size.Y * .82f));
                    var face = art == null ? area : BoardArt.FitArtwork(art, area);
                    var alignment = standalone ? .5f : 1;
                    trap.Position = bounds.Position + (bounds.Size - face.Size) * new Vector2(alignment, .5f);
                    trap.Size = face.Size;
                    if (buriedHost != null)
                    {
                        buriedHost.Position = trap.Position - new Vector2(
                            Math.Min(trap.Position.X - bounds.Position.X, face.Size.X * .22f), face.Size.Y * .1f);
                        buriedHost.Size = face.Size;
                    }
                }
                anchor.Resized += Arrange;
                Arrange();
            }
        }
    }
}
