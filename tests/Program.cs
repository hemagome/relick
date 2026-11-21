using Relick.Core;

var tests = new (string Name, Action Run)[]
{
    ("Preparación: habilidad + cuatro cartas, mazo conservado", () =>
    {
        var g = TestGame.Create(8);
        foreach (var p in g.State.Players)
        {
            Eq(p.Hand.Count, 5);
            Eq(p.Hand[0], "magnet-shuffle");
            Eq(Count(p), CardCatalog.TrainingDeck().Count);
        }
        Ok(!g.Advance(0).Success);
    }),
    ("Mulligan único, bonus y dado de inicio", () =>
    {
        var g = TestGame.Create(15);
        var old = g.State.Players[0].Hand.Take(2).ToArray();
        Ok(g.Start(new[] { 0, 1, 1 }).Success);
        Ok(!g.Start(Array.Empty<int>()).Success);
        foreach (var p in g.State.Players) { Eq(Count(p), CardCatalog.TrainingDeck().Count); Ok(p.Bonus != null); }
        Ok(g.State.Players[0].Deck.TakeLast(2).Order().SequenceEqual(old.Order()));
        Eq(g.State.Turn, 1);
    }),
    ("Mulligan inválido no modifica la preparación", () =>
    {
        var g = TestGame.Create(15);
        var before = Fingerprint(g);
        Ok(!g.Start(new[] { 99 }).Success);
        Eq(Fingerprint(g), before);
    }),
    ("D6: fallos, mitad redondeada, normal y crítico", () =>
    {
        for (var die = 1; die <= 3; die++) Eq(GameEngine.DamageFor(die, 3, 5), 0);
        Eq(GameEngine.DamageFor(4, 3, 5), 2);
        Eq(GameEngine.DamageFor(5, 3, 5), 3);
        Eq(GameEngine.DamageFor(6, 3, 5), 5);
    }),
    ("No se puede atacar sin enseñar una habilidad", () =>
    {
        var (g, _) = Combat();
        g.State.Active.Skills.Clear();
        var before = Fingerprint(g);
        Ok(!AttackKeepingRoll(g,0, 0, 0).Success);
        Eq(Fingerprint(g), before);
    }),
    ("Propietario, índice y etapa se validan antes de mutar", () =>
    {
        var (g, _) = Combat();
        var before = Fingerprint(g);
        Ok(!AttackKeepingRoll(g,1, 0, 0).Success);
        Ok(!AttackKeepingRoll(g,0, -1, 0).Success);
        Ok(!g.PlayCode(0, 0).Success);
        Ok(!g.Exchange(0, 0).Success);
        Ok(!g.Discard(0, 0).Success);
        Eq(Fingerprint(g), before);
    }),
    ("Primera habilidad gratis; segunda paga 300 PR", () =>
    {
        var (g, _) = Combat();
        var p = g.State.Active;
        p.Skills.Clear();
        p.Hand.Clear(); p.Hand.AddRange(new[] { "magnet-shuffle", "destiny-shuffle" });
        Ok(g.Learn(0, 0).Success); Eq(p.Relikia, 0);
        Ok(!g.Learn(0, 0).Success);
        g.State.LearnedThisTurn = false;
        Ok(!g.Learn(0, 0).Success);
        p.Relikia = 300;
        Ok(g.Learn(0, 0).Success); Eq(p.Relikia, 0); Eq(p.Skills.Count, 2);
    }),
    ("Se pueden enseñar copias repetidas de una habilidad", () =>
    {
        var (g, _) = Combat();
        g.State.Active.Hand.Add("destiny-shuffle"); g.State.Active.Relikia = 300;
        Ok(g.Learn(0, 1).Success); Eq(g.State.Active.Skills.Count, 2);
    }),
    ("Se puede enseñar una habilidad después de atacar", () =>
    {
        var (g, rng) = Combat();
        var p = g.State.Active;
        p.Hand.Add("destiny-shuffle"); p.Relikia = 300;
        rng.EnqueueDie(1); Ok(AttackKeepingRoll(g,0, 0, 0).Success);
        Eq(g.State.Phase, Phase.Combat); Ok(g.State.AttackedThisTurn);
        var learn = DuelOptions.For(g, 0).Single(o => o.Move.Kind == DuelMoveKind.Learn);
        Eq(learn.Move.Index, 0);
        Ok(g.Learn(0, 0).Success); Eq(p.Skills.Count, 2); Eq(p.Relikia, 0);
        Ok(!g.Learn(0, 0).Success);
    }),
    ("Cuatro habilidades: 0, 300, 500 y 1000 PR, sin permitir una quinta", () =>
    {
        var (g, _) = Combat();
        var p = g.State.Active;
        p.Skills.Clear(); p.Hand.Clear();
        p.Hand.AddRange(new[] { "magnet-shuffle", "destiny-shuffle", "magnet-shuffle", "destiny-shuffle" });
        p.Relikia = 1800;
        var balance = 1800;
        var costs = new[] { 0, 300, 500, 1000 };
        Ok(GameEngine.SkillSlotCosts.SequenceEqual(costs));
        for (var i = 0; i < costs.Length; i++)
        {
            g.State.LearnedThisTurn = false;
            Eq(g.SkillCost(p), costs[i]);
            if (costs[i] > 0)
            {
                p.Relikia = costs[i] - 1;
                var before = Fingerprint(g);
                Ok(!g.Learn(0, 0).Success); Eq(Fingerprint(g), before);
                p.Relikia = balance;
            }
            Ok(g.Learn(0, 0).Success);
            balance -= costs[i];
            Eq(p.Relikia, balance); Eq(p.Skills.Count, i + 1);
        }
        g.State.LearnedThisTurn = false;
        p.Hand.Add("magnet-shuffle"); p.Relikia = 2000;
        var full = Fingerprint(g);
        var rejected = g.Learn(0, 0);
        Ok(!rejected.Success && rejected.Message.Contains("cuatro")); Eq(Fingerprint(g), full);
    }),
    ("El mazo conserva 42 cartas y contiene las tres habilidades y los equipos", () =>
    {
        var deck = CardCatalog.TrainingDeck();
        Eq(deck.Count, 42);
        var skills = deck.Where(id => CardCatalog.Get(id).Kind == CardKind.Skill).ToArray();
        Eq(skills.Length, 3); Eq(skills.Distinct().Count(), 3);
        Eq(skills.Count(id => id == CardCatalog.Magnet.Id), 1); Eq(skills.Count(id => id == CardCatalog.Destiny.Id), 1);
    }),
    ("El rival puede enseñar en los espacios de 500 y 1000 PR", () =>
    {
        var (g, _) = Combat();
        var p = g.State.Active;
        p.Skills.Clear(); p.Skills.AddRange(new[] { "magnet-shuffle", "destiny-shuffle" });
        p.Hand.Clear(); p.Hand.AddRange(new[] { "magnet-shuffle", "destiny-shuffle" });
        p.Relikia = 500;
        Ok(SimulationDriver.Step(g, 0).Success); Eq(p.Skills.Count, 3); Eq(p.Relikia, 0);
        g.State.LearnedThisTurn = false; p.Relikia = 1000;
        Ok(SimulationDriver.Step(g, 0).Success); Eq(p.Skills.Count, 4); Eq(p.Relikia, 0);
    }),
    ("Un ataque por turno y premio solo cuando acierta", () =>
    {
        var (g, rng) = Combat();
        g.State.Active.Hand.Add("attack");
        rng.EnqueueDie(3);
        Ok(AttackKeepingRoll(g,0, 0, 0).Success); Eq(g.State.Active.Relikia, 0);
        Eq(g.State.Players[1].Health, 8);
        Ok(!AttackKeepingRoll(g,0, 0, 0).Success);
        g.State.AttackedThisTurn = false; rng.EnqueueDie(5);
        Ok(AttackKeepingRoll(g,0, 0, 0).Success); Eq(g.State.Active.Relikia, 600);
        Eq(g.State.Players[1].Health, 5);
    }),
    ("El dado notifica su resultado real sin consumir azar adicional", () =>
    {
        var (g, rng) = Combat();
        var rolls = new List<int>();
        g.DieRolled += rolls.Add;
        Ok(!AttackKeepingRoll(g,1, 0, 0).Success); Eq(rolls.Count, 0);
        rng.EnqueueDie(6); Ok(AttackKeepingRoll(g,0, 0, 0).Success);
        Eq(rolls.Single(), 6); Eq(g.State.LastDie, 6);
        Eq(g.State.Players[1].Health, 3);
        Ok(!AttackKeepingRoll(g,0, 0, 0).Success); Eq(rolls.Count, 1);
        var startRandom = new ScriptedRandom();
        var initial = TestGame.Create(startRandom);
        var startRolls = new List<int>();
        initial.DieRolled += startRolls.Add;
        startRandom.EnqueueDie(6); startRandom.EnqueueDie(1);
        Ok(initial.Start(Array.Empty<int>()).Success);
        Ok(startRolls.SequenceEqual(new[] { 6, 1 })); Eq(initial.State.CurrentPlayer, 0);
        Eq(initial.State.LastDie, 1);
    }),
    ("Eventos visuales: solo ataques exitosos definitivos animan; fallos y rechazos no emiten", () =>
    {
        var (g, rng) = Combat();
        var attacks = new List<int>();
        var hits = new List<(int Target, int Amount)>();
        g.AttackPerformed += attacks.Add;
        g.DamageReceived += (target, amount, _) => hits.Add((target, amount));
        Ok(!AttackKeepingRoll(g,1, 0, 0).Success); Eq(attacks.Count, 0); Eq(hits.Count, 0);
        rng.EnqueueDie(2); Ok(AttackKeepingRoll(g,0, 0, 0).Success);
        Eq(attacks.Count, 0); Eq(hits.Count, 0);
        Ok(!AttackKeepingRoll(g,0, 0, 0).Success); Eq(attacks.Count, 0);
        g.State.AttackedThisTurn = false; g.State.Active.Hand.Add("attack");
        rng.EnqueueDie(5); Ok(AttackKeepingRoll(g,0, 0, 0).Success);
        Eq(attacks.Count, 1); Eq(attacks.Single(), 0); Eq(hits.Single(), (1, 3));
        g.State.Players[1].Health = 0;
        g.State.AttackedThisTurn = false; g.State.Active.Hand.Add("attack");
        rng.EnqueueDie(5); Ok(AttackKeepingRoll(g,0, 0, 0).Success);
        Eq(hits.Count, 2); Ok(g.State.IsOver);
    }),
    ("Llegar a cero exige un golpe posterior, incluso con daño excedente", () =>
    {
        var (g, rng) = Combat();
        g.State.Players[1].Health = 1;
        rng.EnqueueDie(6); Ok(AttackKeepingRoll(g,0, 0, 0).Success);
        Eq(g.State.Players[1].Health, 0); Ok(!g.State.IsOver);
        g.State.AttackedThisTurn = false; g.State.Active.Hand.Add("attack");
        rng.EnqueueDie(4); Ok(AttackKeepingRoll(g,0, 0, 0).Success);
        Eq(g.State.Winner, 0); Ok(g.State.IsOver);
        Ok(!g.Advance(0).Success);
    }),
    ("Curarse tras cero mantiene la derrota en la siguiente llegada a cero", () =>
    {
        var (g, rng) = Combat();
        g.State.Players[1].Health = 1;
        rng.EnqueueDie(5); AttackKeepingRoll(g,0, 0, 0);
        g.State.CurrentPlayer = 1; g.State.Phase = Phase.Actions; g.State.ActionsLeft = 2;
        g.State.Active.Hand.Clear(); g.State.Active.Hand.Add("test-recover");
        Ok(g.PlayCode(1, 0).Success); Eq(g.State.Active.Health, 2);
        g.State.CurrentPlayer = 0; g.State.Phase = Phase.Combat; g.State.AttackedThisTurn = false;
        g.State.Active.Hand.Add("attack"); rng.EnqueueDie(5); AttackKeepingRoll(g,0, 0, 0);
        Eq(g.State.Winner, 0);
    }),
    ("Dos acciones como máximo; curación respeta el máximo", () =>
    {
        var (g, _) = Combat(); g.Advance(0);
        var p = g.State.Active;
        p.Health = 7; p.Hand.Clear(); p.Hand.AddRange(new[] { "test-recover", "test-recover", "test-recover" });
        Ok(g.PlayCode(0, 0).Success); Eq(p.Health, 8);
        Ok(g.PlayCode(0, 0).Success); Eq(g.State.ActionsLeft, 0);
        var before = Fingerprint(g); Ok(!g.PlayCode(0, 0).Success); Eq(Fingerprint(g), before);
    }),
    ("Canjes sucesivos: Bonus a mano y siguiente carta del mazo para ambos jugadores", () =>
    {
        foreach (var actor in new[] { 0, 1 })
        {
            var (g, _) = Combat();
            g.State.CurrentPlayer = actor; g.State.Phase = Phase.Actions; g.State.ActionsLeft = 2;
            var p = g.State.Active;
            p.Hand.Clear(); p.Hand.AddRange(new[] { "attack", "test-recover", "attack", "test-recover" }); p.Bonus = "destiny-shuffle";
            p.Deck.Clear(); p.Deck.AddRange(new[] { "senshuni", "magnet-shuffle", "attack" });
            var count = Count(p);
            Ok(g.Exchange(actor, 0).Success); Eq(p.Exchange.Count, 1);
            Eq(p.Bonus, "destiny-shuffle"); Eq(p.Deck[0], "senshuni");
            Ok(g.Exchange(actor, 0).Success); Eq(p.Exchange.Count, 0);
            Eq(p.Memory.Count, 2); Eq(p.Hand.Last(), "destiny-shuffle"); Eq(p.Bonus, "senshuni");
            Ok(p.Deck.SequenceEqual(new[] { "magnet-shuffle", "attack" })); Eq(Count(p), count);
            var before = Fingerprint(g);
            Ok(!g.Exchange(actor, 0).Success); Eq(Fingerprint(g), before);
            g.State.ActionsLeft = 2;
            Ok(g.Exchange(actor, 0).Success); Ok(g.Exchange(actor, 0).Success);
            Ok(p.Hand.SequenceEqual(new[] { "destiny-shuffle", "senshuni" })); Eq(p.Bonus, "magnet-shuffle");
            Eq(p.Memory.Count, 4); Eq(p.Exchange.Count, 0); Eq(p.Deck.Single(), "attack"); Eq(Count(p), count);
        }
    }),
    ("Reponer Bonus con la última carta activa el reciclaje y conserva cartas", () =>
    {
        var (g, rng) = Combat(); g.Advance(0);
        var p = g.State.Active;
        p.Hand.Clear(); p.Hand.AddRange(new[] { "attack", "test-recover" }); p.Bonus = "destiny-shuffle";
        p.Deck.Clear(); p.Deck.Add("senshuni");
        var count = Count(p); rng.EnqueueDie(3);
        Ok(g.Exchange(0, 0).Success); Ok(g.Exchange(0, 0).Success);
        Ok(g.ResolveRecycling(0, [0, 1]).Success);
        Eq(p.Bonus, "senshuni"); Eq(p.Hand.Single(), "destiny-shuffle"); Ok(p.Recycled);
        Eq(p.Deck.Count, 2); Eq(p.Memory.Count, 0); Eq(Count(p), count); Ok(!g.State.IsOver);
    }),
    ("Reponer Bonus con la última carta respeta el segundo agotamiento", () =>
    {
        var (g, _) = Combat(); g.Advance(0);
        var p = g.State.Active;
        p.Hand.Clear(); p.Hand.AddRange(new[] { "attack", "test-recover" }); p.Bonus = "destiny-shuffle";
        p.Deck.Clear(); p.Deck.Add("senshuni"); p.Recycled = true;
        var count = Count(p);
        Ok(g.Exchange(0, 0).Success); Ok(g.Exchange(0, 0).Success);
        Eq(p.Bonus, "senshuni"); Eq(p.Hand.Single(), "destiny-shuffle"); Eq(p.Health, 6);
        Ok(g.State.IsOver); Eq(g.State.Winner, 1); Eq(Count(p), count);
    }),
    ("Límite de mano: ambos jugadores eligen descartes antes de pasar el turno", () =>
    {
        foreach (var actor in new[] { 0, 1 })
        foreach (var size in new[] { 8, 20, 100 })
        {
            var (g, _) = Combat();
            g.State.CurrentPlayer = actor; g.State.Phase = Phase.Actions;
            var p = g.State.Active;
            p.Hand.Clear(); p.Hand.AddRange(Enumerable.Repeat("attack", size));
            var memoryCount = p.Memory.Count;
            var rivalHand = g.State.Players[1 - actor].Hand.Count;
            Ok(g.Advance(actor).Success);
            Eq(g.State.Phase, Phase.Final); Eq(g.State.CurrentPlayer, actor);
            Eq(g.State.Players[1 - actor].Hand.Count, rivalHand);
            var before = Fingerprint(g);
            Ok(!g.Advance(actor).Success && !g.Discard(1 - actor, 0).Success && !g.Discard(actor, size).Success);
            Eq(Fingerprint(g), before);
            while (p.Hand.Count > GameEngine.MaxHandSize)
            {
                Ok(g.HasAvailablePlay(actor));
                Ok(g.Discard(actor, p.Hand.Count - 1).Success);
            }
            Eq(p.Hand.Count, 7); Eq(p.Memory.Count, memoryCount + size - 7);
            Ok(g.State.Phase is Phase.Combat or Phase.Draw); Eq(g.State.CurrentPlayer, 1 - actor);
            Eq(g.State.DiscardsRemaining, 0);
        }
    }),
    ("Sin efecto pendiente no se permite descartar voluntariamente", () =>
    {
        var (g, _) = Combat();
        var before = Fingerprint(g);
        Ok(!g.Discard(0, 0).Success); Eq(Fingerprint(g), before);
        g.State.Phase = Phase.Discard;
        before = Fingerprint(g);
        Ok(!g.Discard(0, 0).Success); Eq(Fingerprint(g), before);
    }),
    ("Descarte por efecto: cantidad exacta y regreso a la misma etapa", () =>
    {
        var (g, _) = Combat();
        var p = g.State.Active;
        p.Hand.Clear(); p.Hand.AddRange(Enumerable.Repeat("attack", 12));
        // Simulate an explicit card-effect request, independently of hand size.
        g.State.Phase = Phase.Discard; g.State.DiscardsRemaining = 2; g.State.DiscardResumePhase = Phase.Combat;
        var before = Fingerprint(g);
        Ok(!g.Discard(1, 0).Success); Ok(!g.Discard(0, 99).Success); Ok(!g.Advance(0).Success);
        Eq(Fingerprint(g), before);
        Ok(g.Discard(0, 3).Success); Eq(p.Hand.Count, 11); Eq(g.State.DiscardsRemaining, 1);
        Eq(g.State.Phase, Phase.Discard);
        Ok(g.Discard(0, 0).Success); Eq(p.Hand.Count, 10); Eq(p.Memory.Count, 2);
        Eq(g.State.CurrentPlayer, 0); Eq(g.State.Phase, Phase.Combat); Eq(g.State.DiscardsRemaining, 0);
    }),
    ("Recarga permite superar siete durante acciones y exige descartar al terminar", () =>
    {
        var (g, _) = Combat(); g.Advance(0);
        var p = g.State.Active;
        p.Hand.Clear(); p.Hand.Add("senshuni"); p.Hand.AddRange(Enumerable.Repeat("attack", 8));
        Ok(g.PlayCode(0, 0).Success); Eq(p.Hand.Count, 10);
        Ok(g.Advance(0).Success); Eq(p.Hand.Count, 10); Eq(g.State.Phase, Phase.Final); Eq(g.State.CurrentPlayer, 0);
        for (var i = 0; i < 3; i++) Ok(g.Discard(0, 0).Success);
        Eq(p.Hand.Count, 7); Eq(g.State.CurrentPlayer, 1);
    }),
    ("Mano vacía roba tres, mano no vacía roba una", () =>
    {
        var (g, _) = Combat();
        g.State.Players[1].Hand.Clear();
        g.Advance(0); g.Advance(0);
        Eq(g.State.Players[1].Hand.Count, 3);
        if (g.State.Phase == Phase.Draw) Ok(g.Advance(1).Success);
        g.Advance(1); g.Advance(1);
        Eq(g.State.Players[0].Hand.Count, 2);
    }),
    ("Primer agotamiento elige cartas de la memoria y continúa el robo", () =>
    {
        var (g, rng) = Combat();
        var p = g.State.Players[1];
        p.Hand.Clear(); p.Deck.Clear(); p.Deck.Add("attack");
        p.Memory.Clear(); p.Memory.AddRange(new[] { "test-recover", "senshuni", "attack", "attack" });
        var before = Count(p); rng.EnqueueDie(3);
        g.Advance(0); g.Advance(0);
        Eq(g.State.Phase, Phase.RecycleChoice); Eq(p.Hand.Count, 1);
        Ok(g.ResolveRecycling(1, [0, 2, 3]).Success);
        Ok(p.Recycled); Eq(p.Hand.Count, 3); Eq(p.Deck.Count, 1); Eq(p.Memory.Count, 1);
        Eq(Count(p), before);
    }),
    ("Segundo agotamiento resta dos vidas y compara; empate permitido", () =>
    {
        var (g, _) = Combat();
        var p = g.State.Players[1]; p.Hand.Clear(); p.Hand.Add("magnet-shuffle");
        p.Deck.Clear(); p.Deck.Add("attack"); p.Recycled = true;
        p.Health = 8; g.State.Players[0].Health = 6;
        g.Advance(0); g.Advance(0);
        Eq(p.Health, 6); Eq(g.State.Winner, -1); Ok(g.State.IsOver);
    }),
    ("Agotamiento sin memoria termina sin bloquearse", () =>
    {
        var (g, _) = Combat();
        var p = g.State.Players[1]; p.Deck.Clear(); p.Memory.Clear();
        g.Advance(0); g.Advance(0); Ok(g.State.IsOver);
    }),
    ("Recarga interrumpe robos adicionales si termina el duelo", () =>
    {
        var (g, _) = Combat(); g.Advance(0);
        var p = g.State.Active; p.Hand.Clear(); p.Hand.Add("senshuni");
        p.Deck.Clear(); p.Deck.Add("attack"); p.Recycled = true;
        Ok(g.PlayCode(0, 0).Success); Eq(p.Hand.Count, 1); Ok(g.State.IsOver);
    }),
    ("Relikia: costes, acciones y restricciones sin mutar ante rechazos", () =>
    {
        Ok(RelikiaEffects.All.Select(e => e.Cost).SequenceEqual(new[] { 100, 300, 400, 500 }));
        foreach (var effect in RelikiaEffects.All)
        {
            var (g, _) = Actions(); var p = g.State.Active;
            p.Relikia = effect.Cost - 1; p.Memory.Add("test-recover");
            var before = Fingerprint(g);
            Ok(!g.UseRelikiaEffect(0, effect.Id, 0).Success); Eq(Fingerprint(g), before);
            p.Relikia = 1000; g.State.ActionsLeft = 0; before = Fingerprint(g);
            Ok(!g.UseRelikiaEffect(0, effect.Id, 0).Success); Eq(Fingerprint(g), before);
            g.State.ActionsLeft = 2; before = Fingerprint(g);
            Ok(!g.UseRelikiaEffect(1, effect.Id, 0).Success); Eq(Fingerprint(g), before);
        }
        var (replay, _) = Actions(); replay.State.ActionsLeft = 1; replay.State.Active.Memory.Add("test-recover");
        var snapshot = Fingerprint(replay); Ok(!replay.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success); Eq(Fingerprint(replay), snapshot);
    }),
    ("100 PR mueve Bonus a canje, repone y exige canje vacío", () =>
    {
        var (g, _) = Actions(); var p = g.State.Active; var count = Count(p); var top = p.Deck[0];
        Ok(g.UseRelikiaEffect(0, RelikiaEffect.ChangeBonus).Success);
        Eq(p.Exchange.Single(), "destiny-shuffle"); Eq(p.Bonus, top); Eq(p.Relikia, 900); Eq(g.State.ActionsLeft, 1); Eq(Count(p), count);
        var before = Fingerprint(g); Ok(!g.UseRelikiaEffect(0, RelikiaEffect.ChangeBonus).Success); Eq(Fingerprint(g), before);
        Ok(g.Exchange(0, 0).Success); Eq(p.Exchange.Count, 0); Ok(p.Hand.Contains(top)); Eq(Count(p), count);
    }),
    ("300 PR roba y permite escoger carta al fondo o canje sin otra acción", () =>
    {
        foreach (var destination in new[] { CardDestination.DeckBottom, CardDestination.Exchange })
        {
            var (g, _) = Actions(); var p = g.State.Active;
            p.Hand.Clear(); p.Hand.Add("test-recover"); p.Exchange.Add("attack"); var count = Count(p);
            var drawn = p.Deck[0];
            Ok(g.UseRelikiaEffect(0, RelikiaEffect.DrawAndPlace).Success);
            Eq(p.Hand.Last(), drawn); Eq(g.State.PendingRelikia, RelikiaChoice.PlaceHandCard);
            var before = Fingerprint(g);
            Ok(!g.Advance(0).Success); Ok(!g.PlayCode(0, 0).Success); Ok(!g.ResolveRelikiaPlacement(1, 0, destination).Success);
            Ok(!g.ResolveRelikiaPlacement(0, 99, destination).Success); Eq(Fingerprint(g), before);
            Ok(g.ResolveRelikiaPlacement(0, 0, destination).Success);
            Eq(g.State.Phase, Phase.Actions); Eq(g.State.PendingRelikia, null); Eq(g.State.ActionsLeft, 1); Eq(p.Relikia, 700);
            if (destination == CardDestination.DeckBottom) Eq(p.Deck.Last(), "test-recover");
            else { Ok(p.Hand.Contains("destiny-shuffle")); Eq(p.Exchange.Count, 0); Ok(p.Memory.Contains("test-recover")); }
            Eq(Count(p), count);
        }
    }),
    ("400 PR activa código, consume dos acciones y corrompe solo la copia elegida", () =>
    {
        var (g, _) = Actions(); var p = g.State.Active;
        p.Health = 5; p.Memory.Clear(); p.Memory.AddRange(new[] { "attack", "test-recover", "test-recover" });
        var count = Count(p); var before = Fingerprint(g);
        Ok(!g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success); Eq(Fingerprint(g), before);
        Ok(g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 1).Success);
        Eq(p.Health, 7); Eq(p.Relikia, 600); Eq(g.State.ActionsLeft, 0);
        Ok(p.Memory.SequenceEqual(new[] { "attack", "test-recover" })); Eq(p.CorruptedMemory.Single(), "test-recover"); Eq(Count(p), count);
        p.Memory.Clear(); g.State.ActionsLeft = 2; before = Fingerprint(g);
        Ok(!g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success); Eq(Fingerprint(g), before);
    }),
    ("Reciclar Recarga no reutiliza el código corrompido al agotar el mazo", () =>
    {
        var (g, rng) = Actions(); var p = g.State.Active;
        p.Memory.Clear(); p.Memory.AddRange(new[] { "senshuni", "attack", "attack", "attack" });
        p.Deck.Clear(); p.Deck.Add("test-recover"); var count = Count(p); rng.EnqueueDie(3);
        Ok(g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success);
        Ok(g.ResolveRecycling(0, [0, 1, 2]).Success);
        Eq(p.CorruptedMemory.Single(), "senshuni"); Ok(p.Recycled); Eq(p.Hand.Count, 3); Eq(Count(p), count);
        Ok(!p.Hand.Contains("senshuni") && !p.Deck.Contains("senshuni"));
    }),
    ("500 PR: todos los resultados D6, Bonus y elección solo con 5/6", () =>
    {
        for (var die = 1; die <= 6; die++)
        {
            var (g, rng) = Actions(); var p = g.State.Active; var count = Count(p); var rolls = new List<int>(); g.DieRolled += rolls.Add;
            rng.EnqueueDie(die); Ok(g.UseRelikiaEffect(0, RelikiaEffect.ClaimBonus).Success);
            Ok(p.Hand.Contains("destiny-shuffle")); Eq(p.Relikia, 500); Eq(g.State.ActionsLeft, 1); Eq(Count(p), count); Eq(rolls.Single(), die);
            Eq(g.State.Phase, die >= 5 ? Phase.RelikiaChoice : Phase.Actions);
            Eq(g.State.PendingRelikia, die >= 5 ? RelikiaChoice.BonusReward : null);
        }
    }),
    ("500 PR: el rival escoge dos descartes durante el turno del atacante", () =>
    {
        foreach (var actor in new[] { 0, 1 })
        {
            var (g, rng) = Actions(); g.State.CurrentPlayer = actor; var p = g.State.Active; p.Relikia = 1000; p.Bonus = "destiny-shuffle";
            var other = g.State.Players[1 - actor]; other.Hand.Clear(); other.Hand.AddRange(new[] { "attack", "test-recover", "senshuni" });
            var counts = g.State.Players.Select(Count).ToArray(); rng.EnqueueDie(6);
            Ok(g.UseRelikiaEffect(actor, RelikiaEffect.ClaimBonus).Success);
            Ok(g.ChooseRelikiaReward(actor, RelikiaReward.OpponentDiscards).Success);
            Eq(g.State.DecisionPlayer, 1 - actor); Eq(g.State.CurrentPlayer, actor);
            var before = Fingerprint(g); Ok(!g.Discard(actor, 0).Success); Ok(!g.Advance(actor).Success); Eq(Fingerprint(g), before);
            Ok(g.Discard(1 - actor, 1).Success); Eq(other.Memory.Last(), "test-recover");
            Ok(g.Discard(1 - actor, 1).Success); Eq(other.Hand.Single(), "attack");
            Eq(g.State.Phase, Phase.Actions); Eq(g.State.DecisionPlayer, actor); Eq(g.State.ActionsLeft, 1); Eq(g.State.DiscardPlayer, null);
            for (var i = 0; i < 2; i++) Eq(Count(g.State.Players[i]), counts[i]);
        }
    }),
    ("500 PR: corrupción de ambas memorias, objetivos insuficientes y rechazos", () =>
    {
        var (g, rng) = Actions(); var p = g.State.Active; var other = g.State.Players[1];
        p.Memory.Clear(); p.Memory.AddRange(new[] { "test-recover", "attack" }); other.Memory.Clear(); other.Memory.Add("senshuni");
        rng.EnqueueDie(5); g.UseRelikiaEffect(0, RelikiaEffect.ClaimBonus);
        Ok(g.ChooseRelikiaReward(0, RelikiaReward.CorruptCodes).Success); Eq(g.State.CorruptionsRemaining, 2);
        var before = Fingerprint(g); Ok(!g.ResolveRelikiaCorruption(1, 0, 0).Success);
        Ok(!g.ResolveRelikiaCorruption(0, 0, 1).Success); Ok(!g.ResolveRelikiaCorruption(0, 99, 0).Success); Eq(Fingerprint(g), before);
        Ok(g.ResolveRelikiaCorruption(0, 1, 0).Success); Eq(g.State.CorruptionsRemaining, 1);
        Ok(g.ResolveRelikiaCorruption(0, 0, 0).Success); Eq(g.State.Phase, Phase.Actions);
        Eq(p.CorruptedMemory.Single(), "test-recover"); Eq(other.CorruptedMemory.Single(), "senshuni");
        foreach (var reward in new[] { RelikiaReward.CorruptCodes, RelikiaReward.OpponentDiscards })
        foreach (var available in new[] { 0, 1 })
        {
            var (small, random) = Actions(); var rival = small.State.Players[1];
            rival.Hand.Clear(); rival.Memory.Clear(); small.State.Active.Memory.Clear();
            if (available == 1) { rival.Hand.Add("attack"); rival.Memory.Add("test-recover"); }
            random.EnqueueDie(6); small.UseRelikiaEffect(0, RelikiaEffect.ClaimBonus); small.ChooseRelikiaReward(0, reward);
            if (available == 1) Ok(SimulationDriver.Step(small, small.State.DecisionPlayer).Success);
            Eq(small.State.Phase, Phase.Actions);
        }
    }),
    ("Disponibilidad para brillo: habilidades, ataques, acciones y Relikia; consulta sin azar ni mutaciones", () =>
    {
        var (g, rng) = Combat(); var p = g.State.Active;
        Ok(g.HasAvailablePlay(0)); Ok(!g.HasAvailablePlay(1));
        p.Hand.Clear(); p.Hand.Add("destiny-shuffle"); Eq(g.HasAvailablePlay(0), false);
        p.Relikia = 300; Ok(g.HasAvailablePlay(0)); g.State.LearnedThisTurn = true; Ok(!g.HasAvailablePlay(0));
        p.Hand.Add("attack"); Ok(g.HasAvailablePlay(0)); g.State.AttackedThisTurn = true; Ok(!g.HasAvailablePlay(0));
        g.State.LearnedThisTurn = false; Ok(g.HasAvailablePlay(0));
        g.State.LearnedThisTurn = true;
        g.Advance(0); p.Bonus = "destiny-shuffle"; p.Hand.Clear(); p.Relikia = 99; Ok(!g.HasAvailablePlay(0));
        p.Relikia = 100; Ok(g.HasAvailablePlay(0)); p.Exchange.Add("attack"); Ok(!g.HasAvailablePlay(0));
        p.Relikia = 300; Ok(g.HasAvailablePlay(0)); p.Relikia = 0; p.Hand.Add("test-recover"); Ok(g.HasAvailablePlay(0));
        p.Hand.Clear(); p.Relikia = 500; var before = Fingerprint(g); rng.EnqueueDie(6);
        for (var i = 0; i < 20; i++) Ok(g.HasAvailablePlay(0)); Eq(Fingerprint(g), before);
        g.UseRelikiaEffect(0, RelikiaEffect.ClaimBonus); Eq(g.State.LastDie, 6);
        g.ChooseRelikiaReward(0, RelikiaReward.CorruptCodes); g.State.ActionsLeft = 0; Ok(!g.HasAvailablePlay(0));
    }),
    ("Revelación: sigue la copia concreta al cambiar de posición y se pierde al salir de mano", () =>
    {
        foreach (var command in new[] { "learn", "attack", "code", "exchange", "discard", "bottom" })
        {
            var (g, rng) = Combat(); var p = g.State.Active;
            var id = command == "learn" ? "destiny-shuffle" : command == "code" ? "test-recover" : "attack";
            p.Hand.Clear(); p.Hand.AddRange(new[] { "test-recover", id, id, "senshuni" }); p.Relikia = 1000;
            Ok(p.RevealHandCard(2)); Eq(p.RevealedHandCard(2), id); Eq(p.RevealedHandCard(1), null);
            Ok(!p.RevealHandCard(-1) && !p.RevealHandCard(99));
            p.RemoveHandCard(0); Eq(p.RevealedHandCard(1), id); Eq(p.RevealedHandCard(0), null);
            CommandResult result;
            if (command == "learn") result = g.Learn(0, 1);
            else if (command == "attack") { rng.EnqueueDie(1); result = AttackKeepingRoll(g,0, 1, 0); }
            else
            {
                g.Advance(0); p.Bonus = "destiny-shuffle";
                if (command == "code") result = g.PlayCode(0, 1);
                else if (command == "exchange") result = g.Exchange(0, 1);
                else if (command == "discard")
                {
                    g.State.Phase = Phase.Discard; g.State.DiscardsRemaining = 1;
                    result = g.Discard(0, 1);
                }
                else
                {
                    Ok(g.UseRelikiaEffect(0, RelikiaEffect.DrawAndPlace).Success);
                    result = g.ResolveRelikiaPlacement(0, 1, CardDestination.DeckBottom);
                }
            }
            Ok(result.Success, $"{command}: {result.Message}");
            Eq(p.RevealedHandSlots.Count, 0); Ok(p.Hand.Select((_, i) => p.RevealedHandCard(i)).All(c => c == null));
            p.Hand.Add(id); Eq(p.RevealedHandCard(p.Hand.Count - 1), null);
        }
        var (game, _) = Combat(); var player = game.State.Active;
        Ok(player.RevealHandCard(0)); player.HideHandCards(); Eq(player.RevealedHandCard(0), null);
    }),
    ("2000 duelos: terminan, decisiones legales, conservación y límites", () =>
    {
        for (var seed = 0; seed < 2000; seed++)
        {
            var g = TestGame.Create(seed);
            foreach (var p in g.State.Players) p.Dexterity = p.Relikario.StartingDexterity;
            g.Start(Array.Empty<int>());
            var steps = 0;
            while (!g.State.IsOver && steps++ < 1500)
            {
                var result = SimulationDriver.Step(g, g.State.DecisionPlayer);
                Ok(result.Success, $"seed {seed}: {result.Message}");
                foreach (var p in g.State.Players)
                {
                    Eq(g.State.Players.Sum(Count), 2 * CardCatalog.TrainingDeck().Count);
                    Ok(p.Health >= 0 && p.Health <= p.MaxHealth);
                    Ok(p.Relikia >= 0);
                    Ok(p.Skills.Count <= GameEngine.MaxSkills);
                    Ok(p.Exchange.Count <= 1);
                }
                Ok(g.State.ActionsLeft >= 0 && g.State.ActionsLeft <= 2);
            }
            Ok(g.State.IsOver, $"No terminó seed {seed}");
        }
    }),
    ("Misma semilla y decisiones reproducen el duelo", () =>
    {
        var first = Run(180); var second = Run(180); Eq(first, second);
    })
}.Concat(DeckRulesTests.Cases).Concat(AccountClaimsTests.Cases).Concat(DuelOutcomeTests.Cases).Concat(DuelHistoryTests.Cases).Concat(OpeningTurnTests.Cases).Concat(ComboResponseTests.Cases).Concat(SuperAttackTests.Cases).Concat(ZahiraFlairTests.Cases).Concat(CardOwnershipTests.Cases).Concat(SuperMagnetTests.Cases).Concat(GoldenTotemTests.Cases).Concat(TotemTests.Cases).Concat(TimeBenderTests.Cases).Concat(ReloadCartridgeTests.Cases).Concat(QuickLoadTests.Cases).Concat(JessicaTests.Cases).Concat(PlumberStarTests.Cases).Concat(FloppyDiskTests.Cases).Concat(BonfireTests.Cases).Concat(ReverseTests.Cases).Concat(AttackRewardTests.Cases).Concat(DexterityTests.Cases).Concat(HandLimitTests.Cases).Concat(ReplayQueryTests.Cases).Concat(InitialSkillTests.Cases).Concat(SnapshotTests.Cases).Concat(DuelTests.Cases).Concat(SkillTests.Cases).Concat(YunoTests.Cases).Concat(EvolutionTests.Cases).Concat(EquipmentTests.Cases).Concat(PurpleRingTests.Cases).Concat(GalacticShieldTests.Cases).Concat(VictoriousManualControlTests.Cases).Concat(EllieTests.Cases).Concat(RobotizeTests.Cases).Concat(ComboParryTests.Cases).Concat(MetalHoodTests.Cases).Concat(DestinyPickTests.Cases).Concat(DiamondHelmetTests.Cases).Concat(TimeBombTests.Cases).Concat(TakeEquipmentTests.Cases).Concat(EquipmentRaidTests.Cases).Concat(KonamyCodeTests.Cases).Concat(MindSwapTests.Cases).Concat(AgilityTests.Cases).Concat(MindResetTests.Cases).Concat(DodgeTests.Cases).Concat(PcTests.Cases).Concat(SniperTests.Cases).Concat(TripleAttackTests.Cases).Concat(RecyclingTests.Cases).Concat(SenshuniTests.Cases).Concat(CardboardBoxTests.Cases).Concat(GuardTests.Cases).Concat(ExtraLifeTests.Cases).Concat(PointsTests.Cases).Concat(EggComboTests.Cases).Concat(LoadErrorTests.Cases).Concat(CosmeticTests.Cases).Concat(RelikarioCatalogTests.Cases).Concat(OsamuTests.Cases).Concat(IronwingTests.Cases).Concat(DecisionTests.Cases).Concat(NewCardInteractionsTests.Cases).Concat(TurnTriggerTests.Cases).Concat(ProfileTests.Cases).ToArray();

var failures = 0;
foreach (var (name, run) in tests)
{
    try { run(); Console.WriteLine($"PASS {name}"); }
    catch (Exception e) { failures++; Console.WriteLine($"FAIL {name}: {e}"); }
}
Console.WriteLine($"{tests.Length - failures}/{tests.Length} comprobaciones correctas.");
return failures == 0 ? 0 : 1;

static void Ok(bool condition, string message = "Condición no satisfecha")
{
    if (!condition) throw new Exception(message);
}
static void Eq<T>(T actual, T expected)
{
    if (!Equals(actual, expected)) throw new Exception($"Esperado {expected}, recibido {actual}");
}
static int Count(PlayerState p) => p.Deck.Count + p.Hand.Count(id => CardCatalog.Get(id).CodeType != CodeType.Trap) + p.Memory.Count + p.CorruptedMemory.Count + p.Skills.Count +
    p.Exchange.Count(id => CardCatalog.Get(id).CodeType != CodeType.Trap) + p.AttachedCards.Count + (p.Equipment is { } id && CardCatalog.Get(id).CodeType != CodeType.Trap ? p.EquipmentStackedCopies + 1 : 0) +
    (p.EquipmentCovers?.Count ?? 0) + (p.Bonus == null ? 0 : 1);
static string Fingerprint(GameEngine g) => System.Text.Json.JsonSerializer.Serialize(g.State);
static CommandResult AttackKeepingRoll(GameEngine game, int actor, int hand, int skill)
{
    var result = game.Attack(actor, hand, skill);
    if (result.Success && game.State.Phase == Phase.AttackRerollChoice) return game.ResolveAttackReroll(actor, false);
    return result;
}
static (GameEngine, ScriptedRandom) Combat()
{
    var rng = new ScriptedRandom();
    var g = TestGame.Create(rng);
    g.State.Phase = Phase.Combat; g.State.CurrentPlayer = 0; g.State.Turn = 2;
    foreach (var player in g.State.Players) player.Hand.Clear();
    g.State.Active.Hand.Add("attack");
    g.State.Active.Skills.Add("destiny-shuffle");
    return (g, rng);
}
static (GameEngine, ScriptedRandom) Actions()
{
    var (g, rng) = Combat(); g.Advance(0); g.State.Active.Relikia = 1000; g.State.Active.Bonus = "destiny-shuffle";
    return (g, rng);
}
static string Run(int seed)
{
    var g = TestGame.Create(seed); g.Start(Array.Empty<int>());
    for (var i = 0; i < 1500 && !g.State.IsOver; i++) SimulationDriver.Step(g, g.State.DecisionPlayer);
    return Fingerprint(g);
}
sealed class ScriptedRandom : IGameRandom
{
    private readonly SeededRandom _fallback = new(5);
    private readonly Queue<int> _values = new();
    public void EnqueueDie(int die) => _values.Enqueue(die - 1);
    public int Next(int exclusiveMax) => _values.Count > 0 ? _values.Dequeue() % exclusiveMax : _fallback.Next(exclusiveMax);
}
