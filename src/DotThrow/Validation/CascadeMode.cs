namespace DotThrow;

/// <summary>
/// Says what happens after a failure: keep going, or stop. Used per rule
/// (<see cref="RuleOptionsBase{T, TValue, TOptions}.Cascade(CascadeMode)"/>,
/// <see cref="Validator{T}.RuleCascade"/>) and per validator (<see cref="Validator{T}.ClassCascade"/>).
/// </summary>
public enum CascadeMode
{
    /// <summary>Keep running: later checks of a rule, or later rules of a validator, still run.</summary>
    Continue = 0,

    /// <summary>
    /// Stop: the first failed check ends the rule, or the first failed rule ends the validator.
    /// This is the DotThrow default for rules, so each rule reports at most one failure.
    /// </summary>
    Stop = 2,
}
