using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    // Flauta de Vidrio: the returned card flies from its own field slot to its owner's hand.
    internal async Task CheckGlassFluteReturnLayout()
    {
        async Task Layout() { for (var frame = 0; frame < 4; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        var flute = CardCatalog.GlassFlute.Id;
        var cases = new (string Name, FieldZone Zone, int Owner, int Slot, string Card)[]
        {
            ("habilidad propia en su segundo espacio", FieldZone.Skill, 0, 1, CardCatalog.Destiny.Id),
            ("canje propio", FieldZone.Exchange, 0, 1, CardCatalog.Pendant.Id),
            ("equipo rival", FieldZone.Equipment, 1, 0, CardCatalog.Pc.Id)
        };
        GetWindow().ContentScaleSize = new Vector2I(1440, 1024);
        foreach (var seat in new[] { 0, 1 })
        foreach (var (name, zone, owner, slot, card) in cases)
        {
            var game = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Ana", "Beto"], Hands = [[flute], [CardCatalog.Attack.Id]],
                Decks = [Enumerable.Repeat(CardCatalog.Attack.Id, 6).ToList(), Enumerable.Repeat(CardCatalog.Attack.Id, 6).ToList()],
                Bonuses = [null, null], StartingSeat = 0
            }, new SeededRandom(41));
            game.State.Phase = Phase.Actions; game.State.Turn = 2;
            var ana = game.State.Players[0]; var beto = game.State.Players[1];
            ana.Skills.AddRange([CardCatalog.Reverse.Id, CardCatalog.Destiny.Id]);
            ana.SkillPositions = new() { [1] = 3 };
            ana.Exchange.AddRange([CardCatalog.Senshuni.Id, CardCatalog.Pendant.Id]);
            beto.Equipment = CardCatalog.Pc.Id;
            if (!game.PlayCode(0, 0).Success) throw new Exception("No se activa Flauta de Vidrio.");

            ResetAnimations();
            var before = DuelSnapshot.For(game, seat, 70);
            PresentTestSnapshot(before); await Layout();
            var relativeOwner = owner == seat ? 0 : 1;
            var sourceNode = ZoneNode(relativeOwner, MovementZone(zone), slot);
            if (sourceNode == null) throw new Exception($"Flauta de Vidrio · {name}: no se encuentra la casilla de origen.");
            var sourceRect = ZoneRect(sourceNode);

            var cues = DuelVisualEvents.Capture(game);
            var choice = DuelOptions.For(game, 0).Single(o => o.Move.Kind == DuelMoveKind.Choose && o.Field is { } f &&
                f.Zone == zone && f.Owner == owner && f.Index == slot);
            if (!choice.Move.Apply(game, 0).Success) throw new Exception($"Flauta de Vidrio · {name}: no se puede devolver.");
            var after = DuelSnapshot.For(game, seat, 71); after.Visuals = cues;
            var cue = cues.Single(c => c.Kind == DuelVisualKind.Pile && c.Destination == CardZone.Hand);
            var origins = CaptureOrigins(after, before);
            if (!origins.TryGetValue(cue, out var flight) || flight.Origin is not { } origin ||
                origin.GetCenter().DistanceTo(sourceRect.GetCenter()) > 1 || flight.Face != card)
                throw new Exception($"Flauta de Vidrio · {name}: el vuelo debe salir de su casilla mostrando la carta.");

            PresentTestSnapshot(after); Animate(cues, origins); await Layout();
            var landing = ZoneNode(relativeOwner, CardZone.Hand, cue.Value);
            if (landing == null || landing.Modulate.A != 0)
                throw new Exception($"Flauta de Vidrio · {name}: la carta aparece en la mano antes de llegar.");
            var flew = false;
            for (var frame = 0; IsAnimating && frame < 100; frame++)
            {
                UpdateFlights(0.1);
                if (_activeFlight?.Event == cue)
                {
                    flew = true;
                    // Public on the field, the card turns face down only on its way into a hand the viewer cannot see.
                    if (_flightTarget != landing || _activeFlight.EndsFaceUp != (owner == seat))
                        throw new Exception($"Flauta de Vidrio · {name}: el vuelo debe terminar en la mano de su dueño con la cara correcta.");
                }
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            if (!flew || IsAnimating || landing.Modulate.A != 1)
                throw new Exception($"Flauta de Vidrio · {name}: la devolución debe animarse y dejar la carta visible en la mano.");
        }
        GD.Print("PVP_GLASS_FLUTE_PASS · habilidad, canje y equipo rival vuelan de su casilla a la mano de su dueño; ambos asientos.");
    }

    private static CardZone MovementZone(FieldZone zone) => zone switch
    {
        FieldZone.Skill => CardZone.Skill, FieldZone.Exchange => CardZone.Exchange, _ => CardZone.Equipment
    };
}
