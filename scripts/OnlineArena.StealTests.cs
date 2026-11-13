using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckHandStealAnimation()
    {
        var directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
        foreach (var size in new[] { new Vector2I(1440, 1024), new(844, 390) })
        foreach (var actor in new[] { 0, 1 })
        foreach (var seat in new[] { 0, 1 })
        {
            var game = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Jugador 1", "Jugador 2"], StartingSeat = actor,
                Hands = [Enumerable.Repeat(CardCatalog.Attack.Id, 4).ToList(), Enumerable.Repeat(CardCatalog.Attack.Id, 4).ToList()],
                Decks = [Enumerable.Repeat("attack", 12).ToList(), Enumerable.Repeat("attack", 12).ToList()], Bonuses = [null, null]
            }, new SeededRandom(57));
            game.State.Players[1 - actor].Hand[2] = CardCatalog.Bonfire.Id;
            game.State.Phase = Phase.MagnetChoice; game.State.CurrentPlayer = actor;
            GetWindow().ContentScaleSize = size;
            for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            _selected.Clear(); ResetAnimations(); var before = DuelSnapshot.For(game, seat, 300); PresentTestSnapshot(before);
            for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            async Task Capture(string name)
            {
                if (directory == null || DisplayServer.GetName() == "headless") return;
                System.IO.Directory.CreateDirectory(directory);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, $"{name}-a{actor}s{seat}-{size.X}x{size.Y}.png"));
            }

            if (_handLowered)
            {
                // The rival's fan mirrors ours: opposite tilt, same share of each card on screen.
                var rival = _opponentCards.OrderBy(c => c.Name.ToString()).ToArray();
                var own = _handCards.OrderBy(c => c.Name.ToString()).ToArray();
                if (rival.Length != own.Length || rival.Zip(own).Any(p => !Mathf.IsEqualApprox(p.First.Rotation, -p.Second.Rotation, 0.002f)))
                    throw new Exception("La mano rival no tiene la misma inclinación que la propia.");
                if (rival.All(c => Mathf.IsZeroApprox(c.Rotation))) throw new Exception("La mano rival no forma un abanico.");
                var screen = new Rect2(Vector2.Zero, Size);
                float Shown(Control card)
                {
                    var rect = Upright(card);
                    return rect.Intersection(screen).Size.Y / rect.Size.Y;
                }
                var rivalShown = Shown(rival[1]); var ownShown = Shown(own[1]);
                if (Math.Abs(rivalShown - ownShown) > 0.06f)
                    throw new Exception($"La mano rival muestra {rivalShown:P0} de cada carta y la propia {ownShown:P0} en {size}.");
                await Capture("rival-hand-fan");
            }

            var cues = DuelVisualEvents.Capture(game);
            if (!game.ResolveMagnet(actor, false, 2).Success) throw new Exception("Magnet no toma la carta de la mano rival.");
            var after = DuelSnapshot.For(game, seat, 301); after.Visuals = cues;
            var origins = CaptureOrigins(after, before);
            var steal = origins.SingleOrDefault(p => p.Key.Kind == DuelVisualKind.HandSteal).Value
                ?? throw new Exception("El robo de una carta de la mano rival no produce animación.");
            if (steal.Origin == null && _handLowered) throw new Exception($"El robo no parte de la carta tomada en {size}.");
            if (seat == actor && steal.Face != CardCatalog.Bonfire.Id) throw new Exception("Quien roba no ve la carta que recibe.");
            if (seat != actor && steal.EndsFaceUp != false) throw new Exception("La carta robada se revela al rival al llegar a la mano contraria.");
            _selected.Clear(); PresentTestSnapshot(after); Animate(cues, origins);
            for (var i = 0; i < 3; i++) { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); UpdateFlights(0); }
            // Compact layouts skip flights through clipped panels, as every other card movement does.
            var desktop = _handLowered;
            if (desktop && (_activeFlight?.IsSteal != true || _flightTarget == null))
                throw new Exception($"El robo no vuela hacia la mano de quien roba en {size}.");
            if (_flightTarget?.Modulate.A > 0.01f) throw new Exception("La carta robada aparece en la mano antes de llegar.");
            UpdateFlights(0.12); await Capture("hand-steal-pluck");
            UpdateFlights(0.22); await Capture("hand-steal-flight");
            for (var i = 0; i < 8; i++) { UpdateFlights(0.25); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            if (IsAnimating) throw new Exception("El robo deja una animación pendiente.");
            var landed = ZoneNode(seat == actor ? 0 : 1, CardZone.Hand, after.Players[actor].Hand.Length - 1);
            if (landed == null || landed.Modulate.A < 0.99f) throw new Exception("La carta robada no aparece al aterrizar.");
            if (seat == actor && _opponentCards.Any(c => !c.IsFaceDown))
                throw new Exception("El robo revela la mano del rival.");
        }
        _selected.Clear(); ResetAnimations();
        GD.Print("PVP_HAND_STEAL_PASS · abanico rival simétrico y robo animado en ambos asientos y tamaños.");
    }
}
