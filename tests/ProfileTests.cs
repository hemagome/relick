using Relick.Core;

static class ProfileTests
{
    public static (string, Action)[] Cases => new (string, Action)[]
    {
        ("Perfil: cada catálogo tiene ids únicos y su predeterminado va primero", () =>
        {
            Check(ProfileCatalog.Titles.Select(t => t.Id).Distinct().Count() == ProfileCatalog.Titles.Count && ProfileCatalog.Titles[0].Id == ProfileCatalog.DefaultTitle);
            Check(ProfileCatalog.Frames.Select(f => f.Id).Distinct().Count() == ProfileCatalog.Frames.Count && ProfileCatalog.Frames[0].Id == ProfileCatalog.DefaultFrame);
            Check(ProfileCatalog.Playmats.Select(m => m.Id).Distinct().Count() == ProfileCatalog.Playmats.Count && ProfileCatalog.Playmats[0].Id == ProfileCatalog.DefaultPlaymat);
            Check(ProfileCatalog.Avatars.SequenceEqual(RelikarioCatalog.Playable.Select(r => r.Id)) && ProfileCatalog.Avatars.Contains(ProfileCatalog.DefaultAvatar));
            // Colours travel as six hex digits, the same contract as sleeves and dice.
            static bool Hex(string? value) => value == null || value.Length == 6 && value.All(Uri.IsHexDigit);
            Check(ProfileCatalog.Frames.All(f => Hex(f.Edge) && Hex(f.Glow)));
            Check(ProfileCatalog.Playmats.All(m => new[] { m.Background, m.Surface, m.Raised, m.Frame, m.Trace }.All(Hex)));
        }),
        ("Perfil: lo desconocido o ausente vuelve al predeterminado y las evoluciones no son retratos", () =>
        {
            Check(ProfileCatalog.Avatar("osamu") == "osamu" && ProfileCatalog.Avatar("osamu-lv2") == ProfileCatalog.DefaultAvatar && ProfileCatalog.Avatar(null) == ProfileCatalog.DefaultAvatar);
            Check(!ProfileCatalog.IsAvatar("yuno-lv2") && ProfileCatalog.IsAvatar("ironwing"));
            Check(ProfileCatalog.Title("no-existe").Id == ProfileCatalog.DefaultTitle && ProfileCatalog.Title("estratega").Name == "Estratega");
            Check(ProfileCatalog.Frame(null).Id == ProfileCatalog.DefaultFrame && ProfileCatalog.Playmat("aurora").Id == "aurora");
            Check(!ProfileCatalog.IsTitle(null) && !ProfileCatalog.IsFrame("") && !ProfileCatalog.IsPlaymat("x"));
        }),
        ("Perfil: las estadísticas cuentan por forma base, con favorito y porcentaje estables", () =>
        {
            Check(ProfileCatalog.StatsKey("yuno-lv2") == "yuno" && ProfileCatalog.StatsKey("ironwing") == "ironwing" && ProfileCatalog.StatsKey("desconocido") == "yuno");
            Check(ProfileCatalog.Favorite([]) == null && ProfileCatalog.Favorite([new("osamu", 0, 0, 0)]) == null);
            Check(ProfileCatalog.Favorite([new("yuno", 1, 1, 0), new("osamu", 3, 0, 0)]) == "osamu");
            // Same duels played: more wins first, then catalogue order.
            Check(ProfileCatalog.Favorite([new("ironwing", 2, 1, 0), new("osamu", 1, 2, 0)]) == "ironwing");
            Check(ProfileCatalog.Favorite([new("ironwing", 1, 1, 0), new("yuno", 1, 1, 0)]) == "yuno");
            Check(ProfileCatalog.WinRate(0, 0) == null && ProfileCatalog.WinRate(1, 3) == 33 && ProfileCatalog.WinRate(1, 2) == 50 && ProfileCatalog.WinRate(5, 8) == 63);
            Check(new RelikarioRecord("yuno", 2, 3, 1).Played == 6);
        })
    };

    private static void Check(bool condition) { if (!condition) throw new Exception("Comprobación de perfil fallida"); }
}
