using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MitosScope.App.ViewModels.Panels;

namespace MitosScope.App.Overlay;

public partial class PanelWindow : Window
{
    /// <summary>How close to the edge (in DIPs) the pointer must be to resize instead of move.</summary>
    private const double ResizeMargin = 7;

    private static readonly Cursor MoveCursor = new(StandardCursorType.SizeAll);
    private static readonly Cursor WestEastCursor = new(StandardCursorType.SizeWestEast);
    private static readonly Cursor NorthSouthCursor = new(StandardCursorType.SizeNorthSouth);
    private static readonly Cursor TopLeftCursor = new(StandardCursorType.TopLeftCorner);
    private static readonly Cursor TopRightCursor = new(StandardCursorType.TopRightCorner);
    private static readonly Cursor BottomLeftCursor = new(StandardCursorType.BottomLeftCorner);
    private static readonly Cursor BottomRightCursor = new(StandardCursorType.BottomRightCorner);

    public PanelWindow()
    {
        InitializeComponent();
    }

    public PanelWindow(PanelViewModel panel) : this()
    {
        DataContext = panel;
    }

    public PanelViewModel Panel => (PanelViewModel)DataContext!;

    /// <summary>The ✕ button was clicked (edit mode). OverlayManager hides and saves.</summary>
    public event Action? CloseRequested;

    private void OnCloseClick(object? sender, RoutedEventArgs e) => CloseRequested?.Invoke();

    private void OnPanelPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!Panel.IsEditMode || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (EdgeAt(e.GetPosition(this)) is { } edge)
            BeginResizeDrag(edge, e);
        else
            BeginMoveDrag(e);
    }

    private void OnPanelPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!Panel.IsEditMode)
        {
            Root.ClearValue(CursorProperty);
            return;
        }

        Root.Cursor = EdgeAt(e.GetPosition(this)) switch
        {
            WindowEdge.West or WindowEdge.East => WestEastCursor,
            WindowEdge.North or WindowEdge.South => NorthSouthCursor,
            WindowEdge.NorthWest => TopLeftCursor,
            WindowEdge.NorthEast => TopRightCursor,
            WindowEdge.SouthWest => BottomLeftCursor,
            WindowEdge.SouthEast => BottomRightCursor,
            _ => MoveCursor,
        };
    }

    /// <summary>Which edge/corner the point is on, or null for the interior.</summary>
    private WindowEdge? EdgeAt(Point p)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        var left = p.X <= ResizeMargin;
        var right = p.X >= w - ResizeMargin;
        var top = p.Y <= ResizeMargin;
        var bottom = p.Y >= h - ResizeMargin;

        return (top, bottom, left, right) switch
        {
            (true, _, true, _) => WindowEdge.NorthWest,
            (true, _, _, true) => WindowEdge.NorthEast,
            (_, true, true, _) => WindowEdge.SouthWest,
            (_, true, _, true) => WindowEdge.SouthEast,
            (true, _, _, _) => WindowEdge.North,
            (_, true, _, _) => WindowEdge.South,
            (_, _, true, _) => WindowEdge.West,
            (_, _, _, true) => WindowEdge.East,
            _ => null,
        };
    }
}
