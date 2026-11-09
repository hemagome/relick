using System.Text.Json;
using Relick.Core;

static class DexterityTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Destreza: cantidad inicial por vida, Yuno tiene una ficha y preparación la conserva", () =>
        {
            foreach (var (health, expected) in new[] { (6, 2), (7, 2), (8, 1), (9, 1), (10, 0), (12, 0) })
                Check(new PlayerState(new("fixture", "Fixture", health, "")).Dexterity == expected);
            var g = new GameEngine(8);
            Check(g.State.Players.All(p => p.Dexterity == 1));
            Check(!g.CanUseDexterity(0) && !g.UseDexterity(0).Success);
            Check(g.ChooseInitialSkill(0, CardCatalog.Destiny.Id).Success && g.ChooseInitialSkill(1, CardCatalog.Magnet.Id).Success && g.Start([]).Success);
            Check(g.State.Players.All(p => p.Dexterity == 1));
        }),
        ("Destreza: suma uno al ataque, gasta una ficha sin acciones, PR, relanzamientos ni azar", () =>
        {
            foreach (var seat in new[] { 0, 1 })
            foreach (var die in new[] { 2, 3, 4 })
            {
                var (g, rng) = Fixture(seat); var p = g.State.Active;
                p.ActionsLeft = 0; p.Relikia = 123;
                StartAttack(g, rng, die);
                Check(g.State.Phase == Phase.AttackRerollChoice && g.CanUseDexterity(seat));
                var cues = DuelVisualEvents.Capture(g); var rolls = 0; g.DieRolled += _ => rolls++;
                var option = DuelOptions.For(g, seat).Single(o => o.Move.Kind == DuelMoveKind.Dexterity);
                Check(option.Move.Apply(g, seat).Success && p.Dexterity == 0 && g.State.LastAttackDie == die + 1);
                Check(g.State.Players[1 - seat].Health == 8 - GameEngine.DamageFor(die + 1, 3, 5));
                Check(p.ActionsLeft == 0 && p.Relikia == 123 + (die >= 3 ? 600 : 0) && p.AttackRerollUsedThisTurn);
                Check(rolls == 0 && cues.Any(c => c.Kind == DuelVisualKind.DieAdjustment && c.Value == die + 1));
                Check(!cues.Any(c => c.Kind == DuelVisualKind.Die) && !p.HasNaturalSix);
            }
        }),
        ("Destreza: rechaza 1, 5, 6, rival, jugador inválido, ficha agotada y dado ya resuelto sin mutar", () =>
        {
            foreach (var die in new[] { 1, 5, 6 })
            {
                var (g, rng) = Fixture(); g.State.Active.Equipment = CardCatalog.Pendant.Id;
                StartAttack(g, rng, die); Reject(g, 0);
            }
            var (game, random) = Fixture(); StartAttack(game, random, 3);
            foreach (var actor in new[] { -1, 1, 2 }) Reject(game, actor);
            game.State.Active.Dexterity = 0; Reject(game, 0);
            game.State.Active.Dexterity = 1; Check(game.ResolveAttackReroll(0, false).Success); Reject(game, 0);
            game.Concede(0); Reject(game, 0);
        }),
        ("Destreza: fichas acumuladas permiten 3 a 4 a 5, sin alcanzar seis ni habilitar Colgante", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Dexterity = 2; p.Equipment = CardCatalog.Pendant.Id;
            StartAttack(g, rng, 3);
            Check(g.UseDexterity(0).Success && g.State.PendingAttackRoll!.Die == 4 && p.Dexterity == 1);
            Check(!g.State.PendingAttackRoll.WasRerolled && !g.CanUsePendant(0, PendantEffect.AddOne));
            Check(g.UseDexterity(0).Success && g.State.PendingAttackRoll!.Die == 5 && p.Dexterity == 0);
            p.Dexterity = 1; Reject(g, 0);
        }),
        ("Destreza: curación y PR usan el resultado modificado y el dado queda visible", () =>
        {
            foreach (var code in new[] { CardCatalog.ExtraLife.Id, CardCatalog.PointsIncrease.Id })
            {
                var (g, rng) = Fixture(); var p = g.State.Active; p.Health = 1;
                StartCode(g, rng, code, code == CardCatalog.ExtraLife.Id ? 2 : 4);
                Check(g.State.Phase == Phase.DieRerollChoice && g.UseDexterity(0).Success && p.Dexterity == 0);
                if (code == CardCatalog.ExtraLife.Id)
                    Check(p.Health == 4 && DuelSnapshot.For(g, 0, 1).Die == 3 && g.State.Phase == Phase.Actions);
                else Check(g.State.PendingPoints!.Die == 5 && g.State.PendingPoints.Amount == 1000);
            }
        }),
        ("Destreza: aumenta dado de daño Magnet y valor guardado en PC", () =>
        {
            var (g, rng) = Fixture();
            g.State.Active.Skills.Add(CardCatalog.Magnet.Id); g.State.Active.Hand.Add("attack");
            // The attack die is 5; the following 3 is the separate damage roll.
            rng.EnqueueDie(5); rng.EnqueueDie(3);
            Check(g.Attack(0, 0, 0).Success && g.State.PendingDieRoll?.Purpose == DiePurpose.MagnetDamage);
            Check(g.UseDexterity(0).Success && g.State.Players[1].Health == 4 && g.State.LastAttackDie == 5);
            (g, rng) = Fixture(); g.State.Phase = Phase.Actions; g.State.Active.Hand.Add(CardCatalog.Pc.Id); rng.EnqueueDie(4);
            Check(g.Equip(0, 0).Success && g.State.Phase == Phase.DieRerollChoice);
            Check(g.UseDexterity(0).Success && g.State.Active.EquipmentDie == 5 && g.State.Active.Equipment == CardCatalog.Pc.Id);
        }),
        ("Destreza: D6 de reciclaje aumenta la selección y persiste antes y después de gastarla", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active;
            g.State.Phase = Phase.Actions; p.Hand.Add(CardCatalog.Senshuni.Id);
            p.Deck.Clear(); p.Deck.Add("attack"); p.Memory.AddRange(Enumerable.Repeat("attack", 5)); rng.EnqueueDie(3);
            Check(g.PlayCode(0, 0).Success && g.State.PendingDieRoll?.Purpose == DiePurpose.Recycling);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            Check(g.CanUseDexterity(0) && g.UseDexterity(0).Success && g.RecyclingCount == 4);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
            Check(p.Dexterity == 0 && g.ResolveRecycling(0, [0, 1, 2, 3]).Success);
            Check(p.Hand.Count == 2 && p.Deck.Count == 3 && g.State.Phase == Phase.Actions);
        }),
        ("Destreza: Relikia alcanza premio con 4 a 5; Esquivar la usa en el turno rival", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active;
            g.State.Phase = Phase.Actions; p.Relikia = 500; p.Bonus = "attack"; rng.EnqueueDie(4);
            Check(g.UseRelikiaEffect(0, RelikiaEffect.ClaimBonus).Success && g.UseDexterity(0).Success);
            Check(g.State.PendingRelikia == RelikiaChoice.BonusReward && p.Relikia == 0);
            (g, rng) = Fixture(); var defender = g.State.Players[1]; defender.Hand.Add(CardCatalog.Dodge.Id);
            StartAttack(g, rng, 5); rng.EnqueueDie(3);
            Check(g.PlayCode(1, 0).Success && g.State.DecisionPlayer == 1 && g.CanUseDexterity(1));
            Reject(g, 0);
            Check(g.UseDexterity(1).Success && defender.Dexterity == 0 && g.State.Players[0].Dexterity == 1);
            Check(defender.Health == 8 && g.State.PendingAttack == null && g.State.Players[0].Relikia == 0);
        }),
        ("Destreza: conservar dado no consume ficha; evoluciones, turnos y recuperación no la regeneran", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active;
            StartAttack(g, rng, 3); Check(g.ResolveAttackReroll(0, false).Success && p.Dexterity == 1);
            g.State.AttackedThisTurn = false; StartAttack(g, rng, 3); Check(g.UseDexterity(0).Success);
            p.HasNaturalSix = true; p.Relikia = 1800; Check(g.Evolve(0).Success && p.Dexterity == 0);
            Check(g.Advance(0).Success && g.Advance(0).Success && p.Dexterity == 0);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            Check(g.State.Players[0].Dexterity == 0 && g.State.Players[1].Dexterity == 1);
            var view = JsonSerializer.Deserialize(JsonSerializer.Serialize(DuelSnapshot.For(g, 0, 1), DuelJson.Default.DuelSnapshot), DuelJson.Default.DuelSnapshot)!;
            Check(view.Players[0].Dexterity == 0 && view.Players[1].Dexterity == 1);
        }),
        ("Destreza: relanzar después de gastar no restaura la ficha ni convierte una modificación en seis natural", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.AttackRerollUsedThisTurn = false;
            StartAttack(g, rng, 3); Check(g.UseDexterity(0).Success && g.CanUseYunoOnPendingDie(0));
            rng.EnqueueDie(6); Check(g.ResolveAttackReroll(0, true).Success);
            Check(p.Dexterity == 0 && !p.HasNaturalSix && g.State.LastAttackDie == 5);
        })
    };

    private static (GameEngine, ScriptedRandom) Fixture(int seat = 0)
    {
        var rng = new ScriptedRandom(); var g = new GameEngine(rng);
        g.State.Phase = Phase.Combat; g.State.CurrentPlayer = seat; g.State.Turn = 2;
        foreach (var p in g.State.Players)
        {
            p.Deck.Clear(); p.Deck.AddRange(Enumerable.Repeat("attack", 20));
            p.AttackRerollUsedThisTurn = true;
        }
        return (g, rng);
    }
    private static void StartAttack(GameEngine g, ScriptedRandom rng, int die)
    {
        g.State.Active.Hand.Insert(0, "attack");
        if (g.State.Active.Skills.Count == 0) g.State.Active.Skills.Add(CardCatalog.Destiny.Id);
        rng.EnqueueDie(die); Check(g.Attack(g.State.CurrentPlayer, 0, 0).Success);
    }
    private static void StartCode(GameEngine g, ScriptedRandom rng, string id, int die)
    {
        g.State.Phase = Phase.Actions; g.State.Active.Hand.Insert(0, id); rng.EnqueueDie(die);
        Check(g.PlayCode(g.State.CurrentPlayer, 0).Success);
    }
    private static void Reject(GameEngine g, int actor)
    {
        var before = g.SaveSnapshot(); Check(!g.CanUseDexterity(actor) && !g.UseDexterity(actor).Success && before == g.SaveSnapshot());
    }
    private static void Check(bool ok) { if (!ok) throw new Exception("Regla de destreza no satisfecha."); }
}
