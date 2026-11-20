using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckRobotizeLayout()
    {
        var robotize = CardCatalog.Robotize;
        var attack = CardCatalog.Attack.Id;
        var code = CardCatalog.VictoriousManualControl;
        if (BoardArt.LoadCardArtwork(robotize) == null || BoardArt.CardType(robotize) != "CÓDIGO · JUGADA")
            throw new Exception("Falta el arte o la categoría de Robotizar.");

        foreach (var size in new[] { new Vector2I(1440, 1024), new(700, 390) })
        foreach (var actor in new[] { 0, 1 })
        {
            GetWindow().ContentScaleSize = size;
            for (var frame = 0; frame < 4; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var deck = new List<string> { code.Id, attack, attack };
            var game = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Ana", "Beto"], Hands = [[], []], Decks = [deck.ToList(), deck.ToList()],
                Bonuses = [null, null], StartingSeat = actor
            }, new SeededRandom(26));
            game.State.Phase = Phase.Actions;
            game.State.Active.Hand.Add(robotize.Id);
            if (!game.PlayCode(actor, 0).Success) throw new Exception("No se coloca Robotizar.");
            game.State.Active.AttachedCards.Add(attack);

            ResetAnimations();
            PresentTestSnapshot(DuelSnapshot.For(game, actor, 1));
            if (ZoneNode(0, CardZone.Attached, 0) is not AttachmentStack pile || pile.TopCardId != robotize.Id)
                throw new Exception("Robotizar debe verse encima del Ataque anexo al Relikario.");

            game.State.Phase = Phase.Combat;
            if (!game.Advance(actor).Success || game.State.PendingDecision?.Source != robotize.Id)
                throw new Exception("Robotizar debe ofrecer el Código del tope al entrar en Acciones.");
            PresentTestSnapshot(DuelSnapshot.For(game, 1 - actor, 2));
            if (_snapshot!.Options.Any(option => option.CardId == code.Id) || _selection.Text.Contains(code.Name))
                throw new Exception("La carta del tope debe permanecer privada.");
            var before = DuelSnapshot.For(game, actor, 2);
            PresentTestSnapshot(before);
            var options = before.Options.Where(option => option.Move.Kind == DuelMoveKind.Choose).ToArray();
            if (options.Length != 2 || options.Any(option => !PresentsOption(option)) || !_selection.Text.Contains(code.Name))
                throw new Exception("El jugador necesita ver el nombre completo y ambos destinos.");

            var visuals = DuelVisualEvents.Capture(game);
            if (!options.Single(option => option.Button == "Enviar al canje").Move.Apply(game, actor).Success)
                throw new Exception("La opción de canje debe aplicar el traslado.");
            var after = DuelSnapshot.For(game, actor, 3);
            after.Visuals = visuals;
            var flight = CaptureOrigins(after, before).Values.Single(f => f.Event.Destination == CardZone.Exchange);
            if (!flight.IsExchangePlacement || flight.Event.Source != CardZone.Deck || flight.Face != code.Id ||
                after.Players[actor].Exchange.Single() != code.Id)
                throw new Exception("La carta debe llegar al canje desde el mazo.");
        }
        foreach (var seat in new[] { 0, 1 })
        {
            var game = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Ana", "Beto"], Hands = [[robotize.Id], []], Decks = [[attack], [attack]],
                Bonuses = [null, null], StartingSeat = 0
            }, new SeededRandom(26));
            game.State.Phase = Phase.Actions;
            game.State.Active.AttachedCards.Add(attack);
            game.State.Active.GalacticShields = [new()];
            ResetAnimations();
            var before = DuelSnapshot.For(game, seat, 10);
            PresentTestSnapshot(before);
            var visuals = DuelVisualEvents.Capture(game);
            if (!game.PlayCode(0, 0).Success) throw new Exception("No se juega Robotizar.");
            var after = DuelSnapshot.For(game, seat, 11);
            after.Visuals = visuals;
            var origins = CaptureOrigins(after, before);
            var flight = origins.Values.SingleOrDefault();
            if (flight is not { IsPlacement: true } || flight.Event.Destination != CardZone.Attached || flight.Face != robotize.Id ||
                flight.StartsFaceUp != (seat == 0))
                throw new Exception("Robotizar debe volar desde la mano hasta encima del Relikario, no a la memoria.");
            PresentTestSnapshot(after);
            var relative = seat == 0 ? 0 : 1;
            var stack = (AttachmentStack)ZoneNode(relative, CardZone.Attached, -1)!;
            var shield = (ZoneCardButton)ZoneNode(relative, CardZone.GalacticShield, 0)!;
            Animate(visuals, origins);
            if (stack.PresentedTopCardId != attack || stack.Modulate.A != 1 || stack.SelfModulate.A != 1 ||
                shield.PresentedCard?.Id != CardCatalog.GalacticShield.Id)
                throw new Exception("El Ataque anexo, el contorno y el Escudo deben seguir visibles durante el vuelo de Robotizar.");
            ResetAnimations();
            if (stack.PresentedTopCardId != robotize.Id)
                throw new Exception("Al interrumpir el vuelo, Robotizar debe quedar visible en el tablero.");
        }
        GD.Print("PVP_ROBOTIZE_PASS");
    }
}
