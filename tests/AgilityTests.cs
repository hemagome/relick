using Relick.Core;

static class AgilityTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Agilidad: ataque fija 5 sin tirar ni robar y consume una acción; rechaza dado rival", () =>
        {
            foreach (var die in Enumerable.Range(1, 6))
            {
                var (g, rng) = Fixture(); var p = g.State.Active;
                p.Hand.Add("attack"); p.Skills.Add("destiny-shuffle");
                g.State.Players[1].Hand.Add(CardCatalog.Agility.Id);
                rng.EnqueueDie(die); Check(g.Attack(0, 1, 0).Success);
                Check(!g.PlayCode(1, 0).Success);
                var rolls = 0; g.DieRolled += _ => rolls++;
                Check(g.PlayCode(0, 0).Success && rolls == 0 && p.ActionsLeft == 1);
                Check(g.State.LastAttackDie == 5 && !p.AttackRerollUsedThisTurn && !p.HasNaturalSix);
                if (g.State.Phase == Phase.AttackRerollChoice)
                {
                    Check(g.State.PendingAttackRoll is { Die: 5, Modified: true, WasRerolled: false });
                    Check(g.ResolveAttackReroll(0, false).Success);
                }
                Check(g.State.Players[1].Health == g.State.Players[1].MaxHealth - 3);
                Check(p.Hand.Count == 0 && p.Memory.Contains(CardCatalog.Agility.Id));
                Check(g.Advance(0).Success && g.State.Phase == Phase.Actions && p.ActionsLeft == 1);
            }
        }),
        ("Agilidad: abre ventana de efecto en Yuno Lv1 y cambia curación a 3", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Health = 1;
            g.State.Phase = Phase.Actions; p.Hand.Add("extra-life"); rng.EnqueueDie(1);
            Check(g.PlayCode(0, 1).Success && g.State.Phase == Phase.DieRerollChoice);
            Check(g.PlayCode(0, 0).Success && p.Health == 4 && p.ActionsLeft == 0);
            Check(!p.AttackRerollUsedThisTurn && g.State.PendingDieRoll == null);
        }),
        ("Agilidad: sin acciones rechaza la activación sin consumir carta ni modificar dado", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active;
            p.Hand.Add("attack"); p.Skills.Add("destiny-shuffle");
            rng.EnqueueDie(2); Check(g.Attack(0, 1, 0).Success);
            p.ActionsLeft = 0;
            var before = System.Text.Json.JsonSerializer.Serialize(g.State);
            Check(g.CodeUnavailableReason(0, 0) != null && !g.PlayCode(0, 0).Success);
            Check(System.Text.Json.JsonSerializer.Serialize(g.State) == before);
        }),
        ("Agilidad: no se activa sin dado; Error de Carga conserva resultado original", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active;
            Check(!g.PlayCode(0, 0).Success && p.Hand.Count == 1 && p.ActionsLeft == 2);
            p.Hand.Add("attack"); p.Skills.Add("destiny-shuffle");
            g.State.Players[1].Hand.Add("load-error"); rng.EnqueueDie(2);
            Check(g.Attack(0, 1, 0).Success && g.PlayCode(0, 0).Success);
            Check(g.State.Phase == Phase.ErrorResponse && g.PlayCode(1, 0).Success);
            Check(g.State.PendingAttackRoll is { Die: 2, Modified: false } && p.ActionsLeft == 2);
            Check(CardCatalog.TrainingDeck().Count == 33 && CardCatalog.TrainingDeck().Contains(CardCatalog.Agility.Id));
        })
    };
    private static (GameEngine, ScriptedRandom) Fixture()
    {
        var rng = new ScriptedRandom(); var g = TestGame.Create(rng);
        g.State.CurrentPlayer = 0; g.State.Phase = Phase.Combat;
        foreach (var p in g.State.Players)
        {
            p.Hand.Clear(); p.Memory.Clear(); p.Skills.Clear(); p.Equipment = null;
            p.Deck.Clear(); p.Deck.AddRange(Enumerable.Repeat("attack", 8));
        }
        g.State.Active.Hand.Add(CardCatalog.Agility.Id); return (g, rng);
    }
    private static void Check(bool value) { if (!value) throw new Exception("Regla de Agilidad no satisfecha"); }
}
