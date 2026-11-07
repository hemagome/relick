using Relick.Core;
static class LoadErrorTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Error de Carga: Huevo anulado devuelve acción y 200 PR; doble Error conserva Huevo", () =>
        {
            foreach (var counter in new[] { false, true })
            {
                var g = Fixture(); var a = g.State.Players[0]; var b = g.State.Players[1];
                a.Hand.AddRange(new[] { "blue-egg", "load-error" }); b.Hand.Add("load-error"); a.Relikia = 200; b.Equipment = "lucky-pendant";
                Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.ErrorResponse && a.ActionsLeft == 1);
                Check(g.PlayCode(1, 0).Success && g.State.DecisionPlayer == 0);
                if (counter) Check(g.PlayCode(0, 0).Success); else Check(g.PassErrorResponse(0).Success);
                if (counter)
                {
                    Check(a.ActionsLeft == 0 && b.ActionsLeft == 2 && a.Relikia == 0 && g.State.Phase == Phase.DestroyChoice);
                    Check(g.ResolveDestruction(0, 1, true).Success && b.Equipment == null);
                }
                else Check(a.ActionsLeft == 2 && b.ActionsLeft == 1 && a.Relikia == 200 && b.Equipment == "lucky-pendant" && g.State.Phase == Phase.Actions);
                Check(a.Memory.Contains("blue-egg") && b.Memory.Contains("load-error"));
            }
        }),
        ("Error de Carga: tres respuestas resuelven en orden inverso y respetan ambos cupos", () =>
        {
            var g = Fixture(); var a = g.State.Players[0]; var b = g.State.Players[1];
            a.Hand.AddRange(new[] { "blue-egg", "load-error" }); b.Hand.AddRange(new[] { "load-error", "load-error" }); a.Relikia = 200; b.Equipment = "lucky-pendant";
            Check(g.PlayCode(0, 0).Success && g.PlayCode(1, 0).Success && g.PlayCode(0, 0).Success && g.PlayCode(1, 0).Success);
            Check(a.ActionsLeft == 2 && b.ActionsLeft == 0 && a.Relikia == 200 && b.Equipment != null && g.State.ActivationChain.Count == 0);
        }),
        ("Error cancela equipo nuevo conservando el anterior; un contraerror equipa y destruye el viejo", () =>
        {
            foreach (var counter in new[] { false, true })
            {
                var g = Fixture(); var a = g.State.Players[0]; var b = g.State.Players[1]; a.Equipment = "lucky-pendant";
                a.Hand.AddRange(new[] { "cardboard-box", "load-error" }); b.Hand.Add("load-error");
                Check(g.Equip(0, 0).Success && a.Equipment == "lucky-pendant" && g.PlayCode(1, 0).Success);
                if (counter) Check(g.PlayCode(0, 0).Success); else Check(g.PassErrorResponse(0).Success);
                Check(counter ? a.Equipment == "cardboard-box" && a.Memory.Contains("lucky-pendant") && a.ActionsLeft == 0 && b.ActionsLeft == 2
                    : a.Equipment == "lucky-pendant" && a.Memory.Contains("cardboard-box") && a.ActionsLeft == 2 && b.ActionsLeft == 1);
            }
        }),
        ("Error: sin acciones, jugador incorrecto o fuera de una activación no cambia nada", () =>
        {
            var g = Fixture(); var a = g.State.Players[0]; var b = g.State.Players[1]; a.Hand.Add("senshuni"); b.Hand.Add("load-error");
            var before = Snapshot(g); Check(!g.PlayCode(1, 0).Success && before == Snapshot(g));
            Check(g.PlayCode(0, 0).Success); b.ActionsLeft = 0; before = Snapshot(g);
            Check(!g.PlayCode(1, 0).Success && !g.PassErrorResponse(0).Success && !g.Advance(0).Success && before == Snapshot(g));
            Check(g.PassErrorResponse(1).Success && a.Hand.Count == 2);
        }),
        ("Error al resolver un dado anula curación y restaura código de memoria y costes de Relikia", () =>
        {
            var g = Fixture(); var a = g.State.Players[0]; var b = g.State.Players[1];
            a.Relikario = RelikarioCatalog.YunoLv2; a.Health = 1; a.Relikia = 600; a.Memory.Add("extra-life"); b.Hand.Add("load-error");
            Check(g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success && g.PassErrorResponse(1).Success && g.State.Phase == Phase.DieRerollChoice);
            Check(g.PlayCode(1, 0).Success && a.Health == 1 && a.Relikia == 600 && a.ActionsLeft == 2 && a.CorruptedMemory.Count == 0 && a.Memory.Single() == "extra-life" && g.State.PendingDieRoll == null);
        }),
        ("Error puede negar Re-lanza sin relanzar ni robar y sin cobrar acción a la carta gratuita", () =>
        {
            var g = Fixture(); var a = g.State.Players[0]; var b = g.State.Players[1]; g.State.Phase = Phase.Combat;
            a.Hand.AddRange(new[] { "attack", "reroll" }); a.Skills.Add("destiny-shuffle"); b.Hand.Add("load-error");
            Check(g.Attack(0, 0, 0).Success && g.PlayCode(0, 0).Success && g.PlayCode(1, 0).Success);
            Check(a.ActionsLeft == 2 && b.ActionsLeft == 1 && a.Hand.Count == 0 && g.State.PendingAttackRoll?.Die == 2);
            Check(g.ResolveAttackReroll(0, false).Success && g.State.Players[1].Health == 8);
        })
    };
    private static GameEngine Fixture()
    {
        var g = TestGame.Create(new ErrorRandom()); g.State.CurrentPlayer = 0; g.State.Phase = Phase.Actions;
        foreach (var p in g.State.Players) { p.Hand.Clear(); p.Memory.Clear(); p.Skills.Clear(); }
        return g;
    }
    private sealed class ErrorRandom : IGameRandom { public int Next(int max) => max == 6 ? 1 : max - 1; }
    private static string Snapshot(GameEngine g) => System.Text.Json.JsonSerializer.Serialize(g.State);
    private static void Check(bool value) { if (!value) throw new Exception("Regla de Error de Carga no satisfecha"); }
}
