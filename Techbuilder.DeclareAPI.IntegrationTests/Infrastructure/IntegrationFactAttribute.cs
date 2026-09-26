using DotNet.Testcontainers.Configurations;

namespace Techbuilder.DeclareAPI.IntegrationTests.Infrastructure;

/// <summary>
/// A fact that needs a Docker daemon (PostgreSQL runs in Testcontainers).
/// Skipped when Docker is not reachable, so a plain <c>dotnet test</c> without Docker
/// runs only the unit tests. Set <c>DECLAREAPI_REQUIRE_INTEGRATION=1</c> to turn the
/// skip into a failure (e.g. in CI).
/// Filter with <c>--filter Category=Integration</c> or <c>--filter Category!=Integration</c>.
/// </summary>
public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (!DockerAvailability.IsAvailable && !DockerAvailability.IsRequired)
        {
            Skip = "Docker is not available (integration tests need PostgreSQL via Testcontainers).";
        }
    }
}

/// <summary>
/// Theory variant of <see cref="IntegrationFactAttribute"/>.
/// </summary>
public sealed class IntegrationTheoryAttribute : TheoryAttribute
{
    public IntegrationTheoryAttribute()
    {
        if (!DockerAvailability.IsAvailable && !DockerAvailability.IsRequired)
        {
            Skip = "Docker is not available (integration tests need PostgreSQL via Testcontainers).";
        }
    }
}

internal static class DockerAvailability
{
    private static readonly Lazy<bool> Available = new(() =>
    {
        try
        {
            return TestcontainersSettings.OS.DockerEndpointAuthConfig != null;
        }
        catch
        {
            return false;
        }
    });

    public static bool IsAvailable => Available.Value;

    public static bool IsRequired =>
        Environment.GetEnvironmentVariable("DECLAREAPI_REQUIRE_INTEGRATION") == "1";
}
