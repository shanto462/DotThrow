using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using DotThrow.Dynamic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace DotThrow.DependencyInjection.Internal;

// One dynamic validator in a service provider: the DynamicValidator<T>, the IValidator<T> that is
// registered (the dynamic validator, or a code validator that includes it), and the change token
// registration that reloads the rules. The container disposes it, which stops the reloads.
internal sealed class DynamicValidatorRegistration<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T> : IDisposable
{
    private static readonly Action<ILogger, string, Exception?> LogReloadFailed = LoggerMessage.Define<string>(
        LogLevel.Error, new EventId(1, "DynamicRulesReloadFailed"), "The rules of DynamicValidator<{Model}> were not reloaded; the old rules stay in use.");

    private static readonly Action<ILogger, string, Exception?> LogReloaded = LoggerMessage.Define<string>(
        LogLevel.Information, new EventId(2, "DynamicRulesReloaded"), "The rules of DynamicValidator<{Model}> were reloaded.");

    private static readonly Action<ILogger, string, Exception?> LogHandlerFailed = LoggerMessage.Define<string>(
        LogLevel.Error, new EventId(3, "DynamicRulesReloadHandlerFailed"), "OnReloadError of DynamicValidator<{Model}> threw.");

    private readonly object _gate = new();
    private readonly IConfiguration? _section;
    private readonly Action<RuleDefinitionException, IServiceProvider>? _onReloadError;
    private readonly IServiceProvider _services;
    private readonly ILogger? _logger;
    private readonly IDisposable? _reload;
    private string? _snapshot;

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
        _logger = services.GetService<ILoggerFactory>()?.CreateLogger<DynamicValidator<T>>();

        // Take the change token and the snapshot before reading, so a change while the validator
        // is built is not missed.
        IChangeToken? first = section?.GetReloadToken();
        _snapshot = section is null ? null : Snapshot(section);
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

    // The section as sorted key/value text: a reload of the configuration that did not touch the
    // section (another section, or the same values again) changes nothing and reports nothing.
    internal static string Snapshot(IConfiguration section)
    {
        var entries = new List<KeyValuePair<string, string?>>(section.AsEnumerable(makePathsRelative: true));
        entries.Sort(static (a, b) => string.CompareOrdinal(a.Key, b.Key));
        var text = new StringBuilder();
        foreach (var entry in entries)
        {
            text.Append(entry.Key).Append('=').Append(entry.Value is null ? "\u0001" : entry.Value).Append('\n');
        }

        return text.ToString();
    }

    // Runs on the thread that reloaded the configuration (a file watcher, for example), so it never throws.
    private void Reload()
    {
        lock (_gate)
        {
            RuleDefinitionException? error;
            try
            {
                var snapshot = Snapshot(_section!);
                if (string.Equals(snapshot, _snapshot, StringComparison.Ordinal))
                {
                    return;
                }

                _snapshot = snapshot;
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
                if (_logger is not null)
                {
                    LogReloaded(_logger, typeof(T).Name, null);
                }

                return;
            }

            Report(error);
        }
    }

    private void Report(RuleDefinitionException error)
    {
        if (_logger is not null)
        {
            LogReloadFailed(_logger, typeof(T).Name, error);
        }
        else if (_onReloadError is null)
        {
            Trace.TraceError("DotThrow: the rules of DynamicValidator<{0}> were not reloaded; the old rules stay. {1}", typeof(T).Name, error.Message);
        }

        if (_onReloadError is null)
        {
            return;
        }

        try
        {
            _onReloadError(error, _services);
        }
        catch (Exception exception)
        {
            if (_logger is not null)
            {
                LogHandlerFailed(_logger, typeof(T).Name, exception);
            }
            else
            {
                Trace.TraceError("DotThrow: OnReloadError of DynamicValidator<{0}> threw. {1}", typeof(T).Name, exception);
            }
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
