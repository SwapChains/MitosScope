using MitosScope.Core.Api;
using MitosScope.Core.Stats;

namespace MitosScope.Core.Sessions;

public enum SessionStatus
{
    Authenticating,
    /// <summary>The consent popup is open in the game (or waits for the player to leave the match).</summary>
    WaitingConsent,
    Connected,
    /// <summary>The player clicked "Deny". Stays like this until the game restarts.</summary>
    Denied,
    /// <summary>App key unknown or revoked, or consent removed (403 unknown_app / revoked).</summary>
    AppRejected,
    /// <summary>The client stopped answering (closed or logged out). Final state.</summary>
    Disconnected,
}

[Flags]
public enum SessionChange
{
    None = 0,
    Status = 1 << 0,
    Game = 1 << 1,
    Account = 1 << 2,
    Inventory = 1 << 3,
    Profile = 1 << 5,
    Quests = 1 << 7,
    /// <summary>MatchTracker: a match just ended (new last match).</summary>
    LastMatch = 1 << 8,
    BattlePass = 1 << 9,
    All = Status | Game | Account | Inventory | Profile | Quests | LastMatch | BattlePass,
}

/// <summary>
/// One logged-in game client (one slot/port): authenticates, then polls /v1/game every second
/// and the account sections every 60 s, re-authenticating on 401. <see cref="RunAsync"/> ends when
/// the client goes away.
/// </summary>
public sealed class ClientSession
{
    public static readonly TimeSpan GameInterval = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan SectionInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan AuthPollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan WatchInterval = TimeSpan.FromSeconds(5);
    private const int MaxUnreachable = 3;

    private readonly MitosApiClient _api;
    private readonly string _appKey;
    private string? _token; // never exposed or persisted (ADDON_API.md §8)

    public ClientSession(MitosApiClient api, HelloInfo hello, string appKey)
    {
        _api = api;
        _appKey = appKey;
        Hello = hello;
    }

    public HelloInfo Hello { get; }
    public int Port => Hello.Port;
    public int Slot => Hello.Slot;

    /// <summary>PID of the game process serving this port (null if unknown / not Windows).</summary>
    public int? ProcessId { get; internal set; }

    public SessionStatus Status { get; private set; } = SessionStatus.Authenticating;
    public string? LastError { get; private set; }

    public GameState? Game { get; private set; }
    public AccountInfo? Account { get; private set; }
    public IReadOnlyList<InventoryItem>? Inventory { get; private set; }
    public ProfileStats? Profile { get; private set; }
    public IReadOnlyList<Quest>? Quests { get; private set; }
    public BattlePassInfo? BattlePass { get; private set; }

    /// <summary>Live peak mass and last-match stats, fed by the game poll.</summary>
    public MatchTracker Matches { get; } = new();

    /// <summary>Raised on a background thread. Marshal to the UI thread before touching UI.</summary>
    public event Action<ClientSession, SessionChange>? Changed;

    public async Task RunAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var status = await AuthenticateAsync(ct).ConfigureAwait(false);
                if (status == SessionStatus.Connected)
                {
                    var outcome = await PollAsync(_token!, ct).ConfigureAwait(false);
                    if (outcome == PollOutcome.Reauthenticate)
                        continue;
                    if (outcome == PollOutcome.Rejected)
                        status = SetStatus(SessionStatus.AppRejected);
                    else
                        break; // unreachable
                }

                if (status is SessionStatus.Denied or SessionStatus.AppRejected)
                    await WatchUntilGoneAsync(ct).ConfigureAwait(false); // don't insist, just wait for the client to close

                break;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally
        {
            _token = null;
            SetStatus(SessionStatus.Disconnected);
        }
    }

    // ---- authentication -------------------------------------------------------------------

    private async Task<SessionStatus> AuthenticateAsync(CancellationToken ct)
    {
        SetStatus(SessionStatus.Authenticating);
        var unreachable = 0;

        while (true)
        {
            var r = await _api.AuthAsync(Port, _appKey, ct).ConfigureAwait(false);

            if (r.StatusCode == 200 && r.Value?.Token is { Length: > 0 } token)
            {
                _token = token;
                LastError = null;
                return SetStatus(SessionStatus.Connected);
            }

            switch (r.StatusCode)
            {
                case 202:
                    unreachable = 0;
                    SetStatus(SessionStatus.WaitingConsent);
                    await Task.Delay(AuthPollInterval, ct).ConfigureAwait(false);
                    continue;
                case 403 when r.Error == "denied":
                    LastError = r.Error;
                    return SetStatus(SessionStatus.Denied);
                case 403 or 400:
                    LastError = r.Error;
                    return SetStatus(SessionStatus.AppRejected);
                case 0:
                    LastError = r.Error;
                    if (++unreachable >= MaxUnreachable)
                        return SessionStatus.Disconnected;
                    await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
                    continue;
                default: // 5xx etc.: transient
                    LastError = r.Error;
                    await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
                    continue;
            }
        }
    }

    private async Task WatchUntilGoneAsync(CancellationToken ct)
    {
        var unreachable = 0;
        while (unreachable < MaxUnreachable)
        {
            await Task.Delay(WatchInterval, ct).ConfigureAwait(false);
            var r = await _api.HelloAsync(Port, TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
            unreachable = r.IsUnreachable ? unreachable + 1 : 0;
        }
    }

    // ---- polling --------------------------------------------------------------------------

    private enum PollOutcome { Reauthenticate, Rejected, Unreachable }

    private async Task<PollOutcome> PollAsync(string token, CancellationToken ct)
    {
        using var pollCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var stop = new TaskCompletionSource<PollOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);

        var loops = Task.WhenAll(
            GameLoopAsync(token, stop, pollCts.Token),
            SectionsLoopAsync(token, stop, pollCts.Token));

        try
        {
            return await stop.Task.WaitAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            await pollCts.CancelAsync().ConfigureAwait(false);
            try { await loops.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
    }

    private async Task GameLoopAsync(string token, TaskCompletionSource<PollOutcome> stop, CancellationToken ct)
    {
        var unreachable = 0;
        using var timer = new PeriodicTimer(GameInterval);
        do
        {
            var r = await _api.GameAsync(Port, token, ct).ConfigureAwait(false);
            if (Handle(r, stop, ref unreachable))
            {
                Game = r.Value;
                Raise(Matches.OnGame(r.Value!) ? SessionChange.Game | SessionChange.LastMatch : SessionChange.Game);
            }
        }
        while (!stop.Task.IsCompleted && await timer.WaitForNextTickAsync(ct).ConfigureAwait(false));
    }

    private async Task SectionsLoopAsync(string token, TaskCompletionSource<PollOutcome> stop, CancellationToken ct)
    {
        var unreachable = 0;
        while (!stop.Task.IsCompleted)
        {
            // Sequential on purpose: each one can take up to 20 s on the server side.
            await FetchAsync<AccountInfo>("account", v => Account = v, SessionChange.Account);
            await FetchAsync<List<Quest>>("quests", v => Quests = v, SessionChange.Quests);
            await FetchAsync<ProfileStats>("profile", v => Profile = v, SessionChange.Profile);
            await FetchAsync<List<InventoryItem>>("inventory", v => Inventory = v, SessionChange.Inventory);
            await FetchAsync<BattlePassInfo>("battlepass", v => BattlePass = v, SessionChange.BattlePass);

            await Task.Delay(SectionInterval, ct).ConfigureAwait(false);
        }

        async Task FetchAsync<T>(string section, Action<T> apply, SessionChange change)
        {
            if (stop.Task.IsCompleted)
                return;
            var r = await _api.SectionAsync<T>(Port, section, token, ct).ConfigureAwait(false);
            if (Handle(r, stop, ref unreachable))
            {
                apply(r.Value!.Data);
                Raise(change);
            }
        }
    }

    /// <summary>Returns true when the result carries data; otherwise decides whether polling must stop.</summary>
    private bool Handle<T>(ApiResult<T> r, TaskCompletionSource<PollOutcome> stop, ref int unreachable)
    {
        if (r.IsSuccess)
        {
            unreachable = 0;
            return true;
        }

        LastError = r.Error;
        switch (r.StatusCode)
        {
            case 401:
                stop.TrySetResult(PollOutcome.Reauthenticate);
                break;
            case 403:
                stop.TrySetResult(PollOutcome.Rejected);
                break;
            case 502 when r.Error == "addon_revoked":
                stop.TrySetResult(PollOutcome.Rejected);
                break;
            case 0:
                if (++unreachable >= MaxUnreachable)
                    stop.TrySetResult(PollOutcome.Unreachable);
                break;
            // 5xx / 504 / bad payload: keep the previous data and try again next tick.
        }
        return false;
    }

    // ---- helpers --------------------------------------------------------------------------

    private SessionStatus SetStatus(SessionStatus status)
    {
        if (Status != status)
        {
            Status = status;
            Raise(SessionChange.Status);
        }
        return status;
    }

    private void Raise(SessionChange change) => Changed?.Invoke(this, change);

    public override string ToString() => $"Slot {Slot} (port {Port}) {Status}";
}
