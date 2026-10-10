namespace DotThrow;

/// <summary>
/// A chain of checks for one value that can also read the object being validated, used by
/// <see cref="Validator{T}"/> rules that compare a property with another property.
/// </summary>
/// <typeparam name="T">The type of the checked value.</typeparam>
/// <typeparam name="TRoot">The type of the validated object.</typeparam>
/// <param name="guard">The guard to run the checks on.</param>
/// <param name="root">The object being validated.</param>
/// <example><c>(end, x) =&gt; end.IfBefore(x.Start)</c></example>
public delegate void GuardChain<T, in TRoot>(Guard<T> guard, TRoot root);
