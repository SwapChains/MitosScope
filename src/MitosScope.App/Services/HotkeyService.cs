using System;
using SharpHook;
using SharpHook.Data;

namespace MitosScope.App.Services;

/// <summary>
/// Global (system-wide) hotkeys via SharpHook, so they work while the game has focus.
/// Ctrl+Shift+E toggles the overlay edit mode.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    public const string ToggleEditModeText = "Ctrl+Shift+E";

    private readonly TaskPoolGlobalHook _hook = new();

    /// <summary>Raised on a hook thread. Marshal to the UI thread.</summary>
    public event Action? ToggleEditModePressed;

    public void Start()
    {
        _hook.KeyPressed += OnKeyPressed;
        _ = _hook.RunAsync();
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        var mask = e.RawEvent.Mask;
        if (e.Data.KeyCode == KeyCode.VcE && mask.HasCtrl() && mask.HasShift() && !mask.HasAlt())
            ToggleEditModePressed?.Invoke();
    }

    public void Dispose()
    {
        _hook.KeyPressed -= OnKeyPressed;
        _hook.Dispose();
    }
}
