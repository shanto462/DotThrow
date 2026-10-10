using System;
using System.Collections.Generic;
using System.Threading;
using DotThrow.Dynamic.Internal;
using DotThrow.Internal;

namespace DotThrow.Dynamic;

/// <summary>
/// A custom check for <see cref="DynamicValidator{T}"/>: it gets the guard of the value and the
/// arguments of the check from the definition, and returns the guard like any check.
/// </summary>
/// <typeparam name="T">The type of the checked value.</typeparam>
/// <param name="guard">The guard of the value.</param>
/// <param name="arguments">The arguments of the check in the definition.</param>
/// <returns>The guard returned by the check.</returns>
/// <example><c>(sku, args) =&gt; sku.IfNotStartsWith(args.Value ?? "SKU-", message: args.Message)</c></example>
public delegate Guard<T> CustomCheck<T>(Guard<T> guard, CheckArguments arguments);

/// <summary>
/// Custom checks that definitions can use by name, next to the built-in ones. Create one, add
/// checks, and pass it to <see cref="DynamicValidator{T}"/>. <see cref="Default"/> has no custom
/// checks and is read-only.
/// </summary>
/// <remarks>
/// A custom check is found by its name and the exact value type it was added for; a check
/// added for <see cref="object"/> applies to every type. Names ignore case and cannot be
/// built-in names. Adding checks after a validator was built does not change that validator
/// until it is updated.
/// </remarks>
/// <example>
/// <code>
/// var checks = new CheckRegistry()
///     .Add&lt;string&gt;("NotSku", (sku, args) =&gt; sku.IfNotMatches("^SKU-[0-9]+$", message: args.Message));
/// var validator = new DynamicValidator&lt;Order&gt;(definition, properties, checks);
/// </code>
/// </example>
public sealed class CheckRegistry
{
    private readonly object _gate = new();
    private Dictionary<string, List<Registration>> _checks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Initializes a new instance of the <see cref="CheckRegistry"/> class with no custom checks.</summary>
    public CheckRegistry()
    {
    }

    private CheckRegistry(bool readOnly)
    {
        IsReadOnly = readOnly;
    }

    /// <summary>Gets the registry with only the built-in checks. It is read-only.</summary>
    public static CheckRegistry Default { get; } = new(readOnly: true);

    /// <summary>Gets the names of every built-in check, for tools and documentation. Each is the error code of its check method.</summary>
    public static IReadOnlyCollection<string> BuiltInChecks => CheckNames.All;

    /// <summary>Gets a value indicating whether checks can be added.</summary>
    public bool IsReadOnly { get; }

    /// <summary>Gets the names of the custom checks.</summary>
    public IReadOnlyCollection<string> Names => Volatile.Read(ref _checks).Keys;

    /// <summary>Adds a custom check for values of type <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The value type, or <see cref="object"/> for every type.</typeparam>
    /// <param name="name">The check name used in definitions.</param>
    /// <param name="check">The check.</param>
    /// <returns>This registry.</returns>
    /// <exception cref="InvalidOperationException">The registry is read-only.</exception>
    /// <exception cref="ArgumentException">The name is empty, built in, or already added for <typeparamref name="T"/>.</exception>
    public CheckRegistry Add<T>(string name, CustomCheck<T> check)
    {
        Argument.NotNull(check);
        return Register(name, new Registration<T>(_ => check));
    }

    /// <summary>
    /// Adds a custom check whose arguments are read once, when a validator is built. The factory
    /// gets the arguments and returns the check; a <see cref="FormatException"/> or
    /// <see cref="ArgumentException"/> it throws becomes a definition error.
    /// </summary>
    /// <typeparam name="T">The value type, or <see cref="object"/> for every type.</typeparam>
    /// <param name="name">The check name used in definitions.</param>
    /// <param name="factory">Reads the arguments and returns the check.</param>
    /// <returns>This registry.</returns>
    /// <example>
    /// <code>
    /// checks.Add&lt;int&gt;("Between", args =&gt;
    /// {
    ///     var min = CheckArguments.Parse&lt;int&gt;(args.Min ?? "0");
    ///     var max = CheckArguments.Parse&lt;int&gt;(args.Max ?? "100");
    ///     return (g, _) =&gt; g.IfOutOfRange(min, max, args.Message);
    /// });
    /// </code>
    /// </example>
    public CheckRegistry Add<T>(string name, Func<CheckArguments, CustomCheck<T>> factory)
    {
        Argument.NotNull(factory);
        return Register(name, new Registration<T>(factory));
    }

    internal bool TryCreate<TValue>(string name, CheckArgs args, out GuardStep<TValue>? step, out string canonicalName)
    {
        step = null;
        canonicalName = name;
        if (!Volatile.Read(ref _checks).TryGetValue(name, out var registrations))
        {
            return false;
        }

        foreach (var registration in registrations)
        {
            if (registration is Registration<TValue> exact)
            {
                canonicalName = exact.Name;
                step = exact.Create(args);
                return true;
            }
        }

        foreach (var registration in registrations)
        {
            if (registration is Registration<object> any)
            {
                canonicalName = any.Name;
                step = any.CreateBoxed<TValue>(args);
                return true;
            }
        }

        args.Error($"is a custom check for {Types(registrations)}, not for {TypeText.Of(typeof(TValue))}");
        return true;
    }

    private static string Types(List<Registration> registrations)
    {
        var names = new List<string>();
        foreach (var registration in registrations)
        {
            names.Add(TypeText.Of(registration.ValueType));
        }

        return string.Join(", ", names);
    }

    private CheckRegistry Register(string name, Registration registration)
    {
        if (IsReadOnly)
        {
            throw new InvalidOperationException("The default check registry is read-only. Create a new CheckRegistry for custom checks.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A check name must not be empty.", nameof(name));
        }

        var trimmed = name.Trim();
        if (CheckNames.Find(trimmed) is { } builtIn)
        {
            throw new ArgumentException($"'{trimmed}' is the built-in check '{builtIn}'. Use another name.", nameof(name));
        }

        registration.Name = trimmed;
        lock (_gate)
        {
            var next = new Dictionary<string, List<Registration>>(_checks, StringComparer.OrdinalIgnoreCase);
            var list = next.TryGetValue(trimmed, out var existing) ? new List<Registration>(existing) : new List<Registration>();
            foreach (var item in list)
            {
                if (item.ValueType == registration.ValueType)
                {
                    throw new ArgumentException($"The check '{trimmed}' is already added for {TypeText.Of(registration.ValueType)}.", nameof(name));
                }
            }

            list.Add(registration);
            next[trimmed] = list;
            Volatile.Write(ref _checks, next);
        }

        return this;
    }

    private abstract class Registration
    {
        public string Name { get; set; } = string.Empty;

        public abstract Type ValueType { get; }
    }

    private sealed class Registration<T>(Func<CheckArguments, CustomCheck<T>> factory) : Registration
    {
        public override Type ValueType => typeof(T);

        public GuardStep<T>? Create(CheckArgs args)
        {
            var check = Read(args);
            if (check is null)
            {
                return null;
            }

            var arguments = args.Arguments;
            return g => check(g, arguments);
        }

        // A check for object, used for any value type: the value is boxed for the check.
        public GuardStep<TValue>? CreateBoxed<TValue>(CheckArgs args)
        {
            var check = Read(args);
            if (check is not CustomCheck<object> boxed)
            {
                return null;
            }

            var arguments = args.Arguments;
            return g => boxed(g.Map<object>(g.Value!), arguments).Map(g.Value);
        }

        private CustomCheck<T>? Read(CheckArgs args)
        {
            try
            {
                return factory(args.Arguments);
            }
#pragma warning disable CA1031 // Do not catch general exception types: argument errors become definition errors.
            catch (Exception exception) when (exception is FormatException or ArgumentException or OverflowException)
#pragma warning restore CA1031
            {
                args.Error(exception.Message);
                return null;
            }
        }
    }
}

/// <summary>The arguments of one check in a definition, as given (text), for custom checks.</summary>
public sealed class CheckArguments
{
    internal CheckArguments(CheckDefinition definition)
    {
        Value = definition.Value;
        Min = definition.Min;
        Max = definition.Max;
        Values = definition.Values is { } values ? [.. values] : Array.Empty<string>();
        Comparison = definition.Comparison;
        Options = definition.Options;
        Message = definition.Message;
        ErrorCode = definition.ErrorCode;
    }

    /// <summary>Gets the single argument.</summary>
    public string? Value { get; }

    /// <summary>Gets the lower bound.</summary>
    public string? Min { get; }

    /// <summary>Gets the upper bound.</summary>
    public string? Max { get; }

    /// <summary>Gets the list argument (empty when not given).</summary>
    public IReadOnlyList<string> Values { get; }

    /// <summary>Gets the comparison name.</summary>
    public string? Comparison { get; }

    /// <summary>Gets the options text.</summary>
    public string? Options { get; }

    /// <summary>Gets the message of the check (or of its rule); pass it to the check methods.</summary>
    public string? Message { get; }

    /// <summary>Gets the error code of the check. DotThrow applies it; checks do not need to.</summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Converts argument text to <typeparamref name="TValue"/> with the invariant culture, the same
    /// way built-in checks read their arguments (numbers, dates, TimeSpan, Guid, enums...).
    /// </summary>
    /// <typeparam name="TValue">The target type.</typeparam>
    /// <param name="text">The text.</param>
    /// <returns>The value.</returns>
    /// <exception cref="FormatException">The text is not a valid <typeparamref name="TValue"/>, or the type has no text form.</exception>
    public static TValue Parse<TValue>(string text)
    {
        Argument.NotNull(text);
        if (ValueText.Parser<TValue>() is not { } parser)
        {
            throw new FormatException($"{TypeText.Of(typeof(TValue))} has no text form DotThrow knows.");
        }

        try
        {
            return parser(text);
        }
        catch (TextFormatException exception)
        {
            throw new FormatException(exception.Message);
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
        {
            // The runtime's own message differs between .NET Framework and .NET; ours does not.
            throw new FormatException($"'{text}' is not a valid {TypeText.Of(typeof(TValue))}.");
        }
    }
}
