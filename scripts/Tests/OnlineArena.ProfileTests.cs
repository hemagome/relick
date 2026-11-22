using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    /// <summary>
    /// `--profile-only`: a player's name opens their profile card in the duel, for both seats and a compact
    /// layout, and the editor previews every choice before saving it.
    /// </summary>
    internal async Task CheckProfiles()
    {
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        async Task Layout() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        async Task Capture(string name)
        {
            var directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
            if (directory == null || DisplayServer.GetName() == "headless") return;
            System.IO.Directory.CreateDirectory(directory);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, name + ".png"));
        }
        static void Click(Control control) =>
            control.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left });

        var profiles = new[]
        {
            new ProfileView("Ana", "osamu", "estratega", "oro", "aurora", 7, 3, 1, "osamu"),
            new ProfileView("Beto", "ironwing", "temerario", "coral", "carmesi")
        };
        var game = GameEngine.FromOpening(new DuelOpening { Names = ["Ana", "Beto"], StartingSeat = 0, Hands = [[], []],
            Decks = [Enumerable.Repeat("attack", 20).ToList(), Enumerable.Repeat("attack", 20).ToList()], Bonuses = [null, null] }, new SeededRandom(5));
        game.State.Turn = 2; game.State.Phase = Phase.Actions;

        // Without a server that publishes profiles, a name is only a name.
        TestProfiles = null;
        PresentTestSnapshot(DuelSnapshot.For(game, 0, 900)); await Layout();
        Check(_root!.FindChild("PlayerName1", true, false) is Label { MouseFilter: Control.MouseFilterEnum.Ignore },
            "El nombre se ofrece como perfil aunque el servidor no tenga perfiles.");

        TestProfiles = seat => profiles[seat];
        foreach (var size in new[] { new Vector2I(1820, 1024), new(1024, 768) })
        foreach (var seat in new[] { 0, 1 })
        {
            GetWindow().Size = size; GetWindow().ContentScaleSize = size; await Layout();
            ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, seat, 901)); await Layout();
            var rivalName = _root!.FindChild("PlayerName1", true, false) as Label;
            Check(rivalName is { MouseFilter: Control.MouseFilterEnum.Stop } && rivalName.TooltipText.Contains("Ver perfil"),
                $"El nombre del rival no abre su perfil ({size}, asiento {seat}).");
            Click(rivalName!); await Layout();
            var popup = GetNodeOrNull<PopupPanel>("ProfilePopup");
            var card = popup?.FindChild("ProfileCard", true, false) as ProfileCard;
            var rival = profiles[1 - seat];
            Check(popup is { Visible: true } && card?.View == rival, $"No se muestra la tarjeta del rival ({size}, asiento {seat}).");
            Check(card!.FindChild("ProfileName", true, false) is Label { Text: var shown } && shown == rival.Name &&
                card.FindChild("ProfileTitle", true, false) is Label { Text: var title } && title == ProfileCatalog.Title(rival.Title).Name.ToUpperInvariant() &&
                card.FindChild("ProfileRecord", true, false) is Label { Text: var record } && record == rival.Record(),
                "La tarjeta no muestra nombre, título y resultados del rival.");
            Check(card.GetGlobalRect().Size.X <= size.X && GetViewportRect().Encloses(card.GetGlobalRect()), "La tarjeta de perfil no cabe en pantalla.");
            await Capture($"profile-rival-{size.X}x{size.Y}-seat{seat}");
            Click(rivalName!); await Layout();
            Check(GetChildren().Count(child => child.Name == "ProfilePopup") == 1, "Tocar otra vez el nombre abre una segunda tarjeta.");
            popup!.Hide(); await Layout();
            Check(GetNodeOrNull("ProfilePopup") == null, "Cerrar la tarjeta no la libera.");
        }
        Check(profiles[0].Record() == "7 V · 3 D · 1 E · 64 % de victorias" && profiles[1].Record().StartsWith("Sin duelos"),
            "El resumen de resultados no es legible.");

        // The editor: every choice changes the preview, and saving hands back exactly what was chosen.
        string[]? saved = null;
        var editor = new ProfileEditor { Current = profiles[0], Saved = (a, t, f, p) => saved = [a, t, f, p] };
        AddChild(editor); editor.PopupCentered(new Vector2I(560, 0)); await Layout();
        ProfileCard Preview() => (ProfileCard)editor.FindChild("ProfilePreviewCard", true, false)!;
        Check(Preview().View == profiles[0], "La vista previa no parte del perfil actual.");
        ((Button)editor.FindChild("ProfileAvatars", true, false)!.GetNode("yuno")).EmitSignal(BaseButton.SignalName.Pressed); await Layout();
        ((Button)editor.FindChild("ProfileFrames", true, false)!.GetNode("cian")).EmitSignal(BaseButton.SignalName.Pressed); await Layout();
        ((Button)editor.FindChild("ProfilePlaymats", true, false)!.GetNode("abismo")).EmitSignal(BaseButton.SignalName.Pressed); await Layout();
        var titles = (OptionButton)editor.FindChild("ProfileTitlePicker", true, false)!;
        foreach (var (id, name) in new[] { ("artesanal", "Artesanal"), ("p2w", "P2W"), ("f2p", "F2P") })
        {
            var index = Enumerable.Range(0, titles.ItemCount).Single(i => titles.GetItemText(i) == name);
            titles.Select(index); titles.EmitSignal(OptionButton.SignalName.ItemSelected, (long)index); await Layout();
            Check(Preview().View is { Avatar: "yuno", Frame: "cian", Playmat: "abismo", Name: "Ana", Wins: 7 } view && view.Title == id,
                $"La vista previa no muestra el título {name}.");
        }
        await Capture("profile-editor");
        editor.EmitSignal(AcceptDialog.SignalName.Confirmed); await Layout();
        Check(saved != null && saved.SequenceEqual(["yuno", "f2p", "cian", "abismo"]), "Guardar no entrega las elecciones del editor.");
        if (IsInstanceValid(editor)) editor.QueueFree();
        TestProfiles = null; await Layout();
        Check(ProfilePlaymats.Of("abismo").Background == new Color("03100f") && ReferenceEquals(ProfilePlaymats.Of("no-existe"), RelickPlaymat.Classic),
            "El tapete elegido no se traduce a los colores de la arena.");
    }
}
