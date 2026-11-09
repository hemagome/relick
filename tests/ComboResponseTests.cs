using Relick.Core;
using System.Text.Json;

static class ComboResponseTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("COMBO: preguntar por defecto, preferencia privada persistente y sin recursos ni jugadas", () =>
        {
            var (g, rng) = Fixture(); var before = Public(g); var p = g.State.Players[1];
            Check(!p.AutoPassCombos && g.SetComboResponses(1, false, out var resumed).Success && !resumed);
            Check(p.ActionsLeft == 2 && p.Relikia == 0 && before == Public(g));
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            Check(g.State.Players[1].AutoPassCombos && DuelSnapshot.For(g, 1, 1).AutoPassCombos);
            Check(!DuelSnapshot.For(g, 0, 1).AutoPassCombos && !DuelSnapshot.For(g, -1, 1).AutoPassCombos);
            var packet = new DuelBoardPacket { Board = DuelSnapshot.For(g, -1, 1) };
            var own = new DuelPrivateData { MySeat = 1, AutoPassCombos = true };
            Check(DuelPackets.Combine(packet, own, []).AutoPassCombos);
            Check(!Public(g).Contains("AutoPassCombos"));
            Check(g.SetComboResponses(1, true).Success && !g.State.Players[1].AutoPassCombos);
            var snapshot = g.SaveSnapshot();
            Check(!g.SetComboResponses(-1, false).Success && !g.SetComboResponses(2, false).Success && snapshot == g.SaveSnapshot());
        }),
        ("COMBO: pasar no abre Error ni consume la carta; preguntar vuelve a abrir futuras activaciones", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Players[0]; var other = g.State.Players[1];
            p.Hand.AddRange([CardCatalog.Senshuni.Id, CardCatalog.Senshuni.Id]); other.Hand.Add(CardCatalog.LoadError.Id);
            Check(g.SetComboResponses(1, false).Success && g.PlayCode(0, 0).Success && g.State.Phase == Phase.Actions);
            Check(other.Hand.Single() == CardCatalog.LoadError.Id && other.ActionsLeft == 2 && p.Hand.Count == 3);
            Check(g.SetComboResponses(1, true).Success && g.PlayCode(0, 0).Success && g.State.Phase == Phase.ErrorResponse);
            Check(g.PlayCode(1, 0).Success && other.ActionsLeft == 1 && p.ActionsLeft == 1);
        }),
        ("COMBO: apagar durante Error pasa una sola vez y no permite reabrir la activación", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Players[0]; var other = g.State.Players[1];
            p.Hand.Add(CardCatalog.Senshuni.Id); other.Hand.Add(CardCatalog.LoadError.Id);
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.ErrorResponse);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Players[0]; other = g.State.Players[1];
            Check(g.SetComboResponses(1, false, out var resumed).Success && resumed && p.Hand.Count == 2);
            Check(other.Hand.Single() == CardCatalog.LoadError.Id && other.ActionsLeft == 2);
            Check(g.SetComboResponses(1, true).Success && !g.CanPlayLoadError(1) && p.Hand.Count == 2);
        }),
        ("COMBO: pasar incluye contraerrores y conserva los recursos de la carta original anulada", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Players[0]; var other = g.State.Players[1];
            p.Hand.AddRange([CardCatalog.BlueEgg.Id, CardCatalog.LoadError.Id]); p.Relikia = 200;
            other.Hand.Add(CardCatalog.LoadError.Id); other.Equipment = CardCatalog.Pendant.Id;
            Check(g.SetComboResponses(0, false).Success && g.PlayCode(0, 0).Success && g.PlayCode(1, 0).Success);
            Check(g.State.Phase == Phase.Actions && p.ActionsLeft == 2 && p.Relikia == 200 && other.Equipment == CardCatalog.Pendant.Id);
            Check(p.Hand.Single() == CardCatalog.LoadError.Id && other.ActionsLeft == 1);
        }),
        ("COMBO: pasar Esquivar y Estrella evita la espera de defensa sin gastar cartas", () =>
        {
            foreach (var card in new[] { CardCatalog.Dodge.Id, CardCatalog.PlumberStar.Id })
            {
                var (g, rng) = Fixture(); var p = g.State.Players[0]; var target = g.State.Players[1];
                g.State.Phase = Phase.Combat; p.Hand.Add(CardCatalog.Attack.Id); p.Skills.Add(CardCatalog.Destiny.Id);
                target.Hand.AddRange([card, CardCatalog.Senshuni.Id]); Check(g.SetComboResponses(1, false).Success);
                rng.EnqueueDie(5); Check(g.Attack(0, 0, 0).Success && g.State.Phase == Phase.Combat);
                Check(target.Health == 5 && target.Hand.Count == 2 && target.ActionsLeft == 2 && p.Relikia == 600);
            }
        }),
        ("COMBO: apagar durante defensa conserva Caja y Destiny como decisiones manuales", () =>
        {
            foreach (var equipment in new[] { false, true })
            {
                var (g, rng) = Fixture(); var target = g.State.Players[1];
                g.State.Phase = Phase.Combat; g.State.Active.Hand.Add(CardCatalog.Attack.Id); g.State.Active.Skills.Add(CardCatalog.Destiny.Id);
                target.Hand.AddRange([CardCatalog.Dodge.Id, CardCatalog.Attack.Id, CardCatalog.Attack.Id]);
                if (equipment) target.Equipment = CardCatalog.CardboardBox.Id; else target.Skills.Add(CardCatalog.Destiny.Id);
                rng.EnqueueDie(5); Check(g.Attack(0, 0, 0).Success && g.State.Phase == Phase.DefenseChoice);
                Check(g.SetComboResponses(1, false, out var resumed).Success && !resumed && g.State.Phase == Phase.DefenseChoice);
                Check(!DuelOptions.For(g, 1).Any(o => o.Move.Kind == DuelMoveKind.PlayCode));
                Check((equipment ? g.CancelAttackWithBox(1) : g.ResolveDefense(1, 0, [0, 1, 2])).Success && target.Health == 8);
            }
        }),
        ("COMBO: apagar una defensa ofrecida solo por cartas aplica el ataque sin gastar las cartas", () =>
        {
            var (g, rng) = Fixture(); var target = g.State.Players[1];
            g.State.Phase = Phase.Combat; g.State.Active.Hand.Add(CardCatalog.Attack.Id); g.State.Active.Skills.Add(CardCatalog.Destiny.Id);
            target.Hand.Add(CardCatalog.Dodge.Id); rng.EnqueueDie(5);
            Check(g.Attack(0, 0, 0).Success && g.State.Phase == Phase.DefenseChoice);
            Check(g.SetComboResponses(1, false, out var resumed).Success && resumed && target.Health == 5);
            Check(target.Hand.Single() == CardCatalog.Dodge.Id && target.ActionsLeft == 2);
        }),
        ("COMBO: pasar modificadores no abre espera; apagar un dado pendiente conserva el resultado", () =>
        {
            foreach (var late in new[] { false, true })
            {
                var (g, rng) = Fixture(); var p = g.State.Active;
                p.Hand.AddRange([CardCatalog.ExtraLife.Id, CardCatalog.Agility.Id]); p.Health = 1; rng.EnqueueDie(5);
                if (!late) Check(g.SetComboResponses(0, false).Success);
                Check(g.PlayCode(0, 0).Success);
                if (late) Check(g.State.Phase == Phase.DieRerollChoice && g.SetComboResponses(0, false, out var resumed).Success && resumed);
                Check(g.State.Phase == Phase.Actions && p.Health == 4 && p.Hand.Single() == CardCatalog.Agility.Id && p.ActionsLeft == 1);
            }
        }),
        ("COMBO: pasar conserva dados de Yuno y Destreza y permite jugar COMBO en la etapa propia", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Dexterity = 1;
            p.Hand.AddRange([CardCatalog.ExtraLife.Id, CardCatalog.Agility.Id]); p.Health = 1; rng.EnqueueDie(4);
            Check(g.SetComboResponses(0, false).Success && g.PlayCode(0, 0).Success && g.State.Phase == Phase.DieRerollChoice);
            Check(g.CanUseDexterity(0) && !g.PlayCode(0, 0).Success && g.UseDexterity(0).Success);
            if (g.State.Phase == Phase.DieRerollChoice) Check(g.ResolveDieReroll(0, false).Success);
            Check(p.Health == 4);
            p.Hand.Clear(); p.Hand.AddRange([CardCatalog.PlumberStar.Id, CardCatalog.Attack.Id]);
            Check(g.PlayCode(0, 0).Success && g.ResolveTurnBuffDiscard(0, 0).Success && p.StarEffects == 1);
        }),
        ("COMBO: apagar la defensa de Jessica solo por Estrella continúa el daño y conserva las cartas", () =>
        {
            var (g, rng) = Fixture(); var target = g.State.Players[1]; g.State.Phase = Phase.Combat;
            g.State.Active.Bonus = CardCatalog.Jessica.Id;
            target.Hand.AddRange([CardCatalog.PlumberStar.Id, CardCatalog.Attack.Id]); rng.EnqueueDie(5);
            Check(g.UseFieldCode(0, FieldZone.Bonus).Success && g.State.Phase == Phase.EffectDamageChoice);
            Check(g.SetComboResponses(1, false, out var resumed).Success && resumed && target.Health == 6);
            Check(target.Hand.Count == 2 && target.ActionsLeft == 2 && g.State.Phase == Phase.Combat);
        }),
        ("COMBO: pasar no omite relanzamientos de Yuno ni selecciones obligatorias", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active;
            p.Relikario = RelikarioCatalog.YunoLv2; p.AttackRerollUsedThisTurn = false;
            p.Hand.Add(CardCatalog.ExtraLife.Id); p.Health = 1; rng.EnqueueDie(4);
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.DieRerollChoice);
            Check(g.SetComboResponses(0, false, out var resumed).Success && !resumed && g.CanUseYunoOnPendingDie(0));
            Check(g.ResolveDieReroll(0, false).Success && p.Health == 4);
            p.Hand.Add(CardCatalog.FloppyDisk.Id); g.State.Players[1].Memory.Add(CardCatalog.Attack.Id);
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.MemoryRecoveryChoice);
            Check(g.SetComboResponses(0, false, out resumed).Success && !resumed && g.State.Phase == Phase.MemoryRecoveryChoice);
            Check(g.ResolveMemoryRecovery(0, 1, 0).Success);
        }),
        ("COMBO: apagar después de iniciar Estrella no cancela su descarte obligatorio ni su efecto", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; p.Hand.AddRange([CardCatalog.PlumberStar.Id, CardCatalog.Attack.Id]);
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.TurnBuffDiscard);
            Check(g.SetComboResponses(0, false, out var resumed).Success && !resumed && g.State.Phase == Phase.TurnBuffDiscard);
            Check(g.ResolveTurnBuffDiscard(0, 0).Success && p.StarEffects == 1);
        })
    };
    static (GameEngine, ScriptedRandom) Fixture()
    {
        var rng = new ScriptedRandom(); var g = TestGame.Create(rng); g.State.CurrentPlayer = 0; g.State.Phase = Phase.Actions; g.State.Turn = 2;
        foreach (var p in g.State.Players)
        {
            p.Hand.Clear(); p.Memory.Clear(); p.Skills.Clear(); p.Dexterity = 0; p.AttackRerollUsedThisTurn = true; p.Equipment = null;
            p.Deck.Clear(); p.Deck.AddRange(Enumerable.Repeat(CardCatalog.Attack.Id, 30));
        }
        return (g, rng);
    }
    static string Public(GameEngine g) => JsonSerializer.Serialize(DuelSnapshot.For(g, -1, 1), DuelJson.Default.DuelSnapshot);
    static void Check(bool value) { if (!value) throw new Exception("Preferencia COMBO incorrecta."); }
}
