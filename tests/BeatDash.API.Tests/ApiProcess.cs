using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Shiron.BeatDash.API.Endpoints;

namespace Shiron.BeatDash.API.Tests;

/// <summary>The API server running as a separate process, as it does in production.</summary>
public sealed class ApiProcess : IAsyncDisposable {
    private readonly Process _process;
    private readonly StringBuilder _log;

    private ApiProcess(Process process, Uri address, StringBuilder log) {
        _process = process;
        _log = log;
        Address = address;
    }

    public Uri Address { get; }

    public string Log {
        get { lock (_log) return _log.ToString(); }
    }

    /// <summary>Starts the server with production defaults, then <paramref name="configure"/>'s environment.</summary>
    public static async Task<ApiProcess> StartAsync(string workingDirectory, Action<IDictionary<string, string?>> configure) {
        Directory.CreateDirectory(workingDirectory);
        var start = new ProcessStartInfo("dotnet") {
            WorkingDirectory = workingDirectory, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(typeof(IdentityEndpoints).Assembly.Location);
        start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        start.Environment["Jwt__SecretKey"] = "api-process-test-secret-at-least-32-characters";
        start.Environment["Jwt__Issuer"] = "api-process-test";
        start.Environment["Jwt__Audience"] = "api-process-test";
        start.Environment["Backup__WorkDirectory"] = Path.Combine(workingDirectory, "server-work");
        start.Environment["BeatSaver__FetchOnStartup"] = "false";
        start.Environment["BeatSaver__ScheduledFetchEnabled"] = "false";
        start.Environment["BeatSaver__FetchOnNewMap"] = "false";
        start.Environment["UdpSocket__Host"] = "127.0.0.1";
        start.Environment["UdpSocket__Port"] = GetAvailableUdpPort().ToString();
        configure(start.Environment);

        var process = new Process { StartInfo = start, EnableRaisingEvents = true };
        var log = new StringBuilder();
        var ready = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        process.OutputDataReceived += (_, args) => {
            const string prefix = "Now listening on: ";
            lock (log) log.AppendLine(args.Data);
            if (args.Data is { } line && line.IndexOf(prefix, StringComparison.Ordinal) is var index && index >= 0)
                ready.TrySetResult(new Uri(line[(index + prefix.Length)..].Trim()));
        };
        process.ErrorDataReceived += (_, args) => { lock (log) log.AppendLine(args.Data); };
        process.Exited += (_, _) => { lock (log) ready.TrySetException(new InvalidOperationException(log.ToString())); };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        var address = await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
        return new ApiProcess(process, address, log);
    }

    public HttpClient CreateClient(CookieContainer? cookies = null) =>
        new(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = cookies ?? new CookieContainer() }) { BaseAddress = Address };

    private static int GetAvailableUdpPort() {
        using var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint) socket.Client.LocalEndPoint!).Port;
    }

    public async ValueTask DisposeAsync() {
        if (!_process.HasExited) _process.Kill(entireProcessTree: true);
        await _process.WaitForExitAsync();
        _process.Dispose();
    }
}
