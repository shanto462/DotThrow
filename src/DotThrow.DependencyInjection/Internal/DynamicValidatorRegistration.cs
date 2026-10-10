using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using DotThrow.Dynamic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace DotThrow.DependencyInjection.Internal;

// One dynamic validator in a service provider: the DynamicValidator<T>, the IValidator<T> that is
// registered (the dynamic validator, or a code validator that includes it), and the change token
// registration that reloads the rules. The container disposes it, which stops the reloads.
internal sealed class DynamicValidatorRegistration<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T> : IDisposable
{
    private readonly IConfiguration? _section;
    private readonly Action<RuleDefinitionException, IServiceProvider>? _onReloadError;
    private readonly IServiceProvider _services;
    private readonly IDisposable? _reload;

    internal DynamicValidatorRegistration(
        Func<RuleSetDefinition, CheckRegistry?, DynamicValidator<T>> create,
        RuleSetDefinition? definition,
        IConfiguration? section,
        DynamicValidatorOptions<T> options,
        IServiceProvider services)
    {
        _section = section;
        _onReloadError = options.OnReloadError;
        _services = services;

        // Take the change token before reading, so a change while the validator is built is not missed.
        IChangeToken? first = section?.GetReloadToken();
        Dynamic = create(definition ?? RuleSetConfiguration.Read(section!), options.Checks);
        if (options.CodeRules is { } codeRules)
        {
            var combined = new Validator<T>();
            codeRules(combined);
            combined.Include(Dynamic);
            Validator = combined;
        }
        else
        {
            Validator = Dynamic;
        }

        if (section is not null)
        {
            _reload = ChangeToken.OnChange(
                () =>
                {
                    var token = first ?? section.GetReloadToken();
                    first = null;
                    return token;
                },
                Reload);
        }
    }

    public DynamicValidator<T> Dynamic { get; }

    public IValidator<T> Validator { get; }

    public void Dispose() => _reload?.Dispose();

    // Runs on the thread that reloaded the configuration (a file watcher, for example), so it never throws.
    private void Reload()
    {
        RuleDefinitionException? error;
        try
        {
            error = Dynamic.TryUpdate(RuleSetConfiguration.Read(_section!), out var errors) ? null : new RuleDefinitionException(errors);
        }
        catch (RuleDefinitionException exception)
        {
            error = exception;
        }
#pragma warning disable CA1031 // Do not catch general exception types: an exception here would end the process.
        catch (Exception exception)
        {
            error = new RuleDefinitionException("Reloading the rules failed: " + exception.Message, exception);
        }

        if (error is null)
        {
            return;
        }

        try
        {
            if (_onReloadError is null)
            {
                Trace.TraceError("DotThrow: the rules of DynamicValidator<{0}> were not reloaded; the old rules stay. {1}", typeof(T).Name, error.Message);
            }
            else
            {
                _onReloadError(error, _services);
            }
        }
        catch (Exception exception)
        {
            Trace.TraceError("DotThrow: OnReloadError of DynamicValidator<{0}> threw. {1}", typeof(T).Name, exception);
        }
#pragma warning restore CA1031
    }
}

// The options type that makes ValidateOnStart build a dynamic validator when the host starts.
internal sealed class DynamicValidatorStartup<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>
{
}

internal sealed class DynamicValidatorStartupCheck<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(IServiceProvider services)
    : IValidateOptions<DynamicValidatorStartup<T>>
{
    public ValidateOptionsResult Validate(string? name, DynamicValidatorStartup<T> options)
    {
        try
        {
            _ = services.GetService(typeof(DynamicValidatorRegistration<T>));
            return ValidateOptionsResult.Success;
        }
        catch (RuleDefinitionException exception)
        {
            var failures = new string[exception.Errors.Count];
            for (var i = 0; i < failures.Length; i++)
            {
                failures[i] = $"DynamicValidator<{typeof(T).Name}>: {exception.Errors[i]}";
            }

            return ValidateOptionsResult.Fail(failures);
        }
    }
}
