using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    private PopupMenu? _relikiaMenu;
    private RelikiaEffect? _relikiaPreview;
    // Lower edge of each effect's row inside the menu, to read the rows the menu itself skips.
    private readonly List<(float Bottom, RelikiaEffect Effect)> _relikiaRows = [];
    private float _relikiaRowsTop;

    private static Texture2D? RelikiaSheet(RelikiaEffect effect)
    {
        var path = "res://assets/relikia/" + effect switch
        {
            RelikiaEffect.ChangeBonus => "change-bonus", RelikiaEffect.DrawAndPlace => "draw-and-place",
            RelikiaEffect.ReplayCode => "replay-code", _ => "claim-bonus"
        } + ".webp";
        return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }

    // The rail reads the effect under the pointer or the keyboard focus while the menu is open.
    private void UpdateRelikiaPreview()
    {
        RelikiaEffect? preview = null;
        if (_showcase != null && _relikiaMenu is { } menu && IsInstanceValid(menu) && menu.Visible)
        {
            var item = menu.GetFocusedItem();
            var id = item >= 0 ? menu.GetItemId(item) : -1;
            if (id >= 0 && RelikiaEffects.All.Any(effect => (int)effect.Id == id)) preview = (RelikiaEffect)id;
            else
            {
                // An effect that cannot be used yet is disabled and never takes the menu's focus,
                // but it is exactly the one whose cost the player wants to read.
                var pointer = menu.GetMousePosition();
                if (pointer.X >= 0 && pointer.X <= menu.Size.X && pointer.Y >= 0 && pointer.Y <= menu.Size.Y)
                {
                    var top = _relikiaRowsTop;
                    foreach (var (bottom, effect) in _relikiaRows)
                    {
                        if (pointer.Y >= top && pointer.Y < bottom) { preview = effect; break; }
                        top = bottom;
                    }
                }
            }
        }
        if (preview == _relikiaPreview) return;
        _relikiaPreview = preview;
        RefreshInspector();
    }

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

        var legal = s.Options.Select((option, index) => (option, index))
            .Where(entry => entry.option.Move.Kind == DuelMoveKind.Relikia).ToArray();

        // The desktop rail shows each effect's sheet, costs included, so its menu only names them.
        // Every entry carries one action crystal per action it costs.
        var named = _showcase != null;
        _relikiaRows.Clear();
        float Measured()
        {
            menu.ResetSize();
            return menu.Size.Y - (menu.GetThemeStylebox("panel")?.GetMargin(Side.Bottom) ?? 0);
        }
        _relikiaRowsTop = Measured();
        foreach (var effect in RelikiaEffects.All)
        {
            // Legality always comes from the server options. Disabled entries keep their
            // description but do not explain the blocker.
            var available = legal.Any(entry => entry.option.Move.Value == (int)effect.Id);
            var cost = effect.Id == RelikiaEffect.ReplayCode ? $"{effect.Cost} PR + código" : $"{effect.Cost} PR";
            var item = menu.ItemCount;
            menu.AddIconItem(ActionCrystalPool.Icon(effect.Actions, ArenaPresentation.Mint),
                named ? effect.Name : $"{effect.Name} · {cost} · {effect.ActionLabel}", (int)effect.Id);
            menu.SetItemTooltip(item, named ? $"{cost} · {effect.ActionLabel}\n{effect.Text}" : effect.Text);
            if (!available) menu.SetItemDisabled(item, true);
            _relikiaRows.Add((Measured(), effect.Id));
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
