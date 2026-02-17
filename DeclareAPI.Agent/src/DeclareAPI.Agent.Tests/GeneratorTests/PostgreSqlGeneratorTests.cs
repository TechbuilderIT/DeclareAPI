using DeclareAPI.Agent.Core.Models;
using DeclareAPI.Agent.Generators;
using FluentAssertions;
using Xunit;

namespace DeclareAPI.Agent.Tests.GeneratorTests;

public class PostgreSqlGeneratorTests
{
    private readonly PostgreSqlGenerator _generator = new();

    [Fact]
    public async Task GenerateAsync_ValidSpec_GeneratesSql()
    {
        // Arrange
        var spec = CreateSimpleSpec();

        // Act
        var result = await _generator.GenerateAsync(spec);

        // Assert
        result.Success.Should().BeTrue();
        result.FileName.Should().Be("init.sql");
        result.Content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateAsync_ValidSpec_IncludesPgcryptoExtension()
    {
        // Arrange
        var spec = CreateSimpleSpec();

        // Act
        var result = await _generator.GenerateAsync(spec);

        // Assert
        result.Content.Should().Contain("CREATE EXTENSION IF NOT EXISTS \"pgcrypto\"");
    }

    [Fact]
    public async Task GenerateAsync_ValidSpec_GeneratesDropStatements()
    {
        // Arrange
        var spec = CreateSimpleSpec();

        // Act
        var result = await _generator.GenerateAsync(spec);

        // Assert
        result.Content.Should().Contain("DROP TABLE IF EXISTS users CASCADE");
    }

    [Fact]
    public async Task GenerateAsync_ValidSpec_GeneratesCreateTable()
    {
        // Arrange
        var spec = CreateSimpleSpec();

        // Act
        var result = await _generator.GenerateAsync(spec);

        // Assert
        result.Content.Should().Contain("CREATE TABLE users");
        result.Content.Should().Contain("id UUID");
        result.Content.Should().Contain("PRIMARY KEY");
        result.Content.Should().Contain("DEFAULT gen_random_uuid()");
    }

    [Fact]
    public async Task GenerateAsync_FieldWithMaxLength_GeneratesVarcharWithLength()
    {
        // Arrange
        var spec = CreateSimpleSpec();

        // Act
        var result = await _generator.GenerateAsync(spec);

        // Assert
        result.Content.Should().Contain("email VARCHAR(255)");
        result.Content.Should().Contain("first_name VARCHAR(100)");
    }

    [Fact]
    public async Task GenerateAsync_UniqueField_GeneratesUniqueConstraint()
    {
        // Arrange
        var spec = CreateSimpleSpec();

        // Act
        var result = await _generator.GenerateAsync(spec);

        // Assert
        result.Content.Should().Contain("email VARCHAR(255) NOT NULL UNIQUE");
    }

    [Fact]
    public async Task GenerateAsync_EntityWithTimestamps_AddsTimestampColumns()
    {
        // Arrange
        var spec = CreateSimpleSpec();
        spec.Entities[0].HasTimestamps = true;
        spec.Entities[0].Fields.RemoveAll(f => f.Name == "created_at");

        // Act
        var result = await _generator.GenerateAsync(spec);

        // Assert
        result.Content.Should().Contain("created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()");
        result.Content.Should().Contain("updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()");
    }

    [Fact]
    public async Task GenerateAsync_ValidSpec_GeneratesIndexes()
    {
        // Arrange
        var spec = CreateSimpleSpec();

        // Act
        var result = await _generator.GenerateAsync(spec);

        // Assert
        // For unique fields, the generator creates UNIQUE INDEX
        result.Content.Should().Contain("INDEX");
        result.Content.Should().Contain("idx_users_email");
    }

    [Fact]
    public async Task GenerateAsync_EntityWithForeignKey_GeneratesForeignKeyConstraint()
    {
        // Arrange
        var spec = CreateSpecWithForeignKey();

        // Act
        var result = await _generator.GenerateAsync(spec);

        // Assert
        result.Content.Should().Contain("ALTER TABLE posts");
        result.Content.Should().Contain("ADD CONSTRAINT fk_posts_user_id");
        result.Content.Should().Contain("FOREIGN KEY (user_id)");
        result.Content.Should().Contain("REFERENCES users(id)");
    }

    [Fact]
    public async Task GenerateAsync_EntityWithDescription_GeneratesComments()
    {
        // Arrange
        var spec = CreateSimpleSpec();
        spec.Entities[0].Description = "User accounts";

        // Act
        var result = await _generator.GenerateAsync(spec);

        // Assert
        result.Content.Should().Contain("COMMENT ON TABLE users IS 'User accounts'");
    }

    [Fact]
    public async Task GenerateAsync_FieldWithDescription_GeneratesColumnComment()
    {
        // Arrange
        var spec = CreateSimpleSpec();
        spec.Entities[0].Fields[0].Description = "Unique identifier";

        // Act
        var result = await _generator.GenerateAsync(spec);

        // Assert
        result.Content.Should().Contain("COMMENT ON COLUMN users.id IS 'Unique identifier'");
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

    [Theory]
    [InlineData("uuid", "UUID")]
    [InlineData("varchar", "VARCHAR(255)")]
    [InlineData("text", "TEXT")]
    [InlineData("integer", "INTEGER")]
    [InlineData("bigint", "BIGINT")]
    [InlineData("boolean", "BOOLEAN")]
    [InlineData("timestamp", "TIMESTAMP WITH TIME ZONE")]
    [InlineData("date", "DATE")]
    [InlineData("jsonb", "JSONB")]
    [InlineData("inet", "INET")]
    public async Task GenerateAsync_MapsTypesCorrectly(string inputType, string expectedType)
    {
        // Arrange
        var spec = new ProjectSpec
        {
            Name = "Test",
            Entities =
            [
                new Entity
                {
                    Name = "Test",
                    TableName = "test",
                    Fields =
                    [
                        new Field { Name = "id", Type = "uuid", IsPrimaryKey = true },
                        new Field { Name = "field1", Type = inputType }
                    ]
                }
            ]
        };

        // Act
        var result = await _generator.GenerateAsync(spec);

        // Assert
        result.Content.Should().Contain(expectedType);
    }

    private static ProjectSpec CreateSimpleSpec()
    {
        return new ProjectSpec
        {
            Name = "TestProject",
            Entities =
            [
                new Entity
                {
                    Name = "Users",
                    TableName = "users",
                    HasTimestamps = false,
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
                    ]
                }
            ]
        };
    }

    private static ProjectSpec CreateSpecWithForeignKey()
    {
        return new ProjectSpec
        {
            Name = "TestProject",
            Entities =
            [
                new Entity
                {
                    Name = "Users",
                    TableName = "users",
                    Fields =
                    [
                        new Field { Name = "id", Type = "uuid", IsPrimaryKey = true }
                    ]
                },
                new Entity
                {
                    Name = "Posts",
                    TableName = "posts",
                    Fields =
                    [
                        new Field { Name = "id", Type = "uuid", IsPrimaryKey = true },
                        new Field
                        {
                            Name = "user_id",
                            Type = "uuid",
                            IsForeignKey = true,
                            ForeignKeyTable = "users",
                            ForeignKeyColumn = "id",
                            IsNullable = false
                        },
                        new Field { Name = "content", Type = "text" }
                    ]
                }
            ]
        };
    }
}
