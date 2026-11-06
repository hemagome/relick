namespace Relick.Core;

/// <summary>One legal decision at a time; uses only its own cards and public state.</summary>
public static class PracticeBot
{
    public static CommandResult Step(GameEngine game, int actor)
    {
        var s = game.State;
        if (s.IsOver || s.DecisionPlayer != actor) return CommandResult.Fail("El rival no puede jugar ahora.");
        var p = s.Players[actor];
        if (s.Phase == Phase.ErrorResponse)
        {
            var error = p.Hand.FindIndex(id => CardCatalog.Get(id).CancelActivation);
            return error >= 0 && game.CodeUnavailableReason(actor, error) == null ? game.PlayCode(actor, error) : game.PassErrorResponse(actor);
        }
        if (game.CanEvolve(actor)) return game.Evolve(actor);
        if (s.Phase == Phase.DestroyChoice)
        {
            var target = game.DestructionTargets().OrderBy(t => t.Owner == actor).First();
            return game.ResolveDestruction(actor, target.Owner, target.Zone, target.Index);
        }
        if (s.Phase is Phase.DieRerollChoice or Phase.AttackRerollChoice)
        {
            var attack = s.Phase == Phase.AttackRerollChoice;
            var die = attack ? s.PendingAttackRoll!.Die : s.PendingDieRoll!.Die;
            var combo = p.Hand.FindIndex(id => CardCatalog.Get(id).RerollOwnDie);
            if (die < 4 && combo >= 0 && game.CodeUnavailableReason(actor, combo) == null) return game.PlayCode(actor, combo);
            if (die < 5 && game.CanUseYunoOnPendingDie(actor))
                return attack ? game.ResolveAttackReroll(actor, true) : game.ResolveDieReroll(actor, true);
            if (die < 5 && game.CanUsePendant(actor, PendantEffect.AddTwo)) return game.UsePendant(actor, PendantEffect.AddTwo);
            if (die < 5 && game.CanUsePendant(actor, PendantEffect.AddOne)) return game.UsePendant(actor, PendantEffect.AddOne);
            if (die < 4 && game.CanUsePendant(actor, PendantEffect.Reroll)) return game.UsePendant(actor, PendantEffect.Reroll);
            return attack ? game.ResolveAttackReroll(actor, false) : game.ResolveDieReroll(actor, false);
        }
        if (s.Phase == Phase.PointsChoice) return game.ResolvePointsChange(actor, actor, true);
        if (s.Phase == Phase.CodeResponse) return game.ResolveCodeResponse(actor, true);
        if (s.Phase == Phase.DefenseChoice)
        {
            if (game.CanCancelAttackWithBox(actor)) return game.CancelAttackWithBox(actor);
            var slot = game.AvailableDefenses(actor).FirstOrDefault(-1);
            return slot < 0 ? game.PassDefense(actor) : game.ResolveDefense(actor, slot,
                Enumerable.Range(0, game.DefenseDiscardCost).OrderBy(i => p.Hand[i] == CardCatalog.Attack.Id).ToArray());
        }
        if (s.Phase == Phase.MagnetChoice)
        {
            var rival = s.Players[1 - actor];
            if (rival.Bonus != null) return game.ResolveMagnet(actor, true);
            return rival.Hand.Count > 0 ? game.ResolveMagnet(actor, false, 0) : game.SkipMagnet(actor);
        }
        if (s.Phase == Phase.RelikiaChoice)
        {
            if (s.PendingRelikia == RelikiaChoice.PlaceHandCard)
                return game.ResolveRelikiaPlacement(actor, 0, CardDestination.DeckBottom);
            if (s.PendingRelikia == RelikiaChoice.BonusReward)
                return game.ChooseRelikiaReward(actor, RelikiaReward.OpponentDiscards);
            foreach (var owner in new[] { 1 - actor, actor })
            {
                var index = s.Players[owner].Memory.FindIndex(id => CardCatalog.Get(id).Kind == CardKind.Code);
                if (index >= 0) return game.ResolveRelikiaCorruption(actor, owner, index);
            }
        }
        if (s.Phase == Phase.Combat)
        {
            if (!s.LearnedThisTurn && !s.AttackedThisTurn && p.Skills.Count < GameEngine.MaxSkills)
            {
                var learn = p.Hand.FindIndex(id => CardCatalog.Get(id).Kind == CardKind.Skill);
                if (learn >= 0 && p.Relikia >= game.SkillCost(p)) return game.Learn(actor, learn);
            }
            var attack = p.Hand.IndexOf(CardCatalog.Attack.Id);
            if (!s.AttackedThisTurn && attack >= 0 && p.Skills.Count > 0)
            {
                var best = Enumerable.Range(0, p.Skills.Count)
                    .OrderByDescending(i => CardCatalog.Get(p.Skills[i]).CriticalDamage)
                    .ThenByDescending(i => CardCatalog.Get(p.Skills[i]).NormalDamage)
                    .ThenByDescending(i => CardCatalog.Get(p.Skills[i]).Reward).First();
                return game.Attack(actor, attack, best);
            }
            return game.Advance(actor);
        }
        if (s.Phase == Phase.Actions)
        {
            if (s.ActionsLeft > 0)
            {
                var heal = p.Hand.FindIndex(id => CardCatalog.Get(id).HealFromDie || CardCatalog.Get(id).Heal > 0);
                if (heal >= 0 && p.Health < p.MaxHealth) return game.PlayCode(actor, heal);
                var equipment = p.Hand.FindIndex(id => CardCatalog.Get(id).Kind == CardKind.Equipment);
                if (equipment >= 0 && p.Equipment == null) return game.Equip(actor, equipment);
                var code = Enumerable.Range(0, p.Hand.Count).FirstOrDefault(i => game.CodeUnavailableReason(actor, i) == null && !CardCatalog.Get(p.Hand[i]).HealFromDie && CardCatalog.Get(p.Hand[i]).Heal == 0, -1);
                if (code >= 0) return game.PlayCode(actor, code);
                var redundant = p.Hand.FindIndex(id => p.Skills.Contains(id) ||
                    (CardCatalog.Get(id).Kind == CardKind.Equipment && p.Equipment != null) ||
                    ((CardCatalog.Get(id).HealFromDie || CardCatalog.Get(id).Heal > 0) && p.Health == p.MaxHealth));
                if (redundant >= 0 && p.Bonus != null) return game.Exchange(actor, redundant);
                if (p.Hand.Count == 0 && game.CanUseRelikiaEffect(actor, RelikiaEffect.ClaimBonus))
                    return game.UseRelikiaEffect(actor, RelikiaEffect.ClaimBonus);
                if (p.Hand.Count == 0 && game.CanUseRelikiaEffect(actor, RelikiaEffect.DrawAndPlace))
                    return game.UseRelikiaEffect(actor, RelikiaEffect.DrawAndPlace);
            }
            return game.Advance(actor);
        }
        if (s.Phase == Phase.Discard)
        {
            var index = p.Hand.FindIndex(id => p.Skills.Contains(id));
            if (index < 0) index = p.Hand.FindIndex(id => id != CardCatalog.Attack.Id);
            return game.Discard(actor, index < 0 ? 0 : index);
        }
        return CommandResult.Fail("Etapa sin decisión.");
    }
}
