namespace AgentSync.Core.Configuration;

public enum ValidationSeverity
{
    Warning,
    Error,
}

public sealed record ValidationMessage(
    string Code,
    ValidationSeverity Severity,
    string Message,
    string? Source = null);

/// <summary>Aggregated validation outcome.</summary>
public sealed class ValidationResult
{
    private readonly List<ValidationMessage> _messages = new();

    public IReadOnlyList<ValidationMessage> Messages => _messages;

    public bool IsValid => !_messages.Any(m => m.Severity == ValidationSeverity.Error);

    public bool HasWarnings => _messages.Any(m => m.Severity == ValidationSeverity.Warning);

    public void Add(ValidationMessage message) => _messages.Add(message);

    public void AddError(string code, string message, string? source = null)
        => Add(new ValidationMessage(code, ValidationSeverity.Error, message, source));

    public void AddWarning(string code, string message, string? source = null)
        => Add(new ValidationMessage(code, ValidationSeverity.Warning, message, source));

    public void AddRange(IEnumerable<ValidationMessage> messages) => _messages.AddRange(messages);
}
