namespace DeclareAPI.Agent.AI.Prompts;

/// <summary>
/// Prompts for generating PostgreSQL DDL.
/// </summary>
public static class SqlPrompts
{
    public const string SystemPrompt = """
        You are a PostgreSQL database expert.
        Your task is to generate production-ready SQL DDL scripts.

        Rules:
        1. Use snake_case for table and column names
        2. Always use UUID for primary keys with gen_random_uuid() default
        3. Add created_at and updated_at timestamps
        4. Create appropriate indexes for foreign keys and frequently searched columns
        5. Add comments for tables and columns
        6. Use appropriate data types for PostgreSQL
        7. Order tables by dependencies (referenced tables first)

        Output format: SQL only, no explanations.
        """;

    public static string GenerateCreateTablePrompt(
        string tableName,
        string fieldsDescription,
        string? foreignKeys = null)
    {
        var fkSection = string.IsNullOrEmpty(foreignKeys)
            ? ""
            : $"\nForeign Keys:\n{foreignKeys}";

        return $"""
            Generate PostgreSQL CREATE TABLE statement for "{tableName}".

            Fields:
            {fieldsDescription}
            {fkSection}

            Requirements:
            1. Add UUID primary key if not specified
            2. Add created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() if not present
            3. Add updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() if not present
            4. Add NOT NULL for required fields
            5. Add UNIQUE constraints where specified
            6. Add DEFAULT values where appropriate

            Example output:
            ```sql
            CREATE TABLE {tableName} (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                name VARCHAR(100) NOT NULL,
                email VARCHAR(255) NOT NULL UNIQUE,
                is_active BOOLEAN DEFAULT true,
                created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
            );

            COMMENT ON TABLE {tableName} IS 'Description';
            COMMENT ON COLUMN {tableName}.name IS 'Field description';
            ```

            Generate the SQL:
            """;
    }

    public static string GenerateIndexesPrompt(string tableName, string fieldsDescription)
    {
        return $"""
            Generate PostgreSQL indexes for table "{tableName}".

            Fields:
            {fieldsDescription}

            Rules:
            1. Create indexes for all foreign key columns
            2. Create indexes for columns likely used in WHERE clauses (email, status, name)
            3. Create indexes for columns used in ORDER BY (created_at)
            4. Use UNIQUE index for unique constraints
            5. Consider partial indexes for boolean columns
            6. Don't create redundant indexes (PK is already indexed)

            Output format: SQL only
            Example:
            ```sql
            CREATE INDEX idx_{tableName}_user_id ON {tableName}(user_id);
            CREATE UNIQUE INDEX idx_{tableName}_email ON {tableName}(email);
            CREATE INDEX idx_{tableName}_created_at ON {tableName}(created_at DESC);
            ```

            Generate indexes:
            """;
    }

    public static string GenerateForeignKeysPrompt(
        string tableName,
        string foreignKeysDescription)
    {
        return $"""
            Generate PostgreSQL foreign key constraints for table "{tableName}".

            Foreign Keys:
            {foreignKeysDescription}

            Rules:
            1. Use meaningful constraint names: fk_{tableName}_<column>
            2. Reference the id column by default
            3. Use ON DELETE CASCADE for child tables
            4. Use ON DELETE SET NULL for optional relationships
            5. Use ON DELETE RESTRICT for required relationships

            Output format: SQL ALTER TABLE statements
            Example:
            ```sql
            ALTER TABLE {tableName}
                ADD CONSTRAINT fk_{tableName}_user_id
                FOREIGN KEY (user_id) REFERENCES users(id)
                ON DELETE CASCADE;
            ```

            Generate foreign keys:
            """;
    }

    public static string GenerateFullSchemaPrompt(
        string projectName,
        string entitiesDescription,
        string relationshipsDescription)
    {
        return $"""
            Generate a complete PostgreSQL schema for project "{projectName}".

            Entities:
            {entitiesDescription}

            Relationships:
            {relationshipsDescription}

            Generate a complete init.sql file with:
            1. Header comment with project name and generation date
            2. CREATE EXTENSION IF NOT EXISTS for uuid-ossp or pgcrypto
            3. DROP TABLE IF EXISTS statements in reverse dependency order
            4. CREATE TABLE statements in dependency order
            5. CREATE INDEX statements
            6. ALTER TABLE for foreign keys
            7. COMMENT ON statements

            Output the complete SQL file:
            """;
    }

    public static string TypeMappingReference => """
        PostgreSQL Type Mapping Reference:
        - uuid: UUID primary keys
        - varchar(n): Limited strings
        - text: Unlimited strings
        - integer: Whole numbers
        - bigint: Large numbers
        - numeric(p,s): Precise decimals
        - boolean: True/false
        - timestamp with time zone: Date/time with timezone
        - date: Date only
        - jsonb: JSON data (indexable)
        - text[]: Text arrays
        - inet: IP addresses
        """;
}
