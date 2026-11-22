using Godot;

namespace Relick;

internal static class NativeIdentity
{
    /// <summary>
    /// What tests compare instead of a loaded resource: once the finalizer runs, Godot may return another C# wrapper
    /// for the same resource, which breaks ==, != and Distinct in both directions.
    /// </summary>
    internal static ulong? NativeId(this GodotObject? value) => value?.GetInstanceId();
}
