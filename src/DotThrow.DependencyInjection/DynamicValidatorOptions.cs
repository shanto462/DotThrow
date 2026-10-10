using System;
using DotThrow.Dynamic;

namespace DotThrow.DependencyInjection;

/// <summary>
/// Settings of a dynamic validator registered with <c>AddDynamicValidator&lt;T&gt;(...)</c>.
/// </summary>
/// <typeparam name="T">The validated type.</typeparam>
/// <example>
/// <code>
/// services.AddDynamicValidator&lt;SignUp&gt;(configuration.GetSection("Validation:SignUp"), o =&gt;
/// {
///     o.Checks = new CheckRegistry().Add&lt;string&gt;("NotReserved", (name, _) =&gt; name.IfOneOf("admin", "root"));
///     o.CodeRules = v =&gt; v.For(x =&gt; x.Password, p =&gt; p.IfNullOrEmpty().IfShorterThan(12));
///     o.OnReloadError = (error, services) =&gt;
///         services.GetRequiredService&lt;ILogger&lt;SignUp&gt;&gt;().LogError(error, "The SignUp rules were not reloaded.");
/// });
/// </code>
/// </example>
public sealed class DynamicValidatorOptions<T>
{
    /// <summary>Gets or sets the custom checks the definitions may use, or <see langword="null"/> for the built-in checks only.</summary>
    public CheckRegistry? Checks { get; set; }

    /// <summary>
    /// Gets or sets rules written in code that run together with the dynamic rules (code rules first).
    /// The registered <see cref="IValidator{T}"/> is then a <see cref="Validator{T}"/> with these rules
    /// that includes the <see cref="DynamicValidator{T}"/>; a reload changes only the dynamic part.
    /// </summary>
    public Action<Validator<T>>? CodeRules { get; set; }

    /// <summary>
    /// Gets or sets what happens when changed configuration has errors. The old rules stay in use.
    /// The callback gets every error (<see cref="RuleDefinitionException.Errors"/>) and the root
    /// service provider, for example to resolve a logger. When it is <see langword="null"/>, the
    /// error is written with <see cref="System.Diagnostics.Trace.TraceError(string)"/>.
    /// </summary>
    public Action<RuleDefinitionException, IServiceProvider>? OnReloadError { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a host (Microsoft.Extensions.Hosting 8 or later) builds
    /// the validator when it starts, so a definition with errors stops the app at startup with an
    /// <c>OptionsValidationException</c> that lists every error. The default is <see langword="true"/>.
    /// Without a host the validator is built when it is first resolved.
    /// </summary>
    public bool ValidateOnStart { get; set; } = true;
}
