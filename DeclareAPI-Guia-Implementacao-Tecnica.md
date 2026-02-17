# DeclareAPI — Guia de Implementação Técnica

## Referência complementar para desenvolvimento com Claude Code

---

## 1. Bootstrap do Projeto

### Comandos iniciais

```bash
# Criar solution
mkdir declareapi && cd declareapi
dotnet new sln -n DeclareApi

# Projetos core
dotnet new classlib -n DeclareApi.Core -o src/DeclareApi.Core -f net8.0
dotnet new classlib -n DeclareApi -o src/DeclareApi -f net8.0
dotnet new classlib -n DeclareApi.Dapper -o src/DeclareApi.Dapper -f net8.0

# Testes
dotnet new xunit -n DeclareApi.Core.Tests -o tests/DeclareApi.Core.Tests -f net8.0
dotnet new xunit -n DeclareApi.Tests -o tests/DeclareApi.Tests -f net8.0
dotnet new xunit -n DeclareApi.Integration.Tests -o tests/DeclareApi.Integration.Tests -f net8.0

# Sample
dotnet new web -n BasicCrud -o samples/BasicCrud -f net8.0

# Adicionar tudo à solution
dotnet sln add src/DeclareApi.Core/DeclareApi.Core.csproj
dotnet sln add src/DeclareApi/DeclareApi.csproj
dotnet sln add src/DeclareApi.Dapper/DeclareApi.Dapper.csproj
dotnet sln add tests/DeclareApi.Core.Tests/DeclareApi.Core.Tests.csproj
dotnet sln add tests/DeclareApi.Tests/DeclareApi.Tests.csproj
dotnet sln add tests/DeclareApi.Integration.Tests/DeclareApi.Integration.Tests.csproj
dotnet sln add samples/BasicCrud/BasicCrud.csproj
```

### Dependências por projeto

```bash
# DeclareApi.Core — zero dependências externas pesadas
cd src/DeclareApi.Core
dotnet add package YamlDotNet
dotnet add package NJsonSchema

# DeclareApi — engine principal
cd ../DeclareApi
dotnet add reference ../DeclareApi.Core/DeclareApi.Core.csproj
dotnet add package Microsoft.AspNetCore.OpenApi
dotnet add package Swashbuckle.AspNetCore
dotnet add package FluentValidation
dotnet add package FluentValidation.DependencyInjectionExtensions

# DeclareApi.Dapper
cd ../DeclareApi.Dapper
dotnet add reference ../DeclareApi.Core/DeclareApi.Core.csproj
dotnet add package Dapper
dotnet add package Npgsql           # PostgreSQL
dotnet add package Microsoft.Data.SqlClient  # SQL Server (futuro)

# Sample
cd ../../samples/BasicCrud
dotnet add reference ../../src/DeclareApi/DeclareApi.csproj
dotnet add reference ../../src/DeclareApi.Dapper/DeclareApi.Dapper.csproj
```

---

## 2. Decisões Técnicas Importantes

### 2.1 Por que YamlDotNet e não um parser custom

YamlDotNet é a biblioteca YAML mais madura do ecossistema .NET (16k+ stars, ativamente mantida). Ela faz desserialização direta para POCOs C#, o que significa que o `ConfigLoader` é essencialmente:

```csharp
var yaml = File.ReadAllText(configPath);
var deserializer = new DeserializerBuilder()
    .WithNamingConvention(UnderscoredNamingConvention.Instance)
    .Build();
var config = deserializer.Deserialize<DeclareApiConfig>(yaml);
```

Não reinvente isso. O parsing é commodity — o valor está no que você faz com a config parseada.

### 2.2 Minimal APIs como base (não Controllers)

O RouteGenerator deve usar `app.MapGet()`, `app.MapPost()`, etc. diretamente. Razões:

- Menos boilerplate que controllers
- Mais fácil de gerar programaticamente (uma linha por endpoint vs uma classe por controller)
- Performance ligeiramente melhor (sem overhead de MVC pipeline)
- Alinhado com a direção da Microsoft para .NET 8+

O pattern fica assim no EndpointMapper:

```csharp
// Para cada endpoint declarativo na config, gera algo como:
app.MapGet("/api/patients", async (
    [FromQuery] string? name,
    [FromQuery] int page,
    [FromQuery] int pageSize,
    IDataAccess data) =>
{
    var parameters = new { name, page, pageSize };
    var result = await data.QueryPagedAsync<dynamic>(
        "vw_patients_active", page, pageSize, parameters);
    return Results.Ok(result);
})
.WithName("ListPatients")
.WithOpenApi();
```

A geração é em runtime via reflection/delegates — não é code generation. Isso simplifica enormemente a Fase 1. Source Generators podem vir depois como otimização.

### 2.3 Dapper + dynamic como default

Na Fase 1, não tente mapear para classes tipadas. Use `dynamic` ou `Dictionary<string, object>`:

```csharp
public async Task<IEnumerable<dynamic>> QueryAsync(string source, object? parameters = null)
{
    using var connection = new NpgsqlConnection(_connectionString);
    return await connection.QueryAsync(
        $"SELECT * FROM {source}", // ATENÇÃO: sanitizar isso
        parameters);
}
```

O mapeamento para tipos específicos é uma otimização que pode vir na Fase 2. Para MVP, retornar o JSON que o banco devolve é suficiente.

### 2.4 SQL Injection — ponto crítico

Como o framework gera SQL a partir da configuração, a sanitização é responsabilidade do engine, não do usuário. Regras:

- **Source names (views, SPs)** devem ser validados contra um whitelist no startup. O ConfigLoader deve verificar que os objetos referenciados existem no banco.
- **Parâmetros** sempre via parameterized queries do Dapper (nunca string interpolation).
- **Filtros dinâmicos** devem usar um query builder interno que gera cláusulas WHERE parametrizadas.

```csharp
// ERRADO - vulnerável a SQL injection
var sql = $"SELECT * FROM {source} WHERE name LIKE '%{filter}%'";

// CORRETO - parametrizado
var sql = $"SELECT * FROM \"{source}\" WHERE name LIKE @filter";
var parameters = new { filter = $"%{filterValue}%" };
```

O quoting de identificadores (aspas duplas para PostgreSQL, colchetes para SQL Server) deve ser abstraído por provider.

### 2.5 Hot Reload da configuração

Em modo desenvolvimento, use `FileSystemWatcher` para detectar mudanças no YAML e recarregar os endpoints. Isso acelera drasticamente o ciclo de desenvolvimento:

```csharp
if (environment.IsDevelopment())
{
    var watcher = new FileSystemWatcher(Path.GetDirectoryName(configPath)!)
    {
        Filter = Path.GetFileName(configPath),
        NotifyFilter = NotifyFilters.LastWrite
    };
    watcher.Changed += (s, e) => ReloadConfiguration();
    watcher.EnableRaisingEvents = true;
}
```

Em produção, a config é lida uma vez no startup e cacheada.

---

## 3. JSON Schema da Configuração

Crie o JSON Schema cedo — ele serve tanto para validação no runtime quanto para autocomplete em IDEs e validação por LLMs.

Arquivo: `schemas/declareapi-config.schema.json`

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "DeclareAPI Configuration",
  "type": "object",
  "required": ["version", "database", "entities"],
  "properties": {
    "version": {
      "type": "string",
      "enum": ["1.0"]
    },
    "database": {
      "type": "object",
      "required": ["provider", "connection"],
      "properties": {
        "provider": {
          "type": "string",
          "enum": ["postgresql", "sqlserver", "mysql", "sqlite"]
        },
        "connection": {
          "type": "string",
          "description": "Connection string. Supports ${ENV_VAR} syntax."
        }
      }
    },
    "settings": {
      "type": "object",
      "properties": {
        "base_path": { "type": "string", "default": "/api" },
        "default_page_size": { "type": "integer", "default": 25, "minimum": 1 },
        "max_page_size": { "type": "integer", "default": 100, "minimum": 1 },
        "generate_openapi": { "type": "boolean", "default": true }
      }
    },
    "entities": {
      "type": "object",
      "additionalProperties": {
        "$ref": "#/definitions/entity"
      }
    }
  },
  "definitions": {
    "entity": {
      "type": "object",
      "properties": {
        "description": { "type": "string" },
        "endpoints": {
          "type": "object",
          "additionalProperties": {
            "$ref": "#/definitions/endpoint"
          }
        }
      },
      "required": ["endpoints"]
    },
    "endpoint": {
      "type": "object",
      "required": ["method", "path"],
      "properties": {
        "method": {
          "type": "string",
          "enum": ["GET", "POST", "PUT", "PATCH", "DELETE"]
        },
        "path": { "type": "string" },
        "handler": {
          "type": "string",
          "description": "Fully qualified name of a C# class implementing ICustomHandler. When present, source/fields are ignored."
        },
        "source": {
          "type": "object",
          "required": ["type", "name"],
          "properties": {
            "type": {
              "type": "string",
              "enum": ["table", "view", "procedure", "function"]
            },
            "name": { "type": "string" }
          }
        },
        "fields": {
          "type": "array",
          "items": {
            "$ref": "#/definitions/field"
          }
        },
        "params": {
          "type": "array",
          "items": {
            "$ref": "#/definitions/param"
          }
        },
        "filters": {
          "type": "array",
          "items": {
            "$ref": "#/definitions/filter"
          }
        },
        "sort": {
          "type": "array",
          "items": { "type": "string" }
        },
        "paginated": { "type": "boolean", "default": false },
        "returns": { "type": "string" }
      }
    },
    "field": {
      "type": "object",
      "required": ["name", "type"],
      "properties": {
        "name": { "type": "string" },
        "type": {
          "type": "string",
          "enum": ["string", "int", "long", "decimal", "bool", "date", "datetime", "uuid", "json"]
        },
        "required": { "type": "boolean", "default": false },
        "max": { "type": "integer" },
        "min": { "type": "integer" },
        "pattern": { "type": "string", "description": "Regex pattern for validation" },
        "default": {}
      }
    },
    "param": {
      "type": "object",
      "required": ["name", "type", "from"],
      "properties": {
        "name": { "type": "string" },
        "type": { "type": "string" },
        "from": {
          "type": "string",
          "enum": ["route", "query", "header"]
        }
      }
    },
    "filter": {
      "type": "object",
      "required": ["field", "operator"],
      "properties": {
        "field": { "type": "string" },
        "operator": {
          "type": "string",
          "enum": ["equals", "contains", "starts_with", "ends_with", "between", "in", "gt", "gte", "lt", "lte"]
        }
      }
    }
  }
}
```

---

## 4. Script SQL para o Sample (PostgreSQL)

Use isso para ter dados reais na Fase 1:

```sql
-- Schema para o sample BasicCrud
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Tabelas
CREATE TABLE patients (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(200) NOT NULL,
    birth_date DATE NOT NULL,
    cpf VARCHAR(11) UNIQUE,
    status VARCHAR(20) DEFAULT 'active',
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);

CREATE TABLE doctors (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(200) NOT NULL,
    specialty VARCHAR(100),
    crm VARCHAR(20) UNIQUE NOT NULL
);

CREATE TABLE appointments (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    patient_id UUID REFERENCES patients(id),
    doctor_id UUID REFERENCES doctors(id),
    appointment_date TIMESTAMP NOT NULL,
    notes TEXT,
    status VARCHAR(20) DEFAULT 'scheduled',
    created_at TIMESTAMP DEFAULT NOW()
);

-- Views (o que o DeclareAPI vai consumir)
CREATE VIEW vw_patients_active AS
SELECT id, name, birth_date, cpf, status, created_at
FROM patients
WHERE status = 'active'
ORDER BY name;

CREATE VIEW vw_patient_detail AS
SELECT p.id, p.name, p.birth_date, p.cpf, p.status,
       p.created_at, p.updated_at,
       COUNT(a.id) as total_appointments,
       MAX(a.appointment_date) as last_appointment
FROM patients p
LEFT JOIN appointments a ON a.patient_id = p.id
GROUP BY p.id;

CREATE VIEW vw_appointments AS
SELECT a.id, a.appointment_date, a.notes, a.status,
       a.patient_id, p.name as patient_name,
       a.doctor_id, d.name as doctor_name, d.specialty
FROM appointments a
JOIN patients p ON p.id = a.patient_id
JOIN doctors d ON d.id = a.doctor_id;

-- Stored Procedures
CREATE OR REPLACE FUNCTION sp_create_patient(
    p_name VARCHAR(200),
    p_birth_date DATE,
    p_cpf VARCHAR(11),
    p_guardian_id UUID DEFAULT NULL
)
RETURNS UUID AS $$
DECLARE
    new_id UUID;
BEGIN
    INSERT INTO patients (name, birth_date, cpf)
    VALUES (p_name, p_birth_date, p_cpf)
    RETURNING id INTO new_id;
    
    RETURN new_id;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION sp_update_patient(
    p_id UUID,
    p_name VARCHAR(200) DEFAULT NULL,
    p_guardian_id UUID DEFAULT NULL
)
RETURNS VOID AS $$
BEGIN
    UPDATE patients
    SET name = COALESCE(p_name, name),
        updated_at = NOW()
    WHERE id = p_id;
    
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Patient % not found', p_id;
    END IF;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION sp_delete_patient(p_id UUID)
RETURNS VOID AS $$
BEGIN
    UPDATE patients SET status = 'inactive', updated_at = NOW()
    WHERE id = p_id;
END;
$$ LANGUAGE plpgsql;

-- Seed data
INSERT INTO doctors (name, specialty, crm) VALUES
('Dra. Larissa Silva', 'Neuropediatria', 'CRM-CE 12345'),
('Dr. Carlos Mendes', 'Pediatria', 'CRM-CE 67890');

INSERT INTO patients (name, birth_date, cpf) VALUES
('João Pedro Silva', '2020-03-15', '12345678901'),
('Maria Clara Santos', '2019-07-22', '98765432100'),
('Lucas Oliveira', '2021-01-10', '45678912300');

INSERT INTO appointments (patient_id, doctor_id, appointment_date, notes) 
SELECT p.id, d.id, NOW() + INTERVAL '7 days', 'Consulta de rotina'
FROM patients p, doctors d
WHERE p.name = 'João Pedro Silva' AND d.crm = 'CRM-CE 12345';
```

---

## 5. Dicas para Desenvolvimento com Claude Code

### 5.1 Ordem de implementação sugerida para Fase 1

1. **Modelos de configuração** (`DeclareApiConfig.cs`, `EntityConfig.cs`, `EndpointConfig.cs`) — são POCOs simples que representam o YAML
2. **ConfigLoader** com teste unitário — parse YAML → modelo C#
3. **IDataAccess** interface + **DapperDataAccess** implementação básica (só `QueryAsync`)
4. **RouteGenerator** — o coração do framework, gera `app.MapGet()` a partir da config
5. **ServiceCollectionExtensions** e **EndpointRouteBuilderExtensions** — o `AddDeclareApi()` e `MapDeclareApi()`
6. **Sample funcional** — rodar e testar com curl/Postman

### 5.2 Prompt sugerido para Claude Code na Fase 1

```
Estou construindo o DeclareAPI, um framework .NET 8 que gera REST API endpoints 
a partir de configuração YAML. O arquivo declareapi.yaml define entidades com 
endpoints que mapeiam para views e stored procedures do PostgreSQL.

O projeto usa:
- YamlDotNet para parsing
- Dapper + Npgsql para data access
- Minimal APIs para geração de endpoints
- FluentValidation para validação

Leia o plano completo em docs/plan.md e o schema em schemas/declareapi-config.schema.json.
Implemente o ConfigLoader que lê declareapi.yaml e retorna um DeclareApiConfig tipado.
```

### 5.3 Armadilhas comuns que o Claude Code pode cometer

- **Gerar controllers MVC** em vez de Minimal API endpoints — force o uso de `app.MapGet/Post/Put/Delete`
- **Criar abstrações prematuras** — na Fase 1, classes concretas são suficientes. Interfaces vêm quando há mais de uma implementação
- **Usar EF Core por default** — o default é Dapper. EF Core é uma implementação alternativa futura
- **Over-engineering na validação** — FluentValidation gerado dinamicamente a partir da config é o suficiente. Não precisa de custom validation framework
- **Ignorar os testes** — peça testes unitários para cada componente. O ConfigLoader especialmente precisa de testes pois é a fundação

### 5.4 Docker Compose para desenvolvimento local

```yaml
# docker-compose.yml na raiz do projeto
version: '3.8'
services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: declareapi_sample
      POSTGRES_USER: dev
      POSTGRES_PASSWORD: dev123
    ports:
      - "5432:5432"
    volumes:
      - ./samples/BasicCrud/sql:/docker-entrypoint-initdb.d
      - pgdata:/var/lib/postgresql/data

volumes:
  pgdata:
```

### 5.5 Connection string para desenvolvimento

```
# .env ou appsettings.Development.json
DB_CONNECTION=Host=localhost;Port=5432;Database=declareapi_sample;Username=dev;Password=dev123
```

---

## 6. Referências Técnicas Rápidas

### Dapper — Chamando Views

```csharp
// SELECT simples de view
var patients = await connection.QueryAsync<dynamic>("SELECT * FROM vw_patients_active");

// Com filtro parametrizado
var patients = await connection.QueryAsync<dynamic>(
    "SELECT * FROM vw_patients_active WHERE name ILIKE @filter",
    new { filter = $"%{searchTerm}%" });

// Com paginação
var patients = await connection.QueryAsync<dynamic>(
    "SELECT * FROM vw_patients_active ORDER BY name LIMIT @limit OFFSET @offset",
    new { limit = pageSize, offset = (page - 1) * pageSize });
```

### Dapper — Chamando Stored Procedures/Functions (PostgreSQL)

```csharp
// PostgreSQL functions via SELECT
var newId = await connection.QuerySingleAsync<Guid>(
    "SELECT sp_create_patient(@p_name, @p_birth_date, @p_cpf)",
    new { p_name = "João", p_birth_date = new DateTime(2020, 1, 1), p_cpf = "12345678901" });

// Ou via CommandType.StoredProcedure (funciona para ambos PG e SQL Server)
var newId = await connection.QuerySingleAsync<Guid>(
    "sp_create_patient",
    new { p_name = "João", p_birth_date = new DateTime(2020, 1, 1), p_cpf = "12345678901" },
    commandType: CommandType.StoredProcedure);
```

### Minimal API — Geração dinâmica de endpoints

```csharp
// O pattern que o RouteGenerator vai usar internamente
var endpoint = app.MapGet("/api/patients", async (HttpContext ctx, IDataAccess data) =>
{
    // Extrair query parameters dinamicamente
    var filters = ExtractFilters(ctx.Request.Query, endpointConfig.Filters);
    var result = await data.QueryPagedAsync<dynamic>(
        endpointConfig.Source.Name, 
        page: int.Parse(ctx.Request.Query["page"].FirstOrDefault() ?? "1"),
        pageSize: int.Parse(ctx.Request.Query["pageSize"].FirstOrDefault() ?? "25"),
        filters);
    return Results.Ok(result);
});

// Metadados para OpenAPI
endpoint.WithName("ListPatients")
        .WithTags("Patient")
        .Produces<PagedResult<object>>(200)
        .WithOpenApi();
```

---

## 7. Checklist da Fase 1

- [ ] Solution criada com todos os projetos
- [ ] Dependências instaladas
- [ ] Docker Compose rodando PostgreSQL com seed data
- [ ] Modelos de configuração (POCOs)
- [ ] ConfigLoader com parsing YAML
- [ ] JSON Schema da configuração
- [ ] IDataAccess interface
- [ ] DapperDataAccess implementação (QueryAsync, QueryPagedAsync)
- [ ] RouteGenerator (GET list + GET by id)
- [ ] ServiceCollectionExtensions (AddDeclareApi)
- [ ] EndpointRouteBuilderExtensions (MapDeclareApi)
- [ ] Sample BasicCrud rodando com 2 endpoints
- [ ] Testes unitários para ConfigLoader
- [ ] Testes unitários para DapperDataAccess
- [ ] README.md básico do repositório
- [ ] .gitignore configurado
- [ ] Primeiro commit no repositório

---

*Use este guia como referência rápida durante o desenvolvimento. O plano estratégico completo está em DeclareAPI-Plano-de-Desenvolvimento.md*
