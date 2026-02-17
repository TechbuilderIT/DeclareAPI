using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Results;
using Techbuilder.DeclareAPI.Core.Configuration;

namespace Techbuilder.DeclareAPI.Core.Validation;

/// <summary>
/// Builds FluentValidation validators dynamically from field configuration.
/// </summary>
public class DynamicValidator
{
    /// <summary>
    /// Validates a request body against the field configuration.
    /// </summary>
    public ValidationResult Validate(Dictionary<string, object?> body, List<FieldConfig> fields)
    {
        var validator = BuildValidator(fields);
        return validator.Validate(body);
    }

    /// <summary>
    /// Validates a request body asynchronously against the field configuration.
    /// </summary>
    public async Task<ValidationResult> ValidateAsync(
        Dictionary<string, object?> body,
        List<FieldConfig> fields,
        CancellationToken ct = default)
    {
        var validator = BuildValidator(fields);
        return await validator.ValidateAsync(body, ct);
    }

    private InlineValidator<Dictionary<string, object?>> BuildValidator(List<FieldConfig> fields)
    {
        var validator = new InlineValidator<Dictionary<string, object?>>();

        foreach (var field in fields)
        {
            AddFieldRules(validator, field);
        }

        return validator;
    }

    private void AddFieldRules(InlineValidator<Dictionary<string, object?>> validator, FieldConfig field)
    {
        // Required validation
        if (field.Required)
        {
            validator.RuleFor(x => x)
                .Must(dict => dict.ContainsKey(field.Name) && dict[field.Name] != null)
                .WithMessage($"'{field.Name}' is required");
        }

        // Type-specific validations
        switch (field.FieldType)
        {
            case FieldType.String:
                AddStringRules(validator, field);
                break;
            case FieldType.Int:
            case FieldType.Long:
            case FieldType.Decimal:
                AddNumericRules(validator, field);
                break;
            case FieldType.Date:
            case FieldType.DateTime:
                AddDateRules(validator, field);
                break;
            case FieldType.Uuid:
                AddUuidRules(validator, field);
                break;
            case FieldType.Bool:
                AddBoolRules(validator, field);
                break;
        }
    }

    private void AddStringRules(InlineValidator<Dictionary<string, object?>> validator, FieldConfig field)
    {
        // Max length
        if (field.Max.HasValue)
        {
            validator.RuleFor(x => x)
                .Must(dict =>
                {
                    if (!dict.TryGetValue(field.Name, out var value) || value == null) return true;
                    return value.ToString()!.Length <= field.Max.Value;
                })
                .WithMessage($"'{field.Name}' must not exceed {field.Max.Value} characters");
        }

        // Min length
        if (field.Min.HasValue)
        {
            validator.RuleFor(x => x)
                .Must(dict =>
                {
                    if (!dict.TryGetValue(field.Name, out var value) || value == null) return true;
                    return value.ToString()!.Length >= field.Min.Value;
                })
                .WithMessage($"'{field.Name}' must be at least {field.Min.Value} characters");
        }

        // Pattern (regex)
        if (!string.IsNullOrEmpty(field.Pattern))
        {
            validator.RuleFor(x => x)
                .Must(dict =>
                {
                    if (!dict.TryGetValue(field.Name, out var value) || value == null) return true;
                    return Regex.IsMatch(value.ToString()!, field.Pattern);
                })
                .WithMessage($"'{field.Name}' does not match the required pattern");
        }
    }

    private void AddNumericRules(InlineValidator<Dictionary<string, object?>> validator, FieldConfig field)
    {
        // Type validation
        validator.RuleFor(x => x)
            .Must(dict =>
            {
                if (!dict.TryGetValue(field.Name, out var value) || value == null) return true;
                return TryParseNumeric(value, field.FieldType, out _);
            })
            .WithMessage($"'{field.Name}' must be a valid {field.Type}");

        // Max value
        if (field.Max.HasValue)
        {
            validator.RuleFor(x => x)
                .Must(dict =>
                {
                    if (!dict.TryGetValue(field.Name, out var value) || value == null) return true;
                    if (!TryParseNumeric(value, field.FieldType, out var numericValue)) return true;
                    return numericValue <= field.Max.Value;
                })
                .WithMessage($"'{field.Name}' must not exceed {field.Max.Value}");
        }

        // Min value
        if (field.Min.HasValue)
        {
            validator.RuleFor(x => x)
                .Must(dict =>
                {
                    if (!dict.TryGetValue(field.Name, out var value) || value == null) return true;
                    if (!TryParseNumeric(value, field.FieldType, out var numericValue)) return true;
                    return numericValue >= field.Min.Value;
                })
                .WithMessage($"'{field.Name}' must be at least {field.Min.Value}");
        }
    }

    private void AddDateRules(InlineValidator<Dictionary<string, object?>> validator, FieldConfig field)
    {
        validator.RuleFor(x => x)
            .Must(dict =>
            {
                if (!dict.TryGetValue(field.Name, out var value) || value == null) return true;
                return DateTime.TryParse(value.ToString(), out _);
            })
            .WithMessage($"'{field.Name}' must be a valid date");
    }

    private void AddUuidRules(InlineValidator<Dictionary<string, object?>> validator, FieldConfig field)
    {
        validator.RuleFor(x => x)
            .Must(dict =>
            {
                if (!dict.TryGetValue(field.Name, out var value) || value == null) return true;
                return Guid.TryParse(value.ToString(), out _);
            })
            .WithMessage($"'{field.Name}' must be a valid UUID");
    }

    private void AddBoolRules(InlineValidator<Dictionary<string, object?>> validator, FieldConfig field)
    {
        validator.RuleFor(x => x)
            .Must(dict =>
            {
                if (!dict.TryGetValue(field.Name, out var value) || value == null) return true;
                return value is bool || bool.TryParse(value.ToString(), out _);
            })
            .WithMessage($"'{field.Name}' must be a boolean");
    }

    private static bool TryParseNumeric(object value, FieldType type, out decimal numericValue)
    {
        numericValue = 0;

        if (value is int i) { numericValue = i; return true; }
        if (value is long l) { numericValue = l; return true; }
        if (value is decimal d) { numericValue = d; return true; }
        if (value is double dbl) { numericValue = (decimal)dbl; return true; }
        if (value is float f) { numericValue = (decimal)f; return true; }

        var str = value.ToString();
        return type switch
        {
            FieldType.Int => int.TryParse(str, out var intVal) && (numericValue = intVal) == numericValue,
            FieldType.Long => long.TryParse(str, out var longVal) && (numericValue = longVal) == numericValue,
            FieldType.Decimal => decimal.TryParse(str, out numericValue),
            _ => decimal.TryParse(str, out numericValue)
        };
    }
}
