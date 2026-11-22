using Godot;
using Relick.Core;
using Relick.Network.Types;

namespace Relick;

/// <summary>Signing up, being there for each round and the bracket.</summary>
public partial class TournamentPanel : CompetitionPanel
{
    public event Action? JoinRequested, WithdrawRequested;
    public event Action<bool>? ReadyRequested;
    protected override string Title => "Torneo";
    protected override float LeftShare => 0.55f;

    private Label _name = null!, _state = null!, _rules = null!, _caption = null!;
    private Button _action = null!;
    private ScrollContainer _board = null!;
    private Action _pressed = () => { };
    private string _shownBoard = "";
    private bool? _primary;

    protected override void Build()
    {
        var info = Scrolling(Left, "TournamentInfo", Compact ? 4 : 12);
        _name = Line(info, "", Compact ? MenuVisuals.Type.Subtitle : MenuVisuals.Type.Title); _name.Name = "TournamentName";
        _state = Line(info, "", Compact ? MenuVisuals.Type.Body : MenuVisuals.Type.Control, MenuVisuals.Tone.Reading); _state.Name = "TournamentState";
        _rules = Line(info, $"Eliminación directa. Tienes {CompetitiveRules.CheckInLimit / 60_000_000} minutos para presentarte a cada ronda y " +
            $"{CompetitiveRules.DecisionLimit / 60_000_000} para cada decisión.", MenuVisuals.Type.Caption, BoardArt.Muted);
        _action = ArenaPresentation.Button(Left, "", () => _pressed());
        _action.Name = "TournamentAction"; _action.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _action.CustomMinimumSize = new(0, Compact ? 48 : 64);
        _caption = Line(Right, "", MenuVisuals.Type.Caption, BoardArt.Muted);
        _board = new ScrollContainer { Name = "TournamentBoard", SizeFlagsVertical = SizeFlags.ExpandFill, FollowFocus = true };
        Right.AddChild(_board);
    }

    public void Present(CompetitionView view)
    {
        var cup = view.Tournament;
        var mine = view.Mine;
        _name.Text = cup?.Name ?? "Próximamente";
        var text = ""; var action = ""; var primary = true;
        Action pressed = () => { };
        switch (cup?.State)
        {
            case CompetitionView.Signup:
                var starts = CompetitionView.Until(cup.StartsAt, view.Now);
                text = $"{cup.Entered} de {cup.Capacity} inscritos." + (starts.Length > 0 ? $" Empieza en {starts}." : "");
                if (view.Guest) text += " Inicia sesión para inscribirte.";
                else if (mine != null) { action = "Retirarme"; primary = false; pressed = () => WithdrawRequested?.Invoke(); }
                else if (cup.Entered >= cup.Capacity) text += " El cupo está lleno.";
                else { action = "Inscribirme"; pressed = () => JoinRequested?.Invoke(); }
                break;
            case CompetitionView.Running when mine is { Match: > 0, Playing: false }:
                var left = CompetitionView.Until(mine.Deadline, view.Now);
                text = $"Ronda {cup.Round} de {cup.Rounds}: tu rival es {mine.Rival}." +
                    (mine.Draws > 0 ? " Empataron: se repite." : "") +
                    (mine.RivalReady ? " Ya te está esperando." : mine.Ready ? " Esperamos a que llegue." : "") +
                    (left.Length > 0 ? $" Quedan {left} para presentarse." : "");
                if (mine.Ready) { action = "Salir de la espera"; primary = false; pressed = () => ReadyRequested?.Invoke(false); }
                else { action = "Entrar al duelo"; pressed = () => ReadyRequested?.Invoke(true); }
                break;
            case CompetitionView.Running:
                text = $"Ronda {cup.Round} de {cup.Rounds}." + (mine is { OutRound: > 0 } gone
                    ? $" Tu puesto: {CompetitiveRules.Placing((int)cup.Rounds, (int)gone.OutRound)}."
                    : mine != null ? " Avanzaste. Espera a que termine la ronda." : "");
                break;
            case CompetitionView.Finished:
                text = (cup.Champion.Length > 0 ? $"Campeón: {cup.Champion}." : "Terminó sin campeón.") + (mine is { OutRound: > 0 } placed
                    ? $" Tu puesto: {CompetitiveRules.Placing((int)cup.Rounds, (int)placed.OutRound)}." : "");
                break;
            case CompetitionView.Cancelled:
                text = "Este torneo se canceló.";
                break;
            default:
                text = "Todavía no hay un torneo.";
                break;
        }
        _state.Text = text;
        _action.Visible = action.Length > 0;
        _action.Text = action; _action.Disabled = view.Busy; _pressed = pressed;
        if (primary != _primary)
        {
            if (primary) RelickVisuals.Primary(_action); else RelickVisuals.Secondary(_action);
            _primary = primary;
        }
        _rules.Visible = cup?.State is CompetitionView.Signup or CompetitionView.Running;
        ShowError(view.Error);

        var entrants = view.Entrants ?? [];
        var bracket = view.Bracket ?? [];
        var key = $"{cup?.Id}:{cup?.State}:{mine?.Entry}|" + string.Join(";", entrants.Select(e => $"{e.Id},{e.Name},{e.Seed},{e.OutRound}")) + "|" +
            string.Join(";", bracket.Select(m => $"{m.Id},{m.Result},{m.Playing},{m.EntryA},{m.EntryB}"));
        if (key == _shownBoard) return;
        _shownBoard = key;
        _caption.Text = bracket.Count > 0 ? "CUADRO" : "INSCRITOS";
        foreach (var child in _board.GetChildren()) { _board.RemoveChild(child); child.QueueFree(); }
        _board.AddChild(bracket.Count > 0 ? Bracket((int)(cup?.Rounds ?? 0), entrants, bracket, mine?.Entry ?? 0) : EntrantList(entrants, mine?.Entry ?? 0));
    }

    private Control EntrantList(IReadOnlyList<EntrantInfo> entrants, ulong own)
    {
        var column = new VBoxContainer { Name = "Entrants", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 4);
        if (entrants.Count == 0) Line(column, "Todavía no hay inscritos.", MenuVisuals.Type.Body, BoardArt.Muted);
        for (var i = 0; i < entrants.Count; i++)
            Line(column, $"{i + 1}. {entrants[i].Name}", MenuVisuals.Type.Control, entrants[i].Id == own ? BoardArt.Gold : BoardArt.Ink);
        return column;
    }

    private Control Bracket(int rounds, IReadOnlyList<EntrantInfo> entrants, IReadOnlyList<BracketInfo> bracket, ulong own)
    {
        var names = entrants.ToDictionary(e => e.Id, e => e.Name);
        var columns = new HBoxContainer { Name = "Bracket", SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", Compact ? 8 : 18);
        for (var round = 1; round <= rounds; round++)
        {
            var number = round;
            var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new(Compact ? 132 : 180, 0) };
            column.AddThemeConstantOverride("separation", Compact ? 4 : 10); columns.AddChild(column);
            column.AddChild(ArenaPresentation.Text(round == rounds ? "Final" : round == rounds - 1 ? "Semifinal" : $"Ronda {round}",
                MenuVisuals.Type.Caption, BoardArt.Muted));
            for (var position = 0; position < 1 << (rounds - round); position++)
            {
                var place = position;
                var match = bracket.FirstOrDefault(m => m.Round == number && m.Position == place);
                var box = new PanelContainer { SizeFlagsVertical = SizeFlags.Expand | SizeFlags.ShrinkCenter };
                var here = match != null && match.Result == 0 && own > 0 && (match.EntryA == own || match.EntryB == own);
                var style = RelickVisuals.Frame(MenuVisuals.Tone.Well, here ? BoardArt.Gold
                    : match?.Playing == true ? RelickVisuals.State.Target : MenuVisuals.Tone.Divider, MenuVisuals.Cut.Control, here ? 2 : 1);
                style.ContentMarginLeft = style.ContentMarginRight = 8; style.ContentMarginTop = style.ContentMarginBottom = 4;
                box.AddThemeStyleboxOverride("panel", style); column.AddChild(box);
                var pair = new VBoxContainer(); pair.AddThemeConstantOverride("separation", 0); box.AddChild(pair);
                void Seat(ulong entry, byte side)
                {
                    var name = entry > 0 ? names.GetValueOrDefault(entry, "—") : match == null ? "Por definir" : "Pase";
                    var color = match == null || entry == 0 || match.Result != 0 && match.Result != side ? BoardArt.Muted : entry == own ? BoardArt.Gold : BoardArt.Ink;
                    var label = ArenaPresentation.Text(name, Compact ? MenuVisuals.Type.Caption : MenuVisuals.Type.Body, color);
                    label.ClipText = true; label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                    pair.AddChild(label);
                }
                Seat(match?.EntryA ?? 0, 1);
                Seat(match?.EntryB ?? 0, 2);
            }
        }
        return columns;
    }
}
