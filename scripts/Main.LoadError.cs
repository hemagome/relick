using Godot;
using Relick.Core;
namespace Relick;
public partial class Main
{
    private void ShowLoadErrorResponse()
    {
        var column = new VBoxContainer { CustomMinimumSize = new(620, 0) };
        column.AddThemeConstantOverride("separation", 12);
        column.AddChild(Label("CADENA DE ACTIVACIONES", 22, Gold));
        foreach (var activation in _game.State.ActivationChain)
            column.AddChild(Wrapped($"{(activation.Actor == 0 ? "TÚ" : "RIVAL")} · {CardCatalog.Get(activation.CardId).Name} · {activation.ActionsPaid} acción(es), {activation.PrPaid} PR", 16, Ink, 610));
        column.AddChild(Wrapped("Puedes anular la última activación con Error de Carga. Se resuelve primero la última respuesta. Una carta anulada va a memoria y recupera sus costes.", 16, Muted, 610));
        column.AddChild(Label($"Tus acciones disponibles: {_game.State.Players[0].ActionsLeft}/2", 16, Mint));
        AcceptDialog dialog = null!;
        var hand = _game.State.Players[0].Hand;
        for (var i = 0; i < hand.Count; i++)
        {
            if (!CardCatalog.Get(hand[i]).CancelActivation) continue;
            var index = i;
            var button = Button("Error de Carga · Anular última activación · 1 acción", () =>
            {
                CloseRelikiaDialog(dialog); Execute(_game.PlayCode(0, index));
            }, _game.CodeUnavailableReason(0, i) != null);
            button.Name = $"LoadErrorResponse{i}"; column.AddChild(button);
        }
        dialog = SkillChoiceDialog("Error de Carga · Responder", column, "No responder · Resolver", () => Execute(_game.PassErrorResponse(0)));
    }
}
