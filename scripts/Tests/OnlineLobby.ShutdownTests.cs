using System.Runtime.CompilerServices;
using Godot;

namespace Relick;

public partial class OnlineLobby
{
    /// <summary>
    /// `--shutdown-test` closes through the player's path while the finalizer has its worst possible backlog.
    /// The process itself is the assertion: without <see cref="ManagedShutdown"/> it aborts instead of exiting with 0.
    /// </summary>
    private async void ShutdownSmokeTest()
    {
        GetTree().Root.AddChild(new LateGarbage { Name = "LateGarbage" });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GD.Print("PVP_SHUTDOWN_CLOSING · el proceso debe terminar con código 0.");
        GetWindow().EmitSignal(Window.SignalName.CloseRequested);
    }
}

/// <summary>Leaves wrappers to the finalizer while the nodes are being deleted, after every _ExitTree.</summary>
internal partial class LateGarbage : Node
{
    private const int Wrappers = 80000;

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete) return;
        Abandon();
        GC.Collect();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Abandon()
    {
        for (var index = 0; index < Wrappers; index++) _ = new StyleBoxEmpty();
    }
}
