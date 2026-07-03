using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Shiron.BeatDash.Data;
using Zenject;

namespace Shiron.BeatDash.Mod.Network;

public class TokenRefreshHandler : DelegatingHandler {
    [Inject]
    public TokenRefreshHandler(PluginConfig config) {
        _config = config;
    }

    private PluginConfig _config { get; set; } = null!;

    private static readonly SemaphoreSlim _refreshLock = new(1, 1);
    private static readonly HttpClient HttpClient = new();

    protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.AccessToken);
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized) {
            return response;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try {
            if (request.Headers.Authorization.Parameter == _config.AccessToken) {
                var pair = await PerformTokenRefreshAsync();
                _config.AccessToken = pair.AccessToken;
                _config.RefreshToken = pair.RefreshToken;
                Plugin.Log.Debug("Access token refreshed.");
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.AccessToken);
            response.Dispose();
            var clonedRequest = await CloneRequestAsync(request);
            response = await base.SendAsync(clonedRequest, cancellationToken);
        } finally {
            _refreshLock.Release();
        }

        return response;
    }

    private async Task<TokenPairDto> PerformTokenRefreshAsync() {
        var content = new StringContent(JsonConvert.SerializeObject(new RefreshTokenRequestDto {
            AccessToken = _config.AccessToken,
            RefreshToken = _config.RefreshToken,
            ClientId = Guid.Parse(_config.ClientId)
        }), Encoding.UTF8, "application/json");

        var response = await HttpClient.PostAsync($"{(_config.UseSsl ? "https" : "http")}://{_config.Host}/api/auth/refresh-token", content);
        if (!response.IsSuccessStatusCode) {
            throw new Exception("Failed to refresh token");
        }

        var tokenPair = JsonConvert.DeserializeObject<TokenPairDto>(await response.Content.ReadAsStringAsync());
        if (tokenPair == null) throw new Exception("TokenPair is null!");

        return tokenPair;
    }

    private async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage req) {
        var clone = new HttpRequestMessage(req.Method, req.RequestUri) {
            Version = req.Version
        };

        foreach (var header in req.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (req.Content != null) {
            var ms = new System.IO.MemoryStream();
            await req.Content.CopyToAsync(ms);
            ms.Position = 0;
            clone.Content = new StreamContent(ms);

            foreach (var header in req.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
