using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckComboResponseToggle()
    {
        foreach (var actor in new[] { 0, 1 })
        foreach (var enabled in new[] { true, false })
        foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
        foreach (var seat in new[] { 0, 1 })
        {
            var game = GameEngine.FromOpening(new DuelOpening { Names = ["Ana", "Beto"], StartingSeat = actor,
                Hands = [[CardCatalog.Attack.Id], [CardCatalog.Attack.Id]],
                Decks = [Enumerable.Repeat("attack", 20).ToList(), Enumerable.Repeat("attack", 20).ToList()], Bonuses = [null, null] }, new SeededRandom(31));
            game.State.Phase = Phase.Actions; game.SetComboResponses(actor, enabled);
            GetWindow().Size = size; GetWindow().ContentScaleSize = size;
            for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, seat, 500));
            for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var toggle = _comboResponsesToggle;
            var expected = seat != actor || enabled;
            if (toggle == null || !toggle.IsVisibleInTree() || toggle.ButtonPressed != expected ||
                toggle.AccessibilityName != (expected ? "COMBO: preguntar" : "COMBO: pasar"))
                throw new Exception("El control COMBO pierde el estado propio o muestra la preferencia rival.");
            if (_moveButtons.Contains(toggle) || toggle.GetGlobalRect().Size.Y < 48)
                throw new Exception("El control COMBO se mezcla con jugadas o no tiene tamaño táctil.");
            var minimum = _root!.GetCombinedMinimumSize();
            if (minimum.X > Size.X + 1 || minimum.Y > Size.Y + 1)
                throw new Exception($"El control COMBO desborda {size}: {minimum}.");
            var oldBuilds = BoardBuildCount; var oldVisuals = PlayedVisualRevisions;
            game.SetComboResponses(seat, !expected); ApplyPrivateComboPreference(DuelSnapshot.For(game, seat, 500));
            if (toggle.ButtonPressed == expected || BoardBuildCount != oldBuilds || PlayedVisualRevisions != oldVisuals)
                throw new Exception("Actualizar la preferencia reconstruye el tablero o reproduce animaciones.");
        }
        var search = GameEngine.FromOpening(new DuelOpening { Names = ["Ana", "Beto"], StartingSeat = 0,
            Hands = [[CardCatalog.KonamyCode.Id], []], Decks = [Enumerable.Repeat("attack", 20).ToList(), Enumerable.Repeat("attack", 20).ToList()],
            Bonuses = [null, null] }, new SeededRandom(8));
        search.State.Phase = Phase.Actions;
        if (!search.PlayCode(0, 0).Success) throw new Exception("No empieza la búsqueda para validar el control COMBO.");
        PresentTestSnapshot(DuelSnapshot.For(search, 0, 501)); SelectCard(0);
        var builds = BoardBuildCount;
        search.SetComboResponses(0, false); ApplyPrivateComboPreference(DuelSnapshot.For(search, 0, 501));
        if (_selected.Count != 1 || !PresentsKind(DuelMoveKind.SearchDeck) || BoardBuildCount != builds)
            throw new Exception("Cambiar COMBO pierde el objetivo o sus opciones autorizadas.");
        GD.Print("PVP_COMBO_RESPONSES_PASS · dos estados privados, ambos jugadores/asientos, cuatro tamaños y actualización sin animaciones.");
    }
}
