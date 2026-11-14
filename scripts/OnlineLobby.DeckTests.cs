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
            new() { Id = 1, Name = "Yuno agresivo", Relikario = "yuno", Cards = training, Traps = CardCatalog.TrainingTraps(), Side = ["senshuni", "dodge"], Valid = true, Active = true },
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
            Expect(((Label)active.FindChild("DeckSummary", true, false)).Text.Contains("2 en banquillo"), "la lista no muestra el banquillo del mazo.");
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
            foreach (var name in new[] { "SaveDeck", "EditorBack", "DeckName", "DeckPanel", "ListTabs", "CatalogGallery" })
                Inside((Control)editor.FindChild(name, true, false), name);

            // Side deck: up to ten non-Attack cards, sharing copy limits with the deck.
            var benchable = playable.Skip(15).Where(card => DeckRules.Compatible(card, "yuno")).ToList();
            Expect(editor.AddBlocked(CardCatalog.Attack, toSide: true)!.Contains("Ataque"), "el banquillo admite Ataques.");
            Expect(editor.AddBlocked(playable[0], toSide: true)!.Contains("2 copias"), "el banquillo ignora las copias del mazo.");
            ((Button)editor.FindChild("SideTab", true, false)).EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Expect(editor.EditingSide && editor.FindChild("SideHelp", true, false) != null, "la pestaña Banquillo no muestra el banquillo.");
            ((HandCard)editor.FindChild("Catalog_" + benchable[0].Id, true, false)!).EmitSignal(BaseButton.SignalName.Pressed);
            Expect(editor.Side.SequenceEqual([benchable[0].Id]) && editor.Main.Count == 42, "pulsar el catálogo en la pestaña Banquillo no lo añade al banquillo.");
            Expect(((Label)editor.FindChild("Catalog_" + benchable[0].Id, true, false)!.GetNode("Copies")).Text == "B1", "el catálogo no distingue las copias del banquillo.");
            foreach (var card in benchable.Skip(1).Take(9)) editor.Add(card, toSide: true);
            Expect(editor.Side.Count == 10 && editor.AddBlocked(benchable[0], toSide: true)!.Contains("hasta 10"), "el banquillo pasa de diez cartas.");
            Expect(editor.FindChild("Row_" + benchable[0].Id, true, false)?.FindChild("Move", true, false) != null, "las filas del banquillo no ofrecen pasar al mazo.");
            // Swapping one for one keeps both totals, as the swap between duels will.
            editor.Move(playable[1], toSide: true);
            Expect(editor.AddBlocked(playable[1], toSide: true) != null, "se permite pasar una carta al banquillo lleno.");
            editor.Move(benchable[0], toSide: false);
            Expect(editor.Main.Count == 42 && editor.AddBlocked(benchable[0])!.Contains("42") && editor.Side.Count == 10, "pasar al mazo lleno no se bloquea.");
            editor.Remove(benchable[9], fromSide: true); editor.Move(playable[1], toSide: true); editor.Move(benchable[0], toSide: false); editor.Add(benchable[9], toSide: true);
            Expect(editor.Main.Count == 42 && editor.Side.Count == 10 && editor.Main.Contains(benchable[0].Id) && editor.Side.Contains(playable[1].Id) &&
                editor.Problems.Count == 0, "un intercambio uno por uno no conserva los totales.");
            editor.Remove(benchable[0]); editor.Move(playable[1], toSide: false); editor.Add(benchable[0], toSide: true);
            Expect(editor.Side.Count == 10 && editor.Main.Count(id => id == playable[1].Id) == 2, "deshacer el intercambio no restaura el mazo.");
            ((Button)editor.FindChild("DeckTab", true, false)).EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Expect(!editor.EditingSide && editor.Problems.Count == 0 && ((Label)editor.FindChild("DeckTotals", true, false)).Text.Contains("Banquillo 10/10"),
                "volver al mazo pierde el banquillo o los totales.");

            var saved = (name: "", main: 0, traps: 0, side: 0);
            editor.SaveRequested += (name, main, traps, side) => saved = (name, main.Count, traps.Count, side.Count);
            ((Button)editor.FindChild("SaveDeck", true, false)).EmitSignal(BaseButton.SignalName.Pressed);
            Expect(saved == ("Mazo nuevo", 42, 3, 10), "guardar no entrega el nombre, las 42 cartas, las 3 Trampas y el banquillo.");
            editor.Remove(playable[0]);
            Expect(editor.Problems.Count > 0 && editor.Main.Count == 41, "quitar una carta no actualiza el mazo.");
            editor.Add(playable[0]);
            await Capture("deck-editor");
            editor.EditingSide = true; await Frames();
            foreach (var button in editor.FindChild("DeckList", true, false)!.FindChildren("*", "Button", true, false).OfType<Button>().Where(b => b.IsVisibleInTree()))
                Inside(button, "Un botón del banquillo");
            await Capture("deck-side");
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
        GD.Print("PVP_DECKS_PASS · lista, mazo activo, borrador, límites de copias, Trampas y banquillo, guardado e invitado; cuatro tamaños.");
    }
}
