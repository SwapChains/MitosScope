using MitosScope.Core.Api;

namespace MitosScope.Core.Stats;

/// <summary>The last finished match (one life: from becoming alive until death).</summary>
public sealed record LastMatch
{
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset EndedAt { get; init; }
    public TimeSpan Duration => EndedAt - StartedAt;
    public double PeakMass { get; init; }
    public int? BestPosition { get; init; }   // 1-based
    public int Players { get; init; }
    public double MatchExp { get; init; }
    public long EatenPlayers { get; init; }
    public long EatenFood { get; init; }
    public double EatenFoodMass { get; init; }
}

/// <summary>
/// Tracks matches from /v1/game alone (polled every second), so results are immediate:
/// a match starts when the player becomes alive and ends when they die or leave.
/// Replays are ignored. Kept in memory only.
/// </summary>
public sealed class MatchTracker
{
    private readonly object _lock = new();
    private LiveMatch? _current;

    /// <summary>Highest mass of the current life; 0 while dead / not playing.</summary>
    public double CurrentPeakMass { get { lock (_lock) return _current?.PeakMass ?? 0; } }

    /// <summary>When the current life started; null while dead / not playing.</summary>
    public DateTimeOffset? CurrentStartedAt { get { lock (_lock) return _current?.StartedAt; } }

    public LastMatch? Last { get; private set; }

    /// <summary>Feed every /v1/game result. Returns true when a match just ended (<see cref="Last"/> changed).</summary>
    public bool OnGame(GameState g)
    {
        lock (_lock)
        {
            if (g.Alive && !g.Replay)
            {
                _current ??= new LiveMatch { StartedAt = DateTimeOffset.Now };
                _current.PeakMass = Math.Max(_current.PeakMass, g.Mass);
                _current.Players = Math.Max(_current.Players, g.Players);
                if (g.InGame && g.LeaderboardPosition >= 0) // 0-based in the API
                    _current.BestPosition = Math.Min(_current.BestPosition ?? int.MaxValue, g.LeaderboardPosition + 1);

                // The API's match counters only grow during a life and drop to 0 once the player
                // stops playing, so keep the highest value seen: that's the life's total.
                _current.MatchExp = Math.Max(_current.MatchExp, g.MatchExp);
                _current.EatenPlayers = Math.Max(_current.EatenPlayers, g.EatenPlayers);
                _current.EatenFood = Math.Max(_current.EatenFood, g.EatenFood);
                _current.EatenFoodMass = Math.Max(_current.EatenFoodMass, g.EatenFoodMass);
                return false;
            }

            if (_current is null)
                return false;

            Last = new LastMatch
            {
                StartedAt = _current.StartedAt,
                EndedAt = DateTimeOffset.Now,
                PeakMass = _current.PeakMass,
                BestPosition = _current.BestPosition,
                Players = _current.Players,
                MatchExp = _current.MatchExp,
                EatenPlayers = _current.EatenPlayers,
                EatenFood = _current.EatenFood,
                EatenFoodMass = _current.EatenFoodMass,
            };
            _current = null;
            return true;
        }
    }

    private sealed class LiveMatch
    {
        public DateTimeOffset StartedAt;
        public double PeakMass;
        public int? BestPosition;
        public int Players;
        public double MatchExp;
        public long EatenPlayers;
        public long EatenFood;
        public double EatenFoodMass;
    }
}
