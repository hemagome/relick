using Godot;

namespace Relick;

// A duel that counts: leaving costs, the decision has a limit and the result says what moved.
public partial class OnlineArena
{
    private const long LimitShownFrom = 60_000_000;

#if RELICK_DEV_TOOLS
    /// <summary>The decision limit of a rated room, for tests without a server.</summary>
    internal long? TestDecisionLimit { get; set; }
#endif

    private long RatedLimit
    {
        get
        {
#if RELICK_DEV_TOOLS
            if (TestDecisionLimit is { } limit) return limit;
#endif
            return Connection?.RoomMode?.DecisionLimit ?? 0;
        }
    }

    private bool RatedRoom => RatedLimit > 0;

    private void ConfirmRatedLeave()
    {
        if (GetNodeOrNull("RatedLeaveDialog") != null) return;
        var dialog = new ConfirmationDialog { Name = "RatedLeaveDialog", Title = "Cerrar sala",
            DialogText = "Este duelo cuenta: cerrar la sala ahora es una derrota.", OkButtonText = "Salir y perder", CancelButtonText = "Seguir" };
        dialog.Confirmed += () => { Connection.Leave(RoomCode); dialog.QueueFree(); };
        dialog.Canceled += () => dialog.QueueFree();
        AddChild(dialog);
        foreach (var button in new[] { dialog.GetOkButton(), dialog.GetCancelButton() }) button.CustomMinimumSize = new(132, 48);
        dialog.PopupCentered();
    }

    /// <summary>The rated outcome of this room, or empty for a casual one.</summary>
    private string RatedOutcome()
    {
        if (Connection?.RatedResult is not { } result || result.RoomCode != RoomCode)
            return Connection?.RoomMode?.Mode == "tournament" ? "Duelo de torneo" : "";
        var change = result.After - result.Before;
        var text = change == 0 ? $"ELO {result.After}" : $"ELO {result.Before} → {result.After} ({change:+#;-#})";
        return result.Note.Length > 0 ? $"{text}. {result.Note}" : text;
    }
}
