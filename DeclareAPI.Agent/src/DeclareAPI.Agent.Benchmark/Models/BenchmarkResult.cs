namespace DeclareAPI.Agent.Benchmark.Models;

/// <summary>
/// Result of a single benchmark run.
/// </summary>
public class BenchmarkResult
{
    public string ModelName { get; set; } = "";
    public string TestName { get; set; } = "";
    public string PromptType { get; set; } = ""; // YAML, SQL, Filter, Validation

    // Timing metrics
    public TimeSpan TimeToFirstToken { get; set; }
    public TimeSpan TotalTime { get; set; }
    public double TokensPerSecond { get; set; }

    // Output metrics
    public int PromptTokens { get; set; }
    public int OutputTokens { get; set; }
    public string Output { get; set; } = "";

    // Quality metrics (0-100)
    public int SyntaxScore { get; set; }      // Is output valid YAML/SQL?
    public int CompletenessScore { get; set; } // Does it have all required parts?
    public int AccuracyScore { get; set; }     // Are the values correct?
    public int OverallScore => (SyntaxScore + CompletenessScore + AccuracyScore) / 3;

    // Status
    public bool Success { get; set; }
    public string? Error { get; set; }

    // Hardware info
    public string? GpuUsage { get; set; }
    public long MemoryUsedMb { get; set; }
}

/// <summary>
/// Summary of benchmark results for a model.
/// </summary>
public class ModelBenchmarkSummary
{
    public string ModelName { get; set; } = "";
    public string ModelSize { get; set; } = "";
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }

    // Average metrics
    public double AvgTimeToFirstTokenMs { get; set; }
    public double AvgTotalTimeMs { get; set; }
    public double AvgTokensPerSecond { get; set; }
    public double AvgQualityScore { get; set; }

    // By task type
    public Dictionary<string, double> AvgScoreByTaskType { get; set; } = new();
    public Dictionary<string, double> AvgTimeByTaskType { get; set; } = new();

    public List<BenchmarkResult> Results { get; set; } = new();
}

/// <summary>
/// Test case definition.
/// </summary>
public class BenchmarkTestCase
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = ""; // YAML, SQL
    public string SystemPrompt { get; set; } = "";
    public string UserPrompt { get; set; } = "";
    public Func<string, int>? SyntaxValidator { get; set; }
    public Func<string, int>? CompletenessValidator { get; set; }
    public Func<string, int>? AccuracyValidator { get; set; }
}
