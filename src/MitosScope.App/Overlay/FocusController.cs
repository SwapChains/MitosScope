using System;
using MitosScope.App.Services;
using MitosScope.App.ViewModels;
using MitosScope.Core.Sessions;

namespace MitosScope.App.Overlay;

/// <summary>
/// Follows the foreground window:
/// - a game client gets focus → the panels show that client and the overlay is visible;
/// - anything else gets focus → the overlay hides until a game client is focused again.
/// Edit mode always keeps the overlay visible (otherwise using the tray menu would hide it).
/// Our own windows never count (ForegroundWatcher skips them).
/// </summary>
public sealed class FocusController : IDisposable
{
    private readonly ForegroundWatcher _watcher = new();
    private readonly SessionManager? _sessions;
    private readonly TrackerViewModel _tracker;
    private readonly OverlayManager _overlay;
    private bool _gameFocused;

    public FocusController(SessionManager? sessions, TrackerViewModel tracker, OverlayManager overlay)
    {
        _sessions = sessions;
        _tracker = tracker;
        _overlay = overlay;
    }

    public void Start()
    {
        if (!ForegroundWatcher.IsSupported || _sessions is null)
            return; // no focus info: keep the overlay always visible

        _watcher.ForegroundChanged += OnForegroundChanged;
        _overlay.EditModeChanged += _ => UpdateVisibility();
        _watcher.Start();
    }

    private void OnForegroundChanged(int processId)
    {
        _gameFocused = _sessions!.IsGameProcess(processId);
        if (_gameFocused)
            _tracker.FocusProcess(processId);
        UpdateVisibility();
    }

    private void UpdateVisibility() => _overlay.SetFocusVisible(_overlay.IsEditMode || _gameFocused);

    public void Dispose() => _watcher.Dispose();
}
