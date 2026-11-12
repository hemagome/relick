using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    private PopupMenu? _relikiaMenu;

    /// <summary>
    /// The player's own PR counter is the entry point for the Relikia effects. The menu always
    /// lists the four effects so their cost and current blocker stay discoverable; only the
    /// server-authorized options can be activated.
    /// </summary>
    private void OpenRelikiaMenu(ResourceBadge badge)
    {
        if (_preparing || _snapshot is not { Winner: null } s || !IsInstanceValid(badge)) return;
        if (_relikiaMenu is { } previous && IsInstanceValid(previous))
        {
            previous.Hide();
            previous.QueueFree();
        }

        var menu = new PopupMenu { Name = "RelikiaMenu" };
        badge.AddChild(menu);
        _relikiaMenu = menu;
        ArenaViewport.TouchMenu(menu);

        var player = s.Players[s.MySeat];
        var legal = s.Options.Select((option, index) => (option, index))
            .Where(entry => entry.option.Move.Kind == DuelMoveKind.Relikia).ToArray();

        menu.AddItem($"EFECTOS DE RELIKIA · {player.Pr} PR · {player.Actions} ACCIONES", -1);
        menu.SetItemDisabled(0, true);
        menu.AddSeparator();
        foreach (var effect in RelikiaEffects.All)
        {
            // Legality always comes from the server options. Disabled entries keep their
            // description but do not explain the blocker.
            var available = legal.Any(entry => entry.option.Move.Value == (int)effect.Id);
            var cost = effect.Id == RelikiaEffect.ReplayCode ? $"{effect.Cost} PR + código" : $"{effect.Cost} PR";
            var item = menu.ItemCount;
            menu.AddItem($"{effect.Name} · {cost} · {effect.ActionLabel}", (int)effect.Id);
            menu.SetItemTooltip(item, effect.Text);
            if (!available) menu.SetItemDisabled(item, true);
        }

        menu.IdPressed += id =>
        {
            if (id < 0) return;
            var index = s.Options.FindIndex(option => option.Move.Kind == DuelMoveKind.Relikia && option.Move.Value == id);
            if (index < 0) return;
            if ((RelikiaEffect)id == RelikiaEffect.ReplayCode)
            {
                _showReplayMemory = true; _selected.Clear(); _fieldSelection = null; _skillContext = false; Render();
                return;
            }
            PlayOption(index, s);
        };

        menu.ResetSize();
        var content = menu.Size;
        var size = new Vector2I(Math.Max(content.X, (int)badge.Size.X), Math.Max(content.Y, 1));
        var viewport = GetViewportRect();
        var origin = badge.GetScreenPosition();
        var x = Mathf.Clamp(origin.X, viewport.Position.X + 4, Math.Max(viewport.Position.X + 4, viewport.End.X - size.X - 4));
        var y = origin.Y + badge.Size.Y + 4;
        if (y + size.Y > viewport.End.Y - 4) y = origin.Y - size.Y - 4;
        y = Mathf.Max(viewport.Position.Y + 4, y);
        menu.Size = size;
        menu.Position = new Vector2I((int)x, (int)y);
        menu.Popup();
    }
}
