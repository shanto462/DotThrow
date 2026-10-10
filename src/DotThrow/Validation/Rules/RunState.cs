using System.Collections.Generic;
using System.Threading;

namespace DotThrow.Internal;

// What one validation run shares across its contexts (the root, child, item and included
// validators). It belongs to a root context and is reused with it.
internal sealed class RunState
{
    public ValidationReport Report = null!;

    // The rule that runs right now. A failed check in collect mode goes through it, so rule
    // options (path, message, code, severity, state, cascade) apply.
    public RuleFrame Frame;

    public IDictionary<string, object?>? Items;

    public List<string>? ExecutedGroups;

    public string[]? Properties;

    public CascadeMode? ClassCascadeOverride;

    public bool IsAsync;

    public CancellationToken CancellationToken;

    // ValidationOptions.RuleSelector.
    public IRuleSelector? RuleSelector;

    // The rule message rendered for the failed check that is being added right now (see
    // ValidationReport.RenderCheckMessage), taken by Rule<T>.AddFailure.
    public string? RuleMessage;

    // Message arguments added with ValidationContext.AppendArgument by the running rule.
    public Dictionary<string, object?>? Arguments;

    public void ClearArguments()
    {
        if (Arguments is { Count: > 0 })
        {
            Arguments.Clear();
        }
    }

    public void Clear()
    {
        Report = null!;
        Frame = default;
        Items = null;
        ExecutedGroups = null;
        Properties = null;
        ClassCascadeOverride = null;
        IsAsync = false;
        CancellationToken = default;
        RuleSelector = null;
        RuleMessage = null;
        ClearArguments();
    }
}
