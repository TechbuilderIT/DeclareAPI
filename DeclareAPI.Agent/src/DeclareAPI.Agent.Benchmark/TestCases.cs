using DeclareAPI.Agent.AI.Prompts;
using DeclareAPI.Agent.Benchmark.Models;

namespace DeclareAPI.Agent.Benchmark;

/// <summary>
/// Predefined test cases using real prompts from DeclareAPI.Agent.
/// </summary>
public static class TestCases
{
    /// <summary>
    /// Get all test cases for the benchmark.
    /// </summary>
    public static IEnumerable<BenchmarkTestCase> GetAllTestCases()
    {
        return GetYamlTestCases()
            .Concat(GetSqlTestCases())
            .Concat(GetFilterTestCases())
            .Concat(GetValidationTestCases());
    }

    /// <summary>
    /// Get quick test cases for fast benchmarking.
    /// </summary>
    public static IEnumerable<BenchmarkTestCase> GetQuickTestCases()
    {
        yield return CreateSimpleYamlTestCase();
        yield return CreateSimpleSqlTestCase();
    }

    #region YAML Test Cases

    public static IEnumerable<BenchmarkTestCase> GetYamlTestCases()
    {
        yield return CreateSimpleYamlTestCase();
        yield return CreateComplexYamlTestCase();
    }

    private static BenchmarkTestCase CreateSimpleYamlTestCase()
    {
        const string entityName = "User";
        const string tableName = "users";
        const string fieldsDescription = """
            - id: UUID (Primary Key, auto-generated)
            - email: VARCHAR(255) (Unique, Not Null)
            - first_name: VARCHAR(100) (Nullable)
            - last_name: VARCHAR(100) (Nullable)
            - is_active: BOOLEAN (Default: true)
            - created_at: TIMESTAMP WITH TIME ZONE (Default: NOW())
            """;

        return new BenchmarkTestCase
        {
            Name = "Simple Entity YAML",
            Type = "YAML",
            SystemPrompt = YamlPrompts.SystemPrompt,
            UserPrompt = YamlPrompts.GenerateEntityEndpointsPrompt(entityName, tableName, fieldsDescription),
            SyntaxValidator = output => QualityEvaluator.EvaluateYamlSyntax(output),
            CompletenessValidator = output => QualityEvaluator.EvaluateYamlCompleteness(output, entityName),
            AccuracyValidator = output => QualityEvaluator.EvaluateYamlAccuracy(output, tableName)
        };
    }

    private static BenchmarkTestCase CreateComplexYamlTestCase()
    {
        const string entityName = "Order";
        const string tableName = "orders";
        const string fieldsDescription = """
            - id: UUID (Primary Key, auto-generated)
            - customer_id: UUID (Foreign Key -> customers.id, Not Null)
            - status: VARCHAR(50) (Enum: pending, confirmed, shipped, delivered, cancelled)
            - total_amount: NUMERIC(10,2) (Not Null, Min: 0)
            - shipping_address_id: UUID (Foreign Key -> addresses.id, Nullable)
            - notes: TEXT (Nullable)
            - created_at: TIMESTAMP WITH TIME ZONE (Default: NOW())
            - updated_at: TIMESTAMP WITH TIME ZONE (Default: NOW())
            """;

        return new BenchmarkTestCase
        {
            Name = "Complex Entity YAML (with FK)",
            Type = "YAML",
            SystemPrompt = YamlPrompts.SystemPrompt,
            UserPrompt = YamlPrompts.GenerateEntityEndpointsPrompt(entityName, tableName, fieldsDescription),
            SyntaxValidator = output => QualityEvaluator.EvaluateYamlSyntax(output),
            CompletenessValidator = output => QualityEvaluator.EvaluateYamlCompleteness(output, entityName),
            AccuracyValidator = output => QualityEvaluator.EvaluateYamlAccuracy(output, tableName)
        };
    }

    #endregion

    #region SQL Test Cases

    public static IEnumerable<BenchmarkTestCase> GetSqlTestCases()
    {
        yield return CreateSimpleSqlTestCase();
        yield return CreateComplexSqlTestCase();
    }

    private static BenchmarkTestCase CreateSimpleSqlTestCase()
    {
        const string tableName = "users";
        const string fieldsDescription = """
            - id: UUID, Primary Key, Default: gen_random_uuid()
            - email: VARCHAR(255), Unique, Not Null
            - first_name: VARCHAR(100)
            - last_name: VARCHAR(100)
            - is_active: BOOLEAN, Default: true
            """;
        string[] expectedColumns = ["id", "email", "first_name", "last_name", "is_active"];

        return new BenchmarkTestCase
        {
            Name = "Simple Table SQL",
            Type = "SQL",
            SystemPrompt = SqlPrompts.SystemPrompt,
            UserPrompt = SqlPrompts.GenerateCreateTablePrompt(tableName, fieldsDescription),
            SyntaxValidator = output => QualityEvaluator.EvaluateSqlSyntax(output),
            CompletenessValidator = output => QualityEvaluator.EvaluateSqlCompleteness(output, tableName, expectedColumns),
            AccuracyValidator = output => QualityEvaluator.EvaluateSqlAccuracy(output, tableName)
        };
    }

    private static BenchmarkTestCase CreateComplexSqlTestCase()
    {
        const string tableName = "orders";
        const string fieldsDescription = """
            - id: UUID, Primary Key, Default: gen_random_uuid()
            - customer_id: UUID, Not Null, Foreign Key -> customers(id)
            - status: VARCHAR(50), Not Null, Default: 'pending'
            - total_amount: NUMERIC(10,2), Not Null
            - shipping_address_id: UUID, Nullable, Foreign Key -> addresses(id)
            - notes: TEXT
            """;
        const string foreignKeys = """
            - customer_id -> customers(id) ON DELETE CASCADE
            - shipping_address_id -> addresses(id) ON DELETE SET NULL
            """;
        string[] expectedColumns = ["id", "customer_id", "status", "total_amount", "shipping_address_id", "notes"];

        return new BenchmarkTestCase
        {
            Name = "Complex Table SQL (with FK)",
            Type = "SQL",
            SystemPrompt = SqlPrompts.SystemPrompt,
            UserPrompt = SqlPrompts.GenerateCreateTablePrompt(tableName, fieldsDescription, foreignKeys),
            SyntaxValidator = output => QualityEvaluator.EvaluateSqlSyntax(output),
            CompletenessValidator = output => QualityEvaluator.EvaluateSqlCompleteness(output, tableName, expectedColumns),
            AccuracyValidator = output => QualityEvaluator.EvaluateSqlAccuracy(output, tableName)
        };
    }

    #endregion

    #region Filter Test Cases

    public static IEnumerable<BenchmarkTestCase> GetFilterTestCases()
    {
        yield return CreateFilterExtractionTestCase();
    }

    private static BenchmarkTestCase CreateFilterExtractionTestCase()
    {
        const string fieldsDescription = """
            - id: UUID (Primary Key)
            - email: VARCHAR(255) (Unique)
            - name: VARCHAR(100)
            - status: VARCHAR(50) (Enum: active, inactive, pending)
            - created_at: TIMESTAMP WITH TIME ZONE
            - is_verified: BOOLEAN
            - department_id: UUID (Foreign Key)
            """;

        return new BenchmarkTestCase
        {
            Name = "Extract Filters",
            Type = "Filter",
            SystemPrompt = YamlPrompts.SystemPrompt,
            UserPrompt = YamlPrompts.ExtractFiltersPrompt(fieldsDescription),
            SyntaxValidator = output =>
            {
                var score = 0;
                if (output.Contains("field:")) score += 30;
                if (output.Contains("operator:")) score += 30;
                if (output.Contains("contains") || output.Contains("equals")) score += 20;
                if (output.Contains("-")) score += 20; // YAML list
                return Math.Min(100, score);
            },
            CompletenessValidator = output =>
            {
                var score = 0;
                if (output.ToLower().Contains("email")) score += 20;
                if (output.ToLower().Contains("name")) score += 20;
                if (output.ToLower().Contains("status")) score += 20;
                if (output.ToLower().Contains("created_at")) score += 20;
                if (output.ToLower().Contains("department_id")) score += 20;
                return Math.Min(100, score);
            },
            AccuracyValidator = output =>
            {
                var score = 0;
                // Correct operators for field types
                if (output.Contains("contains")) score += 25; // For strings
                if (output.Contains("equals")) score += 25;   // For booleans/enums/FKs
                if (output.Contains("gte") || output.Contains("lte") || output.Contains("between")) score += 25; // For dates
                if (!output.ToLower().Contains("password")) score += 25; // Should exclude sensitive fields
                return Math.Min(100, score);
            }
        };
    }

    #endregion

    #region Validation Test Cases

    public static IEnumerable<BenchmarkTestCase> GetValidationTestCases()
    {
        yield return CreateValidationExtractionTestCase();
    }

    private static BenchmarkTestCase CreateValidationExtractionTestCase()
    {
        const string fieldsDescription = """
            - id: UUID (Primary Key, auto-generated)
            - email: VARCHAR(255), Not Null, Unique
            - first_name: VARCHAR(100), Not Null
            - age: INTEGER, Nullable, Min: 0, Max: 150
            - website: TEXT, Nullable (URL format)
            - cpf: VARCHAR(14), Unique (CPF format)
            - balance: NUMERIC(10,2), Default: 0
            """;

        return new BenchmarkTestCase
        {
            Name = "Extract Validations",
            Type = "Validation",
            SystemPrompt = YamlPrompts.SystemPrompt,
            UserPrompt = YamlPrompts.ExtractValidationsPrompt(fieldsDescription),
            SyntaxValidator = output =>
            {
                var score = 0;
                if (output.Contains("name:")) score += 25;
                if (output.Contains("type:")) score += 25;
                if (output.Contains("required:")) score += 25;
                if (output.Contains("-")) score += 25; // YAML list
                return Math.Min(100, score);
            },
            CompletenessValidator = output =>
            {
                var score = 0;
                if (output.ToLower().Contains("email")) score += 20;
                if (output.ToLower().Contains("first_name")) score += 20;
                if (output.ToLower().Contains("age")) score += 20;
                if (output.ToLower().Contains("cpf")) score += 20;
                // Should NOT include auto-generated id or timestamps
                if (!output.Contains("id:") || !output.Contains("required: true")) score += 20;
                return Math.Min(100, score);
            },
            AccuracyValidator = output =>
            {
                var score = 0;
                // Correct validations
                if (output.Contains("max: 255") || output.Contains("max: 100")) score += 20;
                if (output.Contains("pattern") && output.ToLower().Contains("email")) score += 20;
                if (output.Contains("min:") && output.Contains("max:")) score += 20; // For age
                if (output.Contains("type: string") || output.Contains("type: integer") || output.Contains("type: decimal")) score += 20;
                if (output.Contains("required: true")) score += 20;
                return Math.Min(100, score);
            }
        };
    }

    #endregion
}
