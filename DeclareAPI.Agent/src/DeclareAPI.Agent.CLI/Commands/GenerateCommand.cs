using System.CommandLine;
using DeclareAPI.Agent.CLI.Wizard;
using DeclareAPI.Agent.Core.Configuration;
using DeclareAPI.Agent.Generators;
using DeclareAPI.Agent.Parsers;
using Spectre.Console;

namespace DeclareAPI.Agent.CLI.Commands;

/// <summary>
/// The main generate command that creates DeclareAPI configuration from ER diagrams.
/// </summary>
public static class GenerateCommand
{
    public static Command Create()
    {
        var inputOption = new Option<FileInfo?>(
            aliases: ["--input", "-i"],
            description: "Input ER diagram file (Mermaid format)");

        var outputOption = new Option<DirectoryInfo>(
            aliases: ["--output", "-o"],
            getDefaultValue: () => new DirectoryInfo("./output"),
            description: "Output directory for generated files");

        var projectOption = new Option<string?>(
            aliases: ["--project", "-p"],
            description: "Project name (defaults to input file name)");

        var modelOption = new Option<string>(
            aliases: ["--model", "-m"],
            getDefaultValue: () => "codellama",
            description: "Ollama model to use for AI-enhanced generation");

        var ollamaUrlOption = new Option<string>(
            aliases: ["--ollama-url"],
            getDefaultValue: () => "http://localhost:11434",
            description: "Ollama server URL");

        var noAiOption = new Option<bool>(
            aliases: ["--no-ai"],
            getDefaultValue: () => false,
            description: "Skip AI enhancement, use rule-based generation only");

        var wizardOption = new Option<bool>(
            aliases: ["--wizard", "-w"],
            getDefaultValue: () => true,
            description: "Run in interactive wizard mode");

        var forceOption = new Option<bool>(
            aliases: ["--force", "-f"],
            getDefaultValue: () => false,
            description: "Overwrite existing files without confirmation");

        var command = new Command("generate", "Generate DeclareAPI configuration from ER diagram")
        {
            inputOption,
            outputOption,
            projectOption,
            modelOption,
            ollamaUrlOption,
            noAiOption,
            wizardOption,
            forceOption
        };

        command.SetHandler(async (context) =>
        {
            var input = context.ParseResult.GetValueForOption(inputOption);
            var output = context.ParseResult.GetValueForOption(outputOption)!;
            var project = context.ParseResult.GetValueForOption(projectOption);
            var model = context.ParseResult.GetValueForOption(modelOption)!;
            var ollamaUrl = context.ParseResult.GetValueForOption(ollamaUrlOption)!;
            var noAi = context.ParseResult.GetValueForOption(noAiOption);
            var wizard = context.ParseResult.GetValueForOption(wizardOption);
            var force = context.ParseResult.GetValueForOption(forceOption);

            var options = new GenerationOptions
            {
                InputFile = input?.FullName ?? "",
                OutputDirectory = output.FullName,
                ProjectName = project,
                Model = model,
                GenerateYaml = true,
                GenerateSql = true
            };

            var agentOptions = new AgentOptions
            {
                OllamaUrl = ollamaUrl,
                DefaultModel = model,
                OutputDirectory = output.FullName,
                InteractiveMode = wizard,
                OverwriteExisting = force
            };

            if (wizard || input == null)
            {
                // Run interactive wizard
                var wizardRunner = new InteractiveWizard(agentOptions);
                await wizardRunner.RunAsync(options, context.GetCancellationToken());
            }
            else
            {
                // Run non-interactive
                await RunNonInteractiveAsync(options, agentOptions, noAi, context.GetCancellationToken());
            }
        });

        return command;
    }

    private static async Task RunNonInteractiveAsync(
        GenerationOptions options,
        AgentOptions agentOptions,
        bool noAi,
        CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[blue]DeclareAPI Agent[/] - Non-interactive mode\n");

        // Validate input
        if (string.IsNullOrEmpty(options.InputFile) || !File.Exists(options.InputFile))
        {
            AnsiConsole.MarkupLine("[red]✗[/] Input file is required and must exist");
            return;
        }

        // Parse ER diagram
        AnsiConsole.MarkupLine($"[blue]Parsing:[/] {Markup.Escape(options.InputFile)}");

        var parser = new MermaidErParser();
        var spec = await parser.ParseFileAsync(options.InputFile, cancellationToken);

        if (!string.IsNullOrEmpty(options.ProjectName))
        {
            spec.Name = options.ProjectName;
        }

        AnsiConsole.MarkupLine($"[green]✓[/] Found {spec.Entities.Count} entities, {spec.Relationships.Count} relationships");

        // Generate outputs
        var yamlGenerator = new DeclareApiYamlGenerator();
        var sqlGenerator = new PostgreSqlGenerator();

        // Create output directory
        Directory.CreateDirectory(options.OutputDirectory);

        // Generate YAML
        if (options.GenerateYaml)
        {
            AnsiConsole.MarkupLine("\n[blue]Generating YAML...[/]");
            var yamlResult = await yamlGenerator.GenerateAsync(spec, cancellationToken);

            if (yamlResult.Success)
            {
                var yamlPath = Path.Combine(options.OutputDirectory, yamlResult.FileName);

                if (!agentOptions.OverwriteExisting && File.Exists(yamlPath))
                {
                    AnsiConsole.MarkupLine($"[yellow]⚠[/] File exists: {Markup.Escape(yamlPath)} (use --force to overwrite)");
                }
                else
                {
                    await File.WriteAllTextAsync(yamlPath, yamlResult.Content, cancellationToken);
                    AnsiConsole.MarkupLine($"[green]✓[/] Created: {Markup.Escape(yamlPath)}");
                }
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]✗[/] YAML generation failed: {yamlResult.Error}");
            }
        }

        // Generate SQL
        if (options.GenerateSql)
        {
            AnsiConsole.MarkupLine("\n[blue]Generating SQL...[/]");
            var sqlResult = await sqlGenerator.GenerateAsync(spec, cancellationToken);

            if (sqlResult.Success)
            {
                var sqlPath = Path.Combine(options.OutputDirectory, sqlResult.FileName);

                if (!agentOptions.OverwriteExisting && File.Exists(sqlPath))
                {
                    AnsiConsole.MarkupLine($"[yellow]⚠[/] File exists: {Markup.Escape(sqlPath)} (use --force to overwrite)");
                }
                else
                {
                    await File.WriteAllTextAsync(sqlPath, sqlResult.Content, cancellationToken);
                    AnsiConsole.MarkupLine($"[green]✓[/] Created: {Markup.Escape(sqlPath)}");
                }
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]✗[/] SQL generation failed: {sqlResult.Error}");
            }
        }

        AnsiConsole.MarkupLine("\n[green]Done![/]");
    }
}
