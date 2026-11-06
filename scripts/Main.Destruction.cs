using Godot;
using Relick.Core;
namespace Relick;
public partial class Main
{
    private void ShowDestructionChoice()
    {
        var column = new VBoxContainer { CustomMinimumSize = new(620, 0) };
        column.AddChild(Wrapped("Huevo Azul Explosivo · Elige una no-habilidad del campo. La carta destruida va a la memoria de su dueño.", 18, Ink, 610));
        AcceptDialog? dialog = null;
        foreach (var target in _game.DestructionTargets())
        {
            var zoneName = target.Zone switch
            {
                FieldZone.Equipment => "Equipo", FieldZone.Bonus => "Bonus",
                FieldZone.Exchange => $"Canje {target.Index + 1}", _ => "Sobre Relikario"
            };
            var button = Button($"{(target.Owner == 0 ? "TÚ" : "RIVAL")} · {zoneName} · {CardCatalog.Get(target.CardId).Name}", () =>
            {
                var result = _game.ResolveDestruction(0, target.Owner, target.Zone, target.Index);
                if (result.Success && dialog != null) DismissBoardDialog(dialog);
                Execute(result);
            });
            button.Name = $"Destroy{target.Owner}{(target.Zone == FieldZone.Attached ? target.Index.ToString() : target.Zone == FieldZone.Exchange ? $"Exchange{target.Index}" : target.Zone.ToString())}";
            column.AddChild(button);
        }
        dialog = OpenBoardDialog("Huevo Azul Explosivo · Destruir", column, new(660, 390));
    }
}
