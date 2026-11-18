using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckOsamuPresentation()
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        var blue = RelikarioArt.Load("osamu", "attack-card");
        var red = BoardArt.LoadCardArtwork(CardCatalog.Attack, "yuno");
        Check(blue != null && red != null && blue != red && BoardArt.LoadCardArtwork(CardCatalog.Attack, "osamu-lv2") == blue,
            "Falta la carta azul de Ataque de Osamu o su evolución muestra otro arte.");
        var super = BoardArt.LoadCardArtwork(CardCatalog.SuperAttack, "osamu");
        Check(super != null && super != blue && super == BoardArt.LoadCardArtwork(CardCatalog.SuperAttack, "yuno"),
            "Súper Ataque debe conservar su propia ilustración.");

        CardDefinition[] variants = [CardCatalog.AttackYuno, CardCatalog.AttackOsamu, CardCatalog.AttackYunoDt, CardCatalog.AttackOsamuDt];
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Osamu", "Yuno"], Relikarios = ["osamu", "yuno"],
            Hands = [[CardCatalog.Attack.Id, CardCatalog.Attack.Id, .. variants.Select(card => card.Id)], [CardCatalog.Attack.Id]],
            Decks = [[], []], Bonuses = [null, null]
        }, new SeededRandom(73));
        game.State.Players[0].MarkForeignHandCard(1);
        game.State.Players[0].Memory.Add(CardCatalog.Attack.Id);
        game.State.Players[1].RevealHandCard(0);
        GetWindow().ContentScaleSize = new(1820, 1024);
        for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, 0, 103));
        Check(_handCards.Single(card => card.Name == "HandCard0").FaceTexture == blue &&
            _handCards.Single(card => card.Name == "HandCard1").FaceTexture == red,
            "Ataques de dos dueños en una mano deben conservar su arte de origen.");
        Check(_opponentCards.Single().GetNode<HandCard>("RevealedFront").FaceTexture == red &&
            _root!.FindChild("MemoryTopCard", true, false) is HandCard memory && memory.FaceTexture == blue,
            "El rival revelado y la memoria deben usar el arte de sus dueños.");
        SelectCard(0);
        Check(_showcase?.FaceTexture == blue, "El inspector no amplía el Ataque azul que se seleccionó.");
        for (var i = 0; i < variants.Length; i++)
        {
            var index = i + 2;
            SelectCard(index);
            var path = $"res://assets/cards/{variants[i].Id}-clean.png";
            Check(_handCards.Single(card => card.Name == $"HandCard{index}").FaceTexture?.ResourcePath == path &&
                _showcase?.FaceTexture?.ResourcePath == path,
                $"La mano o el inspector pierde el arte de {variants[i].ArtworkLabel} al jugar con otro Relikario.");
        }

        RelikarioPortrait Probe()
        {
            var portrait = new RelikarioPortrait { Visible = false };
            portrait.SetRelikario("osamu");
            AddChild(portrait); portrait.SetProcess(false);
            return portrait;
        }
        var low = Probe();
        var high = Probe();
        const double tick = 1.0 / 120;
        foreach (var motion in Enum.GetValues<PortraitMotion>())
        {
            low.Play(motion); high.Play(motion);
            var poses = new HashSet<OsamuPortraitRig.Pose>();
            for (var step = 0; step < 48; step++)
            {
                high.AdvancePlayback(tick);
                poses.Add(high.OsamuPose ?? throw new Exception("No se cargó la figura articulada de Osamu."));
                if (step % 2 == 1) low.AdvancePlayback(2 * tick);
            }
            var a = low.OsamuPose!.Value; var b = high.OsamuPose!.Value;
            Check(high.IsContinuous && poses.Count >= 46 && high.Motion == motion &&
                a.Offset.DistanceTo(b.Offset) < .01f && Math.Abs(a.SwordAngle - b.SwordAngle) < .01f,
                $"El clip {motion} debe cambiar de pose a 120 Hz y conservar su tiempo a 60 Hz.");
        }
        high.Play(PortraitMotion.HitCritical); high.SetHealth(0);
        Check(high.Motion == PortraitMotion.HitCritical, "El último golpe debe terminar antes del reposo a cero.");
        high.AdvancePlayback(RelikarioPortrait.DurationFor(PortraitMotion.HitCritical) + .2);
        Check(high.Motion == PortraitMotion.ExhaustedIdle && high.OsamuPose!.Value.Height < .95f,
            "Osamu debe bajar la guardia y continuar respirando a cero vidas.");
        var exhausted = high.OsamuPose;
        high.SetRelikario("osamu-lv2");
        Check(high.OsamuPose == exhausted, "Evolucionar reinicia el movimiento de Osamu.");
        high.Play(PortraitMotion.Heal); high.SetHealth(3);
        high.AdvancePlayback(RelikarioPortrait.DurationFor(PortraitMotion.Heal) + .2);
        Check(high.Motion == PortraitMotion.Idle, "Después de curarse, Osamu debe recuperar su guardia.");
        low.QueueFree(); high.QueueFree();
        await CheckAttackProjectiles(game);
        await CheckTurboOverlapDice();
        GD.Print("PVP_OSAMU_ART_PASS · Ataques por Relikario, siete movimientos continuos a 120 Hz y regreso a la guardia.");
    }

    private async Task CheckTurboOverlapDice()
    {
        var random = new DuelRandom();
        random.Replay([5, 1, 4]);
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Osamu", "Yuno"], Relikarios = [RelikarioCatalog.OsamuLv2.Id, RelikarioCatalog.Yuno.Id],
            Hands = [[CardCatalog.Attack.Id], []], Decks = [[], []], Bonuses = [null, null]
        }, random);
        game.State.Turn = 2;
        game.State.Active.Skills.Add(CardCatalog.TurboOverlap.Id);
        var cues = DuelVisualEvents.Capture(game);
        if (!game.Attack(0, 0, 0).Success) throw new Exception("No se pudo lanzar el crítico de Turbo Overlap.");
        random.Finish();
        var snapshot = DuelSnapshot.For(game, 0, 105);
        snapshot.Visuals = cues;
        // The client receives these cues through the public JSON projection.
        var payload = System.Text.Json.JsonSerializer.Serialize(snapshot, DuelJson.Default.DuelSnapshot);
        snapshot = System.Text.Json.JsonSerializer.Deserialize(payload, DuelJson.Default.DuelSnapshot)!;
        PresentTestSnapshot(snapshot);
        _die.SetProcess(false); _secondDie.SetProcess(false);
        try
        {
            Animate(snapshot.Visuals, []);
            if (!_die.IsRolling || _die.Value != 6 || _secondDie.IsPresented)
                throw new Exception("El dado de ataque debe salir antes de los dos dados de daño.");
            _die.AdvancePlayback(DiceFace.RollSeconds + DiceFace.ResultSeconds);
            _die.AdvancePlayback(0);
            UpdateDiceOverlay();
            for (var i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!_die.IsRolling || !_secondDie.IsRolling || !_secondDie.IsVisibleInTree() ||
                _die.GetGlobalRect().Intersects(_secondDie.GetGlobalRect()))
                throw new Exception("Los dos dados de daño de Turbo Overlap deben rodar juntos y en paralelo.");
            _die.AdvancePlayback(DiceFace.RollSeconds);
            if (!IsAnimating) throw new Exception("La selección se abre antes de que caigan los dos dados.");
            _secondDie.AdvancePlayback(DiceFace.RollSeconds);
            var choices = _moveButtons.Where(button => PresentedOption(button)?.Move.Kind == DuelMoveKind.Choose).ToArray();
            if (IsAnimating || _die.TopFaceValue != 2 || _secondDie.TopFaceValue != 5 || choices.Length != 2 ||
                !choices[0].Text.Contains("2 de daño") || !choices[1].Text.Contains("5 de daño"))
                throw new Exception("Falta la selección entre los dos resultados de daño crítico.");
            var rival = DuelSnapshot.For(game, 1, 105);
            if (rival.Options.Any(option => option.Move.Kind == DuelMoveKind.Choose) ||
                !PresentedOption(choices[1])!.Move.Apply(game, 0).Success || game.State.Players[1].Health != 3)
                throw new Exception("El resultado elegido debe aplicarse y solo el atacante puede elegir.");
        }
        finally
        {
            ResetAnimations(); UpdateDiceOverlay();
            _die.SetProcess(true); _secondDie.SetProcess(true);
        }
        GD.Print("PVP_TURBO_OVERLAP_PASS · crítico con dos dados simultáneos y elección del atacante.");
    }

    private async Task CheckAttackProjectiles(GameEngine game)
    {
        var glove = RelikarioArt.Load(RelikarioCatalog.Yuno.Id, "attack-projectile");
        var bomb = RelikarioArt.Load(RelikarioCatalog.Osamu.Id, "attack-projectile");
        if (glove == null || bomb == null || glove == bomb)
            throw new Exception("Yuno y Osamu necesitan proyectiles propios.");
        var leftGlove = RelikarioArt.Load(RelikarioCatalog.Yuno.Id, "attack-projectile-left");
        if (leftGlove == null || leftGlove == glove || leftGlove.GetSize() != glove.GetSize())
            throw new Exception("El guantelete necesita una imagen hacia la izquierda con los botones legibles y el mismo tamaño.");
        var directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
        var reducedMotion = _impacts.ReducedMotion;
        try
        {
            _impacts.ReducedMotion = false;
            foreach (var seat in new[] { 0, 1 })
            foreach (var actor in new[] { 0, 1 })
            {
                game.State.Players[0].Relikario = seat == 0 ? RelikarioCatalog.Osamu : RelikarioCatalog.OsamuLv2;
                game.State.Players[1].Relikario = seat == 0 ? RelikarioCatalog.Yuno : RelikarioCatalog.YunoLv2;
                ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, seat, 104));
                for (var i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var relative = seat == actor ? 0 : 1;
                var defender = _portraits[1 - relative];
                Animate([new() { Kind = DuelVisualKind.Attack, Player = actor },
                    new() { Kind = DuelVisualKind.Hit, Player = 1 - actor, Value = 2, AttackDie = 5 }], []);
                _impacts.SetProcess(false);
                _impacts._Process(.65);
                for (var i = 0; i < 3; i++) UpdateFlights(0);
                if (_portraits[relative].Motion != PortraitMotion.Attack || !_impacts.AttackInFlightTo(defender) ||
                    defender.Motion != PortraitMotion.Idle || _impacts.LastNumberAt != null)
                    throw new Exception("El daño se muestra antes de que llegue el proyectil al rival.");
                if (directory != null && DisplayServer.GetName() != "headless")
                {
                    _portraits[relative].AdvancePlayback(.65);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    System.IO.Directory.CreateDirectory(directory);
                    GetViewport().GetTexture().GetImage().SavePng($"{directory}/attack-{_portraits[relative].RelikarioId}-seat-{seat}.png");
                }
                _impacts._Process(.25);
                UpdateFlights(0);
                if (_impacts.AttackInFlightTo(defender) || defender.Motion != PortraitMotion.HitNormal || _impacts.LastNumberAt == null)
                    throw new Exception("El proyectil llega sin mostrar el daño.");
            }
            ResetAnimations();
            _impacts.ReducedMotion = true;
            Animate([new() { Kind = DuelVisualKind.Attack, Player = 1 },
                new() { Kind = DuelVisualKind.Hit, Player = 0, Value = 2, AttackDie = 5 }], []);
            if (_impacts.AttackInFlightTo(_portraits[1]) || _portraits[1].Motion != PortraitMotion.HitNormal)
                throw new Exception("El movimiento reducido debe mostrar el daño sin esperar el vuelo.");
        }
        finally { _impacts.ReducedMotion = reducedMotion; ResetAnimations(); }
        GD.Print("PVP_ATTACK_PROJECTILES_PASS · Yuno y Osamu, ambos asientos y niveles, impacto al llegar y movimiento reducido.");
    }
}
