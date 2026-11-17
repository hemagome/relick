namespace Relick.Core;

public enum DuelResolutionStatus { Resolving, Response, Resolved, Canceled }
public sealed class PublicActivation
{
    public int Actor { get; set; }
    public string CardId { get; set; } = "";
}
/// <summary>One resolved link of a public response chain. <see cref="Attack"/> marks the pending attack the chain answered;
/// it is never canceled by the chain itself.</summary>
public sealed class PublicChainLink
{
    public int Actor { get; set; }
    public string CardId { get; set; } = "";
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool Canceled { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool Attack { get; set; }
}
public sealed class DuelResolutionView
{
    public int Actor { get; set; }
    public int DecisionPlayer { get; set; }
    public string CardId { get; set; } = "";
    public DuelResolutionStatus Status { get; set; }
    public List<PublicActivation> Responses { get; set; } = [];
    /// <summary>The pending attack an open Error de Carga chain answers (a COMBO during combat); null otherwise.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public PublicActivation? Attack { get; set; }
}
public sealed class DuelHistoryEntry
{
    public ulong Id { get; set; }
    public int Turn { get; set; }
    public int Actor { get; set; }
    public string CardId { get; set; } = "";
    public int SourceActor { get; set; }
    public string Text { get; set; } = "";
    public string[] Details { get; set; } = [];
    public DuelResolutionStatus Status { get; set; }
}
public sealed class DuelHistory
{
    public const int Capacity = 20;
    public ulong Sequence { get; set; }
    public List<DuelHistoryEntry> Entries { get; set; } = [];
}

public sealed partial class GameEngine
{
    internal DuelHistory? PublicHistory { get; set; }

    // An explicit public projection; never serialize the activation frames or their continuations.
    internal DuelResolutionView? PublicResolution()
    {
        var state = State;
        if (state.IsOver || state.Phase is Phase.Preparation or Phase.Draw or Phase.Combat or Phase.Actions or Phase.Final or Phase.Finished) return null;
        var frame = _activationChain.FirstOrDefault() ?? _activeActivation;
        var card = frame?.CardId ?? state.PendingCode?.CardId ?? state.PendingAttack?.SkillId ?? state.PendingAttackRoll?.SkillId ??
            state.PendingDecision?.Attack?.SkillId;
        var actor = frame?.Actor ?? state.PendingCode?.Actor ?? state.PendingAttack?.Attacker ?? state.PendingAttackRoll?.Attacker ?? state.DecisionPlayer;
        if (card == null && state.PendingDieRoll is { } die)
        {
            actor = die.Player;
            card = die.Purpose switch
            {
                DiePurpose.ExtraLife => CardCatalog.ExtraLife.Id, DiePurpose.PointsChange => CardCatalog.PointsIncrease.Id,
                DiePurpose.Dodge => CardCatalog.Dodge.Id, DiePurpose.PcStore => CardCatalog.Pc.Id,
                DiePurpose.SkillLearning => CardCatalog.Reverse.Id, DiePurpose.FieldCombat => CardCatalog.Jessica.Id,
                DiePurpose.QuickLoad => CardCatalog.QuickLoad.Id, DiePurpose.Zahira => CardCatalog.ZahiraFlair.Id,
                DiePurpose.TurboSwordDamage or DiePurpose.TurboSwordDefense => CardCatalog.TurboSword.Id,
                DiePurpose.TurboOverlapDamage => CardCatalog.TurboOverlap.Id, _ => null
            };
        }
        return new DuelResolutionView { Actor = actor, DecisionPlayer = state.DecisionPlayer, CardId = card ?? "",
            Status = state.Phase is Phase.ErrorResponse or Phase.CodeResponse or Phase.DefenseChoice or Phase.DestructionResponse or Phase.EffectDamageChoice or Phase.TotemChoice
                ? DuelResolutionStatus.Response : DuelResolutionStatus.Resolving,
            Responses = _activationChain.Skip(1).Select(f => new PublicActivation { Actor = f.Actor, CardId = f.CardId }).ToList(),
            Attack = _activationChain.Count > 0 && ChainAttack() is { } attack ? new PublicActivation { Actor = attack.Actor, CardId = attack.CardId } : null };
    }
}

// One observer per accepted protocol command. Captures public outcomes, never the diagnostic log.
internal sealed class PublicDuelObservation : IDisposable
{
    private readonly GameEngine _game;
    private readonly DuelMove _move;
    private readonly int _actor, _turn;
    private readonly DuelResolutionView? _beforeResolution;
    private readonly (int Health, int Pr, int Actions, string? Equipment, int Copies, FieldTarget? ZahiraHost, FieldTarget? ZahiraHost2)[] _before;
    private readonly List<string> _outcomes = [];
    private string _card = "";
    private int _sourceActor;
    private readonly string _text;

    public PublicDuelObservation(GameEngine game, int actor, DuelMove move)
    {
        _game = game; _actor = actor; _move = move; _turn = game.State.Turn;
        _beforeResolution = game.PublicResolution();
        _before = game.State.Players.Select(p => (p.Health, p.Relikia, p.ActionsLeft, p.Equipment, p.EquipmentStackedCopies, p.ZahiraHost, p.ZahiraHost2)).ToArray();
        var source = PublicSource(game, actor, move);
        _card = source ?? _beforeResolution?.CardId ?? "";
        _sourceActor = source != null ? actor : _beforeResolution?.Actor ?? actor;
        _text = ActionText(game, actor, move, _card);
        game.DieRolled += Die; game.DieAdjusted += Adjusted; game.DamageReceived += Damage;
        game.HealingReceived += Heal; game.CardDrawn += Draw; game.HandDiscarded += Discard;
        game.CardMovedToPile += Pile; game.HandTransferred += Transfer;
        game.BonusClaimed += Bonus; game.MemoryCardClaimed += Memory;
        game.SuperMagnetCharged += TrapCharged;
    }
    private string Player(int player) => player is 0 or 1 ? _game.State.Players[player].Name : "Jugador";
    private static string Card(string id) => CardCatalog.All.TryGetValue(id, out var c) ? c.Name : "Carta";
    private void Die(int value) => _outcomes.Add($"D6: {value}.");
    private void Adjusted(int value) => _outcomes.Add($"Dado modificado: {value}.");
    private void Damage(int player, int value, int die) => _outcomes.Add($"{Player(player)} recibe {value} de daño.");
    private void Heal(int player, int value) => _outcomes.Add($"{Player(player)} recupera {value} vida(s).");
    private readonly int[] _draws = [0, 0];
    private void Draw(int player, int slot) { if (player is 0 or 1) _draws[player]++; }
    private void Discard(int player, int count) => _outcomes.Add($"{Player(player)} descarta {count} carta(s).");
    private void Transfer(int owner, int recipient, int count) => _outcomes.Add($"{Player(owner)} entrega {count} carta(s) a {Player(recipient)}.");
    private void Bonus(int owner, int recipient, int slot, string id) => _outcomes.Add($"{Player(recipient)} obtiene {Card(id)} del Bonus de {Player(owner)}.");
    private void Memory(int owner, int recipient, int index, int slot) => _outcomes.Add($"{Player(recipient)} recibe una carta de la memoria de {Player(owner)}.");
    private void TrapCharged(int owner, int count) => _outcomes.Add($"Súper Imán de {Player(owner)} sustituye el efecto rival y recibe un contador ({count}/{CardCatalog.SuperMagnetCounterLimit}).");
    private void Pile(PileMovement move)
    {
        // Hidden hand/deck moves and face-down corruption never contribute identities.
        if (move.Destination == CardZone.Memory && move.Source is CardZone.Equipment or CardZone.EquipmentCover or CardZone.Skill or CardZone.Bonus or CardZone.Exchange or CardZone.Attached or CardZone.Activation)
            _outcomes.Add(move.ToRivalMemory ? $"{Card(move.CardId)} de {Player(move.Owner)} va a la memoria de {Player(move.MemoryOwner)}, su dueño."
                : $"{Card(move.CardId)} de {Player(move.Owner)} va a memoria.");
        if (move.Destination == CardZone.OutOfGame && move.Source != CardZone.Hand)
            _outcomes.Add($"{Card(move.CardId)} de {Player(move.Owner)} queda fuera del juego.");
    }
    public void Complete()
    {
        var state = _game.State;
        var live = _game.PublicResolution();
        var status = live?.Status ?? DuelResolutionStatus.Resolved;
        var before = _beforeResolution;
        if (before != null && state.Phase != Phase.ErrorResponse && before.Status == DuelResolutionStatus.Response && (_move.Kind == DuelMoveKind.PassError || _move.Kind == DuelMoveKind.ComboResponses || _move.Kind == DuelMoveKind.PlayCode && _card == CardCatalog.LoadError.Id))
        {
            var chain = new List<PublicActivation> { new() { Actor = before.Actor, CardId = before.CardId } };
            chain.AddRange(before.Responses);
            if (_move.Kind == DuelMoveKind.PlayCode && _card == CardCatalog.LoadError.Id) chain.Add(new() { Actor = _actor, CardId = _card });
            if (chain.Count > 1)
            {
                var cancellations = new List<string>();
                for (var i = chain.Count - 1; i > 0; i -= 2)
                    cancellations.Add($"{Card(chain[i].CardId)} de {Player(chain[i].Actor)} anula {Card(chain[i - 1].CardId)} de {Player(chain[i - 1].Actor)}.");
                _outcomes.InsertRange(0, cancellations);
                if (chain.Count % 2 == 0)
                { status = DuelResolutionStatus.Canceled; _outcomes.Insert(cancellations.Count, "Activación anulada: se devuelven sus costes."); }
                _card = before.CardId; _sourceActor = before.Actor;
            }
        }
        if (_move.Kind == DuelMoveKind.CodeResponse && _move.Flag)
        { status = live?.Status ?? DuelResolutionStatus.Canceled; _card = before?.CardId ?? _card; _sourceActor = before?.Actor ?? _actor; _outcomes.Insert(0, "Caja de Cartón anula el código y devuelve sus costes."); }
        for (var i = 0; i < state.Players.Length; i++)
        {
            var p = state.Players[i]; var old = _before[i];
            if (p.Health != old.Health) _outcomes.Add($"{Player(i)}: vidas {old.Health} → {p.Health}.");
            if (p.ActionsLeft > old.Actions) _outcomes.Add($"{Player(i)}: acciones {old.Actions} → {p.ActionsLeft}.");
            if (p.Relikia != old.Pr) _outcomes.Add($"{Player(i)}: {(p.Relikia > old.Pr ? "+" : "")}{p.Relikia - old.Pr} PR.");
            if (_draws[i] > 0) _outcomes.Add($"{Player(i)} roba {_draws[i]} carta(s).");
            if (p.Equipment != null && (p.Equipment != old.Equipment || p.EquipmentStackedCopies > old.Copies))
                _outcomes.Add($"{Player(i)} equipa {Card(p.Equipment)}.");
            if (_move.Kind == DuelMoveKind.ZahiraTarget &&
                (p.ZahiraHost != old.ZahiraHost ? p.ZahiraHost : p.ZahiraHost2 != old.ZahiraHost2 ? p.ZahiraHost2 : null) is { } host)
                _outcomes.Add($"Zahira Flair de {Player(i)} cubre {_game.FieldCardName(host)} de {Player(host.Owner)}.");
        }
        if (state.Winner != null) _outcomes.Add(state.Winner == -1 ? "El duelo termina en empate." : $"{Player(state.Winner.Value)} gana el duelo.");
        var history = _game.PublicHistory ??= new();
        history.Entries.Add(new DuelHistoryEntry { Id = ++history.Sequence, Turn = _turn, Actor = _actor, CardId = _card,
            SourceActor = _sourceActor, Text = _text, Details = _outcomes.Take(10).ToArray(), Status = status });
        if (history.Entries.Count > DuelHistory.Capacity) history.Entries.RemoveRange(0, history.Entries.Count - DuelHistory.Capacity);
    }
    public void Dispose()
    {
        _game.DieRolled -= Die; _game.DieAdjusted -= Adjusted; _game.DamageReceived -= Damage;
        _game.HealingReceived -= Heal; _game.CardDrawn -= Draw; _game.HandDiscarded -= Discard; _game.CardMovedToPile -= Pile;
        _game.HandTransferred -= Transfer; _game.BonusClaimed -= Bonus; _game.MemoryCardClaimed -= Memory;
        _game.SuperMagnetCharged -= TrapCharged;
    }
    private static string? PublicSource(GameEngine g, int actor, DuelMove m)
    {
        if (actor is not (0 or 1)) return null;
        var p = g.State.Players[actor];
        // Only these hand plays announce their identity to everybody on acceptance.
        if (m.Kind is DuelMoveKind.PlayCode or DuelMoveKind.Equip or DuelMoveKind.Learn or DuelMoveKind.Exchange)
            return p.Hand.ElementAtOrDefault(m.Index);
        return m.Kind switch
        {
            DuelMoveKind.Attack or DuelMoveKind.DestinyDefense => p.Skills.ElementAtOrDefault(m.Kind == DuelMoveKind.Attack ? m.Value : m.Index),
            DuelMoveKind.Relikia when m.Value == (int)RelikiaEffect.ReplayCode => p.Memory.ElementAtOrDefault(m.Index),
            DuelMoveKind.FieldCode => m.Value == (int)FieldZone.Bonus ? p.Bonus : p.Exchange.ElementAtOrDefault(m.Index),
            DuelMoveKind.BoxDefense or DuelMoveKind.CodeResponse when m.Flag => CardCatalog.CardboardBox.Id,
            DuelMoveKind.Pc => CardCatalog.Pc.Id, DuelMoveKind.Pendant => CardCatalog.Pendant.Id,
            DuelMoveKind.TrapCraft when m.Flag => g.State.TrapCraftQueue?.FirstOrDefault() is { } craft
                ? craft.Options.ElementAtOrDefault(m.Value) ?? craft.CardId : null,
            DuelMoveKind.ZahiraTarget => CardCatalog.ZahiraFlair.Id,
            DuelMoveKind.Choose when g.State.PendingDecision is { } decision && CardCatalog.All.ContainsKey(decision.Source) => decision.Source,
            _ => null
        };
    }
    private static string ActionText(GameEngine g, int actor, DuelMove m, string source)
    {
        var name = actor is 0 or 1 ? g.State.Players[actor].Name : "Jugador";
        var card = Card(source);
        string Target() => m.Target is 0 or 1 ? g.State.Players[m.Target].Name : "el objetivo";
        return m.Kind switch
        {
            DuelMoveKind.PlayCode => $"{name} activa {card}.",
            DuelMoveKind.Attack when actor is 0 or 1 && g.State.Players[actor].Hand.ElementAtOrDefault(m.Index) == CardCatalog.SuperAttack.Id
                => $"{name} usa Súper Ataque con {card}.",
            DuelMoveKind.Attack => $"{name} ataca con {card}.",
            DuelMoveKind.Learn => $"{name} enseña {card}.", DuelMoveKind.Equip => $"{name} juega {card} como equipo.",
            DuelMoveKind.Exchange => $"{name} coloca {card} en canje.", DuelMoveKind.FieldCode => $"{name} activa {card} desde {(m.Value == (int)FieldZone.Bonus ? "Bonus" : "canje")}.",
            DuelMoveKind.PassError => $"{name} deja resolver la cadena.",
            DuelMoveKind.CodeResponse => m.Flag ? $"{name} responde con Caja de Cartón." : $"{name} deja resolver el código.",
            DuelMoveKind.PassDefense or DuelMoveKind.EffectDamage => $"{name} confirma recibir el daño.",
            DuelMoveKind.BoxDefense => $"{name} cancela el daño con Caja de Cartón.", DuelMoveKind.DestinyDefense => $"{name} se defiende con {card}.",
            DuelMoveKind.AttackDie or DuelMoveKind.EffectDie => m.Flag ? $"{name} relanza con {(actor is 0 or 1 ? g.State.Players[actor].Relikario.DisplayName : "su Relikario")}." : $"{name} confirma el resultado del dado.",
            DuelMoveKind.Pc or DuelMoveKind.Pendant => $"{name} modifica el dado con {card}.",
            DuelMoveKind.Dexterity => $"{name} usa una ficha de destreza.", DuelMoveKind.LearningDie => $"{name} relanza por Reverse Shuffle.",
            DuelMoveKind.Discard or DuelMoveKind.TurnBuffDiscard => $"{name} descarta una carta.",
            DuelMoveKind.Points => $"{name} {(m.Flag ? "aumenta" : "reduce")} los PR de {Target()}.",
            DuelMoveKind.Destroy => $"{name} elige destruir una carta de {Target()} ({(FieldZone)m.Value switch { FieldZone.Hand => "mano al azar", FieldZone.Bonus => "Bonus", FieldZone.Equipment => "equipo", _ => "campo" }}).",
            DuelMoveKind.Choose => g.DecisionChoices(actor).ElementAtOrDefault(m.Index) is { } choice ? $"{name} {choice.Public}" : $"{name} decide.",
            DuelMoveKind.BlueEggDraw => m.Flag ? $"{name} paga {GameEngine.BlueEggDrawCost} PR para robar con Huevo Azul Explosivo." : $"{name} continúa sin robar con Huevo Azul Explosivo.",
            DuelMoveKind.Magnet => $"{name} elige tomar una carta {(m.Flag ? "del Bonus rival" : "de la mano rival")} por Magnet Shuffle.",
            DuelMoveKind.SkipMagnet => $"{name} decide no tomar una carta por Magnet Shuffle.",
            DuelMoveKind.Placement => $"{name} coloca una carta {(m.Value == (int)CardDestination.Exchange ? "en canje" : "al fondo del mazo")}.",
            DuelMoveKind.Relikia => $"{name} activa Relikia: {RelikiaEffects.All.FirstOrDefault(e => e.Id == (RelikiaEffect)m.Value)?.Name ?? "efecto"}.",
            DuelMoveKind.Recycle => $"{name} recicla {m.Discards.Length} carta(s) de memoria.",
            DuelMoveKind.SearchDeck => $"{name} añade una carta del mazo a su mano.",
            DuelMoveKind.MindResetTarget => $"{name} revisa la mano de {Target()}.", DuelMoveKind.MindResetMemory => $"{name} elige una carta de memoria para Resetear la Mente.",
            DuelMoveKind.MindResetHand => $"{name} devuelve una carta al mazo por Resetear la Mente.",
            DuelMoveKind.RecoverMemory => $"{name} recupera una carta de la memoria de {Target()}.",
            DuelMoveKind.Corrupt => $"{name} corrompe una carta de memoria de {Target()}.",
            DuelMoveKind.ProtectDestruction => m.Index >= 0 ? $"{name} sacrifica Reverse Shuffle para proteger su carta." : $"{name} permite la destrucción.",
            DuelMoveKind.Sniper => $"{name} coloca Mira de Sniper {(m.Flag ? "como equipo" : "en canje")}.",
            DuelMoveKind.Totem => m.Flag ? $"{name} usa un Tótem para cancelar el daño." : $"{name} recibe el daño sin usar un Tótem.",
            DuelMoveKind.GoldenTotem => $"{name} coloca Tótem de Oro con {m.Discards.Length} Tótem(s) de Salvación.",
            DuelMoveKind.TrapCraft => m.Flag ? $"{name} elige crear {card}." : $"{name} pasa sin crear un Código Trampa.",
            DuelMoveKind.ZahiraTarget => $"{name} elige dónde colocar Zahira Flair.",
            DuelMoveKind.Evolve => $"{name} evoluciona su Relikario.", DuelMoveKind.Advance => $"{name} avanza de fase.",
            DuelMoveKind.RelikarioSearch => $"{name} activa la búsqueda de Osamu Lv 2.",
            DuelMoveKind.Concede => $"{name} abandona el duelo.", DuelMoveKind.ComboResponses => "Continúa la resolución de la jugada.",
            DuelMoveKind.Reward => $"{name} elige el efecto adicional de Relikia.", _ => $"{name} resuelve una decisión."
        };
    }
}
