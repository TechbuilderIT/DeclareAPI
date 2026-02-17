using System.Text.Json.Serialization;

namespace Techbuilder.DeclareAPI.Core.Models;

/// <summary>
/// Standard API error response following RFC 7807 Problem Details.
/// </summary>
public class ApiError
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "about:blank";

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    [JsonPropertyName("instance")]
    public string? Instance { get; set; }

    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string[]>? Errors { get; set; }

    public static ApiError BadRequest(string detail, Dictionary<string, string[]>? errors = null)
        => new()
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = "Bad Request",
            Status = 400,
            Detail = detail,
            Errors = errors
        };

    public static ApiError NotFound(string detail)
        => new()
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            Title = "Not Found",
            Status = 404,
            Detail = detail
        };

    public static ApiError InternalServerError(string detail)
        => new()
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            Title = "Internal Server Error",
            Status = 500,
            Detail = detail
        };
}
