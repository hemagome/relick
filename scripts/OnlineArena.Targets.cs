using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    private MenuButton? _arenaMenu;

    private void BuildArenaMenu(Node header)
    {
        var s = _snapshot!;
        // The desktop rail keeps its width with three symbols beside the COMBO plate.
        var icon = new Vector2(Desktop ? 44 : 52, 48);
        // Conceding sits beside the menu as a white flag; it still asks for confirmation.
        if (s.Options.Any(o => o.Move.Kind == DuelMoveKind.Concede))
        {
            var concede = new IconButton { Name = "ConcedeButton", Symbol = GameIcon.WhiteFlag, CustomMinimumSize = icon,
                TooltipText = "Abandonar duelo", AccessibilityName = "Abandonar duelo" };
            concede.Pressed += () => SendKind(DuelMoveKind.Concede);
            header.AddChild(concede);
        }
        var sound = new IconButton { Name = "SoundButton", Symbol = GameIcon.Speaker, CustomMinimumSize = icon,
            TooltipText = "Sonido", AccessibilityName = "Sonido" };
        sound.Pressed += () => GetNode<AudioPreferences>("/root/AudioPreferences").OpenControls();
        header.AddChild(sound);
        _arenaMenu = new IconMenuButton { Name = "ArenaMenu", CustomMinimumSize = icon,
            TooltipText = "Menú", AccessibilityName = "Menú" };
        header.AddChild(_arenaMenu); var popup = _arenaMenu.GetPopup(); ArenaViewport.TouchMenu(popup);
        if (Desktop)
        {
            popup.AddCheckItem("Vista de mesa", TableViewMenuId);
            popup.SetItemChecked(popup.GetItemIndex(TableViewMenuId), _requestedTableView);
            popup.SetItemTooltip(popup.GetItemIndex(TableViewMenuId), "Marcada: mesa con profundidad. Desmarcada: vista clásica.");
        }
        if (!_preparing) popup.AddItem("Historial de jugadas", 3);
        if (s.Winner != null || _preparing) popup.AddItem(s.Winner != null ? "Volver al lobby" : "Cerrar sala", 2);
        if (s.Winner != null) popup.AddItem("Ver resultado", 4);
        popup.IdPressed += id =>
        {
            if (id == TableViewMenuId) ToggleTableView(popup);
            if (id == 3) ShowDuelHistory();
            if (id == 2) { if (s.Winner != null) RequestResultExit(); else Connection.Leave(RoomCode); }
            if (id == 4) { _resultDismissed = false; UpdateResultPresentation(); }
        };
    }

    private bool MatchesField(DuelOption option, (int Owner, FieldZone Zone, int Index) field) => MatchesField(option.Move, field, option);

    private bool MatchesField(DuelMove move, (int Owner, FieldZone Zone, int Index) field, DuelOption? option = null)
    {
        var s = _snapshot!;
        return move.Kind switch
        {
            DuelMoveKind.IronwingFire or DuelMoveKind.IronwingRoll =>
                field.Owner == s.MySeat && field.Zone == FieldZone.Skill && field.Index == move.Index,
            DuelMoveKind.Destroy => move.Target == field.Owner && move.Value == (int)field.Zone &&
                (move.Index == field.Index || field.Zone == FieldZone.Hand && move.Index < 0),
            // A card decision names its board target; a hand is one zone, its card drawn at random.
            DuelMoveKind.Choose or DuelMoveKind.UsePurpleRing => option?.Field is { } t && t.Owner == field.Owner && t.Zone == field.Zone &&
                (t.Index == field.Index || field.Zone == FieldZone.Hand && t.Index < 0 ||
                 field.Zone == FieldZone.Equipment && field.Index < 0 && s.Decision?.Source == CardCatalog.VictoriousManualControl.Id),
            DuelMoveKind.FieldCode => field.Owner == s.MySeat && move.Value == (int)field.Zone && move.Index == field.Index,
            DuelMoveKind.Magnet => field.Owner != s.MySeat && (move.Flag
                ? field.Zone == (move.Value == (int)FieldZone.Area ? FieldZone.Area : FieldZone.Bonus)
                : field.Zone == FieldZone.Hand && move.Index == field.Index),
            DuelMoveKind.Pc or DuelMoveKind.Pendant or DuelMoveKind.BoxDefense => field.Owner == s.MySeat && field.Zone == FieldZone.Equipment,
            DuelMoveKind.CodeResponse or DuelMoveKind.Totem => move.Flag && field.Owner == s.MySeat && field.Zone == FieldZone.Equipment,
            _ => false
        };
    }

    private bool IsFieldActivation(DuelOption option) => option.Move.Kind switch
    {
        DuelMoveKind.FieldCode or DuelMoveKind.UsePurpleRing or DuelMoveKind.Pc or DuelMoveKind.Pendant or DuelMoveKind.BoxDefense => true,
        DuelMoveKind.CodeResponse or DuelMoveKind.Totem => option.Move.Flag,
        DuelMoveKind.Choose => _snapshot!.Decision?.Source == CardCatalog.PurpleRing.Id && option.Field?.Zone == FieldZone.LightEquipment,
        _ => false
    };

    private string? FieldCardId((int Owner, FieldZone Zone, int Index) field)
    {
        var p = _snapshot!.Players[field.Owner];
        return field.Zone switch
        {
            FieldZone.Equipment => p.Equipment, FieldZone.EquipmentCover => p.EquipmentCovers?.ElementAtOrDefault(field.Index),
            FieldZone.LightEquipment => p.LightEquipment?.ElementAtOrDefault(field.Index),
            FieldZone.Bonus => p.Bonus, FieldZone.Area => p.Area, FieldZone.Exchange => p.Exchange.ElementAtOrDefault(field.Index),
            FieldZone.Hand => p.Hand.ElementAtOrDefault(field.Index), FieldZone.Skill => p.Skills.ElementAtOrDefault(field.Index),
            FieldZone.Attached => p.Attached.ElementAtOrDefault(field.Index),
            FieldZone.GalacticShield when field.Index >= 0 && field.Index < p.GalacticShieldCharges?.Length => CardCatalog.GalacticShield.Id,
            FieldZone.Zahira when p.Zahira(GameEngine.ZahiraSlot(field.Index)) != null => CardCatalog.ZahiraFlair.Id, _ => null
        };
    }

    private void SelectField(int owner, FieldZone zone, int index)
    {
        if (IsAnimating) return;
        if (zone == FieldZone.Hand && _snapshot!.Options.Any(o => o.Move.Kind == DuelMoveKind.Destroy && o.Move.Target == owner && o.Move.Value == (int)FieldZone.Hand && o.Move.Index < 0 ||
            o.Move.Kind == DuelMoveKind.Choose && o.Field is { Zone: FieldZone.Hand, Index: < 0 } hand && hand.Owner == owner)) index = -1;
        var field = (owner, zone, index);
        if (_snapshot!.Options.Any(o => MatchesField(o, field)) || FieldCardId(field) != null)
        {
            _selected.Clear(); _skillContext = false; _choosingAttackSkill = false; _fieldSelection = field; RefreshSelection();
        }
        else if (zone == FieldZone.Hand) ShowCards("Mano rival", _snapshot.Players[owner].Hand, relikarioOf: index => HandArtworkRelikario(owner, index));
    }

    private void SelectEquipment(int owner) => SelectField(owner, FieldZone.Equipment, -1);

    private void SelectFieldPile(int owner, FieldZone zone)
    {
        var player = _snapshot!.Players[owner];
        var ids = zone switch
        {
            FieldZone.EquipmentCover => player.EquipmentCovers ?? [],
            FieldZone.LightEquipment => player.LightEquipment ?? [],
            _ => player.Attached
        };
        var targetable = _snapshot.Options.Any(o => o.Move.Kind == DuelMoveKind.Destroy && o.Move.Target == owner && o.Move.Value == (int)zone ||
            o.Move.Kind is DuelMoveKind.Choose or DuelMoveKind.UsePurpleRing && o.Field is { } t && t.Owner == owner && t.Zone == zone);
        if (ids.Length == 1 && (targetable || zone == FieldZone.LightEquipment)) { SelectField(owner, zone, 0); return; }
        var relikario = player.RelikarioId;
        var title = zone switch
        {
            FieldZone.EquipmentCover => "Sobre el equipo",
            FieldZone.LightEquipment => $"Equipos ligeros · {player.Name}",
            _ => AttachmentStack.Location(ids, RelikarioCatalog.DisplayNameOf(relikario))
        };
        ShowCards(title, ids, select: targetable ? index => SelectField(owner, zone, index) : null, relikarioOf: _ => relikario);
    }

    private void SelectSkill(int index)
    {
        if (IsAnimating) return;
        var s = _snapshot!;
        if (s.Options.Any(option => MatchesField(option, (s.MySeat, FieldZone.Skill, index))))
        {
            SelectField(s.MySeat, FieldZone.Skill, index);
            return;
        }
        var attacks = SelectedAttacks().ToArray();
        if (_choosingAttackSkill)
        {
            var attack = attacks.FirstOrDefault(entry => entry.Option.Move.Value == index);
            if (attack.Option != null) PlayOption(attack.Index, s);
            return;
        }
        _selectedSkill = index; _fieldSelection = null;
        var attacking = attacks.Length > 0;
        _skillContext = !attacking;
        if (!attacking && !s.Options.Any(o => o.Move.Kind == DuelMoveKind.DestinyDefense)) _selected.Clear();
        RefreshSelection();
    }

    private void RefreshBoardTargets()
    {
        var s = _snapshot!;
        var available = SelectedAttacks().Select(entry => entry.Option.Move.Value).Distinct().ToArray();
        var attack = available.Length > 0;
        if (!attack) _choosingAttackSkill = false;
        if (attack && !available.Contains(_selectedSkill)) _selectedSkill = available.Length == 1 ? available[0] : -1;
        _pulsingTargets.Clear();
        var showActivations = ShowCardActivations;
        // Teaching appends the new skill to the list, independently of its board position.
        var learningReroll = s.Options.Any(o => o.Move.Kind == DuelMoveKind.LearningDie)
            ? s.Players[s.MySeat].Skills.Length - 1 : -1;
        foreach (var tile in _ownSkills)
        {
            var field = (s.MySeat, FieldZone.Skill, tile.SkillIndex);
            tile.Targetable = tile.Skill != null && (available.Contains(tile.SkillIndex) || s.Options.Any(option => MatchesField(option, field)));
            tile.Playable = showActivations && tile.Skill != null &&
                (tile.SkillIndex == learningReroll && tile.Skill.Id == CardCatalog.Reverse.Id || s.Options.Any(o =>
                    o.Move.Kind is DuelMoveKind.DestinyDefense or DuelMoveKind.ProtectDestruction or DuelMoveKind.IronwingRoll or DuelMoveKind.IronwingFire &&
                    o.Move.Index == tile.SkillIndex));
            tile.Chosen = tile.Skill != null && (_fieldSelection == field || tile.SkillIndex == _selectedSkill && (attack || _skillContext));
            if (tile.Targetable) _pulsingTargets.Add(tile);
            tile.QueueRedraw();
        }
        for (var relative = 0; relative < 2; relative++)
        {
            var owner = relative == 0 ? s.MySeat : 1 - s.MySeat;
            void Mark(ZoneCardButton? node, FieldZone zone, int index)
            {
                if (node == null) return;
                bool MatchesZone(DuelOption option)
                {
                    if (zone != FieldZone.LightEquipment) return MatchesField(option, (owner, zone, index));
                    var slot = option.Field?.Index ?? option.Move.Index;
                    return slot >= 0 && slot < s.Players[owner].LightEquipment?.Length && MatchesField(option, (owner, zone, slot));
                }
                node.Playable = showActivations && owner == s.MySeat && node.Card != null &&
                    s.Options.Any(o => IsFieldActivation(o) && MatchesZone(o));
                node.Targetable = s.Options.Any(o => !IsFieldActivation(o) && MatchesZone(o));
                node.Chosen = zone == FieldZone.LightEquipment
                    ? _fieldSelection is { } selected && selected.Owner == owner && selected.Zone == zone
                    : _fieldSelection == (owner, zone, index);
                node.QueueRedraw();
                if (node.Targetable) _pulsingTargets.Add(node);
            }
            Mark(_equipmentZones[relative], FieldZone.Equipment, -1);
            Mark(ZoneNode(relative, CardZone.LightEquipment, -1) as ZoneCardButton, FieldZone.LightEquipment, -1);
            var covers = s.Players[owner].EquipmentCovers;
            Mark(_coverZones[relative], FieldZone.EquipmentCover, (covers?.Length ?? 0) - 1);
            for (var slot = 0; slot < (s.Players[owner].GalacticShieldCharges?.Length ?? 0); slot++)
                Mark(ZoneNode(relative, CardZone.GalacticShield, slot) as ZoneCardButton, FieldZone.GalacticShield, slot);
            if (_exchangeTracks[relative] is { } track)
            {
                Mark(track.BonusButton, FieldZone.Bonus, -1);
                Mark(track.AreaButton, FieldZone.Area, -1);
                for (var i = 0; i < track.ExchangeButtons.Length; i++) Mark(track.ExchangeButtons[i], FieldZone.Exchange, i);
            }
        }
        foreach (var card in _opponentCards)
        {
            var index = int.Parse(card.Name.ToString()[16..]);
            card.Targetable = s.Options.Any(o => MatchesField(o, (1 - s.MySeat, FieldZone.Hand, index)));
            card.Chosen = _fieldSelection is { } field && field.Owner != s.MySeat && field.Zone == FieldZone.Hand && (field.Index < 0 || field.Index == index);
            if (card.Targetable) _pulsingTargets.Add(card);
            card.QueueRedraw();
        }
    }
}
