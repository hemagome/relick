using Godot;

namespace Relick;

public partial class Main
{
    private VBoxContainer? _toolbar;
    private Relick.Core.DuelSnapshot _renderSnapshot = null!;
    private bool _responsiveLayoutQueued;
    private bool CompactLayout => ArenaViewport.Compact(this);
    private bool ShortLayout => ArenaViewport.Short(this);

    private void QueueResponsiveLayout()
    {
        if (_responsiveLayoutQueued || _game == null) return;
        _responsiveLayoutQueued = true;
        CallDeferred(nameof(RefreshResponsiveLayout));
    }

    private void RefreshResponsiveLayout()
    {
        _responsiveLayoutQueued = false;
        Render();
    }
}
