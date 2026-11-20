using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckEquipmentAnimation()
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        async Task Layout() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        var capture = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
        async Task Capture(string name)
        {
            if (capture == null || DisplayServer.GetName() == "headless") return;
            System.IO.Directory.CreateDirectory(capture);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(capture, name + ".png"));
        }
        static GameEngine Fixture(int actor, string card)
        {
            var game = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Ana", "Beto"], StartingSeat = actor,
                Hands = [[card, CardCatalog.Attack.Id, card], [card, CardCatalog.Attack.Id, card]],
                Decks = [[CardCatalog.Attack.Id], [CardCatalog.Attack.Id]], Bonuses = [null, null]
            }, new SeededRandom(76));
            game.State.Phase = Phase.Actions;
            return game;
        }

        var usedPreview = false;
        foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
        foreach (var seat in new[] { 0, 1 })
        foreach (var actor in new[] { 0, 1 })
        {
            var game = Fixture(actor, CardCatalog.Pc.Id);
            // Re-equipping a second PC keeps its identity and copy count but replaces the actual card.
            if (seat == 1) game.State.Active.Equipment = actor == 0 ? CardCatalog.Pc.Id : CardCatalog.Pendant.Id;
            GetWindow().ContentScaleSize = size;
            await Layout();
            ResetAnimations();
            var before = DuelSnapshot.For(game, seat, 830);
            PresentTestSnapshot(before);
            if (actor == seat) SelectCard(2);
            await Layout();
            var handCard = actor == seat ? ZoneNode(0, CardZone.Hand, 2) : null;
            var cues = DuelVisualEvents.Capture(game);
            Check(new DuelMove { Kind = DuelMoveKind.Equip, Index = 2 }.Apply(game, actor).Success, "No se equipa el PC 8BITS de prueba.");
            var after = DuelSnapshot.For(game, seat, 831);
            after.Visuals = cues;
            var install = CaptureEquipmentInstall(after, before);
            Check(install?.Face == CardCatalog.Pc.Id && install.Event.Source == CardZone.Hand, "No se prepara el vuelo desde la mano.");
            if (handCard != null && RectVisible(handCard, ZoneRect(handCard)))
                Check(install!.Origin == ZoneRect(handCard), "El vuelo comienza en otra copia del equipo.");
            var origins = CaptureOrigins(after, before);
            PresentTestSnapshot(after);
            Animate(cues, origins, install);
            var tile = (ZoneCardButton)ZoneNode(actor == seat ? 0 : 1, CardZone.Equipment, -1)!;
            Check(tile.Card?.Id == CardCatalog.Pc.Id && tile.AwaitingEquipment && !_die.IsBusy,
                "El equipo aparece o su dado empieza antes del vuelo.");
            if (seat == 1)
                Check(_flights.Peek().Event.Source == CardZone.Equipment, "El equipo nuevo llega antes de que salga el anterior.");
            await Layout();
            for (var frame = 0; _activeFlight != install && frame < 8; frame++)
                UpdateFlights(_activeFlight == null ? 0 : 0.5);
            Check(_activeFlight == install && _flightTarget == tile && _flightFront?.Card.Id == CardCatalog.Pc.Id &&
                _flightFront.Visible == (actor == seat), "El vuelo no apunta a la zona de equipo o muestra la cara rival demasiado pronto.");
            usedPreview |= _placementPreview;
            if (size.X == 1440)
                Check(!_placementPreview && _flightDestination == ZoneRect(tile), "El vuelo de escritorio no llega al espacio del equipo.");
            var scroll = _arenaScroll?.ScrollVertical;
            UpdateFlights(0.36);
            Check(_flightFront!.Visible && Math.Abs(_flight!.Rotation) < 0.25f && tile.AwaitingEquipment && !_die.IsBusy &&
                _root!.FindChildren("OpponentHandCard*", "Button", true, false).OfType<OpponentHandCard>().All(card => card.IsFaceDown),
                "El equipo pierde su orientación, revela la mano rival o anticipa el dado.");
            if (actor == 0) await Capture($"equipment-flight-{size.X}-seat{seat}");
            UpdateFlights(1.1);
            Check(_activeFlight == null && !tile.AwaitingEquipment && _die.IsBusy && _arenaScroll?.ScrollVertical == scroll,
                "La carta no aterriza, el dado se pierde o se mueve el tablero.");
            ResetAnimations();
            Animate(cues, origins, install);
            ResetAnimations();
            Check(!tile.AwaitingEquipment && !IsAnimating, "Interrumpir el vuelo deja un equipo oculto o reproduce el dado.");
        }
        Check(usedPreview, "La prueba no cubre un equipo fuera del área visible.");

        GetWindow().ContentScaleSize = new(1440, 1024);
        await Layout();
        foreach (var canceled in new[] { false, true })
        {
            var game = Fixture(0, CardCatalog.Pendant.Id);
            game.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
            var before = DuelSnapshot.For(game, 0, 840);
            Check(game.Equip(0, 2).Success && game.State.Phase == Phase.ErrorResponse, "No se abre la respuesta de Error de Carga.");
            var pending = DuelSnapshot.For(game, 0, 841);
            Check(CaptureEquipmentInstall(pending, before) == null, "Un equipo pendiente vuela antes de resolver la respuesta.");
            PresentTestSnapshot(pending);
            await Layout();
            var cues = DuelVisualEvents.Capture(game);
            Check((canceled ? game.PlayCode(1, 3) : game.PassErrorResponse(1)).Success, "No se resuelve la respuesta al equipo.");
            var after = DuelSnapshot.For(game, 0, 842);
            after.Visuals = cues;
            var install = CaptureEquipmentInstall(after, pending);
            Check(canceled ? install == null : install?.Event.Source == CardZone.Activation,
                "Un equipo anulado vuela o uno confirmado pierde su origen público.");
        }

        var stack = Fixture(0, CardCatalog.Totem.Id);
        stack.State.Active.Equipment = CardCatalog.Totem.Id;
        var beforeStack = DuelSnapshot.For(stack, 0, 850);
        PresentTestSnapshot(beforeStack);
        await Layout();
        Check(stack.Equip(0, 2).Success, "No se apila el Tótem de Salvación.");
        var afterStack = DuelSnapshot.For(stack, 0, 851);
        var stackInstall = CaptureEquipmentInstall(afterStack, beforeStack);
        Check(stackInstall?.PreviousEquipmentStatus == "", "Apilar oculta el equipo anterior antes de que llegue la copia.");

        var sniper = Fixture(0, CardCatalog.Sniper.Id);
        sniper.State.Phase = Phase.Discard;
        sniper.State.DiscardPlayer = 1;
        sniper.State.DiscardsRemaining = 1;
        sniper.State.DiscardResumePhase = Phase.Actions;
        Check(sniper.Discard(1, 0).Success && sniper.State.Phase == Phase.SniperChoice, "No se prepara Mira de Sniper en memoria.");
        var beforeSniper = DuelSnapshot.For(sniper, 0, 860);
        PresentTestSnapshot(beforeSniper);
        await Layout();
        var memory = ZoneNode(1, CardZone.Memory, -1)!;
        Check(sniper.ResolveSniper(1, true).Success, "No se equipa Mira de Sniper desde memoria.");
        var sniperInstall = CaptureEquipmentInstall(DuelSnapshot.For(sniper, 0, 861), beforeSniper);
        Check(sniperInstall?.Event.Source == CardZone.Memory && sniperInstall.Origin == ZoneRect(memory),
            "Mira de Sniper sale de la mano en lugar de la memoria.");
        GD.Print("PVP_EQUIPMENT_ANIMATION_PASS · mano, reemplazo, apilado, anulación, memoria, dado y cuatro tamaños desde ambos asientos.");
    }
}
