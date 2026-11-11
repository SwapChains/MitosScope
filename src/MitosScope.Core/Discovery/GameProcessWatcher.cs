using System.Diagnostics;

namespace MitosScope.Core.Discovery;

/// <summary>
/// Polls the process list for the game executable(s) and raises <see cref="Changed"/> whenever a
/// game process starts or exits. Names are matched exactly (case-insensitive, without ".exe"),
/// so similarly named tools are not picked up.
/// </summary>
public sealed class GameProcessWatcher : IAsyncDisposable
{
    private readonly string[] _processNames;
    private readonly TimeSpan _interval;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;
    private HashSet<int> _pids = [];

    public GameProcessWatcher(IEnumerable<string> processNames, TimeSpan interval)
    {
        _processNames = processNames
            .Select(n => n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? n[..^4] : n)
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _interval = interval;
    }

    /// <summary>Raised on a thread-pool thread with the current number of game processes.</summary>
    public event Action<int>? Changed;

    public int ProcessCount => _pids.Count;

    /// <summary>PIDs seen on the last poll.</summary>
    public IReadOnlyCollection<int> ProcessIds => _pids; // replaced, never mutated: safe to read

    public void Start() => _loop ??= Task.Run(() => RunAsync(_cts.Token));

    /// <summary>Checks the executable name directly, so it's right even between two polls.</summary>
    public bool IsGameProcess(int processId)
    {
        try
        {
            using var p = Process.GetProcessById(processId);
            return _processNames.Contains(p.ProcessName, StringComparer.OrdinalIgnoreCase);
        }
        catch (ArgumentException) { return false; }          // already exited
        catch (InvalidOperationException) { return false; }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_interval);
        do
        {
            var current = Snapshot();
            if (!current.SetEquals(_pids))
            {
                _pids = current;
                Changed?.Invoke(current.Count);
            }
        }
        while (await WaitAsync(timer, ct).ConfigureAwait(false));
    }

    private HashSet<int> Snapshot()
    {
        var pids = new HashSet<int>();
        foreach (var name in _processNames)
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                pids.Add(p.Id);
                p.Dispose();
            }
        }
        return pids;
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct).ConfigureAwait(false); }
        catch (OperationCanceledException) { return false; }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        if (_loop is not null)
            await _loop.ConfigureAwait(false);
        _cts.Dispose();
    }
}
