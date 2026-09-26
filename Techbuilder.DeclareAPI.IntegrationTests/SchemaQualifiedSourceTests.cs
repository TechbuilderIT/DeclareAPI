using System.Net.Http.Json;
using FluentAssertions;
using Techbuilder.DeclareAPI.IntegrationTests.Infrastructure;

namespace Techbuilder.DeclareAPI.IntegrationTests;

/// <summary>
/// Sources written as <c>schema.name</c> (view, table and function in schema <c>clinic</c>).
/// </summary>
public class SchemaQualifiedSourceTests : ApiTestBase
{
    public SchemaQualifiedSourceTests(PostgresFixture db) : base(db) { }

    [IntegrationFact]
    public async Task Paginated_get_over_schema_qualified_view()
    {
        var json = await ReadJsonAsync(await Client.GetAsync("/api/rooms?floor=1&orderBy=name"));

        json.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("name").GetString())
            .Should().Contain("Room A").And.NotContain("Room B");
    }

    [IntegrationFact]
    public async Task Get_by_id_over_schema_qualified_table()
    {
        var json = await ReadJsonAsync(await Client.GetAsync("/api/rooms/aaaaaaaa-0000-0000-0000-000000000002"));

        json.GetProperty("name").GetString().Should().Be("Room B");
    }

    [IntegrationFact]
    public async Task Post_to_schema_qualified_function()
    {
        var id = await CreateAsync("/api/rooms", new { name = "Room IT", floor = 7 });

        var json = await ReadJsonAsync(await Client.GetAsync($"/api/rooms/{id}"));
        json.GetProperty("floor").GetInt32().Should().Be(7);
    }
}
