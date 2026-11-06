using Godot;
using Relick.Core;

namespace Relick;

public partial class Main
{
    private void ShowPendingSkillChoice()
    {
        if (_helpOpen || _die.IsBusy || CardsInMotion || _game.State.DecisionPlayer != 0) return;
        if (_game.State.Phase == Phase.ErrorResponse) { ShowLoadErrorResponse(); return; }
        if (_game.State.Phase is Phase.AttackRerollChoice or Phase.DieRerollChoice) ShowYunoReroll();
        else if (_game.State.Phase == Phase.DestroyChoice) ShowDestructionChoice();
        else if (_game.State.Phase == Phase.PointsChoice) ShowPointsChoice();
        else if (_game.State.Phase == Phase.CodeResponse) ShowBoxCodeResponse();
        else if (_game.State.Phase == Phase.DefenseChoice) ShowDestinyDefense();
        else if (_game.State.Phase == Phase.MagnetChoice) ShowMagnetChoice();
    }

    private AcceptDialog SkillChoiceDialog(string title, Control content, string pass, Action onPass)
    {
        _helpOpen = true;
        var dialog = new AcceptDialog { Name = "SkillChoiceDialog", Title = title, MinSize = new(700, 480), OkButtonText = pass };
        dialog.AddChild(content);
        void Pass() { CloseRelikiaDialog(dialog); onPass(); }
        dialog.Confirmed += Pass; dialog.Canceled += Pass;
        AddChild(dialog); dialog.PopupCentered();
        return dialog;
    }

    private void ShowYunoReroll()
    {
        var attack = _game.State.Phase == Phase.AttackRerollChoice;
        var die = attack ? _game.State.PendingAttackRoll!.Die : _game.State.PendingDieRoll!.Die;
        var count = attack ? _game.State.PendingAttackRoll!.Rerolls : _game.State.PendingDieRoll!.Rerolls;
        var owner = _game.State.Players[0];
        var purpose = attack ? "ATAQUE" : _game.State.PendingDieRoll!.Purpose switch
        {
            DiePurpose.MagnetDamage => "DAÑO DE MAGNET", DiePurpose.Recycling => "RECICLAJE", DiePurpose.ExtraLife => "VIDA EXTRA · CURACIÓN", DiePurpose.PointsChange => "CAMBIO DE PR", _ => "EFECTO DE RELIKIA"
        };
        var column = new VBoxContainer { CustomMinimumSize = new(660, 0) };
        column.AddThemeConstantOverride("separation", 18);
        column.AddChild(Label($"DADO · {purpose} · {die}", 26, Gold));
        column.AddChild(Wrapped(owner.Relikario.AbilityText, 18, Ink, 650));
        column.AddChild(Label($"Relanzamientos disponibles: {owner.Relikario.MaxRerolls - count}", 16, Mint));
        column.AddChild(Wrapped("El nuevo resultado sustituye al anterior, aunque sea peor. El efecto se resuelve cuando conserves el dado o terminen los relanzamientos.", 16, Muted, 650));
        AcceptDialog dialog = null!;
        void Resolve(bool use)
        {
            CloseRelikiaDialog(dialog);
            Execute(attack ? _game.ResolveAttackReroll(0, use) : _game.ResolveDieReroll(0, use));
        }
        var reroll = Button("Relanzar con Yuno", () => Resolve(true), !_game.CanUseYunoOnPendingDie(0));
        reroll.Name = "YunoReroll";
        column.AddChild(reroll);
        column.AddChild(Label($"Tu cupo de acciones: {owner.ActionsLeft}/2 · Re-lanza no gasta acciones", 12, Muted));
        var comboIndex = owner.Hand.FindIndex(id => CardCatalog.Get(id).RerollOwnDie && _game.CodeUnavailableReason(0, owner.Hand.IndexOf(id)) == null);
        if (comboIndex >= 0)
        {
            var combo = Button("Re-lanza · COMBO · Gratis", () => { CloseRelikiaDialog(dialog); Execute(_game.PlayCode(0, comboIndex)); });
            combo.Name = "PlayRerollCombo"; column.AddChild(combo);
        }
        if (owner.Equipment == CardCatalog.Pendant.Id)
        {
            column.AddChild(Wrapped("Colgante de la Suerte: se destruye al elegir uno de sus efectos y pasa a memoria. La suma solo se permite después de relanzar y no puede alcanzar 6 al atacar.", 14, Gold, 650));
            var equipmentEffects = new HBoxContainer();
            foreach (var (effect, text) in new[] { (PendantEffect.Reroll, "Colgante · Relanzar"), (PendantEffect.AddOne, "Colgante · +1"), (PendantEffect.AddTwo, "Colgante · +2") })
            {
                var button = Button(text, () =>
                {
                    CloseRelikiaDialog(dialog);
                    Execute(_game.UsePendant(0, effect));
                }, !_game.CanUsePendant(0, effect));
                button.Name = $"Pendant{effect}";
                equipmentEffects.AddChild(button);
            }
            column.AddChild(equipmentEffects);
        }
        dialog = SkillChoiceDialog($"Yuno Lv {owner.Level} · Relanzar dado", column, $"Conservar el {die}", () =>
            Execute(attack ? _game.ResolveAttackReroll(0, false) : _game.ResolveDieReroll(0, false)));
        dialog.MinSize = new(700, 370);
    }

    private void ShowDestinyDefense()
    {
        var attack = _game.State.PendingAttack!;
        AcceptDialog dialog = null!;
        var column = new VBoxContainer { CustomMinimumSize = new(660, 0) };
        column.AddThemeConstantOverride("separation", 10);
        column.AddChild(Wrapped($"Ataque {(attack.Critical ? "crítico" : "normal/reducido")} de {attack.Damage} de daño. Elige una respuesta disponible o recibe el daño.", 16, Ink, 650));
        var hasBox = _game.CanCancelAttackWithBox(0);
        if (hasBox)
        {
            var box = Button("Destruir Caja de Cartón · Cancelar daño", () =>
            {
                CloseRelikiaDialog(dialog);
                Execute(_game.CancelAttackWithBox(0));
            });
            box.Name = "BoxCancelAttack"; column.AddChild(box);
            column.AddChild(Wrapped("La Caja pasa a memoria. No descarta cartas ni consume PR o acciones adicionales.", 14, Gold, 650));
        }
        var available = _game.AvailableDefenses(0).ToArray();
        if (available.Length > 0)
        {
            column.AddChild(Wrapped($"Destiny requiere {_game.DefenseDiscardCost} descartes y añade un contador a la copia elegida.", 14, Muted, 650));
            var copies = new HBoxContainer();
            var slot = available.FirstOrDefault(-1);
            foreach (var i in available)
            {
                var index = i;
                var button = new CheckButton { Name = $"DefendWithSlot{i}", Text = $"Espacio {i + 1} · {_game.State.Players[0].CountersOnSkill(i)}/2", ButtonPressed = i == slot };
                button.Pressed += () =>
                {
                    slot = index;
                    foreach (var other in copies.GetChildren().OfType<CheckButton>()) other.ButtonPressed = other == button;
                };
                copies.AddChild(button);
            }
            column.AddChild(copies);
            var selected = new HashSet<int>();
            var scroll = new ScrollContainer { CustomMinimumSize = new(660, 204), VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
            var cards = new HBoxContainer(); cards.AddThemeConstantOverride("separation", 8);
            scroll.AddChild(cards); column.AddChild(scroll);
            var confirm = Button($"Cancelar daño · 0/{_game.DefenseDiscardCost} descartes", () =>
            {
                var result = _game.ResolveDefense(0, slot, selected);
                if (result.Success) CloseRelikiaDialog(dialog);
                Execute(result);
            }, true);
            confirm.Name = "ConfirmDestiny";
            var hand = _game.State.Players[0].Hand;
            for (var i = 0; i < hand.Count; i++)
            {
                var index = i;
                var card = new HandCard { Name = $"DefenseDiscard{i}", Card = CardCatalog.Get(hand[i]) };
                card.Pressed += () =>
                {
                    if (!selected.Remove(index)) selected.Add(index);
                    card.Chosen = selected.Contains(index); card.QueueRedraw();
                    confirm.Text = $"Cancelar daño · {selected.Count}/{_game.DefenseDiscardCost} descartes";
                    confirm.Disabled = selected.Count != _game.DefenseDiscardCost;
                };
                cards.AddChild(card);
            }
            column.AddChild(confirm);
        }
        dialog = SkillChoiceDialog(hasBox ? "Defender · Caja de Cartón / Destiny" : "Destiny Shuffle · Cancelar daño", column, "Recibir el daño", () => Execute(_game.PassDefense(0)));
    }

    private void ShowBoxCodeResponse()
    {
        var pending = _game.State.PendingCode!;
        var card = CardCatalog.Get(pending.CardId);
        var column = new VBoxContainer { CustomMinimumSize = new(660, 0) };
        column.AddThemeConstantOverride("separation", 16);
        column.AddChild(Label("CÓDIGO OBJETO · RESPUESTA", 22, Gold));
        column.AddChild(Wrapped($"El rival activa {card.Name}: {card.Text}", 18, Ink, 650));
        column.AddChild(Wrapped("Puedes destruir Caja de Cartón para cancelar esta activación. La Caja va a tu memoria y el código queda en la memoria de su dueño sin resolver su efecto.", 16, Muted, 650));
        AcceptDialog dialog = null!;
        var cancel = Button("Destruir Caja de Cartón · Cancelar código", () =>
        {
            CloseRelikiaDialog(dialog);
            Execute(_game.ResolveCodeResponse(0, true));
        });
        cancel.Name = "BoxCancelCode"; column.AddChild(cancel);
        dialog = SkillChoiceDialog("Caja de Cartón · Código objeto", column, "Dejar resolver el código", () => Execute(_game.ResolveCodeResponse(0, false)));
        dialog.MinSize = new(700, 350);
    }

    private void ShowMagnetChoice()
    {
        var column = new VBoxContainer { CustomMinimumSize = new(660, 0) };
        column.AddThemeConstantOverride("separation", 12);
        column.AddChild(Wrapped("Crítico de Magnet Shuffle: puedes mirar la mano del rival y tomar una carta de ella o de su Bonus. Las demás cartas vuelven a quedar ocultas al cerrar esta elección.", 16, Ink, 650));
        AcceptDialog dialog = null!;
        void Take(bool bonus, int index = -1)
        {
            var result = _game.ResolveMagnet(0, bonus, index);
            if (result.Success) CloseRelikiaDialog(dialog);
            Execute(result);
        }
        var rival = _game.State.Players[1];
        var bonusButton = Button(rival.Bonus == null ? "Bonus vacío" : $"Tomar Bonus · {CardCatalog.Get(rival.Bonus).Name}", () => Take(true), rival.Bonus == null);
        bonusButton.Name = "MagnetTakeBonus";
        column.AddChild(bonusButton);
        column.AddChild(Label("MANO DEL RIVAL · ELIGE UNA CARTA", 12, Coral));
        var scroll = new ScrollContainer { CustomMinimumSize = new(660, 230), VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var cards = new HBoxContainer(); cards.AddThemeConstantOverride("separation", 8);
        for (var i = 0; i < rival.Hand.Count; i++)
        {
            var index = i;
            var card = new HandCard { Name = $"MagnetTakeHand{i}", Card = CardCatalog.Get(rival.Hand[i]) };
            card.Pressed += () => Take(false, index); cards.AddChild(card);
        }
        if (rival.Hand.Count == 0) cards.AddChild(Label("Sin cartas en mano", 14, Muted));
        scroll.AddChild(cards); column.AddChild(scroll);
        dialog = SkillChoiceDialog("Magnet Shuffle · Tomar una carta", column, "No tomar carta", () => Execute(_game.SkipMagnet(0)));
    }
}
