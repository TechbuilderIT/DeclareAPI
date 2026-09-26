using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Techbuilder.DeclareAPI.IntegrationTests.Infrastructure;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public abstract class ApiTestBase
{
    protected ApiTestBase(PostgresFixture db, string yamlFile = "integration.yaml")
    {
        Db = db;
        YamlFile = yamlFile;
    }

    protected PostgresFixture Db { get; }

    protected string YamlFile { get; }

    protected HttpClient Client => Db.Api(YamlFile).CreateClient();

    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"expected success but got {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    protected async Task<Guid> CreateAsync(string path, object body)
    {
        var response = await Client.PostAsJsonAsync(path, body);
        var json = await ReadJsonAsync(response);
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        return json.GetProperty("id").GetGuid();
    }

    /// <summary>Unique 11-digit CPF so repeated creates don't hit the UNIQUE constraint.</summary>
    protected static string NewCpf() => Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString();
}
