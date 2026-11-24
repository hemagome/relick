using Godot;

namespace Relick;

/// <summary>
/// The unofficial-project notice, the software licence and the third-party credits shown in the menus.
/// README.md, NOTICE.md and THIRD_PARTY_NOTICES.md hold the same facts; change them together.
/// </summary>
internal static class LegalNotice
{
    /// <summary>One line for the first screens; the full notice is in Ajustes → Acerca de.</summary>
    public const string Short = "Simulador comunitario no oficial. Sin afiliación con Relick ni sus titulares de derechos.";

    public static readonly string[] Disclaimer =
    [
        "Este es un proyecto no oficial de aficionados, desarrollado por la comunidad e inspirado en el juego de cartas coleccionables Relick. " +
        "No está afiliado, respaldado ni patrocinado por Relick ni por sus respectivos titulares de derechos, y no cuenta con su soporte oficial.",
        "Todas las marcas, nombres de cartas, ilustraciones, personajes y demás propiedad intelectual de terceros siguen siendo propiedad de sus respectivos dueños.",
        "La licencia del software se aplica exclusivamente al software original cubierto por ella y no concede ningún derecho sobre la propiedad intelectual de terceros.",
        "Este proyecto tiene fines no comerciales. No se reclama ninguna titularidad ni asociación oficial con la franquicia Relick."
    ];

    public const string License =
        "El código original de este proyecto está disponible bajo la licencia PolyForm Noncommercial 1.0.0 (source available, no open source). " +
        "Los usos comerciales que esa licencia no permite requieren una autorización aparte del titular del código.";

    public const string Credits =
        "Motor: Godot Engine (licencia MIT). Red: SpacetimeDB, de Clockwork Laboratories (Business Source License 1.1). " +
        "Sonido de dado: «Wooden dice on wodden table roll», de Wuzzy (CC0). Barajado: «Playing Card Sounds», de Brian MacIntosh (BMacZero) (CC0). " +
        "La música y los demás efectos son composiciones originales generadas por código para este proyecto.";

    /// <summary>
    /// The licence texts that travel with the game: Godot's own, which its MIT licence requires in every copy, and those of
    /// the components bundled inside the engine, as the engine itself reports them.
    /// </summary>
    public static string ThirdPartyLicenses()
    {
        var text = new System.Text.StringBuilder();
        text.Append("Godot Engine\n\n").Append(Engine.GetLicenseText().StripEdges()).Append("\n\n");
        text.Append("Componentes incluidos en Godot Engine\n");
        foreach (var component in Engine.GetCopyrightInfo())
        {
            text.Append("\n· ").Append(component["name"].AsString()).Append('\n');
            foreach (var part in component["parts"].AsGodotArray<Godot.Collections.Dictionary>())
            {
                foreach (var holder in part["copyright"].AsStringArray()) text.Append("  © ").Append(holder).Append('\n');
                text.Append("  Licencia: ").Append(part["license"].AsString()).Append('\n');
            }
        }
        var licenses = Engine.GetLicenseInfo();
        foreach (var name in licenses.Keys.Select(key => key.AsString()).Order())
            text.Append("\n\n").Append(name).Append("\n\n").Append(licenses[name].AsString().StripEdges());
        text.Append("\n\nLa lista de los demás componentes y recursos de terceros está en THIRD_PARTY_NOTICES.md, junto al código fuente.");
        return text.ToString();
    }
}
