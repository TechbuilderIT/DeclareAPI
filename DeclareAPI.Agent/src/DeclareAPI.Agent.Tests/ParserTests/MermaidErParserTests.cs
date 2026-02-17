using DeclareAPI.Agent.Core.Models;
using DeclareAPI.Agent.Parsers;
using FluentAssertions;
using Xunit;

namespace DeclareAPI.Agent.Tests.ParserTests;

public class MermaidErParserTests
{
    private readonly MermaidErParser _parser = new();

    [Fact]
    public async Task ParseAsync_SimpleEntity_ExtractsFieldsCorrectly()
    {
        // Arrange
        var content = """
            erDiagram
                users {
                    uuid id PK "Identificador único"
                    varchar email UK "Email do usuário"
                    varchar first_name "Nome"
                    boolean is_active "Está ativo"
                    timestamp created_at
                }
            """;

        // Act
        var result = await _parser.ParseAsync(content);

        // Assert
        result.Entities.Should().HaveCount(1);

        var entity = result.Entities[0];
        entity.Name.Should().Be("Users");
        entity.TableName.Should().Be("users");
        entity.Fields.Should().HaveCount(5);

        var idField = entity.Fields.First(f => f.Name == "id");
        idField.Type.Should().Be("uuid");
        idField.IsPrimaryKey.Should().BeTrue();
        idField.Description.Should().Be("Identificador único");

        var emailField = entity.Fields.First(f => f.Name == "email");
        emailField.IsUnique.Should().BeTrue();
    }

    [Fact]
    public async Task ParseAsync_EntityWithForeignKey_DetectsForeignKey()
    {
        // Arrange
        var content = """
            erDiagram
                users {
                    uuid id PK
                    varchar email UK
                }
                customer_profiles {
                    uuid id PK
                    uuid user_id FK "1:1 com users"
                    varchar crm_number
                }
            """;

        // Act
        var result = await _parser.ParseAsync(content);

        // Assert
        result.Entities.Should().HaveCount(2);

        var profile = result.Entities.First(e => e.Name == "CustomerProfiles");
        var userIdField = profile.Fields.First(f => f.Name == "user_id");

        userIdField.IsForeignKey.Should().BeTrue();
    }

    [Fact]
    public async Task ParseAsync_EntityWithRelationships_ParsesRelationships()
    {
        // Arrange
        var content = """
            erDiagram
                users {
                    uuid id PK
                    varchar email UK
                }
                posts {
                    uuid id PK
                    uuid user_id FK
                    text content
                }
                users ||--o{ posts : "has posts"
            """;

        // Act
        var result = await _parser.ParseAsync(content);

        // Assert
        result.Relationships.Should().HaveCount(1);

        var relationship = result.Relationships[0];
        relationship.FromEntity.Should().Be("Users");
        relationship.ToEntity.Should().Be("Posts");
        relationship.Type.Should().Be(RelationshipType.OneToMany);
    }

    [Fact]
    public async Task ParseAsync_OneToOneRelationship_DetectsCorrectly()
    {
        // Arrange
        var content = """
            erDiagram
                users {
                    uuid id PK
                }
                profiles {
                    uuid id PK
                    uuid user_id FK_UK
                }
                users ||--o| profiles : "has profile"
            """;

        // Act
        var result = await _parser.ParseAsync(content);

        // Assert
        result.Relationships.Should().HaveCount(1);
        result.Relationships[0].Type.Should().Be(RelationshipType.OneToOne);
    }

    [Fact]
    public async Task ParseAsync_WithComments_IgnoresComments()
    {
        // Arrange
        var content = """
            erDiagram
                %% This is a comment
                users {
                    uuid id PK
                    %% Another comment
                    varchar email UK
                }
            """;

        // Act
        var result = await _parser.ParseAsync(content);

        // Assert
        result.Entities.Should().HaveCount(1);
        result.Entities[0].Fields.Should().HaveCount(2);
    }

    [Fact]
    public async Task ParseAsync_VarcharWithLength_ExtractsMaxLength()
    {
        // Arrange
        var content = """
            erDiagram
                users {
                    uuid id PK
                    varchar(255) email UK
                    varchar(100) first_name
                }
            """;

        // Act
        var result = await _parser.ParseAsync(content);

        // Assert
        var entity = result.Entities[0];

        var emailField = entity.Fields.First(f => f.Name == "email");
        emailField.MaxLength.Should().Be(255);

        var nameField = entity.Fields.First(f => f.Name == "first_name");
        nameField.MaxLength.Should().Be(100);
    }

    [Fact]
    public async Task ParseAsync_PrimaryKeyUUID_SetsDefaultValue()
    {
        // Arrange
        var content = """
            erDiagram
                users {
                    uuid id PK
                }
            """;

        // Act
        var result = await _parser.ParseAsync(content);

        // Assert
        var idField = result.Entities[0].Fields[0];
        idField.DefaultValue.Should().Be("gen_random_uuid()");
        idField.IsAutoGenerated.Should().BeTrue();
    }

    [Fact]
    public async Task ParseAsync_TimestampField_SetsDefaultValue()
    {
        // Arrange
        var content = """
            erDiagram
                users {
                    uuid id PK
                    timestamp created_at
                }
            """;

        // Act
        var result = await _parser.ParseAsync(content);

        // Assert
        var createdAtField = result.Entities[0].Fields.First(f => f.Name == "created_at");
        createdAtField.DefaultValue.Should().Be("NOW()");
    }

    [Theory]
    [InlineData("string", "varchar")]
    [InlineData("int", "integer")]
    [InlineData("bool", "boolean")]
    [InlineData("datetime", "timestamp")]
    [InlineData("guid", "uuid")]
    [InlineData("json", "jsonb")]
    public async Task ParseAsync_NormalizesTypes(string mermaidType, string expectedType)
    {
        // Arrange
        var content = $"""
            erDiagram
                test {"{"}
                    uuid id PK
                    {mermaidType} field1
                {"}"}
            """;

        // Act
        var result = await _parser.ParseAsync(content);

        // Assert
        var field = result.Entities[0].Fields.First(f => f.Name == "field1");
        field.Type.Should().Be(expectedType);
    }

    [Fact]
    public void CanParse_MermaidFile_ReturnsTrue()
    {
        _parser.CanParse("diagram.mermaid").Should().BeTrue();
        _parser.CanParse("diagram.mmd").Should().BeTrue();
        _parser.CanParse("diagram.md").Should().BeTrue();
    }

    [Fact]
    public void CanParse_NonMermaidFile_ReturnsFalse()
    {
        _parser.CanParse("file.yaml").Should().BeFalse();
        _parser.CanParse("file.json").Should().BeFalse();
        _parser.CanParse("file.txt").Should().BeFalse();
    }
}
