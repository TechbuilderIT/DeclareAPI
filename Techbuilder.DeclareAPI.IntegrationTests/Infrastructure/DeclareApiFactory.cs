using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Techbuilder.DeclareAPI.Dapper;
using Techbuilder.DeclareAPI.Extensions;

namespace Techbuilder.DeclareAPI.IntegrationTests.Infrastructure;

/// <summary>
/// In-memory host wired like <c>Techbuilder.DeclareAPI.Sample/Program.cs</c>, with a YAML file and
/// connection string chosen by the test. Every request is authenticated as a user holding the
/// <c>admin</c> and <c>doctor</c> roles, so the sample's policies can be exercised.
/// All DeclareAPI options keep their defaults (caching and rate limiting enabled).
/// </summary>
public sealed class DeclareApiFactory : WebApplicationFactory<DeclareApiFactory>
{
    private readonly string _yamlPath;
    private readonly string _connectionString;

    public DeclareApiFactory(string yamlPath, string connectionString)
    {
        _yamlPath = yamlPath;
        _connectionString = connectionString;
    }

    protected override IHostBuilder CreateHostBuilder() =>
        Host.CreateDefaultBuilder()
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureWebHostDefaults(web => web
                .ConfigureServices(services =>
                {
                    services
                        .AddAuthentication(TestAuthHandler.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

                    services.AddAuthorization(options =>
                    {
                        options.AddPolicy("admin_only", policy => policy.RequireRole("admin"));
                        options.AddPolicy("doctor_or_admin", policy => policy.RequireRole("admin", "doctor"));
                    });

                    services.AddDeclareApi(options =>
                    {
                        options.ConfigFile = _yamlPath;
                        options.UseDataAccess(_ => new DapperDataAccess(_connectionString, DatabaseProvider.PostgreSQL));
                    });

                    services.AddEndpointsApiExplorer();
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseDeclareApiObservability();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseDeclareApiPolicies();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapDeclareApi();
                        endpoints.MapDeclareApiHealthChecks();
                    });
                }));
}

internal sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "integration-tester"),
            new Claim(ClaimTypes.Role, "admin"),
            new Claim(ClaimTypes.Role, "doctor")
        }, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
