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
/// <summary>What a public outcome is; the client decides how each one reads.</summary>
public enum DuelHistoryEventKind
{
    Die, DieAdjusted, Damage, Heal, Draw, Discard, Transfer, BonusClaimed, MemoryClaimed, TrapCharged,
    ToMemory, OutOfGame, AttackPlaced, LightEquipped, Equipped, Health, Actions, Pr, ChainCancel, CostsReturned,
    BoxCancel, ZahiraCover, Winner
}
/// <summary>One public outcome of a play. <see cref="Player"/> is who it happens to; <see cref="Other"/>, the second
/// player involved (the recipient, the canceled card's actor, the memory's owner…). Values: the amount, die face or
/// the new figure; <see cref="From"/> is the previous figure of a change.</summary>
public sealed class DuelHistoryEvent
{
    public DuelHistoryEventKind Kind { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int Player { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int Other { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int Value { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int From { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? CardId { get; set; }
    /// <summary>The second card: the one a chain link cancels, or the field card Zahira Flair covers.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? OtherCardId { get; set; }
}
public sealed class DuelHistoryEntry
{
    public ulong Id { get; set; }
    public int Turn { get; set; }
    public int Actor { get; set; }
    public string CardId { get; set; } = "";
    public int SourceActor { get; set; }
    public string Text { get; set; } = "";
    /// <summary>The public outcomes, in the order they happened.</summary>
    public List<DuelHistoryEvent> Events { get; set; } = [];
    /// <summary>Pre-written outcomes of entries saved before <see cref="Events"/>; read only, never written again.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Details { get; set; }
    public DuelResolutionStatus Status { get; set; }
    /// <summary>The id of the entry that opened this resolution: an activation, its responses and the plays that
    /// settle it share it. Zero (older entries) means the entry stands alone.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public ulong Resolution { get; set; }
    /// <summary>The main stage the play was made in (Robo, Combate, Acciones, Final).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public Phase Stage { get; set; }
    /// <summary>A formality (advance, pass, confirm a die or damage): its line adds nothing, only its outcomes do.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool Routine { get; set; }

    public ulong Group => Resolution != 0 ? Resolution : Id;
}
public sealed class DuelHistory
{
    public const int Capacity = 20;
    public ulong Sequence { get; set; }
    /// <summary>The resolution still open after the last entry; zero when none.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public ulong OpenResolution { get; set; }
    public List<DuelHistoryEntry> Entries { get; set; } = [];
}

/// <summary>Reads the public outcomes in Spanish. Shared by the board and the tests so both say the same.</summary>
public static class DuelHistoryText
{
    public static string Card(string? id) => id != null && CardCatalog.All.TryGetValue(id, out var c) ? c.Name :
        RelikarioCatalog.Find(id)?.Name ?? "Carta";

    public static string Line(DuelHistoryEvent e, Func<int, string> player)
    {
        string P(int seat) => player(seat);
        return e.Kind switch
        {
            DuelHistoryEventKind.Die => $"D6: {e.Value}.",
            DuelHistoryEventKind.DieAdjusted => $"Dado modificado: {e.Value}.",
            DuelHistoryEventKind.Damage => $"{P(e.Player)} recibe {e.Value} de daño.",
            DuelHistoryEventKind.Heal => $"{P(e.Player)} recupera {e.Value} vida(s).",
            DuelHistoryEventKind.Draw => $"{P(e.Player)} roba {e.Value} carta(s).",
            DuelHistoryEventKind.Discard => $"{P(e.Player)} descarta {e.Value} carta(s).",
            DuelHistoryEventKind.Transfer => $"{P(e.Player)} entrega {e.Value} carta(s) a {P(e.Other)}.",
            DuelHistoryEventKind.BonusClaimed => $"{P(e.Player)} obtiene {Card(e.CardId)} del Bonus de {P(e.Other)}.",
            DuelHistoryEventKind.MemoryClaimed => $"{P(e.Player)} recibe una carta de la memoria de {P(e.Other)}.",
            DuelHistoryEventKind.TrapCharged => $"Súper Imán de {P(e.Player)} sustituye el efecto rival y recibe un contador ({e.Value}/{CardCatalog.SuperMagnetCounterLimit}).",
            DuelHistoryEventKind.ToMemory => e.Other != e.Player
                ? $"{Card(e.CardId)} de {P(e.Player)} va a la memoria de {P(e.Other)}, su dueño." : $"{Card(e.CardId)} de {P(e.Player)} va a memoria.",
            DuelHistoryEventKind.OutOfGame => $"{Card(e.CardId)} de {P(e.Player)} queda fuera del juego.",
            DuelHistoryEventKind.AttackPlaced => $"{P(e.Player)} coloca un Ataque debajo de su Relikario y baraja el mazo.",
            DuelHistoryEventKind.LightEquipped => $"{P(e.Player)} equipa {Card(e.CardId)} como equipo ligero.",
            DuelHistoryEventKind.Equipped => $"{P(e.Player)} equipa {Card(e.CardId)}.",
            DuelHistoryEventKind.Health => $"{P(e.Player)}: vidas {e.From} → {e.Value}.",
            DuelHistoryEventKind.Actions => $"{P(e.Player)}: acciones {e.From} → {e.Value}.",
            DuelHistoryEventKind.Pr => $"{P(e.Player)}: {(e.Value > 0 ? "+" : "")}{e.Value} PR.",
            DuelHistoryEventKind.ChainCancel => $"{Card(e.CardId)} de {P(e.Player)} anula {Card(e.OtherCardId)} de {P(e.Other)}.",
            DuelHistoryEventKind.CostsReturned => "Activación anulada: se devuelven sus costes.",
            DuelHistoryEventKind.BoxCancel => "Caja de Cartón anula el código y devuelve sus costes.",
            DuelHistoryEventKind.ZahiraCover => $"Zahira Flair de {P(e.Player)} cubre {Card(e.OtherCardId)} de {P(e.Other)}.",
            DuelHistoryEventKind.Winner => e.Player == -1 ? "El duelo termina en empate." : $"{P(e.Player)} gana el duelo.",
            _ => ""
        };
    }

    /// <summary>The outcome lines of several entries read together. A damage and the life change it caused read as
    /// one line ("Beto recibe 2 de daño (vidas 5 → 3).").</summary>
    public static List<string> Lines(IEnumerable<DuelHistoryEntry> entries, Func<int, string> player)
    {
        var lines = new List<string>();
        foreach (var entry in entries)
        {
            if (entry.Events.Count == 0) { lines.AddRange(entry.Details ?? []); continue; }
            var merged = new HashSet<DuelHistoryEvent>();
            foreach (var e in entry.Events)
            {
                if (merged.Contains(e)) continue;
                if (e.Kind == DuelHistoryEventKind.Damage &&
                    entry.Events.FirstOrDefault(h => h.Kind == DuelHistoryEventKind.Health && h.Player == e.Player && h.Value < h.From && !merged.Contains(h)) is { } health)
                {
                    merged.Add(health);
                    lines.Add($"{player(e.Player)} recibe {e.Value} de daño (vidas {health.From} → {health.Value}).");
                    continue;
                }
                lines.Add(Line(e, player));
            }
        }
        return lines;
    }
    public static List<string> Lines(DuelHistoryEntry entry, Func<int, string> player) => Lines([entry], player);

    /// <summary>The figures of a play as compact chips (dice, then each player's life, actions, PR and draws, added
    /// up across the play), and what remains to tell as lines. A damage or healing the life chip already shows is
    /// not told again.</summary>
    public static (List<DuelHistoryChip> Chips, List<string> Lines) Summary(IEnumerable<DuelHistoryEntry> entries, Func<int, string> player)
    {
        var dice = new List<DuelHistoryChip>(); var lines = new List<string>();
        var health = new Dictionary<int, (int From, int To)>(); var actions = new Dictionary<int, (int From, int To)>();
        var pr = new Dictionary<int, int>(); var draws = new Dictionary<int, int>();
        var list = entries.ToList();
        foreach (var e in list.SelectMany(entry => entry.Events))
            switch (e.Kind)
            {
                case DuelHistoryEventKind.Health: health[e.Player] = (health.TryGetValue(e.Player, out var h) ? h.From : e.From, e.Value); break;
                case DuelHistoryEventKind.Actions: actions[e.Player] = (actions.TryGetValue(e.Player, out var a) ? a.From : e.From, e.Value); break;
                case DuelHistoryEventKind.Pr: pr[e.Player] = pr.GetValueOrDefault(e.Player) + e.Value; break;
                case DuelHistoryEventKind.Draw: draws[e.Player] = draws.GetValueOrDefault(e.Player) + e.Value; break;
            }
        foreach (var entry in list)
        {
            if (entry.Events.Count == 0) { lines.AddRange(entry.Details ?? []); continue; }
            foreach (var e in entry.Events)
                switch (e.Kind)
                {
                    case DuelHistoryEventKind.Die: dice.Add(new(DuelHistoryChipKind.Die, -1, $"D6 {e.Value}", Line(e, player))); break;
                    case DuelHistoryEventKind.DieAdjusted: dice.Add(new(DuelHistoryChipKind.Die, -1, $"D6 → {e.Value}", Line(e, player))); break;
                    case DuelHistoryEventKind.Health or DuelHistoryEventKind.Actions or DuelHistoryEventKind.Pr or DuelHistoryEventKind.Draw: break;
                    case DuelHistoryEventKind.Damage when health.TryGetValue(e.Player, out var hit) && hit.To < hit.From: break;
                    case DuelHistoryEventKind.Heal when health.TryGetValue(e.Player, out var cure) && cure.To > cure.From: break;
                    default: lines.Add(Line(e, player)); break;
                }
        }
        var chips = dice;
        foreach (var seat in health.Keys.Concat(actions.Keys).Concat(pr.Keys).Concat(draws.Keys).Distinct().Order())
        {
            if (health.TryGetValue(seat, out var life) && life.From != life.To)
                chips.Add(new(DuelHistoryChipKind.Health, seat, $"{life.From} → {life.To}", $"{player(seat)}: vidas {life.From} → {life.To}."));
            if (actions.TryGetValue(seat, out var act) && act.From != act.To)
                chips.Add(new(DuelHistoryChipKind.Actions, seat, $"{act.From} → {act.To}", $"{player(seat)}: acciones {act.From} → {act.To}."));
            if (pr.GetValueOrDefault(seat) is var points && points != 0)
                chips.Add(new(DuelHistoryChipKind.Pr, seat, $"{(points > 0 ? "+" : "")}{points} PR", $"{player(seat)}: {(points > 0 ? "+" : "")}{points} PR."));
            if (draws.GetValueOrDefault(seat) is var drawn && drawn > 0)
                chips.Add(new(DuelHistoryChipKind.Draw, seat, $"+{drawn}", $"{player(seat)} roba {drawn} carta(s)."));
        }
        return (chips, lines);
    }
}

public enum DuelHistoryChipKind { Die, Health, Actions, Pr, Draw }
/// <summary>A figure of a play. <see cref="Player"/> is -1 for the shared die; <see cref="Description"/> reads it in full.</summary>
public sealed record DuelHistoryChip(DuelHistoryChipKind Kind, int Player, string Label, string Description);

public sealed partial class GameEngine
{
    internal DuelHistory? PublicHistory { get; set; }

    // An explicit public projection; never serialize the activation frames or their continuations.
    internal DuelResolutionView? PublicResolution()
    {
        var state = State;
        if (state.IsOver || state.Phase is Phase.Preparation or Phase.Draw or Phase.Combat or Phase.Actions or Phase.Final or Phase.Finished) return null;
        var frame = state.PendingDecision?.Source == CardCatalog.ComboParry.Id ? _activeActivation :
            _activationChain.FirstOrDefault() ?? _activeActivation;
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
    private readonly List<DuelHistoryEvent> _outcomes = [];
    private readonly Phase _stage;
    private string _card = "";
    private int _sourceActor;
    private readonly string _text;

    public PublicDuelObservation(GameEngine game, int actor, DuelMove move)
    {
        _game = game; _actor = actor; _move = move; _turn = game.State.Turn; _stage = DuelSnapshot.StageOf(game);
        _beforeResolution = game.PublicResolution();
        _before = game.State.Players.Select((p, i) => (p.Health, p.Relikia, game.AvailableActions(i), p.Equipment, p.EquipmentStackedCopies, p.ZahiraHost, p.ZahiraHost2)).ToArray();
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
    private void Add(DuelHistoryEventKind kind, int player = 0, int value = 0, int other = 0, string? card = null, int from = 0, string? otherCard = null)
        => _outcomes.Add(new DuelHistoryEvent { Kind = kind, Player = player, Value = value, Other = other, CardId = card, From = from, OtherCardId = otherCard });
    private void Die(int value) => Add(DuelHistoryEventKind.Die, value: value);
    private void Adjusted(int value) => Add(DuelHistoryEventKind.DieAdjusted, value: value);
    private void Damage(int player, int value, int die) => Add(DuelHistoryEventKind.Damage, player, value);
    private void Heal(int player, int value) => Add(DuelHistoryEventKind.Heal, player, value);
    private readonly int[] _draws = [0, 0];
    private void Draw(int player, int slot) { if (player is 0 or 1) _draws[player]++; }
    private void Discard(int player, int count) => Add(DuelHistoryEventKind.Discard, player, count);
    private void Transfer(int owner, int recipient, int count) => Add(DuelHistoryEventKind.Transfer, owner, count, recipient);
    private void Bonus(int owner, int recipient, int slot, string id) => Add(DuelHistoryEventKind.BonusClaimed, recipient, other: owner, card: id);
    private void Memory(int owner, int recipient, int index, int slot) => Add(DuelHistoryEventKind.MemoryClaimed, recipient, other: owner);
    private void TrapCharged(int owner, int count) => Add(DuelHistoryEventKind.TrapCharged, owner, count);
    private void Pile(PileMovement move)
    {
        // Hidden hand/deck moves and face-down corruption never contribute identities.
        if (move.Destination == CardZone.Memory && move.Source is CardZone.Equipment or CardZone.EquipmentCover or CardZone.LightEquipment or CardZone.Skill or CardZone.Bonus or CardZone.Area or CardZone.Exchange or CardZone.Attached or CardZone.Activation or CardZone.GalacticShield)
            Add(DuelHistoryEventKind.ToMemory, move.Owner, other: move.ToRivalMemory ? move.MemoryOwner : move.Owner, card: move.CardId);
        if (move.Destination == CardZone.OutOfGame && move.Source != CardZone.Hand)
            Add(DuelHistoryEventKind.OutOfGame, move.Owner, card: move.CardId);
        if (move.Source == CardZone.Deck && move.Destination == CardZone.Attached)
            Add(DuelHistoryEventKind.AttackPlaced, move.Owner);
        if (move.Destination == CardZone.LightEquipment)
            Add(DuelHistoryEventKind.LightEquipped, move.DestinationPlayer ?? move.Owner, card: move.CardId);
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
                var cancellations = new List<DuelHistoryEvent>();
                for (var i = chain.Count - 1; i > 0; i -= 2)
                    cancellations.Add(new DuelHistoryEvent { Kind = DuelHistoryEventKind.ChainCancel, Player = chain[i].Actor, CardId = chain[i].CardId,
                        Other = chain[i - 1].Actor, OtherCardId = chain[i - 1].CardId });
                _outcomes.InsertRange(0, cancellations);
                if (chain.Count % 2 == 0)
                { status = DuelResolutionStatus.Canceled; _outcomes.Insert(cancellations.Count, new DuelHistoryEvent { Kind = DuelHistoryEventKind.CostsReturned }); }
                _card = before.CardId; _sourceActor = before.Actor;
            }
        }
        if (_move.Kind == DuelMoveKind.CodeResponse && _move.Flag)
        { status = live?.Status ?? DuelResolutionStatus.Canceled; _card = before?.CardId ?? _card; _sourceActor = before?.Actor ?? _actor; _outcomes.Insert(0, new DuelHistoryEvent { Kind = DuelHistoryEventKind.BoxCancel }); }
        for (var i = 0; i < state.Players.Length; i++)
        {
            var p = state.Players[i]; var old = _before[i];
            if (p.Health != old.Health) Add(DuelHistoryEventKind.Health, i, p.Health, from: old.Health);
            var actions = _game.AvailableActions(i);
            if (actions > old.Actions) Add(DuelHistoryEventKind.Actions, i, actions, from: old.Actions);
            if (p.Relikia != old.Pr) Add(DuelHistoryEventKind.Pr, i, p.Relikia - old.Pr);
            if (_draws[i] > 0) Add(DuelHistoryEventKind.Draw, i, _draws[i]);
            if (p.Equipment != null && (p.Equipment != old.Equipment || p.EquipmentStackedCopies > old.Copies))
                Add(DuelHistoryEventKind.Equipped, i, card: p.Equipment);
            if (_move.Kind == DuelMoveKind.ZahiraTarget &&
                (p.ZahiraHost != old.ZahiraHost ? p.ZahiraHost : p.ZahiraHost2 != old.ZahiraHost2 ? p.ZahiraHost2 : null) is { } host)
                Add(DuelHistoryEventKind.ZahiraCover, i, other: host.Owner,
                    otherCard: host.Zone == FieldZone.Relikario ? state.Players[host.Owner].Relikario.Id : host.CardId);
        }
        if (state.Winner != null) Add(DuelHistoryEventKind.Winner, state.Winner.Value);
        var history = _game.PublicHistory ??= new();
        var id = ++history.Sequence;
        // Plays made while a resolution is open (responses, dice, confirmations) belong to it.
        var resolution = _beforeResolution != null && history.OpenResolution != 0 ? history.OpenResolution : id;
        history.OpenResolution = live != null ? resolution : 0;
        history.Entries.Add(new DuelHistoryEntry { Id = id, Turn = _turn, Actor = _actor, CardId = _card,
            SourceActor = _sourceActor, Text = _text, Events = _outcomes.Take(16).ToList(), Status = status,
            Resolution = resolution, Stage = _stage, Routine = IsRoutine(_move) });
        if (history.Entries.Count > DuelHistory.Capacity) history.Entries.RemoveRange(0, history.Entries.Count - DuelHistory.Capacity);
    }
    public void Dispose()
    {
        _game.DieRolled -= Die; _game.DieAdjusted -= Adjusted; _game.DamageReceived -= Damage;
        _game.HealingReceived -= Heal; _game.CardDrawn -= Draw; _game.HandDiscarded -= Discard; _game.CardMovedToPile -= Pile;
        _game.HandTransferred -= Transfer; _game.BonusClaimed -= Bonus; _game.MemoryCardClaimed -= Memory;
        _game.SuperMagnetCharged -= TrapCharged;
    }
    /// <summary>Plays that only move the duel along; what they settle is told by their outcomes.</summary>
    private static bool IsRoutine(DuelMove m) => m.Kind switch
    {
        DuelMoveKind.Advance or DuelMoveKind.PassError or DuelMoveKind.PassDefense or DuelMoveKind.EffectDamage or DuelMoveKind.ComboResponses
            or DuelMoveKind.SkipMagnet => true,
        DuelMoveKind.CodeResponse or DuelMoveKind.AttackDie or DuelMoveKind.EffectDie or DuelMoveKind.Totem or DuelMoveKind.TrapCraft
            or DuelMoveKind.BlueEggDraw => !m.Flag,
        DuelMoveKind.ProtectDestruction => m.Index < 0,
        _ => false
    };
    private static string? PublicSource(GameEngine g, int actor, DuelMove m)
    {
        if (actor is not (0 or 1)) return null;
        var p = g.State.Players[actor];
        // Only these hand plays announce their identity to everybody on acceptance.
        if (m.Kind is DuelMoveKind.PlayCode or DuelMoveKind.ActivateArea or DuelMoveKind.Equip or DuelMoveKind.Learn or DuelMoveKind.Exchange)
            return p.Hand.ElementAtOrDefault(m.Index);
        return m.Kind switch
        {
            DuelMoveKind.Attack or DuelMoveKind.DestinyDefense => p.Skills.ElementAtOrDefault(m.Kind == DuelMoveKind.Attack ? m.Value : m.Index),
            DuelMoveKind.Relikia when m.Value == (int)RelikiaEffect.ReplayCode => p.Memory.ElementAtOrDefault(m.Index),
            DuelMoveKind.DestinyPickMemory => p.Memory.ElementAtOrDefault(m.Index),
            DuelMoveKind.FieldCode => m.Value == (int)FieldZone.Bonus ? p.Bonus : p.Exchange.ElementAtOrDefault(m.Index),
            DuelMoveKind.BoxDefense or DuelMoveKind.CodeResponse when m.Flag => CardCatalog.CardboardBox.Id,
            DuelMoveKind.Pc => CardCatalog.Pc.Id, DuelMoveKind.Pendant => CardCatalog.Pendant.Id,
            DuelMoveKind.UsePurpleRing => CardCatalog.PurpleRing.Id,
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
        var card = DuelHistoryText.Card(source);
        string Target() => m.Target is 0 or 1 ? g.State.Players[m.Target].Name : "el objetivo";
        return m.Kind switch
        {
            DuelMoveKind.PlayCode or DuelMoveKind.ActivateArea => $"{name} activa {card}.",
            DuelMoveKind.Attack when actor is 0 or 1 && g.State.Players[actor].Hand.ElementAtOrDefault(m.Index) == CardCatalog.SuperAttack.Id
                => $"{name} usa Súper Ataque con {card}.",
            DuelMoveKind.Attack => $"{name} ataca con {card}.",
            DuelMoveKind.Learn => $"{name} enseña {card}.",
            DuelMoveKind.Equip when m.Flag => $"{name} juega {card} como equipo para {g.State.Players[1 - actor].Name}.",
            DuelMoveKind.Equip => $"{name} juega {card} como equipo.",
            DuelMoveKind.Exchange => $"{name} coloca {card} en canje.", DuelMoveKind.FieldCode => $"{name} activa {card} desde {(m.Value == (int)FieldZone.Bonus ? "Bonus" : "canje")}.",
            DuelMoveKind.PassError => $"{name} deja resolver la cadena.",
            DuelMoveKind.CodeResponse => m.Flag ? $"{name} responde con Caja de Cartón." : $"{name} deja resolver el código.",
            DuelMoveKind.PassDefense or DuelMoveKind.EffectDamage => $"{name} confirma recibir el daño.",
            DuelMoveKind.BoxDefense => $"{name} cancela el daño con Caja de Cartón.", DuelMoveKind.DestinyDefense => $"{name} se defiende con {card}.",
            DuelMoveKind.AttackDie or DuelMoveKind.EffectDie => m.Flag ? $"{name} relanza con {(actor is 0 or 1 ? g.State.Players[actor].Relikario.DisplayName : "su Relikario")}." : $"{name} confirma el resultado del dado.",
            DuelMoveKind.Pc or DuelMoveKind.Pendant => $"{name} modifica el dado con {card}.",
            DuelMoveKind.UsePurpleRing => $"{name} usa {card} para ganar 500 PR.",
            DuelMoveKind.Dexterity => $"{name} usa una ficha de destreza.", DuelMoveKind.LearningDie => $"{name} relanza por Reverse Shuffle.",
            DuelMoveKind.Discard or DuelMoveKind.TurnBuffDiscard => $"{name} descarta una carta.",
            DuelMoveKind.Points => $"{name} {(m.Flag ? "aumenta" : "reduce")} los PR de {Target()}.",
            DuelMoveKind.Destroy => $"{name} elige destruir una carta de {Target()} ({(FieldZone)m.Value switch { FieldZone.Hand => "mano al azar", FieldZone.Bonus => "Bonus", FieldZone.Equipment => "equipo", _ => "campo" }}).",
            DuelMoveKind.Choose => g.DecisionChoices(actor).ElementAtOrDefault(m.Index) is { } choice ? $"{name} {choice.Public}" : $"{name} decide.",
            DuelMoveKind.BlueEggDraw => m.Flag ? $"{name} paga {GameEngine.BlueEggDrawCost} PR para robar con Huevo Azul Explosivo." : $"{name} continúa sin robar con Huevo Azul Explosivo.",
            DuelMoveKind.Magnet => $"{name} elige tomar una carta {(m.Value == (int)FieldZone.Area ? "del Área rival" : m.Flag ? "del Bonus rival" : "de la mano rival")} por Magnet Shuffle.",
            DuelMoveKind.SkipMagnet => $"{name} decide no tomar una carta por Magnet Shuffle.",
            DuelMoveKind.Placement => $"{name} coloca una carta {(m.Value == (int)CardDestination.Exchange ? "en canje" : "al fondo del mazo")}.",
            DuelMoveKind.Relikia => $"{name} activa Relikia: {RelikiaEffects.All.FirstOrDefault(e => e.Id == (RelikiaEffect)m.Value)?.Name ?? "efecto"}.",
            DuelMoveKind.DestinyPickMemory => $"{name} usa Púa del Destino desde su memoria.",
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
