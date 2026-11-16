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
            async Task Layout() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            if (OS.GetCmdlineUserArgs().Contains("--matchmaking-only"))
            {
                await CheckMatchmakingAnimation(); await Layout(); GD.Print("PVP_MATCHMAKING_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--decks-only"))
            {
                await CheckDeckScreens(); await Layout(); GD.Print("PVP_DECKS_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--account-only"))
            {
                await CheckAccountScreens(); await Layout(); GD.Print("PVP_ACCOUNT_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--lobby-only"))
            {
                await CheckAccountScreens(); await CheckDeckScreens(); await CheckLobbyPolish(); await CheckMatchmakingAnimation();
                await Layout(); GD.Print("PVP_LOBBY_ONLY_PASS"); QuitWithAudio(); return;
            }
            var game = new GameEngine(42); game.ChooseInitialSkill(0, CardCatalog.Magnet.Id); game.ChooseInitialSkill(1, CardCatalog.Destiny.Id); game.Start([]);
            var online = new OnlineArena { Connection = new(), RoomCode = "UITEST00" }; AddChild(online); online.SetProcess(false);
            if (OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--choice-preview-dir=")) is { } choicePreview)
            {
                await online.CaptureChoicePreview(choicePreview.Split('=', 2)[1]); online.QueueFree(); await Layout();
                QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--board-preview-dir=")) is { } boardPreview)
            {
                await online.CaptureBoardPreview(boardPreview.Split('=', 2)[1]); online.QueueFree(); await Layout();
                GD.Print("PVP_BOARD_PREVIEW_DONE"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--dexterity-only"))
            {
                await online.CheckDexterityLayout(); online.QueueFree(); await Layout();
                GD.Print("PVP_DEXTERITY_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--stage-only"))
            {
                await online.CheckStageAnnouncements(); online.QueueFree(); await Layout();
                GD.Print("PVP_STAGE_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--connection-only"))
            {
                await CheckArenaConnectionNotice(online); online.Hide(); await CheckLobbyPolish();
                online.QueueFree(); await Layout(); GD.Print("PVP_CONNECTION_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--closure-only"))
            {
                await online.CheckDuelResults(); online.Hide(); await CheckLobbyPolish();
                online.QueueFree(); await Layout(); GD.Print("PVP_CLOSURE_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--preparation-only"))
            {
                await online.CheckPreparationLayout(); online.QueueFree(); await Layout();
                GC.Collect(); GC.WaitForPendingFinalizers(); await Layout();
                GD.Print("PVP_PREPARATION_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--combo-responses-only"))
            {
                await online.CheckComboResponseToggle(); online.QueueFree(); await Layout();
                GC.Collect(); GC.WaitForPendingFinalizers(); await Layout();
                GD.Print("PVP_COMBO_RESPONSES_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--actions-only"))
            {
                await online.CheckActionCounters(); online.QueueFree(); await Layout();
                GD.Print("PVP_ACTION_COUNTERS_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--skill-lesson-only"))
            {
                await online.CheckSkillLessonAnimation(); online.QueueFree(); await Layout();
                QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--equipment-animation-only"))
            {
                await online.CheckEquipmentAnimation(); online.QueueFree(); await Layout();
                QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--exchange-animation-only"))
            {
                await online.CheckExchangeAnimation(); online.QueueFree(); await Layout();
                QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--guidance-only"))
            {
                await online.CheckDecisionGuidance(); online.QueueFree(); await Layout();
                GD.Print("PVP_GUIDANCE_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--cosmetics-only"))
            {
                await online.CheckCosmetics(); online.QueueFree(); await Layout();
                GD.Print("PVP_COSMETICS_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--trap-recipe-only"))
            {
                await online.CheckTrapRecipe(); online.QueueFree(); await Layout();
                GD.Print("PVP_TRAP_RECIPE_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--polish-only"))
            {
                await online.CheckPolishEffects(); online.QueueFree(); await Layout();
                GD.Print("PVP_POLISH_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--board-reading-only"))
            {
                await online.CheckBoardReading(); online.QueueFree(); await Layout();
                GD.Print("PVP_BOARD_READING_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--feedback-only"))
            {
                await online.CheckDecisionFeedback(); online.QueueFree(); await Layout();
                GD.Print("PVP_FEEDBACK_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--inspector-only"))
            {
                await online.CheckCardInspector(); online.QueueFree(); await Layout();
                GD.Print("PVP_INSPECTOR_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--triple-attack-only"))
            {
                await online.CheckTripleAttackDraws(); online.QueueFree(); await Layout();
                GD.Print("PVP_TRIPLE_ATTACK_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--history-only"))
            {
                await online.CheckPublicHistory(); online.QueueFree(); await Layout();
                GD.Print("PVP_HISTORY_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--context-only"))
            {
                await online.CheckContextActionBar(); online.QueueFree(); await Layout();
                GD.Print("PVP_CONTEXT_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--memory-inspection-only"))
            {
                await online.CheckMemoryInspection(); online.QueueFree(); await Layout();
                GD.Print("PVP_MEMORY_INSPECTION_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--golden-totem-only"))
            {
                await online.CheckGoldenTotemLayout(); online.QueueFree(); await Layout();
                GD.Print("PVP_GOLDEN_TOTEM_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--super-magnet-only"))
            {
                await online.CheckSuperMagnetLayout(); online.QueueFree(); await Layout();
                QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--response-chain-only"))
            {
                await online.CheckResponseChain(); online.QueueFree(); await Layout();
                QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--zahira-only"))
            {
                await online.CheckZahiraLayout(); online.QueueFree(); await Layout();
                QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--super-attack-only"))
            {
                await online.CheckSuperAttackLayout(); online.QueueFree(); await Layout();
                QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--totem-only"))
            {
                await online.CheckTotemLayout(); online.QueueFree(); await Layout();
                GD.Print("PVP_TOTEM_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--time-bender-only"))
            {
                await online.CheckTimeBenderLayout(); online.QueueFree(); await Layout();
                GD.Print("PVP_TIME_BENDER_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--blue-egg-only"))
            {
                await online.CheckBlueEggDrawLayout(); online.QueueFree(); await Layout();
                GD.Print("PVP_BLUE_EGG_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--reload-cartridge-only"))
            {
                await online.CheckReloadCartridgeLayout(); online.QueueFree(); await Layout();
                GD.Print("PVP_RELOAD_CARTRIDGE_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--quick-load-only"))
            {
                await online.CheckQuickLoadLayout(); online.QueueFree(); await Layout();
                GD.Print("PVP_QUICK_LOAD_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--jessica-only"))
            {
                await online.CheckJessicaLayout(); online.QueueFree(); await Layout();
                GD.Print("PVP_JESSICA_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--star-only"))
            {
                await online.CheckPlumberStarLayout(); online.QueueFree(); await Layout();
                GD.Print("PVP_PLUMBER_STAR_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--memory-recovery-only"))
            {
                await online.CheckMemoryRecoveryLayout(); online.QueueFree(); await Layout();
                GD.Print("PVP_MEMORY_RECOVERY_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--steal-only"))
            {
                await online.CheckHandStealAnimation(); online.QueueFree(); await Layout();
                GD.Print("PVP_HAND_STEAL_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--reverse-only"))
            {
                await online.CheckReverseLayout(); online.QueueFree(); await Layout();
                GD.Print("PVP_REVERSE_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--evolution-only"))
            {
                await online.CheckYunoEvolutionLayout(); online.QueueFree(); await Layout();
                GD.Print("PVP_YUNO_EVOLUTION_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--mind-reset-only"))
            {
                await online.CheckMindResetLayout(); online.QueueFree(); await Layout();
                GD.Print("PVP_MIND_RESET_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--mind-swap-only"))
            {
                await online.CheckMindSwapLayout(); online.QueueFree(); await Layout();
                GD.Print("PVP_MIND_SWAP_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--portraits-only"))
            {
                GetWindow().ContentScaleSize = new(1440, 1024); await Layout();
                online.PresentTestSnapshot(DuelSnapshot.For(game, 0, 7)); await Layout();
                await online.CheckZeroHealthPortraits(game); await online.CheckHealingPortraits(game);
                online.QueueFree(); await Layout();
                GD.Print("PVP_PORTRAITS_ONLY_PASS"); QuitWithAudio(); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--deck-search-only"))
            {
                await online.CheckDeckSearchLayout(); online.QueueFree(); await Layout();
                GD.Print("PVP_DECK_SEARCH_ONLY_PASS"); QuitWithAudio(); return;
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
                // The desktop hand is lowered on purpose: only the effect box may drop below the edge.
                var firstCard = online.FindChild("HandCard0", true, false) as Control;
                if (ArenaViewport.Desktop(online)
                    ? firstCard == null || firstCard.GetGlobalRect().Position.Y > Size.Y - 100 || firstCard.GetGlobalRect().End.Y < Size.Y
                    : hand.GetGlobalRect().End.Y > Size.Y + 1) throw new Exception("La mano sale de la pantalla.");
                GD.Print($"PVP_ARENA_LAYOUT_PASS · {size} · asiento {seat}.");
            }
            GetWindow().ContentScaleSize = new(1440, 1024); await Layout();
            await online.CheckVisualCues(DuelSnapshot.For(game, 1, 8));
            await online.CheckComboResponseToggle();
            await online.CheckContextActionBar();
            await online.CheckDecisionGuidance();
            await online.CheckDecisionFeedback();
            await online.CheckBoardReading();
            await online.CheckPolishEffects();
            await online.CheckTrapRecipe();
            await online.CheckCosmetics();
            await online.CheckActionCounters();
            await online.CheckMemoryInspection();
            await online.CheckPublicHistory();
            await online.CheckResponseChain();
            await online.CheckTripleAttackDraws();
            await online.CheckSkillLessonAnimation();
            await online.CheckEquipmentAnimation();
            await online.CheckExchangeAnimation();
            await online.CheckZeroHealthPortraits(game);
            await online.CheckHealingPortraits(game);
            await online.CheckTimeBombLayout();
            await online.CheckDeckSearchLayout();
            await online.CheckMemoryRecoveryLayout();
            await online.CheckPlumberStarLayout();
            await online.CheckJessicaLayout();
            await online.CheckQuickLoadLayout();
            await online.CheckReloadCartridgeLayout();
            await online.CheckBlueEggDrawLayout();
            await online.CheckTimeBenderLayout();
            await online.CheckTotemLayout();
            await online.CheckGoldenTotemLayout();
            await online.CheckSuperMagnetLayout();
            await online.CheckZahiraLayout();
            await online.CheckSuperAttackLayout();
            await online.CheckMindResetLayout();
            await online.CheckMindSwapLayout();
            await online.CheckRecyclingLayout();
            await online.CheckHandLimitLayout();
            await online.CheckDexterityLayout();
            await online.CheckYunoEvolutionLayout();
            await online.CheckReverseLayout();
            await online.CheckHandStealAnimation();
            await online.CheckDuelResults();
            await online.CheckPreparationLayout();
            await online.CheckChoiceBounds();
            await CheckArenaConnectionNotice(online);
            await online.CheckStageAnnouncements();
            online.Hide(); await CheckLobbyPolish(); await CheckMatchmakingAnimation();
            online.QueueFree(); await Layout();
            GC.Collect(); GC.WaitForPendingFinalizers(); await Layout();
            GD.Print("PVP_UI_PASS · solo PvP, cuatro tamaños, ambos asientos, selección inicial, mulligan, recuperación y animaciones.");
            QuitWithAudio();
        }
        catch (Exception error) { GD.PushError("PVP_UI_FAIL · " + error); GetTree().Quit(1); }
    }
}
