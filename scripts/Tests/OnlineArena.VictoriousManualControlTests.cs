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
            if (!tile.Targetable || !source.Label.EndsWith("Habilidad 0 PR"))
                throw new Exception("La habilidad movible debe resaltarse e indicar los PR de su casilla.");
            tile.EmitSignal(BaseButton.SignalName.Pressed);
            if (!tile.Chosen || !PresentsOption(source) || !source.Move.Apply(game, seat).Success)
                throw new Exception("La habilidad debe seleccionarse desde el tablero.");

            var before = DuelSnapshot.For(game, seat, 62);
            PresentTestSnapshot(before);
            for (var frame = 0; frame < 4; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var destinations = _snapshot!.Options.Where(option => option.Move.Kind == DuelMoveKind.Choose).ToArray();
            var cues = DuelVisualEvents.Capture(game);
            if (destinations.Length != 4 || destinations.Any(option => !PresentsOption(option)) ||
                !destinations.Single(option => option.Label == "Habilidad 500 PR").Move.Apply(game, seat).Success)
                throw new Exception("La arena debe mostrar el canje y las tres casillas de habilidad libres.");

            // The skill flies from its old space to the new one, face up, and the new space waits for it.
            var after = DuelSnapshot.For(game, seat, 63); after.Visuals = cues;
            var move = cues.Single(cue => cue.FieldMove);
            var origins = CaptureOrigins(after, before);
            // On the compact board the skill row is off screen: the flight is skipped there, as for any hidden zone.
            var visible = size.X >= 1024;
            if (!origins.TryGetValue(move, out var flight) || visible && flight.Origin == null || flight.Face != CardCatalog.Reverse.Id)
                throw new Exception("El traslado debe salir de la casilla anterior mostrando la habilidad.");
            PresentTestSnapshot(after); Animate(cues, origins);
            for (var frame = 0; frame < 4; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var landing = ZoneNode(0, CardZone.Skill, 0) as SkillTile;
            if (landing?.Name != "Skill0_2" || _snapshot!.Players[seat].Health != 7)
                throw new Exception("La habilidad debe quedar en su nueva casilla y el pago debe reflejarse en el tablero.");
            if (visible && !landing.AwaitingLesson) throw new Exception("La casilla nueva muestra la habilidad antes de que llegue.");
            var flew = false;
            for (var frame = 0; IsAnimating && frame < 100; frame++)
            {
                UpdateFlights(0.1);
                if (_activeFlight?.IsFieldMove == true)
                {
                    flew = true;
                    if (_flightTarget != landing || _flightFront?.Card.Id != CardCatalog.Reverse.Id)
                        throw new Exception("El vuelo debe llevar la habilidad visible hasta su nueva casilla.");
                }
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            if (flew != visible || IsAnimating || landing.AwaitingLesson)
                throw new Exception("El traslado debe animarse y dejar la habilidad visible al aterrizar.");
        }
        GD.Print("PVP_VICTORIOUS_MANUAL_CONTROL_PASS · selección desde el tablero, casillas libres y vuelo de la habilidad a su nueva casilla; dos tamaños y ambos asientos.");
    }
}
