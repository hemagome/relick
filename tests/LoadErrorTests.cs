using Relick.Core;
static class LoadErrorTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Error de Carga: Huevo anulado devuelve la acción; doble Error conserva el robo opcional", () =>
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
                    Check(a.ActionsLeft == 0 && b.ActionsLeft == 2 && a.Relikia == 200 && g.State.Phase == Phase.Decision);
                    Check(g.ResolveDestruction(0, 1, true).Success && b.Equipment == null && (g.State.Phase == Phase.Decision && g.State.PendingDecision!.Kind == DecisionKind.Confirm));
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
        ("Error no responde a un Código rejugado con Relikia: es un efecto de relikia y conserva su coste, tirada y corrupción", () =>
        {
            var g = Fixture(); var a = g.State.Players[0]; var b = g.State.Players[1]; var dice = 0;
            g.DieRolled += _ => dice++;
            a.Relikario = RelikarioCatalog.YunoLv2; a.Health = 1; a.Relikia = 600; a.Memory.Add("extra-life"); b.Hand.Add("load-error");
            Check(g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success && g.State.Phase == Phase.DieRerollChoice && dice == 1);
            Check(a.Relikia == 200 && a.ActionsLeft == 0 && a.CorruptedMemory.Single() == "extra-life");
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), new ErrorRandom()); a = g.State.Players[0];
            var before = g.SaveSnapshot();
            Check(!g.CanPlayLoadError(1) && !g.PlayCode(1, 0).Success && before == g.SaveSnapshot());
            Check(!DuelOptions.For(g, 1).Any(o => o.Move.Kind == DuelMoveKind.PlayCode));
            Check(g.ResolveDieReroll(0, false).Success && a.Health == 3 && g.State.Players[1].Hand.Single() == "load-error");
        }),
        ("Error tras el dado de Vida Extra: anula sin curar, pasar cura con el dado final y un contraerror deja continuar", () =>
        {
            foreach (var answer in new[] { "cancel", "pass", "counter" })
            {
                var g = Fixture(); var a = g.State.Players[0]; var b = g.State.Players[1];
                a.Health = 3; a.Dexterity = 0; a.Hand.Add("extra-life"); b.Hand.Add("load-error");
                if (answer == "counter") a.Hand.Add("load-error");
                Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.ErrorResponse && !g.State.ErrorAfterDie);
                Check(g.PassErrorResponse(1).Success && g.State.Phase == Phase.ErrorResponse && g.State.ErrorAfterDie && g.State.PendingDieRoll?.Die == 2);
                Check(a.Health == 3 && DuelOptions.For(g, 1).Any(o => o.Move.Kind == DuelMoveKind.PassError));
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), new ErrorRandom()); a = g.State.Players[0];
                if (answer == "pass") Check(g.PassErrorResponse(1).Success && a.Health == 5 && g.State.Phase == Phase.Actions);
                else
                {
                    Check(g.PlayCode(1, 0).Success);
                    if (answer == "counter") Check(g.PlayCode(0, a.Hand.IndexOf("load-error")).Success);
                    Check(g.State.Phase == Phase.Actions && !g.State.ErrorAfterDie && g.State.PendingDieRoll == null);
                    Check(answer == "counter" ? a.Health == 5 && a.ActionsLeft == 0 : a.Health == 3 && a.ActionsLeft == 2);
                }
                Check(a.Memory.Contains("extra-life") && a.CorruptedMemory.Count == 0);
            }
        }),
        ("Error tras el dado: no se abre sin Error en la mano rival ni para el dado de un efecto ajeno al Código", () =>
        {
            var g = Fixture(); var a = g.State.Players[0]; a.Health = 3; a.Dexterity = 0; a.Hand.Add("extra-life");
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.Actions && a.Health == 5 && !g.State.ErrorAfterDie);
        }),
        ("Error: las cartas necesarias para descartes pendientes no pueden gastarse en un contraerror", () =>
        {
            foreach (var id in new[] { CardCatalog.PlumberStar.Id, CardCatalog.TimeBender.Id })
            {
                var g = Fixture(); var a = g.State.Players[0]; var b = g.State.Players[1];
                a.Hand.Add(id); a.Hand.Add("load-error");
                if (CardCatalog.Get(id).DiscardCost == 2) a.Hand.Add("attack");
                b.Hand.Add("load-error");
                Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.ErrorResponse);
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), new ErrorRandom()); a = g.State.Players[0];
                Check(g.PlayCode(1, 0).Success && g.State.Phase == Phase.Actions);
                Check(a.Hand.Contains("load-error") && a.Memory.Single() == id && a.ActionsLeft == 2);
                Check(g.State.PendingTurnBuff == null && a.StarEffects == 0 && g.State.ExtraActionStages == 0);
            }
        }),
        ("Error tras el dado de Re-lanza: anularla conserva el dado relanzado y omite el robo; pasar o un contraerror roban", () =>
        {
            foreach (var answer in new[] { "cancel", "pass", "counter" })
            {
                var g = Fixture(); var a = g.State.Players[0]; var b = g.State.Players[1]; g.State.Phase = Phase.Combat;
                a.Hand.AddRange(new[] { "attack", "reroll" }); a.Skills.Add("destiny-shuffle"); a.Dexterity = 0; b.Hand.Add("load-error");
                if (answer == "counter") a.Hand.Add("load-error");
                Check(g.Attack(0, 0, 0).Success && g.State.Phase == Phase.AttackRerollChoice);
                Check(g.PlayCode(0, a.Hand.IndexOf("reroll")).Success && g.State.Phase == Phase.ErrorResponse && !g.State.ErrorAfterDie);
                var hand = a.Hand.Count;
                Check(g.PassErrorResponse(1).Success && g.State.Phase == Phase.ErrorResponse && g.State.ErrorAfterDie && g.State.PendingAfterError != null);
                Check(DuelSnapshot.For(g, 1, 1) is { ErrorAfterDie: true, Die: 2 } && a.Hand.Count == hand);
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), new ErrorRandom()); a = g.State.Players[0];
                if (answer == "pass") Check(g.PassErrorResponse(1).Success);
                else
                {
                    Check(g.PlayCode(1, 0).Success);
                    if (answer == "counter") Check(g.PlayCode(0, a.Hand.IndexOf("load-error")).Success);
                }
                // The reroll already happened in every case; only the draw depends on the answer.
                var drew = answer != "cancel";
                Check(a.Hand.Count == hand + (drew ? 1 : 0) - (answer == "counter" ? 1 : 0) && !g.State.ErrorAfterDie && g.State.PendingAfterError == null);
                Check(g.State.PendingAttackRoll is { Die: 2, WasRerolled: true } && g.ResolveAttackReroll(0, false).Success && g.State.Players[1].Health == 8);
            }
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
        var g = TestGame.Create(new ErrorRandom()); g.State.CurrentPlayer = 0; g.State.Phase = Phase.Actions; g.State.Turn = 2;
        foreach (var p in g.State.Players) { p.Hand.Clear(); p.Memory.Clear(); p.Skills.Clear(); }
        return g;
    }
    private sealed class ErrorRandom : IGameRandom { public int Next(int max) => max == 6 ? 1 : max - 1; }
    private static string Snapshot(GameEngine g) => System.Text.Json.JsonSerializer.Serialize(g.State);
    private static void Check(bool value) { if (!value) throw new Exception("Regla de Error de Carga no satisfecha"); }
}
