using Godot;
using Relick.Core;

namespace Relick;

/// <summary>The season, where the player stands and the rules of a duel that counts.</summary>
public partial class RankedPanel : CompetitionPanel
{
    public event Action? SearchRequested;
    protected override string Title => "Clasificatoria ELO";
    private Label _season = null!, _league = null!, _rating = null!, _record = null!;
    private ProgressBar _placement = null!;
    private Button _search = null!;

    protected override void Build()
    {
        var standing = Scrolling(Left, "RankedStanding", Compact ? 4 : 12);
        _season = Line(standing, "", MenuVisuals.Type.Body, BoardArt.Muted); _season.Name = "SeasonState";
        var icon = new LobbySymbol { Icon = GameIcon.Ranking, Accent = BoardArt.Gold,
            CustomMinimumSize = Compact ? new(48, 48) : new(128, 128), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        standing.AddChild(icon);
        _league = Line(standing, "", MenuVisuals.Type.Title, BoardArt.Gold); _league.Name = "LeagueName";
        _league.AddThemeFontOverride("font", RelickVisuals.Display);
        _rating = Line(standing, "", MenuVisuals.Type.Hero); _rating.Name = "RatingValue";
        _record = Line(standing, "", MenuVisuals.Type.Body, MenuVisuals.Tone.Reading);
        foreach (var label in new[] { _season, _league, _rating, _record }) label.HorizontalAlignment = HorizontalAlignment.Center;
        _placement = new ProgressBar { Name = "Placement", MaxValue = CompetitiveRules.PlacementDuels, ShowPercentage = false, CustomMinimumSize = new(0, 8) };
        _placement.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = MenuVisuals.Tone.Well });
        _placement.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = BoardArt.Gold });
        standing.AddChild(_placement);

        Line(Right, "CÓMO FUNCIONA", MenuVisuals.Type.Caption, BoardArt.Muted);
        var rules = Scrolling(Right, "RankedRules", Compact ? 6 : 14);
        var minutes = CompetitiveRules.DecisionLimit / 60_000_000;
        foreach (var rule in new[]
        {
            $"Tus primeros {CompetitiveRules.PlacementDuels} duelos son de colocación y mueven el doble de puntos.",
            "Te buscamos un rival de puntuación cercana. Mientras más esperes, más amplio es el rango.",
            $"Si pasas {minutes} minutos sin preparar tu mano o sin decidir, pierdes el duelo.",
            "Cerrar la sala o abandonar cuenta como derrota.",
            $"Puedes enfrentar al mismo rival hasta {CompetitiveRules.PairDuelsPerDay} veces al día.",
            "Al cerrar la temporada recibes emblema y premios según tu liga."
        })
            Line(rules, rule, Compact ? MenuVisuals.Type.Body : MenuVisuals.Type.Control, MenuVisuals.Tone.Reading);
        _search = ArenaPresentation.Button(Right, "", () => SearchRequested?.Invoke());
        _search.Name = "RankedSearch"; _search.CustomMinimumSize = new(0, Compact ? 48 : 64);
        RelickVisuals.Primary(_search);
    }

    public void Present(CompetitionView view)
    {
        var season = view.Season;
        var open = season?.Open == true;
        var left = open ? CompetitionView.Until(season!.EndsAt, view.Now) : "";
        _season.Text = season == null ? "Próximamente" : open
            ? left.Length > 0 ? $"{season.Name} · quedan {left}" : season.Name
            : $"{season.Name} · cerrada. La siguiente se anunciará aquí.";
        var own = view.Standing;
        var played = own?.Played ?? 0;
        var placed = played >= CompetitiveRules.PlacementDuels;
        _league.Text = placed ? CompetitiveRules.LeagueName(CompetitiveRules.LeagueOf(own!.Value)) : $"Colocación {played}/{CompetitiveRules.PlacementDuels}";
        _rating.Text = own != null ? own.Value.ToString() : "";
        _rating.Visible = own != null;
        _record.Text = own == null ? "Todavía no juegas esta temporada."
            : $"{own.Wins} V · {own.Losses} D" + (own.Draws > 0 ? $" · {own.Draws} E" : "") + $" · máximo {own.Peak}" + (own.Place > 0 ? $" · puesto #{own.Place}" : "");
        _placement.Visible = !placed; _placement.Value = played;
        _search.Disabled = !open || view.Guest || view.Busy;
        _search.Text = view.Guest ? "Inicia sesión para competir" : !open ? "Temporada cerrada" : "Buscar rival clasificado";
        ShowError(view.Error);
    }
}
