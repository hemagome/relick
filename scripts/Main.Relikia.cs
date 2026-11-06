using Godot;
using Relick.Core;

namespace Relick;

public partial class Main
{
    private AcceptDialog RelikiaDialog(string title, Control content, Vector2I size)
    {
        _helpOpen = true;
        var dialog = new AcceptDialog { Name = "RelikiaDialog", Title = title, MinSize = size, OkButtonText = "Volver al duelo" };
        dialog.AddChild(content);
        dialog.Confirmed += () => CloseRelikiaDialog(dialog);
        dialog.Canceled += () => CloseRelikiaDialog(dialog);
        AddChild(dialog);
        dialog.PopupCentered();
        return dialog;
    }

    private void CloseRelikiaDialog(AcceptDialog dialog)
    {
        dialog.Hide();
        dialog.QueueFree();
        _helpOpen = false;
    }

    private void ShowRelikiaEffects()
    {
        if (_helpOpen || _die.IsBusy || _game.State.CurrentPlayer != 0 || _game.State.Phase != Phase.Actions) return;
        var column = new VBoxContainer { CustomMinimumSize = new(610, 0) };
        column.AddThemeConstantOverride("separation", 12);
        column.AddChild(Label($"TIENES {_game.State.Active.Relikia} PR  ·  {_game.State.ActionsLeft} ACCIONES", 16, Gold));
        AcceptDialog dialog = null!;
        foreach (var definition in RelikiaEffects.All)
        {
            var error = _game.RelikiaEffectUnavailableReason(0, definition.Id);
            var button = Button($"{definition.Cost} PR · {definition.Name} · {definition.ActionLabel}", () =>
            {
                CloseRelikiaDialog(dialog);
                if (definition.Id == RelikiaEffect.ReplayCode) ShowRelikiaMemoryPicker(true);
                else Execute(_game.UseRelikiaEffect(0, definition.Id));
            }, error != null);
            button.Name = $"Relikia{definition.Id}";
            button.TooltipText = error ?? definition.Text;
            column.AddChild(button);
            column.AddChild(Wrapped(definition.Text + (error == null ? "" : $"\n{error}"), 13, error == null ? Ink : Muted, 600));
        }
        dialog = RelikiaDialog("Efectos de Relikia", column, new(650, 530));
    }

    private void ShowPendingRelikiaChoice()
    {
        if (_helpOpen || _die.IsBusy || _game.State.CurrentPlayer != 0 || _game.State.Phase != Phase.RelikiaChoice) return;
        switch (_game.State.PendingRelikia)
        {
            case RelikiaChoice.PlaceHandCard: ShowRelikiaPlacement(); break;
            case RelikiaChoice.BonusReward: ShowRelikiaReward(); break;
            case RelikiaChoice.CorruptCode: ShowRelikiaMemoryPicker(false); break;
        }
    }

    private void ShowRelikiaPlacement()
    {
        var column = new VBoxContainer { CustomMinimumSize = new(610, 0) };
        column.AddThemeConstantOverride("separation", 12);
        column.AddChild(Wrapped("Ya robaste una carta. Elige cualquier carta de tu mano y después su destino. Esta elección no consume otra acción.", 16, Ink, 600));
        var scroll = new ScrollContainer { CustomMinimumSize = new(610, 210), VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var cards = new HBoxContainer();
        cards.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(cards);
        column.AddChild(scroll);
        var selected = -1;
        AcceptDialog dialog = null!;
        var destinations = new HBoxContainer();
        var bottom = Button("Al fondo del mazo", () => Resolve(CardDestination.DeckBottom), true, 270);
        bottom.Name = "RelikiaPlaceDeck";
        var exchange = Button("A canje", () => Resolve(CardDestination.Exchange), true, 270);
        exchange.Name = "RelikiaPlaceExchange";
        void Resolve(CardDestination destination)
        {
            var result = _game.ResolveRelikiaPlacement(0, selected, destination);
            if (result.Success) CloseRelikiaDialog(dialog);
            Execute(result);
        }
        for (var i = 0; i < _game.State.Active.Hand.Count; i++)
        {
            var index = i;
            var card = new HandCard { Name = $"RelikiaHand{index}", Card = CardCatalog.Get(_game.State.Active.Hand[i]) };
            card.Pressed += () =>
            {
                selected = index;
                foreach (var child in cards.GetChildren().OfType<HandCard>()) { child.Chosen = child == card; child.QueueRedraw(); }
                bottom.Disabled = false;
                exchange.Disabled = _game.State.Active.Bonus == null;
            };
            cards.AddChild(card);
        }
        destinations.AddChild(bottom); destinations.AddChild(exchange); column.AddChild(destinations);
        dialog = RelikiaDialog("Relikia · Elige una carta y su destino", column, new(650, 390));
    }

    private void ShowRelikiaReward()
    {
        var column = new VBoxContainer { CustomMinimumSize = new(570, 0) };
        column.AddThemeConstantOverride("separation", 16);
        column.AddChild(Wrapped($"{_game.State.LastCombat}. Ya recibiste tu Bonus. Elige el efecto adicional:", 18, Gold, 560));
        AcceptDialog dialog = null!;
        foreach (var reward in new[] { RelikiaReward.OpponentDiscards, RelikiaReward.CorruptCodes })
        {
            var count = reward == RelikiaReward.OpponentDiscards ? _game.State.Players[1].Hand.Count
                : _game.State.Players.Sum(p => p.Memory.Count(id => CardCatalog.Get(id).Kind == CardKind.Code));
            var text = reward == RelikiaReward.OpponentDiscards ? "El rival descarta 2 cartas de su elección" : "Elegir 2 códigos para corromper";
            var button = Button(text, () =>
            {
                CloseRelikiaDialog(dialog);
                Execute(_game.ChooseRelikiaReward(0, reward));
            });
            button.Name = $"RelikiaReward{reward}";
            column.AddChild(button);
            column.AddChild(Wrapped(reward == RelikiaReward.OpponentDiscards
                ? $"Cartas en la mano del rival: {count}. Él decide cuáles descartar."
                : $"Códigos sin corromper entre ambas memorias: {count}. Tú eliges cuáles corromper.", 13, Muted, 560));
        }
        column.AddChild(Wrapped("Si hay menos de dos cartas válidas, se afectan las disponibles.", 13, Muted, 560));
        dialog = RelikiaDialog("Relikia · Resultado del Bonus", column, new(610, 360));
    }

    private void ShowRelikiaMemoryPicker(bool replay)
    {
        if (_helpOpen) return;
        var column = new VBoxContainer { CustomMinimumSize = new(580, 0) };
        column.AddThemeConstantOverride("separation", 12);
        column.AddChild(Wrapped(replay ? "Elige un código de tu memoria. Se activará, quedará corrompido y pagarás 400 PR y 2 acciones."
            : $"Elige un código de cualquier memoria. Faltan {_game.State.CorruptionsRemaining} por corromper.", 16, Gold, 570));
        var scroll = new ScrollContainer { Name = "RelikiaMemoryGallery", CustomMinimumSize = new(580, 310), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(list); column.AddChild(scroll);
        AcceptDialog dialog = null!;
        foreach (var owner in replay ? new[] { 0 } : new[] { 1, 0 })
        {
            list.AddChild(Label(owner == 0 ? "TU MEMORIA" : "MEMORIA RIVAL", 12, owner == 0 ? Mint : Coral));
            var p = _game.State.Players[owner];
            var found = false;
            var grid = new GridContainer { Columns = 4 };
            grid.AddThemeConstantOverride("h_separation", 10);
            grid.AddThemeConstantOverride("v_separation", 10);
            list.AddChild(grid);
            for (var i = 0; i < p.Memory.Count; i++)
            {
                var card = CardCatalog.Get(p.Memory[i]);
                if (card.Kind != CardKind.Code) continue;
                found = true;
                var index = i;
                var button = new HandCard { Name = $"RelikiaMemory{owner}_{index}", Card = card };
                button.Ready += () => button.TooltipText = $"{card.Name}\n{card.Text}\n{(replay ? "Pulsa para activar y corromper (400 PR y 2 acciones)." : "Pulsa para corromper este código.")}";
                button.Pressed += () =>
                {
                    var result = replay ? _game.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, index) : _game.ResolveRelikiaCorruption(0, owner, index);
                    if (result.Success) CloseRelikiaDialog(dialog);
                    Execute(result);
                };
                grid.AddChild(button);
            }
            if (!found) list.AddChild(Label("Sin códigos disponibles", 13, Muted));
        }
        dialog = RelikiaDialog(replay ? "Relikia · Activar código de memoria" : "Relikia · Corromper códigos", column, new(620, 450));
    }
}
