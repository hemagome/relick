using Godot;
using Relick.Core;
using Relick.Network;
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
            new() { Id = 1, Name = "Yuno agresivo", Relikario = "yuno", Cards = training, Traps = CardCatalog.TrainingTraps(), Side = ["senshuni", "dodge"],
                Sleeve = "oro", Dice = "obsidiana", Valid = true, Active = true },
            new() { Id = 2, Name = "Idea a medias", Relikario = "yuno", Cards = training.Take(30).ToList(), Traps = [],
                Sleeve = "medianoche", Dice = "violeta", Valid = false, Active = false }
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
            void Pointer(Vector2 point, MouseButton button, bool pressed) => GetViewport().PushInput(new InputEventMouseButton
            {
                Position = point, GlobalPosition = point, ButtonIndex = button, Pressed = pressed,
                ButtonMask = !pressed ? 0 : button == MouseButton.Right ? MouseButtonMask.Right : MouseButtonMask.Left
            }, true);
            void Motion(Vector2 point, Vector2 relative = default, MouseButtonMask buttons = 0) => GetViewport().PushInput(new InputEventMouseMotion
            {
                Position = point, GlobalPosition = point, Relative = relative, ButtonMask = buttons
            }, true);
            void RightClick(Control control)
            {
                var point = control.GetGlobalRect().GetCenter();
                Motion(point);
                Pointer(point, MouseButton.Right, true);
                Pointer(point, MouseButton.Right, false);
            }
            void Drag(Control source, Control target)
            {
                var from = source.GetGlobalRect().GetCenter();
                var to = target.GetGlobalRect().GetCenter();
                var start = new Vector2(-16, 0);
                Motion(from);
                Pointer(from, MouseButton.Left, true);
                Motion(from + start, start, MouseButtonMask.Left);
                Motion(to, to - from - start, MouseButtonMask.Left);
                Expect(GetViewport().GuiIsDragging(), "la colección no inicia un arrastre de carta.");
                Pointer(to, MouseButton.Left, false);
            }
            var screen = new DeckScreen { Connection = _lobby, PreviewDecks = samples };
            AddChild(screen); await Frames();
            var rows = screen.FindChild("DeckRows", true, false)!.GetChild(0);
            Expect(rows.GetChildCount() == 3, "faltan el mazo de entrenamiento o los guardados.");
            var active = (Control)rows.GetNode("Deck1"); var draft = (Control)rows.GetNode("Deck2"); var builtIn = (Control)rows.GetNode("Training");
            Expect(active.FindChild("ActiveBadge", true, false) != null && builtIn.FindChild("ActiveBadge", true, false) == null, "el mazo activo no se distingue.");
            Expect(((Button)active.FindChild("Use", true, false)).Disabled && ((Button)draft.FindChild("Use", true, false)).Disabled &&
                !((Button)builtIn.FindChild("Use", true, false)).Disabled, "Usar debe ofrecerse solo en mazos legales que no están en uso.");
            foreach (var (deckRow, sleeveId, diceId) in new[] { (active, "oro", "obsidiana"), (draft, "medianoche", "violeta"),
                (builtIn, CosmeticCatalog.DefaultSleeve, CosmeticCatalog.DefaultDice) })
            {
                var back = deckRow.FindChild("DeckSleeve", true, false) as TextureRect;
                var die = deckRow.FindChild("DeckDice", true, false) as DiceSwatch;
                Expect(back?.Texture == SleeveArt.Back(sleeveId) && die?.Style == diceId, "la fila no muestra la mica y el dado del mazo.");
                Expect(deckRow.GetGlobalRect().Encloses(back!.GetGlobalRect()) && deckRow.GetGlobalRect().Encloses(die!.GetGlobalRect()),
                    "la mica o el dado se salen de la fila.");
                Expect(deckRow.FindChild("ReadyMark", true, false) == null && deckRow.FindChild("DeckSummary", true, false) == null,
                    "la fila conserva el check o los conteos.");
            }
            Expect(builtIn.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.Text == "Mica Transparente"),
                "la lista no llama Transparente a la mica predeterminada.");
            Expect(draft.FindChild("DeckProblem", true, false) is Label problem && problem.Text.StartsWith("Borrador") &&
                builtIn.FindChild("Delete", true, false) == null &&
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
            Expect(picker.Tiles.Count == 18 && RelikarioCatalog.Roster.Count(r => r.Playable) == 3 && picker.Selected.Id == "yuno",
                "el selector no lista los 18 Relikarios con Yuno elegido.");
            Expect(picker.Tiles["yuno"].FindChild("Portrait", true, false) is TextureRect { Texture: not null } &&
                picker.Tiles["osamu"].FindChild("Portrait", true, false) is TextureRect { Texture: not null } &&
                picker.Tiles["wd"].FindChild("Soon", true, false) != null, "Yuno y Osamu deben mostrar sus retratos, y WD debe marcarse como próximamente.");
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
            Expect(picker.FindChild("CreateDeck", true, false) is Button { Disabled: false } osamu && osamu.Text.Contains("Osamu") &&
                RelikarioPicker.Card("osamu", 1) != null && RelikarioPicker.Card("osamu", 2) != null,
                "Osamu no muestra sus dos cartas o no permite crear su mazo.");
            picker.Tiles["ironwing"].EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Expect(picker.FindChild("CreateDeck", true, false) is Button { Disabled: false } ironwing && ironwing.Text.Contains("Ironwing") &&
                RelikarioPicker.Card("ironwing", 1) != null && RelikarioPicker.Card("ironwing", 2) != null &&
                picker.Tiles["ironwing"].FindChild("Portrait", true, false) is TextureRect { Texture: not null },
                "Ironwing debe aparecer con retrato, dos cartas y un mazo seleccionable.");
            picker.Tiles["wd"].EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Expect(picker.FindChild("CreateDeck", true, false) is Button { Disabled: true, Text: "Próximamente" } && picker.FindChild("ComingSoonNote", true, false) != null,
                "un Relikario sin programar permite crear un mazo.");
            if (picker.FindChild("FilterAvailable", true, false) is Button { Visible: true } available)
            {
                available.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
                Expect(picker.Tiles.Values.Count(tile => tile.Visible) == 3 && picker.Selected.Id == "yuno", "el filtro Disponibles debe mostrar a Yuno, Osamu e Ironwing.");
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
            Expect(editor.FindChild("Catalog_" + CardCatalog.EquipmentRaid.Id, true, false) is HandCard raid &&
                raid.FaceTexture?.ResourcePath == "res://assets/cards/robo-de-equipos-clean.png",
                "el catálogo debe mostrar la ilustración de Misión: Robo de Equipos.");
            Expect(!editor.Dirty && editor.Problems.Count > 0 && ((Button)editor.FindChild("SaveDeck", true, false)).Text == "Guardar borrador",
                "un mazo nuevo debe empezar como borrador sin cambios.");
            Expect(editor.AddBlocked(CardCatalog.Attack) != null, "se permite un Ataque de más.");
            var playable = CardCatalog.All.Values.Where(card => !DeckRules.IsAttack(card) && !DeckRules.IsTrap(card) && DeckRules.Compatible(card, editor.Relikario))
                .OrderBy(card => card.Kind == CardKind.Skill ? 0 : 1).ToList();
            foreach (var card in playable.Take(15)) { editor.Add(card); editor.Add(card); }
            Expect(editor.AddBlocked(playable[0])!.Contains("2 copias") && editor.Main.Count == 42 && editor.AddBlocked(playable[20])!.Contains("42"),
                "no se respetan las dos copias por nombre o el tope de 42.");
            editor.Add(CardCatalog.SuperMagnet); editor.Add(CardCatalog.SuperMagnet);
            Expect(editor.AddBlocked(CardCatalog.SuperMagnet)!.Contains("2 copias"), "se permite una tercera copia de una Trampa.");
            editor.Add(CardCatalog.ZahiraFlair);
            Expect(editor.AddBlocked(CardCatalog.SuperAttack)!.Contains("hasta 3") && editor.Traps.Count == 3, "se permite una cuarta Trampa.");
            await Frames();
            Expect(editor.Dirty && editor.Problems.Count == 0 && ((Button)editor.FindChild("SaveDeck", true, false)).Text == "Guardar" &&
                editor.FindChild("DeckTotals", true, false)!.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.Text == "42/42") &&
                ((Button)editor.FindChild("DeckProblems", true, false)).Text.Contains("listo"), "un mazo legal no se presenta como listo.");
            Expect(((Label)editor.FindChild("Catalog_" + playable[0].Id, true, false)!.GetNode("Copies")).Text == "×2", "el catálogo no muestra las copias incluidas.");
            foreach (var name in new[] { "SaveDeck", "EditorBack", "DeckName", "DeckPanel", "ListTabs", "CatalogGallery", "InspectorPanel", "DeckProblems" })
                Inside((Control)editor.FindChild(name, true, false), name);
            var artwork = (TextureRect)editor.FindChild("InspectorArtwork", true, false)!;
            var inspector = (Control)editor.FindChild("InspectorPanel", true, false)!;
            var inspectorCopies = (Label)editor.FindChild("InspectorCopies", true, false)!;
            Expect(inspector.GetGlobalRect().Encloses(artwork.GetGlobalRect()) && inspector.GetGlobalRect().Encloses(inspectorCopies.GetGlobalRect()) &&
                ((Control)editor.FindChild("DeckProblems", true, false)!).GetGlobalRect().End.Y <= size.Y + 1,
                $"la carta del inspector, los controles o los requisitos se cortan en {size.X}x{size.Y}.");
            var mainGrid = (GridContainer)editor.FindChild("DeckCards", true, false)!;
            var attackInDeck = (HandCard)mainGrid.GetNode("Deck_attack");
            var attackInCatalog = (HandCard)editor.FindChild("Catalog_attack", true, false)!;
            GuiInputTestDriver.Click(attackInDeck);
            Expect(artwork.Texture == attackInDeck.FaceTexture && inspectorCopies.Text == "12 copias" && editor.Main.Count == 42,
                "el clic izquierdo no inspecciona la carta completa o cambia el mazo.");
            RightClick(attackInDeck);
            Expect(editor.Main.Count == 41 && inspectorCopies.Text == "11 copias" && editor.Problems.Count > 0,
                "el clic derecho sobre una pila no quita exactamente una copia.");
            GetViewport().PushInput(new InputEventKey { Keycode = Key.Z, CtrlPressed = true, Pressed = true }, true);
            GetViewport().PushInput(new InputEventKey { Keycode = Key.Z, CtrlPressed = true }, true);
            Expect(editor.Main.Count == 42 && inspectorCopies.Text == "12 copias", "Ctrl+Z no restaura la copia retirada.");
            RightClick(attackInDeck);
            RightClick(attackInCatalog);
            Expect(editor.Main.Count == 42 && editor.Problems.Count == 0, "el clic derecho de la colección no añade una sola copia.");
            RightClick(attackInDeck);
            Drag(attackInCatalog, attackInDeck);
            Expect(editor.Main.Count == 42, "soltar una carta sobre el mazo no añade una copia.");

            var grouping = (CheckButton)editor.FindChild("GroupCopies", true, false)!;
            grouping.ButtonPressed = false;
            Expect(mainGrid.GetChildCount() == 42 && mainGrid.HasNode("Deck_attack_12"), "la cuadrícula no muestra copias individuales.");
            grouping.ButtonPressed = true;
            Expect(mainGrid.GetChildCount() == 16, "agrupar no devuelve una ficha por carta.");
            GuiInputTestDriver.Click((Button)editor.FindChild("InspectArtwork", true, false)!);
            Expect(editor.FindChild("DeckCardZoom", true, false) is AcceptDialog { Visible: true } &&
                editor.FindChild("EnlargedCard", true, false) is TextureRect enlarged && enlarged.Texture == artwork.Texture,
                "la ampliación no muestra el arte seleccionado.");
            ((AcceptDialog)editor.FindChild("DeckCardZoom", true, false)!).Hide();
            await Frames();
            CardDefinition[] variants = [CardCatalog.AttackYuno, CardCatalog.AttackYunoDt];
            HandCard[] AttackTiles(DeckEditor view) => view.FindChild("CatalogCards", true, false)!.GetChildren()
                .OfType<HandCard>().Where(tile => DeckRules.IsAttack(tile.Card)).ToArray();
            Expect(AttackTiles(editor).Select(tile => tile.Card).ToHashSet().SetEquals(variants.Append(CardCatalog.Attack)),
                "la colección de Yuno debe mostrar solo sus tres artes.");
            var alternates = variants.Select(card => (HandCard)editor.FindChild("Catalog_" + card.Id, true, false)!).ToArray();
            Expect(alternates.All(tile => tile.FaceTexture?.ResourcePath == $"res://assets/cards/{tile.Card.Id}-clean.png") &&
                alternates.Append(attackInCatalog).Select(tile => tile.FaceTexture).Distinct().Count() == variants.Length + 1,
                "la colección no distingue todos los artes de Ataque.");
            Expect(variants.All(card => editor.AddBlocked(card) != null),
                "un arte alternativo permite superar los doce Ataques.");
            var catalogScroll = (ScrollContainer)editor.FindChild("CatalogGallery", true, false)!;
            foreach (var alternate in alternates)
            {
                RightClick(attackInDeck);
                catalogScroll.EnsureControlVisible(alternate); await Frames();
                RightClick(alternate); await Frames();
                Expect(artwork.Texture == alternate.FaceTexture && inspectorCopies.Text == "1 copia" && mainGrid.HasNode("Deck_" + alternate.Card.Id),
                    "el editor no cuenta ni muestra el arte elegido en su propia ficha.");
            }
            Expect(editor.Main.Count == 42 && editor.Problems.Count == 0 && editor.AddBlocked(CardCatalog.Attack) != null,
                "mezclar artes cambia el total autorizado de Ataques.");
            ((Button)editor.FindChild("TrapTab", true, false)!).EmitSignal(BaseButton.SignalName.Pressed);
            await Frames();
            Expect(mainGrid.GetChildCount() == 2 && mainGrid.HasNode("Deck_super-magnet") && !attackInCatalog.Visible &&
                editor.FindChild("Catalog_zahira-flair", true, false) is HandCard { Visible: true }, "Trampas no separa su mazo y su catálogo.");

            // Side deck: up to ten non-Attack cards, sharing copy limits with the deck.
            var benchable = playable.Skip(15).Where(card => DeckRules.Compatible(card, "yuno")).ToList();
            Expect(editor.AddBlocked(CardCatalog.Attack, toSide: true)!.Contains("Ataque"), "el banquillo admite Ataques.");
            Expect(editor.AddBlocked(playable[0], toSide: true)!.Contains("2 copias"), "el banquillo ignora las copias del mazo.");
            Drag((Control)editor.FindChild("Catalog_zahira-flair", true, false)!, (Control)editor.FindChild("SideTab", true, false)!);
            Expect(editor.EditingSide && editor.Side.SequenceEqual([CardCatalog.ZahiraFlair.Id]) && editor.Traps.Count == 3,
                "soltar en la pestaña Banquillo no añade una copia a esa sección.");
            GuiInputTestDriver.Click((Button)editor.FindChild("UndoDeck", true, false)!);
            await Frames();
            Expect(editor.EditingSide && mainGrid.GetChildCount() == 0 && !attackInCatalog.Visible, "la pestaña Banquillo no muestra su cuadrícula sin Ataques.");
            var search = (LineEdit)editor.FindChild("CatalogSearch", true, false)!;
            GuiInputTestDriver.ReplaceText(search, benchable[0].Name);
            await Frames();
            var catalogTile = (HandCard)editor.FindChild("Catalog_" + benchable[0].Id, true, false)!;
            GuiInputTestDriver.Click(catalogTile);
            Expect(editor.Side.Count == 0 && artwork.Texture == catalogTile.FaceTexture, "el clic en la colección debe servir para inspeccionar.");
            Drag(catalogTile, (Control)editor.FindChild("DeckList", true, false)!);
            Expect(editor.Side.SequenceEqual([benchable[0].Id]), "no se puede arrastrar a una cuadrícula vacía.");
            GuiInputTestDriver.Click((Button)editor.FindChild("UndoDeck", true, false)!);
            Expect(editor.Side.Count == 0, "Deshacer no revierte el arrastre al banquillo.");
            GuiInputTestDriver.Click((Button)editor.FindChild("More", true, false)!);
            Expect(editor.Side.SequenceEqual([benchable[0].Id]) && editor.Main.Count == 42, "el botón + no añade al banquillo.");
            search.Clear();
            Expect(editor.FindChild("Catalog_" + benchable[0].Id, true, false)! is var benched && !((Label)benched.GetNode("Copies")).Visible &&
                ((Control)benched.GetNode("Bench")).Visible && ((Label)benched.FindChild("BenchCount", true, false)!).Text == "1", "el catálogo no distingue las copias del banquillo.");
            foreach (var card in benchable.Skip(1).Take(9)) editor.Add(card, toSide: true);
            Expect(editor.Side.Count == 10 && editor.AddBlocked(benchable[0], toSide: true)!.Contains("hasta 10"), "el banquillo pasa de diez cartas.");
            Expect(editor.FindChild("Move", true, false) is Button move && move.AccessibilityName.Contains("Principal"),
                "el inspector del banquillo no ofrece pasar al mazo.");
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
            Expect(editor.FindChild("StyleHelp", true, false) == null, "la pestaña Estilo conserva el texto explicativo.");
            Expect(editor.FindChild("Sleeve_" + CosmeticCatalog.DefaultSleeve, true, false) is Button { TooltipText: "Transparente" },
                "el selector no llama Transparente a la mica predeterminada.");
            ((Button)editor.FindChild("Catalog_" + playable[0].Id, true, false)!).EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Expect(editor.Main.Count == cardsBefore, "pulsar el catálogo en la pestaña Estilo añade cartas.");
            ((Button)editor.FindChild("Sleeve_oro", true, false)!).EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            ((Button)editor.FindChild("Dice_obsidiana", true, false)!).EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Expect(editor.Sleeve == "oro" && editor.Dice == "obsidiana" && editor.Dirty, "elegir mica y dado no cambia el mazo editado.");
            foreach (var button in editor.FindChild("DeckStyle", true, false)!.FindChildren("*", "Button", true, false).OfType<Button>().Where(b => b.IsVisibleInTree()))
                Inside(button, "Una opción de estilo");
            await Capture("deck-style");
            ((Button)editor.FindChild("DeckTab", true, false)).EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Expect(!editor.EditingStyle && !editor.EditingSide, "volver al mazo deja abierta la pestaña Estilo.");

            var saved = (name: "", main: 0, traps: 0, side: 0, sleeve: "", dice: "");
            string[] savedAttacks = [];
            editor.SaveRequested += (name, main, traps, side, sleeve, die) =>
            {
                saved = (name, main.Count, traps.Count, side.Count, sleeve, die);
                savedAttacks = main.Where(id => DeckRules.IsAttack(CardCatalog.Get(id))).ToArray();
            };
            ((Button)editor.FindChild("SaveDeck", true, false)).EmitSignal(BaseButton.SignalName.Pressed);
            Expect(saved == ("Mazo nuevo", 42, 3, 10, "oro", "obsidiana"), "guardar no entrega el nombre, las cartas, las Trampas, el banquillo y el estilo.");
            Expect(savedAttacks.Count(id => id == CardCatalog.Attack.Id) == DeckRules.AttackCount - variants.Length &&
                variants.All(card => savedAttacks.Count(id => id == card.Id) == 1), "guardar pierde la mezcla de artes de Ataque.");
            editor.Remove(playable[0]);
            Expect(editor.Problems.Count > 0 && editor.Main.Count == 41, "quitar una carta no actualiza el mazo.");
            editor.Add(playable[0]);
            await Capture("deck-editor");
            ((Button)editor.FindChild("SideTab", true, false)!).EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            foreach (var button in editor.FindChild("DeckList", true, false)!.FindChildren("*", "Button", true, false).OfType<Button>().Where(b => b.IsVisibleInTree()))
                Inside(button, "Un botón del banquillo");
            await Capture("deck-side");

            foreach (var (relikario, initial, ns, dt) in new[]
            {
                (RelikarioCatalog.Osamu.Id, CardCatalog.Attack, CardCatalog.AttackOsamu, CardCatalog.AttackOsamuDt),
                (RelikarioCatalog.Ironwing.Id, CardCatalog.AttackIronwingUt, CardCatalog.AttackIronwingNs, CardCatalog.AttackIronwingDt)
            })
            {
                screen.Edit(0, "Mazo anterior", [CardCatalog.Attack.Id, CardCatalog.AttackYuno.Id, ns.Id], [], relikario: relikario);
                await Frames();
                var ownerEditor = screen.Editor!;
                var tiles = AttackTiles(ownerEditor);
                Expect(tiles.Select(tile => tile.Card).ToHashSet().SetEquals([initial, ns, dt]) &&
                    tiles.All(tile => tile.FaceTexture != null) && tiles.Select(tile => tile.FaceTexture).Distinct().Count() == 3,
                    $"la colección de {relikario} debe mostrar sus tres artes sin duplicados.");
                Expect(ownerEditor.Main.SequenceEqual([initial.Id, initial.Id, ns.Id]) && ownerEditor.AddBlocked(CardCatalog.AttackYuno) != null,
                    $"un mazo anterior de {relikario} debe sustituir los artes ajenos por el inicial.");
            }
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
        if (OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--deck-save-server=")) is { } server)
            await CheckDeckSaveAndLeave(server.Split('=', 2)[1]);
    }

    private async Task CheckDeckSaveAndLeave(string server)
    {
        using var connection = new LobbyConnection();
        void Expect(bool ok, string reason) { if (!ok) throw new Exception("Guardado de mazos: " + reason); }
        async Task WaitFor(Func<bool> condition)
        {
            var started = Time.GetTicksMsec();
            while (!condition())
            {
                Expect(Time.GetTicksMsec() - started < 15_000, "tiempo agotado esperando el guardado o la conexión.");
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                connection.Tick();
            }
        }
        static void Press(Control editor, string name) => ((Button)editor.FindChild(name, true, false)!).EmitSignal(BaseButton.SignalName.Pressed);
        static bool Warns(DeckEditor editor) => editor.GetChildren().OfType<ConfirmationDialog>().Any(dialog => dialog.Visible);
        var database = OS.GetEnvironment("RELICK_DATABASE");
        connection.Connect(server, database.Length > 0 ? database : "relick-deck-save-test", null);
        await WaitFor(() => connection.Ready);
        var screen = new DeckScreen { Connection = connection };
        AddChild(screen);
        var sound = GetNode<SoundDirector>("/root/SoundDirector");
        var saveSounds = 0;
        void CountSaveSound(SoundCue cue) { if (cue == SoundCue.DeckSaved) saveSounds++; }
        sound.SoundPlayed += CountSaveSound;
        try
        {
            var cards = CardCatalog.TrainingDeck().Where(id => DeckRules.Compatible(CardCatalog.Get(id), RelikarioCatalog.Osamu.Id))
                .Concat([CardCatalog.TurboSword.Id, CardCatalog.EquipmentRaid.Id, CardCatalog.PurpleRing.Id]).ToList();
            screen.Edit(0, "Prueba", cards, CardCatalog.TrainingTraps(), relikario: RelikarioCatalog.Osamu.Id);
            var editor = screen.Editor!;
            editor.ChooseStyle(sleeve: "oro", dice: "obsidiana");
            Press(editor, "SaveDeck");
            Expect(connection.DeckPending, "el guardado no se envió.");
            Expect(saveSounds == 0, "el sonido se reproduce antes de que el servidor confirme el guardado.");
            Press(editor, "EditorBack");
            Expect(screen.Editor == editor && !Warns(editor), "volver mientras se guarda muestra el aviso de cambios sin guardar.");
            Expect(!editor.CanProcess() && editor.FindChild("SaveDeck", true, false) is Button { Disabled: true, Text: "Guardando…" },
                "el mazo permite editar o repetir el guardado mientras espera confirmación.");
            await WaitFor(() => screen.Editor == null || connection.DeckError.Length > 0);
            Expect(connection.DeckError.Length == 0, connection.DeckError);
            var saved = connection.Decks.Single();
            Expect(saved.Sleeve == "oro" && saved.Dice == "obsidiana" && saved.Relikario == RelikarioCatalog.Osamu.Id &&
                saved.Cards.Order().SequenceEqual(cards.Order()), "la lista no recuperó el mazo de Osamu con las cartas y el estilo guardados.");
            Expect(saveSounds == 1, "crear el mazo debe sonar una sola vez.");
            Press((Control)screen.FindChild("Deck" + saved.Id, true, false)!, "Edit");
            editor = screen.Editor!;
            Press(editor, "EditorBack");
            Expect(screen.Editor == null && !Warns(editor), "volver sin editar el mazo guardado muestra el aviso.");

            Press((Control)screen.FindChild("Deck" + saved.Id, true, false)!, "Edit");
            editor = screen.Editor!;
            editor.ChooseStyle(sleeve: CosmeticCatalog.DefaultSleeve);
            Press(editor, "SaveDeck");
            Expect(saveSounds == 1, "el sonido se reproduce antes de que el servidor confirme la actualización.");
            await WaitFor(() => screen.Editor == null || connection.DeckError.Length > 0);
            Expect(connection.DeckError.Length == 0 && connection.Decks.Single().Sleeve == CosmeticCatalog.DefaultSleeve,
                "la actualización no guardó la mica: " + connection.DeckError);
            Expect(saveSounds == 2, "actualizar el mazo debe sonar una sola vez.");

            screen.Edit(ulong.MaxValue, saved.Name, saved.Cards, saved.Traps);
            editor = screen.Editor!;
            editor.ChooseStyle(sleeve: "oro");
            Press(editor, "SaveDeck");
            await WaitFor(() => connection.DeckError.Length > 0 && !((Button)editor.FindChild("SaveDeck", true, false)!).Disabled);
            Expect(saveSounds == 2, "un guardado rechazado reproduce el sonido de confirmación.");
            Press(editor, "EditorBack");
            Expect(screen.Editor == editor && Warns(editor), "un guardado rechazado permite salir sin avisar de los cambios pendientes.");
        }
        finally { sound.SoundPlayed -= CountSaveSound; RemoveChild(screen); screen.QueueFree(); }
        GD.Print("PVP_DECK_SAVE_AND_LEAVE_PASS · espera la confirmación, reabre el mazo guardado y conserva los cambios rechazados.");
    }
}
