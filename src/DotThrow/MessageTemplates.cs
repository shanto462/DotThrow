using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using DotThrow.Internal;

namespace DotThrow;

/// <summary>
/// The message templates of the built-in checks, keyed by error code. Change a template to
/// reword a default message, or add one per culture to translate it.
/// </summary>
/// <remarks>
/// <para>
/// A template is text with named placeholders in braces, for example
/// <c>"'{PropertyName}' must be at most {MaxLength} characters long."</c>. The names follow
/// FluentValidation where it has one: <c>{PropertyName}</c>, <c>{PropertyValue}</c>,
/// <c>{ComparisonValue}</c>, <c>{From}</c>, <c>{To}</c>, <c>{MinLength}</c>,
/// <c>{MaxLength}</c>, <c>{TotalLength}</c>, <c>{ExpectedPrecision}</c>,
/// <c>{ExpectedScale}</c>, <c>{Digits}</c> and <c>{ActualScale}</c>. DotThrow adds
/// <c>{Length}</c>, <c>{Count}</c>, <c>{MinCount}</c>, <c>{MaxCount}</c>, <c>{Type}</c>,
/// <c>{Pattern}</c>, <c>{Value}</c>, <c>{Condition}</c>, <c>{Divisor}</c>, <c>{MaxAge}</c>,
/// <c>{MinAge}</c>, <c>{Key}</c>, <c>{Flag}</c>, <c>{Schemes}</c> and <c>{Characters}</c>.
/// Each check documents the placeholders it fills in. A placeholder can carry a format,
/// for example <c>{From:N2}</c>. A placeholder without a value stays in the text as written.
/// </para>
/// <para>
/// Values are written with the invariant culture: strings in single quotes, <see langword="null"/>
/// as <c>null</c>, dates in the round-trip format. The default templates never contain the
/// checked value. <c>{PropertyValue}</c> shows it only when you write it in your own
/// template or <c>message</c>, so think about secrets before you do.
/// </para>
/// <para>
/// A few error codes have variants, keyed <c>"Code.Variant"</c>, for wording that depends on
/// the arguments, for example <c>"CountEqualTo.One"</c> for a count of exactly one item, or
/// <c>"EqualTo.Value"</c>, which shows the comparison value and is used for value types only.
/// Setting the plain code replaces the variants too, unless a variant is set as well.
/// </para>
/// <para>
/// The template for a failure is looked up in this order, trying the variant before the
/// plain code at each step: templates set for the message culture and its parents (for
/// example <c>de-AT</c>, then <c>de</c>); templates set for every culture; built-in
/// translations for the culture and its parents; the English defaults.
/// </para>
/// <para>
/// Every member is thread-safe. Reading a template takes no lock, and a resolved template is
/// cached per culture, so a failure does not parse or search anything twice.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// MessageTemplates.Set(ErrorCodes.LongerThan, "{PropertyName} is too long (max {MaxLength}).");
/// MessageTemplates.Set(ErrorCodes.Null, "'{PropertyName}' darf nicht leer sein.", new CultureInfo("de"));
/// </code>
/// </example>
public static class MessageTemplates
{
    private static readonly object WriteLock = new();
    private static readonly MessageTemplate Fallback = MessageTemplate.Parse("'{PropertyName}' is not valid.");

    private static State _state = new(new Dictionary<string, Dictionary<string, MessageTemplate>>(StringComparer.OrdinalIgnoreCase));
    private static CultureInfo? _culture;

    /// <summary>
    /// Gets or sets the culture of the messages. <see langword="null"/> (the default) means
    /// <see cref="CultureInfo.CurrentUICulture"/>, read when a check fails, so each request
    /// or thread can have its own language.
    /// </summary>
    public static CultureInfo? Culture
    {
        get => Volatile.Read(ref _culture);
        set => Volatile.Write(ref _culture, value);
    }

    /// <summary>Sets the template for an error code, for one culture or for every culture.</summary>
    /// <param name="key">An error code such as <see cref="ErrorCodes.LongerThan"/>, a variant key such as <c>"CountEqualTo.One"</c>, or your own code.</param>
    /// <param name="template">The template, with placeholders such as <c>{PropertyName}</c>.</param>
    /// <exception cref="ArgumentException">A placeholder has a format that no value can use, such as <c>{From:Q}</c>.</exception>
    /// <param name="culture">
    /// The culture, for example <c>new CultureInfo("de")</c> (it also serves <c>de-AT</c>), or
    /// <see langword="null"/> (or the invariant culture) for every culture. A template for a
    /// culture wins over a template for every culture, which wins over the built-in ones.
    /// </param>
    public static void Set(string key, string template, CultureInfo? culture = null)
    {
        Argument.NotNullOrEmpty(key);
        Argument.NotNull(template);
        if (!MessageTemplate.TryValidate(template, out var error))
        {
            throw new ArgumentException(error, nameof(template));
        }

        var parsed = MessageTemplate.Parse(template);
        var cultureName = culture?.Name ?? string.Empty;
        lock (WriteLock)
        {
            var overrides = new Dictionary<string, Dictionary<string, MessageTemplate>>(_state.Overrides, StringComparer.OrdinalIgnoreCase);
            var forCulture = overrides.TryGetValue(cultureName, out var existing)
                ? new Dictionary<string, MessageTemplate>(existing, StringComparer.Ordinal)
                : new Dictionary<string, MessageTemplate>(StringComparer.Ordinal);
            forCulture[key] = parsed;
            overrides[cultureName] = forCulture;
            Volatile.Write(ref _state, new State(overrides));
        }
    }

    /// <summary>
    /// Gets the template that a failure with this key would use in a culture, after the
    /// whole fallback chain.
    /// </summary>
    /// <param name="key">An error code or a variant key.</param>
    /// <param name="culture">The culture, or <see langword="null"/> for <see cref="Culture"/> (and then <see cref="CultureInfo.CurrentUICulture"/>).</param>
    /// <returns>The template text, or <see langword="null"/> when no template exists for the key.</returns>
    public static string? Get(string key, CultureInfo? culture = null)
    {
        Argument.NotNullOrEmpty(key);
        return Lookup(Volatile.Read(ref _state), culture ?? Culture ?? CultureInfo.CurrentUICulture, TemplateKeys.CodeOf(key), key)?.Text;
    }

    /// <summary>Removes a template that <see cref="Set"/> added.</summary>
    /// <param name="key">The key passed to <see cref="Set"/>.</param>
    /// <param name="culture">The culture passed to <see cref="Set"/>.</param>
    /// <returns><see langword="true"/> when a template was removed.</returns>
    public static bool Remove(string key, CultureInfo? culture = null)
    {
        Argument.NotNullOrEmpty(key);
        var cultureName = culture?.Name ?? string.Empty;
        lock (WriteLock)
        {
            if (!_state.Overrides.TryGetValue(cultureName, out var existing) || !existing.ContainsKey(key))
            {
                return false;
            }

            var overrides = new Dictionary<string, Dictionary<string, MessageTemplate>>(_state.Overrides, StringComparer.OrdinalIgnoreCase);
            var forCulture = new Dictionary<string, MessageTemplate>(existing, StringComparer.Ordinal);
            forCulture.Remove(key);
            if (forCulture.Count == 0)
            {
                overrides.Remove(cultureName);
            }
            else
            {
                overrides[cultureName] = forCulture;
            }

            Volatile.Write(ref _state, new State(overrides));
            return true;
        }
    }

    /// <summary>
    /// Removes every template added with <see cref="Set"/> and sets <see cref="Culture"/> back
    /// to <see langword="null"/>. The built-in templates and translations stay.
    /// </summary>
    public static void Reset()
    {
        lock (WriteLock)
        {
            Volatile.Write(ref _state, new State(new Dictionary<string, Dictionary<string, MessageTemplate>>(StringComparer.OrdinalIgnoreCase)));
            Culture = null;
        }
    }

    /// <summary>
    /// Fills in the placeholders of a template, the same way the built-in checks do. Use it in
    /// your own checks so their messages can be reworded and translated too.
    /// </summary>
    /// <param name="template">The template, for example the result of <see cref="Get"/>.</param>
    /// <param name="propertyName">The value for <c>{PropertyName}</c>, usually <see cref="Guard{T}.Name"/>.</param>
    /// <param name="values">Values for other placeholders by name, or <see langword="null"/>. Strings are written in single quotes.</param>
    /// <returns>The message.</returns>
    /// <example>
    /// <code>
    /// var template = message ?? MessageTemplates.Get("NotSku") ?? "'{PropertyName}' must be a SKU.";
    /// return guard.Fail("NotSku", MessageTemplates.Format(template, guard.Name));
    /// </code>
    /// </example>
    public static string Format(string template, string propertyName, IReadOnlyDictionary<string, object?>? values = null)
    {
        Argument.NotNull(template);
        Argument.NotNull(propertyName);
        return MessageTemplate.ForCustomMessage(template).Render(propertyName, default(Unset), new DictionaryArgs(values));
    }

    // Finds the template for a failure in the message culture. Called only when a check
    // fails; the result is cached per culture until the templates change. The last culture
    // used is remembered by reference, so the common case (one culture) does no string
    // hashing at all: the keys are the library's own string constants, compared by reference.
    internal static MessageTemplate Resolve(string code, string key)
    {
        var culture = Culture ?? CultureInfo.CurrentUICulture;
        var state = Volatile.Read(ref _state);
        var cache = Volatile.Read(ref state.Last);
        if (cache is null || !ReferenceEquals(cache.Culture, culture))
        {
            if (!state.Resolved.TryGetValue(culture.Name, out cache))
            {
                cache = state.Resolved.GetOrAdd(culture.Name, new CultureCache(culture));
            }

            Volatile.Write(ref state.Last, cache);
        }

        if (!cache.Templates.TryGetValue(key, out var template))
        {
            template = cache.Templates.GetOrAdd(key, Lookup(state, culture, code, key) ?? Fallback);
        }

        return template;
    }

    private static MessageTemplate? Lookup(State state, CultureInfo culture, string code, string key)
    {
        if (state.Overrides.Count > 0)
        {
            var c = culture;
            for (var depth = 0; c.Name.Length > 0 && depth < MaxCultureDepth; depth++)
            {
                if (state.Overrides.TryGetValue(c.Name, out var forCulture) && Find(forCulture, code, key) is { } found)
                {
                    return found;
                }

                c = c.Parent;
            }

            if (state.Overrides.TryGetValue(string.Empty, out var forAll) && Find(forAll, code, key) is { } general)
            {
                return general;
            }
        }

        var current = culture;
        for (var depth = 0; current.Name.Length > 0 && depth < MaxCultureDepth; depth++)
        {
            if (Translations.Find(current.Name) is { } translation && Find(translation, code, key) is { } translated)
            {
                return MessageTemplate.Parse(translated);
            }

            current = current.Parent;
        }

        return Find(DefaultTemplates.All, code, key) is { } english ? MessageTemplate.Parse(english) : null;
    }

    private static TValue? Find<TValue>(IReadOnlyDictionary<string, TValue> templates, string code, string key)
        where TValue : class
    {
        if (key.Length != code.Length && templates.TryGetValue(key, out var variant))
        {
            return variant;
        }

        return templates.TryGetValue(code, out var plain) ? plain : null;
    }

    // Guards against a culture whose parent chain never reaches the invariant culture.
    private const int MaxCultureDepth = 8;

    // Templates set by the application, by culture name ("" for every culture). Replaced as
    // a whole on every change (copy on write), so readers never lock.
    private sealed class State
    {
        // The cache of the culture used last. A race only costs an extra lookup.
        public CultureCache? Last;

        public State(Dictionary<string, Dictionary<string, MessageTemplate>> overrides)
        {
            Overrides = overrides;
        }

        public Dictionary<string, Dictionary<string, MessageTemplate>> Overrides { get; }

        public ConcurrentDictionary<string, CultureCache> Resolved { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class CultureCache
    {
        public CultureCache(CultureInfo culture)
        {
            Culture = culture;
        }

        public CultureInfo Culture { get; }

        public ConcurrentDictionary<string, MessageTemplate> Templates { get; } = new(ReferenceComparer.Instance);
    }

    // Keys come from the library's string constants, which are interned, so comparing them by
    // reference is exact. A key with the same text but another instance only gets its own entry.
    private sealed class ReferenceComparer : IEqualityComparer<string>
    {
        public static readonly ReferenceComparer Instance = new();

        public bool Equals(string? x, string? y) => ReferenceEquals(x, y);

        public int GetHashCode(string obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
