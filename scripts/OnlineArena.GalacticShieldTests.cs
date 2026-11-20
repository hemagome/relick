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
            ResetAnimations();
            var beforeShield = DuelSnapshot.For(game, seat, 40);
            PresentTestSnapshot(beforeShield);
            for (var i = 0; i < 2; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var shieldVisuals = DuelVisualEvents.Capture(game);
            if (!game.PlayCode(seat, 0).Success) throw new Exception("No se coloca Escudo Galáctico.");
            var afterShield = DuelSnapshot.For(game, seat, 41);
            afterShield.Visuals = shieldVisuals;
            var shieldOrigins = CaptureOrigins(afterShield, beforeShield);
            if (shieldVisuals.Any(cue => cue is { Kind: DuelVisualKind.Pile, Destination: CardZone.Memory }) ||
                shieldOrigins.Values.Count(flight => flight.IsPlacement && flight.Event.Destination == CardZone.GalacticShield) != 1)
                throw new Exception("El Escudo debe volar una sola vez sobre el Relikario, sin pasar por Memoria.");
            var loads = shieldVisuals.Where(cue => cue is { Source: CardZone.Deck, Destination: CardZone.ShieldCharge }).ToArray();
            if (loads.Length != 3 || loads.Any(cue => shieldOrigins[cue].Face != null))
                throw new Exception("Las tres cargas del Escudo deben viajar boca abajo.");
            PresentTestSnapshot(afterShield);
            for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var shieldNode = (ZoneCardButton)ZoneNode(0, CardZone.GalacticShield, 0)!;
            var attached = (AttachmentStack)ZoneNode(0, CardZone.Attached, -1)!;
            if (shieldNode.Card?.Id != shield.Id || shieldNode.PresentedShieldCharges != 3 ||
                !shieldNode.CardRect.HasArea() || attached.CardRect.End.X > shieldNode.Position.X)
                throw new Exception($"El tablero debe mostrar el Escudo y conservar espacio para el Ataque anexo ({size}).");
            await CheckAttachmentLoadFlights(shieldVisuals, shieldOrigins, loads, shieldNode, () => shieldNode.PresentedShieldCharges!.Value, faceUp: false);
            ResetAnimations();
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
            var before = DuelSnapshot.For(game, seat, 42);
            PresentTestSnapshot(before);
            for (var i = 0; i < 2; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var origin = ZoneRect(ZoneNode(0, CardZone.GalacticShield, 0)!);
            var cues = DuelVisualEvents.Capture(game);
            if (!game.Attack(1 - seat, 0, 0).Success || game.State.PendingDecision?.Source == shield.Id)
                throw new Exception("El Escudo debe entregar su carta boca abajo sin abrir una elección.");
            random.Finish();
            var after = DuelSnapshot.For(game, seat, 43);
            after.Visuals = cues;
            var origins = CaptureOrigins(after, before);
            var charge = cues.SingleOrDefault(cue => cue is { Kind: DuelVisualKind.Pile, Source: CardZone.ShieldCharge, Destination: CardZone.Hand });
            if (charge == null || !origins.TryGetValue(charge, out var flight) || size.X == 1820 && flight.Origin != origin ||
                flight.Face != after.Players[seat].Hand.ElementAtOrDefault(charge.Value) || flight.StartsFaceUp != false)
                throw new Exception("La carta debe salir boca abajo del Escudo y mostrar su cara solo a su dueño.");
            PresentTestSnapshot(after);
            // The compact board may scroll the hand out of view, which settles the card without a flight.
            if (size.X == 1820) await CheckShieldChargeFlight(cues, origins, charge);
            ResetAnimations();

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

    private async Task CheckAttachmentLoadFlights(List<DuelVisualEvent> cues, Dictionary<DuelVisualEvent, Flight> origins,
        DuelVisualEvent[] loads, Control target, Func<int> presentedCount, bool faceUp)
    {
        Animate(cues, origins);
        if (presentedCount() != 0) throw new Exception("Las cartas deben contarse conforme aterrizan.");
        var attachment = target as AttachmentStack;
        if (target.Modulate.A != 1 || target.SelfModulate.A != 1 ||
            attachment?.PresentedTopCardId != null || target is ZoneCardButton { PresentedCard: not null })
            throw new Exception("La zona debe conservar su contorno mientras espera la carta principal.");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        for (var i = 0; i < loads.Length; i++)
        {
            var load = loads[i];
            for (var frame = 0; _activeFlight?.Event != load && frame < 12; frame++)
                UpdateFlights(_activeFlight == null ? 0 : 1.2);
            if (_activeFlight?.Event != load || _flightTarget != target || _flightFront?.Visible == true || (_flightFront != null) != faceUp)
                throw new Exception($"El vuelo {load.Destination} debe empezar boca abajo y dirigirse a su carta anfitriona.");
            if (attachment != null && attachment.PresentedTopCardId != attachment.TopCardId)
                throw new Exception("El Tomo debe verse en su zona al recibir sus cartas.");
            UpdateFlights(0.27);
            if ((_flightFront?.Visible == true) != faceUp) throw new Exception("Solo se revela el arte de las cartas públicas.");
            UpdateFlights(1.2);
            if (presentedCount() != i + 1) throw new Exception("La carta debe contarse cuando aterriza.");
        }
    }

    private async Task CheckShieldChargeFlight(List<DuelVisualEvent> cues, Dictionary<DuelVisualEvent, Flight> origins, DuelVisualEvent charge)
    {
        Animate(cues, origins);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        for (var frame = 0; _activeFlight?.Event != charge && frame < 12; frame++)
            UpdateFlights(_activeFlight == null ? 0 : 1.2);
        var hand = ZoneNode(0, CardZone.Hand, charge.Value);
        if (_activeFlight?.Event != charge || _flightTarget != hand || _flightFront == null || _flightFront.Visible)
            throw new Exception("El vuelo del Escudo debe dirigirse a la mano y empezar boca abajo.");
        UpdateFlights(1.2);
        if (_activeFlight?.Event == charge || hand!.Modulate.A < 1)
            throw new Exception("La carta del Escudo debe aterrizar en la mano.");
    }
}
