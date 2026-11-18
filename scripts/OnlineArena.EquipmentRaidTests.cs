using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckEquipmentRaidLayout()
    {
        var area = CardCatalog.EquipmentRaid.Id;
        var attack = CardCatalog.Attack.Id;
        if (BoardArt.LoadCardArtwork(CardCatalog.EquipmentRaid) == null)
            throw new Exception("Falta la ilustración de Misión: Robo de Equipos.");
        async Task Layout()
        {
            for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        foreach (var size in new[] { new Vector2I(1820, 1024), new(844, 390), new(700, 390) })
        foreach (var seat in new[] { 0, 1 })
        {
            GetWindow().ContentScaleSize = size;
            await Layout();
            var game = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Yuno", "Osamu"], Hands = [[], []], Decks = [[attack, attack], [attack, attack]],
                Bonuses = [area, area], StartingSeat = seat
            }, new SeededRandom(42));
            ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, seat, 21));
            await Layout();
            var choices = _snapshot!.Options.Where(o => o.Move.Kind == DuelMoveKind.Choose).ToArray();
            if (choices.Length != 2 || choices.Any(o => !PresentsOption(o)))
                throw new Exception("Deben aparecer los dos repartos de lados.");
            if (!game.ResolveDecision(seat, 0).Success || !game.ResolveDecision(1 - seat, 1).Success)
                throw new Exception("Cada jugador debe decidir qué lado de su Área recibe.");
            game.State.Phase = Phase.Actions;
            game.State.Active.Hand.Add(CardCatalog.TimeBomb.Id);
            if (!game.PlayCode(seat, 0).Success) throw new Exception("No se prepara la selección de Bomba de Tiempo.");
            PresentTestSnapshot(DuelSnapshot.For(game, seat, 22));
            await Layout();

            var zone = (ZoneCardButton)ZoneNode(1, CardZone.Area, -1)!;
            var bonus = (ZoneCardButton)ZoneNode(1, CardZone.Bonus, -1)!;
            for (var relative = 0; relative < 2; relative++)
            {
                var track = _exchangeTracks[relative]!;
                var accent = relative == 0 ? RelickVisuals.State.Own : RelickVisuals.State.Rival;
                if (track.BonusButton.Accent != accent || track.AreaButton?.Accent != accent)
                    throw new Exception($"Bonus y Área pierden el color de su dueño en {size}.");
                if (Desktop && (_root!.FindChild($"BonusFrame{relative}", true, false) is not Panel frame ||
                    frame.GetThemeStylebox("panel") is not StyleBoxFlat style || style.BorderColor != BoardArt.Tint(accent, 0.8f)))
                    throw new Exception($"El marco de Bonus y Área pierde el color de su dueño en {size}.");
            }
            if (zone.Card?.Id != area || zone.StatusText != "LADO B" || bonus.Card?.Id != attack ||
                !zone.Targetable || !bonus.Targetable || zone.GetGlobalRect().Intersects(bonus.GetGlobalRect()))
                throw new Exception($"El Área y el Bonus deben aparecer por separado y ser seleccionables en {size}.");
            SelectField(1 - seat, FieldZone.Area, -1);
            var target = _snapshot!.Options.Single(o => o.Field is { Zone: FieldZone.Area } field && field.Owner == 1 - seat);
            if (!zone.Chosen || PresentedButton(target)?.Text != "Destruir")
                throw new Exception("Seleccionar el Área debe mostrar la acción sobre esa carta.");
            var root = (Control)_root!;
            var minimum = root.GetCombinedMinimumSize();
            if (minimum.X > root.Size.X + 1 || minimum.Y > root.Size.Y + 1)
                throw new Exception($"Las Áreas desbordan en {size}: mínimo {minimum}.");
        }
        GD.Print("PVP_EQUIPMENT_RAID_PASS · dos lados, Áreas y Bonus seleccionables, escritorio y compactos, ambos asientos.");
    }
}
