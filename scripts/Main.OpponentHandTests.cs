using Godot;
using Relick.Core;

namespace Relick;

public partial class Main
{
    private async void OpponentHandSmokeTest()
    {
        try
        {
            _helpOpen = true; _die.SetProcess(false); _yuno.SetProcess(false); _enemyYuno.SetProcess(false);
            void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
            OpponentHandCard[] Cards() => _root.FindChild("OpponentHand", true, false).FindChildren("*", "Button", true, false)
                .OfType<OpponentHandCard>().ToArray();
            AcceptDialog Dialog() => GetChildren().OfType<AcceptDialog>().Single(d => !d.IsQueuedForDeletion());
            BindGame(new GameEngine(42)); Render();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var rival = _game.State.Players[1];
            Check(Cards().Length == rival.Hand.Count && Cards().All(c => c.IsFaceDown && c.PublicCard == null && c.HasBackArtwork),
                "La mano no muestra un reverso por carta real.");
            Check(Cards().All(c => c.TooltipText == Cards()[0].TooltipText && c.GetChildCount() == 0), "Una carta oculta filtra información.");
            Check(!_root.FindChildren("*", "Control", true, false).OfType<ZoneTile>().Any(t => t.Caption == "MANO"), "Queda un contador lateral de mano.");
            var before = System.Text.Json.JsonSerializer.Serialize(_game.State);
            _helpOpen = false; Cards()[0].EmitSignal(BaseButton.SignalName.Pressed);
            var gallery = Dialog().FindChildren("*", "Button", true, false).OfType<OpponentHandCard>().ToArray();
            Check(gallery.Length == rival.Hand.Count && gallery.All(c => c.IsFaceDown && c.Disabled && c.PublicCard == null), "Ampliar expone cartas ocultas.");
            Check(System.Text.Json.JsonSerializer.Serialize(_game.State) == before, "Consultar la mano modifica la partida.");
            Dialog().EmitSignal(AcceptDialog.SignalName.Confirmed); _helpOpen = true;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            rival.RevealHandCard(0); Render();
            Check(Cards()[0].PublicCard?.Id == rival.Hand[0] && Cards().Skip(1).All(c => c.IsFaceDown), "Revelar muestra más cartas de las indicadas.");
            _helpOpen = false; Cards()[0].EmitSignal(BaseButton.SignalName.Pressed);
            Check((Dialog().FindChild("ZoneCardPreview", true, false) as HandCard)?.Card.Id == rival.Hand[0], "No se puede consultar la carta revelada.");
            Dialog().EmitSignal(AcceptDialog.SignalName.Confirmed); _helpOpen = true;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            rival.RemoveHandCard(0); Render();
            Check(Cards().Length == rival.Hand.Count && Cards().All(c => c.IsFaceDown), "Jugar una carta revelada revela la siguiente.");
            rival.Hand.Add("lucky-pendant"); Render();
            Check(Cards().Length == rival.Hand.Count && Cards().All(c => c.IsFaceDown), "Robar una carta muestra información nueva.");
            rival.Hand.Clear(); rival.HideHandCards(); Render();
            Check(Cards().Length == 0, "La mano vacía conserva reversos.");
            rival.Hand.AddRange(Enumerable.Repeat("attack", 100)); Render();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(Cards().Length == 100, "La mano visual introduce un límite.");
            var horizontal = _opponentHandScroll!.GetHScrollBar();
            Check(horizontal.MaxValue > horizontal.Page, "No se puede recorrer una mano grande.");
            _opponentHandScroll.ScrollHorizontal = 150; var position = _opponentHandScroll.ScrollHorizontal; Render();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(_opponentHandScroll.ScrollHorizontal == position, "Actualizar el tablero pierde el desplazamiento de la mano rival.");
            _helpOpen = false; ShowOpponentHand();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var list = (ScrollContainer)Dialog().FindChild("OpponentHandGallery", true, false);
            Check(list.FindChildren("*", "Button", true, false).OfType<OpponentHandCard>().Count() == 100 && list.GetVScrollBar().MaxValue > list.GetVScrollBar().Page,
                "La vista ampliada no muestra todas las cartas.");
            Dialog().EmitSignal(AcceptDialog.SignalName.Confirmed); _helpOpen = true;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var minimum = _root.GetCombinedMinimumSize();
            var available = GetViewportRect().Size - new Vector2(48, 30);
            Check(minimum.X <= available.X + 1 && minimum.Y <= available.Y + 1, $"La mano rival desborda el tablero: {minimum} / {available}");
            NewGame();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(Cards().Length == 5 && Cards().All(c => c.IsFaceDown) && _opponentHandScroll!.ScrollHorizontal == 0,
                "Reiniciar no restaura la mano oculta.");
            GD.Print("OPPONENT_HAND_SMOKE_PASS · reversos originales, privacidad, revelación individual, inspección, robos, salidas, mano de 100 cartas y reinicio.");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
