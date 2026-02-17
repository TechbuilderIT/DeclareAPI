using FluentAssertions;
using Techbuilder.DeclareAPI.Core.Configuration;

namespace Techbuilder.DeclareAPI.Tests;

public class ConfigLoaderTests
{
    private readonly ConfigLoader _loader = new();

    [Fact]
    public void LoadFromYaml_ValidConfig_ParsesCorrectly()
    {
        // Arrange
        var yaml = """
            version: "1.0"
            database:
              provider: postgresql
              connection: "Host=localhost;Database=test"
            settings:
              base_path: /api
              default_page_size: 25
            entities:
              Patient:
                description: "Pacientes do sistema"
                endpoints:
                  list:
                    method: GET
                    path: /patients
                    source:
                      type: view
                      name: vw_patients
                    paginated: true
            """;

        // Act
        var config = _loader.LoadFromYaml(yaml);

        // Assert
        config.Version.Should().Be("1.0");
        config.Database.Provider.Should().Be("postgresql");
        config.Database.Connection.Should().Be("Host=localhost;Database=test");
        config.Settings.BasePath.Should().Be("/api");
        config.Settings.DefaultPageSize.Should().Be(25);
        config.Entities.Should().ContainKey("Patient");
        config.Entities["Patient"].Description.Should().Be("Pacientes do sistema");
        config.Entities["Patient"].Endpoints.Should().ContainKey("list");
    }

    [Fact]
    public void LoadFromYaml_WithFilters_ParsesFiltersCorrectly()
    {
        // Arrange
        var yaml = """
            version: "1.0"
            database:
              provider: postgresql
              connection: "Host=localhost"
            entities:
              Patient:
                endpoints:
                  list:
                    method: GET
                    path: /patients
                    source:
                      type: view
                      name: vw_patients
                    filters:
                      - { field: name, operator: contains }
                      - { field: status, operator: equals }
            """;

        // Act
        var config = _loader.LoadFromYaml(yaml);

        // Assert
        var endpoint = config.Entities["Patient"].Endpoints["list"];
        endpoint.Filters.Should().HaveCount(2);
        endpoint.Filters![0].Field.Should().Be("name");
        endpoint.Filters[0].FilterOperator.Should().Be(FilterOperator.Contains);
        endpoint.Filters[1].Field.Should().Be("status");
        endpoint.Filters[1].FilterOperator.Should().Be(FilterOperator.Equals);
    }

    [Fact]
    public void LoadFromYaml_WithFields_ParsesFieldsCorrectly()
    {
        // Arrange
        var yaml = """
            version: "1.0"
            database:
              provider: postgresql
              connection: "Host=localhost"
            entities:
              Patient:
                endpoints:
                  create:
                    method: POST
                    path: /patients
                    source:
                      type: function
                      name: sp_create_patient
                    fields:
                      - { name: name, type: string, required: true, max: 200 }
                      - { name: birth_date, type: date, required: true }
                      - { name: cpf, type: string, pattern: "^\\d{11}$" }
                    returns: uuid
            """;

        // Act
        var config = _loader.LoadFromYaml(yaml);

        // Assert
        var endpoint = config.Entities["Patient"].Endpoints["create"];
        endpoint.Fields.Should().HaveCount(3);
        endpoint.Fields![0].Name.Should().Be("name");
        endpoint.Fields[0].FieldType.Should().Be(FieldType.String);
        endpoint.Fields[0].Required.Should().BeTrue();
        endpoint.Fields[0].Max.Should().Be(200);
        endpoint.Fields[1].FieldType.Should().Be(FieldType.Date);
        endpoint.Fields[2].Pattern.Should().Be(@"^\d{11}$");
        endpoint.Returns.Should().Be("uuid");
    }

    [Fact]
    public void LoadFromYaml_WithCustomHandler_SetsIsCustomHandler()
    {
        // Arrange
        var yaml = """
            version: "1.0"
            database:
              provider: postgresql
              connection: "Host=localhost"
            entities:
              Patient:
                endpoints:
                  transfer:
                    method: POST
                    path: /patients/{id}/transfer
                    handler: PatientTransferHandler
            """;

        // Act
        var config = _loader.LoadFromYaml(yaml);

        // Assert
        var endpoint = config.Entities["Patient"].Endpoints["transfer"];
        endpoint.IsCustomHandler.Should().BeTrue();
        endpoint.Handler.Should().Be("PatientTransferHandler");
    }

    [Fact]
    public void LoadFromYaml_WithEnvironmentVariable_ExpandsVariable()
    {
        // Arrange
        Environment.SetEnvironmentVariable("TEST_DB_CONNECTION", "Host=testhost;Database=testdb");
        var yaml = """
            version: "1.0"
            database:
              provider: postgresql
              connection: "${TEST_DB_CONNECTION}"
            entities:
              Test:
                endpoints:
                  list:
                    method: GET
                    path: /test
                    source:
                      type: view
                      name: vw_test
            """;

        try
        {
            // Act
            var config = _loader.LoadFromYaml(yaml);

            // Assert
            config.Database.Connection.Should().Be("Host=testhost;Database=testdb");
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEST_DB_CONNECTION", null);
        }
    }

    [Fact]
    public void LoadFromYaml_EmptyYaml_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _loader.LoadFromYaml("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LoadFromYaml_MissingConnectionString_ThrowsConfigurationException()
    {
        // Arrange
        var yaml = """
            version: "1.0"
            database:
              provider: postgresql
            entities:
              Test:
                endpoints:
                  list:
                    method: GET
                    path: /test
                    source:
                      type: view
                      name: vw_test
            """;

        // Act & Assert
        var act = () => _loader.LoadFromYaml(yaml);
        act.Should().Throw<ConfigurationException>()
           .Which.Errors.Should().Contain(e => e.Contains("connection string"));
    }

    [Fact]
    public void LoadFromYaml_InvalidProvider_ThrowsConfigurationException()
    {
        // Arrange
        var yaml = """
            version: "1.0"
            database:
              provider: mongodb
              connection: "localhost"
            entities:
              Test:
                endpoints:
                  list:
                    method: GET
                    path: /test
                    source:
                      type: view
                      name: vw_test
            """;

        // Act & Assert
        var act = () => _loader.LoadFromYaml(yaml);
        act.Should().Throw<ConfigurationException>()
           .Which.Errors.Should().Contain(e => e.Contains("Invalid database provider"));
    }

    [Fact]
    public void LoadFromYaml_EndpointWithoutSourceOrHandler_ThrowsConfigurationException()
    {
        // Arrange
        var yaml = """
            version: "1.0"
            database:
              provider: postgresql
              connection: "localhost"
            entities:
              Test:
                endpoints:
                  list:
                    method: GET
                    path: /test
            """;

        // Act & Assert
        var act = () => _loader.LoadFromYaml(yaml);
        act.Should().Throw<ConfigurationException>()
           .Which.Errors.Should().Contain(e => e.Contains("handler") || e.Contains("source"));
    }
}
