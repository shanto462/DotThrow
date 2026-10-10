namespace DotThrow.Internal;

// Failures added outside a rule (in PreValidate): named after the context path, default severity.
internal sealed class ContextSink : FailureSink
{
    public static readonly ContextSink Instance = new();

    private ContextSink()
    {
    }

    public override bool OnCheckFailure(ValidationReport report, in RuleFrame frame, string name, string message, string errorCode, object? value)
    {
        var context = frame.Context!;
        var arguments = context.Run.Arguments is { Count: > 0 } appended
            ? new System.Collections.Generic.Dictionary<string, object?>(appended, System.StringComparer.Ordinal)
            : null;
        report.AddRuleFailure(ValidationFailure.Create(
            PropertyPaths.Join(context.PropertyPath, name),
            RuleMessages.Format(message, arguments),
            errorCode,
            ValidatorDefaults.Severity,
            value,
            null,
            name,
            -1,
            arguments));
        return true;
    }
}
