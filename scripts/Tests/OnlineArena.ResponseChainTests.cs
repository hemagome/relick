using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    /// <summary>
    /// A five-link chain (attack, Estrella del Fontanero and three Errores de Carga) grows link by link and resolves
    /// from the last link to the first, at the four sizes, from both seats, with and without reduced motion.
    /// </summary>
    internal async Task CheckResponseChain()
    {
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        async Task Layout() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        var dir = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
        async Task Capture(string name)
        {
            if (dir == null || DisplayServer.GetName() == "headless") return;
            System.IO.Directory.CreateDirectory(dir);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(dir, name + ".png"));
        }
        static GameEngine Fixture()
        {
            var g = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Ana", "Beto"], StartingSeat = 0, Hands = [[CardCatalog.Attack.Id], [CardCatalog.Attack.Id]],
                Decks = [Enumerable.Repeat(CardCatalog.Attack.Id, 30).ToList(), Enumerable.Repeat(CardCatalog.Attack.Id, 30).ToList()], Bonuses = [null, null]
            }, new SeededRandom(44));
            g.State.Turn = 2;
            var a = g.State.Players[0]; var b = g.State.Players[1];
            a.Hand.Clear(); a.Hand.AddRange([CardCatalog.PlumberStar.Id, CardCatalog.Senshuni.Id, CardCatalog.Attack.Id, CardCatalog.LoadError.Id]);
            a.Skills.Add(CardCatalog.Destiny.Id);
            b.Hand.Clear(); b.Hand.AddRange([CardCatalog.LoadError.Id, CardCatalog.LoadError.Id]);
            foreach (var p in g.State.Players) p.Dexterity = 0;
            return g;
        }
        string[] expected = [CardCatalog.Destiny.Id, CardCatalog.PlumberStar.Id, CardCatalog.LoadError.Id, CardCatalog.LoadError.Id, CardCatalog.LoadError.Id];
        int[] actors = [0, 0, 1, 0, 1];
        int[] ram = [8, 16, 32, 32, 32];
        ResponseChain.Outcome[] outcomes = [ResponseChain.Outcome.Continues, ResponseChain.Outcome.Canceled, ResponseChain.Outcome.Resolved,
            ResponseChain.Outcome.Canceled, ResponseChain.Outcome.Resolved];
        var chain = _responseChain; var reduced = chain.ReducedMotion;
        chain.ManualClock = true;
        void CheckPlacement(string where)
        {
            var area = chain.GetGlobalRect(); var screen = GetGlobalRect();
            Check(chain.Visible && chain.MouseFilter == MouseFilterEnum.Ignore, $"La cadena no se ve o captura el ratón ({where}).");
            Check(area.Position.X >= screen.Position.X - 0.5f && area.Position.Y >= screen.Position.Y - 0.5f &&
                area.End.X <= screen.End.X + 0.5f && area.End.Y <= screen.End.Y + 0.5f, $"La cadena sale de la pantalla ({where}): {area} en {screen}.");
            foreach (var button in _moveButtons.Where(b => IsInstanceValid(b) && b.IsVisibleInTree()))
                Check(!button.GetGlobalRect().Intersects(area), $"La cadena tapa «{button.Text}» ({where}).");
            foreach (var module in chain.ModuleRects)
                Check(module.Size.Y >= ResponseChain.MinModuleHeight - 1 && module.Position.X >= -0.5f && module.End.X <= chain.Size.X + 0.5f,
                    $"Un eslabón no cabe ({where}): {module}.");
        }
        try
        {
            foreach (var still in new[] { false, true })
            foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
            foreach (var seat in new[] { 0, 1 })
            {
                var where = $"{size.X}x{size.Y}, asiento {seat}{(still ? ", movimiento reducido" : "")}";
                GetWindow().Size = size; GetWindow().ContentScaleSize = size;
                chain.ReducedMotion = still; chain.Clear(); ResetAnimations();
                var g = Fixture(); var revision = 900UL;
                Check(g.Attack(0, 2, 0).Success, "No se prepara el ataque de la cadena.");
                PresentTestSnapshot(DuelSnapshot.For(g, seat, revision++)); SyncResponseChain(DuelSnapshot.For(g, seat, revision), false);
                await Layout();
                Check(!chain.Visible && !chain.HasContent, $"Sin cadena abierta no debe verse nada ({where}).");
                var played = new (int Actor, string Card)[] { (0, CardCatalog.PlumberStar.Id), (1, CardCatalog.LoadError.Id), (0, CardCatalog.LoadError.Id), (1, CardCatalog.LoadError.Id) };
                for (var step = 0; step < played.Length; step++)
                {
                    var (actor, card) = played[step];
                    var cues = DuelVisualEvents.Capture(g);
                    var hand = g.State.Players[actor].Hand;
                    Check(g.PlayCode(actor, hand.IndexOf(card)).Success, $"No se juega {card} en el paso {step + 1}.");
                    var view = DuelSnapshot.For(g, seat, revision++); view.Visuals = cues;
                    var before = chain.Links.ToArray();
                    PresentTestSnapshot(view); SyncResponseChain(view, true);
                    await Layout(); PositionResponseChain();
                    var last = step == played.Length - 1;
                    var count = last ? 5 : step + 2;
                    Check(chain.Links.Count == count && chain.Links.Select(l => l.CardId).SequenceEqual(expected.Take(count)) &&
                        chain.Links.Select(l => l.Actor).SequenceEqual(actors.Take(count)), $"Orden o dueños de la cadena incorrectos en el paso {step + 1} ({where}).");
                    Check(chain.Links.Select(l => ResponseChain.Ram(l.CardId)).SequenceEqual(ram.Take(count)) && chain.Links[0].Attack && chain.Links.All(l => l.Art != null),
                        $"Velocidad RAM, ataque o arte incorrectos ({where}).");
                    Check(before.Zip(chain.Links).All(pair => ReferenceEquals(pair.First, pair.Second)), $"Los eslabones previos vuelven a entrar ({where}).");
                    var entering = chain.Links.Skip(before.Length).ToArray();
                    Check(still ? entering.All(l => l.Age >= ResponseChain.EnterSeconds) : entering.All(l => l.Age <= 0),
                        $"El eslabón nuevo {(still ? "se anima con movimiento reducido" : "no entra animado")} ({where}).");
                    Check(chain.Resolving == last, $"La resolución empieza a destiempo ({where}).");
                    if (!last)
                    {
                        Check(g.State.Phase == Phase.ErrorResponse, "La cadena se cierra antes de tiempo.");
                        Check(chain.Links.All(l => l.Outcome == ResponseChain.Outcome.Pending), $"Una respuesta abierta ya muestra un resultado ({where}).");
                        var responder = view.DecisionPlayer == seat;
                        Check(!responder || _moveButtons.Any(b => b.IsVisibleInTree() && PresentedOption(b)?.Move.Kind == DuelMoveKind.PassError),
                            $"Falta el botón Pasar junto a la cadena ({where}).");
                        chain.Advance(ResponseChain.EnterSeconds + ResponseChain.TraceSeconds + ResponseChain.Stagger * 2);
                        CheckPlacement(where + $", paso {step + 1}");
                        if (step == 2 && !still && size.X is 1440 or 700 or 844 or 1024) await Capture($"response-chain-live-{size.X}x{size.Y}-seat{seat}");
                        continue;
                    }
                    Check(view.Phase != Phase.ErrorResponse && view.Visuals.Count(v => v.Kind == DuelVisualKind.Chain) == 1, "La cadena no publica su resolución.");
                    CheckPlacement(where + ", resolución");
                    if (still)
                    {
                        Check(chain.Links.Select(l => l.Outcome).SequenceEqual(outcomes) && chain.Opacity >= 1,
                            $"Con movimiento reducido el resultado no aparece de una vez ({where}).");
                        chain.Advance(ResponseChain.ReducedHoldSeconds - 0.05);
                        Check(chain.HasContent && chain.Opacity >= 1, $"La cadena reducida se desvanece o se oculta pronto ({where}).");
                        chain.Advance(0.1);
                        Check(!chain.HasContent && !chain.Visible, $"La cadena reducida no se retira ({where}).");
                        continue;
                    }
                    // The final Error slots in, then the chain is read back one link at a time.
                    Check(chain.Entering && chain.ResolvedSteps == 0, $"El último eslabón no entra antes de resolver ({where}).");
                    chain.Advance(ResponseChain.EnterSeconds + ResponseChain.TraceSeconds + 0.01);
                    for (var resolvedStep = 1; resolvedStep <= 5; resolvedStep++)
                    {
                        Check(chain.ResolvedSteps == resolvedStep, $"Paso de resolución {resolvedStep}: {chain.ResolvedSteps} resueltos ({where}).");
                        for (var i = 0; i < 5; i++)
                            Check(chain.Links[i].Outcome == (i >= 5 - resolvedStep ? outcomes[i] : ResponseChain.Outcome.Pending),
                                $"El eslabón {i + 1} muestra {chain.Links[i].Outcome} en el paso {resolvedStep} ({where}).");
                        if (resolvedStep == 3 && size.X is 1440 or 700 or 844 or 1024) { chain.QueueRedraw(); await Capture($"response-chain-resolving-{size.X}x{size.Y}-seat{seat}"); }
                        chain.Advance(ResponseChain.StepSeconds);
                    }
                    if (size.X is 1440 or 700) { chain.QueueRedraw(); await Capture($"response-chain-resolved-{size.X}x{size.Y}-seat{seat}"); }
                    Check(chain.HasContent && chain.Opacity >= 1, $"La cadena desaparece antes de mostrar el último resultado ({where}).");
                    chain.Advance(ResponseChain.HoldSeconds);
                    chain.Advance(ResponseChain.FadeSeconds / 2);
                    Check(chain.HasContent && chain.Opacity is > 0 and < 1, $"La cadena no se desvanece ({where}).");
                    chain.Advance(ResponseChain.FadeSeconds);
                    Check(!chain.HasContent && !chain.Visible, $"La cadena resuelta no se retira ({where}).");
                }
            }
            // Recovery shows an open chain in place, without animation, and a gap drops a playback in progress.
            chain.ReducedMotion = false; chain.Clear();
            var recovering = Fixture();
            Check(recovering.Attack(0, 2, 0).Success && recovering.PlayCode(0, 0).Success && recovering.PlayCode(1, 0).Success, "No se prepara la recuperación.");
            var recovered = DuelSnapshot.For(recovering, 1, 990);
            PresentTestSnapshot(recovered); SyncResponseChain(recovered, false); await Layout();
            Check(chain.Links.Count == 3 && !chain.Entering && !chain.Resolving, "Al recuperar, la cadena abierta vuelve a entrar animada.");
        }
        finally { chain.ReducedMotion = reduced; chain.ManualClock = false; chain.Clear(); }
        GD.Print("PVP_RESPONSE_CHAIN_PASS · cinco eslabones, orden, anulaciones, cuatro tamaños, ambos asientos y movimiento reducido.");
    }
}
