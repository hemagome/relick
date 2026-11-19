using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckDestinyPickLayout()
    {
        var pick = CardCatalog.DestinyPick;
        var attack = CardCatalog.Attack.Id;
        if (BoardArt.LoadCardArtwork(pick) == null || BoardArt.CardType(pick) != "CÓDIGO · OBJETO" || !pick.IsCombo)
            throw new Exception("Falta el arte o la clasificación de Púa del Destino.");

        async Task Layout()
        {
            for (var frame = 0; frame < 4; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var embedded = GetViewport().GuiEmbedSubwindows;
        GetViewport().GuiEmbedSubwindows = true;
        try
        {
            foreach (var size in new[] { new Vector2I(1440, 1024), new(700, 390) })
            foreach (var actor in new[] { 0, 1 })
            {
                GetWindow().Size = size;
                GetWindow().ContentScaleSize = size;
                await Layout();
                var game = GameEngine.FromOpening(new DuelOpening
                {
                    Names = ["Ana", "Beto"], Hands = [[], []], Decks = [[attack, attack], [attack, attack]],
                    Bonuses = [null, null], StartingSeat = actor
                }, new SeededRandom(59));
                game.State.Phase = Phase.Actions;
                game.State.Active.Memory.AddRange([attack, pick.Id]);
                game.State.Players[1 - actor].Memory.AddRange([pick.Id, CardCatalog.Magnet.Id]);
                ResetAnimations();
                var before = DuelSnapshot.For(game, actor, 1);
                PresentTestSnapshot(before);
                await Layout();
                foreach (var relative in new[] { 0, 1 })
                {
                    var memory = (MemoryZoneButton)_root!.FindChild($"MemoryButton{relative}", true, false);
                    memory.EmitSignal(Button.SignalName.Pressed);
                    await Layout();
                    var dialog = GetChildren().OfType<AcceptDialog>().Single();
                    var activate = dialog.FindChild("ActivateDestinyPick", true, false) as Button;
                    if (relative == 0)
                    {
                        if (activate is not { Disabled: true })
                            throw new Exception("La activación debe estar visible y deshabilitada al inspeccionar el Ataque.");
                        ((HandCard)dialog.FindChild("CollectionCard1", true, false)).EmitSignal(Button.SignalName.Pressed);
                        if (activate.Disabled) throw new Exception("Seleccionar Púa debe habilitar su efecto de memoria.");
                    }
                    else if (activate != null) throw new Exception("La galería rival ofrece activar una carta ajena.");
                    dialog.QueueFree();
                    await Layout();
                }

                if (!before.Options.Single(o => o.Move.Kind == DuelMoveKind.DestinyPickMemory).Move.Apply(game, actor).Success)
                    throw new Exception("No se vuelve a usar Púa del Destino desde su memoria.");
                var modes = DuelSnapshot.For(game, actor, 2);
                PresentTestSnapshot(modes);
                var corrupt = modes.Options.Single(o => o.Move.Kind == DuelMoveKind.Choose && o.Label == "Corromper 1 carta");
                if (modes.Options.Count(o => o.Move.Kind == DuelMoveKind.Choose && PresentedButton(o) != null) != 3 ||
                    !corrupt.Move.Apply(game, actor).Success)
                    throw new Exception("Faltan las tres opciones del efecto.");

                PresentTestSnapshot(DuelSnapshot.For(game, actor, 3));
                var target = Array.FindIndex(_deckSearchOptions, o => o.Owner == 1 - actor && o.CardId == CardCatalog.Magnet.Id);
                if (target < 0 || MemorySections()?.Count != 2)
                    throw new Exception("La galería no distingue entre las memorias.");
                SelectCard(target);
                if (PresentedButton(_deckSearchOptions[target])?.Text != "Corromper" ||
                    !_deckSearchOptions[target].Move.Apply(game, actor).Success)
                    throw new Exception("No se puede confirmar el objetivo de corrupción.");
            }
        }
        finally
        {
            GetViewport().GuiEmbedSubwindows = embedded;
        }
        GD.Print("PVP_DESTINY_PICK_PASS");
    }
}
