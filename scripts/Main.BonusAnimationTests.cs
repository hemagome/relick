using Godot;
using Relick.Core;

namespace Relick;

public partial class Main
{
    private async void BonusAnimationSmokeTest()
    {
        try
        {
            _helpOpen = true;
            SetProcess(false);
            void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
            async Task Layout()
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                _drawLayoutFrames = 0;
            }
            async Task Drain()
            {
                for (var i = 0; CardsInMotion && i < 20; i++)
                {
                    await Layout();
                    UpdateDrawAnimations(1);
                }
                Check(!CardsInMotion, "La cola de animaciones no termina.");
            }
            BindGame(new GameEngine(42));
            Execute(_game.Start(Array.Empty<int>()));
            await Layout();
            Check(_drawQueue.Count(d => d.BonusDestination) == 2, "Falta la colocación inicial de Bonus.");
            UpdateDrawAnimations(0.2);
            Check(_flyingFront != null && !_flyingFront.Visible && _flyingBack!.Visible, "El Bonus del mazo debe empezar boca abajo.");
            UpdateDrawAnimations(0.3);
            Check(_flyingFront!.Visible, "El nuevo Bonus no se revela al llegar.");
            await Drain();

            // Both players claim through exchange, including an identical replacement card.
            foreach (var actor in new[] { 0, 1 })
            {
                _game.State.CurrentPlayer = actor;
                _game.State.Phase = Phase.Actions;
                _game.State.ActionsLeft = 2;
                var p = _game.State.Players[actor];
                var oldBonus = p.Bonus!;
                p.Deck.Insert(0, oldBonus);
                Execute(_game.Exchange(actor, 0));
                Check(!CardsInMotion, "Un canje incompleto anima un Bonus.");
                Execute(_game.Exchange(actor, 0));
                await Layout();
                Check(_drawQueue.Take(2).All(d => d.PileDestination == CardZone.Memory), "El canje no envía ambas cartas a Memoria antes de reclamar el Bonus.");
                UpdateDrawAnimations(1); UpdateDrawAnimations(1);
                Check(_drawQueue.Count == 2 && _drawQueue.Peek().BonusSource == actor,
                    "El reclamo y la reposición no están en orden.");
                Check(BonusTarget(actor)!.Modulate.A == 0 && DrawTarget(_drawQueue.Peek())!.Modulate.A == 0,
                    "Las cartas aparecen antes de llegar.");
                UpdateDrawAnimations(0.1);
                Check(_flyingFront?.Card.Id == oldBonus && _flyingFront.Visible, "El reclamo no muestra el Bonus original.");
                var progress = _drawTime;
                Render(); await Layout();
                Check(_drawTime == progress, "Renderizar reinicia el vuelo del Bonus.");
                UpdateDrawAnimations(0.4);
                Check(_flyingFront!.Visible == (actor == 0), "El Bonus rival no se oculta al entrar en su mano.");
                await Drain();
                Check(p.Bonus == oldBonus && p.Hand[^1] == oldBonus && BonusTarget(actor)!.Modulate.A == 1,
                    "La reposición idéntica deja un Bonus invisible o cambia su identidad.");
            }

            _game.State.CurrentPlayer = 0; _game.State.Phase = Phase.Actions;
            _game.State.ActionsLeft = 2;
            _game.State.Players[0].Relikia = 1000;
            Execute(_game.UseRelikiaEffect(0, RelikiaEffect.ClaimBonus));
            await Layout();
            Check(_drawQueue.Count == 2 && _drawQueue.Peek().BonusSource == 0, "Relikia no anima el reclamo.");
            UpdateDrawAnimations(0.2);
            NewGame(); await Layout();
            Check(_drawQueue.Count == 10 && _activeDraw == null && _flyingCard == null, "Reiniciar conserva el vuelo del Bonus o no reparte la nueva mano.");
            GD.Print("BONUS_ANIMATION_PASS · colocación inicial, canje, reclamo PR, orden, identidad, privacidad, render y reinicio.");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
