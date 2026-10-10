using System;
using DotThrow;
using DotThrow.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Validates options classes with DotThrow validators, the DotThrow version of
/// <c>ValidateDataAnnotations()</c>. Combine it with <c>ValidateOnStart()</c> so a bad
/// configuration stops the app at startup instead of at first use.
/// </summary>
/// <example>
/// <code>
/// builder.Services.AddValidator&lt;SmtpOptions, SmtpOptionsValidator&gt;(ServiceLifetime.Singleton);
/// builder.Services.AddOptions&lt;SmtpOptions&gt;()
///     .BindConfiguration("Smtp")
///     .ValidateWithDotThrow()
///     .ValidateOnStart();
/// </code>
/// </example>
public static class DotThrowOptionsBuilderExtensions
{
    /// <summary>
    /// Validates the options with the <see cref="IValidator{T}"/> of <typeparamref name="TOptions"/>
    /// registered in the service collection (any lifetime). The options fail with one message per
    /// failure, named by property path, for example <c>"SmtpOptions.Port: ..."</c>.
    /// </summary>
    /// <typeparam name="TOptions">The options class.</typeparam>
    /// <param name="builder">The options builder.</param>
    /// <returns>The options builder.</returns>
    public static OptionsBuilder<TOptions> ValidateWithDotThrow<TOptions>(this OptionsBuilder<TOptions> builder)
        where TOptions : class
    {
        builder.Throw().IfNull();
        var name = builder.Name;
        builder.Services.AddSingleton<IValidateOptions<TOptions>>(services => new DotThrowValidateOptions<TOptions>(name, services));
        return builder;
    }

    /// <summary>Validates the options with the given validator.</summary>
    /// <typeparam name="TOptions">The options class.</typeparam>
    /// <param name="builder">The options builder.</param>
    /// <param name="validator">The validator. It is shared, so it must be thread-safe (every <see cref="Validator{T}"/> is).</param>
    /// <returns>The options builder.</returns>
    public static OptionsBuilder<TOptions> ValidateWithDotThrow<TOptions>(this OptionsBuilder<TOptions> builder, IValidator<TOptions> validator)
        where TOptions : class
    {
        builder.Throw().IfNull();
        validator.Throw().IfNull();
        builder.Services.AddSingleton<IValidateOptions<TOptions>>(new DotThrowValidateOptions<TOptions>(builder.Name, validator));
        return builder;
    }

    /// <summary>Validates the options with rules written inline. The validator is built once, here.</summary>
    /// <typeparam name="TOptions">The options class.</typeparam>
    /// <param name="builder">The options builder.</param>
    /// <param name="configure">Adds the rules, for example <c>v =&gt; v.For(x =&gt; x.Port, port =&gt; port.IfOutOfRange(1, 65535))</c>.</param>
    /// <returns>The options builder.</returns>
    /// <example>
    /// <code>
    /// services.AddOptions&lt;SmtpOptions&gt;()
    ///     .BindConfiguration("Smtp")
    ///     .ValidateWithDotThrow(v =&gt; v
    ///         .For(x =&gt; x.Host, host =&gt; host.IfNullOrWhiteSpace())
    ///         .For(x =&gt; x.Port, port =&gt; port.IfOutOfRange(1, 65535)))
    ///     .ValidateOnStart();
    /// </code>
    /// </example>
    public static OptionsBuilder<TOptions> ValidateWithDotThrow<TOptions>(this OptionsBuilder<TOptions> builder, Action<Validator<TOptions>> configure)
        where TOptions : class
    {
        builder.Throw().IfNull();
        configure.Throw().IfNull();
        var validator = new Validator<TOptions>();
        configure(validator);
        return builder.ValidateWithDotThrow(validator);
    }
}
