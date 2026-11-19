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
        if (game.CanEvolve(actor) && RelikarioCatalog.EvolutionFor(p.Relikario) is { } next)
            Add(DuelMoveKind.Evolve, $"Evolucionar a {next.DisplayName} Lv {next.Level} · {next.EvolutionCost} PR");
        if (s.DecisionPlayer == actor)
        {
            foreach (var slot in game.IronwingFireDestructions(actor))
            {
                Add(DuelMoveKind.IronwingFire, $"Ironwing Fire · destruir una carta · espacio {p.SkillPosition(slot) + 1}", slot);
                result[^1].CardId = CardCatalog.IronwingFire.Id;
            }
            if (s.Phase is Phase.Draw or Phase.Combat or Phase.Actions or Phase.Final)
                for (var i = 0; i < (p.LightEquipment?.Count ?? 0); i++)
                {
                    if (!game.CanUsePurpleRing(actor, i)) continue;
                    Add(DuelMoveKind.UsePurpleRing, $"Destruir Anillo Morado · ganar 500 PR · equipo ligero {i + 1}", i);
                    result[^1].Field = new(actor, FieldZone.LightEquipment, i, CardCatalog.PurpleRing.Id);
                    result[^1].CardId = CardCatalog.PurpleRing.Id;
                }
            switch (s.Phase)
            {
                case Phase.TrapCraftChoice:
                    // One option per recipe the exchange met; the value is the recipe's position.
                    var recipes = s.TrapCraftQueue![0].Options;
                    for (var choice = 0; choice < recipes.Count; choice++)
                        Add(DuelMoveKind.TrapCraft, $"Crear {Card(recipes[choice])}", value: choice, flag: true);
                    Add(DuelMoveKind.TrapCraft, "Pasar sin crear");
                    break;
                case Phase.ZahiraTarget:
                    foreach (var host in game.ZahiraTargets(actor))
                    {
                        var position = host.Zone switch
                        {
                            FieldZone.Equipment when s.Players[host.Owner].EquipmentStackedCopies > 0 => $" · equipo {host.Index + 1}",
                            FieldZone.EquipmentCover => $" · cobertura {host.Index + 1}",
                            FieldZone.LightEquipment => $" · equipo ligero {host.Index + 1}",
                            FieldZone.Skill => $" · habilidad {s.Players[host.Owner].SkillPosition(host.Index) + 1}",
                            FieldZone.Exchange => $" · canje {host.Index + 1}",
                            FieldZone.Attached => $" · anexo {host.Index + 1}",
                            FieldZone.Bonus => " · Bonus",
                            FieldZone.Area => " · Área",
                            _ => ""
                        };
                        Add(DuelMoveKind.ZahiraTarget, $"{game.FieldCardName(host)} · {s.Players[host.Owner].Name}{position}",
                            host.Index, (int)host.Zone, host.Owner);
                    }
                    break;
                case Phase.GoldenTotemChoice:
                    var maxTotems = Math.Min(2, p.Hand.Count(id => id == CardCatalog.Totem.Id));
                    for (var count = p.Equipment == null ? 1 : 0; count <= maxTotems; count++)
                        Add(DuelMoveKind.GoldenTotem, count == 0 ? "Colocar Tótem de Oro sin mostrar Tótems" : $"Mostrar y colocar {count} Tótem(s) de Salvación", discards: count);
                    break;
                case Phase.TotemChoice:
                    Add(DuelMoveKind.Totem, $"Destruir un Tótem · cancelar {s.PendingTotemDamage!.Amount} de daño", flag: true);
                    Add(DuelMoveKind.Totem, $"Recibir {s.PendingTotemDamage.Amount} de daño sin protección del Tótem");
                    break;
                case Phase.Draw:
                    Add(DuelMoveKind.Advance, s.ExtraDrawStages > 0 ? "Repetir etapa de robo" : "Ir a combate");
                    break;
                case Phase.TurnBuffDiscard:
                    for (var i = 0; i < p.Hand.Count; i++)
                        Add(DuelMoveKind.TurnBuffDiscard, $"Descartar {Card(p.Hand[i])} para {Card(s.PendingTurnBuff!.CardId)} · carta {i + 1}", i);
                    break;
                case Phase.EffectDamageChoice:
                    Add(DuelMoveKind.EffectDamage, "Resolver el daño de Jessica Lee");
                    break;
                case Phase.DestructionResponse:
                    Add(DuelMoveKind.ProtectDestruction, "Permitir la destrucción");
                    foreach (var slot in game.DestructionProtectors(actor))
                        Add(DuelMoveKind.ProtectDestruction, $"Sacrificar Reverse Shuffle · espacio {p.SkillPosition(slot) + 1} · proteger carta propia", slot);
                    break;
                case Phase.SniperChoice:
                    Add(DuelMoveKind.Sniper, "Equipar gratis Mira de Sniper", flag: true);
                    Add(DuelMoveKind.Sniper, "Colocar Mira de Sniper en canje");
                    break;
                case Phase.Combat:
                    foreach (var source in game.FieldCombatTargets(actor))
                        Add(DuelMoveKind.FieldCode, $"Activar {Card(source.CardId)} · {(source.Zone == FieldZone.Bonus ? "Bonus" : $"canje {source.Index + 1}")} · sin acciones", source.Index, (int)source.Zone);
                    for (var i = 0; i < p.Hand.Count; i++)
                    {
                        var card = CardCatalog.Get(p.Hand[i]);
                        if (GameEngine.CanLearnSkill(p, card) && !s.LearnedThisTurn && p.Skills.Count < GameEngine.MaxSkills && p.Relikia >= game.SkillCost(p))
                            Add(DuelMoveKind.Learn, $"Enseñar {card.Name} · {game.SkillCost(p)} PR · carta {i + 1}", i);
                        if (card.Kind == CardKind.Attack && game.CanAttack(actor))
                            for (var skill = 0; skill < p.Skills.Count; skill++)
                                if (GameEngine.CanAttackWithTaughtSkill(p, p.Skills[skill]))
                                    Add(DuelMoveKind.Attack, $"Atacar con {Card(p.Skills[skill])} · espacio {p.SkillPosition(skill) + 1} · carta {i + 1}", i, skill);
                    }
                    Add(DuelMoveKind.Advance, s.ExtraCombatStages > 0 ? "Repetir etapa de combate" : "Ir a acciones");
                    break;
                case Phase.Actions:
                    if (game.CanUseOsamuSearch(actor)) Add(DuelMoveKind.RelikarioSearch, "Buscar con Osamu Lv 2 · 1 acción");
                    foreach (var i in game.DestinyPickMemorySlots(actor))
                        Add(DuelMoveKind.DestinyPickMemory, $"Activar Púa del Destino · memoria {i + 1}", i);
                    if (game.AvailableActions(actor) > 0)
                        for (var i = 0; i < p.Hand.Count; i++)
                        {
                            if (CardCatalog.Get(p.Hand[i]).Kind == CardKind.Equipment)
                            {
                                Add(DuelMoveKind.Equip, $"Equipar {Card(p.Hand[i])} · carta {i + 1}", i);
                                if (p.Hand[i] == CardCatalog.MetalHood.Id)
                                    Add(DuelMoveKind.Equip, $"Equipar {Card(p.Hand[i])} al rival · carta {i + 1}", i, flag: true);
                            }
                            if (game.CanActivateArea(actor, i)) Add(DuelMoveKind.ActivateArea, $"Activar {Card(p.Hand[i])} · carta {i + 1}", i);
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
                    Add(DuelMoveKind.Advance, s.ExtraActionStages > 0 ? "Repetir etapa de acciones" : "Terminar turno");
                    break;
                case Phase.Discard:
                    for (var i = 0; i < p.Hand.Count; i++) Add(DuelMoveKind.Discard, $"Descartar {Card(p.Hand[i])} · carta {i + 1}", i);
                    break;
                case Phase.Final:
                    if (p.Hand.Count > GameEngine.MaxHandSize)
                        for (var i = 0; i < p.Hand.Count; i++) Add(DuelMoveKind.Discard, $"Descartar {Card(p.Hand[i])} · carta {i + 1}", i);
                    else Add(DuelMoveKind.Advance, s.ExtraFinalStages > 0 ? "Repetir etapa final" : "Terminar turno");
                    break;
                case Phase.RecycleChoice:
                    Add(DuelMoveKind.Recycle, $"Reciclar {game.RecyclingCount} cartas de tu memoria", discards: game.RecyclingCount);
                    break;
                case Phase.MindResetChoice:
                    var reset = s.PendingMindReset!;
                    if (reset.Target < 0)
                    {
                        for (var owner = 0; owner < 2; owner++)
                            if (s.Players[owner].Hand.Count > 0)
                                Add(DuelMoveKind.MindResetTarget, "Mirar mano de " + s.Players[owner].Name, target: owner);
                        if (s.Players.All(player => player.Hand.Count == 0))
                            Add(DuelMoveKind.MindResetHand, "No quedan manos objetivo: finalizar");
                    }
                    else if (!s.Players[reset.Target].Hand.Any(id => CardCatalog.Get(id).Kind != CardKind.Skill))
                        Add(DuelMoveKind.MindResetHand, "No hay no-habilidades: finalizar sin devolver cartas");
                    else if (reset.Memories.Count < 2)
                    {
                        for (var owner = 0; owner < 2; owner++)
                            for (var i = 0; i < s.Players[owner].Memory.Count; i++)
                                if (!reset.Memories.Contains(new(owner, i)) && game.MindResetMemories().Contains(new(owner, i)))
                                {
                                    Add(DuelMoveKind.MindResetMemory, $"Memoria {reset.Memories.Count + 1}/2: {Card(s.Players[owner].Memory[i])} · {s.Players[owner].Name} · carta {i + 1}", i, target: owner);
                                    result[^1].CardId = s.Players[owner].Memory[i];
                                }
                    }
                    else
                        for (var i = 0; i < s.Players[reset.Target].Hand.Count; i++)
                            if (CardCatalog.Get(s.Players[reset.Target].Hand[i]).Kind != CardKind.Skill)
                            {
                                Add(DuelMoveKind.MindResetHand, $"Devolver {Card(s.Players[reset.Target].Hand[i])} · mano de {s.Players[reset.Target].Name} · carta {i + 1}", i);
                                result[^1].CardId = s.Players[reset.Target].Hand[i];
                            }
                    break;
                case Phase.MemoryRecoveryChoice:
                    foreach (var selection in game.RecoverableMemoryCards())
                    {
                        var id = s.Players[selection.Owner].Memory[selection.Index];
                        Add(DuelMoveKind.RecoverMemory, $"Recuperar {Card(id)} · memoria de {s.Players[selection.Owner].Name} · carta {selection.Index + 1}", selection.Index, target: selection.Owner);
                        result[^1].CardId = id;
                    }
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
                    if (game.CanRerollLearningDie(actor)) Add(DuelMoveKind.LearningDie, $"Relanzar por Reverse Shuffle · {2 - s.PendingDieRoll!.LearningRerolls} restantes");
                    if (game.CanUseDexterity(actor)) Add(DuelMoveKind.Dexterity, "Usar destreza · +1 al dado · 1 ficha");
                    for (var count = 1; count <= game.IronwingDieCostLimit(actor); count++)
                    {
                        var increase = game.IronwingDieIncrease(actor, count);
                        Add(DuelMoveKind.IronwingDie, $"Ironwing · destruir {count} Ataque(s) · +{increase} al dado", value: count);
                    }
                    if (game.CanUsePc(actor)) Add(DuelMoveKind.Pc, $"Destruir PC 8BITS · valor guardado {p.EquipmentDie}");
                    if (game.CanUseRelikarioOnPendingDie(actor)) Add(kind, $"Relanzar con {p.Relikario.DisplayName}", flag: true);
                    foreach (var effect in Enum.GetValues<PendantEffect>().Where(e => game.CanUsePendant(actor, e)))
                        Add(DuelMoveKind.Pendant, "Usar Colgante: " + (effect == PendantEffect.Reroll ? "relanzar" : effect == PendantEffect.AddOne ? "+1" : "+2"), value: (int)effect);
                    break;
                case Phase.DefenseChoice:
                    Add(DuelMoveKind.PassDefense, "Recibir el daño");
                    if (game.CanCancelAttackWithBox(actor)) Add(DuelMoveKind.BoxDefense, "Cancelar daño con Caja de Cartón");
                    foreach (var slot in game.AvailableDefenses(actor))
                        Add(DuelMoveKind.DestinyDefense, $"Defender con Destiny · espacio {p.SkillPosition(slot) + 1} · selecciona {game.DefenseDiscardCost} cartas", slot, discards: game.DefenseDiscardCost);
                    foreach (var slot in game.IronwingRollDefenses(actor))
                    {
                        Add(DuelMoveKind.IronwingRoll, $"Ironwing Roll · destruir {game.IronwingRollCost} Ataques · espacio {p.SkillPosition(slot) + 1}", slot);
                        result[^1].CardId = CardCatalog.IronwingRoll.Id;
                    }
                    break;
                case Phase.MagnetChoice:
                    Add(DuelMoveKind.SkipMagnet, "No tomar ninguna carta");
                    var opponent = s.Players[1 - actor];
                    if (opponent.Bonus != null) Add(DuelMoveKind.Magnet, "Tomar Bonus rival: " + Card(opponent.Bonus), flag: true);
                    if (opponent.Area != null) Add(DuelMoveKind.Magnet, "Tomar Área rival: " + Card(opponent.Area), value: (int)FieldZone.Area, flag: true);
                    for (var i = 0; i < opponent.Hand.Count; i++) Add(DuelMoveKind.Magnet, $"Tomar {Card(opponent.Hand[i])} · carta rival {i + 1}", i);
                    break;
                case Phase.CodeResponse:
                    Add(DuelMoveKind.CodeResponse, "Dejar resolver el código");
                    if (p.Equipment == CardCatalog.CardboardBox.Id && !game.EffectsSuppressed(actor, FieldZone.Equipment)) Add(DuelMoveKind.CodeResponse, "Cancelar con Caja de Cartón", flag: true);
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
                                FieldZone.EquipmentCover => $"{(own ? "Tu" : "Rival")} Tótem de Oro · copia {t.Index + 1}",
                                FieldZone.Zahira => $"{(own ? "Tu" : "Rival")} Zahira Flair{(GameEngine.ZahiraSlot(t.Index) > 0 ? " · segunda copia" : "")}",
                                FieldZone.Area => own ? "Tu Área" : "Área rival",
                                FieldZone.LightEquipment => GameEngine.FieldPlace(s, t, actor),
                                _ => own ? "Tu Bonus" : "Bonus rival"
                            };
                            Add(DuelMoveKind.Destroy, place, t.Index, (int)t.Zone, t.Owner);
                            continue;
                        }
                        var zone = t.Zone switch { FieldZone.Equipment or FieldZone.EquipmentCover => "equipo", FieldZone.LightEquipment => "equipo ligero",
                            FieldZone.Attached => $"sobre {s.Players[t.Owner].Relikario.DisplayName}", FieldZone.Bonus => "Bonus", _ => "canje" };
                        Add(DuelMoveKind.Destroy, $"Destruir {Card(t.CardId)} de {s.Players[t.Owner].Name} · {zone}" + (t.Index >= 0 ? $" {t.Index + 1}" : ""), t.Index, (int)t.Zone, t.Owner);
                    }
                    break;
                case Phase.Decision:
                    var choices = game.DecisionChoices(actor);
                    for (var i = 0; i < choices.Count; i++)
                    {
                        Add(DuelMoveKind.Choose, choices[i].Label, i);
                        result[^1].CardId = choices[i].CardId; result[^1].Field = choices[i].Field;
                        result[^1].Button = choices[i].Button; result[^1].Primary = choices[i].Primary; result[^1].Owner = choices[i].Owner;
                    }
                    break;
                case Phase.BlueEggDrawChoice:
                    if (p.Relikia >= GameEngine.BlueEggDrawCost)
                        Add(DuelMoveKind.BlueEggDraw, $"Robar 1 carta · {GameEngine.BlueEggDrawCost} PR", flag: true);
                    Add(DuelMoveKind.BlueEggDraw, "Continuar sin robar");
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
