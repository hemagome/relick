using Godot;
using Relick.Core;

namespace Relick;

public partial class Main
{
    private async void EquipmentSmokeTest()
    {
        try
        {
            SetProcess(false); _die.SetProcess(false); _yuno.SetProcess(false); _enemyYuno.SetProcess(false);
            void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
            var rng = new SkillsTestRandom();
            BindGame(new GameEngine(rng));
            var s = _game.State; s.Phase = Phase.Actions; s.CurrentPlayer = 0; s.ActionsLeft = 2;
            var p = s.Active; p.Hand.Clear(); p.Hand.AddRange(new[] { CardCatalog.Pendant.Id, CardCatalog.Pendant.Id });
            _helpOpen = false; Render();
            async System.Threading.Tasks.Task Layout()
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            AcceptDialog Dialog() => GetChildren().OfType<AcceptDialog>().Single(d => !d.IsQueuedForDeletion());
            void Press(string name) => ((Button)Dialog().FindChild(name, true, false)).EmitSignal(BaseButton.SignalName.Pressed);
            await Layout();
            Check(CardCatalog.All.Count == 11 && CardCatalog.All.Values.All(c => !c.Name.Contains("práctica")), "El catálogo del juego conserva cartas de ejemplo.");
            var hand = (HandCard)_root.FindChild("HandCard0", true, false);
            Check(hand.HasEquipmentArtwork, "El Colgante no usa su imagen limpia.");
            hand.EmitSignal(BaseButton.SignalName.Pressed); Check(CanUseSelected() && SelectedVerb().Contains("Poner equipo"), "No se puede poner el equipo desde la mano.");
            UseSelected(); await Layout();
            Check(p.Equipment == CardCatalog.Pendant.Id && s.ActionsLeft == 1 && ((ZoneCardButton)_root.FindChild("EquipmentZone0", true, false)).Card?.Id == CardCatalog.Pendant.Id,
                "El equipo visible no coincide con el equipo real o no consume una acción.");
            ShowEquipment(0); Check(((HandCard)Dialog().FindChild("ZoneCardPreview", true, false)).HasEquipmentArtwork, "No se puede ampliar el Colgante equipado.");
            Dialog().EmitSignal(AcceptDialog.SignalName.Confirmed); await Layout();
            ((HandCard)_root.FindChild("HandCard0", true, false)).EmitSignal(BaseButton.SignalName.Pressed); UseSelected(); await Layout();
            Check(p.Hand.Count == 0 && p.Memory.Single() == CardCatalog.Pendant.Id && p.Equipment == CardCatalog.Pendant.Id && s.ActionsLeft == 0, "Reemplazar equipo no destruye el anterior.");
            p.Hand.Add("attack"); p.Skills.Add(CardCatalog.Destiny.Id); s.Phase = Phase.Combat;
            rng.Enqueue(3); Execute(_game.Attack(0, 0, 0)); DrainTestDice(); ShowPendingSkillChoice();
            Check(((Button)Dialog().FindChild("PendantAddTwo", true, false)).Disabled, "Se permite sumar a un dado que no fue relanzado.");
            rng.Enqueue(3); Press("YunoReroll"); DrainTestDice(); await Layout(); ShowPendingSkillChoice();
            Check(!((Button)Dialog().FindChild("PendantAddTwo", true, false)).Disabled && ((Button)Dialog().FindChild("YunoReroll", true, false)).Disabled,
                "Tras usar Yuno, no se permite el efecto adecuado del Colgante.");
            Press("PendantAddTwo"); await Layout();
            Check(s.LastAttackDie == 5 && p.Equipment == null && p.Memory.Count(id => id == CardCatalog.Pendant.Id) == 2 && s.Players[1].Health == 5,
                "El +2 no destruye el equipo o no modifica el ataque.");
            Check(((ZoneCardButton)_root.FindChild("EquipmentZone0", true, false)).Card == null, "El equipo destruido permanece en su zona visual.");

            BindGame(new GameEngine(rng)); s = _game.State; s.Phase = Phase.Actions; s.CurrentPlayer = 0; s.ActionsLeft = 2;
            p = s.Active; p.Hand.Clear(); p.Hand.Add(CardCatalog.Pendant.Id); _game.Equip(0, 0); p.Hand.Add("attack"); p.Skills.Add(CardCatalog.Destiny.Id);
            s.Phase = Phase.Combat; rng.Enqueue(1); Execute(_game.Attack(0, 0, 0)); DrainTestDice(); ShowPendingSkillChoice();
            Check(((Button)Dialog().FindChild("YunoReroll", true, false)).Disabled && !((Button)Dialog().FindChild("PendantReroll", true, false)).Disabled,
                "El Colgante no permite relanzar un 1 independientemente de Yuno.");
            rng.Enqueue(6); Press("PendantReroll");
            Check(_die.RollCaption.Contains("Colgante") && !_die.RollCaption.Contains("convierte"), "Se convierte por error el 6 del Colgante como si fuera de Yuno.");
            DrainTestDice(); await Layout(); ShowPendingSkillChoice(); Dialog().EmitSignal(AcceptDialog.SignalName.Confirmed); await Layout();
            Check(s.LastAttackDie == 6 && !p.HasNaturalSix && p.Equipment == null && !p.AttackRerollUsedThisTurn,
                "El relanzamiento del Colgante afecta indebidamente al crítico, la evolución o la habilidad de Yuno.");
            ShowAnimationLab(); Check(!Dialog().FindChildren("*", "Button", true, false).OfType<Button>().Any(b => b.Name.ToString().StartsWith("EquipSample") || b.Name.ToString().StartsWith("AddAttachment")), "El laboratorio aún permite crear cartas de ejemplo.");
            Dialog().EmitSignal(AcceptDialog.SignalName.Confirmed); await Layout();
            NewGame(); await Layout();
            Check(_game.State.Players.All(player => player.Equipment == null && player.AttachedCards.Count == 0), "Reiniciar no vacía equipo y cartas sobre Relikario.");
            GD.Print("EQUIPMENT_SMOKE_PASS · catálogo real, arte limpio, poner por 1 acción, reemplazo a memoria, Colgante +2/relanzar, interacción Yuno y reinicio.");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
