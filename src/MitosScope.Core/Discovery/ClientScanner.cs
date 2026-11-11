using MitosScope.Core.Api;

namespace MitosScope.Core.Discovery;

/// <summary>Probes ports 47300–47319 with GET /v1/hello and returns the logged-in Mitos.is clients.</summary>
public static class ClientScanner
{
    public const string ExpectedGame = "mitosis";
    public const int SupportedApiVersion = 1;

    public static async Task<IReadOnlyList<HelloInfo>> ScanAsync(MitosApiClient api, IReadOnlySet<int> skipPorts, CancellationToken ct)
    {
        var probes = Enumerable.Range(MitosApiClient.FirstPort, MitosApiClient.SlotCount)
            .Where(port => !skipPorts.Contains(port))
            .Select(port => api.HelloAsync(port, MitosApiClient.DiscoveryTimeout, ct));

        var results = await Task.WhenAll(probes).ConfigureAwait(false);

        return results
            .Where(r => r.IsSuccess && IsCompatible(r.Value!))
            .Select(r => r.Value!)
            .OrderBy(h => h.Slot)
            .ToList();
    }

    public static bool IsCompatible(HelloInfo hello) =>
        string.Equals(hello.Game, ExpectedGame, StringComparison.OrdinalIgnoreCase)
        && hello.ApiVersion == SupportedApiVersion;
}
