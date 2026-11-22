using Godot;

namespace Relick;

/// <summary>Music and effects volume. The sound dialog and the settings screen show the same controls.</summary>
public partial class AudioControls : VBoxContainer
{
    private const string SilentSlider = "SilentVolumeSlider";
    private static readonly Color SilentText = new("9a9ca2");
    private sealed record VolumeControl(HSlider Slider, Label Value, Button Mute, IconView Speaker);

    public AudioPreferences Preferences { get; init; } = null!;
    private VolumeControl _music = null!, _effects = null!;

    public override void _Ready()
    {
        // Only the sliders: every other type keeps resolving through the screen's theme.
        Theme = new Theme();
        Theme.SetTypeVariation(SilentSlider, "HSlider");
        SetSliderColors("HSlider", MenuVisuals.Tone.Raised, BoardArt.Gold);
        SetSliderColors(SilentSlider, new Color("34363b"), new Color("747780"));
        AddThemeConstantOverride("separation", 14);
        _music = AddVolume(this, "Música", "MusicVolume", "MusicMute", Preferences.SetMusicVolume, Preferences.SetMusicMuted);
        _effects = AddVolume(this, "Efectos de sonido", "EffectsVolume", "EffectsMute", Preferences.SetEffectsVolume, Preferences.SetEffectsMuted);
        Refresh();
    }

    private void SetSliderColors(string type, Color trackColor, Color fillColor)
    {
        Theme.SetStylebox("slider", type, new StyleBoxFlat
        {
            BgColor = trackColor, ContentMarginTop = 3, ContentMarginBottom = 3,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3
        });
        var fill = new StyleBoxFlat { BgColor = fillColor, ContentMarginTop = 3, ContentMarginBottom = 3 };
        Theme.SetStylebox("grabber_area", type, fill);
        Theme.SetStylebox("grabber_area_highlight", type, fill);
    }

    private static VolumeControl AddVolume(VBoxContainer column, string caption, string name, string muteName,
        Action<float> setVolume, Action<bool> setMuted)
    {
        var row = new HBoxContainer(); column.AddChild(row);
        var title = ArenaPresentation.Text(caption, MenuVisuals.Type.Control); title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(title);
        var value = ArenaPresentation.Text("", MenuVisuals.Type.Body, BoardArt.Gold); row.AddChild(value);
        var channel = caption.ToLowerInvariant();
        var mute = new Button
        {
            Name = muteName, ToggleMode = true, CustomMinimumSize = new(MenuVisuals.TouchTarget, MenuVisuals.TouchTarget),
            AccessibilityName = $"Silenciar {channel}",
            TooltipText = $"Silenciar o reactivar {channel}"
        };
        row.AddChild(mute);
        mute.AddThemeStyleboxOverride("pressed", mute.GetThemeStylebox("normal"));
        mute.AddThemeStyleboxOverride("hover_pressed", mute.GetThemeStylebox("hover"));
        var speaker = new IconView { Name = "SpeakerIcon" };
        mute.AddChild(speaker);
        speaker.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        speaker.OffsetLeft = -12;
        speaker.OffsetTop = -12;
        speaker.OffsetRight = 12;
        speaker.OffsetBottom = 12;

        var slider = new HSlider
        {
            Name = name, MinValue = 0, MaxValue = 100, Step = 1, CustomMinimumSize = new(260, MenuVisuals.TouchTarget),
            FocusMode = Control.FocusModeEnum.All, TooltipText = caption
        };
        column.AddChild(slider);
        var control = new VolumeControl(slider, value, mute, speaker);
        slider.ValueChanged += level =>
        {
            setVolume((float)level / 100);
            UpdateAppearance(control);
        };
        mute.Toggled += muted =>
        {
            setMuted(muted);
            UpdateAppearance(control);
        };
        return control;
    }

    private static void UpdateAppearance(VolumeControl control)
    {
        var silent = control.Mute.ButtonPressed || control.Slider.Value == 0;
        control.Slider.ThemeTypeVariation = silent ? SilentSlider : "";
        control.Value.Text = $"{control.Slider.Value:0} %";
        control.Value.AddThemeColorOverride("font_color", silent ? SilentText : BoardArt.Gold);
        control.Speaker.Icon = silent ? GameIcon.SpeakerMuted : GameIcon.Speaker;
        control.Speaker.Color = silent ? SilentText : BoardArt.Ink;
        control.Speaker.QueueRedraw();
    }

    private static void RefreshVolume(VolumeControl control, float volume, bool muted)
    {
        control.Slider.SetValueNoSignal(Math.Round(volume * 100));
        control.Mute.SetPressedNoSignal(muted);
        UpdateAppearance(control);
    }

    public void Refresh()
    {
        if (_music == null) return;
        RefreshVolume(_music, Preferences.MusicVolume, Preferences.MusicMuted);
        RefreshVolume(_effects, Preferences.EffectsVolume, Preferences.EffectsMuted);
    }
}
