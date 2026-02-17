namespace DeclareAPI.Agent.AI.Prompts;

/// <summary>
/// AI prompts for code generation tasks.
/// </summary>
public static class GenerationPrompts
{
    /// <summary>
    /// System prompt for YAML filter suggestions.
    /// </summary>
    public const string YamlFilterSystemPrompt = """
        You are an expert API designer. Your task is to suggest appropriate filters for REST API list endpoints.

        Given an entity with its fields, suggest which fields should be filterable and with what operators.

        Available operators:
        - equals: Exact match (good for IDs, booleans, enums)
        - contains: Substring match (good for names, descriptions)
        - starts_with: Prefix match
        - ends_with: Suffix match
        - gt, gte, lt, lte: Numeric/date comparisons
        - between: Range queries (dates, numbers)
        - in: Multiple values (good for status, type fields)

        Rules:
        - Always filter on status/type fields with 'in' operator
        - Filter email/name fields with 'contains'
        - Filter date fields with 'gte' or 'between'
        - Filter foreign keys with 'equals'
        - Never filter on password, hash, token, or secret fields
        - Limit to 5-7 most useful filters

        Respond with ONLY a YAML list in this exact format:
        ```yaml
        filters:
          - { field: field_name, operator: operator_name }
        ```
        """;

    /// <summary>
    /// System prompt for SQL index suggestions.
    /// </summary>
    public const string SqlIndexSystemPrompt = """
        You are a PostgreSQL database expert. Your task is to suggest optimal indexes for tables.

        Given a table schema, suggest which indexes should be created for query performance.

        Index types:
        - B-tree (default): Good for equality, range queries
        - GIN: Good for JSONB, arrays, full-text search
        - GiST: Good for geometric data, ranges

        Rules:
        - Always index foreign keys
        - Index fields commonly used in WHERE clauses (status, type, email)
        - Consider composite indexes for common query patterns
        - created_at DESC is useful for "recent items" queries
        - Avoid over-indexing (each index slows writes)
        - Use partial indexes for filtered queries (e.g., WHERE is_active = true)

        Respond with ONLY SQL CREATE INDEX statements, one per line:
        ```sql
        CREATE INDEX idx_name ON table(column);
        ```
        """;

    /// <summary>
    /// System prompt for entity description generation.
    /// </summary>
    public const string EntityDescriptionSystemPrompt = """
        You are a technical writer. Generate a brief, clear description for a database entity/API resource.

        The description should:
        - Be 1-2 sentences
        - Explain the purpose of the entity
        - Be suitable for API documentation

        Respond with ONLY the description text, no quotes or formatting.
        """;

    /// <summary>
    /// Creates a user prompt for filter suggestion.
    /// </summary>
    public static string CreateFilterPrompt(string entityName, string tableName, IEnumerable<(string name, string type, bool isFk)> fields)
    {
        var fieldList = string.Join("\n", fields.Select(f =>
            $"  - {f.name}: {f.type}{(f.isFk ? " (foreign key)" : "")}"));

        return $"""
            Entity: {entityName}
            Table: {tableName}

            Fields:
            {fieldList}

            Suggest the most useful filters for a GET list endpoint.
            """;
    }

    /// <summary>
    /// Creates a user prompt for index suggestion.
    /// </summary>
    public static string CreateIndexPrompt(string tableName, IEnumerable<(string name, string type, bool isPk, bool isFk, bool isUnique)> columns)
    {
        var columnList = string.Join("\n", columns.Select(c =>
        {
            var flags = new List<string>();
            if (c.isPk) flags.Add("PK");
            if (c.isFk) flags.Add("FK");
            if (c.isUnique) flags.Add("UNIQUE");
            var flagStr = flags.Any() ? $" ({string.Join(", ", flags)})" : "";
            return $"  - {c.name}: {c.type}{flagStr}";
        }));

        return $"""
            Table: {tableName}

            Columns:
            {columnList}

            Suggest optimal indexes for this table. Focus on query performance.
            """;
    }

    /// <summary>
    /// Creates a user prompt for entity description.
    /// </summary>
    public static string CreateDescriptionPrompt(string entityName, IEnumerable<string> fieldNames)
    {
        return $"""
            Entity name: {entityName}
            Fields: {string.Join(", ", fieldNames)}

            Generate a brief description for this entity.
            """;
    }
}
