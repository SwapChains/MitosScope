using System;
using System.ComponentModel;
using MitosScope.App.ViewModels;

namespace MitosScope.App.Overlay;

/// <summary>
/// Tells the overlay whether the displayed client is in a match or in the menu, so panels can be
/// shown only in one of the two ("Show in menu" / "Show in match" in the tray).
/// In a match = game.in_game (player in a match with at least one cell). Dead, menu, replay or no
/// client connected count as the menu. Must be used on the UI thread.
/// </summary>
public sealed class MatchStateController : IDisposable
{
    private readonly TrackerViewModel _tracker;
    private readonly OverlayManager _overlay;
    private SessionViewModel? _session;

    public MatchStateController(TrackerViewModel tracker, OverlayManager overlay)
    {
        _tracker = tracker;
        _overlay = overlay;
    }

    public void Start()
    {
        _tracker.PropertyChanged += OnTrackerChanged;
        Watch(_tracker.Current);
    }

    private void OnTrackerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrackerViewModel.Current))
            Watch(_tracker.Current);
    }

    private void Watch(SessionViewModel? session)
    {
        if (_session is not null)
            _session.PropertyChanged -= OnSessionChanged;
        _session = session;
        if (_session is not null)
            _session.PropertyChanged += OnSessionChanged;
        Update();
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SessionViewModel.Game))
            Update();
    }

    private void Update() => _overlay.SetInMatch(_session?.Game?.InGame == true);

    public void Dispose()
    {
        _tracker.PropertyChanged -= OnTrackerChanged;
        Watch(null);
    }
}
