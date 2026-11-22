using Godot;

namespace Relick;

public partial class AudioSettingsDialog : AcceptDialog
{
    public AudioPreferences Preferences { get; set; } = null!;
    private AudioControls? _controls;

    public override void _Ready()
    {
        Title = "Sonido";
        Theme = RelickVisuals.Controls();
        Theme.SetStylebox("panel", "AcceptDialog", RelickVisuals.Frame(RelickVisuals.Playmat.Surface, RelickVisuals.Playmat.Frame, MenuVisuals.Cut.Control));
        GetOkButton().Text = "Listo"; MenuDialog.Style(this);
        _controls = new AudioControls { Preferences = Preferences }; AddChild(_controls);
        Confirmed += Preferences.Flush; CloseRequested += Preferences.Flush;
    }

    public void Refresh() => _controls?.Refresh();
}
