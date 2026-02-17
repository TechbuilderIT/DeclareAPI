# DeclareAPI

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Tests](https://img.shields.io/badge/Tests-85%20passing-brightgreen)]()

**Crie APIs REST a partir de configuracao YAML com escape hatches para C#.**

DeclareAPI e uma biblioteca .NET 8 que permite criar APIs REST completas a partir de configuracao declarativa em YAML. Comece com 40 linhas de YAML, escale com C# — sem reescrever nada.

[Read in English](README.md)

## Funcionalidades

- **Configuracao Declarativa**: Defina endpoints, filtros, validacao e mais em YAML
- **Database-First**: Mapeamento direto para views, stored procedures e functions
- **Validacao Automatica**: Regras FluentValidation geradas a partir da config
- **OpenAPI/Swagger**: Documentacao gerada automaticamente
- **Custom Handlers**: Escape para C# quando precisar de logica imperativa
- **Autorizacao**: Suporte integrado para policies e roles
- **Rate Limiting**: Limitacao de taxa por endpoint com janela fixa
- **Output Caching**: Cache configuravel com opcoes de vary-by
- **Observabilidade**: Correlation IDs, logging estruturado, health checks

## Inicio Rapido

### 1. Instale o Pacote

```bash
dotnet add package Techbuilder.DeclareAPI
dotnet add package Techbuilder.DeclareAPI.Dapper  # Para PostgreSQL/Dapper
```

### 2. Crie o Arquivo de Configuracao

Crie `declareapi.yaml` na raiz do projeto:

```yaml
version: "1.0"

database:
  provider: postgresql
  connection: "${DB_CONNECTION}"

settings:
  base_path: /api
  default_page_size: 25

entities:
  Produto:
    endpoints:
      listar:
        method: GET
        path: /produtos
        source:
          type: view
          name: vw_produtos
        filters:
          - { field: nome, operator: contains }
          - { field: categoria, operator: equals }
        sort: [nome, preco, criado_em]
        paginated: true
        cache:
          duration: 300
          vary_by_query: true

      obter:
        method: GET
        path: /produtos/{id}
        source:
          type: view
          name: vw_produtos
        params:
          - { name: id, type: uuid, from: route }

      criar:
        method: POST
        path: /produtos
        source:
          type: function
          name: sp_criar_produto
        fields:
          - { name: nome, type: string, required: true, max: 200 }
          - { name: preco, type: decimal, required: true, min: 0 }
          - { name: categoria, type: string, required: true }
        returns: uuid
        authorize: true
        rate_limit:
          limit: 10
          window: 60
```

### 3. Configure Sua Aplicacao

```csharp
using Techbuilder.DeclareAPI.Dapper;
using Techbuilder.DeclareAPI.Extensions;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDeclareApi(options =>
{
    options.ConfigFile = "declareapi.yaml";
    options.UseDataAccess(sp => new DapperDataAccess(connectionString, DatabaseProvider.PostgreSQL));
    options.ScanHandlersFrom<Program>();  // Opcional: escaneia custom handlers

    // Todos habilitados por padrao
    options.EnableRequestLogging = true;
    options.EnableCorrelationId = true;
    options.EnableHealthChecks = true;
    options.EnableRateLimiting = true;
    options.EnableCaching = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// Adiciona middleware de observabilidade (correlation ID, logging)
app.UseDeclareApiObservability();

// Adiciona autenticacao/autorizacao se necessario
// app.UseAuthentication();
// app.UseAuthorization();

// Adiciona rate limiting e caching
app.UseDeclareApiPolicies();

// Mapeia todos os endpoints do YAML
app.MapDeclareApi();
app.MapDeclareApiHealthChecks();

app.Run();
```

### 4. Execute Sua API

```bash
dotnet run
```

Sua API esta disponivel em `http://localhost:5000/api/produtos` com:
- Paginacao (`?page=1&pageSize=10`)
- Filtragem (`?nome=widget&categoria=eletronicos`)
- Ordenacao (`?sort=preco&order=desc`)
- Documentacao OpenAPI em `/swagger`

## Referencia de Configuracao

### Configuracao de Endpoint

```yaml
endpoints:
  nome_endpoint:
    method: GET | POST | PUT | PATCH | DELETE
    path: /recurso/{id}
    source:
      type: view | table | function | procedure
      name: nome_objeto_banco

    # Parametros de Rota/Query
    params:
      - name: id
        type: uuid | int | string | date | datetime | decimal | bool
        from: route | query

    # Campos do Body (POST/PUT/PATCH)
    fields:
      - name: nome_campo
        type: string | int | decimal | date | datetime | bool | uuid
        required: true | false
        min: 0           # Para numeros: valor minimo; para strings: tamanho minimo
        max: 100         # Para numeros: valor maximo; para strings: tamanho maximo
        pattern: "regex" # Validacao regex para strings

    # Filtros de Query (endpoints GET)
    filters:
      - field: nome_coluna
        operator: equals | contains | starts_with | ends_with | gt | gte | lt | lte | between | in

    # Ordenacao
    sort: [coluna1, coluna2]  # Colunas permitidas para ordenacao

    # Paginacao
    paginated: true | false

    # Tipo de retorno para functions
    returns: uuid | int | string | object

    # Autorizacao
    authorize: true           # Requer autenticacao
    policy: "nome_policy"     # Policy de autorizacao nomeada
    roles:                    # Autorizacao baseada em roles
      - admin
      - gerente

    # Rate Limiting
    rate_limit:
      limit: 100              # Requisicoes permitidas
      window: 60              # Janela de tempo em segundos
      policy: "nome_custom"   # Opcional: policy nomeada

    # Cache
    cache:
      duration: 300           # Duracao do cache em segundos
      vary_by_query: true     # Variar cache por query string
      vary_by_user: false     # Variar cache por usuario autenticado
      vary_by_params:         # Variar por parametros especificos
        - page
        - pageSize

    # Custom Handler (escape para C#)
    handler: MeuHandlerCustomizado
```

## Custom Handlers

Quando precisar de logica imperativa, crie um custom handler:

```csharp
using Techbuilder.DeclareAPI.Core.Abstractions;

// Modelo de request
public record CriarPedidoRequest(
    Guid ClienteId,
    List<ItemPedido> Itens,
    string? Observacoes
);

// Modelo de response
public record CriarPedidoResponse(
    Guid PedidoId,
    decimal Total,
    string Status
);

// Implementacao do handler
[HandlerName("CriarPedido")]  // Nome usado na config YAML
public class CriarPedidoHandler : ICustomHandler<CriarPedidoRequest, CriarPedidoResponse>
{
    private readonly IPedidoService _pedidoService;
    private readonly ILogger<CriarPedidoHandler> _logger;

    public CriarPedidoHandler(IPedidoService pedidoService, ILogger<CriarPedidoHandler> logger)
    {
        _pedidoService = pedidoService;
        _logger = logger;
    }

    public async Task<CriarPedidoResponse> HandleAsync(
        CriarPedidoRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Criando pedido para cliente {ClienteId}", request.ClienteId);

        var pedido = await _pedidoService.CriarPedidoAsync(
            request.ClienteId,
            request.Itens,
            request.Observacoes,
            cancellationToken);

        return new CriarPedidoResponse(pedido.Id, pedido.Total, pedido.Status);
    }
}
```

Referencia no YAML:

```yaml
endpoints:
  criar_pedido:
    method: POST
    path: /pedidos
    handler: CriarPedido  # Corresponde a [HandlerName("CriarPedido")]
    authorize: true
```

## Suporte a Banco de Dados

### PostgreSQL com Dapper

```csharp
options.UseDataAccess(sp => new DapperDataAccess(
    connectionString,
    DatabaseProvider.PostgreSQL
));
```

### Data Access Customizado

Implemente `IDataAccess` para outros bancos de dados:

```csharp
public interface IDataAccess
{
    Task<IEnumerable<dynamic>> QueryAsync(string sql, object? parameters = null);
    Task<T?> QuerySingleAsync<T>(string sql, object? parameters = null);
    Task<T?> ExecuteScalarAsync<T>(string sql, object? parameters = null);
    Task<int> ExecuteAsync(string sql, object? parameters = null);
}
```

## Observabilidade

### Health Checks

```csharp
app.MapDeclareApiHealthChecks();
// GET /health       - Liveness probe
// GET /health/ready - Readiness probe (inclui verificacao do DB)
```

### Correlation IDs

Todas as requisicoes sao marcadas com header `X-Correlation-ID` para rastreamento distribuido.

### Logging Estruturado

Logging de request/response com tempo, status codes e correlation IDs.

## Estrutura do Projeto

```
Techbuilder.DeclareAPI/
+-- Techbuilder.DeclareAPI.Core/       # Interfaces, modelos, configuracao
|   +-- Abstractions/                   # IDataAccess, ICustomHandler
|   +-- Configuration/                  # Modelos de config, ConfigLoader
|   +-- Validation/                     # DynamicValidator
|   +-- Query/                          # FilterQueryBuilder
+-- Techbuilder.DeclareAPI/            # Biblioteca principal
|   +-- Routing/                        # RouteGenerator
|   +-- Handlers/                       # HandlerRegistry, HandlerInvoker
|   +-- RateLimiting/                   # Extensoes de rate limiting
|   +-- Caching/                        # Extensoes de caching
|   +-- Observability/                  # Middleware, health checks
|   +-- Extensions/                     # Registro de servicos
+-- Techbuilder.DeclareAPI.Dapper/     # Implementacao Dapper
+-- Techbuilder.DeclareAPI.Tests/      # Testes unitarios (85 testes)
```

## Executando o Sample

```bash
cd Techbuilder.DeclareAPI.Sample

# Inicia PostgreSQL
docker-compose up -d

# Executa a API
dotnet run

# Abre Swagger UI
open http://localhost:5000/swagger
```

## Filosofia de Design

1. **Biblioteca, nao plataforma** — Pacote NuGet no projeto do usuario
2. **Declarativo por padrao, imperativo por excecao** — 80% config, 20% C#
3. **Banco de dados como cidadao de primeira classe** — Referencia direta a views, SPs, functions
4. **Escape hatch sem atrito** — Adicione `handler:` para mudar para C#
5. **Agnosto de arquitetura** — Funciona com Minimal APIs, MVC, hexagonal

## Contribuindo

Contribuicoes sao bem-vindas! Sinta-se a vontade para enviar um Pull Request.

## Licenca

Este projeto esta licenciado sob a Licenca MIT - veja o arquivo [LICENSE](LICENSE) para detalhes.

## Agradecimentos

- Construido com [.NET 8](https://dotnet.microsoft.com/)
- Parse de YAML por [YamlDotNet](https://github.com/aaubry/YamlDotNet)
- Data access com [Dapper](https://github.com/DapperLib/Dapper)
- Validacao com [FluentValidation](https://fluentvalidation.net/)
