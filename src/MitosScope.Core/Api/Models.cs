using System.Text.Json;
using System.Text.Json.Serialization;

namespace MitosScope.Core.Api;

// Property names are mapped with snake_case (see MitosJson). Types follow ADDON_API.md;
// fields whose type the docs don't pin down are string (FlexibleStringConverter accepts
// numbers too) or JsonElement.

public sealed record HelloInfo(string Game, int ApiVersion, int Slot, int Port, string ClientVersion);

public sealed record AuthResponse(string? Token, string? Status, string? App, string? Error);

public sealed record ErrorResponse(string? Error);

public sealed record SectionResponse<T>(string Section, long Time, T Data);

public sealed record GameState
{
    public bool InGame { get; init; }
    public bool Playing { get; init; }
    public bool Alive { get; init; }
    public int Mode { get; init; }
    public string? ModeTitle { get; init; }
    public string? Match { get; init; }
    public bool PrivateRoom { get; init; }
    public bool Replay { get; init; }
    public double Mass { get; init; }
    public int Cells { get; init; }
    public int LeaderboardPosition { get; init; } = -1;
    public int Players { get; init; }
    public string? Zone { get; init; }

    // Match counters: counted by the client since the last spawn; 0 outside a match and in replays.
    public double MatchExp { get; init; }
    public long EatenPlayers { get; init; }
    public long EatenFood { get; init; }
    public double EatenFoodMass { get; init; }
}

public sealed record Guild(long Id, string? Name, string? Role);

public sealed record AccountInfo
{
    public long Id { get; init; }
    public string? Nickname { get; init; }
    public string? Display { get; init; }
    public JsonElement Registered { get; init; }
    public long Level { get; init; }
    public long Exp { get; init; }
    public long ExpLevel { get; init; }
    public long ExpNext { get; init; }
    public long Coins { get; init; }
    public long Diamonds { get; init; }
    public long Energy { get; init; }
    public long Rank { get; init; }
    public long RankStars { get; init; }
    public long Mmr { get; init; }
    public long MmrRank { get; init; }
    public string? Title { get; init; }
    public string? Skin { get; init; }
    public string? Server { get; init; }
    public string? Language { get; init; }
    public long AchievementsNew { get; init; }
    public long QuestsOpen { get; init; }
    public Guild? Guild { get; init; }
}

public sealed record InventoryItem
{
    public long Id { get; init; }
    public string? Name { get; init; }
    public string? Type { get; init; }
    public int Category { get; init; }
    public long Level { get; init; }
    public long ItemLevel { get; init; }
    public long Durability { get; init; }
    public long MaxDurability { get; init; }
    public bool Passive { get; init; }
    public string? Sprite { get; init; }
    public bool Equipped { get; init; }
    public int Slot { get; init; } = -1;
}

/// <summary>/v1/achievements. Not polled for now (no panel uses it).</summary>
public sealed record Achievement
{
    public long Id { get; init; }
    public string? Tag { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Image { get; init; }
    public long Current { get; init; }
    public long Total { get; init; }
    public bool Obtained { get; init; }
    public long Date { get; init; }
    public bool Seasonal { get; init; }
    public string? Reward { get; init; }
    public long RewardAmount { get; init; }
}

public sealed record ModeStats
{
    public int Mode { get; init; }
    public string? Name { get; init; }
    public long Games { get; init; }
    public long TotalDuration { get; init; }
    public double MaxScore { get; init; }
    public long EnemyNr { get; init; }
}

public sealed record ProfileStats
{
    public long Games { get; init; }
    public long TotalDuration { get; init; }
    public long MaxDuration { get; init; }
    public double MaxScore { get; init; }
    public long SplitNr { get; init; }
    public long VirusNr { get; init; }
    public long FoodNr { get; init; }
    public long EnemyNr { get; init; }
    public long ShootNr { get; init; }
    public double HighestEat { get; init; }
    public long TeamGames { get; init; }
    public long TeamWins { get; init; }
    public long RankedWins { get; init; }
    public long RankedLoss { get; init; }
    public IReadOnlyList<ModeStats> Modes { get; init; } = [];
}

/// <summary>/v1/battlepass. "level" is the tier bought (0 = free); the level reached is season_level.</summary>
public sealed record BattlePassInfo
{
    public long Level { get; init; }
    public string? Type { get; init; }
    /// <summary>Unix timestamp in seconds; -1 if the player never had a battle pass.</summary>
    public long Expires { get; init; } = -1;
    public double CoinMultiplier { get; init; }
    public long SeasonLevel { get; init; }
    public long SeasonExp { get; init; }
    /// <summary>-1 (with next_level_exp 0) when the pass is finished.</summary>
    public long NextLevel { get; init; } = -1;
    public long NextLevelExp { get; init; }

    public bool IsFinished => NextLevel < 0;

    public bool IsPremium => string.Equals(Type, "premium", StringComparison.OrdinalIgnoreCase);
}

public sealed record Quest
{
    public long Id { get; init; }
    [JsonPropertyName("quest")]
    public string? QuestKey { get; init; }
    public string? Text { get; init; }
    public long Current { get; init; }
    public long Target { get; init; }
    public string? Reward { get; init; }
    public long RewardAmount { get; init; }

    public bool IsDone => Current >= Target;

    /// <summary>
    /// "+200 coins" / "+150 EXP": reward_amount is coins for coin quests and experience for exp
    /// quests. Other reward types show only the type.
    /// </summary>
    public string RewardText => Reward switch
    {
        null or "" or "none" => "",
        "coins" when RewardAmount > 0 => $"+{RewardAmount:N0} coins",
        "exp" when RewardAmount > 0 => $"+{RewardAmount:N0} EXP",
        _ => Reward.ToUpperInvariant(),
    };
}
