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
        GD.Print("PVP_ROBOTIZE_PASS");
    }
}
