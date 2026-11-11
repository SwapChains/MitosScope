using System.Net.Http.Headers;
using System.Text.Json;

namespace MitosScope.Core.Api;

/// <summary>
/// Result of one API call. <see cref="StatusCode"/> is 0 when the client could not be reached
/// (connection refused, timeout, malformed response).
/// </summary>
public readonly record struct ApiResult<T>(int StatusCode, T? Value, string? Error)
{
    public bool IsSuccess => StatusCode is >= 200 and < 300 && Value is not null;
    public bool IsUnreachable => StatusCode == 0;
}

/// <summary>
/// Thin HTTP wrapper around the local addon API (ADDON_API.md). Stateless: the caller keeps
/// the port and token. HttpClient never sends an Origin header, and Host comes from the URL,
/// which is what the server requires.
/// </summary>
public sealed class MitosApiClient : IDisposable
{
    public const int FirstPort = 47300;
    public const int SlotCount = 20;
    public static readonly TimeSpan DiscoveryTimeout = TimeSpan.FromMilliseconds(300);
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(25); // server waits up to 20 s on its backend

    private readonly HttpClient _http;

    public MitosApiClient()
    {
        _http = new HttpClient(new SocketsHttpHandler
        {
            UseProxy = false,                       // 127.0.0.1 only, never go through a proxy
            PooledConnectionLifetime = TimeSpan.Zero, // server answers Connection: close anyway
        })
        {
            Timeout = Timeout.InfiniteTimeSpan,     // per-request timeouts via CancellationToken
        };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public Task<ApiResult<HelloInfo>> HelloAsync(int port, TimeSpan timeout, CancellationToken ct) =>
        SendAsync<HelloInfo>(port, HttpMethod.Get, "/v1/hello", null, null, timeout, ct);

    public Task<ApiResult<AuthResponse>> AuthAsync(int port, string appKey, CancellationToken ct) =>
        SendAsync<AuthResponse>(port, HttpMethod.Post, "/v1/auth", new { app_key = appKey }, null, DefaultTimeout, ct);

    public Task<ApiResult<GameState>> GameAsync(int port, string token, CancellationToken ct) =>
        SendAsync<GameState>(port, HttpMethod.Get, "/v1/game", null, token, TimeSpan.FromSeconds(5), ct);

    public Task<ApiResult<SectionResponse<T>>> SectionAsync<T>(int port, string section, string token, CancellationToken ct) =>
        SendAsync<SectionResponse<T>>(port, HttpMethod.Get, "/v1/" + section, null, token, DefaultTimeout, ct);

    public async Task<ApiResult<T>> SendAsync<T>(int port, HttpMethod method, string path, object? body,
        string? token, TimeSpan timeout, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        using var request = new HttpRequestMessage(method, $"http://127.0.0.1:{port}{path}");
        if (body is not null)
        {
            // Buffered on purpose: JsonContent streams with chunked transfer encoding (no
            // Content-Length), which the client's minimal HTTP server rejects with 400.
            var json = JsonSerializer.SerializeToUtf8Bytes(body);
            request.Content = new ByteArrayContent(json);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        }
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var response = await _http.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
            var status = (int)response.StatusCode;
            var bytes = await response.Content.ReadAsByteArrayAsync(timeoutCts.Token).ConfigureAwait(false);

            if (status is >= 200 and < 300)
            {
                // A payload we can't parse is our bug, not a dead client: keep the real status.
                try { return new ApiResult<T>(status, JsonSerializer.Deserialize<T>(bytes, MitosJson.Options), null); }
                catch (JsonException ex) { return new ApiResult<T>(status, default, "invalid_json: " + ex.Message); }
            }

            string? error = null;
            try { error = JsonSerializer.Deserialize<ErrorResponse>(bytes, MitosJson.Options)?.Error; }
            catch (JsonException) { }
            return new ApiResult<T>(status, default, error ?? response.ReasonPhrase);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new ApiResult<T>(0, default, "timeout");
        }
        catch (HttpRequestException ex)
        {
            return new ApiResult<T>(0, default, ex.Message);
        }
    }

    public void Dispose() => _http.Dispose();
}
