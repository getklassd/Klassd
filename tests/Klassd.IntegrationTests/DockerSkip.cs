using System.IO.Pipes;
using TUnit.Core;

namespace Klassd.IntegrationTests;

/// <summary>Skips the decorated test(s) when no Docker daemon is reachable (e.g. CI without Docker).</summary>
public sealed class SkipWhenDockerUnavailableAttribute() : SkipAttribute("Docker is not available")
{
    public override Task<bool> ShouldSkip(TestRegisteredContext context) =>
        Task.FromResult(!DockerProbe.IsAvailable());
}

internal static class DockerProbe
{
    private static bool? _cached;

    public static bool IsAvailable() => _cached ??= Probe();

    private static bool Probe()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var pipe = new NamedPipeClientStream(".", "docker_engine", PipeDirection.InOut);
                pipe.Connect(500);
                return true;
            }

            return File.Exists("/var/run/docker.sock")
                || Environment.GetEnvironmentVariable("DOCKER_HOST") is { Length: > 0 };
        }
        catch
        {
            return false;
        }
    }
}
