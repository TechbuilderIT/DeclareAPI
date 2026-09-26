using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Techbuilder.DeclareAPI.IntegrationTests.Infrastructure;

namespace Techbuilder.DeclareAPI.IntegrationTests;

/// <summary>
/// <c>source.type: procedure</c> is executed with <c>CALL</c>. Fields without the <c>p_</c> prefix
/// (<c>name</c>, <c>specialty</c>) reach the procedure as <c>p_name</c>, <c>p_specialty</c>.
/// </summary>
public class ProcedureWriteTests : ApiTestBase
{
    public ProcedureWriteTests(PostgresFixture db) : base(db) { }

    [IntegrationFact]
    public async Task Post_without_returns_calls_the_procedure()
    {
        var message = $"procedure-{Guid.NewGuid()}";

        var response = await Client.PostAsJsonAsync("/api/events/procedure", new { message });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        (await Db.ScalarAsync<string>("SELECT level || '/' || via FROM audit_log WHERE message = @m", ("m", message)))
            .Should().Be("info/procedure");
    }

    [IntegrationFact]
    public async Task Post_with_returns_reads_the_procedure_inout_parameter()
    {
        var crm = $"CRM-IT {Guid.NewGuid():N}"[..20];

        var id = await CreateAsync("/api/doctors", new { name = "Dr. Procedure", specialty = "Clinica", crm });

        var json = await ReadJsonAsync(await Client.GetAsync($"/api/doctors/{id}"));
        json.GetProperty("crm").GetString().Should().Be(crm);
    }

    [IntegrationFact]
    public async Task Put_and_delete_call_the_procedure_with_the_route_param()
    {
        var crm = $"CRM-IT {Guid.NewGuid():N}"[..20];
        var id = await CreateAsync("/api/doctors", new { name = "Dr. Update", specialty = "Old", crm });

        var put = await Client.PutAsJsonAsync($"/api/doctors/{id}", new { specialty = "New" });
        put.StatusCode.Should().Be(HttpStatusCode.NoContent, await put.Content.ReadAsStringAsync());
        (await Db.ScalarAsync<string>("SELECT specialty FROM doctors WHERE id = @id", ("id", id))).Should().Be("New");

        var delete = await Client.DeleteAsync($"/api/doctors/{id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent, await delete.Content.ReadAsStringAsync());
        (await Db.ScalarAsync<long>("SELECT COUNT(*) FROM doctors WHERE id = @id", ("id", id))).Should().Be(0);
    }
}
