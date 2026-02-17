using System.CommandLine;
using DeclareAPI.Agent.CLI.Commands;
using Spectre.Console;

namespace DeclareAPI.Agent.CLI;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("DeclareAPI Agent - AI-powered API configuration generator")
        {
            Name = "declareapi-agent"
        };

        // Add generate command
        rootCommand.AddCommand(GenerateCommand.Create());

        // Add version command
        var versionCommand = new Command("version", "Show version information");
        versionCommand.SetHandler(() =>
        {
            AnsiConsole.MarkupLine("[blue]DeclareAPI Agent[/] v0.1.0");
            AnsiConsole.MarkupLine("[dim]Generate DeclareAPI configurations from ER diagrams[/]");
        });
        rootCommand.AddCommand(versionCommand);

        // Add check command (verify Ollama)
        var checkCommand = new Command("check", "Check if Ollama is running and list available models");
        var urlOption = new Option<string>(
            "--url",
            getDefaultValue: () => "http://localhost:11434",
            description: "Ollama server URL");
        checkCommand.AddOption(urlOption);
        checkCommand.SetHandler(async (string url) =>
        {
            await CheckOllamaAsync(url);
        }, urlOption);
        rootCommand.AddCommand(checkCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task CheckOllamaAsync(string url)
    {
        AnsiConsole.MarkupLine($"[blue]Checking Ollama at {url}...[/]");

        try
        {
            var provider = new AI.OllamaProvider(url);

            var isAvailable = await provider.IsAvailableAsync();

            if (!isAvailable)
            {
                AnsiConsole.MarkupLine("[red]✗[/] Ollama is not available");
                AnsiConsole.MarkupLine("[dim]Make sure Ollama is running: ollama serve[/]");
                return;
            }

            AnsiConsole.MarkupLine("[green]✓[/] Ollama is running");

            var models = await provider.ListModelsAsync();

            if (!models.Any())
            {
                AnsiConsole.MarkupLine("[yellow]⚠[/] No models installed");
                AnsiConsole.MarkupLine("[dim]Install a model: ollama pull codellama[/]");
                return;
            }

            AnsiConsole.MarkupLine($"\n[blue]Available models ({models.Count}):[/]");

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Model")
                .AddColumn("Size");

            foreach (var model in models)
            {
                var size = model.SizeBytes.HasValue
                    ? FormatBytes(model.SizeBytes.Value)
                    : "-";
                table.AddRow(model.Name, size);
            }

            AnsiConsole.Write(table);

            // Recommend a model
            var hasCodeLlama = models.Any(m => m.Name.Contains("codellama", StringComparison.OrdinalIgnoreCase));
            var hasLlama3 = models.Any(m => m.Name.Contains("llama3", StringComparison.OrdinalIgnoreCase));

            if (hasCodeLlama)
            {
                AnsiConsole.MarkupLine("\n[green]✓[/] Recommended model (codellama) is available");
            }
            else if (hasLlama3)
            {
                AnsiConsole.MarkupLine("\n[yellow]ℹ[/] llama3 is available (codellama recommended for code generation)");
            }
            else
            {
                AnsiConsole.MarkupLine("\n[yellow]⚠[/] Consider installing codellama: [dim]ollama pull codellama[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Error: {ex.Message}");
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}
