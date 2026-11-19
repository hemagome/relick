using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckEllieLayout()
    {
        var ellie = CardCatalog.Ellie;
        if (BoardArt.LoadCardArtwork(ellie) == null || BoardArt.CardType(ellie) != "CÓDIGO · PERSONAJE")
            throw new Exception("Falta el arte o la categoría de Ellie.");

        foreach (var size in new[] { new Vector2I(1440, 1024), new(700, 390) })
        foreach (var actor in new[] { 0, 1 })
        {
            GetWindow().ContentScaleSize = size;
            for (var frame = 0; frame < 4; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var deck = new[] { ellie.Id, CardCatalog.Attack.Id, CardCatalog.ExtraLife.Id, CardCatalog.Bonfire.Id, CardCatalog.Totem.Id };
            var game = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Ana", "Beto"], Hands = [[], []], Decks = [deck.ToList(), deck.ToList()],
                Bonuses = [null, null], StartingSeat = actor
            }, new SeededRandom(25));
            game.State.Phase = Phase.Actions;
            game.State.Active.Hand.Add(ellie.Id);
            if (!game.PlayCode(actor, 0).Success) throw new Exception("No empieza la búsqueda de Ellie.");
            ResetAnimations();
            PresentTestSnapshot(DuelSnapshot.For(game, actor, 1));
            if (!ChoosingDeckGallery || _deckSearchOptions.Length != 3)
                throw new Exception("La galería de Ellie debe mostrar solo las tres coincidencias.");
            var first = Array.FindIndex(_deckSearchOptions, option => option.CardId == CardCatalog.Bonfire.Id);
            SelectCard(first);
            if (!PresentsOption(_deckSearchOptions[first]) || !_deckSearchOptions[first].Move.Apply(game, actor).Success)
                throw new Exception("La primera elección debe confirmarse en la galería.");

            PresentTestSnapshot(DuelSnapshot.For(game, 1 - actor, 2));
            if (ChoosingDeckGallery || _deckSearchOptions.Length != 0 || _snapshot!.Players[actor].Hand.Single() != null)
                throw new Exception("El rival debe ver únicamente la carta boca abajo.");

            PresentTestSnapshot(DuelSnapshot.For(game, actor, 2));
            if (!ChoosingDeckGallery || _deckSearchOptions.Length != 2)
                throw new Exception("La segunda elección debe mostrar solo las coincidencias restantes.");
            SelectCard(0);
            if (!PresentsOption(_deckSearchOptions[0]) || !_deckSearchOptions[0].Move.Apply(game, actor).Success)
                throw new Exception("La segunda elección debe confirmarse.");
            PresentTestSnapshot(DuelSnapshot.For(game, actor, 3));
            if (ChoosingDeckGallery || _snapshot!.Players[actor].Hand.Length != 2)
                throw new Exception("La arena debe volver a la mano con las dos cartas.");
        }
        GD.Print("PVP_ELLIE_PASS");
    }
}
