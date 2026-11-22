using Godot;
using Relick.Core;

namespace Relick;

// The player's own profile: what the rival sees, the name, and the results with each Relikario.
public partial class OnlineLobby
{
    private int _presentedProfileRevision = -1, _presentedRecordsRevision = -1;
    private Container _profilePreview = null!;
    private VBoxContainer _profileResults = null!;
    private Control _profileResultsSection = null!;
    private Button _customize = null!;

    private Control BuildProfilePage()
    {
        var (page, column) = Page("ProfilePage", "Mi perfil");
        _profilePreview = new MarginContainer { Name = "OwnProfileCard" }; column.AddChild(_profilePreview);

        var identity = SettingsSection(column, "TU NOMBRE");
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10); identity.AddChild(row);
        _name = new LineEdit { Name = "PlayerName", PlaceholderText = "¿Cómo te llamas?", Text = "Jugador", MaxLength = 24,
            CustomMinimumSize = new(0, MenuVisuals.TouchTarget), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(_name);
        _name.TextSubmitted += _ => { SaveAccountName(); _name.ReleaseFocus(); };
        _name.FocusExited += () => { SaveAccountName(); _presentedProfileRevision = -1; };
        _customize = AddButton(row, "Personalizar", () => ShowPage(MenuPage.Customize));
        _customize.Name = "OpenProfile";
        identity.AddChild(Description("Es el nombre que lee tu rival."));

        _profileResults = SettingsSection(column, "RESULTADOS POR RELIKARIO");
        _profileResults.Name = "ProfileResults";
        _profileResultsSection = (Control)_profileResults.GetParent();
        return page;
    }

    private void PresentProfileState(bool available)
    {
        _customize.Disabled = !available;
        // The wardrobe reports its own refusals, beside what it was saving.
        if (_wardrobe == null && _lobby.TakeProfileError() is { Length: > 0 } refused) _localError = refused;
        if (_page == MenuPage.Profile) PresentProfile();
    }

    private void PresentProfile()
    {
        if (_presentedProfileRevision != _lobby.ProfileRevision)
        {
            _presentedProfileRevision = _lobby.ProfileRevision;
            foreach (var child in _profilePreview.GetChildren()) { _profilePreview.RemoveChild(child); child.QueueFree(); }
            _profilePreview.AddChild(new ProfileCard { Name = "ProfileCard", View = OwnProfile(), Accent = RelickVisuals.State.Own });
        }
        _profileResultsSection.Visible = _lobby.PersonalizationAvailable;
        if (_presentedRecordsRevision == _lobby.PersonalizationRevision) return;
        _presentedRecordsRevision = _lobby.PersonalizationRevision;
        PresentRecords(_lobby.Records);
    }

    private void PresentRecords(IReadOnlyList<RelikarioRecord> records)
    {
        foreach (var child in _profileResults.GetChildren().Skip(1)) { _profileResults.RemoveChild(child); child.QueueFree(); }
        var played = records.Where(record => record.Played > 0).OrderByDescending(record => record.Played).ToList();
        if (played.Count == 0) _profileResults.AddChild(Description("Termina tu primer duelo para verlos aquí."));
        foreach (var record in played)
        {
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 12); _profileResults.AddChild(row);
            row.AddChild(new TextureRect { Texture = RelikarioArt.Thumbnail(record.Relikario), CustomMinimumSize = new(48, 48),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
            var texts = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            texts.AddThemeConstantOverride("separation", 4); row.AddChild(texts);
            texts.AddChild(Text(RelikarioCatalog.NameOf(record.Relikario)));
            var rate = ProfileCatalog.WinRate(record.Wins, record.Played) ?? 0;
            var bar = new ProgressBar { Value = rate, ShowPercentage = false, CustomMinimumSize = new(0, 6) };
            bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = MenuVisuals.Tone.Well });
            bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = RelickVisuals.State.Own });
            texts.AddChild(bar);
            var draws = record.Draws > 0 ? $" · {record.Draws} E" : "";
            var total = ArenaPresentation.Text($"{record.Wins} V · {record.Losses} D{draws} · {rate} %", MenuVisuals.Type.Body, MenuVisuals.Tone.Reading);
            total.SizeFlagsVertical = SizeFlags.ShrinkCenter; row.AddChild(total);
        }
    }

    private ProfileView OwnProfile()
    {
        var view = _lobby.Profile is { } profile ? ProfileView.From(profile) : ProfileView.Default(_name.Text);
        // The field may hold a name not saved yet; the card previews what the rival will read.
        return _name.Text.Trim().Length > 0 ? view with { Name = _name.Text.Trim() } : view;
    }
}
