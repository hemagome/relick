using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckActionCounters()
    {
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        async Task Layout() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        var originalMotion = _actionPools.Select(pool => pool.ReducedMotion).ToArray();
        foreach (var pool in _actionPools) pool.ReducedMotion = false;
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
            GetWindow().Size = size; GetWindow().ContentScaleSize = size; await Layout(); ResetAnimations();
            ulong revision = 900;
            var directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
            async Task Capture(string name)
            {
                if (directory == null || DisplayServer.GetName() == "headless" || actor != 0 || seat != 0 || size.X is not (1440 or 700)) return;
                System.IO.Directory.CreateDirectory(directory);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, $"{name}-{size.X}x{size.Y}.png"));
            }
            async Task Show(int activeActions, int respondingActions,
                ActionCrystalChange actorChange = ActionCrystalChange.None, ActionCrystalChange responderChange = ActionCrystalChange.None,
                bool animate = true)
            {
                Check(game.State.Players[actor].ActionsLeft == activeActions && game.State.Players[1 - actor].ActionsLeft == respondingActions,
                    $"La prueba no alcanza el presupuesto esperado en {game.State.Phase}: {game.State.Players[actor].ActionsLeft}/{game.State.Players[1 - actor].ActionsLeft}, esperado {activeActions}/{respondingActions}.");
                foreach (var pool in _actionPools) pool.Settle();
                PresentTestSnapshot(DuelSnapshot.For(game, seat, revision++), animateActions: animate);
                foreach (var pool in _actionPools) pool.SetProcess(false);
                await Layout();
                var mine = _actionPools[0];
                var theirs = _actionPools[1];
                Check(mine.Amount == (seat == actor ? activeActions : respondingActions) &&
                    theirs.Amount == (seat == actor ? respondingActions : activeActions),
                    "Las acciones siguen al dueño del turno o a quien responde, en vez de al jugador indicado.");
                Check(mine.Change == (seat == actor ? actorChange : responderChange) &&
                    theirs.Change == (seat == actor ? responderChange : actorChange),
                    "El gasto, devolución o recarga se anima para otro jugador.");
                Render();
                await Layout();
                Check(mine.Change == (seat == actor ? actorChange : responderChange) &&
                    theirs.Change == (seat == actor ? responderChange : actorChange),
                    "Reconstruir el tablero reinicia la animación de los cristales.");
                foreach (var (pool, panelName) in new[] { (mine, "OwnRelikarioPanel"), (theirs, "EnemyRelikarioPanel") })
                {
                    var panel = (Control)_root!.FindChild(panelName, true, false);
                    Check((Desktop || panel.IsAncestorOf(pool)) && panel.GetGlobalRect().Encloses(pool.GetGlobalRect()) && pool.IsVisibleInTree(),
                        $"Los cristales no están dentro de su relikario ({panelName}) en {size}.");
                    if (!animate)
                    {
                        _arenaScroll?.EnsureControlVisible(pool);
                        await Layout();
                        Check(RectVisible(pool, pool.GetGlobalRect()) && new Rect2(Vector2.Zero, Size).Encloses(pool.GetGlobalRect()),
                            $"Los cristales de {panelName} no quedan a la vista al llegar a su relikario en {size}.");
                    }
                }
                if (!animate && _arenaScroll != null)
                {
                    _arenaScroll.ScrollVertical = 0;
                    await Layout();
                }
                CheckPendingCodeBounds();
                if (actorChange != ActionCrystalChange.None || responderChange != ActionCrystalChange.None)
                {
                    foreach (var pool in _actionPools) pool._Process(0.25);
                    await Capture($"actions-{actorChange.ToString().ToLowerInvariant()}-{activeActions}-{respondingActions}");
                    foreach (var pool in _actionPools) pool._Process(1);
                    Check(_actionPools.All(pool => pool.Change == ActionCrystalChange.None && !pool.IsProcessing()),
                        "Los cristales siguen procesando cuando termina su efecto.");
                }
            }
            void Move(int player, DuelMoveKind kind) => Check(new DuelMove { Kind = kind, Index = 0 }.Apply(game, player).Success, "No avanza el escenario de acciones.");
            await Show(2, 2, animate: false); // Combat still has the full budget, independently of the selected card.
            Move(actor, DuelMoveKind.Advance); await Show(2, 2);
            SelectCard(0); await Layout();
            Check(_actionPools[0].Amount == 2 && _actionPools[0].Change == ActionCrystalChange.None,
                "Seleccionar una carta descuenta o anima acciones antes de confirmarla.");
            await Capture("actions-ready");
            Move(actor, DuelMoveKind.PlayCode); await Show(1, 2, ActionCrystalChange.Spend);
            Check(game.State.Phase == Phase.ErrorResponse, "El contador no se prueba durante la respuesta rival.");
            Move(1 - actor, DuelMoveKind.PlayCode); await Show(2, 1, ActionCrystalChange.Refund, ActionCrystalChange.Spend); // Error refunds the original activation only.
            Move(actor, DuelMoveKind.PlayCode); await Show(1, 1, ActionCrystalChange.Spend);
            Move(actor, DuelMoveKind.PlayCode); await Show(0, 1, ActionCrystalChange.Spend);
            var hand = game.State.Players[actor].Hand;
            hand.AddRange(Enumerable.Repeat(CardCatalog.Attack.Id, GameEngine.MaxHandSize + 1 - hand.Count));
            Move(actor, DuelMoveKind.Advance); await Show(0, 1);
            Move(actor, DuelMoveKind.Discard); await Show(2, 2, ActionCrystalChange.Recharge, ActionCrystalChange.Recharge); // Both budgets reset after the final mandatory discard.
            var recovered = GameEngine.FromSnapshot(game.SaveSnapshot(false), new SeededRandom(82));
            PresentTestSnapshot(DuelSnapshot.For(recovered, seat, revision)); await Layout();
            Check(_actionPools[0].Amount == 2 && _actionPools.All(pool => pool.Change == ActionCrystalChange.None) && !IsAnimating,
                "Recuperar el duelo conserva un contador obsoleto o repite una animación.");
            var relikia = PendingCodeFixture(actor, CardCatalog.Attack.Id);
            relikia.State.Active.Relikia = 500;
            relikia.State.Active.Memory.Add(CardCatalog.Senshuni.Id);
            PresentTestSnapshot(DuelSnapshot.For(relikia, seat, revision++));
            Check(relikia.UseRelikiaEffect(actor, RelikiaEffect.ReplayCode, 0).Success, "No se puede activar Relikia para comprobar el gasto doble.");
            PresentTestSnapshot(DuelSnapshot.For(relikia, seat, revision++), animateActions: true);
            Check(_actionPools[seat == actor ? 0 : 1] is { Amount: 0, Change: ActionCrystalChange.Spend },
                "Activar Relikia no consume los dos cristales del jugador correcto.");
            foreach (var pool in _actionPools) { pool.SetProcess(false); pool._Process(0.25); }
            await Capture("actions-relikia");
            var ownPool = _actionPools[0];
            ownPool.Settle();
            ownPool.ReducedMotion = true;
            ownPool.Present(2, "Acciones propias", animate: true, recharge: true);
            Check(ownPool.Change == ActionCrystalChange.None && !ownPool.IsProcessing(), "El modo de movimiento reducido reproduce la recarga.");
            ownPool.ReducedMotion = false;
            ownPool.Present(2, "Acciones propias", animate: true, recharge: true);
            Check(ownPool.Change == ActionCrystalChange.Recharge, "El cambio de turno omite la recarga cuando quedaban las dos acciones.");
            ownPool._Process(1);
            var preparation = DuelSnapshot.For(recovered, seat, revision); preparation.Phase = Phase.Preparation;
            PresentTestSnapshot(preparation); await Layout();
            Check(_actionPools.All(pool => !pool.IsVisibleInTree()), "La preparación muestra un presupuesto que aún no se puede usar.");
        }
        for (var index = 0; index < _actionPools.Length; index++) _actionPools[index].ReducedMotion = originalMotion[index];
        GD.Print("PVP_ACTION_COUNTERS_PASS · cristales por relikario, gasto, Relikia, devolución por Error, recarga, selección sin gasto, recuperación y preparación; cinco tamaños y ambos jugadores/asientos.");
    }
}
