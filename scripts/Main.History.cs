using Godot;
using System.Text.RegularExpressions;

namespace Relick;

public partial class Main
{
    private void ShowHistory()
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        var legend = new HBoxContainer();
        legend.AddThemeConstantOverride("separation", 24);
        legend.AddChild(Label("Jugador 1 · Tú", 15, Mint));
        legend.AddChild(Label("Jugador 2 · Rival", 15, Coral));
        column.AddChild(legend);

        var history = new RichTextLabel
        {
            Name = "DuelHistory", CustomMinimumSize = new(550, 340),
            SizeFlagsVertical = SizeFlags.ExpandFill, ScrollFollowing = true, BbcodeEnabled = false
        };
        history.AddThemeFontSizeOverride("normal_font_size", 15);
        column.AddChild(history);

        var names = _game.State.Players.Select(p => p.Name).Where(name => name.Length > 0).OrderByDescending(name => name.Length);
        var mentions = new Regex(@"(?<![\p{L}\p{N}_])(?:" + string.Join("|", names.Select(Regex.Escape)) + @")(?![\p{L}\p{N}_])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        for (var index = 0; index < _game.State.Log.Count; index++)
        {
            if (index > 0) history.AddText("\n\n");
            var line = _game.State.Log[index];
            var position = 0;
            foreach (Match mention in mentions.Matches(line))
            {
                history.AddText(line[position..mention.Index]);
                var player = Array.FindIndex(_game.State.Players, p => string.Equals(p.Name, mention.Value, StringComparison.OrdinalIgnoreCase));
                history.PushColor(player == 0 ? Mint : Coral);
                history.PushBold();
                history.AddText(player == 0 ? "J1 · Tú" : "J2 · Rival");
                history.Pop(); history.Pop();
                position = mention.Index + mention.Length;
            }
            // Plain text keeps card names and other log contents from becoming markup.
            history.AddText(line[position..]);
        }
        OpenBoardDialog("Registro del duelo", column, new Vector2I(620, 460));
    }
}
