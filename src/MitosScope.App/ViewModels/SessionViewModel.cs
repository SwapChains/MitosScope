using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using MitosScope.Core.Api;
using MitosScope.Core.Sessions;
using MitosScope.Core.Stats;

namespace MitosScope.App.ViewModels;

/// <summary>UI-thread mirror of a <see cref="ClientSession"/>. Call <see cref="Refresh"/> on the UI thread.</summary>
public partial class SessionViewModel : ViewModelBase
{
    public SessionViewModel(ClientSession session)
    {
        Session = session;
        Refresh(SessionChange.Status);
    }

    public ClientSession Session { get; }
    public int Slot => Session.Slot;
    public string SlotText => $"Slot {Session.Slot}";

    [ObservableProperty] public partial SessionStatus Status { get; private set; }
    [ObservableProperty] public partial GameState? Game { get; private set; }
    [ObservableProperty] public partial AccountInfo? Account { get; private set; }
    [ObservableProperty] public partial IReadOnlyList<InventoryItem>? Inventory { get; private set; }
    [ObservableProperty] public partial ProfileStats? Profile { get; private set; }
    [ObservableProperty] public partial IReadOnlyList<Quest>? Quests { get; private set; }
    [ObservableProperty] public partial IReadOnlyList<EquippedItemViewModel>? EquippedItems { get; private set; }

    public bool IsConnected => Status == SessionStatus.Connected;

    public string StatusText => Status switch
    {
        SessionStatus.Authenticating => "Connecting…",
        SessionStatus.WaitingConsent => "Click \"Allow\" in the game",
        SessionStatus.Connected => "Connected",
        SessionStatus.Denied => "Permission denied. Restart the game to try again.",
        SessionStatus.AppRejected => $"Addon rejected by the client ({Session.LastError ?? "?"})",
        SessionStatus.Disconnected => "Disconnected",
        _ => Status.ToString(),
    };

    /// <summary>Account nickname; falls back to the display name, then to the slot.</summary>
    public string DisplayName => Account switch
    {
        { Nickname: { Length: > 0 } n } => n,
        { Display: { Length: > 0 } d } => d,
        _ => SlotText,
    };

    // ---- derived values for the panels ----

    public double ExpProgress
    {
        get
        {
            if (Account is not { } a || a.ExpNext <= a.ExpLevel)
                return 0;
            return Math.Clamp((double)(a.Exp - a.ExpLevel) / (a.ExpNext - a.ExpLevel), 0, 1);
        }
    }

    public string ExpText => Account is { } a ? $"{a.Exp - a.ExpLevel:N0} / {a.ExpNext - a.ExpLevel:N0} XP" : "";

    public bool HasMatchName => !string.IsNullOrEmpty(Game?.Match);

    public string AliveText => Game?.Alive == true ? "Yes" : "No";

    public bool HasModeTitle => !string.IsNullOrEmpty(Game?.ModeTitle);

    // ---- Match panel rows: live while alive, last recorded match otherwise ----

    /// <summary>While dead (or in the menu) the Match panel shows the last match instead of live data.</summary>
    public bool ShowsLastMatch => Game?.Alive != true;

    /// <summary>
    /// Match panel subtitle. Alive: "Live · 2m 13s" (time alive, refreshed by the 1 s game poll).
    /// Dead: "Ended 14:32 · 4m 49s" for the last match. Empty (hidden) when there's nothing to show.
    /// </summary>
    public string MatchSubtitleText => ShowsLastMatch
        ? LastMatch is { } m ? $"Ended {m.EndedAt.LocalDateTime:t} · {FormatDuration(m.Duration)}" : ""
        : Session.Matches.CurrentStartedAt is { } start ? $"Live · {FormatDuration(DateTimeOffset.Now - start)}" : "";

    public bool ShowMatchSubtitle => MatchSubtitleText.Length > 0;

    public string MassLabel => ShowsLastMatch ? "Highest mass" : "Mass";
    public string LeaderboardLabel => ShowsLastMatch ? "Best position" : "Leaderboard";
    public string MatchExpLabel => ShowsLastMatch ? "Last match XP" : "Match XP";
    public string EatenPlayersLabel => ShowsLastMatch ? "Total players eaten" : "Players eaten";
    public string EatenFoodLabel => ShowsLastMatch ? "Total food eaten" : "Food eaten";

    /// <summary>Alive: "1,520 / 2,300" (current / peak of this life). Dead: the last match's peak.</summary>
    public string MassText => ShowsLastMatch
        ? LastMatch is { } m ? $"{m.PeakMass:N0}" : "—"
        : $"{Game!.Mass:N0} / {Session.Matches.CurrentPeakMass:N0}";

    /// <summary>
    /// Alive: current position. The API's leaderboard_position is 0-based (0 = first place; -1 = unknown),
    /// so +1 for display. Dead: best (lowest) position reached in the last match.
    /// </summary>
    public string LeaderboardText => ShowsLastMatch
        ? LastMatch is { BestPosition: { } p } m ? (m.Players > 0 ? $"#{p} of {m.Players}" : $"#{p}") : "—"
        : Game is { InGame: true, LeaderboardPosition: >= 0 } g ? $"#{g.LeaderboardPosition + 1} of {g.Players}" : "—";

    public string MatchExpText => ShowsLastMatch
        ? LastMatch is { } m ? $"{m.MatchExp:N0}" : "—"
        : $"{Game!.MatchExp:N0}";

    public string EatenPlayersText => ShowsLastMatch
        ? LastMatch is { } m ? $"{m.EatenPlayers:N0}" : "—"
        : $"{Game!.EatenPlayers:N0}";

    /// <summary>"230 (460 mass)": pellets eaten and the mass they gave (this life, or the last match).</summary>
    public string EatenFoodText => ShowsLastMatch
        ? LastMatch is { } m ? $"{m.EatenFood:N0} ({m.EatenFoodMass:N0} mass)" : "—"
        : $"{Game!.EatenFood:N0} ({Game.EatenFoodMass:N0} mass)";

    private void NotifyMatchRows()
    {
        foreach (var name in (string[])[nameof(ShowsLastMatch), nameof(MatchSubtitleText), nameof(ShowMatchSubtitle), nameof(MassLabel), nameof(LeaderboardLabel),
                     nameof(MatchExpLabel), nameof(EatenPlayersLabel), nameof(EatenFoodLabel), nameof(MassText),
                     nameof(LeaderboardText), nameof(MatchExpText), nameof(EatenPlayersText), nameof(EatenFoodText)])
            OnPropertyChanged(name);
    }

    public string RoomTypeText => Game switch
    {
        { PrivateRoom: true } => "Private Room",
        { Replay: true } => "Replay",
        _ => "Public Mode",
    };

    public string GuildText => Account?.Guild switch
    {
        null => "—",
        { Role: { Length: > 0 } role } g => $"{g.Name} · {Capitalize(role)}",
        var g => g.Name ?? "—",
    };

    /// <summary>"1,850 (123)": MMR and position in the MMR ranking; mmr_rank is 0 when not ranked, then just the MMR.</summary>
    public string RatingText => Account switch
    {
        null => "—",
        { MmrRank: > 0 } a => $"{a.Mmr:N0} ({a.MmrRank:N0})",
        var a => $"{a.Mmr:N0}",
    };

    /// <summary>profile.total_duration is in seconds: "2h 05m", "4m 49s".</summary>
    public string TotalDurationText => Profile is { } p ? FormatDuration(p.TotalDuration) : "—";

    public string RankedText => Profile is { } p ? $"{p.RankedWins:N0} / {p.RankedLoss:N0}" : "—";

    private static string FormatDuration(long seconds) => FormatDuration(TimeSpan.FromSeconds(seconds));

    private static string FormatDuration(TimeSpan t)
    {
        if (t < TimeSpan.Zero)
            t = TimeSpan.Zero;
        return t.TotalHours >= 1
            ? $"{(long)t.TotalHours:N0}h {t.Minutes:00}m"
            : $"{t.Minutes}m {t.Seconds:00}s";
    }

    public string ServerText => Account?.Server is { Length: > 0 } s ? Capitalize(s) : "—";

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    public int OpenQuestCount => Quests?.Count(q => !q.IsDone) ?? 0;

    // ---- battle pass ----

    [ObservableProperty] public partial BattlePassInfo? BattlePass { get; private set; }

    public bool HasBattlePass => BattlePass is not null;

    /// <summary>The tier row is shown only for a premium pass.</summary>
    public bool ShowBattlePassTier => BattlePass is { IsPremium: true };

    /// <summary>Tier bought by the player; 0 is the free pass.</summary>
    public string BattlePassTierText => BattlePass is { } b ? $"{b.Level:N0}" : "—";

    public string BattlePassTypeText => BattlePass?.Type is { Length: > 0 } t ? Capitalize(t) : "—";

    /// <summary>The expiry row is shown only for a premium pass.</summary>
    public bool ShowBattlePassExpires => BattlePass is { IsPremium: true, Expires: > 0 };

    public string BattlePassExpiresText => BattlePass is { Expires: > 0 } b
        ? DateTimeOffset.FromUnixTimeSeconds(b.Expires).LocalDateTime.ToString("g")
        : "—";

    /// <summary>"Level 45 · 12,340 XP": season_level and season_exp.</summary>
    public string CurrentLevelText => BattlePass is { } b ? $"Level {b.SeasonLevel:N0} · {b.SeasonExp:N0} XP" : "—";

    /// <summary>"Level 45 · 12,000 XP", or "Completed" when the pass is finished.</summary>
    public string NextLevelText => BattlePass switch
    {
        null => "—",
        { IsFinished: true } => "Completed",
        var b => $"Level {b.NextLevel:N0} · {b.NextLevelExp:N0} XP",
    };

    /// <summary>Last finished match (MatchTracker); shown by the Match panel while dead.</summary>
    [ObservableProperty] public partial LastMatch? LastMatch { get; private set; }

    public void Refresh(SessionChange change)
    {
        if (change.HasFlag(SessionChange.Status))
        {
            Status = Session.Status;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(IsConnected));
        }
        if (change.HasFlag(SessionChange.Game))
        {
            // LastMatch first: on the death tick Game and LastMatch change together.
            if (change.HasFlag(SessionChange.LastMatch))
                LastMatch = Session.Matches.Last;
            Game = Session.Game;
            OnPropertyChanged(nameof(HasMatchName));
            OnPropertyChanged(nameof(AliveText));
            OnPropertyChanged(nameof(RoomTypeText));
            OnPropertyChanged(nameof(HasModeTitle));
            NotifyMatchRows();
        }
        if (change.HasFlag(SessionChange.Account))
        {
            Account = Session.Account;
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(ExpProgress));
            OnPropertyChanged(nameof(ExpText));
            OnPropertyChanged(nameof(GuildText));
            OnPropertyChanged(nameof(RatingText));
            OnPropertyChanged(nameof(ServerText));
        }
        if (change.HasFlag(SessionChange.Profile))
        {
            Profile = Session.Profile;
            OnPropertyChanged(nameof(TotalDurationText));
            OnPropertyChanged(nameof(RankedText));
        }
        if (change.HasFlag(SessionChange.Quests))
        {
            Quests = Session.Quests;
            OnPropertyChanged(nameof(OpenQuestCount));
        }
        if (change.HasFlag(SessionChange.Inventory))
        {
            Inventory = Session.Inventory;
            EquippedItems = Inventory?
                .Where(i => i.Equipped)
                .OrderBy(i => i.Slot)
                .Select(i => new EquippedItemViewModel(i))
                .ToList();
        }
        if (change.HasFlag(SessionChange.BattlePass))
        {
            BattlePass = Session.BattlePass;
            OnPropertyChanged(nameof(HasBattlePass));
            OnPropertyChanged(nameof(ShowBattlePassTier));
            OnPropertyChanged(nameof(BattlePassTierText));
            OnPropertyChanged(nameof(BattlePassTypeText));
            OnPropertyChanged(nameof(ShowBattlePassExpires));
            OnPropertyChanged(nameof(BattlePassExpiresText));
            OnPropertyChanged(nameof(CurrentLevelText));
            OnPropertyChanged(nameof(NextLevelText));
        }
        if (change.HasFlag(SessionChange.LastMatch) && !change.HasFlag(SessionChange.Game))
        {
            LastMatch = Session.Matches.Last;
            NotifyMatchRows();
        }
    }
}
