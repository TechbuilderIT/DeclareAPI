namespace Techbuilder.DeclareAPI.Core.Models;

/// <summary>
/// RFC 7807 compliant validation error response.
/// </summary>
public class ValidationProblemDetails
{
    public string Type { get; set; } = "https://tools.ietf.org/html/rfc7807";
    public string Title { get; set; } = "Validation Failed";
    public int Status { get; set; } = 400;
    public string? Detail { get; set; }
    public Dictionary<string, string[]> Errors { get; set; } = new();

    public static ValidationProblemDetails FromErrors(IEnumerable<ValidationErrorItem> errors)
    {
        var grouped = errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray()
            );

        return new ValidationProblemDetails
        {
            Detail = "One or more validation errors occurred.",
            Errors = grouped
        };
    }
}

/// <summary>
/// Single validation error item.
/// </summary>
public record ValidationErrorItem(string PropertyName, string ErrorMessage);
