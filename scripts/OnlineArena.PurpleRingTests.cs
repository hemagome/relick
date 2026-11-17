using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckPurpleRingLayout()
    {
        var ring = CardCatalog.PurpleRing;
        var normal = CardCatalog.Pendant.Id;
        if (BoardArt.LoadCardArtwork(ring) == null || BoardArt.CardType(ring) != "EQUIPO LIGERO")
            throw new Exception("Falta el arte o la clasificación de Anillo Morado.");
        async Task Layout()
        {
            for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        foreach (var size in new[] { new Vector2I(1820, 1024), new(844, 390), new(700, 390) })
        foreach (var seat in new[] { 0, 1 })
        {
            GetWindow().ContentScaleSize = size;
            await Layout();
            var rng = new DuelRandom();
            var game = GameEngine.FromOpening(new DuelOpening { Names = ["Yuno", "Osamu"], Hands = [[], []],
                Decks = [[], []], Bonuses = [null, null], StartingSeat = seat }, rng);
            game.State.Phase = Phase.Actions;
            game.State.Active.Hand.Add(ring.Id);
            game.State.Active.Equipment = normal;
            ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, seat, 41));
            await Layout();
            var before = _snapshot!;
            if (!game.Equip(seat, 0).Success) throw new Exception("No se equipa el anillo de prueba.");
            var after = DuelSnapshot.For(game, seat, 42);
            if (CaptureEquipmentInstall(after, before)?.Event.Destination != CardZone.LightEquipment)
                throw new Exception("El Anillo debe viajar a la zona de equipos ligeros.");

            PresentTestSnapshot(after);
            await Layout();
            var light = (ZoneCardButton)ZoneNode(0, CardZone.LightEquipment, -1)!;
            var equipment = (ZoneCardButton)ZoneNode(0, CardZone.Equipment, -1)!;
            if (light.Card?.Id != ring.Id || !light.Targetable || equipment.Card?.Id != normal ||
                light.GetGlobalRect().Intersects(equipment.GetGlobalRect()))
                throw new Exception($"El equipo ligero debe estar separado del normal en {size}.");
            SelectFieldPile(seat, FieldZone.LightEquipment);
            var points = after.Options.Single(o => o.Move.Kind == DuelMoveKind.UsePurpleRing);
            if (!light.Chosen || PresentedButton(points)?.Text != "Ganar 500 PR")
                throw new Exception("Seleccionar el Anillo debe ofrecer 500 PR.");

            game.State.CurrentPlayer = 1 - seat;
            game.State.Phase = Phase.Combat; game.State.Turn = 2;
            game.State.Active.AttackRerollUsedThisTurn = true;
            game.State.Active.Dexterity = 0;
            game.State.Active.Skills.Add(CardCatalog.Destiny.Id);
            game.State.Active.Hand.Add(CardCatalog.Attack.Id);
            rng.Replay([5]);
            if (!game.Attack(1 - seat, 0, 0).Success || game.State.Phase != Phase.Decision)
                throw new Exception("El daño debe abrir una elección para el defensor.");
            rng.Finish();
            PresentTestSnapshot(DuelSnapshot.For(game, seat, 43));
            await Layout();
            var choices = _snapshot!.Options.Where(o => o.Move.Kind == DuelMoveKind.Choose).ToArray();
            if (!PresentsOption(choices.Single(o => o.Field == null)))
                throw new Exception("Debe poder conservarse el Anillo.");
            SelectFieldPile(seat, FieldZone.LightEquipment);
            if (choices.Where(o => o.Field != null).Any(o => !PresentsOption(o)) ||
                !choices.Any(o => o.Button == "Recibir 2 de daño"))
                throw new Exception("El Anillo debe ofrecer la mitad del daño y los 500 PR.");
            var root = (Control)_root!;
            await Layout();
            var minimum = root.GetCombinedMinimumSize();
            if (minimum.X > root.Size.X + 1 || minimum.Y > root.Size.Y + 1)
                throw new Exception($"Los equipos ligeros desbordan en {size}: tamaño {root.Size}, mínimo {minimum}, escritorio {Desktop}. " +
                    string.Join(", ", root.GetChildren().OfType<Control>().Select(c => $"{c.Name} {c.GetCombinedMinimumSize()}")));
        }
        GD.Print("PVP_PURPLE_RING_PASS · equipo ligero, acciones de PR y daño, escritorio y compactos, ambos asientos.");
    }
}
