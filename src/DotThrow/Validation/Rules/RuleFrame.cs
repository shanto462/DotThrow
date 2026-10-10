namespace DotThrow.Internal;

// Receives the failed checks of the rule that runs right now (see ValidationReport.Frame).
internal abstract class FailureSink
{
    // Adds the failure with the rule options applied. Returns true when the chain must stop.
    public abstract bool OnCheckFailure(ValidationReport report, in RuleFrame frame, string name, string message, string errorCode, object? value);
}

// What a failed check needs to know about the running rule. A struct stored in the report,
// so entering a rule allocates nothing.
internal readonly struct RuleFrame
{
    public RuleFrame(FailureSink sink, ValidationContext context, int index, object? item)
    {
        Sink = sink;
        Context = context;
        Index = index;
        Item = item;
    }

    public FailureSink? Sink { get; }

    public ValidationContext? Context { get; }

    // The collection index for ForEach rules, or -1.
    public int Index { get; }

    // The collection item, kept only when a custom indexer needs it.
    public object? Item { get; }
}
