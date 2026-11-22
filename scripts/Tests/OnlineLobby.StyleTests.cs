using Godot;
using Relick.Core;
using Relick.Network;
using Relick.Network.Types;

namespace Relick;

public partial class OnlineLobby
{
    // The screens outside the duel share one palette, type scale, set of corners, main action and way back.
    private async Task CheckMenuStyle()
    {
        async Task Frames() { for (var i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        void Expect(bool ok, string reason) { if (!ok) throw new Exception("Estilo de menús: " + reason); }

        static double Luminance(Color color)
        {
            static double Channel(float value) => value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
            return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
        }
        static double Contrast(Color a, Color b) =>
            (Math.Max(Luminance(a), Luminance(b)) + 0.05) / (Math.Min(Luminance(a), Luminance(b)) + 0.05);
        static bool Purple(Color color) => color.A > 0.1f && color.S > 0.2f && color.V > 0.08f && color.H is > 0.68f and < 0.88f;

        void Inspect(Node root, string screen)
        {
            var primaries = 0;
            foreach (var control in root.FindChildren("*", "Control", true, false).OfType<Control>())
            {
                // Cards draw their own faces, and cosmetics keep the colour the player picked.
                if (!control.IsVisibleInTree() || control is HandCard) continue;
                var text = control switch { Label label => label.Text, Button button => button.Text, LineEdit field => field.Text + field.PlaceholderText, _ => "" };
                if (text.Length > 0)
                {
                    var size = control.GetThemeFontSize("font_size");
                    Expect(MenuVisuals.Type.Scale.Contains(size), $"«{text}» en {screen} usa letra de {size}.");
                    Expect(!Purple(control.GetThemeColor("font_color")), $"«{text}» en {screen} está en morado.");
                }
                var box = control switch
                {
                    PanelContainer => control.GetThemeStylebox("panel"),
                    Button or LineEdit => control.GetThemeStylebox("normal"),
                    _ => null
                };
                if (box is StyleBoxFlat flat)
                {
                    Expect(flat.CornerRadiusTopLeft is 0 or MenuVisuals.Cut.Badge or MenuVisuals.Cut.Control or MenuVisuals.Cut.Panel,
                        $"{control.Name} en {screen} usa una esquina de {flat.CornerRadiusTopLeft}.");
                    Expect(!(flat.DrawCenter && Purple(flat.BgColor)) && !(flat.BorderWidthTop > 0 && Purple(flat.BorderColor)),
                        $"{control.Name} en {screen} vuelve al morado.");
                }
                else if (box is StyleBoxTexture { Texture: { } texture } && texture.GetImage() is { } image && !image.IsEmpty())
                    Expect(!Purple(image.GetPixel(image.GetWidth() / 2, image.GetHeight() / 2)), $"{control.Name} en {screen} vuelve al morado.");

                if (control is not Button action || !RelickVisuals.IsPrimary(action)) continue;
                primaries++;
                foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
                foreach (var plate in new[] { RelickVisuals.State.Own, RelickVisuals.State.Danger })
                    Expect(Contrast(action.GetThemeColor(state), plate) >= 4.5, $"«{action.Text}» en {screen} no se lee con {state}.");
            }
            Expect(primaries <= 1, $"{screen} tiene {primaries} acciones principales.");
        }

        async Task Dialog(AcceptDialog? dialog, string screen, float widest)
        {
            await Frames();
            Expect(dialog is { Visible: true }, $"{screen} no se abre.");
            var position = (Vector2)dialog!.Position; var size = (Vector2)dialog.Size;
            Expect(position.X >= 0 && position.Y >= MenuDialog.TitleBar && position.X + size.X <= Size.X + 1 && position.Y + size.Y <= Size.Y + 1,
                $"{screen} sale de la pantalla {Size}: {position}, {size}.");
            Expect(size.X <= widest, $"{screen} se estira a {size.X} de ancho.");
            Expect(dialog.GetOkButton().Size.Y >= MenuVisuals.TouchTarget, $"los botones de {screen} son bajos para tocarlos.");
            Inspect(dialog, screen);
            dialog.Hide(); await Frames();
        }

        static void Press(Node root, string name) =>
            root.FindChildren(name, "Button", true, false).OfType<Button>().First().EmitSignal(BaseButton.SignalName.Pressed);

        var training = CardCatalog.TrainingDeck();
        _content.Show();
        try
        {
            foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(1000, 600), new(844, 390), new(700, 390) })
            {
                GetWindow().Size = size; GetWindow().ContentScaleSize = size;
                _localError = _lobbyNotice = "";
                PresentLobbyState(null, true, false, RoomRequest.None, "", ""); await Frames();
                Inspect(_content, "la sala");
                Expect(RelickVisuals.IsPrimary(_quickMatch) && !RelickVisuals.IsPrimary(_create) && !RelickVisuals.IsPrimary(_openJoin),
                    "Buscar rival no es la única acción principal de la sala.");
                PresentLobbyState(null, true, false, RoomRequest.None, "", "", searching: true); await Frames();
                Expect(!RelickVisuals.IsPrimary(_quickMatch), "Cancelar búsqueda parece la acción principal.");
                PresentLobbyState(new RoomInfo { Code = "AB12CD34", PlayerOne = "Ana", PlayerTwo = "" }, true, false, RoomRequest.None, "", ""); await Frames();
                Inspect(_content, "la sala privada");
                PresentLobbyState(null, true, false, RoomRequest.None, "", ""); await Frames();
                Expect(RelickVisuals.IsPrimary(_quickMatch), "Buscar rival no recupera su acento.");

                foreach (var page in new[] { MenuPage.Profile, MenuPage.Settings })
                {
                    ShowPage(page); await Frames();
                    Inspect(_content, page.ToString());
                    var bounds = _shell.Body.GetGlobalRect();
                    Expect(bounds.End.X <= Size.X + 1 && bounds.End.Y <= Size.Y + 1, $"{page} desborda {size}.");
                }
                foreach (var (page, button) in _shell.Navigation.Buttons)
                    Expect(GetViewportRect().Grow(1).Encloses(button.GetGlobalRect()) && button.Size.X >= MenuVisuals.TouchTarget && button.Size.Y >= MenuVisuals.TouchTarget,
                        $"la pestaña {page} no cabe o es pequeña en {size}.");
                ShowPage(MenuPage.Play); await Frames();
                OpenJoinDialog();
                await Dialog(_joinDialog, "Tengo un código", 480);
                _content.Hide(); _loginScreen.Show(); await Frames();
                Inspect(_loginScreen, "el inicio de sesión");
                _loginScreen.Hide(); _content.Show();

                PresentConnectionNotice(false, false); await Frames();
                Inspect(_connectionNotice, "el aviso de conexión");
                PresentConnectionNotice(true, false); await Frames();

                var profile = new ProfileEditor { Current = ProfileView.Default("Ana") };
                AddChild(profile); MenuDialog.Show(profile);
                await Dialog(profile, "Mi perfil", 620);
                ShowDeleteAccount(20);
                await Dialog(GetNodeOrNull<AcceptDialog>("DeleteAccountDialog"), "Eliminar cuenta", 600);

                _content.Hide();
                foreach (var touch in new[] { false, true })
                {
                    var screen = new DeckScreen { Connection = new(), Touch = touch, PreviewDecks = [
                        new() { Id = 1, Name = "Mazo en uso", Relikario = "yuno", Cards = training, Traps = [], Side = [], Valid = true, Active = true },
                        new() { Id = 2, Name = "Otro mazo", Relikario = "yuno", Cards = training.Take(30).ToList(), Traps = [], Side = [], Valid = false, Active = false }] };
                    AddChild(screen); await Frames();
                    try
                    {
                        Inspect(screen, "Mis mazos");
                        Press(screen.FindChild("Deck1", true, false)!, "Delete");
                        await Dialog(screen.GetChildren().OfType<AcceptDialog>().LastOrDefault(), "Borrar mazo", 480);

                        screen.ChooseRelikario(); await Frames();
                        Inspect(screen, "el selector");
                        Expect(screen.FindChild("PickerBack", true, false) is Button { Text: "‹ Volver" }, "el selector no usa el botón de volver común.");

                        screen.Edit(2, "Otro mazo", training.Take(30).ToList(), [], relikario: "yuno"); await Frames();
                        var editor = screen.Editor!;
                        Inspect(editor, "el editor");
                        Expect(editor.FindChild("EditorBack", true, false) is Button { Text: "‹ Volver" }, "el editor no usa el botón de volver común.");
                        Expect(editor.GetGlobalRect().End.X <= Size.X + 1 && editor.GetGlobalRect().End.Y <= Size.Y + 1,
                            $"el editor desborda {size}, táctil {touch}.");
                        void CheckTargets()
                        {
                            if (!touch) return;
                            foreach (var target in editor.FindChildren("*", "Control", true, false).OfType<Control>().Where(c => c is BaseButton or LineEdit && c.IsVisibleInTree()))
                                Expect(target.Size.X >= MenuVisuals.TouchTarget && target.Size.Y >= MenuVisuals.TouchTarget,
                                    $"{target.Name} mide {target.Size} en táctil a {size}.");
                        }
                        CheckTargets();
                        foreach (var tab in ((Control)editor.FindChild("ListTabs", true, false)!).GetChildren().OfType<Button>())
                            Expect(tab.GetThemeFont("font").GetStringSize(tab.Text, fontSize: tab.GetThemeFontSize("font_size")).X <= tab.Size.X - 6,
                                $"«{tab.Text}» se corta a {size}, táctil {touch}.");
                        Press(editor, "DeckProblems");
                        await Dialog(editor.GetNodeOrNull<AcceptDialog>("DeckRequirements"), "Requisitos", 560);
                        Press(editor, "InspectArtwork");
                        await Dialog(editor.GetNodeOrNull<AcceptDialog>("DeckCardZoom"), "la carta ampliada", Size.X);
                        Press(editor, "StyleTab"); await Frames();
                        Inspect(editor, "Estilo");
                        CheckTargets();
                        editor.ChooseStyle(sleeve: "oro");
                        Press(editor, "EditorBack");
                        await Dialog(editor.GetChildren().OfType<AcceptDialog>().LastOrDefault(), "Cambios sin guardar", 480);
                    }
                    finally { RemoveChild(screen); screen.QueueFree(); await Frames(); }
                }
                _content.Show();
            }
        }
        finally { _content.Hide(); }
        GD.Print("PVP_MENU_STYLE_PASS · sin morado, escalas de letra y esquinas, una acción principal, foco legible, diálogos ajustados y editor táctil; cinco tamaños.");
    }
}
