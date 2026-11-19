using Godot;
using Relick.Core;
using Relick.Network.Types;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckOpeningReveal()
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        async Task Layout() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        var capture = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
        async Task Capture(string stage)
        {
            if (capture == null || DisplayServer.GetName() == "headless") return;
            System.IO.Directory.CreateDirectory(capture);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(capture, $"apertura-{stage}.png"));
        }

        var director = GetNode<SoundDirector>("/root/SoundDirector");
        var sounds = new List<SoundCue>();
        void Heard(SoundCue cue) => sounds.Add(cue);
        var previous = _snapshot;
        var previousReducedMotion = _impacts.ReducedMotion;
        var previousPresented = _openingRevealPresented;
        var previousScale = GetWindow().ContentScaleSize;
        var previousSize = GetWindow().Size;
        var skills = new InitialSkillInfo(false, "", [CardCatalog.Destiny.Id, CardCatalog.Magnet.Id]);
        var preparation = new PreparationInfo { DeckCountOne = 15, DeckCountTwo = 15 };
        var relikarios = new RoomRelikarioInfo(RoomCode, RelikarioCatalog.Yuno.Id, RelikarioCatalog.Osamu.Id);
        director.SoundPlayed += Heard;
        try
        {
            _impacts.ReducedMotion = false;
            foreach (byte seat in new byte[] { 0, 1 })
            {
                var size = seat == 0 ? new Vector2I(1440, 900) : new Vector2I(700, 390);
                var room = new RoomInfo(RoomCode, seat, "Ana", "Beto", true);
                GetWindow().Size = GetWindow().ContentScaleSize = size;
                await Layout();
                ResetAnimations(); _openingRevealPresented = false; _initialSkillViewKey = null;
                _initialSkillCandidate = null; sounds.Clear();
                PresentInitialSkillChoice(room, skills, true, false, preparation, animateEntrance: true);
                Check(!IsAnimating, "La presentación comenzó antes de recibir los Relikarios de la sala.");
                PresentInitialSkillChoice(room, skills, true, false, preparation, animateEntrance: true, relikarios: relikarios);
                await Layout();
                var expected = seat == 0 ? new[] { RelikarioCatalog.Yuno, RelikarioCatalog.Osamu } : [RelikarioCatalog.Osamu, RelikarioCatalog.Yuno];
                var bounds = _openingRelikarios.GetGlobalRect().Grow(1);
                Check(IsAnimating && _initialSkillCards.All(card => card.Disabled) && InitialSkillConfirmButton!.Disabled,
                    "La elección inicial acepta interacción durante la presentación.");
                SelectInitialSkillCandidate(skills.Options[0]);
                Check(_initialSkillCandidate == null, "Se cambió la habilidad detrás de la presentación.");
                Check(_openingRelikarios.Cards.SequenceEqual(expected.Select(form => RelikarioArt.Card(form.Id, 1))) &&
                    _openingRelikarios.Cards.All(card => card != null) && _openingRelikarios.Portraits.All(portrait => !portrait.Visible),
                    "La escena no empieza con las cartas Lv 1 de ambos asientos.");
                var meters = _openingRelikarios.Meters;
                Check(meters.All(meter => bounds.Encloses(meter.GetGlobalRect()) && meter.VisibleHealth == 0 && meter.VisibleRatio == 0 && !meter.FinalBlow) &&
                    _snapshot!.Players[0].Health == RelikarioCatalog.Yuno.MaxHealth && _snapshot.Players[1].Health == RelikarioCatalog.Osamu.MaxHealth,
                    "La vida inicial no se dibuja vacía o modifica la vida real del Relikario.");
                await Capture($"cartas-{size.X}");
                _openingRelikarios.AdvancePlayback(.65);
                Check(_openingRelikarios.Portraits.Select(p => p.RelikarioId).SequenceEqual(expected.Select(form => form.Id)) &&
                    _openingRelikarios.Portraits.All(p => p.Visible && p.AssetsReady && p.Motion == PortraitMotion.Idle) &&
                    meters.All(meter => meter.VisibleHealth == 0) && sounds.Count == 0,
                    "Ambas cartas deben transformarse antes del primer pulso de vida.");
                _openingRelikarios.AdvancePlayback(.15);
                Check(meters.All(meter => meter.VisibleHealth == 1 && meter.VisibleRatio > 0), "Los dos contadores no empezaron juntos.");
                ApplyLayout(); await Layout();
                Check(IsAnimating && meters.All(meter => meter.VisibleHealth == 1), "Reacomodar la interfaz interrumpió la presentación.");
                for (var tick = 2; tick <= 10; tick++)
                {
                    _openingRelikarios.AdvancePlayback(.08);
                    Check(sounds.Count == tick && meters.All(meter => (meter.VisibleRatio == 1) == (tick == 10)),
                        "El pulso de audio se duplica o una barra se completa antes que la otra.");
                    if (tick is 5 or 10) await Capture($"{(tick == 5 ? "carga" : "completa")}-{size.X}");
                }
                Check(sounds.All(cue => cue == SoundCue.OpeningLife) &&
                    meters.Select(meter => meter.VisibleHealth).SequenceEqual(expected.Select(form => form.MaxHealth)),
                    "La carga no usa su propio sonido o no termina en los máximos correctos.");
                _openingRelikarios.AdvancePlayback(1); UpdateEnabled();
                SelectInitialSkillCandidate(skills.Options[0]);
                Check(!IsAnimating && _initialSkillCandidate == skills.Options[0] && InitialSkillConfirmButton?.Disabled == false,
                    "La presentación termina sin habilitar la elección de habilidad.");
                PresentInitialSkillChoice(room, skills, true, true, preparation, animateEntrance: true, relikarios: relikarios);
                Check(!IsAnimating, "La presentación se repitió al confirmar la elección.");
            }

            var recovered = new RoomInfo(RoomCode, 0, "Ana", "Beto", true);
            _openingRevealPresented = false; _initialSkillViewKey = null; _initialSkillCandidate = null;
            PresentInitialSkillChoice(recovered, skills, true, false, preparation, relikarios: relikarios);
            Check(!IsAnimating, "Una sala recuperada reproduce la presentación de entrada.");
            _initialSkillViewKey = null;
            PresentInitialSkillChoice(recovered, skills, true, false, preparation, animateEntrance: true, relikarios: relikarios);
            Check(IsAnimating, "La entrada de prueba no se inicia.");
            ResetAnimations(); UpdateEnabled();
            PresentInitialSkillChoice(recovered, skills, true, true, preparation, animateEntrance: true, relikarios: relikarios);
            Check(!IsAnimating && _life.All(meter => meter.VisibleHealth == meter.Maximum), "Cancelar la entrada deja la vida a medias o vuelve a reproducirla.");
            _openingRevealPresented = false; _initialSkillViewKey = null; _impacts.ReducedMotion = true;
            PresentInitialSkillChoice(recovered, skills, true, false, preparation, animateEntrance: true, relikarios: relikarios);
            Check(!IsAnimating && _openingRevealPresented && CanChooseInitialSkill, "Movimiento reducido retrasa la elección inicial.");
        }
        finally
        {
            director.SoundPlayed -= Heard;
            _impacts.ReducedMotion = previousReducedMotion;
            _openingRevealPresented = previousPresented; _initialSkillViewKey = null;
            GetWindow().Size = previousSize; GetWindow().ContentScaleSize = previousScale;
            ResetAnimations();
            if (previous != null) PresentTestSnapshot(previous);
            await Layout();
        }
        GD.Print("PVP_OPENING_REVEAL_PASS · dos Relikarios, carga simultánea, pulso de audio, selección inicial y reconexión.");
    }
}
