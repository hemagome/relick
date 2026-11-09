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
            ResetAnimations(); _openingKey = null; _openingDealt = false; _openingReturns.Clear(); _initialSkillCandidate = null;
            var room = new RoomInfo("UITEST00", seat, "Ana", "Beto", true);
            var info = new PreparationInfo { HandCountOne = 5, HandCountTwo = 5, DeckCountOne = 15, DeckCountTwo = 15 };
            var hand = new[] { "magnet-shuffle", "magnet-shuffle", "senshuni", "extra-life", "attack" }
                .Select((id, index) => new NetworkCard((ulong)(100 + index), id, (uint)index)).ToArray();
            var skills = new InitialSkillInfo(false, "", ["destiny-shuffle", "magnet-shuffle"]);
            var beforeDeal = new PreparationInfo { DeckCountOne = (uint)CardCatalog.TrainingDeck().Count, DeckCountTwo = (uint)CardCatalog.TrainingDeck().Count };
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
            PresentPreparation(room, info, hand, true, false); await Layout(); await Drain(); UpdateEnabled();
            Check(!IsChoosingInitialSkill && _root!.FindChild("InitialSkillScroll", true, false) == null, "La elección de habilidad permanece sobre el mulligan.");
            var minimum = _root!.GetCombinedMinimumSize();
            Check(minimum.X <= _root.Size.X + 1 && minimum.Y <= _root.Size.Y + 1, $"Preparación desborda {size}: {minimum}, raíz {_root.Size}.");
            Check(_snapshot!.Players[1 - seat].Hand.All(id => id == null), "La proyección inicial contiene una carta rival.");
            Check(FindChildren("OpponentHandCard*", "Button", true, false).OfType<OpponentHandCard>().All(c => c.IsFaceDown), "El reparto rival muestra frentes.");
            Check(PreparationConfirmButton?.Disabled == false, "No se habilita la confirmación tras el reparto.");
            for (var slot = 0; slot < 2; slot++) GuiInputTestDriver.Click((Control)_root.FindChild("HandCard" + slot, true, false));
            Check(OpeningSelectionCount == 2 && PreparationConfirmButton!.Text.Contains("Cambiar 2"), "Dos copias iguales no se seleccionan de forma independiente.");
            await Layout(); await Capture($"preparation-seat{seat}-{size.X}x{size.Y}");
            info.MyConfirmed = true; info.MyReturnedCards = [100, 101];
            PresentPreparation(room, info, hand, true, false); await Layout();
            Check(PreparationConfirmButton!.Disabled && FindChildren("HandCard*", "Button", true, false).OfType<HandCard>().All(c => c.Disabled), "Se puede editar una mano confirmada.");
            Check(_selection.Text.Contains("rival"), "No se muestra la espera del rival.");
            _openingConnected = false; _lastDisabled = null; UpdateEnabled();
            PresentPreparation(room, info, hand, true, false); await Layout();
            Check(!IsAnimating && OpeningSelectionCount == 2 && PreparationConfirmButton!.Disabled, "Recuperar la preparación repite el reparto o pierde la elección.");
            var own = _snapshot.Players[seat]; own.Hand = ["senshuni", "extra-life", "attack", "points-increase", "load-error", "attack"];
            own.HandCount = 6; own.Bonus = "attack"; own.DeckCount = 13;
            _snapshot.Players[1 - seat].Bonus = "attack";
            var combat = new DuelSnapshot { MySeat = seat, CurrentPlayer = seat, DecisionPlayer = seat, Phase = Phase.Combat,
                Turn = 1, Players = _snapshot.Players };
            var origins = CaptureMulliganOrigins(combat);
            _snapshot = combat; _preparing = false; _selected.Clear(); ResetAnimations(); Render(); await Layout();
            var opening = new PreparationInfo { Started = true, StartingSeat = seat,
                RollOne = (byte)(seat == 0 ? 6 : 1), RollTwo = (byte)(seat == 1 ? 6 : 1) };
            var initiativeCount = PlayedInitiativeRolls;
            AnimateOpening(combat, origins, opening);
            Check(_flights.Count(f => f.Event.Kind == DuelVisualKind.Draw && f.Event.Player == seat) == 2 &&
                _openingTurnDraw?.Event.Player == seat, "Faltan los reemplazos o el robo de turno posterior a la iniciativa.");
            Check(_flights.All(f => f.Event.Kind != DuelVisualKind.Return || f.Event.Player == seat), "Se inventó un mulligan oculto del rival.");
            Check(!_initiativeOverlay.Visible, "Los dados se muestran antes de terminar el mulligan y los Bonus.");
            for (var i = 0; !_initiativeStarted && i < 40; i++) { UpdateFlights(0.5); await Layout(); }
            Check(_initiativeOverlay.Visible && _initiativeDice.All(d => d.IsRolling) && PlayedInitiativeRolls == initiativeCount + 1,
                "No se lanzan los dos dados de iniciativa a la vez.");
            Check(_initiativeNames[seat].Text.Contains("TÚ") && _initiativeNames[1 - seat].Text.Contains("RIVAL") &&
                _initiativeValues.All(v => v.Text == "Rodando…") && !_initiativeWinner.Text.Contains("Empieza"),
                "La iniciativa confunde al dueño de un dado o revela el ganador antes de aterrizar.");
            UpdateEnabled(); Check(_handCards.All(c => c.Disabled), "Se puede jugar durante la iniciativa.");
            Check(_openingTurnDraw != null && _flights.Count == 0, "El robo inicial se adelanta a los dados.");
            await Capture($"initiative-rolling-seat{seat}-{size.X}x{size.Y}");
            foreach (var die in _initiativeDice) die.AdvancePlayback(DiceFace.RollSeconds);
            UpdateFlights(0);
            Check(_initiativeDice[0].DisplayedValue == opening.RollOne && _initiativeDice[1].DisplayedValue == opening.RollTwo &&
                _initiativeValues[0].Text == opening.RollOne.ToString() && _initiativeValues[1].Text == opening.RollTwo.ToString() &&
                _initiativeWinner.Text == $"Empieza {combat.Players[seat].Name}", "El resultado visual no coincide con la iniciativa del servidor.");
            await Capture($"initiative-result-seat{seat}-{size.X}x{size.Y}");
            Check(new Rect2(Vector2.Zero, Size).Encloses(_initiativePanel.GetGlobalRect()), $"La iniciativa desborda {size}.");
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
