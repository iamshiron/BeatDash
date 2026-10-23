using System.Net.Http.Headers;
using System.Net.Http.Json;
using Shiron.BeatDash.Data.Backup;
using Spectre.Console;

namespace Shiron.BeatDash.Cli.Commands;

internal static class BackupClient {
    internal static async Task<HttpClient> LoginAsync(BackupSettings settings, CancellationToken ct) {
        var email = settings.Email ?? Environment.GetEnvironmentVariable("BEATDASH_ADMIN_EMAIL")
            ?? throw new InvalidOperationException("Supply --email or BEATDASH_ADMIN_EMAIL.");
        var password = Environment.GetEnvironmentVariable(settings.PasswordEnvironmentVariable);
        if (string.IsNullOrEmpty(password)) {
            if (Console.IsInputRedirected) throw new InvalidOperationException("Set the admin password environment variable for unattended use.");
            password = AnsiConsole.Prompt(new TextPrompt<string>("Admin password:").Secret());
        }
        var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) {
            BaseAddress = new Uri(settings.Host), Timeout = Timeout.InfiniteTimeSpan
        };
        try {
            var twoFactorCode = Environment.GetEnvironmentVariable(settings.TwoFactorEnvironmentVariable);
            using var response = await http.PostAsJsonAsync("/api/admin/backups/login", new BackupLoginRequest(email, password, twoFactorCode!), ct);
            await EnsureSuccessAsync(response, ct);
            var login = await response.Content.ReadFromJsonAsync<BackupLoginResponse>(ct)
                ?? throw new InvalidDataException("Backend returned no authentication token.");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
            return http;
        } catch { http.Dispose(); throw; }
    }

    internal static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct) {
        if (response.IsSuccessStatusCode) return;
        var error = await response.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException($"Backend returned {(int) response.StatusCode}: {error}");
    }

    internal static async Task<int> RunAsync(Func<Task> operation) {
        try { await operation(); return 0; } catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException or OperationCanceledException) {
            AnsiConsole.MarkupLineInterpolated($"[red]{ex.Message}[/]");
            return 1;
        }
    }
}
