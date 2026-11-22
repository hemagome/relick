using Godot;

namespace Relick;

/// <summary>
/// Works around a Godot .NET 4.7.2 shutdown race: a finalizer still releasing a resource while the engine tears C#
/// down aborts the process in debug builds and segfaults it in release ones.
///
/// Godot disposes every live managed wrapper (DisposablesTracker) just before that teardown, after the last node is
/// gone. This object uses its turn to let the pending finalizers finish while the engine is still whole. Flushing
/// before Quit or in _ExitTree is too early: deleting the tree leaves more work for the finalizer.
///
/// That order is engine behaviour, not documented API: run --shutdown-test after every Godot upgrade.
/// </summary>
public partial class ManagedShutdown : RefCounted
{
    private static ManagedShutdown? _instance;

    public static void Install() => _instance ??= new ManagedShutdown();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        base.Dispose(disposing);
    }
}
