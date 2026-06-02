using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using TUnit.Core;

namespace Klassd.UiTests;

/// <summary>
/// Boots the Klassd.Sample app once for the whole test run on a free port,
/// backed by a throwaway SQLite database. Blazor Server needs a real Kestrel server
/// (not an in-memory TestServer), so we launch it as a process. TUnit.Playwright
/// manages the browser; we only own the app under test.
///
/// Assumes the Sample's Program uses <c>.UseSqlite(GetSection("Sqlite"))</c>; the
/// connection string is overridden to a temp file via Sqlite__ConnectionString.
/// </summary>
public static class GlobalHooks
{
    private static Process? _process;
    private static string _dbPath = "";

    /// <summary>Base URL of the running Sample, e.g. http://127.0.0.1:5173.</summary>
    public static string BaseUrl { get; private set; } = "";

    [Before(HookType.TestSession)]
    public static async Task StartSampleAsync()
    {
        var repoRoot = FindRepoRoot();
        var sampleProject = Path.Combine(repoRoot, "src", "Klassd.Sample", "Klassd.Sample.csproj");
        if (!File.Exists(sampleProject))
            throw new FileNotFoundException($"Sample project not found at {sampleProject}");

        var port = GetFreePort();
        BaseUrl = $"http://127.0.0.1:{port}";
        _dbPath = Path.Combine(Path.GetTempPath(), $"cfcms-uitests-{Guid.NewGuid():N}.db");

        var psi = new ProcessStartInfo("dotnet", $"run --project \"{sampleProject}\" --no-launch-profile")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["ASPNETCORE_URLS"] = BaseUrl;
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        psi.Environment["Sqlite__ConnectionString"] = $"Data Source={_dbPath}";

        _process = new Process { StartInfo = psi };

        var listening = new TaskCompletionSource();
        void OnLine(string? line)
        {
            if (line is null) return;
            Console.WriteLine($"[sample] {line}");
            if (line.Contains("Now listening on", StringComparison.OrdinalIgnoreCase))
                listening.TrySetResult();
        }
        _process.OutputDataReceived += (_, e) => OnLine(e.Data);
        _process.ErrorDataReceived += (_, e) => OnLine(e.Data);

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        var startup = await Task.WhenAny(listening.Task, Task.Delay(TimeSpan.FromSeconds(180)));
        if (startup != listening.Task)
            throw new TimeoutException("Sample app did not start within 180s.");
    }

    [After(HookType.TestSession)]
    public static void StopSample()
    {
        try
        {
            if (_process is { HasExited: false })
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(10_000);
            }
        }
        catch { /* best effort */ }
        finally
        {
            _process?.Dispose();
            foreach (var f in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
                try { if (File.Exists(f)) File.Delete(f); } catch { }
        }
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Klassd.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Could not locate Klassd.slnx above the test output directory.");
    }
}
