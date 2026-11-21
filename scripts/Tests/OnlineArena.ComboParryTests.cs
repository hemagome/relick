using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckComboParryLayout()
    {
        var parry = CardCatalog.ComboParry;
        var attack = CardCatalog.Attack.Id;
        var error = CardCatalog.LoadError.Id;
        var dodge = CardCatalog.Dodge.Id;
        if (BoardArt.LoadCardArtwork(parry) == null || BoardArt.CardType(parry) != "CÓDIGO · JUGADA" || !parry.IsCombo)
            throw new Exception("Falta el arte o la clasificación de Combo Parry.");

        foreach (var size in new[] { new Vector2I(1440, 1024), new(700, 390) })
        foreach (var actor in new[] { 0, 1 })
        {
            GetWindow().ContentScaleSize = size;
            for (var frame = 0; frame < 4; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var game = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Ana", "Beto"], Hands = [[], []], Decks = [[attack, attack], [attack, attack]],
                Bonuses = [null, null], StartingSeat = actor
            }, new SeededRandom(27));
            game.State.Phase = Phase.Actions;
            game.State.Active.Hand.AddRange([parry.Id, CardCatalog.Bonfire.Id]);
            game.State.Players[1 - actor].Hand.AddRange([error, attack, dodge]);
            if (!game.PlayCode(actor, 0).Success) throw new Exception("No se activa Combo Parry.");

            ResetAnimations();
            PresentTestSnapshot(DuelSnapshot.For(game, 1 - actor, 1));
            if (_opponentCards.Any(card => !card.IsFaceDown) || _snapshot!.Options.Any(option => option.Move.Kind == DuelMoveKind.PlayCode))
                throw new Exception("La revelación no debe mostrar la mano del activador ni abrir respuestas.");

            var before = DuelSnapshot.For(game, actor, 1);
            PresentTestSnapshot(before);
            var choice = before.Options.Single(option => option.Move.Kind == DuelMoveKind.Choose);
            if (_opponentCards.Count != 3 || _opponentCards.Any(card => card.IsFaceDown) || PresentedButton(choice)?.Text != "Continuar")
                throw new Exception("Se debe ver la mano completa y poder confirmar desde la arena.");

            var visuals = DuelVisualEvents.Capture(game);
            if (!choice.Move.Apply(game, actor).Success) throw new Exception("No se resuelve la revelación.");
            var after = DuelSnapshot.For(game, actor, 2);
            after.Visuals = visuals;
            var flights = CaptureOrigins(after, before).Values.Where(flight => flight.Event.Kind == DuelVisualKind.Return).ToArray();
            if (!flights.Select(flight => flight.Face).SequenceEqual([dodge, error]))
                throw new Exception("Deben viajar al mazo solo las cartas COMBO reveladas.");
            PresentTestSnapshot(after);
            if (_opponentCards.Count != 1 || !_opponentCards[0].IsFaceDown || _snapshot!.Decision != null)
                throw new Exception("La carta restante vuelve a quedar oculta.");

            game.State.Players[1 - actor].Hand.Add(parry.Id);
            if (!game.Equip(actor, 0).Success || game.State.Phase != Phase.ErrorResponse)
                throw new Exception("El equipo debe ofrecer la respuesta de Combo Parry.");
            var response = DuelSnapshot.For(game, 1 - actor, 3);
            PresentTestSnapshot(response);
            var parryOption = response.Options.Single(option => option.Move.Kind == DuelMoveKind.PlayCode);
            SelectCard(parryOption.Move.Index);
            if (PresentedButton(parryOption) is not CardActionButton { Text: "Activar" } activate ||
                activate.GetParent() != _cardActions || _contextTitle.Text != parry.Name || _decisionPopup is { Visible: true })
                throw new Exception("La respuesta debe ofrecer Activar junto a Combo Parry.");
        }
        GD.Print("PVP_COMBO_PARRY_PASS");
    }
}
