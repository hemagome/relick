using Relick.Core;
static class DodgeTests
{
    public static (string, Action)[] Cases => new (string, Action)[]
    {
        ("Esquivar: seis resultados, coste del defensor y negación completa del crítico Magnet", () =>
        {
            foreach (var die in Enumerable.Range(1, 6))
            {
                var (g, rng) = Fixture(); var p = g.State.Players[1];
                rng.EnqueueDie(die); Check(g.PlayCode(1, 0).Success && p.ActionsLeft == 1 && g.State.Players[0].ActionsLeft == 2);
                Check(p.Memory.Contains(CardCatalog.Dodge.Id) && p.Health == p.MaxHealth);
                if (die <= 3)
                {
                    Check(p.Hand.Count == 1 && g.State.Phase == Phase.DefenseChoice);
                    Check(g.PassDefense(1).Success && p.Health == p.MaxHealth - 5 && g.State.Phase == Phase.MagnetChoice);
                }
                else Check(p.Hand.Count == 0 && g.State.Phase == Phase.Combat && g.State.PendingAttack == null && g.State.Players[0].Relikia == 0);
            }
        }),
        ("Esquivar: rechazos sin ataque, atacante y sin acciones; Error devuelve coste sin tirar", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Players[1];
            p.ActionsLeft = 0; var before = g.SaveSnapshot(); Check(!g.PlayCode(1, 0).Success && before == g.SaveSnapshot());
            p.ActionsLeft = 2; g.State.Players[0].Hand.Add("load-error");
            var rolls = 0; g.DieRolled += _ => rolls++;
            Check(g.PlayCode(1, 0).Success && g.State.Phase == Phase.ErrorResponse);
            Check(g.PlayCode(0, 0).Success && p.ActionsLeft == 2 && rolls == 0 && g.State.Phase == Phase.DefenseChoice);
            Check(g.PassDefense(1).Success && p.Health == p.MaxHealth - 5);
            g.State.Phase = Phase.Actions; p.Hand.Add(CardCatalog.Dodge.Id); before = g.SaveSnapshot();
            Check(!g.PlayCode(1, 0).Success && before == g.SaveSnapshot());
        }),
        ("Esquivar: Agilidad modifica dado propio, persiste decisión y cuesta la segunda acción", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Players[1]; p.Hand.Add(CardCatalog.Agility.Id);
            rng.EnqueueDie(1); Check(g.PlayCode(1, 0).Success && g.State.Phase == Phase.DieRerollChoice);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            Check(g.PlayCode(1, 0).Success && g.State.Phase == Phase.Combat && g.State.PendingAttack == null);
            Check(g.State.Players[1].ActionsLeft == 0 && g.State.Players[1].Health == 8);
        })
    };
    private static (GameEngine, ScriptedRandom) Fixture()
    {
        var rng = new ScriptedRandom(); var g = TestGame.Create(rng); g.State.CurrentPlayer = 0; g.State.Phase = Phase.Combat;
        foreach (var p in g.State.Players) { p.Hand.Clear(); p.Skills.Clear(); p.Equipment = null; p.Deck.Clear(); p.Deck.AddRange(Enumerable.Repeat("attack", 8)); }
        g.State.Active.Hand.Add("attack"); g.State.Active.Skills.Add("magnet-shuffle"); g.State.Active.Relikia = 0;
        g.State.Players[1].Hand.Add(CardCatalog.Dodge.Id);
        rng.EnqueueDie(6); Check(g.Attack(0, 0, 0).Success && g.ResolveAttackReroll(0, false).Success && g.State.Phase == Phase.DefenseChoice);
        return (g, rng);
    }
    private static void Check(bool value) { if (!value) throw new Exception("Regla de Esquivar no satisfecha"); }
}
