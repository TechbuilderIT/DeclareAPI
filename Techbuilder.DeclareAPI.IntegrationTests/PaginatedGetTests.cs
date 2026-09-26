using System.Text.Json;
using FluentAssertions;
using Techbuilder.DeclareAPI.IntegrationTests.Infrastructure;

namespace Techbuilder.DeclareAPI.IntegrationTests;

/// <summary>
/// GET paginated over the <c>items</c> table: every filter operator, sorting and page limits.
/// </summary>
public class PaginatedGetTests : ApiTestBase
{
    public PaginatedGetTests(PostgresFixture db) : base(db) { }

    [IntegrationTheory]
    [InlineData("name=widget", "Alpha Widget,Gamma Widget")]                          // contains (ILIKE)
    [InlineData("sku=AW", "Alpha Widget,Gamma Widget")]                               // starts_with
    [InlineData("supplier=inc", "Beta Gadget,Delta Tool")]                            // ends_with
    [InlineData("category=toys", "Beta Gadget,Epsilon Gizmo")]                        // equals (text)
    [InlineData("qty=25", "Delta Tool,Epsilon Gizmo")]                                // gt (int)
    [InlineData("price=25.50", "Beta Gadget,Epsilon Gizmo,Gamma Widget")]             // gte (numeric)
    [InlineData("weight=7", "Alpha Widget,Beta Gadget")]                              // lt (int)
    [InlineData("stock=5", "Delta Tool,Gamma Widget")]                                // lte (int)
    [InlineData("status=active,pending", "Alpha Widget,Beta Gadget,Delta Tool,Epsilon Gizmo")] // in
    [InlineData("created_on=2024-02-01,2024-04-30", "Beta Gadget,Delta Tool,Gamma Widget")]    // between (date)
    [InlineData("owner_id=22222222-2222-2222-2222-222222222222", "Delta Tool,Gamma Widget")]   // equals (uuid)
    [InlineData("category=tools&qty=10", "Delta Tool,Gamma Widget")]                  // filters combine with AND
    public async Task Filter_operator_returns_matching_rows(string query, string expectedNames)
    {
        var json = await GetItemsAsync($"{query}&orderBy=name");

        var expected = expectedNames.Split(',');
        json.GetProperty("totalCount").GetInt32().Should().Be(expected.Length);
        Names(json).Should().Equal(expected.Take(3)); // max_page_size: 3
    }

    [IntegrationFact]
    public async Task OrderBy_sorts_ascending_and_descending()
    {
        Names(await GetItemsAsync("orderBy=qty")).Should().Equal("Alpha Widget", "Beta Gadget", "Gamma Widget");
        Names(await GetItemsAsync("orderBy=price%20DESC")).Should().Equal("Epsilon Gizmo", "Gamma Widget", "Beta Gadget");
    }

    [IntegrationFact]
    public async Task Page_and_pageSize_slice_the_result()
    {
        var json = await GetItemsAsync("orderBy=name&page=2&pageSize=2");

        Names(json).Should().Equal("Delta Tool", "Epsilon Gizmo");
        json.GetProperty("page").GetInt32().Should().Be(2);
        json.GetProperty("pageSize").GetInt32().Should().Be(2);
        json.GetProperty("totalCount").GetInt32().Should().Be(5);
        json.GetProperty("totalPages").GetInt32().Should().Be(3);
    }

    [IntegrationFact]
    public async Task PageSize_is_clamped_to_max_page_size()
    {
        var json = await GetItemsAsync("orderBy=name&pageSize=50");

        json.GetProperty("pageSize").GetInt32().Should().Be(3);
        json.GetProperty("items").GetArrayLength().Should().Be(3);
        json.GetProperty("hasNextPage").GetBoolean().Should().BeTrue();
    }

    private async Task<JsonElement> GetItemsAsync(string query) =>
        await ReadJsonAsync(await Client.GetAsync($"/api/items?{query}"));

    private static List<string> Names(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString()!).ToList();
}
