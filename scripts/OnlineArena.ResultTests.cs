using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckDuelResults()
    {
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        async Task Layout() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        var embedded = GetViewport().GuiEmbedSubwindows; GetViewport().GuiEmbedSubwindows = true;
        try
        {
            foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(1280, 480), new(844, 390), new(700, 390) })
            foreach (var seat in new[] { 0, 1 })
            foreach (var ending in new[] { 0, 1, 2, 3 })
            {
                var game = GameEngine.FromOpening(new DuelOpening { Names = ["Ana", "Beto"], StartingSeat = 0,
                    Hands = [[CardCatalog.Senshuni.Id], [CardCatalog.Attack.Id]],
                    Decks = [[CardCatalog.Attack.Id], Enumerable.Repeat(CardCatalog.Attack.Id, 12).ToList()], Bonuses = [null, null] }, new SeededRandom(66));
                if (ending == 2)
                {
                    game.State.Phase = Phase.Actions; game.State.Active.Recycled = true; game.State.Players[1].Health = 6;
                    Check(new DuelMove { Kind = DuelMoveKind.PlayCode, Index = 0 }.Apply(game, 0).Success && game.State.Winner == -1, "No se alcanza el empate de la prueba.");
                }
                else Check(new DuelMove { Kind = DuelMoveKind.Concede }.Apply(game, ending == 1 ? 1 : 0).Success, "No se abandona el duelo de prueba.");
                var s = DuelSnapshot.For(GameEngine.FromSnapshot(game.SaveSnapshot(false), new SeededRandom(66)), seat, 990);
                if (ending == 3) s.EndReason = null; // Older finished checkpoints have no public cause.
                GetWindow().ContentScaleSize = size; await Layout();
                ResetAnimations(); _resultDismissed = false; PresentTestSnapshot(s); await Layout();
                Check(_resultPanel is { Visible: true } && !_actionDock.Visible, "El resultado no sustituye al área de jugadas.");
                var expected = s.Winner == -1 ? "EMPATE" : s.Winner == seat ? "VICTORIA" : "DERROTA";
                Check(((Label)_resultPanel!.FindChild("DuelResultTitle", true, false)).Text == expected, "El resultado confunde vencedor y asiento.");
                Check(((Label)_resultPanel.FindChild("DuelResultReason", true, false)).Text == (s.EndReason ?? "Resultado confirmado por el servidor."), "El cierre inventa una causa o pierde la causa persistida.");
                CheckPendingCodeBounds();
                Check(new Rect2(Vector2.Zero, Size).Encloses(_resultPanel.GetGlobalRect()) && RectVisible(_resultReturn!, _resultReturn!.GetGlobalRect()), "El cierre o su acción disponible queda fuera de pantalla.");
                Check(_resultReturn!.Disabled, "El cierre desconectado permite enviar sin reconectar.");
                GuiInputTestDriver.Click((Button)_resultPanel.FindChild("ReviewResultBoard", true, false)); await Layout();
                Check(!_resultPanel.Visible && _actionDock.Visible, "No se puede volver a consultar el tablero final.");
                GuiInputTestDriver.Click(_arenaMenu!); await Layout();
                var popup = _arenaMenu!.GetPopup(); popup.GrabFocus(); popup.SetFocusedItem(popup.GetItemIndex(4)); await Layout();
                GetTree().Root.PushInput(new InputEventKey { Keycode = Key.Enter, Pressed = true }, true);
                GetTree().Root.PushInput(new InputEventKey { Keycode = Key.Enter, Pressed = false }, true); await Layout();
                Check(_resultPanel.Visible, $"Menú no permite recuperar el resultado: popup={popup.Visible}, foco={popup.GetFocusedItem()}, resultado={popup.GetItemIndex(4)}, descartado={_resultDismissed}, estable={ResultSettled}.");
                _die.RollTo(6); UpdateResultPresentation(); Check(!_resultPanel.Visible, "El resultado interrumpe el dado.");
                _die.AdvancePlayback(DiceFace.RollSeconds + 2); UpdateResultPresentation();
                Check(_resultPanel.Visible, "El resultado no aparece después del dado.");
                _portraits[0].Play(PortraitMotion.HitCritical); UpdateResultPresentation();
                Check(!_resultPanel.Visible, "El resultado interrumpe el último impacto.");
                _portraits[0].AdvancePlayback(2); UpdateResultPresentation(); Check(_resultPanel.Visible, "El resultado no espera al final del impacto.");
            }
        }
        finally { GetViewport().GuiEmbedSubwindows = embedded; ResetAnimations(); _resultDismissed = false; }
        GD.Print("PVP_DUEL_RESULTS_PASS · victoria/derrota/empate, causa pública y heredada, recuperación, dado e impacto final, revisar tablero, reapertura y conexión; cinco tamaños y ambos asientos.");
    }
}
