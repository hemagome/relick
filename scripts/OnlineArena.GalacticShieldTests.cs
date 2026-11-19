using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckGalacticShieldLayout()
    {
        var shield = CardCatalog.GalacticShield;
        var attack = CardCatalog.Attack.Id;
        if (BoardArt.LoadCardArtwork(shield) == null)
            throw new Exception("Falta el arte importado de Escudo Galáctico.");

        foreach (var size in new[] { new Vector2I(1820, 1024), new(700, 390) })
        foreach (var seat in new[] { 0, 1 })
        {
            GetWindow().ContentScaleSize = size;
            var random = new DuelRandom();
            var game = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Yuno", "Osamu"], Hands = [[], []], Decks = [[attack, attack, attack, attack], [attack, attack, attack, attack]],
                Bonuses = [null, null], StartingSeat = seat
            }, random);
            game.State.Phase = Phase.Actions;
            game.State.Active.AttachedCards.Add(attack);
            game.State.Active.Hand.Add(shield.Id);
            if (!game.PlayCode(seat, 0).Success) throw new Exception("No se coloca Escudo Galáctico.");
            ResetAnimations();
            PresentTestSnapshot(DuelSnapshot.For(game, seat, 41));
            for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var shieldNode = (ZoneCardButton)ZoneNode(0, CardZone.GalacticShield, 0)!;
            var attached = (AttachmentStack)ZoneNode(0, CardZone.Attached, -1)!;
            if (shieldNode.Card?.Id != shield.Id || shieldNode.StatusText != "×3" ||
                !shieldNode.CardRect.HasArea() || attached.CardRect.End.X > shieldNode.Position.X)
                throw new Exception($"El tablero debe mostrar el Escudo y conservar espacio para el Ataque anexo ({size}).");
            shieldNode.EmitSignal(BaseButton.SignalName.Pressed);
            if (_fieldSelection != (seat, FieldZone.GalacticShield, 0))
                throw new Exception("El Escudo debe poder inspeccionarse por separado.");

            game.State.CurrentPlayer = 1 - seat;
            game.State.Phase = Phase.Combat;
            game.State.Turn = 2;
            game.State.Active.Dexterity = 0;
            game.State.Active.AttackRerollUsedThisTurn = true;
            game.State.Active.Hand.Add(attack);
            game.State.Active.Skills.Add(CardCatalog.Destiny.Id);
            random.Replay([5]);
            if (!game.Attack(1 - seat, 0, 0).Success || game.State.PendingDecision?.Source != shield.Id)
                throw new Exception("El Escudo debe abrir una elección antes del daño.");
            random.Finish();
            PresentTestSnapshot(DuelSnapshot.For(game, seat, 42));
            var choices = _snapshot!.Options.Where(option => option.Move.Kind == DuelMoveKind.Choose).ToArray();
            if (choices.Length != 3 || choices.Any(option => option.CardId != null || !PresentsOption(option)))
                throw new Exception("La arena debe mostrar tres elecciones sin revelar sus cartas.");

            if (!game.ResolveDecision(seat, 0).Success) throw new Exception("No se resuelve la elección del Escudo.");
            game.State.Phase = Phase.Actions;
            game.State.Active.Hand.Add(CardCatalog.BlueEgg.Id);
            if (!game.PlayCode(1 - seat, 0).Success) throw new Exception("No se activa el Huevo de prueba.");
            PresentTestSnapshot(DuelSnapshot.For(game, 1 - seat, 43));
            var rivalShield = (ZoneCardButton)ZoneNode(1, CardZone.GalacticShield, 0)!;
            var target = _snapshot!.Options.Single(option => option.Field is { Zone: FieldZone.GalacticShield });
            rivalShield.EmitSignal(BaseButton.SignalName.Pressed);
            if (!rivalShield.Targetable || !rivalShield.Chosen || !PresentsOption(target))
                throw new Exception("El Escudo rival debe poder seleccionarse como objetivo.");
        }
        GD.Print("PVP_GALACTIC_SHIELD_PASS");
    }
}
