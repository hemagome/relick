using Relick.Core;

static class RelikarioCatalogTests
{
    public static (string, Action)[] Cases => new (string, Action)[]
    {
        ("Relikario: la evolución, el nombre corto y la familia salen del catálogo, no de Yuno", () =>
        {
            foreach (var form in RelikarioCatalog.All.Values)
            {
                Check(RelikarioCatalog.Find(form.Id) == form && RelikarioCatalog.All.ContainsKey(form.Family));
                if (RelikarioCatalog.EvolutionFor(form) is { } next)
                    Check(next.Family == form.Family && next.Level == form.Level + 1 && next.EvolutionCost > 0);
            }
            foreach (var playable in RelikarioCatalog.Playable)
                Check(playable.Family == playable.Id && RelikarioCatalog.Roster.Any(r => r.Id == playable.Id && r.Definition == playable));
            Check(RelikarioCatalog.EvolutionFor(RelikarioCatalog.Yuno) == RelikarioCatalog.YunoLv2 && RelikarioCatalog.EvolutionFor(RelikarioCatalog.YunoLv2) == null);
            Check(RelikarioCatalog.YunoLv2.DisplayName == "Yuno" && RelikarioCatalog.NameOf("yuno-lv2") == "Yuno Gyambura" &&
                RelikarioCatalog.NameOf("osamu") == "Osamu Takahashi" && RelikarioCatalog.EvolutionFor(RelikarioCatalog.Osamu) == RelikarioCatalog.OsamuLv2);
        }),
        ("Relikario: la opción de evolucionar describe la forma siguiente con su nivel y coste", () =>
        {
            var g = TestGame.Create(new ScriptedRandom()); g.State.CurrentPlayer = 0; g.State.Phase = Phase.Combat; g.State.Turn = 2;
            var p = g.State.Active; p.HasNaturalSix = true; p.Relikia = RelikarioCatalog.YunoLv2.EvolutionCost;
            var evolve = DuelOptions.For(g, 0).Single(o => o.Move.Kind == DuelMoveKind.Evolve);
            Check(evolve.Label == "Evolucionar a Yuno Lv 2 · 1800 PR");
            Check(g.Evolve(0).Success && !DuelOptions.For(g, 0).Any(o => o.Move.Kind == DuelMoveKind.Evolve));
        }),
        ("Relikario: un duelo guardado recupera la definición del catálogo y el corte de relanzamientos con su nombre antiguo", () =>
        {
            var rng = new ScriptedRandom(); var g = TestGame.Create(rng);
            g.State.PendingDieRoll = new PendingDieRoll(0, DiePurpose.ExtraLife, 1, Phase.Actions, Rerolls: 1, WasRerolled: true, Modified: true, RerollStopped: true);
            var json = g.SaveSnapshot();
            // Saved duels already use this key; the catalog-only data is never written.
            Check(json.Contains("\"YunoStopped\":true") && !json.Contains("EvolvesTo") && !json.Contains("ShortName"));
            var restored = GameEngine.FromSnapshot(json, rng);
            Check(restored.State.PendingDieRoll!.RerollStopped);
            foreach (var p in restored.State.Players)
                Check(p.Relikario == RelikarioCatalog.Yuno && RelikarioCatalog.EvolutionFor(p.Relikario) == RelikarioCatalog.YunoLv2);
        }),
    };

    private static void Check(bool value, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    { if (!value) throw new Exception($"Catálogo de Relikarios no satisfecho (línea {line})"); }
}
