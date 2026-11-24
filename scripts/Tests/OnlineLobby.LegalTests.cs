using Godot;

namespace Relick;

// The unofficial-project notice: one line on the first screens, the full text and the licences in Ajustes.
public partial class OnlineLobby
{
    private async Task CheckLegalNotice()
    {
        async Task Frames() { for (var i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        void Expect(bool ok, string reason) { if (!ok) throw new Exception("Aviso legal: " + reason); }

        Expect(FindChild("PlayLegalNotice", true, false) is Label { Text: var play } && play.Contains("no oficial") && play.Contains("Ajustes"),
            "la página Jugar debe decir que es un simulador no oficial y dónde está el aviso completo");
        Expect(FindChild("LoginLegalNotice", true, false) is Label { Text: LegalNotice.Short }, "la pantalla de inicio debe mostrar el aviso breve");

        var about = FindChild("AboutSection", true, false) as Control;
        Expect(about != null, "falta la sección Acerca de en Ajustes");
        var texts = about!.FindChildren("*", "Label", true, false).OfType<Label>().Select(label => label.Text).ToList();
        Expect(LegalNotice.Disclaimer.All(texts.Contains) && texts.Contains(LegalNotice.License) && texts.Contains(LegalNotice.Credits),
            "Acerca de debe mostrar el aviso completo, la licencia y los créditos");
        Expect(LegalNotice.License.Contains("PolyForm Noncommercial 1.0.0") && !LegalNotice.License.Contains("código abierto"),
            "la licencia debe nombrarse sin presentarla como código abierto");

        ShowPage(MenuPage.Settings); await Frames();
        ((Button)about.FindChild("ThirdPartyLicenses", true, false)).EmitSignal(BaseButton.SignalName.Pressed);
        await Frames();
        var dialog = GetNodeOrNull<AcceptDialog>("ThirdPartyLicensesDialog");
        Expect(dialog is { Visible: true }, "el botón debe abrir las licencias de terceros");
        var licences = dialog!.GetNode<ScrollContainer>("MenuDialogBody").GetChild<TextEdit>(0).Text;
        Expect(licences.Contains("Godot Engine") && licences.Contains("Permission is hereby granted"), "las licencias deben incluir el texto MIT de Godot");
        dialog.Hide(); await Frames();
        ShowPage(MenuPage.Play); await Frames();
        GD.Print("PVP_LEGAL_NOTICE_PASS");
    }
}
