using Godot;
using Relick.Core;
using Relick.Network.Types;
using NetworkCard = Relick.Network.Types.HandCard;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckPreparationLayout()
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        async Task Layout()
        {
            for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        async Task Drain()
        {
            for (var i = 0; IsAnimating && i < 100; i++)
            {
                foreach (var die in _initiativeDice) die.AdvancePlayback(0.5);
                UpdateFlights(0.5); await Layout();
                Check(_activeFlight?.Event.Kind != DuelVisualKind.Draw || _activeFlight.Event.Player == _snapshot!.MySeat || _flightFront == null,
                    "El reparto revela una carta rival.");
            }
            Check(!IsAnimating, "Las animaciones de preparación no terminan.");
        }
        var directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
        async Task Capture(string name)
        {
            if (directory == null || DisplayServer.GetName() == "headless") return;
            System.IO.Directory.CreateDirectory(directory);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, name + ".png"));
        }
        foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
        foreach (byte seat in new byte[] { 0, 1 })
        {
            GetWindow().Size = size; GetWindow().ContentScaleSize = size; await Layout();
            GetParent().MoveChild(this, GetParent().GetChildCount() - 1);
            ResetAnimations(); _openingKey = null; Array.Clear(_openingHandsPresented); _openingReturns.Clear(); _initialSkillCandidate = null;
            var room = new RoomInfo("UITEST00", seat, "Ana", "Beto", true);
            var info = new PreparationInfo { HandCountOne = 5, HandCountTwo = 5, DeckCountOne = 15, DeckCountTwo = 15 };
            var hand = new[] { "magnet-shuffle", "magnet-shuffle", "senshuni", "extra-life", "attack" }
                .Select((id, index) => new NetworkCard((ulong)(100 + index), id, (uint)index)).ToArray();
            var skills = new InitialSkillInfo(false, "", ["destiny-shuffle", "magnet-shuffle"]);
            var beforeDeal = new PreparationInfo { DeckCountOne = (uint)CardCatalog.TrainingDeck().Count, DeckCountTwo = (uint)CardCatalog.TrainingDeck().Count };
            if (seat == 0) beforeDeal.HandCountTwo = 5; // The rival chose a skill before this player.
            else info.HandCountOne = 0; // This player chooses first and receives their hand before the rival.
            PresentInitialSkillChoice(room, skills, true, false, beforeDeal); await Layout();
            Check(InitialSkillConfirmButton!.Disabled && _snapshot!.Players[seat].Hand.Length == 0, "Se reparte antes de elegir habilidad.");
            var buildCount = BoardBuildCount; var confirmId = InitialSkillConfirmButton.GetInstanceId();
            GuiInputTestDriver.Click((Control)_root!.FindChild("InitialSkillCard0", true, false)); await Layout();
            Check(BoardBuildCount == buildCount && InitialSkillConfirmButton!.GetInstanceId() == confirmId, "Elegir habilidad reconstruye el tablero.");
            Check(!InitialSkillConfirmButton!.Disabled && _initialSkillCandidate == "destiny-shuffle", "No se puede elegir Destiny.");
            Check(_inspector.TitleLabel.Text == CardCatalog.Destiny.Name && _inspector.Attributes.Visible &&
                !_inspector.DescriptionLabel.Text.Contains("Daño normal:"), "La elección inicial no actualiza el inspector legible.");
            await Capture($"initial-skill-seat{seat}-{size.X}x{size.Y}");
            PresentInitialSkillChoice(room, skills, true, true, beforeDeal); await Layout();
            Check(InitialSkillConfirmButton!.Disabled && _hand!.FindChildren("InitialSkillCard*", "Button", true, false).OfType<HandCard>().All(c => c.Disabled), "La elección pendiente sigue editable.");
            PresentPreparation(room, info, hand, true, false);
            Check(_flights.Count(f => f.Event.Kind == DuelVisualKind.Draw && f.Event.Player == seat) == 5 &&
                !_flights.Any(f => f.Event.Kind == DuelVisualKind.Draw && f.Event.Player != seat),
                "El reparto anima cartas rivales ya mostradas o que todavía no se repartieron.");
            await Layout(); await Drain();
            if (seat == 1)
            {
                info.HandCountOne = 5;
                PresentPreparation(room, info, hand, true, false);
                Check(_flights.Count == 5 && _flights.All(f => f.Event.Kind == DuelVisualKind.Draw && f.Event.Player == 0),
                    "La primera mano rival no se reparte una sola vez cuando llega más tarde.");
                await Layout(); await Drain();
            }
            UpdateEnabled();
            Check(!IsChoosingInitialSkill && _root!.FindChild("InitialSkillScroll", true, false) == null, "La elección de habilidad permanece sobre el mulligan.");
            var minimum = _root!.GetCombinedMinimumSize();
            Check(minimum.X <= _root.Size.X + 1 && minimum.Y <= _root.Size.Y + 1, $"Preparación desborda {size}: {minimum}, raíz {_root.Size}.");
            Check(_snapshot!.Players[1 - seat].Hand.All(id => id == null), "La proyección inicial contiene una carta rival.");
            Check(FindChildren("OpponentHandCard*", "Button", true, false).OfType<OpponentHandCard>().All(c => c.IsFaceDown), "El reparto rival muestra frentes.");
            Check(PreparationConfirmButton?.Disabled == false, "No se habilita la confirmación tras el reparto.");
            for (var slot = 0; slot < 2; slot++) GuiInputTestDriver.Click((Control)_root.FindChild("HandCard" + slot, true, false));
            Check(OpeningSelectionCount == 2 && PreparationConfirmButton!.Text.Contains("Cambiar 2"), "Dos copias iguales no se seleccionan de forma independiente.");
            await Layout(); await Capture($"preparation-seat{seat}-{size.X}x{size.Y}");
            if (seat == 0)
            {
                info.OpponentConfirmed = true;
                PresentPreparation(room, info, hand, true, true);
            }
            else
            {
                info.MyConfirmed = true; info.MyReturnedCards = [100, 101];
                PresentPreparation(room, info, hand, true, false);
            }
            await Layout();
            Check(PreparationConfirmButton!.Disabled && FindChildren("HandCard*", "Button", true, false).OfType<HandCard>().All(c => c.Disabled), "Se puede editar una mano confirmada.");
            Check(!IsAnimating && OpeningSelectionCount == 2, "Confirmar repite el reparto o pierde la selección.");
            if (seat == 1)
            {
                Check(_selection.Text.Contains("rival"), "No se muestra la espera del rival.");
                _openingConnected = false; _lastDisabled = null; UpdateEnabled();
                PresentPreparation(room, info, hand, true, false); await Layout();
                Check(!IsAnimating && OpeningSelectionCount == 2 && PreparationConfirmButton!.Disabled, "Recuperar la preparación repite el reparto o pierde la elección.");
            }
            var starter = (byte)(size.X is 1440 or 844 ? 1 - seat : seat);
            var own = _snapshot.Players[seat]; own.Hand = ["senshuni", "extra-life", "attack", "points-increase", "load-error"];
            own.Bonus = "attack"; own.DeckCount = 13;
            _snapshot.Players[1 - seat].Bonus = "attack";
            var combat = new DuelSnapshot { MySeat = seat, CurrentPlayer = starter, DecisionPlayer = starter, Phase = Phase.Combat,
                Revision = 1, Turn = 1, Players = _snapshot.Players };
            Check(!IsLiveOpening(combat), "Una revisión posterior al inicio vuelve a representar la apertura.");
            combat.Revision = 0;
            Check(IsLiveOpening(combat), "La confirmación no conecta la preparación con el combate inicial.");
            var startingPlayer = combat.Players[starter];
            startingPlayer.Hand = [.. startingPlayer.Hand, starter == seat ? "attack" : null];
            startingPlayer.HandCount++;
            var origins = CaptureMulliganOrigins(combat);
            _snapshot = combat; _preparing = false; _selected.Clear(); ResetAnimations(); Render(); await Layout();
            var opening = new PreparationInfo { Started = true, StartingSeat = starter,
                RollOne = (byte)(starter == 0 ? 6 : 1), RollTwo = (byte)(starter == 1 ? 6 : 1) };
            var initiativeCount = PlayedInitiativeRolls;
            AnimateOpening(combat, origins, opening);
            Check(_flights.Count(f => f.Event.Kind == DuelVisualKind.Draw && f.Event.Player == seat) == 2 &&
                _openingTurnDraw?.Event.Player == starter && _openingTurnDraw.Event.Value == 5,
                "Faltan los reemplazos o la sexta carta de quien empieza.");
            Check(_flights.All(f => f.Event.Kind is not (DuelVisualKind.Return or DuelVisualKind.Draw) || f.Event.Player == seat) &&
                Enumerable.Range(0, 5).All(slot => ZoneNode(1, CardZone.Hand, slot)?.Modulate.A == 1),
                "Confirmar vuelve a animar u oculta la mano rival ya repartida.");
            Check(!_initiativeOverlay.Visible, "Los dados se muestran antes de terminar el mulligan y los Bonus.");
            for (var i = 0; !_initiativeStarted && i < 40; i++) { UpdateFlights(0.5); await Layout(); }
            Check(_initiativeOverlay.Visible && _initiativeDice.All(d => d.IsRolling) && PlayedInitiativeRolls == initiativeCount + 1,
                "No se lanzan los dos dados de iniciativa a la vez.");
            Check(_initiativeNames[seat].Text.Contains("TÚ") && _initiativeNames[1 - seat].Text.Contains("RIVAL") &&
                _initiativeValues.All(v => string.IsNullOrWhiteSpace(v.Text)) && !_initiativeWinner.Text.Contains("Empieza"),
                "La iniciativa confunde al dueño de un dado o revela el ganador antes de aterrizar.");
            UpdateEnabled(); Check(_handCards.All(c => c.Disabled), "Se puede jugar durante la iniciativa.");
            Check(_openingTurnDraw != null && _flights.Count == 0, "El robo inicial se adelanta a los dados.");
            await Capture($"initiative-rolling-seat{seat}-{size.X}x{size.Y}");
            foreach (var die in _initiativeDice) die.AdvancePlayback(DiceFace.RollSeconds);
            UpdateFlights(0);
            Check(_initiativeDice[0].DisplayedValue == opening.RollOne && _initiativeDice[1].DisplayedValue == opening.RollTwo &&
                _initiativeValues[0].Text == opening.RollOne.ToString() && _initiativeValues[1].Text == opening.RollTwo.ToString() &&
                _initiativeWinner.Text == $"Empieza {combat.Players[starter].Name}" && _initiativeHint.Text.Contains("roba una carta"),
                "La iniciativa muestra otro ganador o no anuncia su robo de turno.");
            await Capture($"initiative-result-seat{seat}-{size.X}x{size.Y}");
            Check(new Rect2(Vector2.Zero, Size).Encloses(_initiativePanel.GetGlobalRect()), $"La iniciativa desborda {size}.");
            UpdateFlights(2);
            Check(_flights.Count == 1 && _flights.Peek().Event is { Kind: DuelVisualKind.Draw, Value: 5 } firstDraw && firstDraw.Player == starter,
                "La iniciativa vuelve a repartir cartas en vez de animar un único robo.");
            await Drain(); UpdateEnabled();
            Check(!_initiativeOverlay.Visible && _openingTurnDraw == null, "La iniciativa permanece encima del combate.");
            Check(FindChildren("HandCard*", "Button", true, false).OfType<HandCard>().All(c => c.Modulate.A == 1), "Quedan cartas invisibles al entrar en combate.");
            ResetAnimations(); Render(); await Layout();
            Check(!IsAnimating && !_initiativeOverlay.Visible && PlayedInitiativeRolls == initiativeCount + 1,
                "Recuperar o reconstruir el combate repite la iniciativa.");
            GD.Print($"PVP_OPENING_LAYOUT_PASS · {size} · asiento {seat} · reparto, selección, espera, recuperación y combate.");
        }
    }
}
