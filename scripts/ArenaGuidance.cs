using Relick.Core;

namespace Relick;

// Presentation reads only the caller's authorized options. It never predicts a move.
internal static class ArenaGuidance
{
    public static bool SuggestAdvance(DuelSnapshot s) => s.Winner == null && s.CurrentPlayer == s.MySeat &&
        s.DecisionPlayer == s.MySeat && s.Phase.IsTurnStage() &&
        s.Options.Any(o => o.Move.Kind == DuelMoveKind.Advance) &&
        s.Options.All(o => o.Move.Kind is DuelMoveKind.Advance or DuelMoveKind.Concede);

    public static string NextStep(DuelSnapshot s) => s.Phase switch
    {
        Phase.Draw => s.ExtraDrawStages > 0 ? "Repite la fase de robo." : "Pasa a combate.",
        Phase.Combat => s.ExtraCombatStages > 0 ? "Repite la fase de combate." : "Pasa a acciones.",
        Phase.Actions => s.ExtraActionStages > 0 ? "Repite la fase de acciones." : "Finaliza tu turno.",
        _ => s.ExtraFinalStages > 0 ? "Repite la fase final." : "Finaliza tu turno."
    };

    public static string Instruction(DuelSnapshot s)
    {
        bool Has(DuelMoveKind kind) => s.Options.Any(o => o.Move.Kind == kind);
        if (s.Winner != null) return "El duelo terminó.";
        if (s.DecisionPlayer != s.MySeat)
        {
            var waiting = s.Phase switch
            {
                Phase.DefenseChoice or Phase.TotemChoice or Phase.EffectDamageChoice => "El rival está decidiendo cómo recibir el daño.",
                Phase.ErrorResponse or Phase.CodeResponse or Phase.DestructionResponse => "Esperando la respuesta del rival.",
                Phase.AttackRerollChoice or Phase.DieRerollChoice => "El rival está resolviendo su dado.",
                Phase.Discard or Phase.TurnBuffDiscard or Phase.Final => "El rival está resolviendo sus descartes.",
                Phase.TrapCraftChoice => "El rival está decidiendo si crea un Código Trampa.",
                Phase.ZahiraTarget => "El rival elige dónde colocar Zahira Flair.",
                _ => "Esperando la decisión del rival."
            };
            return waiting + (Has(DuelMoveKind.PlayCode) ? " Puedes activar un COMBO." : "");
        }
        if (SuggestAdvance(s)) return "No quedan jugadas disponibles. " + NextStep(s);
        return s.Phase switch
        {
            Phase.AttackRerollChoice or Phase.DieRerollChoice => s.Options.Any(o => o.Move.Kind is DuelMoveKind.Dexterity or DuelMoveKind.Pendant or DuelMoveKind.Pc or DuelMoveKind.LearningDie or DuelMoveKind.ConvertD10 or DuelMoveKind.PlayCode ||
                o.Move.Kind == DuelMoveKind.IronwingDie && IsUsefulDieModifier(o, s) ||
                o.Move.Kind is DuelMoveKind.AttackDie or DuelMoveKind.EffectDie && o.Move.Flag)
                ? "Conserva el dado o elige cómo modificarlo." : "Confirma el resultado del dado.",
            Phase.DefenseChoice => "Elige cómo defenderte o confirma recibir el daño.",
            Phase.EffectDamageChoice => Has(DuelMoveKind.PlayCode) ? "Responde con un COMBO o confirma recibir el daño." : "Confirma recibir el daño.",
            Phase.CodeResponse => "Anula el código con Caja o deja que se resuelva.",
            Phase.DestructionResponse => "Elige una protección o permite la destrucción.",
            Phase.BlueEggDrawChoice => $"Decide si pagas {GameEngine.BlueEggDrawCost} PR para robar una carta.",
            Phase.Decision when s.Decision is { } decision => decision.Decider == s.MySeat ? decision.Instruction.TrimEnd('.') + "." : $"{decision.Title}: decide el rival.",
            Phase.TotemChoice => "Decide si destruyes un Tótem para cancelar el daño.",
            Phase.SniperChoice => "Elige si equipas Mira de Sniper o la colocas en canje.",
            Phase.TrapCraftChoice when s.TrapCardId == CardCatalog.ZahiraFlair.Id => "Tu canje permite crear Zahira Flair.",
            Phase.TrapCraftChoice when s.TrapCardId == CardCatalog.SuperAttack.Id => "Tu canje permite añadir Súper Ataque a tu mano.",
            Phase.TrapCraftChoice => "Tu canje permite crear Súper Imán en la zona de equipos.",
            Phase.ZahiraTarget => "Elige una carta del campo para colocar a Zahira Flair encima.",
            Phase.PointsChoice => "Elige de quién modificar los PR y después aumenta o resta.",
            Phase.MindResetChoice => "Elige la mano que quieres revisar.",
            Phase.RelikiaChoice => Has(DuelMoveKind.Placement) ? "Elige una carta y su destino: canje o fondo del mazo." : "Elige cómo resolver la Relikia.",
            Phase.Combat when Has(DuelMoveKind.Learn) && Has(DuelMoveKind.Attack) => "Enseña una habilidad o elige un Ataque.",
            Phase.Combat when Has(DuelMoveKind.Attack) => "Elige un Ataque y la habilidad que usarás.",
            Phase.Combat when Has(DuelMoveKind.Learn) => "Puedes enseñar una habilidad antes de avanzar.",
            Phase.Combat when Has(DuelMoveKind.FieldCode) => "Puedes activar una carta iluminada en Bonus o canje.",
            Phase.Actions when Has(DuelMoveKind.Equip) || Has(DuelMoveKind.Exchange) || Has(DuelMoveKind.PlayCode) || Has(DuelMoveKind.ActivateArea) => "Elige una carta para ver sus jugadas disponibles.",
            Phase.Actions when Has(DuelMoveKind.DestinyPickMemory) => "Abre tu memoria para activar Púa del Destino.",
            Phase.Actions when Has(DuelMoveKind.Relikia) => "Puedes activar una Relikia antes de terminar.",
            _ when Has(DuelMoveKind.UsePurpleRing) => "Selecciona Anillo Morado en tus equipos ligeros para usarlo.",
            _ when Has(DuelMoveKind.PlayCode) => "Puedes activar un COMBO o avanzar de fase.",
            _ when Has(DuelMoveKind.Evolve) => "Puedes evolucionar antes de avanzar.",
            _ => "Elige una opción para continuar la resolución."
        };
    }

    internal static bool IsUsefulDieModifier(DuelOption option, DuelSnapshot s)
    {
        if (option.Move.Kind != DuelMoveKind.IronwingDie || s.Players[s.MySeat].RelikarioId != RelikarioCatalog.Ironwing.Id)
            return true;
        // Offer the cheapest way to reach each result, including when the cap makes two costs equivalent.
        var cost = option.Move.Value;
        return GameEngine.IronwingAttackDieIncrease(s.Die, cost) > GameEngine.IronwingAttackDieIncrease(s.Die, cost - 1);
    }

    // Called only when this hand card has no authorized action in the current snapshot.
    public static string UnavailableCard(DuelSnapshot s, int index)
    {
        var p = s.Players[s.MySeat];
        var id = p.Hand.ElementAtOrDefault(index);
        if (id == null) return "Esta carta no está disponible.";
        var card = CardCatalog.Get(id);
        var own = s.CurrentPlayer == s.MySeat && s.DecisionPlayer == s.MySeat;
        var normal = s.Phase.IsTurnStage();
        if (card.IsCombo && s.AutoPassCombos && (!own || !normal))
            return "Pulsa COMBO hasta ver la mano alzada para responder con esta carta.";
        if (!own && !card.IsCombo) return "";
        if (!normal) return "Completa primero la decisión pendiente. " + Instruction(s);
        if (card.Kind is CardKind.Attack or CardKind.Skill)
        {
            if (!own) return "Espera a tu fase de combate.";
            if (s.Phase != Phase.Combat) return "Esta carta se usa en tu fase de combate.";
            if (card.Kind == CardKind.Attack)
            {
                if (s.Turn <= 1) return "Quien inicia no puede atacar en su primer turno. Podrás atacar en tu siguiente turno.";
                if (p.Skills.Length == 0) return "Primero enseña una habilidad para poder atacar.";
                return "Ya realizaste tu ataque de este turno.";
            }
            if (p.Skills.Length >= GameEngine.MaxSkills) return "Los cuatro espacios de habilidad están ocupados.";
            var position = Enumerable.Range(0, GameEngine.MaxSkills).First(i => p.SkillIndexAtPosition(i) < 0);
            if (p.Pr < GameEngine.SkillSlotCosts[position]) return "No tienes PR suficientes para enseñar en el siguiente espacio.";
            return "Solo puedes enseñar una habilidad por turno.";
        }
        if (card.Kind == CardKind.Equipment && s.Phase != Phase.Actions) return "Los equipos se colocan en tu fase de acciones.";
        if (card.Kind == CardKind.Area && s.Phase != Phase.Actions) return "Las Áreas se activan en tu fase de acciones.";
        if (card.Kind == CardKind.Area && p.Area != null) return "Retira primero tu Área activa.";
        if (card.Kind == CardKind.Code && !card.IsCombo && s.Phase != Phase.Actions) return "";
        if (p.Actions < (card.Kind == CardKind.Equipment ? 1 : card.ActionCost)) return "No te quedan acciones suficientes para jugar esta carta.";
        if (s.Turn < card.EarliestTurn) return $"Esta carta puede activarse a partir del turno {card.EarliestTurn}.";
        if (p.Pr < GameEngine.CodePrCost(card, s.LegacyBlueEgg)) return "No tienes PR suficientes para activar esta carta.";
        if (p.HandCount - 1 < card.DiscardCost) return "No tienes suficientes cartas adicionales para pagar el descarte.";
        if (card.FieldCombatDie) return "Activa Jessica Lee desde Bonus o canje durante tu fase de combate.";
        if (card.Id == CardCatalog.Dodge.Id) return "Requiere un ataque rival pendiente de defensa.";
        if (card.RerollOwnDie || card.SetOwnDie > 0) return "Requiere un dado propio pendiente de resolver.";
        if (card.CancelActivation) return "Requiere una activación rival que puedas anular.";
        return "El efecto de esta carta no tiene una activación disponible ahora.";
    }
}
