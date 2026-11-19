using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckVictoriousManualControlLayout()
    {
        var code = CardCatalog.VictoriousManualControl;
        if (BoardArt.LoadCardArtwork(code) == null || ArenaActionText.CardCost(code) != "1 acción · 1 vida")
            throw new Exception("El inspector debe mostrar el arte y el coste de Control Manual Victorioso.");

        foreach (var size in new[] { new Vector2I(1440, 1024), new(700, 390) })
        foreach (var seat in new[] { 0, 1 })
        {
            GetWindow().ContentScaleSize = size;
            for (var frame = 0; frame < 4; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var game = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Yuno", "Osamu"], Hands = [[], []], Decks = [[], []],
                Bonuses = [CardCatalog.Senshuni.Id, CardCatalog.Senshuni.Id], StartingSeat = seat
            }, new SeededRandom(30));
            game.State.Phase = Phase.Actions;
            var p = game.State.Active;
            p.Hand.Add(code.Id);
            p.Skills.Add(CardCatalog.Reverse.Id);
            if (!game.PlayCode(seat, 0).Success) throw new Exception("No se activa Control Manual Victorioso.");

            ResetAnimations();
            PresentTestSnapshot(DuelSnapshot.For(game, seat, 61));
            var source = _snapshot!.Options.Single(option => option.Field is { Zone: FieldZone.Skill, Index: 0 });
            var tile = _ownSkills.Single(skill => skill.SkillIndex == 0 && skill.Skill != null);
            if (!tile.Targetable) throw new Exception("La habilidad movible debe resaltarse.");
            tile.EmitSignal(BaseButton.SignalName.Pressed);
            if (!tile.Chosen || !PresentsOption(source) || !source.Move.Apply(game, seat).Success)
                throw new Exception("La habilidad debe seleccionarse desde el tablero.");

            PresentTestSnapshot(DuelSnapshot.For(game, seat, 62));
            var destinations = _snapshot!.Options.Where(option => option.Move.Kind == DuelMoveKind.Choose).ToArray();
            if (destinations.Length != 4 || destinations.Any(option => !PresentsOption(option)) ||
                !destinations.Single(option => option.Label == "Habilidad 3").Move.Apply(game, seat).Success)
                throw new Exception("La arena debe mostrar el canje y las tres casillas de habilidad libres.");

            PresentTestSnapshot(DuelSnapshot.For(game, seat, 63));
            if ((ZoneNode(0, CardZone.Skill, 0) as SkillTile)?.Name != "Skill0_2" || _snapshot!.Players[seat].Health != 7)
                throw new Exception("La habilidad debe quedar en su nueva casilla y el pago debe reflejarse en el tablero.");
        }
        GD.Print("PVP_VICTORIOUS_MANUAL_CONTROL_PASS");
    }
}
