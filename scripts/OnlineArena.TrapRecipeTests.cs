using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    /// <summary>
    /// `--trap-recipe-only`: the decision shows the two exchanged cards; «Crear» calls the Trap from echoes
    /// that rise from memory and meet over the exchange, then sends it to its place (hand or equipment);
    /// «Pasar» and reduced motion show nothing, and the generic played-code flash stays out of the way.
    /// </summary>
    internal async Task CheckTrapRecipe()
    {
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        async Task Layout() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        async Task Capture(string name)
        {
            var directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
            if (directory == null || DisplayServer.GetName() == "headless") return;
            System.IO.Directory.CreateDirectory(directory);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, name + ".png"));
        }
        GameEngine Exchanged(CardDefinition first, CardDefinition second)
        {
            var game = GameEngine.FromOpening(new DuelOpening { Names = ["Ana", "Beto"], StartingSeat = 0,
                Hands = [[first.Id, second.Id, CardCatalog.Senshuni.Id], [CardCatalog.Attack.Id]], IncludeSuperAttack = true, SuperMagnetCopies = [1, 1],
                Decks = [Enumerable.Repeat("attack", 20).ToList(), Enumerable.Repeat("attack", 20).ToList()],
                Bonuses = [CardCatalog.Attack.Id, CardCatalog.Attack.Id] }, new SeededRandom(29));
            game.State.Turn = 2; game.State.Phase = Phase.Actions; game.State.Active.ActionsLeft = 2;
            foreach (var _ in new[] { 0, 1 })
                Check(new DuelMove { Kind = DuelMoveKind.Exchange, Index = 0 }.Apply(game, 0).Success, "No se completa el canje de la prueba.");
            Check(game.State.Phase == Phase.TrapCraftChoice, "El canje no ofrece el Código Trampa.");
            return game;
        }
        var reduced = _impacts.ReducedMotion;
        foreach (var size in new[] { new Vector2I(1820, 1024), new(1024, 768) })
        foreach (var seat in new[] { 0, 1 })
        {
            GetWindow().Size = size; GetWindow().ContentScaleSize = size; await Layout();
            foreach (var (first, second, trap) in new[] { (CardCatalog.Attack, CardCatalog.Attack, CardCatalog.SuperAttack), (CardCatalog.Bonfire, CardCatalog.Attack, CardCatalog.SuperMagnet) })
            {
                // The decision: the exchanged cards are already in memory (rulebook § 4.6) and shown as the recipe.
                var game = Exchanged(first, second);
                var asked = DuelSnapshot.For(game, seat, 1100);
                Check(asked.TrapIngredients!.SequenceEqual([first.Id, second.Id]) && game.State.Players[0].Memory.TakeLast(2).SequenceEqual([first.Id, second.Id]),
                    "La receta no coincide con las cartas que el canje ya envió a la memoria.");
                ResetAnimations(); PresentTestSnapshot(asked); await Layout();
                Check(_inspector.Recipe.IsVisibleInTree() == (seat == 0) && (seat != 0 || _inspector.RecipeNames.Text.Contains(first.Name) && _inspector.RecipeNames.Text.Contains(second.Name)),
                    $"La receta no se muestra solo a quien decide ({trap.Name}, asiento {seat}, {size}).");
                if (seat == 0) Check(_moveButtons.Any(b => b.Text == "Crear " + trap.Name), $"No se ofrece «Crear {trap.Name}».");
                if (seat == 0 && size.X == 1820 && trap == CardCatalog.SuperAttack) await Capture($"trap-recipe-decision-{size.X}x{size.Y}");

                // «Crear»: echoes meet over the exchange and the Trap goes to its place.
                var cues = DuelVisualEvents.Capture(game);
                Check(new DuelMove { Kind = DuelMoveKind.TrapCraft, Flag = true }.Apply(game, 0).Success, "No se crea la trampa.");
                var created = DuelSnapshot.For(game, seat, 1101);
                created.Visuals = cues;
                var recipe = CaptureTrapRecipe(asked, created);
                Check(recipe?.Entry.CardId == trap.Id, "No se prepara la invocación al crear la trampa.");
                var install = CaptureEquipmentInstall(created, asked, recipe);
                Check(install == null, $"{trap.Name} también prepara un vuelo genérico de equipo.");
                var origins = CaptureOrigins(created, asked);
                // Live path: the board is rebuilt for the new revision and the summon waits for its layout,
                // so it meets over the exchange instead of the top-left corner of a board not yet laid out.
                _impacts.Clear(); PresentTestSnapshot(created); Animate(cues, origins, install);
                QueuePlayedCard(asked, created, recipe);
                var equipment = trap == CardCatalog.SuperMagnet ? (ZoneCardButton)ZoneNode(seat == 0 ? 0 : 1, CardZone.Equipment, -1)! : null;
                Check(equipment == null || equipment.AwaitingEquipment, "Súper Imán aparece en Equipo antes de iniciar la invocación.");
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); PresentQueuedPlay(); // The arena's next _Process (off in tests).
                var liveExchange = _exchangeTracks[seat == 0 ? 0 : 1]!.ExchangeButtons.Select(b => b.GetGlobalRect()).Aggregate((a, b) => a.Merge(b));
                Check(_impacts.SummonMeet is { } meet && liveExchange.Grow(4).HasPoint(meet) && liveExchange.Size.X > 0,
                    $"La invocación en vivo no aparece sobre el canje ({trap.Name}, asiento {seat}, {size}).");
                Check(_impacts.ShowingSummon && !_impacts.ShowingCard && _impacts.SummonEchoes == 2,
                    $"La creación de {trap.Name} no invoca con sus dos ecos o repite el destello genérico.");
                var place = trap == CardCatalog.SuperMagnet ? (Control)_root!.FindChild($"EquipmentZone{(seat == 0 ? 0 : 1)}", true, false)
                    : seat == 0 ? _hand : (Control?)_root!.FindChild("OpponentHandFan", true, false) ?? (Control)_root.FindChild("OpponentHandScroll", true, false);
                Check(_impacts.SummonTarget is { } target && place != null && place.GetGlobalRect().Grow(80).Intersects(target),
                    $"{trap.Name} no vuela a su lugar ({size}, asiento {seat}).");
                if (trap == CardCatalog.SuperAttack)
                {
                    // The card already in the rebuilt hand waits for the flying Trap instead of showing early.
                    var card = HiddenSummonedCard;
                    Check(card != null && IsInstanceValid(card) && card.Modulate.A < 0.01f,
                        $"Súper Ataque se ve en la mano antes de que termine su invocación ({size}, asiento {seat}).");
                    await ToSignal(GetTree().CreateTimer(ImpactLayer.SummonLanding + ImpactLayer.SummonLandingFade + 0.1), SceneTreeTimer.SignalName.Timeout);
                    Check(IsInstanceValid(card) && card!.Modulate.A > 0.99f,
                        $"Súper Ataque no aparece en la mano al aterrizar ({size}, asiento {seat}).");
                }
                else
                {
                    Check(equipment!.AwaitingEquipment && _impacts.SummonTarget == ZoneRect(equipment) && !IsAnimating,
                        "Súper Imán se ve antes de aterrizar o repite su vuelo.");
                    await ToSignal(GetTree().CreateTimer(ImpactLayer.SummonLanding + 0.1), SceneTreeTimer.SignalName.Timeout);
                    Check(!equipment.AwaitingEquipment && equipment.Card?.Id == trap.Id,
                        "Súper Imán no se revela al aterrizar en Equipo.");
                    QueuePlayedCard(asked, created, recipe); PresentQueuedPlay();
                    ResetAnimations();
                    Check(!equipment.AwaitingEquipment && !_impacts.ShowingSummon, "Interrumpir la invocación deja Súper Imán oculto.");
                }
                if (seat == 0 && size.X == 1820)
                {
                    foreach (var (moment, tag) in new[] { (0.3, "echoes"), (0.62, "summoned") })
                    {
                        _impacts.Clear(); SummonCreatedTrap(created, recipe!); _impacts._Process(moment); _impacts.SetProcess(false); _impacts.QueueRedraw();
                        await Capture($"trap-recipe-{tag}-{trap.Id}-{size.X}x{size.Y}");
                    }
                    _impacts.SetProcess(true);
                }

                // «Pasar» calls nothing; reduced motion keeps the board still.
                var passed = Exchanged(first, second); var passAsked = DuelSnapshot.For(passed, seat, 1102);
                Check(new DuelMove { Kind = DuelMoveKind.TrapCraft, Flag = false }.Apply(passed, 0).Success, "No se puede pasar sin crear.");
                var passResult = DuelSnapshot.For(passed, seat, 1103);
                _impacts.Clear(); PresentTestSnapshot(passResult); QueuePlayedCard(passAsked, passResult, CaptureTrapRecipe(passAsked, passResult));
                PresentQueuedPlay();
                Check(!_impacts.ShowingSummon, "Pasar sin crear invoca una trampa.");
                _impacts.ReducedMotion = true; _impacts.Clear(); PresentTestSnapshot(created);
                QueuePlayedCard(asked, created, recipe); PresentQueuedPlay();
                Check(!_impacts.ShowingSummon && (trap != CardCatalog.SuperMagnet ||
                    ZoneNode(seat == 0 ? 0 : 1, CardZone.Equipment, -1) is ZoneCardButton { AwaitingEquipment: false }),
                    "La invocación ignora el movimiento reducido u oculta el equipo sin animarlo.");
                _impacts.ReducedMotion = reduced; _impacts.Clear();
            }
        }

        foreach (var protect in new[] { false, true })
        {
            var game = Exchanged(CardCatalog.Bonfire, CardCatalog.Attack);
            var player = game.State.Players[0];
            player.Equipment = CardCatalog.SuperMagnet.Id;
            player.Skills.Add(CardCatalog.Reverse.Id);
            var asked = DuelSnapshot.For(game, 0, 1104);
            ResetAnimations(); PresentTestSnapshot(asked);
            Check(new DuelMove { Kind = DuelMoveKind.TrapCraft, Flag = true }.Apply(game, 0).Success &&
                game.State.Phase == Phase.DestructionResponse, "No se ofrece proteger el Súper Imán anterior.");
            var waiting = DuelSnapshot.For(game, 0, 1105);
            var recipe = CaptureTrapRecipe(asked, waiting);
            PresentTestSnapshot(waiting); QueuePlayedCard(asked, waiting, recipe); await Layout(); PresentQueuedPlay();
            Check(recipe == null && !_impacts.ShowingSummon &&
                ZoneNode(0, CardZone.Equipment, -1) is ZoneCardButton { AwaitingEquipment: false },
                "Súper Imán se invoca antes de resolver la protección del equipo anterior.");

            var cues = DuelVisualEvents.Capture(game);
            Check(new DuelMove { Kind = DuelMoveKind.ProtectDestruction, Index = protect ? 0 : -1 }.Apply(game, 0).Success,
                "No se resuelve la protección del equipo.");
            var result = DuelSnapshot.For(game, 0, 1106);
            result.Visuals = cues;
            recipe = CaptureTrapRecipe(waiting, result);
            Check(CaptureEquipmentInstall(result, waiting, recipe) == null, "El reemplazo protegido o confirmado duplica el vuelo.");
            PresentTestSnapshot(result); QueuePlayedCard(waiting, result, recipe); await Layout(); PresentQueuedPlay();
            Check(_impacts.ShowingSummon == !protect && _impacts.SummonEchoes == (protect ? 0 : 2),
                "La receta diferida no refleja la decisión de protección.");
            ResetAnimations();
        }
        GD.Print("PVP_TRAP_RECIPE_PASS · receta visible para quien decide, invocación desde la memoria hasta la mano o el equipo, sin destello genérico, pasar y movimiento reducido; dos tamaños y ambos asientos.");
    }
}
