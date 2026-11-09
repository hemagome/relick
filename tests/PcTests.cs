using Relick.Core;
static class PcTests
{
    public static (string, Action)[] Cases => new (string, Action)[]
    {
        ("PC: guarda D6 al equipar y modifica ataque sin relanzar, seis valores", () =>
        {
            foreach (var value in Enumerable.Range(1, 6))
            {
                var (g, rng) = Fixture(); var p = g.State.Active;
                rng.EnqueueDie(value); Check(g.Equip(0, 0).Success && p.EquipmentDie == value && p.ActionsLeft == 1);
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
                Check(DuelSnapshot.For(g, -1, 1).Players[0].EquipmentDie == value);
                p.HasNaturalSix = false; g.State.Phase = Phase.Combat; p.Hand.Add("attack"); p.Skills.Add("destiny-shuffle");
                rng.EnqueueDie(1); Check(g.Attack(0, 0, 0).Success && g.State.Phase == Phase.AttackRerollChoice);
                Check(!g.UsePc(1).Success && g.UsePc(0).Success);
                Check(g.State.LastAttackDie == (value <= 3 ? 1 + value : value));
                Check(p.Equipment == null && p.EquipmentDie == 0 && p.Memory.Contains(CardCatalog.Pc.Id) && p.ActionsLeft == 1 && !p.HasNaturalSix);
            }
        }),
        ("PC: suma limitada a 5 y modifica dados de efecto en Yuno Lv1", () =>
        {
            foreach (var value in new[] { 2, 6 })
            {
                var (g, rng) = Fixture(); var p = g.State.Active;
                rng.EnqueueDie(value); Check(g.Equip(0, 0).Success);
                p.HasNaturalSix = false; p.Health = 1; p.Hand.Add("extra-life"); rng.EnqueueDie(4);
                Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.DieRerollChoice);
                Check(g.UsePc(0).Success && p.Health == (value == 2 ? 4 : 5) && p.ActionsLeft == 0);
                Check(!g.UsePc(0).Success && !p.HasNaturalSix);
            }
        }),
        ("PC: Error cancela antes de tirar; reemplazar equipo limpia valor guardado", () =>
        {
            var (g, rng) = Fixture(); g.State.Players[1].Hand.Add("load-error");
            var rolls = 0; g.DieRolled += _ => rolls++;
            Check(g.Equip(0, 0).Success && g.PlayCode(1, 0).Success);
            Check(rolls == 0 && g.State.Active.ActionsLeft == 2 && g.State.Active.Equipment == null && g.State.Active.EquipmentDie == 0);
            var p = g.State.Active; p.Hand.Add(CardCatalog.Pc.Id); rng.EnqueueDie(5); Check(g.Equip(0, 0).Success);
            p.Hand.Add("lucky-pendant"); Check(g.Equip(0, 0).Success && p.EquipmentDie == 0 && p.Equipment == "lucky-pendant");
        })
    };
    private static (GameEngine, ScriptedRandom) Fixture()
    {
        var rng = new ScriptedRandom(); var g = TestGame.Create(rng); g.State.CurrentPlayer = 0; g.State.Phase = Phase.Actions; g.State.Turn = 2;
        foreach (var p in g.State.Players) { p.Hand.Clear(); p.Skills.Clear(); p.Equipment = null; p.Deck.Clear(); p.Deck.AddRange(Enumerable.Repeat("attack", 8)); }
        g.State.Active.Hand.Add(CardCatalog.Pc.Id); return (g, rng);
    }
    private static void Check(bool value) { if (!value) throw new Exception("Regla de PC 8BITS no satisfecha"); }
}
