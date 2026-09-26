using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Techbuilder.DeclareAPI.IntegrationTests.Infrastructure;

namespace Techbuilder.DeclareAPI.IntegrationTests;

public class HealthCheckTests : ApiTestBase
{
    public HealthCheckTests(PostgresFixture db) : base(db) { }

    [IntegrationFact]
    public async Task Liveness_is_healthy()
    {
        var response = await Client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [IntegrationFact]
    public async Task Readiness_reports_the_database_as_healthy()
    {
        var response = await Client.GetAsync("/health/ready");

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        body.Should().Be("Healthy");
    }
}

/// <summary>
/// No endpoint declares <c>cache</c> or <c>rate_limit</c>, with caching and rate limiting enabled (the default).
/// </summary>
public class NoPoliciesStartupTests : ApiTestBase
{
    public NoPoliciesStartupTests(PostgresFixture db) : base(db, "no-policies.yaml") { }

    [IntegrationFact]
    public async Task App_starts_and_serves_requests()
    {
        var response = await Client.GetAsync("/api/doctors");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }
}

/// <summary>
/// The unmodified <c>Techbuilder.DeclareAPI.Sample/declareapi.yaml</c> against the unmodified sample schema.
/// </summary>
public class SampleConfigTests : ApiTestBase
{
    public SampleConfigTests(PostgresFixture db) : base(db, "sample.yaml") { }

    [IntegrationFact]
    public async Task Sample_create_appointment_works()
    {
        var patientId = await Db.ScalarAsync<Guid>("SELECT id FROM patients WHERE cpf = '78912345600'");
        var doctorId = await Db.ScalarAsync<Guid>("SELECT id FROM doctors WHERE crm = 'CRM-CE 67890'");

        var response = await Client.PostAsJsonAsync("/api/appointments", new
        {
            patient_id = patientId,
            doctor_id = doctorId,
            appointment_date = "2026-11-20T09:00:00Z"
        });

        var json = await ReadJsonAsync(response);
        var id = json.GetProperty("id").GetGuid();
        (await Db.ScalarAsync<Guid>("SELECT patient_id FROM appointments WHERE id = @id", ("id", id))).Should().Be(patientId);
    }

    [IntegrationFact]
    public async Task Sample_patient_lifecycle_works()
    {
        var id = await CreateAsync("/api/patients", new { p_name = "Sample Flow", p_birth_date = "2021-06-01", p_cpf = NewCpf() });

        (await Client.PutAsJsonAsync($"/api/patients/{id}", new { p_name = "Sample Flow 2" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadJsonAsync(await Client.GetAsync($"/api/patients/{id}"))).GetProperty("name").GetString()
            .Should().Be("Sample Flow 2");
        (await Client.DeleteAsync($"/api/patients/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
