using FluentAssertions;
using Techbuilder.DeclareAPI.Core.Configuration;
using Techbuilder.DeclareAPI.Core.Query;

namespace Techbuilder.DeclareAPI.Tests;

public class FilterQueryBuilderTests
{
    [Fact]
    public void BuildFromFilters_EqualsOperator_BuildsCorrectSql()
    {
        // Arrange
        var filters = new List<FilterConfig>
        {
            new() { Field = "status", Operator = "equals" }
        };
        var values = new Dictionary<string, object?> { ["status"] = "active" };

        // Act
        var (whereClause, parameters) = FilterQueryBuilder.BuildFromFilters(filters, values);

        // Assert
        whereClause.Should().Contain("\"status\" = @p0");
        parameters.Should().ContainKey("p0");
        parameters["p0"].Should().Be("active");
    }

    [Fact]
    public void BuildFromFilters_ContainsOperator_BuildsILikeSql()
    {
        // Arrange
        var filters = new List<FilterConfig>
        {
            new() { Field = "name", Operator = "contains" }
        };
        var values = new Dictionary<string, object?> { ["name"] = "John" };

        // Act
        var (whereClause, parameters) = FilterQueryBuilder.BuildFromFilters(filters, values);

        // Assert
        whereClause.Should().Contain("\"name\" ILIKE @p0");
        parameters["p0"].Should().Be("%John%");
    }

    [Fact]
    public void BuildFromFilters_StartsWithOperator_BuildsCorrectPattern()
    {
        // Arrange
        var filters = new List<FilterConfig>
        {
            new() { Field = "name", Operator = "starts_with" }
        };
        var values = new Dictionary<string, object?> { ["name"] = "Dr." };

        // Act
        var (whereClause, parameters) = FilterQueryBuilder.BuildFromFilters(filters, values);

        // Assert
        whereClause.Should().Contain("\"name\" ILIKE @p0");
        parameters["p0"].Should().Be("Dr.%");
    }

    [Fact]
    public void BuildFromFilters_EndsWithOperator_BuildsCorrectPattern()
    {
        // Arrange
        var filters = new List<FilterConfig>
        {
            new() { Field = "email", Operator = "ends_with" }
        };
        var values = new Dictionary<string, object?> { ["email"] = "@gmail.com" };

        // Act
        var (whereClause, parameters) = FilterQueryBuilder.BuildFromFilters(filters, values);

        // Assert
        whereClause.Should().Contain("\"email\" ILIKE @p0");
        parameters["p0"].Should().Be("%@gmail.com");
    }

    [Fact]
    public void BuildFromFilters_GreaterThanOperator_BuildsCorrectSql()
    {
        // Arrange
        var filters = new List<FilterConfig>
        {
            new() { Field = "age", Operator = "gt" }
        };
        var values = new Dictionary<string, object?> { ["age"] = 18 };

        // Act
        var (whereClause, parameters) = FilterQueryBuilder.BuildFromFilters(filters, values);

        // Assert
        whereClause.Should().Contain("\"age\" > @p0");
        parameters["p0"].Should().Be(18);
    }

    [Fact]
    public void BuildFromFilters_GreaterThanOrEqualOperator_BuildsCorrectSql()
    {
        // Arrange
        var filters = new List<FilterConfig>
        {
            new() { Field = "score", Operator = "gte" }
        };
        var values = new Dictionary<string, object?> { ["score"] = 70 };

        // Act
        var (whereClause, parameters) = FilterQueryBuilder.BuildFromFilters(filters, values);

        // Assert
        whereClause.Should().Contain("\"score\" >= @p0");
    }

    [Fact]
    public void BuildFromFilters_LessThanOperator_BuildsCorrectSql()
    {
        // Arrange
        var filters = new List<FilterConfig>
        {
            new() { Field = "price", Operator = "lt" }
        };
        var values = new Dictionary<string, object?> { ["price"] = 100 };

        // Act
        var (whereClause, parameters) = FilterQueryBuilder.BuildFromFilters(filters, values);

        // Assert
        whereClause.Should().Contain("\"price\" < @p0");
    }

    [Fact]
    public void BuildFromFilters_LessThanOrEqualOperator_BuildsCorrectSql()
    {
        // Arrange
        var filters = new List<FilterConfig>
        {
            new() { Field = "quantity", Operator = "lte" }
        };
        var values = new Dictionary<string, object?> { ["quantity"] = 50 };

        // Act
        var (whereClause, parameters) = FilterQueryBuilder.BuildFromFilters(filters, values);

        // Assert
        whereClause.Should().Contain("\"quantity\" <= @p0");
    }

    [Fact]
    public void BuildFromFilters_MultipleFilters_BuildsAndCondition()
    {
        // Arrange
        var filters = new List<FilterConfig>
        {
            new() { Field = "status", Operator = "equals" },
            new() { Field = "name", Operator = "contains" }
        };
        var values = new Dictionary<string, object?>
        {
            ["status"] = "active",
            ["name"] = "John"
        };

        // Act
        var (whereClause, parameters) = FilterQueryBuilder.BuildFromFilters(filters, values);

        // Assert
        whereClause.Should().Contain("AND");
        parameters.Should().HaveCount(2);
    }

    [Fact]
    public void BuildFromFilters_NullValue_SkipsFilter()
    {
        // Arrange
        var filters = new List<FilterConfig>
        {
            new() { Field = "status", Operator = "equals" }
        };
        var values = new Dictionary<string, object?> { ["status"] = null };

        // Act
        var (whereClause, parameters) = FilterQueryBuilder.BuildFromFilters(filters, values);

        // Assert
        whereClause.Should().BeEmpty();
        parameters.Should().BeEmpty();
    }

    [Fact]
    public void BuildFromFilters_MissingValue_SkipsFilter()
    {
        // Arrange
        var filters = new List<FilterConfig>
        {
            new() { Field = "status", Operator = "equals" }
        };
        var values = new Dictionary<string, object?>();

        // Act
        var (whereClause, parameters) = FilterQueryBuilder.BuildFromFilters(filters, values);

        // Assert
        whereClause.Should().BeEmpty();
        parameters.Should().BeEmpty();
    }

    [Fact]
    public void BuildFromFilters_SqlServerDialect_UsesBrackets()
    {
        // Arrange
        var filters = new List<FilterConfig>
        {
            new() { Field = "name", Operator = "equals" }
        };
        var values = new Dictionary<string, object?> { ["name"] = "test" };

        // Act
        var (whereClause, _) = FilterQueryBuilder.BuildFromFilters(filters, values, DatabaseDialect.SqlServer);

        // Assert
        whereClause.Should().Contain("[name]");
    }

    [Fact]
    public void BuildFromFilters_MySqlDialect_UsesBackticks()
    {
        // Arrange
        var filters = new List<FilterConfig>
        {
            new() { Field = "name", Operator = "equals" }
        };
        var values = new Dictionary<string, object?> { ["name"] = "test" };

        // Act
        var (whereClause, _) = FilterQueryBuilder.BuildFromFilters(filters, values, DatabaseDialect.MySQL);

        // Assert
        whereClause.Should().Contain("`name`");
    }

    [Fact]
    public void AddEquals_FluentApi_BuildsCorrectSql()
    {
        // Arrange
        var builder = new FilterQueryBuilder();

        // Act
        builder.AddEquals("status", "active");
        var (whereClause, parameters) = builder.Build();

        // Assert
        whereClause.Should().Contain("\"status\" = @p0");
        parameters["p0"].Should().Be("active");
    }

    [Fact]
    public void AddFilter_InOperator_BuildsInClause()
    {
        // Arrange
        var builder = new FilterQueryBuilder();
        var filter = new FilterConfig { Field = "status", Operator = "in" };

        // Act
        builder.AddFilter(filter, "active,pending,completed");
        var (whereClause, _) = builder.Build();

        // Assert
        whereClause.Should().Contain("\"status\" IN (");
    }

    [Fact]
    public void AddFilter_BetweenOperator_BuildsBetweenClause()
    {
        // Arrange
        var builder = new FilterQueryBuilder();
        var filter = new FilterConfig { Field = "age", Operator = "between" };

        // Act
        builder.AddFilter(filter, "18,65");
        var (whereClause, _) = builder.Build();

        // Assert
        whereClause.Should().Contain("\"age\" BETWEEN @p0_min AND @p0_max");
    }

    [Fact]
    public void AddFilter_InOperator_EmitsOneParameterPerPlaceholder()
    {
        // Arrange
        var builder = new FilterQueryBuilder();
        var filter = new FilterConfig { Field = "status", Operator = "in" };

        // Act
        builder.AddFilter(filter, "active, pending");
        var (whereClause, parameters) = builder.Build();

        // Assert
        whereClause.Should().Contain("\"status\" IN (@p0_0, @p0_1)");
        parameters.Should().HaveCount(2);
        parameters["p0_0"].Should().Be("active");
        parameters["p0_1"].Should().Be("pending");
    }

    [Fact]
    public void AddFilter_BetweenOperator_EmitsMinAndMaxParameters()
    {
        // Arrange
        var builder = new FilterQueryBuilder();
        var filter = new FilterConfig { Field = "age", Operator = "between" };

        // Act
        builder.AddFilter(filter, "18, 65");
        var (_, parameters) = builder.Build();

        // Assert
        parameters.Should().HaveCount(2);
        parameters["p0_min"].Should().Be("18");
        parameters["p0_max"].Should().Be("65");
    }

    [Fact]
    public void AddFilter_InAndBetweenMixedWithEquals_KeepsParameterNamesAligned()
    {
        // Arrange
        var builder = new FilterQueryBuilder();

        // Act
        builder.AddFilter(new FilterConfig { Field = "status", Operator = "in" }, "a,b");
        builder.AddFilter(new FilterConfig { Field = "age", Operator = "between" }, "1,2");
        builder.AddFilter(new FilterConfig { Field = "name", Operator = "equals" }, "x");
        var (whereClause, parameters) = builder.Build();

        // Assert: every @placeholder in the SQL has a parameter, and nothing else
        var placeholders = System.Text.RegularExpressions.Regex.Matches(whereClause, @"@(\w+)")
            .Select(m => m.Groups[1].Value);
        parameters.Keys.Should().BeEquivalentTo(placeholders);
    }

    [Fact]
    public void Build_NoConditions_ReturnsEmpty()
    {
        // Arrange
        var builder = new FilterQueryBuilder();

        // Act
        var (whereClause, parameters) = builder.Build();

        // Assert
        whereClause.Should().BeEmpty();
        parameters.Should().BeEmpty();
    }
}
