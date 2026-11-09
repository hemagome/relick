using Relick.Core;

static class JessicaTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Jessica: Personaje de canje/Bonus y una copia en el mazo de 42 cartas", () =>
        {
            var c = CardCatalog.Jessica;
            Check(c.Kind == CardKind.Code && c.CodeType == CodeType.Character && c.FieldCombatDie && c.ActionCost == 0);
            Check(CardCatalog.TrainingDeck().Count == 42 && CardCatalog.TrainingDeck().Count(id => id == c.Id) == 1);
            var (g, _) = Fixture(); g.State.Active.Hand.Add(c.Id); g.State.Phase = Phase.Actions;
            Reject(g, () => g.PlayCode(0, 0)); g.State.Active.Memory.Add(c.Id); g.State.Active.Relikia = 400;
            Reject(g, () => g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0));
            Check(g.Exchange(0, 0).Success && g.State.Active.Exchange.Single() == c.Id);
        }),
        ("Jessica: los seis resultados en ambas zonas y asientos, sin acciones, PR ni ataque", () =>
        {
            foreach (var actor in new[] { 0, 1 }) foreach (var zone in new[] { FieldZone.Bonus, FieldZone.Exchange }) foreach (var die in Enumerable.Range(1, 6))
            {
                var (g, rng) = Fixture(actor, zone); var p = g.State.Active; p.ActionsLeft = 0;
                var count = Count(g); var cues = DuelVisualEvents.Capture(g); rng.EnqueueDie(die);
                Check(g.UseFieldCode(actor, zone, zone == FieldZone.Bonus ? -1 : 0).Success && g.State.Phase == Phase.Combat);
                Check(g.State.Players[1 - actor].Health == 8 - (die == 5 ? 2 : die == 6 ? 3 : 0));
                Check(p.Hand.Count == (die == 6 ? 1 : 0) && p.ActionsLeft == 0 && p.Relikia == 0 && !g.State.AttackedThisTurn && Count(g) == count);
                Check(cues.All(v => v.Kind != DuelVisualKind.Attack) && cues.Where(v => v.Kind == DuelVisualKind.Hit).All(v => v.AttackDie == 0));
                Check(cues.Count(v => v.Kind == DuelVisualKind.Draw) == (die == 6 ? 1 : 0));
                Check(zone == FieldZone.Bonus ? p.Bonus == CardCatalog.Jessica.Id : p.Exchange.Single() == CardCatalog.Jessica.Id);
            }
        }),
        ("Jessica: consume su uso incluso al fallar y se renueva al siguiente combate propio", () =>
        {
            var (g, rng) = Fixture(); rng.EnqueueDie(1); Check(g.UseFieldCode(0, FieldZone.Bonus).Success);
            Reject(g, () => g.UseFieldCode(0, FieldZone.Bonus));
            Check(g.Advance(0).Success && g.Advance(0).Success); Reject(g, () => g.UseFieldCode(0, FieldZone.Bonus));
            Check(g.Advance(1).Success && g.Advance(1).Success); rng.EnqueueDie(5);
            Check(g.UseFieldCode(0, FieldZone.Bonus).Success && g.State.Players[1].Health == 6);
        }),
        ("Jessica: cada copia tiene un uso; los usos son públicos y sobreviven al reconectar", () =>
        {
            var (g, rng) = Fixture(); g.State.Active.Exchange.Add(CardCatalog.Jessica.Id);
            rng.EnqueueDie(5); Check(g.UseFieldCode(0, FieldZone.Bonus).Success);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); rng.EnqueueDie(5); Check(g.UseFieldCode(0, FieldZone.Exchange, 0).Success);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            Check(!g.FieldCombatTargets(0).Any() && g.State.Players[1].Health == 4);
            var view = DuelSnapshot.For(g, 1, 3).Players[0]; Check(view.BonusEffectUsed && view.ExchangeEffectsUsed!.Single() == 0);
        }),
        ("Jessica: puede usarse después de atacar y valida turno, etapa, zona e índice sin mutar", () =>
        {
            var (g, rng) = Fixture(); g.State.AttackedThisTurn = true;
            Check(g.HasAvailablePlay(0) && DuelOptions.For(g, 0).Any(o => o.Move.Kind == DuelMoveKind.FieldCode));
            Reject(g, () => g.UseFieldCode(1, FieldZone.Bonus)); Reject(g, () => g.UseFieldCode(0, FieldZone.Bonus, 0));
            Reject(g, () => g.UseFieldCode(0, FieldZone.Hand)); Reject(g, () => g.UseFieldCode(0, FieldZone.Exchange, 99));
            g.State.Phase = Phase.Actions; Reject(g, () => g.UseFieldCode(0, FieldZone.Bonus)); g.State.Phase = Phase.Combat;
            rng.EnqueueDie(5); Check(DuelOptions.For(g, 0).Single(o => o.Move.Kind == DuelMoveKind.FieldCode).Move.Apply(g, 0).Success);
        }),
        ("Jessica: Yuno Lv 2 no suma al dado de efecto; relanzar sí aumenta Reverse", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Relikario = RelikarioCatalog.YunoLv2;
            p.Skills.Add(CardCatalog.Reverse.Id); rng.EnqueueDie(3); Check(g.UseFieldCode(0, FieldZone.Bonus).Success);
            Check(g.State.Phase == Phase.DieRerollChoice && g.State.PendingDieRoll?.Die == 3);
            rng.EnqueueDie(5); Check(g.ResolveDieReroll(0, true).Success && g.State.PendingDieRoll?.Die == 5);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
            Check(g.ResolveDieReroll(0, false).Success && p.CountersOnSkill(0) == 1 && g.State.Players[1].Health == 6 && p.Hand.Count == 0);
        }),
        ("Jessica: Agilidad, PC y destreza modifican el resultado sin convertirlo en ataque", () =>
        {
            foreach (var modifier in new[] { "agility", "pc", "dexterity" })
            {
                var (g, rng) = Fixture(); var p = g.State.Active;
                if (modifier == "agility") p.Hand.Add(CardCatalog.Agility.Id);
                if (modifier == "pc") { p.Equipment = CardCatalog.Pc.Id; p.EquipmentDie = 6; }
                if (modifier == "dexterity") p.Dexterity = 1;
                rng.EnqueueDie(4); Check(g.UseFieldCode(0, FieldZone.Bonus).Success && g.State.Phase == Phase.DieRerollChoice);
                Check((modifier == "agility" ? g.PlayCode(0, 0) : modifier == "pc" ? g.UsePc(0) : g.UseDexterity(0)).Success);
                if (g.State.Phase == Phase.DieRerollChoice) Check(g.ResolveDieReroll(0, false).Success);
                Check(g.State.Players[1].Health == (modifier == "pc" ? 5 : 6) && p.Hand.Count == (modifier == "pc" ? 1 : 0));
            }
        }),
        ("Jessica: Error cancela antes del dado en el turno de entrada y rechaza cancelaciones tardías", () =>
        {
            foreach (var zone in new[] { FieldZone.Bonus, FieldZone.Exchange }) foreach (var late in new[] { false, true })
            {
                var (g, rng) = Fixture(0, zone); var p = g.State.Active; var other = g.State.Players[1]; p.Dexterity = 1;
                if (!late) other.Hand.Add(CardCatalog.LoadError.Id);
                rng.EnqueueDie(4); Check(g.UseFieldCode(0, zone, zone == FieldZone.Bonus ? -1 : 0).Success);
                Check(DuelSnapshot.For(g, 0, 1).Stage == Phase.Combat);
                if (late) other.Hand.Add(CardCatalog.LoadError.Id);
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active; other = g.State.Players[1];
                if (late)
                {
                    Reject(g, () => g.PlayCode(1, 0));
                    Check(!g.CanPlayLoadError(1) && g.State.PendingDieRoll?.Die == 4);
                    Check(g.ResolveDieReroll(0, false).Success && p.Memory.Count == 0 && other.Health == 8);
                    Check(zone == FieldZone.Bonus ? p.Bonus == CardCatalog.Jessica.Id : p.Exchange.Single() == CardCatalog.Jessica.Id);
                    continue;
                }
                Check(g.PlayCode(1, 0).Success && g.State.Phase == Phase.Combat && g.State.PendingDieRoll == null);
                Check(p.Memory.Single() == CardCatalog.Jessica.Id && p.Bonus == null && p.Exchange.Count == 0 && other.Health == 8 && p.Hand.Count == 0);
            }
        }),
        ("Jessica: contraerror conserva la carta y Caja no responde a un Personaje", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Hand.Add(CardCatalog.LoadError.Id);
            var other = g.State.Players[1]; other.Hand.Add(CardCatalog.LoadError.Id); other.Equipment = CardCatalog.CardboardBox.Id;
            Check(g.UseFieldCode(0, FieldZone.Bonus).Success && g.State.Phase == Phase.ErrorResponse);
            rng.EnqueueDie(6); Check(g.PlayCode(1, 0).Success && g.PlayCode(0, 0).Success && g.State.Phase == Phase.Combat);
            Check(p.Bonus == CardCatalog.Jessica.Id && other.Equipment == CardCatalog.CardboardBox.Id && other.Health == 5 && p.Hand.Count == 1);
        }),
        ("Jessica: Error no abre ventana ni cancela el dado en turnos posteriores", () =>
        {
            foreach (var actor in new[] { 0, 1 }) foreach (var zone in new[] { FieldZone.Bonus, FieldZone.Exchange })
            {
                var (g, rng) = Fixture(actor, zone); var p = g.State.Active; p.Dexterity = 1;
                Check(g.Advance(actor).Success && g.Advance(actor).Success && g.Advance(1 - actor).Success && g.Advance(1 - actor).Success);
                var other = g.State.Players[1 - actor]; other.Hand.Add(CardCatalog.LoadError.Id);
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); rng.EnqueueDie(4);
                Check(g.UseFieldCode(actor, zone, zone == FieldZone.Bonus ? -1 : 0).Success && g.State.Phase == Phase.DieRerollChoice);
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); other = g.State.Players[1 - actor];
                Check(!g.CanPlayLoadError(1 - actor)); Reject(g, () => g.PlayCode(1 - actor, other.Hand.IndexOf(CardCatalog.LoadError.Id)));
                Check(g.UseDexterity(actor).Success && g.State.Phase == Phase.Combat && other.Health == 6);
                Check(other.Hand.Contains(CardCatalog.LoadError.Id) && !g.State.Active.Memory.Contains(CardCatalog.Jessica.Id));
            }
        }),
        ("Jessica: Bonus a canje conserva el turno de entrada y las partidas antiguas quedan protegidas", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active;
            Check(g.Advance(0).Success && g.Advance(0).Success && g.Advance(1).Success && g.Advance(1).Success);
            Check(g.Advance(0).Success); p.Relikia = 100;
            Check(g.UseRelikiaEffect(0, RelikiaEffect.ChangeBonus).Success && p.ExchangePlacementTurns![0] == 1);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); Check(g.State.Active.ExchangePlacementTurns![0] == 1);
            foreach (var zone in new[] { FieldZone.Bonus, FieldZone.Exchange })
            {
                (g, rng) = Fixture(0, zone); p = g.State.Active;
                p.BonusPlacedTurn = 0; p.ExchangePlacementTurns = null; p.Dexterity = 1;
                g.State.Players[1].Hand.Add(CardCatalog.LoadError.Id); rng.EnqueueDie(4);
                Check(g.UseFieldCode(0, zone, zone == FieldZone.Bonus ? -1 : 0).Success && g.State.Phase == Phase.DieRerollChoice && !g.CanPlayLoadError(1));
            }
        }),
        ("Jessica: una copia nueva en Bonus obtiene su propio turno de entrada para Error", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active;
            Check(g.Advance(0).Success && g.Advance(0).Success && g.Advance(1).Success && g.Advance(1).Success);
            p.Exchange.Add(CardCatalog.Attack.Id); p.Hand.AddRange([CardCatalog.PlumberStar.Id, CardCatalog.Sniper.Id]);
            p.Deck.Insert(0, CardCatalog.Jessica.Id); rng.EnqueueDie(1);
            Check(g.UseFieldCode(0, FieldZone.Bonus).Success && g.ResolveDieReroll(0, false).Success);
            Check(g.PlayCode(0, p.Hand.IndexOf(CardCatalog.PlumberStar.Id)).Success);
            Check(g.ResolveTurnBuffDiscard(0, p.Hand.IndexOf(CardCatalog.Sniper.Id)).Success && g.ResolveSniper(0, false).Success);
            Check(p.BonusPlacedTurn == g.State.Turn && p.BonusPlacedTurn == 3);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active; g.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
            Check(g.UseFieldCode(0, FieldZone.Bonus).Success && g.State.Phase == Phase.ErrorResponse);
            Check(g.PlayCode(1, g.State.Players[1].Hand.IndexOf(CardCatalog.LoadError.Id)).Success);
            Check(p.Bonus == null && p.Memory.Contains(CardCatalog.Jessica.Id) && p.Hand.Contains(CardCatalog.Jessica.Id));
        }),
        ("Jessica: daño respeta Casco, Hoguera y Estrella; un seis roba aunque se absorba todo", () =>
        {
            foreach (var protection in new[] { "diamond-helmet", "bonfire", "star" }) foreach (var die in new[] { 5, 6 })
            {
                var (g, rng) = Fixture(); var p = g.State.Active; var other = g.State.Players[1];
                if (protection == "star") other.StarEffects = 1; else other.Equipment = protection;
                var cues = DuelVisualEvents.Capture(g); rng.EnqueueDie(die); Check(g.UseFieldCode(0, FieldZone.Bonus).Success);
                var damage = protection == "diamond-helmet" ? die - 5 : 0;
                Check(other.Health == 8 - damage && p.Hand.Count == (die == 6 ? 1 : 0));
                Check(cues.Count(v => v.Kind == DuelVisualKind.Hit) == (damage == 0 ? 0 : 1));
            }
        }),
        ("Jessica: Mira suma al daño del efecto y Estrella duplica ese total", () =>
        {
            foreach (var die in new[] { 4, 5, 6 })
            {
                var (g, rng) = Fixture(); var p = g.State.Active; p.Equipment = CardCatalog.Sniper.Id; p.StarEffects = 1;
                var damage = 0; g.DamageReceived += (_, amount, _) => damage = amount;
                rng.EnqueueDie(die); Check(g.UseFieldCode(0, FieldZone.Bonus).Success);
                Check(damage == (die == 4 ? 0 : die == 5 ? 6 : 8));
            }
        }),
        ("Jessica: el rival puede activar Estrella antes del daño; Esquivar y Caja no defienden este efecto", () =>
        {
            foreach (var protect in new[] { false, true })
            {
                var (g, rng) = Fixture(); var other = g.State.Players[1]; other.Equipment = CardCatalog.CardboardBox.Id;
                other.Hand.AddRange([CardCatalog.PlumberStar.Id, CardCatalog.Senshuni.Id, CardCatalog.Dodge.Id]);
                rng.EnqueueDie(6); Check(g.UseFieldCode(0, FieldZone.Bonus).Success && g.State.Phase == Phase.EffectDamageChoice);
                Check(g.State.DecisionPlayer == 1 && DuelSnapshot.For(g, 1, 1).Stage == Phase.Combat);
                Reject(g, () => g.PlayCode(1, 2)); Reject(g, () => g.CancelAttackWithBox(1)); Reject(g, () => g.ResolveEffectDamage(0));
                if (protect)
                {
                    Check(g.PlayCode(1, 0).Success && g.ResolveTurnBuffDiscard(1, 0).Success);
                    g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); other = g.State.Players[1];
                    Check(g.State.Phase == Phase.EffectDamageChoice && other.StarEffects == 1);
                }
                Check(g.ResolveEffectDamage(1).Success && other.Health == (protect ? 8 : 5) && g.State.Active.Hand.Count == 1);
                Check(g.State.PendingEffectDamage == null && g.State.Phase == Phase.Combat && other.Equipment == CardCatalog.CardboardBox.Id);
            }
        }),
        ("Jessica: daño y robo esperan la decisión de destrucción del tercer contador de Mira", () =>
        {
            var (g, rng) = Fixture(); var other = g.State.Players[1];
            other.Equipment = CardCatalog.Sniper.Id; other.EquipmentCounters = 2; other.Skills.Add(CardCatalog.Reverse.Id);
            rng.EnqueueDie(6); Check(g.UseFieldCode(0, FieldZone.Bonus).Success && g.State.Phase == Phase.DestructionResponse);
            Check(g.State.Active.Hand.Count == 0 && other.Health == 8);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); other = g.State.Players[1];
            Check(g.ResolveDestructionProtection(1, 0).Success && other.Health == 5 && g.State.Active.Hand.Count == 1 && g.State.Phase == Phase.Combat);
        }),
        ("Jessica: el robo de seis conserva su uso y decisión al agotar el mazo", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Deck.Clear(); p.Deck.Add(CardCatalog.Attack.Id);
            p.Memory.AddRange([CardCatalog.Senshuni.Id, CardCatalog.ExtraLife.Id]); rng.EnqueueDie(6); rng.EnqueueDie(2);
            Check(g.UseFieldCode(0, FieldZone.Bonus).Success && g.State.Phase == Phase.RecycleChoice && p.Hand.Count == 1);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
            Check(g.ResolveRecycling(0, [0, 1]).Success && p.Hand.Count == 1 && p.BonusEffectUsed && g.State.Phase == Phase.Combat);
            Reject(g, () => g.UseFieldCode(0, FieldZone.Bonus));
        }),
        ("Jessica: una copia nueva repuesta en Bonus no hereda el uso de la anterior", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Exchange.Add(CardCatalog.Attack.Id);
            p.Hand.AddRange([CardCatalog.PlumberStar.Id, CardCatalog.Sniper.Id]); p.Deck.Insert(0, CardCatalog.Jessica.Id);
            rng.EnqueueDie(1); Check(g.UseFieldCode(0, FieldZone.Bonus).Success);
            if (g.State.Phase == Phase.DieRerollChoice) Check(g.ResolveDieReroll(0, false).Success);
            Check(p.BonusEffectUsed && g.PlayCode(0, 0).Success && g.ResolveTurnBuffDiscard(0, 0).Success && g.ResolveSniper(0, false).Success);
            Check(p.Bonus == CardCatalog.Jessica.Id && !p.BonusEffectUsed && p.Hand.Contains(CardCatalog.Jessica.Id));
            rng.EnqueueDie(5); Check(g.UseFieldCode(0, FieldZone.Bonus).Success && g.State.Players[1].Health == 4);
        }),
        ("Jessica: destruir otra carta de canje desplaza los usos sin reactivar la misma copia", () =>
        {
            var (g, rng) = Fixture(0, FieldZone.Exchange); var p = g.State.Active; p.Exchange.Insert(0, CardCatalog.Attack.Id);
            p.ExchangePlacementTurns = new() { [1] = g.State.Turn };
            rng.EnqueueDie(5); Check(g.UseFieldCode(0, FieldZone.Exchange, 1).Success);
            g.State.Phase = Phase.Actions; p.Hand.Add(CardCatalog.BlueEgg.Id); p.Relikia = 200;
            Check(g.PlayCode(0, 0).Success && g.ResolveDestruction(0, 0, FieldZone.Exchange, 0).Success);
            Check(p.ExchangeEffectsUsed!.Single() == 0 && p.Exchange.Single() == CardCatalog.Jessica.Id && p.ExchangePlacementTurns![0] == g.State.Turn);
            g.State.Phase = Phase.Combat; Reject(g, () => g.UseFieldCode(0, FieldZone.Exchange, 0));
        }),
        ("Jessica: un golpe definitivo termina antes del robo", () =>
        {
            var (g, rng) = Fixture(); var other = g.State.Players[1]; other.Health = 0; other.HasReachedZero = true;
            rng.EnqueueDie(6); Check(g.UseFieldCode(0, FieldZone.Bonus).Success && g.State.IsOver && g.State.Winner == 0 && g.State.Active.Hand.Count == 0);
        }),
        ("Jessica: mover el origen durante el dado no permite que Error retire una copia de reemplazo", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active;
            p.Exchange.Add(CardCatalog.Attack.Id); p.Hand.AddRange([CardCatalog.PlumberStar.Id, CardCatalog.Sniper.Id]);
            p.Deck.Insert(0, CardCatalog.Jessica.Id); rng.EnqueueDie(4);
            Check(g.UseFieldCode(0, FieldZone.Bonus).Success && g.State.Phase == Phase.DieRerollChoice);
            Check(g.PlayCode(0, 0).Success && g.ResolveTurnBuffDiscard(0, 0).Success && g.ResolveSniper(0, false).Success);
            Check(g.State.Phase == Phase.DieRerollChoice && p.Hand.Contains(CardCatalog.Jessica.Id) && !p.BonusEffectUsed);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active; g.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
            Reject(g, () => g.PlayCode(1, 0));
            Check(g.ResolveDieReroll(0, false).Success && g.State.Phase == Phase.Combat && p.Bonus == CardCatalog.Jessica.Id);
            Check(p.Hand.Contains(CardCatalog.Jessica.Id) && !p.BonusEffectUsed && g.FieldCombatTargets(0).Count() == 1);
            rng.EnqueueDie(5); Check(g.UseFieldCode(0, FieldZone.Bonus).Success && g.State.Phase == Phase.ErrorResponse);
            Check(g.PassErrorResponse(1).Success && g.State.Players[1].Health == 4);
        })
    };

    static (GameEngine, ScriptedRandom) Fixture(int actor = 0, FieldZone zone = FieldZone.Bonus)
    {
        var rng = new ScriptedRandom(); var g = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Jugador 1", "Jugador 2"], StartingSeat = actor, Hands = [[], []],
            Decks = [Enumerable.Repeat("attack", 30).ToList(), Enumerable.Repeat("attack", 30).ToList()], Bonuses = [null, null]
        }, rng);
        foreach (var player in g.State.Players) player.Dexterity = 0;
        if (zone == FieldZone.Bonus) { g.State.Active.Bonus = CardCatalog.Jessica.Id; g.State.Active.BonusPlacedTurn = g.State.Turn; }
        else { g.State.Active.Exchange.Add(CardCatalog.Jessica.Id); g.State.Active.ExchangePlacementTurns = new() { [0] = g.State.Turn }; }
        return (g, rng);
    }
    static int Count(GameEngine g) => g.State.Players.Sum(p => p.Deck.Count + p.Hand.Count + p.Memory.Count + p.CorruptedMemory.Count + p.Exchange.Count + (p.Bonus == null ? 0 : 1));
    static void Reject(GameEngine g, Func<CommandResult> action)
    {
        var before = g.SaveSnapshot(); Check(!action().Success && before == g.SaveSnapshot());
    }
    static void Check(bool value) { if (!value) throw new Exception("Regla de Jessica Lee incorrecta."); }
}
