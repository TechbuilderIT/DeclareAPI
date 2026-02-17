# Benchmark de Modelos Ollama para DeclareAPI.Agent

**Data**: 2026-02-17
**Hardware**: RTX 3050 6GB VRAM
**Objetivo**: Identificar o menor modelo capaz de gerar YAML e SQL de qualidade para o agente

---

## Resumo Executivo

| Modelo | Qualidade | Tokens/s | Tempo Médio | YAML | SQL | Recomendação |
|--------|-----------|----------|-------------|------|-----|--------------|
| **qwen2.5-coder:3b** | **96/100** | **63.7** | **5.2s** | **100** | **92** | **RECOMENDADO** |
| qwen2.5:7b | 97/100 | 19.5 | 16.4s | 100 | 94 | Melhor qualidade |
| mistral:7b | 95/100 | 23.7 | 12.2s | 100 | 90 | Alternativa |
| phi3:mini | 94/100 | 55.0 | 6.2s | 100 | 89 | Mais rápido |
| llama2:7b-chat | 93/100 | 14.0 | 30.8s | 98 | 88 | Não recomendado |

---

## Modelo Recomendado: qwen2.5-coder:3b

### Por que este modelo?

1. **Qualidade Excelente (96/100)**
   - Apenas 1 ponto abaixo do modelo 7B
   - YAML: 100/100 (perfeito)
   - SQL: 92/100 (muito bom)

2. **Velocidade Superior (63.7 tok/s)**
   - 3.3x mais rápido que qwen2.5:7b
   - 2.7x mais rápido que mistral:7b
   - Tempo médio de resposta: 5.2 segundos

3. **Otimizado para GPU de 6GB**
   - 3B parâmetros = cabe 100% na VRAM
   - Sem CPU offloading
   - Latência consistente

4. **Especializado em Código**
   - Treinado especificamente para geração de código
   - Melhor compreensão de YAML e SQL
   - Menos "alucinações" em estruturas técnicas

### Instalação

```bash
ollama pull qwen2.5-coder:3b
```

### Configuração no Agente

```csharp
// Em OllamaProvider ou configuração
var options = new AgentOptions
{
    Model = "qwen2.5-coder:3b",
    OllamaUrl = "http://localhost:11434"
};
```

---

## Análise Detalhada por Modelo

### qwen2.5-coder:3b (RECOMENDADO)

| Métrica | Valor |
|---------|-------|
| Tamanho | 1.9 GB |
| Parâmetros | 3B |
| First Token | 232ms |
| Total Time | 5184ms |
| Tokens/sec | 63.7 |
| YAML Score | 100/100 |
| SQL Score | 92/100 |
| Overall | 96/100 |

**Prós:**
- Melhor relação qualidade/velocidade
- Cabe inteiro na GPU
- Especializado em código

**Contras:**
- SQL ligeiramente inferior ao 7B (92 vs 94)

---

### qwen2.5:7b

| Métrica | Valor |
|---------|-------|
| Tamanho | ~4.5 GB |
| Parâmetros | 7B |
| First Token | 441ms |
| Total Time | 16374ms |
| Tokens/sec | 19.5 |
| YAML Score | 100/100 |
| SQL Score | 94/100 |
| Overall | 97/100 |

**Prós:**
- Melhor qualidade absoluta
- Excelente em SQL

**Contras:**
- 3x mais lento
- Requer CPU offloading em 6GB VRAM
- Latência inconsistente

---

### mistral:7b

| Métrica | Valor |
|---------|-------|
| Tamanho | ~4.1 GB |
| Parâmetros | 7B |
| First Token | 440ms |
| Total Time | 12190ms |
| Tokens/sec | 23.7 |
| YAML Score | 100/100 |
| SQL Score | 90/100 |
| Overall | 95/100 |

**Prós:**
- Bom modelo generalista
- Mais rápido que outros 7B

**Contras:**
- SQL inferior ao qwen2.5
- Ainda requer CPU offloading

---

### phi3:mini

| Métrica | Valor |
|---------|-------|
| Tamanho | ~2.2 GB |
| Parâmetros | 3.8B |
| First Token | 234ms |
| Total Time | 6245ms |
| Tokens/sec | 55.0 |
| YAML Score | 100/100 |
| SQL Score | 89/100 |
| Overall | 94/100 |

**Prós:**
- Muito rápido
- Cabe na GPU
- Bom para YAML

**Contras:**
- SQL inferior (89)
- Não especializado em código
- Qualidade inconsistente em testes complexos

---

## Metodologia do Benchmark

### Casos de Teste

1. **Simple Entity YAML** - Entidade User com campos básicos
2. **Complex Entity YAML** - Entidade Order com FKs e enums
3. **Simple Table SQL** - CREATE TABLE users
4. **Complex Table SQL** - CREATE TABLE orders com FKs

### Métricas Avaliadas

| Métrica | Descrição |
|---------|-----------|
| First Token | Tempo até primeiro token (latência) |
| Total Time | Tempo total de geração |
| Tokens/sec | Taxa de geração |
| Syntax Score | Output é YAML/SQL válido? (0-100) |
| Completeness Score | Contém todos os elementos? (0-100) |
| Accuracy Score | Valores estão corretos? (0-100) |
| Overall Score | Média dos 3 scores |

### Critérios de Qualidade YAML

- Estrutura de endpoints (list, get, create, update, delete)
- Métodos HTTP corretos (GET, POST, PUT, DELETE)
- Referências a tabelas corretas
- Configuração de paginação
- Filtros apropriados

### Critérios de Qualidade SQL

- Sintaxe PostgreSQL válida
- Tipos de dados corretos (UUID, VARCHAR, TIMESTAMP WITH TIME ZONE)
- Primary Keys e Foreign Keys
- Defaults corretos (gen_random_uuid(), NOW())
- Constraints (NOT NULL, UNIQUE)

---

## Conclusões

### Para RTX 3050 6GB VRAM

1. **Produção**: Use `qwen2.5-coder:3b`
   - Qualidade 96/100, velocidade 63.7 tok/s
   - Melhor custo-benefício

2. **Máxima Qualidade** (se tempo não importa): Use `qwen2.5:7b`
   - Qualidade 97/100, mas 3x mais lento

3. **Evitar**: `llama2:7b-chat`
   - Mais lento e menor qualidade

### Para GPUs com mais VRAM (8GB+)

Considere testar:
- `qwen2.5-coder:7b` - Versão maior do coder
- `deepseek-coder:6.7b` - Especializado em código
- `codellama:7b` - Meta's code model

---

## Comandos Úteis

```bash
# Listar modelos instalados
ollama list

# Instalar modelo recomendado
ollama pull qwen2.5-coder:3b

# Testar modelo
ollama run qwen2.5-coder:3b "Generate a CREATE TABLE for users"

# Verificar uso de VRAM (Windows)
nvidia-smi

# Executar benchmark novamente
cd DeclareAPI.Agent/src/DeclareAPI.Agent.Benchmark
dotnet run -- --auto --suite standard
```

---

## Histórico de Benchmarks

| Data | GPU | Modelo Recomendado | Score |
|------|-----|-------------------|-------|
| 2026-02-17 | RTX 3050 6GB | qwen2.5-coder:3b | 96/100 |

---

*Gerado por DeclareAPI.Agent.Benchmark*
