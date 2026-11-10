using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckActionCounters()
    {
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        async Task Layout() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(1280, 480), new(844, 390), new(700, 390) })
        foreach (var actor in new[] { 0, 1 })
        foreach (var seat in new[] { 0, 1 })
        {
            var hands = new List<string>[] { [], [] };
            hands[actor].AddRange(Enumerable.Repeat(CardCatalog.Senshuni.Id, 3));
            hands[1 - actor].Add(CardCatalog.LoadError.Id);
            var game = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Ana", "Beto"], StartingSeat = actor, Hands = hands,
                Decks = [Enumerable.Repeat(CardCatalog.Attack.Id, 20).ToList(), Enumerable.Repeat(CardCatalog.Attack.Id, 20).ToList()],
                Bonuses = [null, null]
            }, new SeededRandom(82));
            game.State.Turn = 2;
            GetWindow().ContentScaleSize = size; await Layout(); ResetAnimations();
            ulong revision = 900;
            async Task Show(int activeActions, int respondingActions)
            {
                Check(game.State.Players[actor].ActionsLeft == activeActions && game.State.Players[1 - actor].ActionsLeft == respondingActions,
                    $"La prueba no alcanza el presupuesto esperado en {game.State.Phase}: {game.State.Players[actor].ActionsLeft}/{game.State.Players[1 - actor].ActionsLeft}, esperado {activeActions}/{respondingActions}.");
                PresentTestSnapshot(DuelSnapshot.For(game, seat, revision++)); await Layout();
                var panel = (Control)_root!.FindChild("ActionCounters", true, false);
                var mine = (Label)panel.FindChild("OwnActionsCounter", true, false);
                var theirs = (Label)panel.FindChild("RivalActionsCounter", true, false);
                Check(mine.Text == $"TÚ {(seat == actor ? activeActions : respondingActions)}" &&
                    theirs.Text == $"RIVAL {(seat == actor ? respondingActions : activeActions)}",
                    "Las acciones siguen al dueño del turno o a quien responde, en vez de al jugador indicado.");
                Check(RectVisible(panel, panel.GetGlobalRect()) && new Rect2(Vector2.Zero, Size).Encloses(panel.GetGlobalRect()),
                    "El contador permanente queda oculto o fuera de pantalla.");
                CheckPendingCodeBounds();
                var position = panel.GlobalPosition;
                if (_arenaScroll != null) { _arenaScroll.ScrollVertical = 100000; await Layout(); }
                Check(panel.GlobalPosition == position && RectVisible(panel, panel.GetGlobalRect()), "Desplazar el tablero oculta el contador.");
                if (_arenaScroll != null) _arenaScroll.ScrollVertical = 0;
            }
            void Move(int player, DuelMoveKind kind) => Check(new DuelMove { Kind = kind, Index = 0 }.Apply(game, player).Success, "No avanza el escenario de acciones.");
            await Show(2, 2); // Combat still has the full budget, independently of the selected card.
            Move(actor, DuelMoveKind.Advance); await Show(2, 2);
            SelectCard(0); await Layout();
            Check(((Label)_root!.FindChild("OwnActionsCounter", true, false)).Text == "TÚ 2", "Seleccionar una carta descuenta acciones antes de confirmarla.");
            var directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
            if (directory != null && DisplayServer.GetName() != "headless" && actor == 0 && seat == 0 && size.X is 1440 or 700)
            {
                System.IO.Directory.CreateDirectory(directory);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, $"actions-{size.X}x{size.Y}.png"));
            }
            Move(actor, DuelMoveKind.PlayCode); await Show(1, 2);
            Check(game.State.Phase == Phase.ErrorResponse, "El contador no se prueba durante la respuesta rival.");
            Move(1 - actor, DuelMoveKind.PlayCode); await Show(2, 1); // Error refunds the original activation only.
            Move(actor, DuelMoveKind.PlayCode); await Show(1, 1);
            Move(actor, DuelMoveKind.PlayCode); await Show(0, 1);
            var hand = game.State.Players[actor].Hand;
            hand.AddRange(Enumerable.Repeat(CardCatalog.Attack.Id, GameEngine.MaxHandSize + 1 - hand.Count));
            Move(actor, DuelMoveKind.Advance); await Show(0, 1);
            Move(actor, DuelMoveKind.Discard); await Show(2, 2); // Both budgets reset after the final mandatory discard.
            var recovered = GameEngine.FromSnapshot(game.SaveSnapshot(false), new SeededRandom(82));
            PresentTestSnapshot(DuelSnapshot.For(recovered, seat, revision)); await Layout();
            Check(((Label)_root!.FindChild("OwnActionsCounter", true, false)).Text == "TÚ 2" && !IsAnimating,
                "Recuperar el duelo conserva un contador obsoleto o repite una animación.");
            var preparation = DuelSnapshot.For(recovered, seat, revision); preparation.Phase = Phase.Preparation;
            PresentTestSnapshot(preparation); await Layout();
            Check(_root!.FindChild("ActionCounters", true, false) == null, "La preparación muestra un presupuesto que aún no se puede usar.");
        }
        GD.Print("PVP_ACTION_COUNTERS_PASS · gasto, respuesta rival, devolución por Error, cero, reinicio de ambos jugadores, selección sin gasto, cabecera fija, recuperación y preparación; cinco tamaños y ambos jugadores/asientos.");
    }
}
