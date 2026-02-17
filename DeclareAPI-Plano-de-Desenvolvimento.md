# DeclareAPI — Plano de Desenvolvimento

## Framework Declarativo para APIs .NET com Evolução Gradual

**Versão:** 1.0
**Data:** 16 de Fevereiro de 2026
**Autor:** Caio de Souza e Silva / Claude (Anthropic)

---

## 1. Visão do Projeto

**DeclareAPI** (nome provisório) é uma biblioteca .NET open source que permite criar APIs REST completas a partir de configuração declarativa YAML/JSON, com escape hatch natural para código imperativo C#.

**Proposta de valor em uma frase:** *"Comece com 40 linhas de YAML, escale com C# — sem reescrever nada."*

### Princípios Fundamentais

1. **Biblioteca, não plataforma.** Vive dentro do projeto do desenvolvedor como um NuGet package. Sem processos externos, sem containers separados, sem vendor lock-in.
2. **Declarativo por padrão, imperativo por exceção.** 80% das operações são definidas em configuração. Os 20% restantes são código C# normal.
3. **Banco como cidadão de primeira classe.** Referência direta a views, stored procedures e functions do banco. Sem ORM obrigatório.
4. **Escape hatch sem fricção.** Substituir qualquer endpoint declarativo por código imperativo é uma mudança de uma linha na configuração.
5. **Agnóstico de arquitetura.** Funciona com Minimal APIs, MVC, hexagonal. Funciona com Dapper, EF Core, ADO.NET.
6. **OpenAPI automático.** A configuração declarativa gera documentação Swagger/OpenAPI como subproduto gratuito.

---

## 2. Arquitetura Core

### 2.1 Diagrama Conceitual

```
┌─────────────────────────────────────────────────────┐
│                   Projeto .NET do Usuário            │
│                                                      │
│  ┌─────────────┐    ┌────────────────────────────┐  │
│  │ YAML/JSON   │───▶│      DeclareAPI Engine      │  │
│  │ Config      │    │                              │  │
│  └─────────────┘    │  ┌────────┐  ┌───────────┐  │  │
│                     │  │ Route  │  │ Validation│  │  │
│  ┌─────────────┐    │  │ Gen    │  │ Engine    │  │  │
│  │ Custom C#   │───▶│  ├────────┤  ├───────────┤  │  │
│  │ Handlers    │    │  │ OpenAPI│  │ Data      │  │  │
│  └─────────────┘    │  │ Gen    │  │ Access    │  │  │
│                     │  └────────┘  └───────────┘  │  │
│                     └──────────┬───────────────────┘  │
│                                │                      │
│                     ┌──────────▼───────────────────┐  │
│                     │   IDataAccess Abstraction     │  │
│                     │  ┌────────┐ ┌──────┐ ┌─────┐ │  │
│                     │  │ Dapper │ │EFCore│ │ADO  │ │  │
│                     │  └────────┘ └──────┘ └─────┘ │  │
│                     └──────────────────────────────┘  │
│                                                      │
└──────────────────────────────────────────────────────┘
                         │
              ┌──────────▼──────────┐
              │   PostgreSQL /      │
              │   SQL Server /      │
              │   MySQL / SQLite    │
              │                     │
              │  Views │ SPs │ Fns  │
              └─────────────────────┘
```

### 2.2 Componentes Principais

**ConfigLoader** — Lê e valida o arquivo YAML/JSON de configuração contra um JSON Schema. Suporta hot-reload em desenvolvimento.

**RouteGenerator** — Transforma a configuração em Minimal API endpoints em runtime. Cada entidade declarada vira um conjunto de rotas REST.

**ValidationEngine** — Gera validações automaticamente a partir das constraints declaradas (required, max length, regex, etc). Usa FluentValidation internamente.

**DataAccessAbstraction** — Interface `IDataAccess` com implementações para Dapper (padrão), EF Core e ADO.NET puro. O usuário escolhe ou cria a sua.

**OpenAPIGenerator** — Gera o documento OpenAPI/Swagger a partir da configuração declarativa. Sem precisar de controllers ou anotações.

**HandlerResolver** — Detecta quando um endpoint tem um handler C# custom registrado e roteia para ele em vez do pipeline declarativo automático.

**PipelineMiddleware** — Pipeline de request configurável: autenticação → autorização → validação → handler (declarativo ou custom) → serialização → response.

### 2.3 Interface Core de Data Access

```csharp
public interface IDataAccess
{
    // Leitura
    Task<T?> QuerySingleAsync<T>(string source, object? parameters = null);
    Task<IEnumerable<T>> QueryAsync<T>(string source, object? parameters = null);
    Task<PagedResult<T>> QueryPagedAsync<T>(string source, int page, int pageSize, 
                                             object? parameters = null);
    
    // Escrita
    Task<int> ExecuteAsync(string source, object? parameters = null);
    Task<T?> ExecuteScalarAsync<T>(string source, object? parameters = null);
}

public interface ICustomHandler<TRequest, TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, IDataAccess data, 
                                 CancellationToken ct);
}
```

---

## 3. Formato da Configuração Declarativa

### 3.1 Estrutura do Arquivo YAML

```yaml
# declareapi.yaml
version: "1.0"

database:
  provider: postgresql          # postgresql | sqlserver | mysql | sqlite
  connection: "${DB_CONNECTION}" # suporte a env vars

settings:
  base_path: /api
  default_page_size: 25
  max_page_size: 100
  generate_openapi: true

entities:

  Patient:
    description: "Pacientes do sistema"
    
    endpoints:
      list:
        method: GET
        path: /patients
        source:
          type: view                  # view | table | function
          name: vw_patients_active
        filters:
          - { field: name, operator: contains }
          - { field: birth_date, operator: between }
          - { field: status, operator: equals }
        sort: [name, birth_date, created_at]
        paginated: true

      get:
        method: GET
        path: /patients/{id}
        source:
          type: view
          name: vw_patient_detail
        params:
          - { name: id, type: uuid, from: route }

      create:
        method: POST
        path: /patients
        source:
          type: procedure
          name: sp_create_patient
        fields:
          - { name: name, type: string, required: true, max: 200 }
          - { name: birth_date, type: date, required: true }
          - { name: cpf, type: string, required: true, pattern: "^\\d{11}$" }
          - { name: guardian_id, type: uuid, required: false }
        returns: uuid   # retorna o ID criado

      update:
        method: PUT
        path: /patients/{id}
        source:
          type: procedure
          name: sp_update_patient
        params:
          - { name: id, type: uuid, from: route }
        fields:
          - { name: name, type: string, max: 200 }
          - { name: guardian_id, type: uuid }

      delete:
        method: DELETE
        path: /patients/{id}
        source:
          type: procedure
          name: sp_delete_patient
        params:
          - { name: id, type: uuid, from: route }

      # Endpoint com handler custom (escape hatch)
      transfer:
        method: POST
        path: /patients/{id}/transfer
        handler: PatientTransferHandler   # ← aponta para classe C#

  Appointment:
    endpoints:
      list:
        method: GET
        path: /appointments
        source:
          type: view
          name: vw_appointments
        filters:
          - { field: patient_id, operator: equals }
          - { field: date, operator: between }
          - { field: doctor_id, operator: equals }
        paginated: true
      
      create:
        method: POST
        path: /appointments
        source:
          type: procedure
          name: sp_create_appointment
        fields:
          - { name: patient_id, type: uuid, required: true }
          - { name: doctor_id, type: uuid, required: true }
          - { name: date, type: datetime, required: true }
          - { name: notes, type: string, max: 2000 }
```

### 3.2 Exemplo do Handler Custom (Escape Hatch)

```csharp
// Quando o declarativo não basta, escreva C# normal
public class PatientTransferHandler : ICustomHandler<PatientTransferRequest, TransferResult>
{
    private readonly IDataAccess _data;
    private readonly IEmailService _email;

    public PatientTransferHandler(IDataAccess data, IEmailService email)
    {
        _data = data;
        _email = email;
    }

    public async Task<TransferResult> HandleAsync(
        PatientTransferRequest request, 
        IDataAccess data, 
        CancellationToken ct)
    {
        // Lógica que precisa ir além do banco
        var result = await data.ExecuteScalarAsync<Guid>(
            "sp_transfer_patient", 
            new { request.PatientId, request.NewDoctorId });

        await _email.SendAsync(
            to: request.NewDoctorEmail,
            subject: "Novo paciente transferido",
            body: $"Paciente {request.PatientName} foi transferido.");

        return new TransferResult { Success = true, TransferId = result };
    }
}
```

### 3.3 Setup no Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

// Uma linha para registrar o DeclareAPI
builder.Services.AddDeclareApi(options =>
{
    options.ConfigFile = "declareapi.yaml";
    options.UseDataAccess<DapperDataAccess>();     // ou EfCoreDataAccess
    options.ScanHandlersFrom(typeof(Program));     // detecta handlers custom
});

var app = builder.Build();

// Uma linha para mapear todos os endpoints
app.MapDeclareApi();

app.Run();
```

---

## 4. Roadmap de Desenvolvimento

### Fase 1 — Foundation (Semanas 1-3)

**Objetivo:** Provar que o conceito funciona end-to-end com um caso mínimo.

**Entregáveis:**
- Projeto .NET 8 com estrutura de solution (src/, tests/, samples/)
- ConfigLoader: parser de YAML com validação via JSON Schema
- IDataAccess com implementação Dapper para PostgreSQL
- RouteGenerator: gerar endpoints GET (list + get by id) a partir da config
- Um sample funcional: definir uma entidade em YAML, rodar a API, fazer GET e receber JSON
- Testes unitários para ConfigLoader e DataAccess

**Critérios de sucesso:** Um arquivo YAML de 20 linhas produz uma API funcional com 2 endpoints que retornam dados de uma view do PostgreSQL.

### Fase 2 — CRUD Completo (Semanas 4-6)

**Objetivo:** Cobrir todas as operações CRUD declarativamente.

**Entregáveis:**
- Suporte a POST, PUT, DELETE mapeando para stored procedures
- ValidationEngine: validações automáticas a partir dos fields da config
- Suporte a filtros (contains, equals, between, in) e paginação automática
- Suporte a sort dinâmico
- Geração automática de OpenAPI/Swagger
- Sample completo com 3-4 entidades cobrindo CRUD

**Critérios de sucesso:** Uma API CRUD completa rodando com 5 entidades, Swagger funcional, sem uma linha de C# além do Program.cs.

### Fase 3 — Escape Hatch e Flexibilidade (Semanas 7-9)

**Objetivo:** Permitir a transição gradual de declarativo para imperativo.

**Entregáveis:**
- HandlerResolver: detectar e rotear para handlers C# custom
- Interface ICustomHandler<TRequest, TResponse> com DI completa
- Suporte a middleware custom por endpoint (autenticação, logging, etc)
- Implementação EF Core do IDataAccess como alternativa ao Dapper
- Suporte a SQL Server como segundo provider de banco
- Hot-reload da configuração em modo desenvolvimento

**Critérios de sucesso:** Um mesmo projeto tem 80% de endpoints declarativos e 20% com handlers custom, coexistindo sem conflito.

### Fase 4 — Produção Ready (Semanas 10-13)

**Objetivo:** Preparar para uso real e publicação open source.

**Entregáveis:**
- Suporte a autenticação JWT e autorização por role na config
- Health checks integrados
- Logging estruturado (Serilog-compatible)
- Error handling padronizado (Problem Details RFC 7807)
- Cache declarativo por endpoint (in-memory, com extensão para Redis)
- CLI tool: `declareapi init`, `declareapi validate`, `declareapi generate-schema`
- Documentação completa: README, Getting Started, API Reference
- Publicação no NuGet como pacote público
- GitHub repository com CI/CD (GitHub Actions)
- Sample project: mini-SaaS com autenticação, 10+ entidades, mix declarativo/imperativo

**Critérios de sucesso:** Um desenvolvedor externo consegue instalar o NuGet, seguir o Getting Started, e ter uma API rodando em menos de 15 minutos.

### Fase 5 — Integração NeuralSeam (Semanas 14-16)

**Objetivo:** O NeuralSeam gera projetos usando DeclareAPI como target.

**Entregáveis:**
- JSON Schema publicado do formato de configuração (para validação por LLMs)
- Template de projeto para geração automática
- Fluxwing Skill específica para DeclareAPI
- Documentação de integração com agentes de IA
- Exemplos de prompts otimizados para geração de config YAML

**Critérios de sucesso:** O NeuralSeam recebe uma descrição de domínio em linguagem natural, gera o YAML de configuração, o schema SQL (views + stored procedures), e produz uma API funcional sem intervenção humana.

---

## 5. Stack Técnica

| Componente | Tecnologia | Justificativa |
|---|---|---|
| Runtime | .NET 8+ (LTS) | Estabilidade, Minimal APIs, Source Generators |
| Config Format | YAML (primário), JSON (alternativo) | YAML é mais legível para humanos; JSON para máquinas |
| YAML Parser | YamlDotNet | Biblioteca madura e bem mantida no ecossistema .NET |
| Config Validation | JSON Schema (NJsonSchema) | Validação determinística do formato da configuração |
| Default Data Access | Dapper | Performance, simplicidade, suporte nativo a SPs e views |
| Alternativa Data Access | EF Core | Para times que já usam EF Core |
| Validação | FluentValidation | Standard da indústria, extensível |
| OpenAPI | Swashbuckle ou NSwag | Geração de documentação |
| Serialização | System.Text.Json | Performance nativa, sem dependência extra |
| Testes | xUnit + FluentAssertions | Standard .NET |
| CI/CD | GitHub Actions | Integração nativa com GitHub |
| Package | NuGet | Canal de distribuição padrão .NET |

---

## 6. Estrutura do Repositório

```
declareapi/
├── src/
│   ├── DeclareApi.Core/              # Interfaces, modelos, ConfigLoader
│   │   ├── Configuration/
│   │   │   ├── DeclareApiConfig.cs
│   │   │   ├── EntityConfig.cs
│   │   │   ├── EndpointConfig.cs
│   │   │   └── ConfigLoader.cs
│   │   ├── Abstractions/
│   │   │   ├── IDataAccess.cs
│   │   │   ├── ICustomHandler.cs
│   │   │   └── IHandlerResolver.cs
│   │   ├── Validation/
│   │   │   └── ValidationBuilder.cs
│   │   └── Models/
│   │       ├── PagedResult.cs
│   │       └── ApiError.cs
│   │
│   ├── DeclareApi/                   # Engine principal (NuGet package)
│   │   ├── Routing/
│   │   │   ├── RouteGenerator.cs
│   │   │   └── EndpointMapper.cs
│   │   ├── Pipeline/
│   │   │   ├── RequestPipeline.cs
│   │   │   └── ResponseBuilder.cs
│   │   ├── OpenApi/
│   │   │   └── OpenApiGenerator.cs
│   │   ├── Extensions/
│   │   │   ├── ServiceCollectionExtensions.cs
│   │   │   └── EndpointRouteBuilderExtensions.cs
│   │   └── DeclareApi.csproj
│   │
│   ├── DeclareApi.Dapper/            # Implementação Dapper
│   │   ├── DapperDataAccess.cs
│   │   └── DeclareApi.Dapper.csproj
│   │
│   ├── DeclareApi.EfCore/            # Implementação EF Core
│   │   ├── EfCoreDataAccess.cs
│   │   └── DeclareApi.EfCore.csproj
│   │
│   └── DeclareApi.Cli/               # CLI tool
│       ├── Commands/
│       └── DeclareApi.Cli.csproj
│
├── tests/
│   ├── DeclareApi.Core.Tests/
│   ├── DeclareApi.Tests/
│   ├── DeclareApi.Dapper.Tests/
│   └── DeclareApi.Integration.Tests/
│
├── samples/
│   ├── BasicCrud/                    # Exemplo mínimo
│   ├── HealthcareApi/                # Exemplo completo (domínio saúde)
│   └── HybridApi/                    # Mix declarativo + imperativo
│
├── docs/
│   ├── getting-started.md
│   ├── configuration-reference.md
│   ├── custom-handlers.md
│   ├── migration-guide.md            # "Validei meu MVP, e agora?"
│   └── ai-integration.md             # Guia para uso com LLMs
│
├── schemas/
│   └── declareapi-config.schema.json  # JSON Schema da configuração
│
├── .github/
│   └── workflows/
│       ├── ci.yml
│       └── publish.yml
│
├── README.md
├── LICENSE (MIT)
├── CONTRIBUTING.md
└── declareapi.sln
```

---

## 7. Estratégia de Evolução (O Guia "Validei, e agora?")

Este é o diferencial competitivo do framework. O desenvolvedor segue um caminho claro:

### Nível 1 — MVP Puro (100% declarativo)
Tudo está na configuração YAML. O banco faz o trabalho pesado via views e stored procedures. Zero código C# além do setup.

### Nível 2 — Lógica Pontual (90% declarativo, 10% custom)
Alguns endpoints precisam de lógica além do banco (enviar e-mail, chamar API externa). Adiciona-se handlers custom apenas para esses endpoints. O resto continua declarativo.

### Nível 3 — Complexidade Crescente (60% declarativo, 40% custom)
O negócio cresceu. Mais regras, mais integrações. Os handlers custom aumentam gradualmente. A configuração declarativa continua servindo para os endpoints simples de CRUD.

### Nível 4 — Migração de Banco
Precisa trocar de PostgreSQL para SQL Server (ou vice-versa). Muda-se o provider na config e reescrevem-se as views/stored procedures. Os endpoints, handlers custom e consumidores da API não mudam nada.

### Nível 5 — Arquitetura Madura
O projeto pode evoluir para hexagonal, CQRS, microserviços. O DeclareAPI continua servindo os endpoints simples. Os endpoints complexos já estão em C# e podem ser reorganizados livremente. A transição é gradual, nunca big bang.

---

## 8. Diferenciais vs Concorrência

| Característica | DeclareAPI | DAB (Microsoft) | Supabase | Hasura | EasyData |
|---|---|---|---|---|---|
| Vive dentro do projeto | ✅ | ❌ (sidecar) | ❌ (plataforma) | ❌ (serviço) | ✅ |
| Escape hatch C# nativo | ✅ | ❌ | ❌ (Edge Fns) | ❌ (webhooks) | ❌ |
| Views + Stored Procedures | ✅ | ✅ | Parcial | Parcial | ❌ |
| Agnóstico de ORM | ✅ | N/A | N/A | N/A | ❌ (EF Core) |
| Config declarativa | ✅ (YAML) | ✅ (JSON) | Schema SQL | Schema SQL | DbContext |
| OpenAPI automático | ✅ | ✅ | ✅ | ❌ (GraphQL) | ❌ |
| Evolução gradual | ✅ | ❌ | ❌ | ❌ | ❌ |
| Otimizado para LLMs | ✅ | ❌ | ❌ | ❌ | ❌ |
| Multi-banco | ✅ | ✅ | ❌ (só PG) | Parcial | Parcial |

---

## 9. Riscos e Mitigações

**Risco: O YAML fica complexo demais e vira outra linguagem.**
Mitigação: Limitar o vocabulário declarativo ao essencial (CRUD + filtros + validação). Qualquer coisa além disso vai pro handler C# custom. Resistir à tentação de adicionar condicionais, loops ou lógica no YAML.

**Risco: Performance do runtime parsing da configuração.**
Mitigação: Cachear a configuração parseada no startup. Futuramente, implementar Source Generators para gerar código em compile-time (Fase 5+).

**Risco: Adoção limitada por não suportar GraphQL.**
Mitigação: REST-first é uma decisão consciente. GraphQL pode ser adicionado como extensão futura sem impactar o core. A maioria dos MVPs não precisa de GraphQL.

**Risco: Competição com DAB da Microsoft.**
Mitigação: Posicionamento claramente diferente — biblioteca embeddable vs sidecar. O DeclareAPI resolve um problema que o DAB explicitamente não resolve: coexistência de código declarativo e imperativo no mesmo processo.

---

## 10. Métricas de Sucesso

**Fase 1 (MVP):** Prova de conceito funcional em 3 semanas.
**Fase 4 (NuGet):** Publicação do pacote em até 13 semanas.
**6 meses:** 100+ stars no GitHub, 5+ issues de contribuidores externos.
**12 meses:** Primeiro projeto Veridia rodando 100% sobre DeclareAPI.
**18 meses:** Integração completa com NeuralSeam para geração automática de projetos.

---

*Este documento é vivo e deve ser atualizado a cada fase concluída.*
