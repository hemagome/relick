using Godot;
using Relick.Core;
using Relick.Network.Types;

namespace Relick;

public partial class OnlineLobby
{
    // Offline: the deck list and the editor with sample decks, at the four supported sizes.
    private async Task CheckDeckScreens()
    {
        async Task Frames() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        void Expect(bool ok, string reason) { if (!ok) throw new Exception("Mazos: " + reason); }
        var directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
        var training = CardCatalog.TrainingDeck();
        var samples = new List<DeckInfo>
        {
            new() { Id = 1, Name = "Yuno agresivo", Relikario = "yuno", Cards = training, Traps = CardCatalog.TrainingTraps(), Valid = true, Active = true },
            new() { Id = 2, Name = "Idea a medias", Relikario = "yuno", Cards = training.Take(30).ToList(), Traps = [], Valid = false, Active = false }
        };
        foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
        {
            GetWindow().Size = size; GetWindow().ContentScaleSize = size; await Frames();
            async Task Capture(string name)
            {
                if (directory == null || DisplayServer.GetName() == "headless") return;
                System.IO.Directory.CreateDirectory(directory);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, $"{name}-{size.X}x{size.Y}.png"));
            }
            void Inside(Control control, string what)
            {
                var rect = control.GetGlobalRect();
                Expect(rect.Position.X >= -1 && rect.End.X <= size.X + 1, $"{what} se sale del ancho en {size.X}x{size.Y}.");
            }
            var screen = new DeckScreen { Connection = _lobby, PreviewDecks = samples };
            AddChild(screen); await Frames();
            var rows = screen.FindChild("DeckRows", true, false)!.GetChild(0);
            Expect(rows.GetChildCount() == 3, "faltan el mazo de entrenamiento o los guardados.");
            var active = (Control)rows.GetNode("Deck1"); var draft = (Control)rows.GetNode("Deck2"); var builtIn = (Control)rows.GetNode("Training");
            Expect(active.FindChild("ActiveBadge", true, false) != null && builtIn.FindChild("ActiveBadge", true, false) == null, "el mazo activo no se distingue.");
            Expect(((Button)active.FindChild("Use", true, false)).Disabled && ((Button)draft.FindChild("Use", true, false)).Disabled &&
                !((Button)builtIn.FindChild("Use", true, false)).Disabled, "Usar debe ofrecerse solo en mazos legales que no están en uso.");
            Expect(((Label)draft.FindChild("DeckSummary", true, false)).Text.Contains("Borrador") && builtIn.FindChild("Delete", true, false) == null &&
                builtIn.FindChild("Edit", true, false) == null, "el borrador o el mazo de entrenamiento se presentan mal.");
            Expect(new[] { builtIn, active, draft }.All(row => row.FindChild("RelikarioThumbnail", true, false) is TextureRect { Texture: not null } thumb &&
                thumb.GetGlobalRect().Position.X >= row.GetGlobalRect().Position.X && thumb.GetGlobalRect().End.X <= row.GetGlobalRect().End.X),
                "los mazos de Yuno deben mostrar su miniatura dentro de la fila.");
            foreach (var button in screen.FindChildren("*", "Button", true, false).OfType<Button>().Where(b => b.IsVisibleInTree()))
            {
                Inside(button, "Un botón de la lista");
                Expect(button.Size.Y >= 44, "un botón de la lista es demasiado bajo para tocarlo.");
            }
            await Capture("deck-list");

            screen.Edit(0, "Mazo nuevo", Enumerable.Repeat(CardCatalog.Attack.Id, 12).ToList(), []); await Frames();
            var editor = screen.Editor!;
            Expect(!editor.Dirty && editor.Problems.Count > 0 && ((Button)editor.FindChild("SaveDeck", true, false)).Text == "Guardar borrador",
                "un mazo nuevo debe empezar como borrador sin cambios.");
            Expect(editor.AddBlocked(CardCatalog.Attack) != null, "se permite un Ataque de más.");
            var playable = CardCatalog.All.Values.Where(card => !DeckRules.IsAttack(card) && !DeckRules.IsTrap(card)).OrderBy(card => card.Kind == CardKind.Skill ? 0 : 1).ToList();
            foreach (var card in playable.Take(15)) { editor.Add(card); editor.Add(card); }
            Expect(editor.AddBlocked(playable[0])!.Contains("2 copias") && editor.Main.Count == 42 && editor.AddBlocked(playable[20])!.Contains("42"),
                "no se respetan las dos copias por nombre o el tope de 42.");
            editor.Add(CardCatalog.SuperMagnet); editor.Add(CardCatalog.SuperMagnet);
            Expect(editor.AddBlocked(CardCatalog.SuperMagnet)!.Contains("2 copias"), "se permite una tercera copia de una Trampa.");
            editor.Add(CardCatalog.ZahiraFlair);
            Expect(editor.AddBlocked(CardCatalog.SuperAttack)!.Contains("hasta 3") && editor.Traps.Count == 3, "se permite una cuarta Trampa.");
            await Frames();
            Expect(editor.Dirty && editor.Problems.Count == 0 && ((Button)editor.FindChild("SaveDeck", true, false)).Text == "Guardar" &&
                ((Label)editor.FindChild("DeckTotals", true, false)).Text.Contains("42/42") &&
                ((Label)editor.FindChild("DeckProblems", true, false)).Text.Contains("listo"), "un mazo legal no se presenta como listo.");
            Expect(((Label)editor.FindChild("Catalog_" + playable[0].Id, true, false)!.GetNode("Copies")).Text == "×2", "el catálogo no muestra las copias incluidas.");
            foreach (var name in new[] { "SaveDeck", "EditorBack", "DeckName", "DeckSide", "CatalogGallery" })
                Inside((Control)editor.FindChild(name, true, false), name);
            var saved = (name: "", main: 0, traps: 0);
            editor.SaveRequested += (name, main, traps) => saved = (name, main.Count, traps.Count);
            ((Button)editor.FindChild("SaveDeck", true, false)).EmitSignal(BaseButton.SignalName.Pressed);
            Expect(saved == ("Mazo nuevo", 42, 3), "guardar no entrega el nombre, las 42 cartas y las 3 Trampas.");
            editor.Remove(playable[0]);
            Expect(editor.Problems.Count > 0 && editor.Main.Count == 41, "quitar una carta no actualiza el mazo.");
            editor.Add(playable[0]);
            await Capture("deck-editor");
            RemoveChild(screen); screen.QueueFree(); await Frames();

            var guest = new DeckScreen { Connection = _lobby, PreviewDecks = [], Guest = true };
            AddChild(guest); await Frames();
            var guestRows = guest.FindChild("DeckRows", true, false)!.GetChild(0);
            Expect(guestRows.GetChildCount() == 1 && guest.FindChild("Copy", true, false) == null && guest.FindChild("GuestNotice", true, false) is Label { Visible: true } &&
                !((Control)guest.FindChild("NewDeck", true, false)).Visible && guest.FindChild("ActiveBadge", true, false) != null,
                "un invitado debe ver solo el mazo de entrenamiento, sin crear ni duplicar.");
            Inside((Control)guest.FindChild("GuestNotice", true, false), "El aviso de invitado");
            await Capture("deck-guest");
            RemoveChild(guest); guest.QueueFree(); await Frames();
        }
        GD.Print("PVP_DECKS_PASS · lista, mazo activo, borrador, límites de copias y Trampas, guardado e invitado; cuatro tamaños.");
    }
}
