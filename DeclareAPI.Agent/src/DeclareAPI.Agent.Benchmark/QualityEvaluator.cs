using System.Text.RegularExpressions;

namespace DeclareAPI.Agent.Benchmark;

/// <summary>
/// Evaluates quality of generated YAML and SQL outputs.
/// </summary>
public static partial class QualityEvaluator
{
    #region YAML Validation

    public static int EvaluateYamlSyntax(string output)
    {
        if (string.IsNullOrWhiteSpace(output)) return 0;

        var score = 0;
        var cleanOutput = ExtractCodeBlock(output, "yaml");

        // Basic structure checks
        if (cleanOutput.Contains(':')) score += 20;
        if (YamlIndentationPattern().IsMatch(cleanOutput)) score += 20;
        if (!cleanOutput.Contains('{') || cleanOutput.Contains("{ ")) score += 10; // Proper YAML, not JSON

        // No obvious errors
        if (!cleanOutput.Contains("```")) score += 10; // Code blocks cleaned
        if (!cleanOutput.Contains("undefined")) score += 10;
        if (!cleanOutput.Contains("null:")) score += 10;

        // Valid YAML markers
        if (cleanOutput.Contains("method:")) score += 10;
        if (cleanOutput.Contains("path:")) score += 10;

        return Math.Min(100, score);
    }

    public static int EvaluateYamlCompleteness(string output, string entityName)
    {
        if (string.IsNullOrWhiteSpace(output)) return 0;

        var score = 0;
        var cleanOutput = ExtractCodeBlock(output, "yaml").ToLowerInvariant();
        var entityLower = entityName.ToLowerInvariant();

        // Required endpoints
        if (cleanOutput.Contains("list:")) score += 15;
        if (cleanOutput.Contains("get:")) score += 15;
        if (cleanOutput.Contains("create:")) score += 15;
        if (cleanOutput.Contains("update:")) score += 15;
        if (cleanOutput.Contains("delete:")) score += 15;

        // Required properties
        if (cleanOutput.Contains("method:")) score += 5;
        if (cleanOutput.Contains("path:")) score += 5;
        if (cleanOutput.Contains("source:")) score += 5;
        if (cleanOutput.Contains(entityLower)) score += 5;
        if (cleanOutput.Contains("paginated:")) score += 5;

        return Math.Min(100, score);
    }

    public static int EvaluateYamlAccuracy(string output, string tableName)
    {
        if (string.IsNullOrWhiteSpace(output)) return 0;

        var score = 0;
        var cleanOutput = ExtractCodeBlock(output, "yaml");
        var tableNameLower = tableName.ToLowerInvariant();

        // Correct HTTP methods
        if (Regex.IsMatch(cleanOutput, @"list:.*?method:\s*GET", RegexOptions.Singleline | RegexOptions.IgnoreCase)) score += 15;
        if (Regex.IsMatch(cleanOutput, @"get:.*?method:\s*GET", RegexOptions.Singleline | RegexOptions.IgnoreCase)) score += 15;
        if (Regex.IsMatch(cleanOutput, @"create:.*?method:\s*POST", RegexOptions.Singleline | RegexOptions.IgnoreCase)) score += 15;
        if (Regex.IsMatch(cleanOutput, @"update:.*?method:\s*PUT", RegexOptions.Singleline | RegexOptions.IgnoreCase)) score += 15;
        if (Regex.IsMatch(cleanOutput, @"delete:.*?method:\s*DELETE", RegexOptions.Singleline | RegexOptions.IgnoreCase)) score += 15;

        // Correct table reference
        if (cleanOutput.ToLowerInvariant().Contains($"name: {tableNameLower}")) score += 15;

        // Correct path format
        if (cleanOutput.Contains($"/{tableNameLower}")) score += 10;

        return Math.Min(100, score);
    }

    #endregion

    #region SQL Validation

    public static int EvaluateSqlSyntax(string output)
    {
        if (string.IsNullOrWhiteSpace(output)) return 0;

        var score = 0;
        var cleanOutput = ExtractCodeBlock(output, "sql").ToUpperInvariant();

        // Basic SQL keywords
        if (cleanOutput.Contains("CREATE TABLE")) score += 20;
        if (cleanOutput.Contains("PRIMARY KEY")) score += 15;
        if (SqlColumnPattern().IsMatch(cleanOutput)) score += 15;

        // Proper syntax
        if (cleanOutput.Contains(';')) score += 10;
        if (cleanOutput.Contains('(') && cleanOutput.Contains(')')) score += 10;
        if (BalancedParentheses(cleanOutput)) score += 10;

        // PostgreSQL specific
        if (cleanOutput.Contains("UUID") || cleanOutput.Contains("VARCHAR") || cleanOutput.Contains("TIMESTAMP")) score += 10;
        if (cleanOutput.Contains("DEFAULT")) score += 10;

        return Math.Min(100, score);
    }

    public static int EvaluateSqlCompleteness(string output, string tableName, string[] expectedColumns)
    {
        if (string.IsNullOrWhiteSpace(output)) return 0;

        var score = 0;
        var cleanOutput = ExtractCodeBlock(output, "sql").ToUpperInvariant();
        var tableUpper = tableName.ToUpperInvariant();

        // Table creation
        if (cleanOutput.Contains($"CREATE TABLE {tableUpper}")) score += 20;

        // Expected columns
        var columnsFound = 0;
        foreach (var col in expectedColumns)
        {
            if (cleanOutput.Contains(col.ToUpperInvariant()))
                columnsFound++;
        }
        score += (int)(40.0 * columnsFound / expectedColumns.Length);

        // Timestamps
        if (cleanOutput.Contains("CREATED_AT")) score += 10;
        if (cleanOutput.Contains("UPDATED_AT")) score += 10;

        // Indexes
        if (cleanOutput.Contains("CREATE INDEX") || cleanOutput.Contains("CREATE UNIQUE INDEX")) score += 10;

        // Comments
        if (cleanOutput.Contains("COMMENT ON")) score += 10;

        return Math.Min(100, score);
    }

    public static int EvaluateSqlAccuracy(string output, string tableName)
    {
        if (string.IsNullOrWhiteSpace(output)) return 0;

        var score = 0;
        var cleanOutput = ExtractCodeBlock(output, "sql").ToUpperInvariant();

        // Correct PostgreSQL types
        if (cleanOutput.Contains("UUID")) score += 15;
        if (cleanOutput.Contains("TIMESTAMP WITH TIME ZONE") || cleanOutput.Contains("TIMESTAMPTZ")) score += 15;
        if (cleanOutput.Contains("VARCHAR(")) score += 10;

        // Correct defaults
        if (cleanOutput.Contains("GEN_RANDOM_UUID()") || cleanOutput.Contains("UUID_GENERATE_V4()")) score += 15;
        if (cleanOutput.Contains("DEFAULT NOW()") || cleanOutput.Contains("DEFAULT CURRENT_TIMESTAMP")) score += 10;

        // Constraints
        if (cleanOutput.Contains("NOT NULL")) score += 10;
        if (cleanOutput.Contains("UNIQUE")) score += 10;

        // Foreign keys format
        if (cleanOutput.Contains("FOREIGN KEY") || cleanOutput.Contains("REFERENCES")) score += 15;

        return Math.Min(100, score);
    }

    #endregion

    #region Helpers

    private static string ExtractCodeBlock(string output, string language)
    {
        // Try to extract code from markdown code blocks
        var pattern = $@"```{language}\s*([\s\S]*?)```";
        var match = Regex.Match(output, pattern, RegexOptions.IgnoreCase);

        if (match.Success)
            return match.Groups[1].Value.Trim();

        // Try generic code block
        pattern = @"```\s*([\s\S]*?)```";
        match = Regex.Match(output, pattern);

        if (match.Success)
            return match.Groups[1].Value.Trim();

        // Return as-is
        return output.Trim();
    }

    private static bool BalancedParentheses(string text)
    {
        var count = 0;
        foreach (var c in text)
        {
            if (c == '(') count++;
            else if (c == ')') count--;
            if (count < 0) return false;
        }
        return count == 0;
    }

    [GeneratedRegex(@"^\s{2,}", RegexOptions.Multiline)]
    private static partial Regex YamlIndentationPattern();

    [GeneratedRegex(@"\w+\s+\w+", RegexOptions.IgnoreCase)]
    private static partial Regex SqlColumnPattern();

    #endregion
}
