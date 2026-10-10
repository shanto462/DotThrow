namespace DotThrow;

/// <summary>
/// Well-known group names for <see cref="Validator{T}"/>. A rule without a group belongs to
/// <see cref="Default"/>. Validating without groups runs only the <see cref="Default"/> rules.
/// </summary>
public static class ValidationGroups
{
    /// <summary>The group of rules added without a group.</summary>
    public const string Default = "default";

    /// <summary>Pass this group to run every rule, whatever its group.</summary>
    public const string All = "*";
}
