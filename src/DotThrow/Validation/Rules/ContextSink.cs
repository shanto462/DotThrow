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
        report.AddRuleFailure(ValidationFailure.Create(
            PropertyPaths.Join(frame.Context!.PropertyPath, name),
            message,
            errorCode,
            ValidatorDefaults.Severity,
            value,
            null));
        return true;
    }
}
