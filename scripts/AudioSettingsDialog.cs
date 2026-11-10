using Godot;

namespace Relick;

public partial class AudioSettingsDialog : AcceptDialog
{
    public AudioPreferences Preferences { get; set; } = null!;
    private HSlider _music = null!, _effects = null!;
    private Label _musicValue = null!, _effectsValue = null!;

    public override void _Ready()
    {
        Title = "Sonido";
        Theme = RelickVisuals.Controls();
        Theme.SetStylebox("panel", "AcceptDialog", RelickVisuals.Frame(RelickVisuals.Playmat.Surface, RelickVisuals.Playmat.Frame));
        Theme.SetStylebox("slider", "HSlider", new StyleBoxFlat
        {
            BgColor = RelickVisuals.Playmat.Raised, ContentMarginTop = 3, ContentMarginBottom = 3,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3
        });
        var fill = new StyleBoxFlat { BgColor = BoardArt.Gold, ContentMarginTop = 3, ContentMarginBottom = 3 };
        Theme.SetStylebox("grabber_area", "HSlider", fill); Theme.SetStylebox("grabber_area_highlight", "HSlider", fill);
        GetOkButton().Text = "Listo"; GetOkButton().CustomMinimumSize = new(120, 44);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 14); AddChild(column);
        (_music, _musicValue) = AddVolume(column, "Música", "MusicVolume");
        (_effects, _effectsValue) = AddVolume(column, "Efectos de sonido", "EffectsVolume");
        var hint = ArenaPresentation.Text("Se guarda en este dispositivo.", 13, BoardArt.Muted);
        column.AddChild(hint);
        _music.ValueChanged += value => { Preferences.SetMusicVolume((float)value / 100); Caption(_musicValue, value); };
        _effects.ValueChanged += value => { Preferences.SetEffectsVolume((float)value / 100); Caption(_effectsValue, value); };
        Confirmed += Preferences.Flush; CloseRequested += Preferences.Flush;
        Refresh();
    }

    private static (HSlider Slider, Label Value) AddVolume(VBoxContainer column, string caption, string name)
    {
        var row = new HBoxContainer(); column.AddChild(row);
        var title = ArenaPresentation.Text(caption, 16); title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(title);
        var value = ArenaPresentation.Text("", 14, BoardArt.Gold); row.AddChild(value);
        var slider = new HSlider
        {
            Name = name, MinValue = 0, MaxValue = 100, Step = 1, CustomMinimumSize = new(260, 44),
            FocusMode = Control.FocusModeEnum.All, TooltipText = caption
        };
        column.AddChild(slider);
        return (slider, value);
    }

    private static void Caption(Label label, double value) => label.Text = value == 0 ? "Silenciado" : $"{value:0} %";

    public void Refresh()
    {
        if (_music == null) return;
        _music.SetValueNoSignal(Math.Round(Preferences.MusicVolume * 100));
        _effects.SetValueNoSignal(Math.Round(Preferences.EffectsVolume * 100));
        Caption(_musicValue, _music.Value); Caption(_effectsValue, _effects.Value);
    }
}
