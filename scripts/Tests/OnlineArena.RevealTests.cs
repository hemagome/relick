using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    /// <summary>
    /// Revealing Perro Fantasma turns the card over for both duelists and leaves the eye badge on it: its owner sees the
    /// card rise in place, the rival sees it turn from the sleeve to its face.
    /// </summary>
    private async Task CheckGhostDogReveal(GameEngine game, int seat, int index, DuelOption reveal)
    {
        var reduced = _impacts.ReducedMotion; _impacts.ReducedMotion = false;
        var (scale, window) = (GetWindow().ContentScaleSize, GetWindow().Size);
        try
        {
            // The desktop board, where both hands sit in their fans.
            GetWindow().Size = GetWindow().ContentScaleSize = new(1440, 1024);
            for (var i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var before = new[] { DuelSnapshot.For(game, seat, 5), DuelSnapshot.For(game, 1 - seat, 5) };
            var cues = DuelVisualEvents.Capture(game);
            if (!reveal.Move.Apply(game, seat).Success) throw new Exception("No se pudo revelar Perro Fantasma.");
            if (cues.SingleOrDefault(c => c.Kind == DuelVisualKind.Reveal) is not { } cue || cue.Player != seat || cue.Value != index)
                throw new Exception("Revelar Perro Fantasma debe emitir una señal pública con su posición en la mano.");

            foreach (var viewer in new[] { seat, 1 - seat })
            {
                ResetAnimations();
                PresentTestSnapshot(before[viewer == seat ? 0 : 1]);
                var after = DuelSnapshot.For(game, viewer, 6); after.Visuals = cues;
                var view = after.Players[seat];
                if (view.RevealedHand?.SequenceEqual([index]) != true || view.ArmedHand?.SequenceEqual([index]) != true)
                    throw new Exception("La vista debe decir qué carta de la mano es pública y cuál espera activarse.");
                PresentTestSnapshot(after); Animate(cues, CaptureOrigins(after, before[viewer == seat ? 0 : 1]));
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (viewer == seat)
                {
                    var card = _handCards.Single(c => c.Name == $"HandCard{index}");
                    if (!card.Revealed || !card.Armed || !card.PlayingReveal || !card.TooltipText.Contains("REVELADA"))
                        throw new Exception("Tu Perro Fantasma debe girar en la mano y quedar marcado como revelado y pendiente.");
                    if (_handCards.Where(c => c != card).Any(c => c.Revealed))
                        throw new Exception("Solo la carta revelada debe llevar el ojo.");
                }
                else
                {
                    var card = _opponentCards.Single(c => c.Name == $"OpponentHandCard{index}");
                    if (card.IsFaceDown || !card.Revealed || !card.Armed || !card.PlayingReveal)
                        throw new Exception("El rival debe ver Perro Fantasma girar boca arriba y quedar marcado.");
                    if (card.FindChild("RevealedFront", false, false) is HandCard { Visible: true })
                        throw new Exception("La cara debe aparecer a mitad del giro, no antes.");
                    if (_opponentCards.Where(c => c != card).Any(c => !c.IsFaceDown))
                        throw new Exception("Revelar una carta no debe destapar el resto de la mano rival.");
                }
                var who = viewer == seat ? "propia" : "rival";
                if (viewer != seat) _arenaScroll?.EnsureControlVisible(_opponentCards.Single(c => c.Name == $"OpponentHandCard{index}"));
                await WaitReveal(HandCard.RevealSeconds * 0.36f); await CaptureReveal($"revelar-{who}-1-giro");
                await WaitReveal(HandCard.RevealSeconds * 0.3f); await CaptureReveal($"revelar-{who}-2-cara");
                // The reveal settles on its own and the badge stays.
                await WaitReveal(HandCard.RevealSeconds * 0.34f + 0.3f); await CaptureReveal($"revelar-{who}-3-final");
                if (_handCards.Concat<Control>(_opponentCards).Any(Playing)) throw new Exception("El giro de revelar no termina.");
                if (viewer != seat && (_opponentCards.Single(c => c.Name == $"OpponentHandCard{index}") is not { Revealed: true } settled ||
                    settled.FindChild("RevealedFront", false, false) is not HandCard { Visible: true }))
                    throw new Exception("Al terminar el giro, la carta rival debe quedar boca arriba con su marca.");
            }
        }
        finally { _impacts.ReducedMotion = reduced; GetWindow().ContentScaleSize = scale; GetWindow().Size = window; }
    }

    private async Task WaitReveal(float seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private async Task CaptureReveal(string name)
    {
        var directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
        if (directory == null || DisplayServer.GetName() == "headless") return;
        System.IO.Directory.CreateDirectory(directory);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, name + ".png"));
    }

    private static bool Playing(Control card) => card is HandCard { PlayingReveal: true } or OpponentHandCard { PlayingReveal: true };
}
