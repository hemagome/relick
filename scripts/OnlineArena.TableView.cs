using Godot;

namespace Relick;

public partial class OnlineArena
{
    private const int TableViewMenuId = 6;
    private string _boardSettingsPath = "user://board.cfg";
    /// <summary>Where the board view preference is saved; the UI tests point it elsewhere so a player's choice never leaks in.</summary>
    internal string BoardSettingsPath { get => _boardSettingsPath; init => _boardSettingsPath = value; }
    private bool _tableView, _requestedTableView;
    private bool TableView => Desktop && _tableView;

    private bool LoadTableView()
    {
        using var config = new ConfigFile();
        return config.Load(_boardSettingsPath) == Error.Ok && config.GetValue("board", "table_view", false).AsBool();
    }

    private void ToggleTableView(PopupMenu menu)
    {
        _requestedTableView = !_requestedTableView;
        menu.SetItemChecked(menu.GetItemIndex(TableViewMenuId), _requestedTableView);
        using var config = new ConfigFile();
        config.SetValue("board", "table_view", _requestedTableView);
        if (config.Save(_boardSettingsPath) != Error.Ok)
            GD.PushWarning("No se pudo guardar la vista del tablero en este dispositivo.");
    }

    private void ApplyRequestedTableView()
    {
        // Even purely cosmetic effects carry screen positions from the old board.
        if (_requestedTableView == _tableView || IsAnimating || _die.IsPresented || _queuedPlay != null ||
            _impacts.ActiveCount != 0 || _pendingPortraits.Count != 0 || _portraits.Any(p => p.IsActionPlaying)) return;
        _tableView = _requestedTableView;
        if (Desktop) Render();
    }
}
