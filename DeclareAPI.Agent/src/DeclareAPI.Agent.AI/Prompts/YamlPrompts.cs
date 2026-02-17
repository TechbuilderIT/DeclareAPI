namespace DeclareAPI.Agent.AI.Prompts;

/// <summary>
/// Prompts for generating DeclareAPI YAML configuration.
/// </summary>
public static class YamlPrompts
{
    public const string SystemPrompt = """
        You are an expert API designer specializing in REST APIs.
        Your task is to generate DeclareAPI YAML configuration for a given entity.

        DeclareAPI is a .NET 8 library that creates REST APIs from YAML configuration.

        Rules:
        1. Generate standard CRUD endpoints: list, get, create, update, delete
        2. Use snake_case for field names and paths
        3. Use appropriate HTTP methods: GET, POST, PUT, DELETE
        4. Add pagination to list endpoints
        5. Add appropriate filters based on field types
        6. Add field validations based on constraints
        7. Use 'table' as source type for simple CRUD

        Output format: YAML only, no explanations.
        """;

    public static string GenerateEntityEndpointsPrompt(
        string entityName,
        string tableName,
        string fieldsDescription,
        string basePath = "/api")
    {
        return $$"""
            Generate DeclareAPI YAML configuration for the entity "{{entityName}}".

            Table: {{tableName}}
            Base Path: {{basePath}}

            Fields:
            {{fieldsDescription}}

            Generate YAML for these endpoints:
            1. list: GET {{basePath}}/{{tableName}} - paginated list with filters
            2. get: GET {{basePath}}/{{tableName}}/{id} - get by ID
            3. create: POST {{basePath}}/{{tableName}} - create new record
            4. update: PUT {{basePath}}/{{tableName}}/{id} - update record
            5. delete: DELETE {{basePath}}/{{tableName}}/{id} - delete record

            Example output format:
            ```yaml
            {{entityName}}:
              description: "Description here"
              endpoints:
                list:
                  method: GET
                  path: /{{tableName}}
                  source:
                    type: table
                    name: {{tableName}}
                  filters:
                    - { field: name, operator: contains }
                  paginated: true
                get:
                  method: GET
                  path: /{{tableName}}/{id}
                  source:
                    type: table
                    name: {{tableName}}
                  params:
                    - { name: id, type: uuid, from: route }
                # ... more endpoints
            ```

            Generate the complete YAML now:
            """;
    }

    public static string GenerateFullConfigPrompt(
        string projectName,
        string entitiesYaml,
        string databaseProvider = "postgresql")
    {
        return $$"""
            Combine the following entity configurations into a complete DeclareAPI configuration file.

            Project: {{projectName}}
            Database: {{databaseProvider}}

            Entities YAML:
            {{entitiesYaml}}

            Generate a complete declareapi.yaml with:
            1. version: "1.0"
            2. database section with provider and connection placeholder
            3. settings section with base_path, default_page_size, max_page_size
            4. entities section combining all provided entities

            Output the complete YAML file:
            """;
    }

    public static string ExtractFiltersPrompt(string fieldsDescription)
    {
        return $$"""
            Based on these fields, suggest appropriate filters for a list endpoint:

            {{fieldsDescription}}

            Rules:
            - String fields: use 'contains' or 'equals' operators
            - Date fields: use 'between', 'gte', 'lte' operators
            - Boolean fields: use 'equals' operator
            - Enum/Status fields: use 'in' or 'equals' operators
            - Foreign keys (ending with _id): use 'equals' operator
            - Never filter on sensitive fields (password, hash, token)

            Output format: YAML array of filter objects
            Example:
            ```yaml
            - { field: email, operator: contains }
            - { field: created_at, operator: gte }
            - { field: is_active, operator: equals }
            ```

            Generate filters:
            """;
    }

    public static string ExtractValidationsPrompt(string fieldsDescription)
    {
        return $$"""
            Based on these fields, suggest appropriate validations for create/update endpoints:

            {{fieldsDescription}}

            Rules:
            - Required fields (NOT NULL, no default): required: true
            - String with max length: max: <length>
            - Email fields: pattern: email
            - UUID fields: type: uuid
            - Integer fields: type: integer, optional min/max
            - Decimal fields: type: decimal
            - Boolean fields: type: boolean
            - Date fields: type: date or type: datetime
            - CPF fields: pattern for CPF validation
            - Never include auto-generated fields (PK with default, created_at, updated_at)

            Output format: YAML array of field objects
            Example:
            ```yaml
            - { name: email, type: string, required: true, max: 255, pattern: email }
            - { name: first_name, type: string, required: true, max: 100 }
            - { name: amount, type: decimal, required: true, min: 0 }
            ```

            Generate field validations:
            """;
    }
}
