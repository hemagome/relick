using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineLobby
{
    private async void ArenaUiSmokeTest()
    {
        try
        {
            if (GetTree().CurrentScene != this || typeof(OnlineLobby).Assembly.GetType("Relick.Main") != null ||
                typeof(OnlineLobby).Assembly.GetType("Relick.Core.PracticeBot") != null ||
                typeof(OnlineLobby).Assembly.GetType("Relick.Core.SimulationDriver") != null)
                throw new Exception("El juego aún incluye el controlador o rival de práctica.");
            _content.Hide();
            var game = new GameEngine(42); game.ChooseInitialSkill(0, CardCatalog.Magnet.Id); game.ChooseInitialSkill(1, CardCatalog.Destiny.Id); game.Start([]);
            var online = new OnlineArena { Connection = new(), RoomCode = "UITEST00" }; AddChild(online); online.SetProcess(false);
            async Task Layout() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            if (OS.GetCmdlineUserArgs().Contains("--preparation-only"))
            {
                await online.CheckPreparationLayout(); online.QueueFree(); await Layout();
                GC.Collect(); GC.WaitForPendingFinalizers(); await Layout();
                GD.Print("PVP_PREPARATION_ONLY_PASS"); GetTree().Quit(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--combo-responses-only"))
            {
                await online.CheckComboResponseToggle(); online.QueueFree(); await Layout();
                GC.Collect(); GC.WaitForPendingFinalizers(); await Layout();
                GD.Print("PVP_COMBO_RESPONSES_ONLY_PASS"); GetTree().Quit(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--context-only"))
            {
                await online.CheckContextActionBar(); online.QueueFree(); await Layout();
                GD.Print("PVP_CONTEXT_ONLY_PASS"); GetTree().Quit(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--golden-totem-only"))
            {
                await online.CheckGoldenTotemLayout(); online.QueueFree(); await Layout();
                GD.Print("PVP_GOLDEN_TOTEM_ONLY_PASS"); GetTree().Quit(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--totem-only"))
            {
                await online.CheckTotemLayout(); online.QueueFree(); await Layout();
                GD.Print("PVP_TOTEM_ONLY_PASS"); GetTree().Quit(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--time-bender-only"))
            {
                await online.CheckTimeBenderLayout(); online.QueueFree(); await Layout();
                GD.Print("PVP_TIME_BENDER_ONLY_PASS"); GetTree().Quit(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--reload-cartridge-only"))
            {
                await online.CheckReloadCartridgeLayout(); online.QueueFree(); await Layout();
                GD.Print("PVP_RELOAD_CARTRIDGE_ONLY_PASS"); GetTree().Quit(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--quick-load-only"))
            {
                await online.CheckQuickLoadLayout(); online.QueueFree(); await Layout();
                GD.Print("PVP_QUICK_LOAD_ONLY_PASS"); GetTree().Quit(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--jessica-only"))
            {
                await online.CheckJessicaLayout(); online.QueueFree(); await Layout();
                GD.Print("PVP_JESSICA_ONLY_PASS"); GetTree().Quit(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--star-only"))
            {
                await online.CheckPlumberStarLayout(); online.QueueFree(); await Layout();
                GD.Print("PVP_PLUMBER_STAR_ONLY_PASS"); GetTree().Quit(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--memory-recovery-only"))
            {
                await online.CheckMemoryRecoveryLayout(); online.QueueFree(); await Layout();
                GD.Print("PVP_MEMORY_RECOVERY_ONLY_PASS"); GetTree().Quit(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--reverse-only"))
            {
                await online.CheckReverseLayout(); online.QueueFree(); await Layout();
                GD.Print("PVP_REVERSE_ONLY_PASS"); GetTree().Quit(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--evolution-only"))
            {
                await online.CheckYunoEvolutionLayout(); online.QueueFree(); await Layout();
                GD.Print("PVP_YUNO_EVOLUTION_ONLY_PASS"); GetTree().Quit(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--mind-reset-only"))
            {
                await online.CheckMindResetLayout(); online.QueueFree(); await Layout();
                GD.Print("PVP_MIND_RESET_ONLY_PASS"); GetTree().Quit(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--mind-swap-only"))
            {
                await online.CheckMindSwapLayout(); online.QueueFree(); await Layout();
                GD.Print("PVP_MIND_SWAP_ONLY_PASS"); GetTree().Quit(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--deck-search-only"))
            {
                await online.CheckDeckSearchLayout(); online.QueueFree(); await Layout();
                GD.Print("PVP_DECK_SEARCH_ONLY_PASS"); GetTree().Quit(); return;
            }
            foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
            foreach (var seat in new[] { 0, 1 })
            {
                GetWindow().ContentScaleSize = size; await Layout();
                online.PresentTestSnapshot(DuelSnapshot.For(game, seat, 7)); await Layout();
                var root = (Control)online.FindChild("DuelInterface", true, false);
                var minimum = root.GetCombinedMinimumSize();
                if (minimum.X > root.Size.X + 1 || minimum.Y > root.Size.Y + 1) throw new Exception($"Arena desborda {size}: {minimum}, raíz {root.Size}.");
                if (online.FindChildren("OpponentHandCard*", "Button", true, false).OfType<OpponentHandCard>().Any(c => !c.IsFaceDown)) throw new Exception("Se revela la mano rival.");
                if (online.FindChildren("*", "Button", true, false).OfType<Button>().Any(b => b.Text.Contains("práctica"))) throw new Exception("Queda un acceso al modo retirado.");
                var hand = (ScrollContainer)online.FindChild("HandScroll", true, false);
                if (hand.GetGlobalRect().End.Y > Size.Y + 1) throw new Exception("La mano sale de la pantalla.");
                GD.Print($"PVP_ARENA_LAYOUT_PASS · {size} · asiento {seat}.");
            }
            GetWindow().ContentScaleSize = new(1440, 1024); await Layout();
            await online.CheckVisualCues(DuelSnapshot.For(game, 1, 8));
            await online.CheckComboResponseToggle();
            await online.CheckContextActionBar();
            await online.CheckZeroHealthPortraits(game);
            await online.CheckHealingPortraits(game);
            await online.CheckTimeBombLayout();
            await online.CheckDeckSearchLayout();
            await online.CheckMemoryRecoveryLayout();
            await online.CheckPlumberStarLayout();
            await online.CheckJessicaLayout();
            await online.CheckQuickLoadLayout();
            await online.CheckReloadCartridgeLayout();
            await online.CheckTimeBenderLayout();
            await online.CheckTotemLayout();
            await online.CheckGoldenTotemLayout();
            await online.CheckMindResetLayout();
            await online.CheckMindSwapLayout();
            await online.CheckRecyclingLayout();
            await online.CheckHandLimitLayout();
            await online.CheckDexterityLayout();
            await online.CheckYunoEvolutionLayout();
            await online.CheckReverseLayout();
            await online.CheckPreparationLayout();
            online.QueueFree(); await Layout();
            GC.Collect(); GC.WaitForPendingFinalizers(); await Layout();
            GD.Print("PVP_UI_PASS · solo PvP, cuatro tamaños, ambos asientos, selección inicial, mulligan, recuperación y animaciones.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError("PVP_UI_FAIL · " + error); GetTree().Quit(1); }
    }
}
