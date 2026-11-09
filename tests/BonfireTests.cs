using Relick.Core;

static class BonfireTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Hoguera: una copia por mazo, equipar cura una vida sin superar el máximo", () =>
        {
            Check(CardCatalog.TrainingDeck().Count == 42 && CardCatalog.TrainingDeck().Count(id => id == CardCatalog.Bonfire.Id) == 1);
            foreach (var actor in new[] { 0, 1 }) foreach (var health in new[] { 0, 5, 8 })
            {
                var (g, _) = Fixture(actor); var p = g.State.Active; p.Health = health;
                var cues = DuelVisualEvents.Capture(g);
                Check(g.Equip(actor, 0).Success);
                Check(p.Health == Math.Min(8, health + 1) && p.ActionsLeft == 1 && p.Equipment == CardCatalog.Bonfire.Id);
                Check(cues.Count(c => c.Kind == DuelVisualKind.Heal) == (health == 8 ? 0 : 1));
                Check(p.EquipmentExpiresAtTurnStart && !p.Memory.Contains(CardCatalog.Bonfire.Id));
            }
        }),
        ("Hoguera: Error de Carga impide curación y conserva el equipo anterior", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Health = 4; p.Equipment = CardCatalog.Pendant.Id;
            g.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
            Check(g.Equip(0, 0).Success && g.State.Phase == Phase.ErrorResponse && p.Health == 4);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
            Check(g.PlayCode(1, g.State.Players[1].Hand.IndexOf(CardCatalog.LoadError.Id)).Success && p.Health == 4 && p.ActionsLeft == 2);
            Check(p.Equipment == CardCatalog.Pendant.Id && !p.EquipmentExpiresAtTurnStart && p.Memory.Contains(CardCatalog.Bonfire.Id));
        }),
        ("Hoguera: absorbe ataques reducidos, normales y críticos, incluso en cero vidas", () =>
        {
            foreach (var actor in new[] { 0, 1 }) foreach (var health in new[] { 0, 6 }) foreach (var die in new[] { 4, 5, 6 })
            {
                var (g, rng) = Fixture(actor); Check(g.Equip(actor, 0).Success);
                var defender = g.State.Active; defender.Health = health; defender.HasReachedZero = health == 0;
                NextTurn(g); var attacker = g.State.Active;
                attacker.Skills.Add(CardCatalog.Destiny.Id); attacker.Hand.Insert(0, CardCatalog.Attack.Id);
                var cues = DuelVisualEvents.Capture(g); rng.EnqueueDie(die);
                Check(g.Attack(1 - actor, 0, 0).Success && g.ResolveAttackReroll(1 - actor, false).Success);
                Check(defender.Health == health && !g.State.IsOver && cues.All(c => c.Kind != DuelVisualKind.Hit));
                Check(attacker.Relikia == CardCatalog.Destiny.Reward && defender.Equipment == CardCatalog.Bonfire.Id);
            }
        }),
        ("Hoguera: solo protege a su dueño; sus ataques dañan al rival y conservan el premio", () =>
        {
            foreach (var actor in new[] { 0, 1 }) foreach (var die in new[] { 4, 5, 6 })
            {
                var (g, rng) = Fixture(actor); Check(g.Equip(actor, 0).Success);
                g.State.Turn = 2; g.State.Phase = Phase.Combat; g.State.Active.Hand.Add(CardCatalog.Attack.Id); g.State.Active.Skills.Add(CardCatalog.Destiny.Id);
                var cues = DuelVisualEvents.Capture(g); rng.EnqueueDie(die);
                Check(g.Attack(actor, 0, 0).Success && g.ResolveAttackReroll(actor, false).Success);
                var damage = GameEngine.DamageFor(die, 3, 5);
                Check(g.State.Players[1 - actor].Health == 8 - damage && g.State.Active.Relikia == 600);
                Check(cues.Single(c => c.Kind == DuelVisualKind.Hit).Value == damage);
            }
        }),
        ("Hoguera: el crítico de Magnet conserva su elección y entrega el premio después", () =>
        {
            var (g, rng) = Fixture(); Check(g.Equip(0, 0).Success); NextTurn(g);
            var defender = g.State.Players[0]; defender.Hand.Add(CardCatalog.Senshuni.Id);
            g.State.Active.Skills.Add(CardCatalog.Magnet.Id); rng.EnqueueDie(6);
            Check(g.Attack(1, 0, 0).Success && g.ResolveAttackReroll(1, false).Success);
            Check(defender.Health == 8 && g.State.Phase == Phase.MagnetChoice && g.State.Active.Relikia == 0);
            Check(g.ResolveMagnet(1, false, 0).Success && g.State.Active.Hand.Contains(CardCatalog.Senshuni.Id));
            Check(g.State.Phase == Phase.Combat && g.State.Active.Relikia == CardCatalog.Magnet.Reward);
        }),
        ("Hoguera: al siguiente turno propio se destruye antes del robo, también al reconectar", () =>
        {
            foreach (var actor in new[] { 0, 1 })
            {
                var (g, rng) = Fixture(actor); Check(g.Equip(actor, 0).Success);
                NextTurn(g); Check(g.State.Players[actor].Equipment == CardCatalog.Bonfire.Id);
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); var p = g.State.Players[actor];
                var order = new List<string>();
                g.CardDrawn += (seat, _) => { if (seat == actor) { Check(p.Equipment == null); order.Add("draw"); } };
                NextTurn(g);
                Check(g.State.CurrentPlayer == actor && g.State.Phase == Phase.Combat && order.Count > 0);
                Check(p.Equipment == null && !p.EquipmentExpiresAtTurnStart && p.Memory.Count(id => id == CardCatalog.Bonfire.Id) == 1);
                Check(g.State.Turn == 3 && p.ActionsLeft == 2);
            }
        }),
        ("Hoguera: Reverse permite sustituir su destrucción antes del robo y recuperar la decisión", () =>
        {
            foreach (var protect in new[] { false, true })
            {
                var (g, rng) = Fixture(); Check(g.Equip(0, 0).Success); var p = g.State.Active;
                p.Skills.Add(CardCatalog.Reverse.Id); NextTurn(g); var hand = p.Hand.Count;
                NextTurn(g);
                Check(g.State.Phase == Phase.DestructionResponse && g.State.DecisionPlayer == 0 && p.Hand.Count == hand);
                var before = g.SaveSnapshot(); Check(!g.Advance(0).Success && !g.ResolveDestructionProtection(1, 0).Success && g.SaveSnapshot() == before);
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
                Check(g.ResolveDestructionProtection(0, protect ? 0 : -1).Success);
                Check(g.State.Phase == Phase.Combat && p.Hand.Count == hand + (hand == 0 ? 3 : 1) && !p.EquipmentExpiresAtTurnStart);
                Check(protect ? p.Equipment == CardCatalog.Bonfire.Id && p.Memory.Contains(CardCatalog.Reverse.Id) :
                    p.Equipment == null && p.Skills.Single() == CardCatalog.Reverse.Id);
                // "Tu siguiente turno" triggers once; substituting that destruction does not rearm it.
                NextTurn(g); NextTurn(g); Check(g.State.Phase == Phase.Combat);
            }
        }),
        ("Hoguera: reemplazarla limpia su vencimiento; proteger el equipo anterior no cura", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active;
            Check(g.Equip(0, 0).Success); p.Hand.Add(CardCatalog.Pendant.Id);
            Check(g.Equip(0, 0).Success && p.Equipment == CardCatalog.Pendant.Id && !p.EquipmentExpiresAtTurnStart);
            NextTurn(g); NextTurn(g); Check(p.Equipment == CardCatalog.Pendant.Id);
            (g, _) = Fixture(); p = g.State.Active; p.Health = 4; p.Equipment = CardCatalog.Pendant.Id; p.Skills.Add(CardCatalog.Reverse.Id);
            Check(g.Equip(0, 0).Success && g.ResolveDestructionProtection(0, 0).Success);
            Check(p.Health == 4 && p.Equipment == CardCatalog.Pendant.Id && !p.EquipmentExpiresAtTurnStart && p.Memory.Contains(CardCatalog.Bonfire.Id));
        }),
        ("Hoguera: destrucción por código quita la protección y reequiparla vuelve a curar", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; p.Health = 4; Check(g.Equip(0, 0).Success);
            p.Hand.Add(CardCatalog.TimeBomb.Id); Check(g.PlayCode(0, 0).Success && g.ResolveDestruction(0, 0, FieldZone.Equipment).Success);
            Check(p.Equipment == null && !p.EquipmentExpiresAtTurnStart);
            p.Memory.Remove(CardCatalog.Bonfire.Id); p.Hand.Add(CardCatalog.Bonfire.Id); p.ActionsLeft = 1;
            Check(g.Equip(0, 0).Success && p.Health == 6 && p.EquipmentExpiresAtTurnStart);
        }),
        ("Hoguera: no evita las vidas inevitables del segundo agotamiento", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; Check(g.Equip(0, 0).Success);
            p.Deck.Clear(); p.Recycled = true; p.Hand.Add(CardCatalog.Senshuni.Id);
            Check(g.PlayCode(0, 0).Success && p.Health == 6 && g.State.IsOver);
        })
    };

    static (GameEngine, ScriptedRandom) Fixture(int actor = 0)
    {
        var rng = new ScriptedRandom(); var g = TestGame.Create(rng);
        g.State.Phase = Phase.Actions; g.State.CurrentPlayer = actor; g.State.Turn = 1;
        foreach (var p in g.State.Players)
        {
            p.Hand.Clear(); p.Memory.Clear(); p.Skills.Clear(); p.Bonus = null;
            p.Deck.Clear(); p.Deck.AddRange(Enumerable.Repeat(CardCatalog.Attack.Id, 20));
            p.Hand.Add(CardCatalog.Attack.Id);
        }
        g.State.Active.Hand[0] = CardCatalog.Bonfire.Id;
        return (g, rng);
    }
    static void NextTurn(GameEngine g)
    {
        var actor = g.State.CurrentPlayer;
        if (g.State.Phase == Phase.Combat) Check(g.Advance(actor).Success);
        Check(g.Advance(actor).Success);
    }
    static void Check(bool value) { if (!value) throw new Exception("Regla de Hoguera incorrecta."); }
}
