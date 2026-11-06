using Godot;
using Relick.Core;

namespace Relick;

public partial class Main
{
    private void ShowMemory(int owner, bool corrupted = false, int scrollPosition = 0)
    {
        if (_helpOpen) return;
        var p = _game.State.Players[owner];
        var column = new VBoxContainer { CustomMinimumSize = new(580, 0) };
        column.AddThemeConstantOverride("separation", 10);
        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 10);
        AcceptDialog? dialog = null;
        foreach (var showCorrupted in new[] { false, true })
        {
            var tab = Button(showCorrupted ? $"Corruptas · {p.CorruptedMemory.Count}" : $"Memoria · {p.Memory.Count}", () =>
            {
                if (dialog == null) return;
                DismissBoardDialog(dialog);
                ShowMemory(owner, showCorrupted);
            }, showCorrupted == corrupted, 270);
            tab.Name = showCorrupted ? "CorruptedMemoryTab" : "MemoryTab";
            if (showCorrupted == corrupted)
            {
                var accent = corrupted ? new Color("b5a2fa") : owner == 0 ? Mint : Coral;
                tab.AddThemeColorOverride("font_disabled_color", accent);
                tab.AddThemeStyleboxOverride("disabled", Box("202b3e", accent.ToHtml(false), 8));
            }
            tabs.AddChild(tab);
        }
        column.AddChild(tabs);
        var hint = Wrapped(corrupted ? "Cartas corruptas boca abajo. Puedes seleccionarlas; su contenido permanece oculto."
            : "Cartas boca arriba. Pulsa una miniatura para ampliar la carta y consultar su efecto.", 14, Muted, 570);
        hint.Name = "MemoryGalleryHint";
        column.AddChild(hint);
        var scroll = new ScrollContainer { Name = "MemoryGallery", CustomMinimumSize = new(580, 360),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var grid = new GridContainer { Name = "MemoryCards", Columns = 4 };
        grid.AddThemeConstantOverride("h_separation", 10); grid.AddThemeConstantOverride("v_separation", 12);
        var count = corrupted ? p.CorruptedMemory.Count : p.Memory.Count;
        for (var i = 0; i < count; i++)
        {
            var index = i;
            if (corrupted)
            {
                var back = new CardBackButton { Name = $"CorruptedMemoryCard{index}" };
                back.Pressed += () =>
                {
                    foreach (var child in grid.GetChildren().OfType<CardBackButton>()) { child.Chosen = child == back; child.QueueRedraw(); }
                    hint.Text = $"Carta corrupta {index + 1} seleccionada. Permanece boca abajo; solo un efecto puede moverla.";
                };
                grid.AddChild(back);
            }
            else
            {
                var card = CardCatalog.Get(p.Memory[i]);
                var thumb = new HandCard { Name = $"MemoryCard{index}", Card = card };
                thumb.Pressed += () =>
                {
                    if (dialog == null) return;
                    var position = scroll.ScrollVertical;
                    DismissBoardDialog(dialog);
                    ShowCardDetails(owner, card, owner == 0 ? "TU MEMORIA" : "MEMORIA RIVAL",
                        "Carta boca arriba en memoria. Consultarla no la activa ni la mueve; solo un efecto permite hacerlo.",
                        "Carta en memoria", () => ShowMemory(owner, false, position));
                };
                grid.AddChild(thumb);
            }
        }
        if (count == 0) grid.AddChild(Label(corrupted ? "No hay cartas corruptas." : "No hay cartas en memoria.", 16, Muted));
        scroll.AddChild(grid); column.AddChild(scroll);
        dialog = OpenBoardDialog(owner == 0 ? "Tu memoria" : "Memoria rival", column, new(620, 500));
        scroll.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, scrollPosition);
    }
}
