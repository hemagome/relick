using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    /// <summary>
    /// Renders the desktop board in its main states for visual review:
    /// `./jugar.sh -- --pvp-ui-test --board-preview-dir=DIR`. It needs a rendering display.
    /// </summary>
    internal async Task CaptureBoardPreview(string directory)
    {
        async Task Layout() { for (var i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        async Task Capture(string name)
        {
            PositionCardActions(); PresentPlaymat();
            await Layout();
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, name + ".png"));
        }
        System.IO.Directory.CreateDirectory(directory);
        var embedded = GetViewport().GuiEmbedSubwindows; GetViewport().GuiEmbedSubwindows = true;
        foreach (var size in new[] { new Vector2I(1820, 1024), new(1440, 1024) })
        {
            GetWindow().Size = size; GetWindow().ContentScaleSize = size; await Layout();
            var tag = $"{size.X}x{size.Y}";
            var game = new GameEngine(42); game.ChooseInitialSkill(0, CardCatalog.Magnet.Id); game.ChooseInitialSkill(1, CardCatalog.Destiny.Id); game.Start([]);
            game.State.Players[0].Memory.AddRange([CardCatalog.Bonfire.Id, CardCatalog.Reroll.Id]);
            game.State.Players[1].Memory.AddRange([CardCatalog.Attack.Id, CardCatalog.MindReset.Id]);
            ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, 0, 900)); await Layout();
            await Capture($"reposo-{tag}");
            SelectCard(Math.Min(2, _handCards.Count - 1)); await Layout();
            // Selected cards rise like hovered ones; let the lift settle before the capture.
            foreach (var card in _handCards) for (var i = 0; i < 40; i++) card._Process(0.05);
            await Capture($"carta-{tag}");
            ClearContext(); await Layout();
            if (_root!.FindChild("ResourceBadge0", true, false) is ResourceBadge badge)
            {
                OpenRelikiaMenu(badge); await Layout();
                _relikiaPreview = RelikiaEffect.ClaimBonus; RefreshInspector();
                await Capture($"relikia-{tag}");
                _relikiaMenu?.Hide(); _relikiaPreview = null; RefreshInspector(); await Layout();
            }

            var disk = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Ana", "Beto"], StartingSeat = 0,
                Hands = [[CardCatalog.Attack.Id, CardCatalog.Bonfire.Id, CardCatalog.PlumberStar.Id], [CardCatalog.Attack.Id]],
                Decks = [Enumerable.Repeat("attack", 12).ToList(), Enumerable.Repeat("attack", 12).ToList()], Bonuses = [null, null]
            }, new SeededRandom(43));
            disk.State.Phase = Phase.Actions; disk.State.Active.Hand[0] = CardCatalog.FloppyDisk.Id;
            disk.State.Players[0].Memory.AddRange([CardCatalog.Bonfire.Id, CardCatalog.Dodge.Id, CardCatalog.Attack.Id, CardCatalog.Reroll.Id]);
            disk.State.Players[1].Memory.AddRange([CardCatalog.SuperAttack.Id, CardCatalog.TimeBomb.Id, CardCatalog.Pendant.Id, CardCatalog.Attack.Id, CardCatalog.MindReset.Id]);
            if (!disk.PlayCode(0, 0).Success) throw new Exception("No comienza la recuperación de Disquete.");
            ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(disk, 0, 901)); await Layout();
            var pick = Array.FindIndex(_deckSearchOptions, o => o.CardId == CardCatalog.TimeBomb.Id);
            if (pick >= 0) SelectCard(pick);
            await Layout();
            await Capture($"disquete-{tag}");
        }
        GetViewport().GuiEmbedSubwindows = embedded;
    }
}
