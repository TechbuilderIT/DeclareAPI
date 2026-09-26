using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Techbuilder.DeclareAPI.IntegrationTests.Infrastructure;

namespace Techbuilder.DeclareAPI.IntegrationTests;

/// <summary>
/// GET by id and the write endpoints backed by the sample's PostgreSQL FUNCTIONs
/// (<c>sp_create_patient</c>, <c>sp_update_patient</c>, <c>sp_delete_patient</c>, <c>sp_create_appointment</c>).
/// Route <c>{id}</c> reaches the routine as <c>p_id</c>; body fields already named <c>p_*</c> keep their name.
/// </summary>
public class FunctionWriteTests : ApiTestBase
{
    public FunctionWriteTests(PostgresFixture db) : base(db) { }

    [IntegrationFact]
    public async Task Get_by_uuid_route_returns_the_row()
    {
        var id = await Db.ScalarAsync<Guid>("SELECT id FROM patients WHERE cpf = '12345678901'");

        var json = await ReadJsonAsync(await Client.GetAsync($"/api/patients/{id}"));

        json.GetProperty("id").GetGuid().Should().Be(id);
        json.GetProperty("name").GetString().Should().Be("João Pedro Silva");
        json.GetProperty("total_appointments").GetInt64().Should().Be(1);
    }

    [IntegrationFact]
    public async Task Get_by_unknown_id_returns_404()
    {
        var response = await Client.GetAsync($"/api/patients/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [IntegrationFact]
    public async Task Post_with_returns_calls_the_function_and_returns_the_new_id()
    {
        var cpf = NewCpf();

        var response = await Client.PostAsJsonAsync("/api/patients",
            new { p_name = "Created Patient", p_birth_date = "2020-03-15", p_cpf = cpf });

        var json = await ReadJsonAsync(response);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = json.GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().Should().Be($"/api/patients/{id}");

        (await Db.ScalarAsync<string>("SELECT cpf FROM patients WHERE id = @id", ("id", id))).Should().Be(cpf);
        (await Db.ScalarAsync<DateOnly>("SELECT birth_date FROM patients WHERE id = @id", ("id", id)))
            .Should().Be(new DateOnly(2020, 3, 15));
    }

    [IntegrationFact]
    public async Task Post_ignores_body_keys_not_declared_in_fields()
    {
        var id = await CreateAsync("/api/patients",
            new { p_name = "Extra Keys", p_birth_date = "2019-01-01", p_cpf = NewCpf(), p_guardian_id = Guid.NewGuid(), unknown = 1 });

        (await Db.ScalarAsync<string>("SELECT name FROM patients WHERE id = @id", ("id", id))).Should().Be("Extra Keys");
    }

    [IntegrationFact]
    public async Task Post_with_invalid_body_returns_400()
    {
        var response = await Client.PostAsJsonAsync("/api/patients",
            new { p_name = "No CPF", p_birth_date = "2020-01-01" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [IntegrationFact]
    public async Task Put_passes_route_param_and_body_to_the_function()
    {
        var id = await CreateAsync("/api/patients", new { p_name = "Before Put", p_birth_date = "2020-01-01", p_cpf = NewCpf() });

        var response = await Client.PutAsJsonAsync($"/api/patients/{id}", new { p_name = "After Put" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        var json = await ReadJsonAsync(await Client.GetAsync($"/api/patients/{id}"));
        json.GetProperty("name").GetString().Should().Be("After Put");
    }

    [IntegrationFact]
    public async Task Patch_passes_route_param_and_body_to_the_function()
    {
        var id = await CreateAsync("/api/patients", new { p_name = "Before Patch", p_birth_date = "2020-01-01", p_cpf = NewCpf() });

        var response = await Client.PatchAsJsonAsync($"/api/patients/{id}", new { p_name = "After Patch" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        (await Db.ScalarAsync<string>("SELECT name FROM patients WHERE id = @id", ("id", id))).Should().Be("After Patch");
    }

    [IntegrationFact]
    public async Task Put_without_optional_field_uses_the_function_default()
    {
        var id = await CreateAsync("/api/patients", new { p_name = "Keep Name", p_birth_date = "2020-01-01", p_cpf = NewCpf() });

        var response = await Client.PutAsJsonAsync($"/api/patients/{id}", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        (await Db.ScalarAsync<string>("SELECT name FROM patients WHERE id = @id", ("id", id))).Should().Be("Keep Name");
    }

    [IntegrationFact]
    public async Task Delete_passes_route_param_to_the_function()
    {
        var id = await CreateAsync("/api/patients", new { p_name = "To Delete", p_birth_date = "2020-01-01", p_cpf = NewCpf() });

        var response = await Client.DeleteAsync($"/api/patients/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        (await Db.ScalarAsync<string>("SELECT status FROM patients WHERE id = @id", ("id", id))).Should().Be("inactive");
    }

    [IntegrationFact]
    public async Task Post_without_returns_calls_the_function()
    {
        var message = $"function-{Guid.NewGuid()}";

        var response = await Client.PostAsJsonAsync("/api/events", new { message, level = "warn" });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        (await Db.ScalarAsync<string>("SELECT level || '/' || via FROM audit_log WHERE message = @m", ("m", message)))
            .Should().Be("warn/function");
    }

    [IntegrationFact]
    public async Task Post_appointment_calls_sp_create_appointment()
    {
        var patientId = await Db.ScalarAsync<Guid>("SELECT id FROM patients WHERE cpf = '45678912300'");
        var doctorId = await Db.ScalarAsync<Guid>("SELECT id FROM doctors WHERE crm = 'CRM-CE 11111'");

        var id = await CreateAsync("/api/appointments", new
        {
            patient_id = patientId,
            doctor_id = doctorId,
            appointment_date = "2026-10-01T14:30:00",
            notes = "integration"
        });

        (await Db.ScalarAsync<DateTime>("SELECT appointment_date FROM appointments WHERE id = @id", ("id", id)))
            .Should().Be(new DateTime(2026, 10, 1, 14, 30, 0));
    }
}
