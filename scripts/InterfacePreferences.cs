using Godot;

namespace Relick;

/// <summary>Device preferences of the interface that are not sound.</summary>
internal static class InterfacePreferences
{
    private const string Path = "user://interface.cfg";
    private const string BoardPath = "user://board.cfg";
    internal const string ReducedMotionSetting = "relick/accessibility/reduced_motion";

    /// <summary>The system's preference always wins; this one can only ask for less motion.</summary>
    public static bool ReducedMotion =>
        DisplayServer.AccessibilityShouldReduceAnimation() == 1 || ProjectSettings.GetSetting(ReducedMotionSetting, false).AsBool();

    /// <summary>Every animated control reads the project setting, so the saved choice is written there for this run only.</summary>
    public static void Load()
    {
        using var config = new ConfigFile();
        if (config.Load(Path) == Error.Ok && config.GetValue("motion", "reduced", false).AsBool())
            ProjectSettings.SetSetting(ReducedMotionSetting, true);
    }

    public static bool ReducedByPlayer => ProjectSettings.GetSetting(ReducedMotionSetting, false).AsBool();

    public static void SetReducedMotion(bool reduced)
    {
        ProjectSettings.SetSetting(ReducedMotionSetting, reduced);
        using var config = new ConfigFile();
        config.SetValue("motion", "reduced", reduced);
        if (config.Save(Path) != Error.Ok) GD.PushWarning("No se pudo guardar la preferencia de animaciones.");
    }

    public static bool TableView
    {
        get
        {
            using var config = new ConfigFile();
            return config.Load(BoardPath) == Error.Ok && config.GetValue("board", "table_view", false).AsBool();
        }
        set
        {
            using var config = new ConfigFile();
            config.SetValue("board", "table_view", value);
            if (config.Save(BoardPath) != Error.Ok) GD.PushWarning("No se pudo guardar la vista del tablero en este dispositivo.");
        }
    }
}
