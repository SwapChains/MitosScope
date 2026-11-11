using System.Collections.Concurrent;
using MitosScope.Core.Api;
using MitosScope.Core.Discovery;

namespace MitosScope.Core.Sessions;

/// <summary>
/// Finds game clients and keeps one <see cref="ClientSession"/> per port.
///
/// Discovery strategy: a <see cref="GameProcessWatcher"/> tells us when a game process starts or
/// exits, and only then the ports are scanned. Because the API only comes up after login, a
/// process can exist before its port answers; while there are more game processes than active
/// sessions, the ports are rescanned every <see cref="PendingRescanInterval"/>. When no process
/// names are configured, it falls back to scanning every <see cref="FallbackScanInterval"/>.
/// </summary>
public sealed class SessionManager : IAsyncDisposable
{
    public static readonly TimeSpan ProcessPollInterval = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan PendingRescanInterval = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan FallbackScanInterval = TimeSpan.FromSeconds(10);

    private readonly MitosApiClient _api;
    private readonly string _appKey;
    private readonly GameProcessWatcher? _watcher;
    private readonly ConcurrentDictionary<int, ClientSession> _sessions = new();
    private readonly SemaphoreSlim _wake = new(0, 1);
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _sessionTasks = [];
    private Task? _loop;

    public SessionManager(MitosApiClient api, string appKey, IReadOnlyCollection<string> gameProcessNames)
    {
        _api = api;
        _appKey = appKey;
        if (gameProcessNames.Count > 0)
        {
            _watcher = new GameProcessWatcher(gameProcessNames, ProcessPollInterval);
            _watcher.Changed += _ =>
            {
                Wake();
                GameProcessesChanged?.Invoke();
            };
        }
    }

    /// <summary>Raised on a background thread.</summary>
    public event Action<ClientSession>? SessionAdded;

    /// <summary>Raised on a background thread after the session reached <see cref="SessionStatus.Disconnected"/>.</summary>
    public event Action<ClientSession>? SessionRemoved;

    /// <summary>Raised on a background thread when a game process starts or exits.</summary>
    public event Action? GameProcessesChanged;

    public bool UsesProcessWatcher => _watcher is not null;

    /// <summary>
    /// Running game processes that no session belongs to: not logged in yet, or the client's API
    /// is down (seen after logout → login in the same client). Empty when the PID ↔ port mapping
    /// isn't available (no process watcher, or a session with unknown PID), to avoid false alarms.
    /// </summary>
    public IReadOnlyList<int> GetProcessesWithoutApi()
    {
        if (_watcher is null)
            return [];
        var sessions = _sessions.Values.ToList();
        if (sessions.Any(s => s.ProcessId is null))
            return [];
        var withApi = sessions.Select(s => s.ProcessId!.Value).ToHashSet();
        return _watcher.ProcessIds.Where(pid => !withApi.Contains(pid)).Order().ToList();
    }

    public IReadOnlyList<ClientSession> Sessions => _sessions.Values.OrderBy(s => s.Slot).ToList();

    /// <summary>
    /// True when <paramref name="processId"/> is a game client: it serves one of the sessions, or
    /// its executable name is one of the configured game process names (covers clients that are
    /// still on the login screen or waiting for consent).
    /// </summary>
    public bool IsGameProcess(int processId)
    {
        if (_sessions.Values.Any(s => s.ProcessId == processId))
            return true;
        return _watcher?.IsGameProcess(processId) ?? false;
    }

    public void Start()
    {
        if (_loop is not null)
            return;
        _watcher?.Start();
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (_watcher is null || _watcher.ProcessCount > _sessions.Count)
                await ScanAsync(ct).ConfigureAwait(false);

            TimeSpan delay;
            if (_watcher is null)
                delay = FallbackScanInterval;
            else if (_watcher.ProcessCount > _sessions.Count)
                delay = PendingRescanInterval; // a client is starting or not logged in yet
            else
                delay = Timeout.InfiniteTimeSpan; // nothing to find until a process starts/exits

            try { await _wake.WaitAsync(delay, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task ScanAsync(CancellationToken ct)
    {
        IReadOnlyList<HelloInfo> found;
        try
        {
            found = await ClientScanner.ScanAsync(_api, _sessions.Keys.ToHashSet(), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { return; }

        foreach (var hello in found)
            StartSession(hello, ct);
    }

    private void StartSession(HelloInfo hello, CancellationToken ct)
    {
        var session = new ClientSession(_api, hello, _appKey)
        {
            ProcessId = PortOwner.FindListeningProcessId(hello.Port),
        };
        if (!_sessions.TryAdd(hello.Port, session))
            return;

        SessionAdded?.Invoke(session);

        var task = Task.Run(async () =>
        {
            try
            {
                await session.RunAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                _sessions.TryRemove(new KeyValuePair<int, ClientSession>(hello.Port, session));
                SessionRemoved?.Invoke(session);
                Wake(); // the slot may come back (e.g. logout + login), re-evaluate
            }
        }, CancellationToken.None);

        lock (_sessionTasks)
        {
            _sessionTasks.RemoveAll(t => t.IsCompleted);
            _sessionTasks.Add(task);
        }
    }

    private void Wake()
    {
        try { _wake.Release(); }
        catch (SemaphoreFullException) { } // already signaled
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        if (_watcher is not null)
            await _watcher.DisposeAsync().ConfigureAwait(false);

        Task[] pending;
        lock (_sessionTasks)
            pending = [.. _sessionTasks];
        if (_loop is not null)
            pending = [.. pending, _loop];

        try { await Task.WhenAll(pending).ConfigureAwait(false); }
        catch (OperationCanceledException) { }

        _cts.Dispose();
        _wake.Dispose();
    }
}
