using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckMetalHoodLayout()
    {
        var hood = CardCatalog.MetalHood;
        var attack = CardCatalog.Attack.Id;
        if (BoardArt.LoadCardArtwork(hood) == null || BoardArt.CardType(hood) != "EQUIPO")
            throw new Exception("Falta el arte o el tipo de Capirote Metálico.");

        foreach (var size in new[] { new Vector2I(1440, 1024), new(700, 390) })
        foreach (var actor in new[] { 0, 1 })
        foreach (var seat in new[] { 0, 1 })
        {
            GetWindow().ContentScaleSize = size;
            for (var frame = 0; frame < 4; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var game = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Ana", "Beto"], Hands = [[], []], Decks = [[attack, attack], [attack, attack]],
                Bonuses = [null, null], StartingSeat = actor
            }, new SeededRandom(27));
            game.State.Phase = Phase.Actions;
            game.State.Active.Hand.Add(hood.Id);

            ResetAnimations();
            var before = DuelSnapshot.For(game, seat, 1);
            PresentTestSnapshot(before);
            var options = DuelOptions.For(game, actor);
            var rival = options.Single(o => o.Move.Kind == DuelMoveKind.Equip && o.Move.Flag);
            if (seat == actor)
            {
                SelectCard(0);
                var own = before.Options.Single(o => o.Move.Kind == DuelMoveKind.Equip && !o.Move.Flag);
                if (PresentedButton(own)?.Text != "Equiparme" ||
                    PresentedButton(before.Options.Single(o => o.Move.Kind == DuelMoveKind.Equip && o.Move.Flag))?.Text != "Equipar al rival")
                    throw new Exception("Faltan las dos opciones de equipamiento junto a la carta.");
            }

            var visuals = DuelVisualEvents.Capture(game);
            if (!rival.Move.Apply(game, actor).Success) throw new Exception("No se equipa el Capirote al rival.");
            var recipient = 1 - actor;
            var after = DuelSnapshot.For(game, seat, 2);
            after.Visuals = visuals;
            var origins = CaptureOrigins(after, before);
            PresentTestSnapshot(after);
            Animate(visuals, origins);
            var relative = recipient == seat ? 0 : 1;
            if (ZoneNode(relative, CardZone.Equipment, -1) is not ZoneCardButton tile ||
                tile.Card?.Id != hood.Id || !tile.AwaitingEquipment || _actionPools[relative].Amount != 1)
                throw new Exception("El portador debe mostrar una acción y esperar a que llegue el equipo.");
            for (var frame = 0; frame < 4; frame++) UpdateFlights(0);
            if (_flightTarget != tile || _flightFront?.Card.Id != hood.Id || _flightFront.Visible != (seat == actor))
                throw new Exception("El equipo debe salir de la mano y volar al portador.");
            UpdateFlights(1.1);
            if (tile.AwaitingEquipment) throw new Exception("El equipo debe revelarse cuando aterrice.");
        }
        GD.Print("PVP_METAL_HOOD_PASS");
    }
}
