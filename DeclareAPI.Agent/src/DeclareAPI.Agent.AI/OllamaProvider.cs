using System.Text;
using DeclareAPI.Agent.Core.Abstractions;
using OllamaSharp;
using OllamaSharp.Models;

namespace DeclareAPI.Agent.AI;

/// <summary>
/// AI provider implementation for Ollama (local LLM server).
/// </summary>
public class OllamaProvider : IAIProvider
{
    private readonly OllamaApiClient _client;
    private readonly TimeSpan _timeout;
    private readonly int _maxRetries;

    public string ProviderName => "Ollama";

    public OllamaProvider(string baseUrl = "http://localhost:11434", int timeoutSeconds = 120, int maxRetries = 3)
    {
        var uri = new Uri(baseUrl);
        _client = new OllamaApiClient(uri);
        _timeout = TimeSpan.FromSeconds(timeoutSeconds);
        _maxRetries = maxRetries;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var models = await _client.ListLocalModelsAsync(cts.Token);
            return models.Any();
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<AIModel>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        var models = await _client.ListLocalModelsAsync(cancellationToken);

        return models.Select(m => new AIModel
        {
            Name = m.Name,
            Description = $"Modified: {m.ModifiedAt:yyyy-MM-dd}",
            SizeBytes = m.Size
        }).ToList();
    }

    public async Task<string> GenerateAsync(string prompt, string model, CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async ct =>
        {
            var response = new StringBuilder();

            await foreach (var token in _client.GenerateAsync(new GenerateRequest
            {
                Model = model,
                Prompt = prompt,
                Stream = false
            }, ct))
            {
                if (token?.Response != null)
                {
                    response.Append(token.Response);
                }
            }

            return response.ToString();
        }, cancellationToken);
    }

    public async Task<string> GenerateWithContextAsync(
        string systemPrompt,
        string userPrompt,
        string model,
        CancellationToken cancellationToken = default)
    {
        // Combine system and user prompts for generate API
        var combinedPrompt = $"""
            System: {systemPrompt}

            User: {userPrompt}

            Assistant:
            """;

        return await GenerateAsync(combinedPrompt, model, cancellationToken);
    }

    public async Task<string> GenerateStreamingAsync(
        string prompt,
        string model,
        Action<string> onToken,
        CancellationToken cancellationToken = default)
    {
        var response = new StringBuilder();

        await foreach (var token in _client.GenerateAsync(new GenerateRequest
        {
            Model = model,
            Prompt = prompt,
            Stream = true
        }, cancellationToken))
        {
            if (!string.IsNullOrEmpty(token?.Response))
            {
                response.Append(token.Response);
                onToken(token.Response);
            }
        }

        return response.ToString();
    }

    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        var attempt = 0;
        Exception? lastException = null;

        while (attempt < _maxRetries)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_timeout);

                return await operation(cts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout occurred
                lastException = new TimeoutException($"Operation timed out after {_timeout.TotalSeconds} seconds");
                attempt++;
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                attempt++;

                if (attempt < _maxRetries)
                {
                    // Exponential backoff
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
                }
            }
        }

        throw new InvalidOperationException(
            $"Operation failed after {_maxRetries} attempts",
            lastException);
    }
}
