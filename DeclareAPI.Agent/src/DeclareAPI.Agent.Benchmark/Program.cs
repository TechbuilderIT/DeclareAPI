using DeclareAPI.Agent.Benchmark;
using DeclareAPI.Agent.Benchmark.Models;
using Spectre.Console;

// ============================================================================
// DeclareAPI.Agent Model Benchmark
// Compares Ollama models for code generation quality and speed
// ============================================================================
// Usage:
//   dotnet run                           # Interactive mode
//   dotnet run -- --auto                 # Auto mode: all models, quick suite
//   dotnet run -- --auto --suite full    # Auto mode with full suite
//   dotnet run -- --models "model1,model2" --suite quick
// ============================================================================

// Parse command line arguments
var autoMode = args.Contains("--auto") || args.Contains("-a");
var modelsArg = GetArgValue(args, "--models") ?? GetArgValue(args, "-m");
var suiteArg = GetArgValue(args, "--suite") ?? GetArgValue(args, "-s") ?? "quick";

AnsiConsole.Write(new FigletText("Model Benchmark")
    .Color(Color.Cyan1));

AnsiConsole.MarkupLine("[grey]DeclareAPI.Agent - Ollama Model Comparison[/]\n");

// Check hardware info
DisplayHardwareInfo();

var runner = new BenchmarkRunner();

// Check Ollama connection
AnsiConsole.MarkupLine("[yellow]Connecting to Ollama...[/]");
var availableModels = await runner.GetAvailableModelsAsync();

if (!availableModels.Any())
{
    AnsiConsole.MarkupLine("[red]No models found. Please ensure Ollama is running and has models installed.[/]");
    AnsiConsole.MarkupLine("\n[grey]To install recommended models for RTX 3050 6GB:[/]");
    AnsiConsole.MarkupLine("  [cyan]ollama pull qwen2.5-coder:3b[/]   (Best for 6GB VRAM)");
    AnsiConsole.MarkupLine("  [cyan]ollama pull phi3:mini[/]          (3.8B, fast)");
    AnsiConsole.MarkupLine("  [cyan]ollama pull codegemma:2b[/]       (2B, very fast)");
    AnsiConsole.MarkupLine("  [cyan]ollama pull deepseek-coder:1.3b[/] (1.3B, fastest)");
    return;
}

AnsiConsole.MarkupLine($"[green]Found {availableModels.Count} models[/]");
foreach (var model in availableModels)
{
    AnsiConsole.MarkupLine($"  - {model}");
}
AnsiConsole.WriteLine();

// Filter to coding models and small models suitable for 6GB VRAM
var recommendedPatterns = new[]
{
    "qwen2.5-coder", "qwen2.5:3b", "qwen2:1.5b",
    "phi3", "phi",
    "codegemma", "gemma:2b",
    "deepseek-coder", "deepseek:1.3b",
    "codellama:7b", "codellama:code",
    "starcoder", "starcoder2:3b",
    "stable-code", "granite-code"
};

var codingModels = availableModels
    .Where(m => recommendedPatterns.Any(p => m.ToLower().Contains(p.ToLower())))
    .OrderBy(m => m)
    .ToList();

// Also include any small models (by name pattern)
var smallModels = availableModels
    .Where(m => m.Contains(":1") || m.Contains(":2") || m.Contains(":3") ||
                m.Contains("mini") || m.Contains("tiny") || m.Contains("small"))
    .Where(m => !codingModels.Contains(m))
    .ToList();

var suggestedModels = codingModels.Concat(smallModels).Distinct().ToList();

if (!suggestedModels.Any())
{
    suggestedModels = availableModels.Take(5).ToList();
}

// Select models (auto or interactive)
List<string> selectedModels;

if (autoMode || !Environment.UserInteractive)
{
    // Auto mode: use specified models or all available
    if (!string.IsNullOrEmpty(modelsArg))
    {
        selectedModels = modelsArg.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(m => m.Trim())
            .Where(m => availableModels.Any(a => a.Equals(m, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (!selectedModels.Any())
        {
            AnsiConsole.MarkupLine($"[yellow]No matching models found for: {modelsArg}[/]");
            AnsiConsole.MarkupLine("[yellow]Using all available models instead.[/]");
            selectedModels = availableModels;
        }
    }
    else
    {
        // Use suggested models if available, otherwise all
        selectedModels = suggestedModels.Any() ? suggestedModels : availableModels;
    }

    AnsiConsole.MarkupLine($"[cyan]Auto mode: testing {selectedModels.Count} models[/]");
}
else
{
    // Interactive mode
    selectedModels = AnsiConsole.Prompt(
        new MultiSelectionPrompt<string>()
            .Title("Select models to benchmark:")
            .PageSize(15)
            .Required()
            .MoreChoicesText("[grey](Use arrows to navigate, space to select)[/]")
            .InstructionsText("[grey](Press space to toggle, enter to confirm)[/]")
            .AddChoiceGroup("[green]Recommended for 6GB VRAM[/]", suggestedModels)
            .AddChoiceGroup("[yellow]Other available models[/]",
                availableModels.Except(suggestedModels).ToList()));
}

if (!selectedModels.Any())
{
    AnsiConsole.MarkupLine("[red]No models selected.[/]");
    return;
}

// Select test suite (auto or interactive)
string testSuite;

if (autoMode || !Environment.UserInteractive)
{
    testSuite = suiteArg.ToLower() switch
    {
        "full" => "Full",
        "standard" => "Standard",
        _ => "Quick"
    };
    AnsiConsole.MarkupLine($"[cyan]Using {testSuite} test suite[/]");
}
else
{
    testSuite = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("Select test suite:")
            .AddChoices(
                "Quick (2 tests - ~30 sec/model)",
                "Standard (6 tests - ~2 min/model)",
                "Full (8 tests - ~3 min/model)"));
}

var testCases = testSuite switch
{
    var s when s.StartsWith("Quick") => TestCases.GetQuickTestCases().ToList(),
    var s when s.StartsWith("Full") => TestCases.GetAllTestCases().ToList(),
    _ => TestCases.GetYamlTestCases().Concat(TestCases.GetSqlTestCases()).ToList()
};

AnsiConsole.MarkupLine($"\n[cyan]Running {testCases.Count} tests on {selectedModels.Count} models...[/]\n");

// Run benchmarks
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    AnsiConsole.MarkupLine("\n[yellow]Cancelling...[/]");
};

try
{
    var results = await runner.CompareModelsAsync(selectedModels, testCases, cts.Token);

    // Display final comparison
    BenchmarkRunner.DisplayComparisonTable(results);

    // Save results automatically
    await SaveResultsAsync(results);

    // Show recommendations for RTX 3050
    ShowRecommendations(results);
}
catch (OperationCanceledException)
{
    AnsiConsole.MarkupLine("[yellow]Benchmark cancelled.[/]");
}

// ============================================================================
// Argument parsing helper
// ============================================================================
string? GetArgValue(string[] arguments, string name)
{
    var index = Array.IndexOf(arguments, name);
    if (index >= 0 && index < arguments.Length - 1)
    {
        return arguments[index + 1];
    }
    return null;
}

// ============================================================================
// Helper Methods
// ============================================================================

void DisplayHardwareInfo()
{
    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("Hardware Info")
        .AddColumn("Value");

    table.AddRow("Target GPU", "[cyan]RTX 3050 6GB[/]");
    table.AddRow("Max Model Size", "[yellow]~4B parameters (100% GPU)[/]");
    table.AddRow("7B Models", "[grey]Possible with Q4 quantization[/]");
    table.AddRow("Recommended", "[green]qwen2.5-coder:3b, phi3:mini[/]");

    AnsiConsole.Write(table);
    AnsiConsole.WriteLine();
}

async Task SaveResultsAsync(List<ModelBenchmarkSummary> results)
{
    var filename = $"benchmark_results_{DateTime.Now:yyyyMMdd_HHmmss}.md";

    using var writer = new StreamWriter(filename);
    await writer.WriteLineAsync("# DeclareAPI.Agent Model Benchmark Results");
    await writer.WriteLineAsync($"\nDate: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    await writer.WriteLineAsync($"Hardware: RTX 3050 6GB VRAM\n");

    await writer.WriteLineAsync("## Summary\n");
    await writer.WriteLineAsync("| Model | Pass Rate | First Token | Total Time | Tokens/s | Quality |");
    await writer.WriteLineAsync("|-------|-----------|-------------|------------|----------|---------|");

    foreach (var summary in results.OrderByDescending(s => s.AvgQualityScore))
    {
        var passRate = summary.TotalTests > 0
            ? (double)summary.PassedTests / summary.TotalTests * 100
            : 0;
        await writer.WriteLineAsync(
            $"| {summary.ModelName} | {passRate:F0}% | {summary.AvgTimeToFirstTokenMs:F0}ms | {summary.AvgTotalTimeMs:F0}ms | {summary.AvgTokensPerSecond:F1} | {summary.AvgQualityScore:F0}/100 |");
    }

    await writer.WriteLineAsync("\n## Detailed Results\n");

    foreach (var summary in results)
    {
        await writer.WriteLineAsync($"### {summary.ModelName}\n");

        foreach (var result in summary.Results)
        {
            var status = result.Success ? "✅" : "❌";
            await writer.WriteLineAsync($"#### {status} {result.TestName} ({result.PromptType})\n");
            await writer.WriteLineAsync($"- Time to first token: {result.TimeToFirstToken.TotalMilliseconds:F0}ms");
            await writer.WriteLineAsync($"- Total time: {result.TotalTime.TotalMilliseconds:F0}ms");
            await writer.WriteLineAsync($"- Tokens/sec: {result.TokensPerSecond:F1}");
            await writer.WriteLineAsync($"- Quality: Syntax={result.SyntaxScore}, Complete={result.CompletenessScore}, Accuracy={result.AccuracyScore}");

            if (!string.IsNullOrEmpty(result.Error))
            {
                await writer.WriteLineAsync($"- Error: {result.Error}");
            }

            await writer.WriteLineAsync();
        }
    }

    AnsiConsole.MarkupLine($"[green]Results saved to {filename}[/]");
}

void ShowRecommendations(List<ModelBenchmarkSummary> results)
{
    var successfulResults = results.Where(r => r.PassedTests > 0).ToList();
    if (!successfulResults.Any())
    {
        AnsiConsole.MarkupLine("\n[yellow]No successful tests to base recommendations on.[/]");
        return;
    }

    AnsiConsole.MarkupLine("\n[bold underline]Recommendations for RTX 3050 6GB[/]\n");

    // Best quality
    var bestQuality = successfulResults
        .OrderByDescending(r => r.AvgQualityScore)
        .First();

    // Fastest
    var fastest = successfulResults
        .OrderBy(r => r.AvgTotalTimeMs)
        .First();

    // Best balance (quality * speed factor)
    var bestBalance = successfulResults
        .OrderByDescending(r => r.AvgQualityScore * (1000 / Math.Max(100, r.AvgTotalTimeMs)))
        .First();

    var recTable = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("Category")
        .AddColumn("Model")
        .AddColumn("Why");

    recTable.AddRow(
        "[green]Best Quality[/]",
        $"[bold]{bestQuality.ModelName}[/]",
        $"Score: {bestQuality.AvgQualityScore:F0}/100");

    recTable.AddRow(
        "[cyan]Fastest[/]",
        $"[bold]{fastest.ModelName}[/]",
        $"{fastest.AvgTokensPerSecond:F1} tok/s, {fastest.AvgTotalTimeMs:F0}ms avg");

    recTable.AddRow(
        "[yellow]Best Balance[/]",
        $"[bold]{bestBalance.ModelName}[/]",
        $"Quality {bestBalance.AvgQualityScore:F0} @ {bestBalance.AvgTokensPerSecond:F1} tok/s");

    AnsiConsole.Write(recTable);

    // Final recommendation
    AnsiConsole.WriteLine();

    if (bestQuality.AvgQualityScore >= 70)
    {
        AnsiConsole.MarkupLine($"[bold green]✓ Recommended:[/] Use [cyan]{bestBalance.ModelName}[/] for DeclareAPI.Agent");
        AnsiConsole.MarkupLine($"  It provides good quality ({bestBalance.AvgQualityScore:F0}/100) with reasonable speed.");
    }
    else
    {
        AnsiConsole.MarkupLine("[yellow]⚠ Consider installing better coding models:[/]");
        AnsiConsole.MarkupLine("  [cyan]ollama pull qwen2.5-coder:3b[/]  (Recommended)");
        AnsiConsole.MarkupLine("  [cyan]ollama pull deepseek-coder:6.7b[/]  (If you can offload to CPU)");
    }
}
