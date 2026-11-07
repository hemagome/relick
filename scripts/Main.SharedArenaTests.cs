using Godot;
using Relick.Core;

namespace Relick;

public partial class Main
{
    private async void SharedArenaSmokeTest()
    {
        try
        {
            SetProcess(false); _helpOpen = true;
            BindGame(new GameEngine(42)); _game.Start([]); ResetDrawAnimations();
            var sizes = new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) };
            async Task Layout()
            {
                for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
            Check(ArenaViewport.LandscapeSize(new(390, 844)) == new Vector2I(844, 390), "Una ventana alta no conserva el lienzo horizontal.");
            var directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
            async Task Capture(string name)
            {
                if (directory == null || DisplayServer.GetName() == "headless") return;
                System.IO.Directory.CreateDirectory(directory);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, name + ".png"));
            }
            foreach (var size in sizes)
            {
                GetWindow().ContentScaleSize = size; await Layout(); Render(); await Layout();
                var minimum = _root.GetCombinedMinimumSize();
                Check(_root.GetGlobalRect().End.X <= Size.X + 1 && _root.GetGlobalRect().End.Y <= Size.Y + 1, $"Práctica sale del área útil {size}: {_root.GetGlobalRect()}.");
                Check(minimum.X <= _root.Size.X + 1 && minimum.Y <= _root.Size.Y + 1, $"Práctica desborda {size}: mínimo {minimum}, raíz {_root.Size}.");
                Check(_handScroll!.GetGlobalRect().End.Y <= Size.Y + 1, "La mano queda fuera de la pantalla.");
                _handScroll.ScrollHorizontal = 0; await Layout();
                var card = (HandCard)_root.FindChild("HandCard0", true, false);
                var boardId = _root.GetInstanceId(); var cardId = card.GetInstanceId();
                GuiInputTestDriver.Click(card); await Layout();
                Check(_selectedCard == 0 && card.Chosen && _root.GetInstanceId() == boardId && card.GetInstanceId() == cardId,
                    "Seleccionar reconstruye el tablero o no recibe el clic.");
                if (CompactLayout)
                {
                    Check(_root.FindChild("ArenaScroll", true, false) is ScrollContainer, "Falta desplazamiento del tablero móvil.");
                    Check(_root.FindChildren("*", "Button", true, false).OfType<Button>().All(b => b.CustomMinimumSize.Y >= 48), "Hay controles táctiles menores a 48 unidades.");
                }
                await Capture($"practice-{size.X}x{size.Y}");
                GD.Print($"SHARED_ARENA_LAYOUT · práctica · {size} · mínimo {minimum} · selección estable.");
            }
            var online = new OnlineArena { Connection = new(), RoomCode = "UITEST00" }; AddChild(online); online.SetProcess(false);
            foreach (var size in sizes)
            {
                GetWindow().ContentScaleSize = size; await Layout(); _root.Hide(); MoveChild(online, GetChildCount() - 1);
                var view = DuelSnapshot.For(_game, 1, 7);
                online.PresentTestSnapshot(view); await Layout();
                var root = (Control)online.FindChild("DuelInterface", true, false);
                var minimum = root.GetCombinedMinimumSize();
                Check(root.GetGlobalRect().End.X <= Size.X + 1 && root.GetGlobalRect().End.Y <= Size.Y + 1, $"PvP sale del área útil {size}: {root.GetGlobalRect()}.");
                Check(minimum.X <= root.Size.X + 1 && minimum.Y <= root.Size.Y + 1, $"PvP desborda {size}: mínimo {minimum}, raíz {root.Size}.");
                Check(online.FindChild("OwnRelikarioPanel", true, false) != null && online.FindChild("CombatCenter", true, false) != null && online.FindChild("EnemyRelikarioPanel", true, false) != null,
                    "Los dos modos no comparten los componentes del tablero.");
                var portrait = (YunoPortrait)online.FindChild("YunoPortrait", true, false);
                Check(portrait.AssetsReady && portrait.IsVisibleInTree() && portrait.Size.X > 0, "El retrato PvP no tiene espacio o no carga sus hojas.");
                var backs = online.FindChildren("OpponentHandCard*", "Button", true, false).OfType<OpponentHandCard>();
                Check(backs.All(c => c.IsFaceDown), "El asiento 1 revela la mano rival.");
                var hand = (ScrollContainer)online.FindChild("HandScroll", true, false);
                Check(hand.GetGlobalRect().End.Y <= Size.Y + 1, $"La mano PvP queda fuera de pantalla {size}: mano {hand.GetGlobalRect()}, raíz {root.Size}, pantalla {Size}.");
                var builds = online.BoardBuildCount; await Layout();
                Check(online.BoardBuildCount == builds, "El tablero se reconstruye sin cambios.");
                await Capture($"pvp-{size.X}x{size.Y}");
                GD.Print($"SHARED_ARENA_LAYOUT · PvP · {size} · mínimo {minimum} · privacidad del asiento 1.");
            }
            GetWindow().ContentScaleSize = new(1440, 1024); await Layout(); _root.Hide(); MoveChild(online, GetChildCount() - 1);
            await online.CheckVisualCues(DuelSnapshot.For(_game, 1, 8));
            online.QueueFree();
            GD.Print("SHARED_ARENA_PASS · escritorio, tablet horizontal, teléfono horizontal, pantalla pequeña horizontal, selección estable y privacidad.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError("SHARED_ARENA_FAIL · " + error); GetTree().Quit(1); }
    }
}

public partial class OnlineArena
{
    internal void PresentTestSnapshot(DuelSnapshot snapshot) { _snapshot = snapshot; Render(); }

    internal async Task CheckVisualCues(DuelSnapshot snapshot)
    {
        ResetAnimations(); PresentTestSnapshot(snapshot);
        for (var i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var events = new List<DuelVisualEvent>
        {
            new() { Kind = DuelVisualKind.Die, Player = -1, Value = 3 }, new() { Kind = DuelVisualKind.Die, Player = -1, Value = 3 },
            new() { Kind = DuelVisualKind.Draw, Player = 1, Value = 0 }, new() { Kind = DuelVisualKind.Return, Player = 0, Value = 0 },
            new() { Kind = DuelVisualKind.Shuffle, Player = 1 }, new() { Kind = DuelVisualKind.BonusPlace, Player = 1 },
            new() { Kind = DuelVisualKind.BonusClaim, Player = 0, Target = 1, Value = 0 },
            new() { Kind = DuelVisualKind.Pile, Player = 1, Value = 0, Source = CardZone.Hand, Destination = CardZone.Memory },
            new() { Kind = DuelVisualKind.Attack, Player = 1 }, new() { Kind = DuelVisualKind.Hit, Player = 0 }
        };
        snapshot.Visuals = events;
        var origins = CaptureOrigins(snapshot, snapshot);
        Animate(events, origins);
        var frames = 0;
        while (IsAnimating && frames++ < 100)
        {
            _die.AdvancePlayback(0.5); UpdateFlights(0.5);
            if (_flightFront != null && _activeFlight?.Event.Kind == DuelVisualKind.Return && _activeFlight.Event.Player == 0)
                throw new Exception("La devolución de una mano oculta revela una carta.");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        PlayPendingPortraits();
        if (IsAnimating || _die.TopFaceValue != 3 || _portraits[0].Motion != PortraitMotion.Attack || _portraits[1].Motion != PortraitMotion.Hit)
            throw new Exception("No termina la secuencia visual o se pierde un efecto.");
        GD.Print("SHARED_ARENA_VISUALS_PASS · dados repetidos, robo, devolución oculta, barajado, Bonus, memoria, ataque y daño.");
    }
}
