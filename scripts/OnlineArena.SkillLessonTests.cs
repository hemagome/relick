using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckSkillLessonAnimation()
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

        var usedPreview = false;
        foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
        foreach (var seat in new[] { 0, 1 })
        foreach (var actor in new[] { 0, 1 })
        {
            var rolling = seat == 1;
            var skill = rolling ? CardCatalog.Reverse : CardCatalog.Magnet;
            var game = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Ana", "Beto"], StartingSeat = actor,
                Hands = [[skill.Id, CardCatalog.Attack.Id, skill.Id], [skill.Id, CardCatalog.Attack.Id, skill.Id]],
                Decks = [[CardCatalog.Attack.Id], [CardCatalog.Attack.Id]], Bonuses = [null, null]
            }, new SeededRandom(76));
            game.State.Phase = Phase.Combat;
            var player = game.State.Active;
            player.Skills.AddRange(rolling ? [CardCatalog.Destiny.Id, CardCatalog.Magnet.Id, CardCatalog.Reverse.Id]
                : [CardCatalog.Destiny.Id, CardCatalog.Magnet.Id]);
            player.SkillPositions = rolling ? new() { [0] = 0, [1] = 1, [2] = 3 } : new() { [0] = 0, [1] = 2 };
            player.Relikia = 1000;

            GetWindow().ContentScaleSize = size;
            await Layout();
            ResetAnimations();
            var before = DuelSnapshot.For(game, seat, 820);
            PresentTestSnapshot(before);
            if (seat == actor) SelectCard(2);
            await Layout();
            var handCard = seat == actor ? ZoneNode(0, CardZone.Hand, 2) : null;
            var cues = DuelVisualEvents.Capture(game);
            Check(new DuelMove { Kind = DuelMoveKind.Learn, Index = 2 }.Apply(game, actor).Success, "No se enseña la habilidad de prueba.");
            Check(cues.Any(c => c.Kind == DuelVisualKind.Die) == rolling, "No se prepara el dado de Reverse Shuffle.");
            var after = DuelSnapshot.For(game, seat, 821);
            var lesson = CaptureSkillLesson(after, before);
            var relative = seat == actor ? 0 : 1;
            Check(lesson?.Face == skill.Id, "La revisión aceptada no anima la habilidad enseñada.");
            if (handCard != null && RectVisible(handCard, ZoneRect(handCard)))
                Check(lesson!.Origin == ZoneRect(handCard), "El vuelo comienza en otra copia de la mano.");
            var origins = CaptureOrigins(after, before);
            PresentTestSnapshot(after);
            Animate(cues, origins, lesson);
            var tile = (SkillTile)_root!.FindChild($"Skill{relative}_{(rolling ? 2 : 1)}", true, false);
            Check(tile.Skill?.Id == skill.Id && tile.AwaitingLesson, "La habilidad aparece en su ranura antes de aterrizar.");
            await Layout();
            UpdateFlights(0); UpdateFlights(0); UpdateFlights(0.1);
            Check(_activeFlight == lesson && _flightTarget == tile && _flightFront?.Card.Id == skill.Id &&
                _flightFront.Visible == (seat == actor) && !_die.IsBusy, "El vuelo empieza con una carta incorrecta o después del dado.");
            usedPreview |= _skillLessonPreview;
            if (size.X == 1440) Check(!_skillLessonPreview, "El vuelo de escritorio no aterriza en la ranura.");
            var scroll = _arenaScroll?.ScrollVertical;
            UpdateFlights(0.29);
            Check(_flightFront!.Visible && _flight!.Rotation > 0.5f && tile.AwaitingLesson &&
                !_die.IsBusy && _root.FindChildren("OpponentHandCard*", "Button", true, false).OfType<OpponentHandCard>().All(card => card.IsFaceDown),
                "La habilidad no gira o el vuelo revela otras cartas de la mano rival.");
            if (actor == 0) await Capture($"skill-lesson-{size.X}-seat{seat}");

            UpdateFlights(1.1);
            Check(_activeFlight == null && !tile.AwaitingLesson && _die.IsBusy == rolling && _arenaScroll?.ScrollVertical == scroll,
                "La habilidad no aterriza, el dado se pierde o el tablero cambia de posición.");
            ResetAnimations();
            Animate(cues, origins, lesson);
            ResetAnimations();
            Check(!tile.AwaitingLesson && !IsAnimating, "Interrumpir la animación deja una habilidad oculta o reproduce el dado.");
        }
        Check(usedPreview, "La prueba no cubre el caso de una ranura fuera del área visible.");
        GD.Print("PVP_SKILL_LESSON_PASS · carta de origen, giro, ranura, privacidad, dado e interrupción; cuatro tamaños y ambos jugadores/asientos.");
    }
}
