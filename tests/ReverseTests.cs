using Relick.Core;

static class ReverseTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Reverse: ataque base 2/4, premio 600 y aumento por cada relanzamiento propio", () =>
        {
            foreach (var source in new[] { "yuno", "pendant", "code" })
            {
                var (g, rng) = Fixture(); var p = g.State.Active; p.Skills.Add(CardCatalog.Reverse.Id);
                if (source == "pendant") p.Equipment = CardCatalog.Pendant.Id;
                if (source == "code") p.Hand.Add(CardCatalog.Reroll.Id);
                Attack(g, rng, 2); rng.EnqueueDie(5);
                Check((source switch { "yuno" => g.ResolveAttackReroll(0, true), "pendant" => g.UsePendant(0, PendantEffect.Reroll), _ => g.PlayCode(0, 0) }).Success);
                if (g.State.Phase == Phase.DestructionResponse) Check(g.ResolveDestructionProtection(0).Success);
                Keep(g);
                Check(p.CountersOnSkill(0) == 1 && g.State.Players[1].Health == 5 && p.Relikia == 600);
            }
        }),
        ("Reverse: todos los ejemplares propios aumentan; cambiar dado y relanzar el rival no aumentan", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active;
            p.Skills.AddRange([CardCatalog.Reverse.Id, CardCatalog.Destiny.Id, CardCatalog.Reverse.Id]);
            g.State.Players[1].Skills.Add(CardCatalog.Reverse.Id);
            Attack(g, rng, 2, 1); rng.EnqueueDie(5); Check(g.ResolveAttackReroll(0, true).Success);
            Check(p.CountersOnSkill(0) == 1 && p.CountersOnSkill(2) == 1 && g.State.Players[1].CountersOnSkill(0) == 0);
            g.State.AttackedThisTurn = false; p.AttackRerollUsedThisTurn = false; p.Hand.Add(CardCatalog.Agility.Id);
            Attack(g, rng, 3, 1); Check(g.PlayCode(0, 0).Success); Keep(g);
            Check(p.CountersOnSkill(0) == 1 && p.CountersOnSkill(2) == 1);
        }),
        ("Reverse: normal máximo cinco, crítico sigue creciendo y los aumentos persisten", () =>
        {
            foreach (var die in new[] { 5, 6 })
            {
                var (g, rng) = Fixture(); var p = g.State.Active; p.Skills.Add(CardCatalog.Reverse.Id); p.SkillCounters[0] = 6;
                var damage = 0; g.DamageReceived += (_, amount, _) => damage = amount;
                Attack(g, rng, die); Keep(g);
                Check(damage == (die == 5 ? 5 : 10) && p.Relikia == 600 && p.CountersOnSkill(0) == 6);
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
                Check(g.State.Players[0].CountersOnSkill(0) == 6);
            }
        }),
        ("Reverse: solo aprender en 500/1000 PR permite dos relanzamientos gratuitos, incluso del uno", () =>
        {
            foreach (var slot in new[] { 0, 1, 2, 3 })
            {
                var (g, rng) = Fixture(); var p = g.State.Active; p.Skills.AddRange(Enumerable.Repeat(CardCatalog.Destiny.Id, slot));
                p.Hand.Add(CardCatalog.Reverse.Id); p.Relikia = GameEngine.SkillSlotCosts[slot]; rng.EnqueueDie(1);
                Check(g.Learn(0, 0).Success && p.Relikia == 0 && p.ActionsLeft == 2);
                if (slot < 2) { Check(g.State.Phase == Phase.Combat && p.Hand.Count == 0); continue; }
                Check(g.CanRerollLearningDie(0) && g.State.PendingDieRoll!.Die == 1);
                rng.EnqueueDie(1); Check(g.RerollLearningDie(0).Success && g.CanRerollLearningDie(0));
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
                rng.EnqueueDie(6); Check(g.RerollLearningDie(0).Success);
                Check(g.State.Phase == Phase.Combat && p.Hand.Count == 3 && p.CountersOnSkill(slot) == 2 && !p.AttackRerollUsedThisTurn);
                Reject(g, () => g.RerollLearningDie(0));
            }
        }),
        ("Reverse: robo de aprendizaje redondea arriba y conservar el dado no aumenta daños", () =>
        {
            foreach (var die in Enumerable.Range(1, 6))
            {
                var (g, rng) = Fixture(); var p = g.State.Active; p.Skills.AddRange([CardCatalog.Destiny.Id, CardCatalog.Magnet.Id]);
                p.Relikia = 500; p.Hand.Add(CardCatalog.Reverse.Id); rng.EnqueueDie(die);
                Check(g.Learn(0, 0).Success && g.ResolveDieReroll(0, false).Success);
                Check(p.Hand.Count == (die + 1) / 2 && p.CountersOnSkill(2) == 0 && g.State.Phase == Phase.Combat);
            }
        }),
        ("Reverse: relanzamientos del dado de efecto aumentan daños y se combinan con aprendizaje", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Relikario = RelikarioCatalog.YunoLv2;
            p.Skills.AddRange([CardCatalog.Destiny.Id, CardCatalog.Magnet.Id]); p.Relikia = 500;
            p.Hand.AddRange([CardCatalog.Reverse.Id, CardCatalog.Reroll.Id]); rng.EnqueueDie(1);
            Check(g.Learn(0, 0).Success); rng.EnqueueDie(3); Check(g.RerollLearningDie(0).Success);
            rng.EnqueueDie(4); Check(g.ResolveDieReroll(0, true).Success);
            rng.EnqueueDie(5); Check(g.PlayCode(0, 0).Success);
            Check(g.State.PendingDieRoll!.Die == 5 && g.State.PendingDieRoll.LearningRerolls == 1 && p.CountersOnSkill(2) == 3);
            Check(g.ResolveDieReroll(0, false).Success && p.Hand.Count == 3);
        }),
        ("Reverse: protege equipos, Bonus, canje y cartas sobre el Relikario; elegir es opcional y propio", () =>
        {
            foreach (var zone in new[] { FieldZone.Equipment, FieldZone.Bonus, FieldZone.Exchange, FieldZone.Attached })
            foreach (var protect in new[] { false, true })
            {
                var (g, rng) = Fixture(); var defender = g.State.Players[1]; defender.Skills.Add(CardCatalog.Reverse.Id);
                if (zone == FieldZone.Equipment) defender.Equipment = CardCatalog.Pc.Id;
                if (zone == FieldZone.Bonus) defender.Bonus = CardCatalog.Pc.Id;
                if (zone == FieldZone.Exchange) defender.Exchange.Add(CardCatalog.Pc.Id);
                if (zone == FieldZone.Attached) defender.AttachedCards.Add(CardCatalog.Pc.Id);
                g.State.Phase = Phase.Actions; g.State.Active.Hand.Add(CardCatalog.BlueEgg.Id); g.State.Active.Relikia = 200;
                Check(g.PlayCode(0, 0).Success && g.ResolveDestruction(0, 1, zone, zone is FieldZone.Exchange or FieldZone.Attached ? 0 : -1).Success);
                Check(g.State.Phase == Phase.DestructionResponse && g.State.DecisionPlayer == 1);
                Check(DuelSnapshot.For(g, 1, 1).Stage == Phase.Actions);
                Reject(g, () => g.ResolveDestructionProtection(0, 0)); Reject(g, () => g.ResolveDestructionProtection(1, 9));
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); defender = g.State.Players[1];
                Check(g.ResolveDestructionProtection(1, protect ? 0 : -1).Success);
                Check(defender.Skills.Count == (protect ? 0 : 1) && defender.Memory.Contains(protect ? CardCatalog.Reverse.Id : CardCatalog.Pc.Id));
                Check(defender.Memory.Contains(CardCatalog.Pc.Id) == !protect);
                Check(g.State.Phase == Phase.Actions && g.State.Active.ActionsLeft == 1 && g.State.Active.Relikia == 0);
            }
        }),
        ("Reverse: protege mano privada sin revelarla ni activar el descarte de Sniper", () =>
        {
            foreach (var protect in new[] { false, true })
            {
                var (g, rng) = Fixture(); var defender = g.State.Players[1]; defender.Skills.Add(CardCatalog.Reverse.Id); defender.Hand.Add(CardCatalog.Sniper.Id);
                g.State.Phase = Phase.Actions; g.State.Active.Hand.Add(CardCatalog.TimeBomb.Id);
                Check(g.PlayCode(0, 0).Success); rng.EnqueueDie(1);
                Check(g.ResolveDestruction(0, 1, FieldZone.Hand).Success);
                var view = DuelSnapshot.For(g, 0, 1);
                Check(view.Players[1].Hand.Single() == null && !view.LastCombat.Contains(CardCatalog.Sniper.Name));
                Check(g.ResolveDestructionProtection(1, protect ? 0 : -1).Success);
                Check(defender.Hand.Count == (protect ? 1 : 0) && defender.Equipment == null && g.State.PendingSniper == null);
            }
        }),
        ("Reverse: una habilidad propia no puede proteger cartas rivales", () =>
        {
            var (g, _) = Fixture(); g.State.Active.Skills.Add(CardCatalog.Reverse.Id); g.State.Players[1].Equipment = CardCatalog.Pc.Id;
            g.State.Phase = Phase.Actions; g.State.Active.Hand.Add(CardCatalog.BlueEgg.Id); g.State.Active.Relikia = 200;
            Check(g.PlayCode(0, 0).Success && g.ResolveDestruction(0, 1, FieldZone.Equipment).Success);
            Check(g.State.Phase == Phase.Actions && g.State.Active.Skills.Count == 1 && g.State.Players[1].Equipment == null);
        }),
        ("Reverse: conserva PC o Colgante pagando la destrucción con la habilidad", () =>
        {
            foreach (var pc in new[] { false, true })
            {
                var (g, rng) = Fixture(); var p = g.State.Active; p.Skills.Add(CardCatalog.Reverse.Id);
                p.Equipment = pc ? CardCatalog.Pc.Id : CardCatalog.Pendant.Id; p.EquipmentDie = pc ? 2 : 0; p.AttackRerollUsedThisTurn = true;
                Attack(g, rng, 1); if (!pc) rng.EnqueueDie(5);
                Check((pc ? g.UsePc(0) : g.UsePendant(0, PendantEffect.Reroll)).Success && g.State.Phase == Phase.DestructionResponse);
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
                Check(g.ResolveDestructionProtection(0, 0).Success && p.Equipment == (pc ? CardCatalog.Pc.Id : CardCatalog.Pendant.Id));
                Check(p.Skills.Count == 0 && p.Memory.Contains(CardCatalog.Reverse.Id));
                Keep(g);
                Check(g.State.LastAttackDie == (pc ? 3 : 5) && g.State.Players[1].Health == (pc ? 8 : 6));
            }
        }),
        ("Reverse: conserva Caja al negar daño o cancelar un código y respeta premios y reembolsos", () =>
        {
            foreach (var code in new[] { false, true })
            {
                var (g, rng) = Fixture(); var defender = g.State.Players[1];
                defender.Skills.Add(CardCatalog.Reverse.Id); defender.Equipment = CardCatalog.CardboardBox.Id;
                if (code)
                {
                    g.State.Phase = Phase.Actions; g.State.Active.Hand.Add(CardCatalog.BlueEgg.Id); g.State.Active.Relikia = 200;
                    Check(g.PlayCode(0, 0).Success && g.ResolveCodeResponse(1, true).Success);
                }
                else { g.State.Active.Skills.Add(CardCatalog.Destiny.Id); Attack(g, rng, 5); Keep(g); Check(g.CancelAttackWithBox(1).Success); }
                Check(g.State.Phase == Phase.DestructionResponse && defender.Health == 8);
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); defender = g.State.Players[1];
                Check(g.ResolveDestructionProtection(1, 0).Success && defender.Equipment == CardCatalog.CardboardBox.Id && defender.Skills.Count == 0);
                Check(g.State.Active.Relikia == (code ? 200 : 600) && g.State.Active.ActionsLeft == 2);
            }
        }),
        ("Reverse: ambos cascos deciden por separado antes de resolver un dado uno", () =>
        {
            var (g, rng) = Fixture(); foreach (var p in g.State.Players) { p.Skills.Add(CardCatalog.Reverse.Id); p.Equipment = CardCatalog.DiamondHelmet.Id; }
            Attack(g, rng, 1); Check(g.State.Phase == Phase.DestructionResponse && g.State.DecisionPlayer == 0);
            Check(DuelSnapshot.For(g, 0, 1).Stage == Phase.Combat);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            Check(g.ResolveDestructionProtection(0, 0).Success && g.State.Phase == Phase.DestructionResponse && g.State.DecisionPlayer == 1);
            Check(g.ResolveDestructionProtection(1).Success && g.State.Phase == Phase.Combat && g.State.LastAttackDie == 1);
            Check(g.State.Players[0].Equipment == CardCatalog.DiamondHelmet.Id && g.State.Players[1].Equipment == null);
        }),
        ("Reverse: protege Sniper del tercer daño y el premio espera a resolver la destrucción", () =>
        {
            var (g, rng) = Fixture(); g.State.Active.Skills.Add(CardCatalog.Destiny.Id);
            var defender = g.State.Players[1]; defender.Skills.Add(CardCatalog.Reverse.Id); defender.Equipment = CardCatalog.Sniper.Id; defender.EquipmentCounters = 2;
            Attack(g, rng, 5); Keep(g);
            Check(g.State.Phase == Phase.DestructionResponse && defender.Health == 8 && g.State.Active.Relikia == 0);
            Check(g.ResolveDestructionProtection(1, 0).Success && defender.Health == 5 && defender.EquipmentCounters == 3 && defender.Equipment == CardCatalog.Sniper.Id);
            Check(g.State.Active.Relikia == 600 && g.State.PendingAttack == null);
        }),
        ("Reverse: sacrificar el atacante conserva su daño declarado y reasigna contadores de otras copias", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active;
            p.Skills.AddRange([CardCatalog.Reverse.Id, CardCatalog.Destiny.Id]); p.SkillCounters[0] = 1; p.SkillCounters[1] = 1;
            p.Hand.AddRange([CardCatalog.Reroll.Id, CardCatalog.Reroll.Id]); p.Equipment = CardCatalog.DiamondHelmet.Id; p.AttackRerollUsedThisTurn = true;
            Attack(g, rng, 2); rng.EnqueueDie(1); Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.DestructionResponse);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
            Check(g.ResolveDestructionProtection(0, 0).Success && p.Skills.Single() == CardCatalog.Destiny.Id && p.CountersOnSkill(0) == 1);
            rng.EnqueueDie(5); Check(g.PlayCode(0, 0).Success); Keep(g);
            Check(g.State.LastAttackDie == 5 && g.State.Players[1].Health == 4 && p.Relikia == 600);
            Check(p.SkillPosition(0) == 1 && g.SkillCost(p) == 0);
            Check(DuelSnapshot.For(g, 0, 2).Players[0].SkillPositions.Single() == 1);
            g.State.AttackedThisTurn = false; p.Hand.Clear(); p.Hand.Add(CardCatalog.Reverse.Id); p.Relikia = 0;
            Check(g.Learn(0, 0).Success && p.SkillPosition(1) == 0 && p.CountersOnSkill(0) == 1);
            Check(g.State.Phase == Phase.Combat && p.Hand.Count == 0 && g.SkillCost(p) == 500);
        }),
        ("Reverse: aprendizaje que agota el mazo se recupera sin duplicar el robo", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active;
            p.Skills.AddRange([CardCatalog.Destiny.Id, CardCatalog.Magnet.Id]); p.Hand.Add(CardCatalog.Reverse.Id); p.Relikia = 500;
            p.Deck.Clear(); p.Deck.Add("attack"); p.Memory.AddRange(Enumerable.Repeat("attack", 5));
            rng.EnqueueDie(1); rng.EnqueueDie(3);
            Check(g.Learn(0, 0).Success && g.ResolveDieReroll(0, false).Success && g.State.Phase == Phase.RecycleChoice);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
            Check(g.ResolveRecycling(0, [0, 1, 2]).Success && p.Hand.Count == 1 && p.Deck.Count == 3 && g.State.Phase == Phase.Combat);
        }),
        ("Reverse: la protección sigue disponible después de cerrar Error; no se puede cancelar la destrucción tardíamente", () =>
        {
            foreach (var counter in new[] { false, true })
            {
                var (g, rng) = Fixture(); var p = g.State.Active; var defender = g.State.Players[1];
                g.State.Phase = Phase.Actions; p.Relikia = 200; p.Hand.Add(CardCatalog.BlueEgg.Id);
                if (counter) p.Hand.Add(CardCatalog.LoadError.Id);
                defender.Equipment = CardCatalog.Pc.Id; defender.Skills.Add(CardCatalog.Reverse.Id); defender.Hand.Add(CardCatalog.LoadError.Id);
                Check(g.PlayCode(0, 0).Success && g.PassErrorResponse(1).Success && g.ResolveDestruction(0, 1, FieldZone.Equipment).Success);
                Check(g.State.Phase == Phase.DestructionResponse);
                var before = g.SaveSnapshot(); Check(!g.PlayCode(1, 0).Success && before == g.SaveSnapshot());
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active; defender = g.State.Players[1];
                Check(!g.CanPlayLoadError(1) && g.ResolveDestructionProtection(1, 0).Success);
                Check(g.State.Phase == Phase.Actions && g.State.PendingDestruction == null && defender.Equipment == CardCatalog.Pc.Id);
                Check(defender.Skills.Count == 0 && p.Relikia == 0 && p.ActionsLeft == 1 && defender.Hand.Contains(CardCatalog.LoadError.Id));
            }
        }),
        ("Reverse: proteger el equipo anterior no crea dos equipos normales al equipar otro", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active;
            p.Skills.Add(CardCatalog.Reverse.Id); p.Equipment = CardCatalog.DiamondHelmet.Id;
            p.Hand.Add(CardCatalog.Pc.Id); g.State.Phase = Phase.Actions;
            Check(g.Equip(0, 0).Success && g.State.Phase == Phase.DestructionResponse);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
            Check(g.ResolveDestructionProtection(0, 0).Success && p.Equipment == CardCatalog.DiamondHelmet.Id && p.Memory.Contains(CardCatalog.Pc.Id));
            Check(p.ActionsLeft == 1 && p.Skills.Count == 0 && g.State.Phase == Phase.Actions);
        })
    };

    private static (GameEngine, ScriptedRandom) Fixture()
    {
        var rng = new ScriptedRandom(); var g = new GameEngine(rng); g.State.CurrentPlayer = 0; g.State.Phase = Phase.Combat; g.State.Turn = 1;
        foreach (var p in g.State.Players) { p.Dexterity = 0; p.Deck.Clear(); p.Deck.AddRange(Enumerable.Repeat("attack", 30)); }
        return (g, rng);
    }
    private static void Attack(GameEngine g, ScriptedRandom rng, int die, int slot = 0)
    {
        g.State.Active.Hand.Add("attack"); rng.EnqueueDie(die);
        Check(g.Attack(0, g.State.Active.Hand.Count - 1, slot).Success);
    }
    private static void Keep(GameEngine g)
    {
        if (g.State.Phase == Phase.AttackRerollChoice) Check(g.ResolveAttackReroll(0, false).Success);
    }
    private static void Reject(GameEngine g, Func<CommandResult> action)
    {
        var saved = g.SaveSnapshot(); Check(!action().Success && saved == g.SaveSnapshot());
    }
    private static void Check(bool ok) { if (!ok) throw new Exception("Reverse Shuffle no cumple su regla."); }
}
