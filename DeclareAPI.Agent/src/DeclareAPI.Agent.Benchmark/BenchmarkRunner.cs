using System.Diagnostics;
using System.Text;
using DeclareAPI.Agent.Benchmark.Models;
using OllamaSharp;
using OllamaSharp.Models;
using Spectre.Console;

namespace DeclareAPI.Agent.Benchmark;

/// <summary>
/// Runs benchmarks against Ollama models.
/// </summary>
public class BenchmarkRunner
{
    private readonly OllamaApiClient _client;
    private readonly string _ollamaUrl;

    public BenchmarkRunner(string ollamaUrl = "http://localhost:11434")
    {
        _ollamaUrl = ollamaUrl;
        _client = new OllamaApiClient(new Uri(ollamaUrl));
    }

    /// <summary>
    /// Check if Ollama is available and list local models.
    /// </summary>
    public async Task<List<string>> GetAvailableModelsAsync()
    {
        try
        {
            var models = await _client.ListLocalModelsAsync();
            return models.Select(m => m.Name).ToList();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error connecting to Ollama: {ex.Message}[/]");
            return new List<string>();
        }
    }

    /// <summary>
    /// Run a single test case against a model.
    /// </summary>
    public async Task<BenchmarkResult> RunTestAsync(
        string modelName,
        BenchmarkTestCase testCase,
        CancellationToken cancellationToken = default)
    {
        var result = new BenchmarkResult
        {
            ModelName = modelName,
            TestName = testCase.Name,
            PromptType = testCase.Type
        };

        var stopwatch = Stopwatch.StartNew();
        var firstTokenReceived = false;
        var outputBuilder = new StringBuilder();
        var tokenCount = 0;

        try
        {
            // Combine system and user prompts
            var fullPrompt = $"""
                System: {testCase.SystemPrompt}

                User: {testCase.UserPrompt}

                Assistant:
                """;

            result.PromptTokens = EstimateTokens(fullPrompt);

            // Stream the response
            await foreach (var token in _client.GenerateAsync(new GenerateRequest
            {
                Model = modelName,
                Prompt = fullPrompt,
                Stream = true
            }, cancellationToken))
            {
                if (!firstTokenReceived && !string.IsNullOrEmpty(token?.Response))
                {
                    result.TimeToFirstToken = stopwatch.Elapsed;
                    firstTokenReceived = true;
                }

                if (!string.IsNullOrEmpty(token?.Response))
                {
                    outputBuilder.Append(token.Response);
                    tokenCount++;
                }
            }

            stopwatch.Stop();
            result.TotalTime = stopwatch.Elapsed;
            result.Output = outputBuilder.ToString();
            result.OutputTokens = tokenCount;
            result.TokensPerSecond = tokenCount / result.TotalTime.TotalSeconds;
            result.Success = true;

            // Evaluate quality
            EvaluateQuality(result, testCase);

            // Get memory usage
            result.MemoryUsedMb = GC.GetTotalMemory(false) / (1024 * 1024);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.TotalTime = stopwatch.Elapsed;
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Run all test cases against a model with progress display.
    /// </summary>
    public async Task<ModelBenchmarkSummary> RunAllTestsAsync(
        string modelName,
        IEnumerable<BenchmarkTestCase> testCases,
        CancellationToken cancellationToken = default)
    {
        var summary = new ModelBenchmarkSummary
        {
            ModelName = modelName
        };

        var testList = testCases.ToList();
        summary.TotalTests = testList.Count;

        await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask($"[cyan]Testing {modelName}[/]", maxValue: testList.Count);

                foreach (var testCase in testList)
                {
                    task.Description = $"[cyan]{modelName}[/] - {testCase.Name}";

                    var result = await RunTestAsync(modelName, testCase, cancellationToken);
                    summary.Results.Add(result);

                    if (result.Success)
                        summary.PassedTests++;
                    else
                        summary.FailedTests++;

                    task.Increment(1);
                }
            });

        // Calculate averages
        CalculateAverages(summary);

        return summary;
    }

    /// <summary>
    /// Run benchmark comparing multiple models.
    /// </summary>
    public async Task<List<ModelBenchmarkSummary>> CompareModelsAsync(
        IEnumerable<string> modelNames,
        IEnumerable<BenchmarkTestCase> testCases,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ModelBenchmarkSummary>();
        var testList = testCases.ToList();

        foreach (var modelName in modelNames)
        {
            AnsiConsole.MarkupLine($"\n[bold yellow]Testing model: {modelName}[/]");

            // Warm up the model
            await WarmUpModelAsync(modelName, cancellationToken);

            var summary = await RunAllTestsAsync(modelName, testList, cancellationToken);
            results.Add(summary);

            // Display intermediate results
            DisplayModelSummary(summary);
        }

        return results;
    }

    private async Task WarmUpModelAsync(string modelName, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine($"[grey]Warming up {modelName}...[/]");

        try
        {
            await foreach (var _ in _client.GenerateAsync(new GenerateRequest
            {
                Model = modelName,
                Prompt = "Say hello",
                Stream = false
            }, cancellationToken))
            {
                // Just consume the response
            }
        }
        catch
        {
            // Ignore warm-up errors
        }
    }

    private void EvaluateQuality(BenchmarkResult result, BenchmarkTestCase testCase)
    {
        result.SyntaxScore = testCase.SyntaxValidator?.Invoke(result.Output) ?? 0;
        result.CompletenessScore = testCase.CompletenessValidator?.Invoke(result.Output) ?? 0;
        result.AccuracyScore = testCase.AccuracyValidator?.Invoke(result.Output) ?? 0;
    }

    private static void CalculateAverages(ModelBenchmarkSummary summary)
    {
        if (!summary.Results.Any()) return;

        var successfulResults = summary.Results.Where(r => r.Success).ToList();
        if (!successfulResults.Any()) return;

        summary.AvgTimeToFirstTokenMs = successfulResults.Average(r => r.TimeToFirstToken.TotalMilliseconds);
        summary.AvgTotalTimeMs = successfulResults.Average(r => r.TotalTime.TotalMilliseconds);
        summary.AvgTokensPerSecond = successfulResults.Average(r => r.TokensPerSecond);
        summary.AvgQualityScore = successfulResults.Average(r => r.OverallScore);

        // Group by task type
        foreach (var group in successfulResults.GroupBy(r => r.PromptType))
        {
            summary.AvgScoreByTaskType[group.Key] = group.Average(r => r.OverallScore);
            summary.AvgTimeByTaskType[group.Key] = group.Average(r => r.TotalTime.TotalMilliseconds);
        }
    }

    private static int EstimateTokens(string text)
    {
        // Rough estimate: ~4 characters per token
        return text.Length / 4;
    }

    public static void DisplayModelSummary(ModelBenchmarkSummary summary)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Metric")
            .AddColumn("Value");

        table.AddRow("Model", $"[bold]{summary.ModelName}[/]");
        table.AddRow("Tests Passed", $"[green]{summary.PassedTests}[/] / {summary.TotalTests}");
        table.AddRow("Avg First Token", $"{summary.AvgTimeToFirstTokenMs:F0} ms");
        table.AddRow("Avg Total Time", $"{summary.AvgTotalTimeMs:F0} ms");
        table.AddRow("Avg Tokens/sec", $"{summary.AvgTokensPerSecond:F1}");
        table.AddRow("Avg Quality Score", GetScoreMarkup(summary.AvgQualityScore));

        AnsiConsole.Write(table);

        // Score by task type
        if (summary.AvgScoreByTaskType.Any())
        {
            var taskTable = new Table()
                .Border(TableBorder.Simple)
                .AddColumn("Task Type")
                .AddColumn("Score")
                .AddColumn("Avg Time");

            foreach (var (taskType, score) in summary.AvgScoreByTaskType)
            {
                var time = summary.AvgTimeByTaskType.GetValueOrDefault(taskType);
                taskTable.AddRow(taskType, GetScoreMarkup(score), $"{time:F0} ms");
            }

            AnsiConsole.Write(taskTable);
        }
    }

    public static void DisplayComparisonTable(List<ModelBenchmarkSummary> summaries)
    {
        AnsiConsole.MarkupLine("\n[bold underline]Model Comparison Summary[/]\n");

        var table = new Table()
            .Border(TableBorder.Double)
            .AddColumn("Model")
            .AddColumn("Pass Rate")
            .AddColumn("First Token")
            .AddColumn("Total Time")
            .AddColumn("Tokens/sec")
            .AddColumn("Quality");

        foreach (var summary in summaries.OrderByDescending(s => s.AvgQualityScore))
        {
            var passRate = summary.TotalTests > 0
                ? (double)summary.PassedTests / summary.TotalTests * 100
                : 0;

            table.AddRow(
                summary.ModelName,
                $"{passRate:F0}%",
                $"{summary.AvgTimeToFirstTokenMs:F0} ms",
                $"{summary.AvgTotalTimeMs:F0} ms",
                $"{summary.AvgTokensPerSecond:F1}",
                GetScoreMarkup(summary.AvgQualityScore)
            );
        }

        AnsiConsole.Write(table);

        // Recommendation
        var best = summaries
            .Where(s => s.PassedTests > 0)
            .OrderByDescending(s => s.AvgQualityScore)
            .ThenBy(s => s.AvgTotalTimeMs)
            .FirstOrDefault();

        if (best != null)
        {
            AnsiConsole.MarkupLine($"\n[bold green]Recommended model: {best.ModelName}[/]");
            AnsiConsole.MarkupLine($"  Quality: {best.AvgQualityScore:F0}/100, Speed: {best.AvgTokensPerSecond:F1} tok/s");
        }
    }

    private static string GetScoreMarkup(double score)
    {
        return score switch
        {
            >= 80 => $"[bold green]{score:F0}[/]",
            >= 60 => $"[yellow]{score:F0}[/]",
            >= 40 => $"[orange3]{score:F0}[/]",
            _ => $"[red]{score:F0}[/]"
        };
    }
}
