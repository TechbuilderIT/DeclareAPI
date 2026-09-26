using System.Collections.Concurrent;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Techbuilder.DeclareAPI.IntegrationTests.Infrastructure;

/// <summary>
/// One PostgreSQL 16 container for the whole integration run, loaded with the sample schema
/// (<c>Sample/sql/init.sql</c>, unmodified) plus the integration-only objects (<c>Sql/integration.sql</c>).
/// Hosts are created per YAML file and reused.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("declareapi_it")
        .WithUsername("dev")
        .WithPassword("dev123")
        .Build();

    private readonly ConcurrentDictionary<string, DeclareApiFactory> _factories = new();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        if (!DockerAvailability.IsAvailable && !DockerAvailability.IsRequired)
            return; // every test in the collection is skipped

        await _container.StartAsync();

        foreach (var script in new[] { "sample-init.sql", "integration.sql" })
        {
            var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Sql", script));
            await ExecuteAsync(sql);
        }
    }

    /// <summary>
    /// Returns an in-memory API host for the given YAML file under <c>Yaml/</c>.
    /// </summary>
    public DeclareApiFactory Api(string yamlFile) =>
        _factories.GetOrAdd(yamlFile, file => new DeclareApiFactory(
            Path.Combine(AppContext.BaseDirectory, "Yaml", file),
            ConnectionString));

    public async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<T?> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? default : (T)result;
    }

    public async Task DisposeAsync()
    {
        foreach (var factory in _factories.Values)
            await factory.DisposeAsync();

        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "PostgreSQL";
}
