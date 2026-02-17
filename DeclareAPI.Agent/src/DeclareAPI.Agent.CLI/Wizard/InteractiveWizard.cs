using DeclareAPI.Agent.AI;
using DeclareAPI.Agent.Core.Abstractions;
using DeclareAPI.Agent.Core.Configuration;
using DeclareAPI.Agent.Core.Models;
using DeclareAPI.Agent.Generators;
using DeclareAPI.Agent.Parsers;
using Spectre.Console;

namespace DeclareAPI.Agent.CLI.Wizard;

/// <summary>
/// Interactive wizard for generating DeclareAPI configurations.
/// Guides the user through the generation process step by step.
/// </summary>
public class InteractiveWizard
{
    private readonly AgentOptions _options;
    private readonly MermaidErParser _parser;
    private readonly DeclareApiYamlGenerator _yamlGenerator;
    private readonly PostgreSqlGenerator _sqlGenerator;

    public InteractiveWizard(AgentOptions options)
    {
        _options = options;
        _parser = new MermaidErParser();
        _yamlGenerator = new DeclareApiYamlGenerator();
        _sqlGenerator = new PostgreSqlGenerator();
    }

    public async Task RunAsync(GenerationOptions options, CancellationToken cancellationToken = default)
    {
        PrintWelcome();

        // Step 1: Select input file
        var inputFile = await SelectInputFileAsync(options.InputFile, cancellationToken);
        if (string.IsNullOrEmpty(inputFile)) return;

        // Step 2: Parse the file
        var spec = await ParseFileAsync(inputFile, cancellationToken);
        if (spec == null) return;

        // Step 3: Configure project
        spec = await ConfigureProjectAsync(spec, options, cancellationToken);

        // Step 4: Select entities to include
        spec = await SelectEntitiesAsync(spec, cancellationToken);

        // Step 5: Check Ollama (optional AI enhancement)
        var useAi = await CheckAndConfigureAiAsync(cancellationToken);

        // Step 6: Preview and confirm
        if (!await PreviewAndConfirmAsync(spec, cancellationToken)) return;

        // Step 7: Generate files (with AI if available)
        await GenerateFilesAsync(spec, options, useAi, cancellationToken);

        PrintCompletion(options.OutputDirectory);
    }

    private static void PrintWelcome()
    {
        AnsiConsole.Clear();

        var rule = new Rule("[blue]DeclareAPI Agent[/]")
        {
            Justification = Justify.Center
        };
        AnsiConsole.Write(rule);

        AnsiConsole.MarkupLine("\n[dim]Generate DeclareAPI configurations from ER diagrams[/]\n");
    }

    private static async Task<string?> SelectInputFileAsync(string? providedPath, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(providedPath) && File.Exists(providedPath))
        {
            AnsiConsole.MarkupLine($"[blue]Input file:[/] {Markup.Escape(providedPath)}");
            return providedPath;
        }

        // Search for .mermaid files
        var currentDir = Directory.GetCurrentDirectory();
        var mermaidFiles = Directory.GetFiles(currentDir, "*.mermaid", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(currentDir, "*.mmd", SearchOption.AllDirectories))
            .ToList();

        if (!mermaidFiles.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No .mermaid or .mmd files found in current directory.[/]");

            var path = AnsiConsole.Ask<string>("Enter path to ER diagram file:");

            if (!File.Exists(path))
            {
                AnsiConsole.MarkupLine("[red]✗[/] File not found");
                return null;
            }

            return path;
        }

        // Let user select from found files
        var relativePaths = mermaidFiles
            .Select(f => Path.GetRelativePath(currentDir, f))
            .ToList();

        // Create a mapping of escaped paths to original paths for display
        var manualOption = "Enter path manually";
        var pathMapping = relativePaths.ToDictionary(
            p => Markup.Escape(p),
            p => p);
        pathMapping[manualOption] = manualOption;

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select ER diagram file:")
                .PageSize(10)
                .AddChoices(pathMapping.Keys));

        if (selected == manualOption)
        {
            var path = AnsiConsole.Ask<string>("Enter path to ER diagram file:");
            return File.Exists(path) ? path : null;
        }

        // Get original path from mapping
        var originalPath = pathMapping[selected];
        return Path.Combine(currentDir, originalPath);
    }

    private async Task<ProjectSpec?> ParseFileAsync(string inputFile, CancellationToken cancellationToken)
    {
        return await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Parsing ER diagram...", async ctx =>
            {
                try
                {
                    var spec = await _parser.ParseFileAsync(inputFile, cancellationToken);

                    ctx.Status("Analysis complete");

                    return spec;
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"\n[red]✗[/] Parse error: {ex.Message}");
                    return null;
                }
            });
    }

    private static Task<ProjectSpec> ConfigureProjectAsync(
        ProjectSpec spec,
        GenerationOptions options,
        CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine($"\n[green]✓[/] Found [blue]{spec.Entities.Count}[/] entities and [blue]{spec.Relationships.Count}[/] relationships\n");

        // Project name
        var defaultName = string.IsNullOrEmpty(options.ProjectName) ? spec.Name : options.ProjectName;
        spec.Name = AnsiConsole.Prompt(
            new TextPrompt<string>("Project name:")
                .DefaultValue(defaultName)
                .ValidationErrorMessage("[red]Name is required[/]")
                .Validate(name => !string.IsNullOrWhiteSpace(name)));

        // Database provider
        spec.Database.Provider = AnsiConsole.Prompt(
            new SelectionPrompt<DatabaseProvider>()
                .Title("Database provider:")
                .AddChoices(DatabaseProvider.PostgreSQL, DatabaseProvider.SqlServer, DatabaseProvider.MySql));

        // Base path
        spec.Api.BasePath = AnsiConsole.Prompt(
            new TextPrompt<string>("API base path:")
                .DefaultValue(spec.Api.BasePath));

        return Task.FromResult(spec);
    }

    private static Task<ProjectSpec> SelectEntitiesAsync(ProjectSpec spec, CancellationToken cancellationToken)
    {
        if (spec.Entities.Count <= 5)
        {
            // For small number of entities, include all by default
            return Task.FromResult(spec);
        }

        var includeAll = AnsiConsole.Confirm($"Include all {spec.Entities.Count} entities?", true);

        if (includeAll)
        {
            return Task.FromResult(spec);
        }

        var entityChoices = spec.Entities.Select(e => e.Name).ToList();

        var selected = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("Select entities to include:")
                .PageSize(15)
                .Required()
                .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to accept)[/]")
                .AddChoices(entityChoices));

        // Filter entities
        spec.Entities = spec.Entities.Where(e => selected.Contains(e.Name)).ToList();

        // Filter relationships
        var selectedNames = new HashSet<string>(selected);
        spec.Relationships = spec.Relationships
            .Where(r => selectedNames.Contains(r.FromEntity) && selectedNames.Contains(r.ToEntity))
            .ToList();

        return Task.FromResult(spec);
    }

    private async Task<bool> CheckAndConfigureAiAsync(CancellationToken cancellationToken)
    {
        var checkAi = AnsiConsole.Confirm("Check Ollama for AI-enhanced generation?", false);

        if (!checkAi)
        {
            AnsiConsole.MarkupLine("[dim]Using rule-based generation only[/]");
            return false;
        }

        var provider = new OllamaProvider(_options.OllamaUrl);

        var isAvailable = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Checking Ollama...", async ctx =>
            {
                return await provider.IsAvailableAsync(cancellationToken);
            });

        if (!isAvailable)
        {
            AnsiConsole.MarkupLine("[yellow]⚠[/] Ollama not available. Using rule-based generation.");
            return false;
        }

        AnsiConsole.MarkupLine("[green]✓[/] Ollama is running");

        var models = await provider.ListModelsAsync(cancellationToken);

        if (!models.Any())
        {
            AnsiConsole.MarkupLine("[yellow]⚠[/] No models installed. Using rule-based generation.");
            return false;
        }

        var modelNames = models.Select(m => m.Name).ToList();

        _options.DefaultModel = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select AI model:")
                .AddChoices(modelNames));

        return true;
    }

    private Task<bool> PreviewAndConfirmAsync(ProjectSpec spec, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("\n[blue]Configuration Summary[/]\n");

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Setting")
            .AddColumn("Value");

        table.AddRow("Project", spec.Name);
        table.AddRow("Database", spec.Database.Provider.ToString());
        table.AddRow("Base Path", spec.Api.BasePath);
        table.AddRow("Entities", spec.Entities.Count.ToString());
        table.AddRow("Relationships", spec.Relationships.Count.ToString());

        AnsiConsole.Write(table);

        // Show entity list
        AnsiConsole.MarkupLine("\n[blue]Entities:[/]");
        foreach (var entity in spec.Entities.Take(10))
        {
            AnsiConsole.MarkupLine($"  • {entity.Name} ({entity.Fields.Count} fields)");
        }

        if (spec.Entities.Count > 10)
        {
            AnsiConsole.MarkupLine($"  [dim]... and {spec.Entities.Count - 10} more[/]");
        }

        AnsiConsole.WriteLine();

        return Task.FromResult(AnsiConsole.Confirm("Generate files?", true));
    }

    private async Task GenerateFilesAsync(
        ProjectSpec spec,
        GenerationOptions options,
        bool useAi,
        CancellationToken cancellationToken)
    {
        // Create output directory
        Directory.CreateDirectory(options.OutputDirectory);

        if (useAi)
        {
            await GenerateWithAiAsync(spec, options, cancellationToken);
        }
        else
        {
            await GenerateWithRulesAsync(spec, options, cancellationToken);
        }
    }

    private async Task GenerateWithAiAsync(
        ProjectSpec spec,
        GenerationOptions options,
        CancellationToken cancellationToken)
    {
        var aiProvider = new OllamaProvider(_options.OllamaUrl);

        AnsiConsole.MarkupLine("\n[blue]Generating with AI enhancement...[/]\n");

        // YAML Generation with AI
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Generating YAML with AI...", async ctx =>
            {
                var yamlGenerator = new AiEnhancedYamlGenerator(
                    aiProvider,
                    _options.DefaultModel,
                    msg => ctx.Status($"[dim]{Markup.Escape(msg)}[/]"));

                var yamlResult = await yamlGenerator.GenerateAsync(spec, cancellationToken);

                if (yamlResult.Success)
                {
                    var yamlPath = Path.Combine(options.OutputDirectory, yamlResult.FileName);
                    await File.WriteAllTextAsync(yamlPath, yamlResult.Content, cancellationToken);
                    AnsiConsole.MarkupLine($"[green]✓[/] Generated: {Markup.Escape(yamlResult.FileName)}");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗[/] YAML: {Markup.Escape(yamlResult.Error ?? "Unknown error")}");
                }
            });

        // SQL Generation with AI
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Generating SQL with AI...", async ctx =>
            {
                var sqlGenerator = new AiEnhancedSqlGenerator(
                    aiProvider,
                    _options.DefaultModel,
                    msg => ctx.Status($"[dim]{Markup.Escape(msg)}[/]"));

                var sqlResult = await sqlGenerator.GenerateAsync(spec, cancellationToken);

                if (sqlResult.Success)
                {
                    var sqlPath = Path.Combine(options.OutputDirectory, sqlResult.FileName);
                    await File.WriteAllTextAsync(sqlPath, sqlResult.Content, cancellationToken);
                    AnsiConsole.MarkupLine($"[green]✓[/] Generated: {Markup.Escape(sqlResult.FileName)}");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗[/] SQL: {Markup.Escape(sqlResult.Error ?? "Unknown error")}");
                }
            });
    }

    private async Task GenerateWithRulesAsync(
        ProjectSpec spec,
        GenerationOptions options,
        CancellationToken cancellationToken)
    {
        await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var yamlTask = ctx.AddTask("Generating YAML", maxValue: 100);
                var sqlTask = ctx.AddTask("Generating SQL", maxValue: 100);

                // Generate YAML
                yamlTask.Increment(10);
                var yamlResult = await _yamlGenerator.GenerateAsync(spec, cancellationToken);
                yamlTask.Increment(70);

                if (yamlResult.Success)
                {
                    var yamlPath = Path.Combine(options.OutputDirectory, yamlResult.FileName);
                    await File.WriteAllTextAsync(yamlPath, yamlResult.Content, cancellationToken);
                    yamlTask.Increment(20);
                    yamlTask.Description = $"[green]✓[/] {yamlResult.FileName}";
                }
                else
                {
                    yamlTask.Description = $"[red]✗[/] YAML: {yamlResult.Error}";
                    yamlTask.Increment(20);
                }

                // Generate SQL
                sqlTask.Increment(10);
                var sqlResult = await _sqlGenerator.GenerateAsync(spec, cancellationToken);
                sqlTask.Increment(70);

                if (sqlResult.Success)
                {
                    var sqlPath = Path.Combine(options.OutputDirectory, sqlResult.FileName);
                    await File.WriteAllTextAsync(sqlPath, sqlResult.Content, cancellationToken);
                    sqlTask.Increment(20);
                    sqlTask.Description = $"[green]✓[/] {sqlResult.FileName}";
                }
                else
                {
                    sqlTask.Description = $"[red]✗[/] SQL: {sqlResult.Error}";
                    sqlTask.Increment(20);
                }
            });
    }

    private static void PrintCompletion(string outputDir)
    {
        AnsiConsole.WriteLine();

        var panel = new Panel(new Markup($"""
            [green]Generation complete![/]

            Output directory: [blue]{outputDir}[/]

            [dim]Next steps:[/]
            1. Review the generated files
            2. Copy declareapi.yaml to your project
            3. Run init.sql against your database
            4. Configure your DeclareAPI application
            """))
            .Border(BoxBorder.Rounded)
            .Header("[blue]Done[/]")
            .Padding(1, 1);

        AnsiConsole.Write(panel);
    }
}
