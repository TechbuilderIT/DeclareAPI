using DeclareAPI.Agent.Core.Models;
using DeclareAPI.Agent.Generators;
using FluentAssertions;
using Xunit;

namespace DeclareAPI.Agent.Tests.GeneratorTests;

public class DeclareApiYamlGeneratorTests
{
    private readonly DeclareApiYamlGenerator _generator = new();

    [Fact]
    public async Task GenerateAsync_ValidSpec_GeneratesYaml()
    {
        // Arrange
        var spec = CreateSimpleSpec();

        // Act
        var result = await _generator.GenerateAsync(spec);

        // Assert
        result.Success.Should().BeTrue();
        result.FileName.Should().Be("declareapi.yaml");
        result.Content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateAsync_ValidSpec_ContainsVersion()
    {
        // Arrange
        var spec = CreateSimpleSpec();

        // Act
        var result = await _generator.GenerateAsync(spec);

        // Assert
        result.Content.Should().Contain("version: \"1.0\"");
    }

    [Fact]
    public async Task GenerateAsync_ValidSpec_ContainsDatabaseConfig()
    {
        // Arrange
        var spec = CreateSimpleSpec();
        spec.Database.Provider = DatabaseProvider.PostgreSQL;

        // Act
        var result = await _generator.GenerateAsync(spec);

        // Assert
        result.Content.Should().Contain("database:");
        result.Content.Should().Contain("provider: postgresql");
        result.Content.Should().Contain("connection:");
    }

    [Fact]
    public async Task GenerateAsync_ValidSpec_ContainsSettings()
    {
        // Arrange
        var spec = CreateSimpleSpec();
        spec.Api.BasePath = "/api";
        spec.Api.DefaultPageSize = 25;

        // Act
        var result = await _generator.GenerateAsync(spec);

        // Assert
        result.Content.Should().Contain("settings:");
        result.Content.Should().Contain("base_path: /api");
        result.Content.Should().Contain("default_page_size: 25");
    }

    [Fact]
    public async Task GenerateAsync_ValidSpec_GeneratesListEndpoint()
    {
        // Arrange
        var spec = CreateSimpleSpec();

        // Act
        var result = await _generator.GenerateAsync(spec);

        // Assert
        result.Content.Should().Contain("list:");
        result.Content.Should().Contain("method: GET");
        result.Content.Should().Contain("path: /users");
        result.Content.Should().Contain("paginated: true");
    }

    [Fact]
    public async Task GenerateAsync_ValidSpec_GeneratesGetEndpoint()
    {
        // Arrange
        var spec = CreateSimpleSpec();

        // Act
        var result = await _generator.GenerateAsync(spec);

        // Assert
        result.Content.Should().Contain("get:");
        result.Content.Should().Contain("path: /users/{id}");
        result.Content.Should().Contain("params:");
        result.Content.Should().Contain("name: id");
        result.Content.Should().Contain("from: route");
    }

    [Fact]
    public async Task GenerateAsync_ValidSpec_GeneratesCreateEndpoint()
    {
        // Arrange
        var spec = CreateSimpleSpec();

        // Act
        var result = await _generator.GenerateAsync(spec);

        // Assert
        result.Content.Should().Contain("create:");
        result.Content.Should().Contain("method: POST");
        result.Content.Should().Contain("fields:");
        result.Content.Should().Contain("returns: uuid");
    }

    [Fact]
    public async Task GenerateAsync_ValidSpec_GeneratesUpdateEndpoint()
    {
        // Arrange
        var spec = CreateSimpleSpec();

        // Act
        var result = await _generator.GenerateAsync(spec);

        // Assert
        result.Content.Should().Contain("update:");
        result.Content.Should().Contain("method: PUT");
    }

    [Fact]
    public async Task GenerateAsync_ValidSpec_GeneratesDeleteEndpoint()
    {
        // Arrange
        var spec = CreateSimpleSpec();

        // Act
        var result = await _generator.GenerateAsync(spec);

        // Assert
        result.Content.Should().Contain("delete:");
        result.Content.Should().Contain("method: DELETE");
    }

    [Fact]
    public async Task GenerateAsync_EntityWithEmail_AddsContainsFilter()
    {
        // Arrange
        var spec = CreateSimpleSpec();

        // Act
        var result = await _generator.GenerateAsync(spec);

        // Assert
        result.Content.Should().Contain("filters:");
        result.Content.Should().Contain("field: email");
        result.Content.Should().Contain("operator: contains");
    }

    [Fact]
    public async Task GenerateAsync_NoEntities_ReturnsError()
    {
        // Arrange
        var spec = new ProjectSpec { Name = "Test" };

        // Act
        var result = await _generator.GenerateAsync(spec);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("At least one entity is required");
    }

    [Fact]
    public async Task GenerateAsync_EntityWithoutPrimaryKey_ReturnsError()
    {
        // Arrange
        var spec = new ProjectSpec
        {
            Name = "Test",
            Entities =
            [
                new Entity
                {
                    Name = "Users",
                    TableName = "users",
                    Fields = [new Field { Name = "email", Type = "varchar" }]
                }
            ]
        };

        // Act
        var result = await _generator.GenerateAsync(spec);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("no primary key");
    }

    [Fact]
    public void Validate_EmptyProjectName_ReturnsError()
    {
        // Arrange
        var spec = new ProjectSpec { Name = "" };

        // Act
        var errors = _generator.Validate(spec);

        // Assert
        errors.Should().Contain(e => e.Contains("Project name is required"));
    }

    private static ProjectSpec CreateSimpleSpec()
    {
        return new ProjectSpec
        {
            Name = "TestProject",
            Version = "1.0",
            Database = new DatabaseSettings
            {
                Provider = DatabaseProvider.PostgreSQL
            },
            Api = new ApiSettings
            {
                BasePath = "/api",
                DefaultPageSize = 25
            },
            Entities =
            [
                new Entity
                {
                    Name = "Users",
                    TableName = "users",
                    Fields =
                    [
                        new Field
                        {
                            Name = "id",
                            Type = "uuid",
                            IsPrimaryKey = true,
                            IsNullable = false,
                            DefaultValue = "gen_random_uuid()"
                        },
                        new Field
                        {
                            Name = "email",
                            Type = "varchar",
                            MaxLength = 255,
                            IsNullable = false,
                            IsUnique = true
                        },
                        new Field
                        {
                            Name = "first_name",
                            Type = "varchar",
                            MaxLength = 100,
                            IsNullable = true
                        }
                    ],
                    PrimaryKeyFields = ["id"]
                }
            ]
        };
    }
}
