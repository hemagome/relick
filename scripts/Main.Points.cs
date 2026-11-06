using Godot;

namespace Relick;

public partial class Main
{
    private void ShowPointsChoice()
    {
        var pending = _game.State.PendingPoints!;
        var column = new VBoxContainer { CustomMinimumSize = new(620, 0) };
        column.AddThemeConstantOverride("separation", 16);
        column.AddChild(Label($"D6 = {pending.Die} · {pending.Amount} PR", 24, Gold));
        column.AddChild(Wrapped("Elige un jugador y aumenta o disminuye sus PR. Los PR no pueden bajar de cero.", 16, Ink, 610));
        AcceptDialog? dialog = null;
        for (var i = 0; i < 2; i++)
        {
            var target = i;
            column.AddChild(Label($"{(i == 0 ? "TÚ" : "RIVAL")} · {_game.State.Players[i].Relikia} PR", 16, i == 0 ? Mint : Coral));
            var row = new HBoxContainer();
            foreach (var increase in new[] { true, false })
            {
                var button = Button($"{(increase ? "Aumentar" : "Disminuir")} {pending.Amount} PR", () =>
                {
                    var result = _game.ResolvePointsChange(0, target, increase);
                    if (result.Success && dialog != null) DismissBoardDialog(dialog);
                    Execute(result);
                }, false, 260);
                button.Name = $"Points{target}{(increase ? "Increase" : "Decrease")}";
                row.AddChild(button);
            }
            column.AddChild(row);
        }
        // Closing does not make an implicit choice; the pending effect opens again.
        dialog = OpenBoardDialog("Aumento de Relick Points · Elige el efecto", column, new(660, 390));
        if (dialog != null) dialog.OkButtonText = "Volver sin elegir";
    }
}
