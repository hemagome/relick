using System.Diagnostics;
using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineLobby
{
    /// <summary>
    /// The old focused flags, each mapped to the checks it runs and the marker it prints on success.
    /// Every check also runs on its own with <c>--checks=Name,Name</c>; <c>--list-checks</c> prints the names.
    /// </summary>
    private static readonly Dictionary<string, (string? Marker, string[] Checks)> UiTestFlags = new()
    {
        ["--matchmaking-only"] = ("PVP_MATCHMAKING_ONLY_PASS", ["MatchmakingAnimation"]),
        ["--decks-only"] = ("PVP_DECKS_ONLY_PASS", ["DeckScreens"]),
        ["--style-only"] = ("PVP_STYLE_ONLY_PASS", ["MenuStyle"]),
        ["--account-only"] = ("PVP_ACCOUNT_ONLY_PASS", ["AccountScreens"]),
        ["--lobby-only"] = ("PVP_LOBBY_ONLY_PASS", ["AccountScreens", "DeckScreens", "LobbyPolish", "MatchmakingAnimation", "MenuStyle"]),
        ["--osamu-only"] = ("PVP_OSAMU_ONLY_PASS", ["OsamuSearchLayout", "OsamuPresentation"]),
        ["--table-view-only"] = (null, ["TableView"]),
        ["--dexterity-only"] = ("PVP_DEXTERITY_ONLY_PASS", ["DexterityLayout"]),
        ["--stage-only"] = ("PVP_STAGE_ONLY_PASS", ["StageAnnouncements"]),
        ["--connection-only"] = ("PVP_CONNECTION_ONLY_PASS", ["ArenaConnectionNotice", "LobbyPolish"]),
        ["--closure-only"] = ("PVP_CLOSURE_ONLY_PASS", ["DuelResults", "LobbyPolish"]),
        ["--preparation-only"] = ("PVP_PREPARATION_ONLY_PASS", ["PreparationLayout"]),
        ["--opening-reveal-only"] = (null, ["OpeningReveal"]),
        ["--opening-playmats-only"] = (null, ["OpeningPlaymats"]),
        ["--combo-responses-only"] = ("PVP_COMBO_RESPONSES_ONLY_PASS", ["ComboResponseToggle"]),
        ["--actions-only"] = ("PVP_ACTION_COUNTERS_ONLY_PASS", ["ActionCounters"]),
        ["--skill-lesson-only"] = (null, ["SkillLessonAnimation"]),
        ["--equipment-animation-only"] = (null, ["EquipmentAnimation"]),
        ["--exchange-animation-only"] = (null, ["ExchangeAnimation"]),
        ["--guard-animation-only"] = (null, ["GuardAnimation"]),
        ["--discard-animation-only"] = (null, ["DiscardAnimation"]),
        ["--turn-core-only"] = ("PVP_TURN_CORE_ONLY_PASS", ["TurnCore"]),
        ["--rail-only"] = ("PVP_RAIL_ONLY_PASS", ["AdaptiveRail"]),
        ["--guidance-only"] = ("PVP_GUIDANCE_ONLY_PASS", ["DecisionGuidance"]),
        ["--profile-only"] = ("PVP_PROFILE_ONLY_PASS", ["Profiles"]),
        ["--cosmetics-only"] = ("PVP_COSMETICS_ONLY_PASS", ["Cosmetics"]),
        ["--trap-recipe-only"] = ("PVP_TRAP_RECIPE_ONLY_PASS", ["TrapRecipe"]),
        ["--polish-only"] = ("PVP_POLISH_ONLY_PASS", ["PolishEffects"]),
        ["--board-reading-only"] = ("PVP_BOARD_READING_ONLY_PASS", ["BoardReading"]),
        ["--feedback-only"] = ("PVP_FEEDBACK_ONLY_PASS", ["DecisionFeedback"]),
        ["--take-equipment-only"] = (null, ["TakeEquipmentLayout"]),
        ["--equipment-raid-only"] = (null, ["EquipmentRaidLayout"]),
        ["--purple-ring-only"] = (null, ["PurpleRingLayout"]),
        ["--galactic-shield-only"] = (null, ["GalacticShieldLayout"]),
        ["--glass-flute-only"] = (null, ["GlassFluteReturnLayout"]),
        ["--victorious-manual-control-only"] = (null, ["VictoriousManualControlLayout"]),
        ["--ellie-only"] = (null, ["EllieLayout"]),
        ["--new-cards-only"] = (null, ["NewCardsLayout"]),
        ["--robotize-only"] = (null, ["RobotizeLayout"]),
        ["--combo-parry-only"] = (null, ["ComboParryLayout"]),
        ["--metal-hood-only"] = (null, ["MetalHoodLayout"]),
        ["--destiny-pick-only"] = (null, ["DestinyPickLayout"]),
        ["--inspector-only"] = ("PVP_INSPECTOR_ONLY_PASS", ["CardInspector"]),
        ["--triple-attack-only"] = ("PVP_TRIPLE_ATTACK_ONLY_PASS", ["TripleAttackDraws"]),
        ["--history-only"] = ("PVP_HISTORY_ONLY_PASS", ["PublicHistory"]),
        ["--context-only"] = ("PVP_CONTEXT_ONLY_PASS", ["ContextActionBar"]),
        ["--memory-inspection-only"] = ("PVP_MEMORY_INSPECTION_ONLY_PASS", ["MemoryInspection"]),
        ["--golden-totem-only"] = ("PVP_GOLDEN_TOTEM_ONLY_PASS", ["GoldenTotemLayout"]),
        ["--super-magnet-only"] = (null, ["SuperMagnetLayout"]),
        ["--response-chain-only"] = (null, ["ResponseChain"]),
        ["--zahira-only"] = (null, ["ZahiraLayout"]),
        ["--super-attack-only"] = (null, ["SuperAttackLayout"]),
        ["--totem-only"] = ("PVP_TOTEM_ONLY_PASS", ["TotemLayout"]),
        ["--time-bender-only"] = ("PVP_TIME_BENDER_ONLY_PASS", ["TimeBenderLayout"]),
        ["--blue-egg-only"] = ("PVP_BLUE_EGG_ONLY_PASS", ["BlueEggDrawLayout"]),
        ["--reload-cartridge-only"] = ("PVP_RELOAD_CARTRIDGE_ONLY_PASS", ["ReloadCartridgeLayout"]),
        ["--quick-load-only"] = ("PVP_QUICK_LOAD_ONLY_PASS", ["QuickLoadLayout"]),
        ["--jessica-only"] = ("PVP_JESSICA_ONLY_PASS", ["JessicaLayout"]),
        ["--star-only"] = ("PVP_PLUMBER_STAR_ONLY_PASS", ["PlumberStarLayout"]),
        ["--memory-recovery-only"] = ("PVP_MEMORY_RECOVERY_ONLY_PASS", ["MemoryRecoveryLayout"]),
        ["--steal-only"] = ("PVP_HAND_STEAL_ONLY_PASS", ["HandStealAnimation"]),
        ["--reverse-only"] = ("PVP_REVERSE_ONLY_PASS", ["ReverseLayout"]),
        ["--evolution-only"] = ("PVP_YUNO_EVOLUTION_ONLY_PASS", ["EvolutionReveal", "YunoEvolutionLayout"]),
        ["--mind-reset-only"] = ("PVP_MIND_RESET_ONLY_PASS", ["MindResetLayout"]),
        ["--mind-swap-only"] = ("PVP_MIND_SWAP_ONLY_PASS", ["MindSwapLayout"]),
        ["--portraits-only"] = ("PVP_PORTRAITS_ONLY_PASS", ["ZeroHealthPortraits", "HealingPortraits", "IronwingPortraits"]),
        ["--deck-search-only"] = ("PVP_DECK_SEARCH_ONLY_PASS", ["DeckSearchLayout"]),
    };

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
            var args = OS.GetCmdlineUserArgs();
            var game = new GameEngine(42); game.ChooseInitialSkill(0, CardCatalog.Magnet.Id); game.ChooseInitialSkill(1, CardCatalog.Destiny.Id); game.Start([]);

            // The arena is created on first use, so the lobby checks run without it when they run alone.
            OnlineArena? arena = null;
            OnlineArena Arena()
            {
                if (arena == null)
                {
                    arena = new OnlineArena { Connection = new(), RoomCode = "UITEST00", BoardSettingsPath = IsolatedBoardSettings };
                    AddChild(arena); arena.SetProcess(false);
                }
                arena.Show();
                return arena;
            }
            void Lobby() => arena?.Hide();
            // The portraits start from the opening board, whatever the previous check left on screen.
            async Task<OnlineArena> OpeningBoard()
            {
                var online = Arena();
                GetWindow().ContentScaleSize = new(1440, 1024); await Layout();
                online.PresentTestSnapshot(DuelSnapshot.For(game, 0, 7)); await Layout();
                return online;
            }

            if (args.FirstOrDefault(arg => arg.StartsWith("--choice-preview-dir=")) is { } choicePreview)
            {
                await Arena().CaptureChoicePreview(choicePreview.Split('=', 2)[1]); arena!.QueueFree(); await Layout();
                QuitWithAudio(); return;
            }
            if (args.FirstOrDefault(arg => arg.StartsWith("--board-preview-dir=")) is { } boardPreview)
            {
                await Arena().CaptureBoardPreview(boardPreview.Split('=', 2)[1]); arena!.QueueFree(); await Layout();
                GD.Print("PVP_BOARD_PREVIEW_DONE"); QuitWithAudio(); return;
            }

            // The full matrix, in order. Each check must pass on its own too: tools run them split across processes.
            var checks = new (string Name, Func<Task> Run)[]
            {
                ("ArenaLayout", async () =>
                {
                    var online = Arena();
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
                }),
                ("VisualCues", () => Arena().CheckVisualCues(DuelSnapshot.For(game, 1, 8))),
                ("ComboResponseToggle", () => Arena().CheckComboResponseToggle()),
                ("ContextActionBar", () => Arena().CheckContextActionBar()),
                ("DecisionGuidance", () => Arena().CheckDecisionGuidance()),
                ("DecisionFeedback", () => Arena().CheckDecisionFeedback()),
                ("TurnCore", () => Arena().CheckTurnCore()),
                ("AdaptiveRail", () => Arena().CheckAdaptiveRail()),
                ("BoardReading", () => Arena().CheckBoardReading()),
                ("PolishEffects", () => Arena().CheckPolishEffects()),
                ("TrapRecipe", () => Arena().CheckTrapRecipe()),
                ("Cosmetics", () => Arena().CheckCosmetics()),
                ("Profiles", () => Arena().CheckProfiles()),
                ("ActionCounters", () => Arena().CheckActionCounters()),
                ("MemoryInspection", () => Arena().CheckMemoryInspection()),
                ("PublicHistory", () => Arena().CheckPublicHistory()),
                ("ResponseChain", () => Arena().CheckResponseChain()),
                ("TripleAttackDraws", () => Arena().CheckTripleAttackDraws()),
                ("SkillLessonAnimation", () => Arena().CheckSkillLessonAnimation()),
                ("EquipmentAnimation", () => Arena().CheckEquipmentAnimation()),
                ("ExchangeAnimation", () => Arena().CheckExchangeAnimation()),
                ("DiscardAnimation", () => Arena().CheckDiscardAnimation()),
                ("GuardAnimation", () => Arena().CheckGuardAnimation()),
                ("ZeroHealthPortraits", async () => await (await OpeningBoard()).CheckZeroHealthPortraits(game)),
                ("HealingPortraits", async () => await (await OpeningBoard()).CheckHealingPortraits(game)),
                ("IronwingPortraits", () => { Arena().CheckIronwingPortraits(); return Task.CompletedTask; }),
                ("TimeBombLayout", () => Arena().CheckTimeBombLayout()),
                ("TakeEquipmentLayout", () => Arena().CheckTakeEquipmentLayout()),
                ("EquipmentRaidLayout", () => Arena().CheckEquipmentRaidLayout()),
                ("PurpleRingLayout", () => Arena().CheckPurpleRingLayout()),
                ("GalacticShieldLayout", () => Arena().CheckGalacticShieldLayout()),
                ("VictoriousManualControlLayout", () => Arena().CheckVictoriousManualControlLayout()),
                ("GlassFluteReturnLayout", () => Arena().CheckGlassFluteReturnLayout()),
                ("EllieLayout", () => Arena().CheckEllieLayout()),
                ("RobotizeLayout", () => Arena().CheckRobotizeLayout()),
                ("NewCardsLayout", () => Arena().CheckNewCardsLayout()),
                ("ComboParryLayout", () => Arena().CheckComboParryLayout()),
                ("MetalHoodLayout", () => Arena().CheckMetalHoodLayout()),
                ("DestinyPickLayout", () => Arena().CheckDestinyPickLayout()),
                ("DeckSearchLayout", () => Arena().CheckDeckSearchLayout()),
                ("OsamuSearchLayout", () => Arena().CheckOsamuSearchLayout()),
                ("OsamuPresentation", () => Arena().CheckOsamuPresentation()),
                ("YunoOroPortrait", () => Arena().CheckYunoOroPortrait()),
                ("MemoryRecoveryLayout", () => Arena().CheckMemoryRecoveryLayout()),
                ("PlumberStarLayout", () => Arena().CheckPlumberStarLayout()),
                ("JessicaLayout", () => Arena().CheckJessicaLayout()),
                ("QuickLoadLayout", () => Arena().CheckQuickLoadLayout()),
                ("ReloadCartridgeLayout", () => Arena().CheckReloadCartridgeLayout()),
                ("BlueEggDrawLayout", () => Arena().CheckBlueEggDrawLayout()),
                ("TimeBenderLayout", () => Arena().CheckTimeBenderLayout()),
                ("TotemLayout", () => Arena().CheckTotemLayout()),
                ("GoldenTotemLayout", () => Arena().CheckGoldenTotemLayout()),
                ("SuperMagnetLayout", () => Arena().CheckSuperMagnetLayout()),
                ("ZahiraLayout", () => Arena().CheckZahiraLayout()),
                ("SuperAttackLayout", () => Arena().CheckSuperAttackLayout()),
                ("MindResetLayout", () => Arena().CheckMindResetLayout()),
                ("MindSwapLayout", () => Arena().CheckMindSwapLayout()),
                ("RecyclingLayout", () => Arena().CheckRecyclingLayout()),
                ("HandLimitLayout", () => Arena().CheckHandLimitLayout()),
                ("DexterityLayout", () => Arena().CheckDexterityLayout()),
                ("EvolutionReveal", () => Arena().CheckEvolutionReveal()),
                ("TableView", () => Arena().CheckTableView()),
                ("YunoEvolutionLayout", () => Arena().CheckYunoEvolutionLayout()),
                ("ReverseLayout", () => Arena().CheckReverseLayout()),
                ("HandStealAnimation", () => Arena().CheckHandStealAnimation()),
                ("DuelResults", () => Arena().CheckDuelResults()),
                ("OpeningReveal", () => Arena().CheckOpeningReveal()),
                ("OpeningPlaymats", () => Arena().CheckOpeningPlaymats()),
                ("PreparationLayout", () => Arena().CheckPreparationLayout()),
                ("ChoiceBounds", () => Arena().CheckChoiceBounds()),
                ("ArenaConnectionNotice", () => CheckArenaConnectionNotice(Arena())),
                ("StageAnnouncements", () => Arena().CheckStageAnnouncements()),
                // Runs inside ContextActionBar in the full matrix; listed so that --inspector-only can reach it.
                ("CardInspector", () => Arena().CheckCardInspector()),
                ("LobbyPolish", () => { Lobby(); return CheckLobbyPolish(); }),
                ("MatchmakingAnimation", () => { Lobby(); return CheckMatchmakingAnimation(); }),
                ("MenuStyle", () => { Lobby(); return CheckMenuStyle(); }),
                ("AccountScreens", () => { Lobby(); return CheckAccountScreens(); }),
                ("LegalNotice", () => { Lobby(); return CheckLegalNotice(); }),
                ("DeckScreens", () => { Lobby(); return CheckDeckScreens(); }),
            };
            var names = checks.Select(check => check.Name).ToArray();
            if (args.Contains("--list-checks"))
            {
                foreach (var name in names.Where(name => name != "CardInspector")) GD.Print("PVP_UI_CHECK_NAME · " + name);
                GetTree().Quit(); return;
            }

            // A misspelled filter must fail instead of silently running the whole matrix.
            foreach (var arg in args.Where(arg => arg.StartsWith("--") && !UiTestFlags.ContainsKey(arg)))
            {
                var near = UiTestFlags.Keys.FirstOrDefault(known => arg.EndsWith("-only") || EditDistance(arg, known) <= 2);
                if (near != null) throw new Exception($"Filtro desconocido: {arg}." + (arg.EndsWith("-only") ? "" : $" ¿Querías {near}?"));
            }
            var flag = args.FirstOrDefault(UiTestFlags.ContainsKey);
            var requested = args.FirstOrDefault(arg => arg.StartsWith("--checks="))?.Split('=', 2)[1]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (requested?.FirstOrDefault(name => !names.Contains(name)) is { } unknown)
                throw new Exception($"Comprobación desconocida: {unknown}. Usa --list-checks para ver los nombres.");
            var selected = flag != null ? UiTestFlags[flag].Checks.ToHashSet()
                : requested?.ToHashSet() ?? names.Where(name => name != "CardInspector").ToHashSet();

            foreach (var (name, run) in checks.Where(check => selected.Contains(check.Name)))
            {
                var clock = Stopwatch.StartNew();
                await run();
                GD.Print($"PVP_UI_CHECK · {name} · {clock.ElapsedMilliseconds} ms");
            }
            arena?.QueueFree(); await Layout();
            GC.Collect(); GC.WaitForPendingFinalizers(); await Layout();
            if (flag != null) { if (UiTestFlags[flag].Marker is { } marker) GD.Print(marker); }
            else if (requested != null) GD.Print($"PVP_UI_CHECKS_PASS · {selected.Count} comprobaciones.");
            else GD.Print("PVP_UI_PASS · solo PvP, cuatro tamaños, ambos asientos, selección inicial, mulligan, recuperación y animaciones.");
            QuitWithAudio();
        }
        catch (Exception error) { GD.PushError("PVP_UI_FAIL · " + error); GetTree().Quit(1); }
    }

    private static int EditDistance(string a, string b)
    {
        var row = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var diagonal = row[0]; row[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var above = row[j];
                row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), diagonal + (a[i - 1] == b[j - 1] ? 0 : 1));
                diagonal = above;
            }
        }
        return row[b.Length];
    }
}
