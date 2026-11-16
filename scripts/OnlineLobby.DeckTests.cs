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

            // A new deck starts by choosing its Relikario.
            ((Button)screen.FindChild("NewDeck", true, false)).EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            var picker = screen.Picker!;
            Expect(picker.Tiles.Count == 18 && RelikarioCatalog.Roster.Count(r => r.Playable) == 1 && picker.Selected.Id == "yuno",
                "el selector no lista los 18 Relikarios con Yuno elegido.");
            Expect(picker.Tiles["yuno"].FindChild("Portrait", true, false) is TextureRect { Texture: not null } &&
                picker.Tiles["osamu"].FindChild("Soon", true, false) != null, "Yuno no muestra su retrato o un Relikario sin programar no se marca como próximamente.");
            foreach (var name in new[] { "PickerBack", "RosterGallery", "RelikarioDetail", "CreateDeck", "CardButton" })
                Inside((Control)picker.FindChild(name, true, false), name);
            var gallery = (Control)picker.FindChild("RosterGallery", true, false);
            foreach (var tile in picker.Tiles.Values.Where(tile => tile.IsVisibleInTree() && tile.GetGlobalRect().Position.Y < gallery.GetGlobalRect().End.Y))
            {
                Expect(tile.GetGlobalRect().End.X <= gallery.GetGlobalRect().End.X + 1 && tile.Size.Y >= 44, $"una ficha se sale de la cuadrícula en {size.X}x{size.Y}.");
                var art = tile.FindChild("Portrait", true, false) as Control ?? tile;
                Expect(art == tile || Math.Abs(art.Size.X - art.Size.Y) <= 1, "el retrato de la ficha no es cuadrado.");
            }
            picker.Tiles["osamu"].EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Expect(picker.FindChild("CreateDeck", true, false) is Button { Disabled: true, Text: "Próximamente" } && picker.FindChild("ComingSoonNote", true, false) != null,
                "un Relikario sin programar permite crear un mazo.");
            if (picker.FindChild("FilterAvailable", true, false) is Button { Visible: true } available)
            {
                available.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
                Expect(picker.Tiles.Values.Count(tile => tile.Visible) == 1 && picker.Selected.Id == "yuno", "el filtro Disponibles no deja solo a Yuno.");
                ((Button)picker.FindChild("FilterAll", true, false)).EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            }
            else picker.Tiles["yuno"].EmitSignal(BaseButton.SignalName.Pressed);
            await Frames();
            Expect(picker.FindChild("CreateDeck", true, false) is Button { Disabled: false } create && create.Text.Contains("Yuno"), "no se ofrece crear el mazo de Yuno.");
            ((Button)picker.FindChild("CardButton", true, false)).EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
            Expect(picker.Level == 2 && ((TextureRect)picker.FindChild("RelikarioCard", true, false)).Texture == RelikarioPicker.Card("yuno", 2) &&
                ((Button)picker.FindChild("Level2", true, false)).ButtonPressed, "tocar la carta no la gira al nivel 2.");
            Expect(RelikarioPicker.Monogram("Yuno 8 bits") == "Y8B" && RelikarioPicker.Monogram("Zii Jay") == "ZJ", "las iniciales de los Relikarios sin retrato son incorrectas.");
            foreach (var button in picker.FindChildren("*", "Button", true, false).OfType<Button>().Where(b => b.IsVisibleInTree() && b.GetGlobalRect().Position.Y < size.Y))
                Expect(button.Size.Y >= 40, "un botón del selector es demasiado bajo para tocarlo.");
            await Capture("relikario-picker");
            ((Button)picker.FindChild("PickerBack", true, false)).EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Expect(screen.Picker == null && screen.FindChild("DeckRows", true, false) != null, "volver desde el selector no regresa a la lista de mazos.");
            ((Button)screen.FindChild("NewDeck", true, false)).EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            ((Button)screen.Picker!.FindChild("CreateDeck", true, false)).EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            var editor = screen.Editor!;
            Expect(editor.Relikario == "yuno" && editor.Main.Count == 12, "elegir a Yuno no abre el editor con sus 12 Ataques.");
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
                ((Button)editor.FindChild("DeckTab", true, false)).Text.Contains("42/42") &&
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
            Expect(editor.FindChild("Catalog_" + benchable[0].Id, true, false)! is var benched && !((Label)benched.GetNode("Copies")).Visible &&
                ((Control)benched.GetNode("Bench")).Visible && ((Label)benched.FindChild("BenchCount", true, false)!).Text == "1", "el catálogo no distingue las copias del banquillo.");
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
            Expect(!editor.EditingSide && editor.Problems.Count == 0 && ((Button)editor.FindChild("SideTab", true, false)).Text.Contains("10/10"),
                "volver al mazo pierde el banquillo o los totales.");

            // Style: a tab of its own; picking only changes the look, and the save carries it.
            var cardsBefore = editor.Main.Count;
            ((Button)editor.FindChild("StyleTab", true, false)).EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Expect(editor.EditingStyle && editor.FindChild("SleeveOptions", true, false) is HFlowContainer sleeves &&
                sleeves.GetChildCount() == CosmeticCatalog.Sleeves.Count && editor.FindChild("DiceOptions", true, false) is HFlowContainer dice &&
                dice.GetChildCount() == CosmeticCatalog.Dice.Count, "la pestaña Estilo no ofrece todas las micas y dados.");
            ((Button)editor.FindChild("Catalog_" + playable[0].Id, true, false)!).EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Expect(editor.Main.Count == cardsBefore, "pulsar el catálogo en la pestaña Estilo añade cartas.");
            ((Button)editor.FindChild("Sleeve_oro", true, false)!).EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            ((Button)editor.FindChild("Dice_obsidiana", true, false)!).EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Expect(editor.Sleeve == "oro" && editor.Dice == "obsidiana" && editor.Dirty, "elegir mica y dado no cambia el mazo editado.");
            foreach (var button in editor.FindChild("DeckList", true, false)!.FindChildren("*", "Button", true, false).OfType<Button>().Where(b => b.IsVisibleInTree()))
                Inside(button, "Una opción de estilo");
            await Capture("deck-style");
            ((Button)editor.FindChild("DeckTab", true, false)).EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Expect(!editor.EditingStyle && !editor.EditingSide, "volver al mazo deja abierta la pestaña Estilo.");

            var saved = (name: "", main: 0, traps: 0, side: 0, sleeve: "", dice: "");
            editor.SaveRequested += (name, main, traps, side, sleeve, die) => saved = (name, main.Count, traps.Count, side.Count, sleeve, die);
            ((Button)editor.FindChild("SaveDeck", true, false)).EmitSignal(BaseButton.SignalName.Pressed);
            Expect(saved == ("Mazo nuevo", 42, 3, 10, "oro", "obsidiana"), "guardar no entrega el nombre, las cartas, las Trampas, el banquillo y el estilo.");
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
