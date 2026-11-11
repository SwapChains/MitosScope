using CommunityToolkit.Mvvm.ComponentModel;
using MitosScope.App.Settings;

namespace MitosScope.App.ViewModels.Panels;

/// <summary>
/// Base for every overlay panel. Each panel gets its own window (see Overlay/PanelWindow);
/// its view is resolved by ViewLocator (XxxPanelViewModel → Views/Panels/XxxPanelView).
/// To add a panel: create the ViewModel + View and register it in App.CreatePanels().
/// </summary>
public abstract partial class PanelViewModel(TrackerViewModel tracker) : ViewModelBase
{
    public TrackerViewModel Tracker { get; } = tracker;

    /// <summary>Stable key used to persist the layout.</summary>
    public abstract string Id { get; }

    public abstract string Title { get; }

    public abstract PanelLayout DefaultLayout { get; }

    [ObservableProperty]
    public partial bool IsEditMode { get; set; }
}

public sealed class StatusPanelViewModel(TrackerViewModel tracker) : PanelViewModel(tracker)
{
    public override string Id => "status";
    public override string Title => "MitosScope";
    public override PanelLayout DefaultLayout => new() { X = 20, Y = 80, Width = 260, Height = 150 };
}

public sealed class GamePanelViewModel(TrackerViewModel tracker) : PanelViewModel(tracker)
{
    public override string Id => "game";
    public override string Title => "Match";
    public override PanelLayout DefaultLayout => new() { X = 20, Y = 240, Width = 260, Height = 285 };
}

public sealed class AccountPanelViewModel(TrackerViewModel tracker) : PanelViewModel(tracker)
{
    public override string Id => "account";
    public override string Title => "Account";
    public override PanelLayout DefaultLayout => new() { X = 20, Y = 450, Width = 260, Height = 205 };
}

public sealed class QuestsPanelViewModel(TrackerViewModel tracker) : PanelViewModel(tracker)
{
    public override string Id => "quests";
    public override string Title => "Quests";
    public override PanelLayout DefaultLayout => new() { X = 290, Y = 80, Width = 300, Height = 220 };
}

public sealed class ProfilePanelViewModel(TrackerViewModel tracker) : PanelViewModel(tracker)
{
    public override string Id => "profile"; // kept as "profile" so the saved layout still applies
    public override string Title => "All Time Stats";
    public override PanelLayout DefaultLayout => new() { X = 600, Y = 80, Width = 260, Height = 190 };
}

public sealed class BattlePassPanelViewModel(TrackerViewModel tracker) : PanelViewModel(tracker)
{
    public override string Id => "battlepass";
    public override string Title => "Battle Pass";
    public override PanelLayout DefaultLayout => new() { X = 870, Y = 80, Width = 260, Height = 165 };
}

public sealed class EquipmentPanelViewModel(TrackerViewModel tracker) : PanelViewModel(tracker)
{
    public override string Id => "equipment";
    public override string Title => "Equipment";
    public override PanelLayout DefaultLayout => new() { X = 290, Y = 310, Width = 300, Height = 310 };
}
