using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    /// <summary>
    /// `--polish-only`: the refused play shakes its card and returns it to place, the final blow breathes,
    /// the finished duel gets one central moment that never takes the pointer, and a newly played code is
    /// flashed once. Every effect respects reduced motion; desktop and a compact layout.
    /// </summary>
    internal async Task CheckPolishEffects()
    {
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        async Task Layout() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        async Task Capture(string name)
        {
            var directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
            if (directory == null || DisplayServer.GetName() == "headless") return;
            System.IO.Directory.CreateDirectory(directory);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, name + ".png"));
        }
        GameEngine Fixture()
        {
            var game = GameEngine.FromOpening(new DuelOpening { Names = ["Ana", "Beto"], StartingSeat = 0,
                Hands = [[CardCatalog.Attack.Id, CardCatalog.Senshuni.Id, CardCatalog.PlumberStar.Id], [CardCatalog.Attack.Id]],
                Decks = [Enumerable.Repeat("attack", 20).ToList(), Enumerable.Repeat("attack", 20).ToList()], Bonuses = [null, null] }, new SeededRandom(23));
            game.State.Turn = 2; game.State.Phase = Phase.Actions;
            return game;
        }
        var reduced = _impacts.ReducedMotion;
        foreach (var size in new[] { new Vector2I(1820, 1024), new(1024, 768) })
        {
            GetWindow().Size = size; GetWindow().ContentScaleSize = size; await Layout();

            // Refused play: the card shakes and lands exactly where it was; reduced motion keeps it still.
            foreach (var still in new[] { false, true })
            {
                _impacts.ReducedMotion = still;
                ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(Fixture(), 0, 900)); await Layout();
                var card = (Control)_root!.FindChild("HandCard1", true, false);
                var rest = card.Position; var shakes = RejectionShakes;
                _lastPlayAnchor = card.Name;
                Connection.RejectPlay("La partida cambió. Revisa el tablero antes de volver a jugar.");
                RefreshDecisionFeedback();
                Check(RejectionShakes == shakes + (still ? 0 : 1), $"La sacudida no respeta el movimiento reducido ({still}) en {size}.");
                await Wait(0.35);
                Check(card.Position.IsEqualApprox(rest), $"La carta rechazada no vuelve a su sitio en {size}: {card.Position} ≠ {rest}.");
                ClearRejection();
            }
            _impacts.ReducedMotion = reduced;

            // Final blow: the heart breathes, and stays steady with reduced motion.
            var lethal = Fixture(); lethal.State.Players[0].Health = 0; lethal.State.Players[0].HasReachedZero = true;
            ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(lethal, 0, 901)); await Layout();
            _life[0].AdvancePlayback(10); _life[0].ReducedMotion = false;
            Check(_life[0].Pulsing && !_life[1].Pulsing, "El golpe final no late o late en un medidor sano.");
            _life[0].ReducedMotion = true;
            Check(!_life[0].Pulsing && _life[0].FinalBlow, "El latido ignora el movimiento reducido.");
            _life[0].ReducedMotion = false;

            // Played code: one flash for a new activation, none for a decision inside the same resolution.
            var before = DuelSnapshot.For(Fixture(), 1, 902);
            var after = DuelSnapshot.For(Fixture(), 1, 903);
            var nextId = (before.History?.Select(e => e.Id).DefaultIfEmpty(0UL).Max() ?? 0) + 1;
            after.History = [.. before.History ?? [], new DuelHistoryEntry { Id = nextId, Turn = 2, Actor = 0, SourceActor = 0,
                CardId = CardCatalog.Senshuni.Id, Text = "Ana activa Senshuni.", Status = DuelResolutionStatus.Resolving }];
            ResetAnimations(); PresentTestSnapshot(after); await Layout();
            _impacts.Clear(); FlashPlayedCard(before, after);
            Check(_impacts.ShowingCard, $"No se muestra el código jugado por el rival en {size}.");
            if (size.X == 1820)
            {
                // Freeze the flash at its full-opacity hold for the screenshot.
                _impacts._Process(0.35); _impacts.SetProcess(false); _impacts.QueueRedraw();
                await Capture($"polish-played-card-{size.X}x{size.Y}"); _impacts.SetProcess(true);
            }
            _impacts.Clear(); before.Resolution = new DuelResolutionView { CardId = CardCatalog.Senshuni.Id }; FlashPlayedCard(before, after);
            Check(!_impacts.ShowingCard, "Una decisión dentro de la misma resolución repite el destello.");
            _impacts.Clear(); before.Resolution = null; _impacts.ReducedMotion = true; FlashPlayedCard(before, after);
            Check(!_impacts.ShowingCard, "El destello ignora el movimiento reducido.");
            _impacts.ReducedMotion = reduced; _impacts.Clear();

            // Finished duel: one central moment that never takes the pointer and gives way to the rail.
            foreach (var seat in new[] { 0, 1 })
            {
                var finished = DuelSnapshot.For(Fixture(), seat, 904 + (ulong)seat);
                finished.Winner = 0; finished.EndReason = "Beto recibe el daño definitivo.";
                _resultDismissed = false; ResetAnimations(); PresentTestSnapshot(finished); UpdateResultPresentation(); await Layout();
                Check(ResultOverlayVisible && ResultOverlayTitle == (seat == 0 ? "¡VICTORIA!" : "DERROTA") &&
                    _resultOverlay.MouseFilter == MouseFilterEnum.Ignore, $"El resultado central no aparece o toma el puntero en {size}, asiento {seat}.");
                PositionResultOverlay(); await Layout();
                if (size.X == 1820 && seat == 0) { await Wait(0.4); await Capture($"polish-result-{size.X}x{size.Y}"); }
                if (_readerRail != null && IsInstanceValid(_readerRail))
                {
                    var rail = _readerRail.GetGlobalRect(); var card = _resultCard.GetGlobalRect();
                    Check(!card.Intersects(rail) && card.Position.X >= GlobalPosition.X &&
                        Math.Abs(card.GetCenter().X - (GlobalPosition.X + rail.Position.X) / 2) < 40,
                        $"El resultado central no queda centrado sobre el tapete en {size}: {card} · riel {rail}.");
                }
                var click = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = Size / 2 };
                // A screenshot can take longer than the hold; start it again so the click is really immediate.
                _resultOverlayShownAt = Time.GetTicksMsec();
                _Input(click);
                Check(ResultOverlayVisible, "Un clic inmediato descarta el resultado antes de poder leerlo.");
                ExpireResultOverlayHold(); _Input(click);
                Check(!ResultOverlayVisible, "Tras la espera, un clic no descarta el resultado.");
                UpdateResultPresentation();
                Check(!ResultOverlayVisible, "El resultado central reaparece para el mismo duelo.");
                _resultDismissed = true; UpdateResultPresentation(); _resultDismissed = false;
            }
            _resultOverlayKey = null;
        }
        GD.Print("PVP_POLISH_PASS · sacudida del rechazo, latido del golpe final, destello del código jugado y resultado central sin bloquear; movimiento reducido, dos tamaños y ambos asientos.");
    }
}
