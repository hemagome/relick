namespace Relick.Core;

public static class DuelOptions
{
    public static List<DuelOption> For(GameEngine game, int actor)
    {
        var result = new List<DuelOption>();
        var s = game.State;
        if (s.IsOver || s.Phase == Phase.Preparation) return result;
        var p = s.Players[actor];
        void Add(DuelMoveKind kind, string label, int index = -1, int value = 0, int target = 0, bool flag = false, int discards = 0)
            => result.Add(new DuelOption { Label = label, RequiredDiscards = discards,
                Move = new DuelMove { Kind = kind, Index = index, Value = value, Target = target, Flag = flag } });
        string Card(string id) => CardCatalog.Get(id).Name;
        // COMBO may be legal during the other player's turn.
        for (var i = 0; i < p.Hand.Count; i++)
            if (game.CodeUnavailableReason(actor, i) == null)
                Add(DuelMoveKind.PlayCode, $"Jugar {Card(p.Hand[i])} · carta {i + 1}", i);
        if (game.CanEvolve(actor)) Add(DuelMoveKind.Evolve, "Evolucionar a Yuno Lv 2 · 1800 PR");
        if (s.DecisionPlayer == actor)
        {
            switch (s.Phase)
            {
                case Phase.Combat:
                    for (var i = 0; i < p.Hand.Count; i++)
                    {
                        var card = CardCatalog.Get(p.Hand[i]);
                        if (card.Kind == CardKind.Skill && !s.LearnedThisTurn && !s.AttackedThisTurn && p.Skills.Count < GameEngine.MaxSkills && p.Relikia >= game.SkillCost(p))
                            Add(DuelMoveKind.Learn, $"Enseñar {card.Name} · {game.SkillCost(p)} PR · carta {i + 1}", i);
                        if (card.Kind == CardKind.Attack && !s.AttackedThisTurn)
                            for (var skill = 0; skill < p.Skills.Count; skill++)
                                Add(DuelMoveKind.Attack, $"Atacar con {Card(p.Skills[skill])} · espacio {skill + 1} · carta {i + 1}", i, skill);
                    }
                    Add(DuelMoveKind.Advance, "Ir a acciones");
                    break;
                case Phase.Actions:
                    if (p.ActionsLeft > 0)
                        for (var i = 0; i < p.Hand.Count; i++)
                        {
                            if (CardCatalog.Get(p.Hand[i]).Kind == CardKind.Equipment)
                                Add(DuelMoveKind.Equip, $"Equipar {Card(p.Hand[i])} · carta {i + 1}", i);
                            if (p.Bonus != null) Add(DuelMoveKind.Exchange, $"Canjear {Card(p.Hand[i])} · carta {i + 1}", i);
                        }
                    foreach (var effect in RelikiaEffects.All)
                    {
                        if (effect.Id == RelikiaEffect.ReplayCode)
                        {
                            foreach (var i in game.ReplayableMemoryCodes(actor))
                                Add(DuelMoveKind.Relikia, $"Reciclar {Card(p.Memory[i])} · memoria {i + 1}", i, (int)effect.Id);
                        }
                        else if (game.CanUseRelikiaEffect(actor, effect.Id)) Add(DuelMoveKind.Relikia, $"{effect.Name} · {effect.Cost} PR", value: (int)effect.Id);
                    }
                    Add(DuelMoveKind.Advance, "Terminar turno");
                    break;
                case Phase.Discard:
                    for (var i = 0; i < p.Hand.Count; i++) Add(DuelMoveKind.Discard, $"Descartar {Card(p.Hand[i])} · carta {i + 1}", i);
                    break;
                case Phase.RecycleChoice:
                    Add(DuelMoveKind.Recycle, $"Reciclar {game.RecyclingCount} cartas de tu memoria", discards: game.RecyclingCount);
                    break;
                case Phase.DeckSearchChoice:
                    foreach (var index in game.SearchableDeckCards(actor))
                    {
                        Add(DuelMoveKind.SearchDeck, $"Añadir {Card(p.Deck[index])} · mazo {index + 1}", index);
                        result[^1].CardId = p.Deck[index];
                    }
                    break;
                case Phase.AttackRerollChoice:
                case Phase.DieRerollChoice:
                    var kind = s.Phase == Phase.AttackRerollChoice ? DuelMoveKind.AttackDie : DuelMoveKind.EffectDie;
                    Add(kind, "Conservar el dado");
                    if (game.CanUseYunoOnPendingDie(actor)) Add(kind, "Relanzar con Yuno", flag: true);
                    foreach (var effect in Enum.GetValues<PendantEffect>().Where(e => game.CanUsePendant(actor, e)))
                        Add(DuelMoveKind.Pendant, "Usar Colgante: " + (effect == PendantEffect.Reroll ? "relanzar" : effect == PendantEffect.AddOne ? "+1" : "+2"), value: (int)effect);
                    break;
                case Phase.DefenseChoice:
                    Add(DuelMoveKind.PassDefense, "Recibir el daño");
                    if (game.CanCancelAttackWithBox(actor)) Add(DuelMoveKind.BoxDefense, "Cancelar daño con Caja de Cartón");
                    foreach (var slot in game.AvailableDefenses(actor))
                        Add(DuelMoveKind.DestinyDefense, $"Defender con Destiny · espacio {slot + 1} · selecciona {game.DefenseDiscardCost} cartas", slot, discards: game.DefenseDiscardCost);
                    break;
                case Phase.MagnetChoice:
                    Add(DuelMoveKind.SkipMagnet, "No tomar ninguna carta");
                    var opponent = s.Players[1 - actor];
                    if (opponent.Bonus != null) Add(DuelMoveKind.Magnet, "Tomar Bonus rival: " + Card(opponent.Bonus), flag: true);
                    for (var i = 0; i < opponent.Hand.Count; i++) Add(DuelMoveKind.Magnet, $"Tomar {Card(opponent.Hand[i])} · carta rival {i + 1}", i);
                    break;
                case Phase.CodeResponse:
                    Add(DuelMoveKind.CodeResponse, "Dejar resolver el código");
                    Add(DuelMoveKind.CodeResponse, "Cancelar con Caja de Cartón", flag: true);
                    break;
                case Phase.ErrorResponse:
                    Add(DuelMoveKind.PassError, "No responder con Error de Carga");
                    break;
                case Phase.PointsChoice:
                    for (var owner = 0; owner < 2; owner++)
                    {
                        Add(DuelMoveKind.Points, "Aumentar PR de " + s.Players[owner].Name, target: owner, flag: true);
                        Add(DuelMoveKind.Points, "Disminuir PR de " + s.Players[owner].Name, target: owner);
                    }
                    break;
                case Phase.DestroyChoice:
                    foreach (var t in game.DestructionTargets())
                    {
                        if (game.IsZoneDestruction)
                        {
                            var own = t.Owner == actor;
                            var place = t.Zone switch
                            {
                                FieldZone.Hand => own ? "Tu mano al azar" : "Mano rival al azar",
                                FieldZone.Equipment => own ? "Tu equipo" : "Equipo rival",
                                _ => own ? "Tu Bonus" : "Bonus rival"
                            };
                            Add(DuelMoveKind.Destroy, place, -1, (int)t.Zone, t.Owner);
                            continue;
                        }
                        var zone = t.Zone switch { FieldZone.Equipment => "equipo", FieldZone.Attached => "sobre Yuno", FieldZone.Bonus => "Bonus", _ => "canje" };
                        Add(DuelMoveKind.Destroy, $"Destruir {Card(t.CardId)} de {s.Players[t.Owner].Name} · {zone}" + (t.Index >= 0 ? $" {t.Index + 1}" : ""), t.Index, (int)t.Zone, t.Owner);
                    }
                    break;
                case Phase.RelikiaChoice:
                    if (s.PendingRelikia == RelikiaChoice.PlaceHandCard)
                        for (var i = 0; i < p.Hand.Count; i++)
                        {
                            Add(DuelMoveKind.Placement, $"Al fondo del mazo: {Card(p.Hand[i])} · carta {i + 1}", i, (int)CardDestination.DeckBottom);
                            if (p.Bonus != null) Add(DuelMoveKind.Placement, $"A canje: {Card(p.Hand[i])} · carta {i + 1}", i, (int)CardDestination.Exchange);
                        }
                    else if (s.PendingRelikia == RelikiaChoice.BonusReward)
                    {
                        Add(DuelMoveKind.Reward, "El rival descarta dos cartas", value: (int)RelikiaReward.OpponentDiscards);
                        Add(DuelMoveKind.Reward, "Corromper dos códigos", value: (int)RelikiaReward.CorruptCodes);
                    }
                    else if (s.PendingRelikia == RelikiaChoice.CorruptCode)
                        for (var owner = 0; owner < 2; owner++)
                            for (var i = 0; i < s.Players[owner].Memory.Count; i++)
                                if (CardCatalog.Get(s.Players[owner].Memory[i]).Kind == CardKind.Code)
                                    Add(DuelMoveKind.Corrupt, $"Corromper {Card(s.Players[owner].Memory[i])} · memoria de {s.Players[owner].Name} {i + 1}", i, target: owner);
                    break;
            }
        }
        Add(DuelMoveKind.Concede, "Abandonar duelo");
        return result;
    }
}
