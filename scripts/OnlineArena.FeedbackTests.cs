using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    /// <summary>
    /// `--feedback-only`: status in words on the phase bar, refused plays in the inspector, hand cards
    /// with an authorized play and the attack table, on the desktop board and a compact layout, both seats.
    /// </summary>
    internal async Task CheckDecisionFeedback()
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
        const int actor = 0, rival = 1;
        GameEngine Fixture()
        {
            var game = GameEngine.FromOpening(new DuelOpening { Names = ["Ana", "Beto"], StartingSeat = actor,
                Hands = [[CardCatalog.Attack.Id, CardCatalog.Magnet.Id, CardCatalog.Senshuni.Id, CardCatalog.PlumberStar.Id],
                    [CardCatalog.Attack.Id, CardCatalog.Senshuni.Id, CardCatalog.PlumberStar.Id, CardCatalog.Reroll.Id]],
                Decks = [Enumerable.Repeat("attack", 20).ToList(), Enumerable.Repeat("attack", 20).ToList()], Bonuses = [null, null] }, new SeededRandom(91));
            game.State.Turn = 2; game.State.Phase = Phase.Combat;
            game.State.Players[actor].Skills.AddRange([CardCatalog.Destiny.Id, CardCatalog.Reverse.Id]);
            game.State.Players[actor].Relikia = 500;
            game.State.Players[actor].Bonus = CardCatalog.Jessica.Id;
            game.State.Players[actor].Exchange.Add(CardCatalog.Jessica.Id);
            game.State.Players[rival].Skills.Add(CardCatalog.Magnet.Id);
            return game;
        }
        string[] Faces() => _inspector.OutlookValues.Select(v => v.Text).ToArray();
        IEnumerable<int> Expected(DuelSnapshot s) => s.Options
            .Where(o => o.Move.Index >= 0 && o.Move.Kind is DuelMoveKind.Learn or DuelMoveKind.Attack or DuelMoveKind.PlayCode or
                DuelMoveKind.ActivateArea or DuelMoveKind.Equip or DuelMoveKind.Exchange)
            .Select(o => o.Move.Index).Distinct().Order();

        // Every status branch, without a server: words and icons, not only colours.
        foreach (var seat in new[] { actor, rival })
        {
            var s = DuelSnapshot.For(Fixture(), seat, 700);
            var own = seat == actor;
            var (headline, detail, _, icon) = DecisionStatus(s, true, false, false, false);
            Check(headline == (own ? "TU TURNO" : "TURNO RIVAL") && detail.StartsWith("TURNO 02") &&
                icon == (own ? GameIcon.Bolt : GameIcon.Hourglass), $"Estado de turno incorrecto para el asiento {seat}: {headline}.");
            s.DecisionPlayer = rival; // Beto decides during Ana's turn: Ana waits, Beto responds.
            Check(DecisionStatus(s, true, false, false, false).Headline == (own ? "RIVAL RESPONDE" : "RESPONDE"),
                "No se distingue responder de esperar.");
            Check(DecisionStatus(s, false, true, true, true).Headline == "SIN CONEXIÓN" &&
                DecisionStatus(s, true, true, true, true).Headline == "JUGADA RECHAZADA" &&
                DecisionStatus(s, true, true, true, false).Headline == "ENVIANDO…" &&
                DecisionStatus(s, true, false, true, false).Headline == "RESOLVIENDO…", "La prioridad de los estados es incorrecta.");
        }

        foreach (var size in new[] { new Vector2I(1440, 1024), new(1820, 1024), new(1024, 768) })
        {
            GetWindow().Size = size; GetWindow().ContentScaleSize = size; await Layout();
            Vector2? dock = null;
            foreach (var seat in new[] { actor, rival })
            {
                var own = seat == actor;
                var game = Fixture(); var before = game.SaveSnapshot();
                ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, seat, 701)); await Layout();
                var expected = Expected(_snapshot!).ToArray();
                Check(PlayableHandIndices.SequenceEqual(expected), $"Cartas jugables {string.Join(",", PlayableHandIndices)} ≠ autorizadas {string.Join(",", expected)} en {size}.");
                if (own) Check(expected.Contains(0) && expected.Contains(1) && expected.Contains(3) && !expected.Contains(2),
                    "El fixture no ofrece Ataque, aprendizaje y COMBO, o permite Senshuni en combate.");
                else Check(PlayableHandIndices.SequenceEqual([2]) && _decisionPopup is { Visible: true } && _decisionTitle.Text == "COMBO disponible",
                    "El COMBO disponible durante el turno rival debe brillar y anunciarse en el centro.");
                if (own && size.X == 1820)
                {
                    // The offline fixture disables the hand; enable it to see the halo as a player does.
                    foreach (var card in _handCards) card.Disabled = false;
                    await Capture($"feedback-playable-{size.X}x{size.Y}");
                    _lastDisabled = null; UpdateEnabled();
                }

                RefreshDecisionFeedback();
                if (ArenaViewport.Desktop(this))
                {
                    // The test connection is offline, which is what the bar must say.
                    Check(StageStatusText == "SIN CONEXIÓN", $"La barra no refleja la conexión: {StageStatusText}.");
                    var turnDock = (Control)_root!.FindChild("TurnDock", true, false);
                    dock ??= turnDock.Size;
                    Check(turnDock.Size.IsEqualApprox(dock.Value), $"La barra cambia de ancho entre turnos en {size}: {turnDock.Size} ≠ {dock}.");
                    var lane = (Control)_root.FindChild("CombatCenter", true, false);
                    Check(lane.GetGlobalRect().Grow(1).Encloses(turnDock.GetGlobalRect()), $"La barra desborda su carril en {size}.");
                }
                else Check(StageStatusText.Length == 0, "Las composiciones compactas conservan su indicador propio.");

                // A refused play is told in the inspector and survives a rebuild of the board.
                Connection.RejectPlay("La partida cambió. Revisa el tablero antes de volver a jugar.");
                RefreshDecisionFeedback();
                Check(_decisionRejection.IsVisibleInTree() && _decisionRejection.Text.Contains("La partida cambió"), "El rechazo no aparece en el aviso central.");
                PresentTestSnapshot(DuelSnapshot.For(game, seat, 702)); RefreshDecisionFeedback(); await Layout();
                Check(_decisionRejection.IsVisibleInTree(), "El rechazo desaparece al reconstruir el tablero.");
                if (own && size.X == 1440) await Capture($"feedback-rejection-{size.X}x{size.Y}");
                _rejectionUntil = Time.GetTicksMsec() - 1; RefreshDecisionFeedback();
                Check(!_decisionRejection.IsVisibleInTree(), "El rechazo no caduca.");
                Connection.RejectPlay("Otra"); RefreshDecisionFeedback(); ClearRejection(); RefreshDecisionFeedback();
                Check(!_decisionRejection.IsVisibleInTree(), "Jugar de nuevo no retira el rechazo.");

                // A rival skill on the board: Magnet's normal damage is its own die.
                SelectField(rival, FieldZone.Skill, 0); await Layout();
                Check(_inspector.Outlook.IsVisibleInTree() && Faces().SequenceEqual(["½·1D6", "1D6", "5"]), $"Tabla de Magnet incorrecta: {string.Join(" ", Faces())}.");
                ClearContext();
                if (!own)
                {
                    Check(game.SaveSnapshot() == before, "La consulta alteró el duelo.");
                    game.SetComboResponses(seat, false);
                    ApplyPrivateComboPreference(DuelSnapshot.For(game, seat, 702)); await Layout();
                    Check(PlayableHandIndices.Count == 0 && _decisionPopup is { Visible: false },
                        "El COMBO sigue anunciado con las respuestas omitidas.");
                    continue;
                }

                // A skill card in hand has no copy yet: base values, labelled as such.
                SelectCard(1); await Layout();
                Check(_inspector.Outlook.IsVisibleInTree() && _inspector.OutlookCaption.Text == "Daño", "La habilidad en mano no muestra su tabla base.");
                // An Attack with no skill chosen yet promises nothing.
                SelectCard(0); await Layout();
                Check(!_inspector.Outlook.IsVisibleInTree(), "El Ataque muestra una tabla antes de elegir habilidad.");
                SelectSkill(0); await Layout();
                Check(_inspector.Outlook.IsVisibleInTree() && _inspector.OutlookCaption.Text.Contains(CardCatalog.Destiny.Name) &&
                    Faces().SequenceEqual(["2", "3", "5"]), $"Tabla de Destiny incorrecta: {string.Join(" ", Faces())}.");
                if (size.X == 1820) await Capture($"feedback-attack-outlook-{size.X}x{size.Y}");

                // Choosing among several skills: reading one shows what this Attack would do with it.
                ClearContext(); SelectCard(0); _choosingAttackSkill = true; BuildMoves(); await Layout();
                var reverse = _ownSkills.Single(tile => tile.SkillIndex == 1);
                reverse.EmitSignal(Control.SignalName.MouseEntered); await Layout();
                Check(_contextTitle.Text == CardCatalog.Reverse.Name && _contextCost.Text == "Consume 1 Ataque" &&
                    Faces().SequenceEqual(["1", "2", "4"]), $"La lectura durante la elección de habilidad es incorrecta: {_contextTitle.Text} · {string.Join(" ", Faces())}.");
                reverse.EmitSignal(Control.SignalName.MouseExited); await Layout();
                Check(_inspectorPreview == null && _choosingAttackSkill, "Salir de la habilidad no devuelve la lectura del Ataque.");
                ClearContext();
                Check(game.SaveSnapshot() == before, "Las lecturas o el halo ejecutaron una jugada.");

                // Lv 2 and Estrella del Fontanero are public and change the table.
                var boosted = Fixture();
                boosted.State.Players[actor].Relikario = RelikarioCatalog.YunoLv2; boosted.State.Players[actor].StarEffects = 1;
                PresentTestSnapshot(DuelSnapshot.For(boosted, seat, 703)); await Layout();
                SelectCard(0); SelectSkill(0); await Layout();
                Check(Faces().SequenceEqual(["4", "6", "10"]) && _inspector.OutlookNotes.Text.Contains("Lv 2") &&
                    _inspector.OutlookNotes.Text.Contains("×2"), $"Lv 2 o Estrella no se reflejan: {string.Join(" ", Faces())} · {_inspector.OutlookNotes.Text}.");
                ClearContext();
            }
        }
        GD.Print("PVP_DECISION_FEEDBACK_PASS · estado con texto e icono, barra estable, rechazo visible y persistente, cartas jugables autorizadas y tabla de ataque, tres tamaños y ambos asientos.");
    }
}
