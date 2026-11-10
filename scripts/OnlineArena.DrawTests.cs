using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckTripleAttackDraws()
    {
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        async Task Layout() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
        foreach (var actor in new[] { 0, 1 })
        foreach (var seat in new[] { 0, 1 })
        {
            var game = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Ana", "Beto"], StartingSeat = actor,
                Hands = [[CardCatalog.Senshuni.Id], [CardCatalog.Senshuni.Id]],
                Decks = [Enumerable.Repeat(CardCatalog.Attack.Id, 12).ToList(), Enumerable.Repeat(CardCatalog.Attack.Id, 12).ToList()],
                Bonuses = [null, null]
            }, new SeededRandom(53));
            game.State.Phase = Phase.Actions; game.State.Turn = 2;
            var player = game.State.Active;
            player.Hand.Clear(); player.Hand.AddRange([CardCatalog.TripleAttack.Id, CardCatalog.Senshuni.Id]);
            GetWindow().ContentScaleSize = size; await Layout();
            ResetAnimations(); var before = DuelSnapshot.For(game, seat, 800); PresentTestSnapshot(before); await Layout();
            var cues = DuelVisualEvents.Capture(game);
            Check(new DuelMove { Kind = DuelMoveKind.PlayCode, Index = 0 }.Apply(game, actor).Success, "No se activa Triple Ataque Cruzado.");
            var after = DuelSnapshot.For(game, seat, 801); after.Visuals = cues;
            var draws = cues.Where(c => c.Kind == DuelVisualKind.Draw).ToArray();
            Check(draws.Length == 3 && draws.Select(c => c.Value).SequenceEqual([1, 2, 3]), "Triple no entrega tres robos consecutivos tras la carta conservada.");
            var origins = CaptureOrigins(after, before);
            PresentTestSnapshot(after); Animate(cues, origins);
            var relative = actor == seat ? 0 : 1;
            Control Card(int slot) => ZoneNode(relative, CardZone.Hand, slot)!;
            Check(draws.All(c => Card(c.Value).Modulate.A == 0), "Los tres Ataques aparecen en la mano antes de salir del mazo.");
            Check(Card(0).Modulate.A == 1, "El robo oculta una carta que ya estaba en la mano.");
            await Layout();
            var inFlight = new HashSet<int>(); var frames = 0;
            while (IsAnimating && frames++ < 100)
            {
                UpdateFlights(0.1);
                var pending = _flights.Where(f => f.Event.Kind == DuelVisualKind.Draw).Select(f => f.Event.Value).ToHashSet();
                if (_activeFlight?.Event.Kind == DuelVisualKind.Draw)
                {
                    pending.Add(_activeFlight.Event.Value); inFlight.Add(_activeFlight.Event.Value);
                    Check(seat == actor ? _flightFront?.Card.Id == CardCatalog.Attack.Id : _flightFront == null,
                        "El vuelo pierde la carta propia o revela un Ataque de la mano rival.");
                }
                foreach (var draw in draws)
                    Check(Card(draw.Value).Modulate.A == (pending.Contains(draw.Value) ? 0 : 1),
                        "Un Ataque aparece antes de llegar o desaparece después de completar su vuelo.");
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            Check(!IsAnimating && draws.All(c => Card(c.Value).Modulate.A == 1), "El robo deja cartas invisibles o vuelos pendientes.");
            if (size.X == 1440) Check(inFlight.SetEquals([1, 2, 3]), "No se animan por separado los tres Ataques visibles.");
            Check(FindChildren("OpponentHandCard*", "Button", true, false).OfType<OpponentHandCard>().All(c => c.IsFaceDown), "El robo revela la mano rival.");

            Animate(cues, origins); UpdateFlights(0); UpdateFlights(0); UpdateFlights(0.1);
            ResetAnimations();
            Check(draws.All(c => Card(c.Value).Modulate.A == 1), "Interrumpir los robos deja cartas invisibles.");
            var played = PlayedVisualRevisions;
            PresentTestSnapshot(DuelSnapshot.For(GameEngine.FromSnapshot(game.SaveSnapshot(false), new SeededRandom(53)), seat, 801));
            await Layout();
            Check(!IsAnimating && PlayedVisualRevisions == played && draws.All(c => Card(c.Value).Modulate.A == 1),
                "Recuperar el tablero repite los robos o conserva cartas ocultas.");
        }
        GD.Print("PVP_TRIPLE_ATTACK_DRAWS_PASS · cola oculta desde el inicio, llegada individual, carta conservada, privacidad, interrupción y recuperación; cuatro tamaños y ambos jugadores/asientos.");
    }
}
