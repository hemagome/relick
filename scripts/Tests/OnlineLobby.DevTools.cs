using Godot;

namespace Relick;

/// <summary>
/// Command-line entry points for the UI tests and visual previews. This folder is compiled only for development
/// (RELICK_DEV_TOOLS); exported builds leave it out, so these flags do nothing there.
/// </summary>
public partial class OnlineLobby
{
    /// <summary>
    /// Board preferences the UI tests read instead of the player's own: missing, so every test starts in the classic view.
    /// The table view test swaps in its own temporary file.
    /// </summary>
    private const string IsolatedBoardSettings = "user://ui-test-board.cfg";

    /// <summary>Starts an offline test or preview instead of connecting; returns whether one was started.</summary>
    private bool StartOfflineDevTool()
    {
        var args = OS.GetCmdlineUserArgs();
        string? tool = args.Any(a => a is "--music-test" or "--audio-test") ? nameof(MusicSmokeTest)
            : args.Any(a => a.StartsWith("--lobby-preview-dir=")) ? nameof(LobbyVisualPreview)
            : args.Contains("--shutdown-test") ? nameof(ShutdownSmokeTest)
            : args.Contains("--pvp-ui-test") ? nameof(ArenaUiSmokeTest)
            : null;
        if (tool == null) return false;
        SetProcess(false);
        CallDeferred(tool);
        return true;
    }

    /// <summary>Starts the smoke tests that run against a live server, once the session has begun.</summary>
    private void StartOnlineDevTools()
    {
        var args = OS.GetCmdlineUserArgs();
        if (args.Contains("--pvp-preparation-test")) CallDeferred(nameof(PreparationSmokeTest));
        if (args.Contains("--pvp-combat-test")) CallDeferred(nameof(CombatSmokeTest));
        if (args.Any(a => a.StartsWith("--pvp-competitive-peer="))) CallDeferred(nameof(CompetitivePeerSmokeTest));
        if (args.Any(a => a.StartsWith("--pvp-input-peer="))) CallDeferred(nameof(InputPeerSmokeTest));
    }
}
