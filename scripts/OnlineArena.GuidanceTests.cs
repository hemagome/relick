using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckDecisionGuidance()
    {
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        GameEngine Fixture(int actor)
        {
            var game = GameEngine.FromOpening(new DuelOpening { Names = ["Ana", "Beto"], StartingSeat = actor,
                Hands = [[CardCatalog.Attack.Id, CardCatalog.Magnet.Id, CardCatalog.Senshuni.Id], [CardCatalog.Attack.Id, CardCatalog.Magnet.Id, CardCatalog.Senshuni.Id]],
                Decks = [Enumerable.Repeat("attack", 20).ToList(), Enumerable.Repeat("attack", 20).ToList()], Bonuses = [null, null] }, new SeededRandom(91));
            game.State.Turn = 2; game.State.Phase = Phase.Combat;
            game.State.Active.Skills.Add(CardCatalog.Magnet.Id);
            game.State.LearnedThisTurn = game.State.AttackedThisTurn = true;
            return game;
        }
        async Task Layout() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        async Task Capture(string name)
        {
            var directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
            if (directory == null || DisplayServer.GetName() == "headless") return;
            System.IO.Directory.CreateDirectory(directory);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, name + ".png"));
        }
        foreach (var actor in new[] { 0, 1 })
        {
            void Available(string title, Action<GameEngine> change, DuelMoveKind kind)
            {
                var game = Fixture(actor); change(game); var s = DuelSnapshot.For(game, actor, 600);
                Check(s.Options.Any(o => o.Move.Kind == kind) && !ArenaGuidance.SuggestAdvance(s), "Se invita a avanzar con " + title + " disponible.");
            }
            Available("ataque", g => g.State.AttackedThisTurn = false, DuelMoveKind.Attack);
            Available("aprendizaje", g => { g.State.LearnedThisTurn = g.State.AttackedThisTurn = false; g.State.Active.Relikia = 500; }, DuelMoveKind.Learn);
            Available("aprendizaje tras atacar", g => { g.State.LearnedThisTurn = false; g.State.AttackedThisTurn = true; g.State.Active.Relikia = 500; }, DuelMoveKind.Learn);
            Available("COMBO", g => g.State.Active.Hand.Add(CardCatalog.PlumberStar.Id), DuelMoveKind.PlayCode);
            Available("COMBO propio con respuestas omitidas", g => { g.State.Active.Hand.Add(CardCatalog.PlumberStar.Id); g.SetComboResponses(actor, false); }, DuelMoveKind.PlayCode);
            Available("Bonus", g => g.State.Active.Bonus = CardCatalog.Jessica.Id, DuelMoveKind.FieldCode);
            Available("canje", g => g.State.Active.Exchange.Add(CardCatalog.Jessica.Id), DuelMoveKind.FieldCode);
            Available("evolución", g => { g.State.Active.HasNaturalSix = true; g.State.Active.Relikia = 1800; }, DuelMoveKind.Evolve);
            Available("equipo", g => { g.State.Phase = Phase.Actions; g.State.Active.Hand.Add(CardCatalog.Bonfire.Id); }, DuelMoveKind.Equip);
            Available("Relikia", g => { g.State.Phase = Phase.Actions; g.State.Active.Relikia = 500; }, DuelMoveKind.Relikia);
            Available("código desde memoria", g => { g.State.Phase = Phase.Actions; g.State.Active.Memory.Add(CardCatalog.Senshuni.Id); g.State.Active.Relikia = 400; }, DuelMoveKind.Relikia);
            var waiting = Fixture(actor); var responder = 1 - actor;
            waiting.State.Players[responder].Hand.Add(CardCatalog.PlumberStar.Id);
            var response = DuelSnapshot.For(waiting, responder, 600);
            Check(response.Options.Any(o => o.Move.Kind == DuelMoveKind.PlayCode) && ArenaGuidance.Instruction(response).Contains("COMBO") &&
                !ArenaGuidance.SuggestAdvance(response), "La espera omite un COMBO legal o invita al rival a avanzar.");
            waiting.SetComboResponses(responder, false); response = DuelSnapshot.For(waiting, responder, 600);
            Check(!response.Options.Any(o => o.Move.Kind == DuelMoveKind.PlayCode) && !ArenaGuidance.Instruction(response).Contains("COMBO"),
                "La espera anuncia respuestas COMBO que el jugador decidió omitir.");
            var conditional = Fixture(actor); conditional.State.Active.Hand.Add(CardCatalog.Reroll.Id);
            Check(ArenaGuidance.SuggestAdvance(DuelSnapshot.For(conditional, actor, 600)), "Un COMBO que requiere un dado inexistente impide avanzar.");
            var pending = Fixture(actor); pending.State.LearnedThisTurn = pending.State.AttackedThisTurn = false;
            Check(pending.Attack(actor, 0, 0).Success, "No inicia el ataque de la prueba.");
            Check(!ArenaGuidance.SuggestAdvance(DuelSnapshot.For(pending, actor, 600)), "Se invita a avanzar durante la resolución de un ataque.");
            var discarded = Fixture(actor); discarded.State.Phase = Phase.Final;
            discarded.State.Active.Hand.AddRange(Enumerable.Repeat(CardCatalog.Attack.Id, 6));
            Check(!ArenaGuidance.SuggestAdvance(DuelSnapshot.For(discarded, actor, 600)), "Se invita a finalizar sin completar los descartes obligatorios.");

            foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
            foreach (var seat in new[] { 0, 1 })
            {
                var game = Fixture(actor); var before = game.SaveSnapshot();
                GetWindow().Size = size; GetWindow().ContentScaleSize = size; await Layout();
                ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, seat, 601)); await Layout();
                var own = seat == actor;
                Check(ArenaGuidance.SuggestAdvance(_snapshot!) == own, "La invitación no respeta al jugador que decide.");
                Check(own ? _selection.Text.Contains("Pasa a acciones") : _selection.Text.Contains("rival") && !_selection.Text.Contains("COMBO"), "La instrucción no explica avanzar o esperar.");
                async Task CheckSilentInspection()
                {
                    var code = _handCards[2];
                    _hand!.EnsureControlVisible(code); await Layout();
                    code.Disabled = false;
                    GuiInputTestDriver.Click(code); await Layout();
                    Check(_decisionPopup is { Visible: false } && _selection.Text.Length == 0 &&
                        _inspector.HasCard && _contextTitle.Text == CardCatalog.Senshuni.Name,
                        $"Consultar un código {(!own ? "en el turno rival" : "fuera de acciones")} abre un aviso u oculta la carta en {size}.");
                }
                if (own)
                {
                    var button = _advanceButton!; var rect = button.GetGlobalRect();
                    Check(PresentedOption(button)?.Move.Kind == DuelMoveKind.Advance && button.Text == "Ir a acciones", "La invitación cambia el destino o el índice autorizado.");
                    button.Disabled = false; RefreshAdvanceInvitation(false);
                    Check(button.Inviting && button.IsProcessing(), "No comienza el pulso al agotarse las jugadas.");
                    var alpha = button.PulseOpacity; button._Process(0.3);
                    Check(button.PulseOpacity != alpha && button.GetGlobalRect() == rect, "El pulso no avanza o desplaza el objetivo táctil.");
                    if (actor == 0 && seat == 0) await Capture($"advance-invitation-{size.X}x{size.Y}");
                    RefreshAdvanceInvitation(true);
                    Check(!button.Inviting && !button.IsProcessing(), "El pulso continúa durante un bloqueo.");
                    button.Disabled = false; RefreshAdvanceInvitation(false); _lastDisabled = null; UpdateEnabled();
                    Check(!button.Inviting && button.Disabled, "La desconexión mantiene la invitación o permite avanzar.");
                    SelectCard(0);
                    await Layout();
                    Check(_selection.Text.Contains("Ya realizaste tu ataque"), "Tocar un Ataque agotado no explica su bloqueo.");
                    await CheckSilentInspection();
                    Check(_advanceButton == button && button.GetGlobalRect() == rect,
                        $"El inspector reemplaza o desplaza el avance en {size}.");
                    ClearContext(); RefreshRuntimeGuidance();
                    Check(_selection.Text.Contains("Conexión interrumpida"), "La instrucción no explica la desconexión.");
                    await Layout();
                    Check(button.GetGlobalRect() == rect, $"Cerrar el inspector desplaza el avance en {size}.");
                    button.Disabled = false;
                    GuiInputTestDriver.Click(button);

                    foreach (var (phase, destination, label, repeat) in new[]
                    {
                        (Phase.Draw, Phase.Combat, "Ir a combate", false),
                        (Phase.Actions, Phase.Final, "Finalizar turno", false),
                        (Phase.Combat, Phase.Combat, "Repetir combate", true),
                        (Phase.Actions, Phase.Actions, "Repetir acciones", true)
                    })
                    {
                        var next = Fixture(actor);
                        next.State.Phase = phase;
                        if (repeat && phase == Phase.Combat) next.State.ExtraCombatStages = 1;
                        if (repeat && phase == Phase.Actions) next.State.ExtraActionStages = 1;
                        PresentTestSnapshot(DuelSnapshot.For(next, seat, 602)); await Layout();
                        var nextButton = _advanceButton!;
                        var frame = nextButton.GetThemeStylebox("normal");
                        var textWidth = nextButton.GetThemeFont("font").GetStringSize(label, fontSize: nextButton.GetThemeFontSize("font_size")).X;
                        Check(nextButton.Text == label && nextButton.GetGlobalRect() == rect &&
                            frame is StyleBoxFlat flat && flat.BorderColor == StageAppearance.For(destination).Accent &&
                            // The desktop board is a scaled stage: the label is measured against the button's own width.
                            textWidth + frame.ContentMarginLeft + frame.ContentMarginRight <= nextButton.Size.X,
                            $"El avance pierde su posición o el color de destino para {phase} en {size}.");
                    }
                }
                else await CheckSilentInspection();
                Check(game.SaveSnapshot() == before, "El pulso o la consulta ejecutan una jugada automáticamente.");
                var minimum = _root!.GetCombinedMinimumSize();
                Check(minimum.X <= Size.X + 1 && minimum.Y <= Size.Y + 1, $"Las instrucciones desbordan {size}: {minimum}.");
            }
            foreach (var phase in new[] { Phase.Draw, Phase.Actions, Phase.Final })
            {
                var game = Fixture(actor); game.State.Phase = phase; game.State.Active.ActionsLeft = 0;
                var s = DuelSnapshot.For(game, actor, 602);
                Check(ArenaGuidance.SuggestAdvance(s), "No se invita a avanzar al agotarse " + phase);
                game.State.ExtraDrawStages = game.State.ExtraCombatStages = game.State.ExtraActionStages = game.State.ExtraFinalStages = 1;
                Check(ArenaGuidance.NextStep(DuelSnapshot.For(game, actor, 602)).StartsWith("Repite"), "Se promete otra fase cuando toca repetirla.");
            }
        }
        GD.Print("PVP_DECISION_GUIDANCE_PASS · jugadas legales completas, combos condicionales, respuestas, descartes, bloqueo, pulso sin desplazamiento ni autoavance, cuatro tamaños y ambos asientos.");
    }
}
