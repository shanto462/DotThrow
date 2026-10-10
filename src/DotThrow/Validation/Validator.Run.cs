using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DotThrow.Internal;

namespace DotThrow;

// Running the rules. Sync runs never allocate more than the report when the object is
// valid. Async runs stay synchronous (no state machine) until a rule really goes async.
public partial class Validator<T>
{
    /// <summary>Runs the rules of the given groups and returns every failure.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="groups">
    /// The groups to run. With no groups, the rules in <see cref="ValidationGroups.Default"/> run.
    /// Pass <see cref="ValidationGroups.All"/> to run every rule.
    /// </param>
    /// <returns>The report.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/> and <see cref="PreValidate"/> did not stop the run.</exception>
    /// <exception cref="AsyncRuleInvokedSynchronouslyException">A selected rule is async. Use <c>ValidateAsync</c>.</exception>
    public ValidationReport Validate(T value, params string[] groups) =>
        Run(value, null, groups, throwOnFailures: false);

    /// <summary>Runs the rules with the given options and returns every failure.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="options">The options, or <see langword="null"/> for a default run.</param>
    /// <returns>The report.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/> and <see cref="PreValidate"/> did not stop the run.</exception>
    /// <exception cref="AsyncRuleInvokedSynchronouslyException">A selected rule is async. Use <c>ValidateAsync</c>.</exception>
    /// <exception cref="ValidationFailedException">A rule failed and <see cref="ValidationOptions.ThrowOnFailures"/> is set.</exception>
    [OverloadResolutionPriority(1)]
    public ValidationReport Validate(T value, ValidationOptions? options) =>
        Run(value, options, null, options?.ThrowOnFailures ?? false);

    /// <summary>
    /// Runs the rules with options set in a callback, like FluentValidation's
    /// <c>Validate(instance, options =&gt; ...)</c>:
    /// <c>validator.Validate(order, o =&gt; o.IncludeProperties(x =&gt; x.Email).IncludeGroups("checkout"))</c>.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="configure">Sets the options.</param>
    /// <returns>The report.</returns>
    public ValidationReport Validate(T value, Action<ValidationOptions<T>> configure) => Validate(value, ValidationOptions<T>.Create(configure));

    /// <summary>Runs the default rules, including async rules, and returns every failure.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>The report. The task is already complete when every rule that ran is synchronous.</returns>
    public Task<ValidationReport> ValidateAsync(T value, CancellationToken cancellationToken = default) =>
        RunAsync(value, null, null, throwOnFailures: false, cancellationToken);

    /// <summary>Runs the rules of one group, including async rules, and returns every failure.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="group">The group to run.</param>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>The report.</returns>
    public Task<ValidationReport> ValidateAsync(T value, string group, CancellationToken cancellationToken = default) =>
        RunAsync(value, null, group is null ? null : [group], throwOnFailures: false, cancellationToken);

    /// <summary>Runs the rules of the given groups, including async rules, and returns every failure.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="groups">The groups to run. With <see langword="null"/> or no groups, the rules in <see cref="ValidationGroups.Default"/> run.</param>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>The report.</returns>
    public Task<ValidationReport> ValidateAsync(T value, IEnumerable<string>? groups, CancellationToken cancellationToken = default) =>
        RunAsync(value, null, AsList(groups), throwOnFailures: false, cancellationToken);

    /// <summary>Runs the rules with the given options, including async rules, and returns every failure.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="options">The options, or <see langword="null"/> for a default run.</param>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>The report. The task is already complete when every rule that ran is synchronous.</returns>
    [OverloadResolutionPriority(1)]
    public Task<ValidationReport> ValidateAsync(T value, ValidationOptions? options, CancellationToken cancellationToken = default) =>
        RunAsync(value, options, null, options?.ThrowOnFailures ?? false, cancellationToken);

    /// <summary>Runs the rules, including async rules, with options set in a callback.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="configure">Sets the options.</param>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>The report.</returns>
    public Task<ValidationReport> ValidateAsync(T value, Action<ValidationOptions<T>> configure, CancellationToken cancellationToken = default) =>
        ValidateAsync(value, ValidationOptions<T>.Create(configure), cancellationToken);

    /// <summary>Runs the rules of the given groups and throws when any rule fails.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="groups">The groups to run. With no groups, the rules in <see cref="ValidationGroups.Default"/> run.</param>
    /// <exception cref="ValidationFailedException">At least one rule failed (or the exception from <see cref="CreateValidationException"/>).</exception>
    public void ValidateAndThrow(T value, params string[] groups) =>
        Run(value, null, groups, throwOnFailures: true);

    /// <summary>Runs the default rules, including async rules, and throws when any rule fails.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>A task that completes when validation passes.</returns>
    /// <exception cref="ValidationFailedException">At least one rule failed.</exception>
    public Task ValidateAndThrowAsync(T value, CancellationToken cancellationToken = default) =>
        RunAsync(value, null, null, throwOnFailures: true, cancellationToken);

    /// <summary>Runs the rules of the given groups, including async rules, and throws when any rule fails.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="groups">The groups to run. With <see langword="null"/> or no groups, the rules in <see cref="ValidationGroups.Default"/> run.</param>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>A task that completes when validation passes.</returns>
    /// <exception cref="ValidationFailedException">At least one rule failed.</exception>
    public Task ValidateAndThrowAsync(T value, IEnumerable<string>? groups, CancellationToken cancellationToken = default) =>
        RunAsync(value, null, AsList(groups), throwOnFailures: true, cancellationToken);

    /// <summary>Returns <see langword="true"/> when this validator can validate objects of <paramref name="type"/> (<typeparamref name="T"/> or a type derived from it).</summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> when <paramref name="type"/> can be assigned to <typeparamref name="T"/>.</returns>
    public bool CanValidate(Type type)
    {
        Argument.NotNull(type);
        return typeof(T).IsAssignableFrom(type);
    }

    /// <summary>Describes the rules of this validator, for tools, documentation and tests.</summary>
    /// <returns>A snapshot of the rules.</returns>
    public ValidatorDescriptor Describe()
    {
        var rules = Volatile.Read(ref _rules);
        return new(GetType(), typeof(T), Descriptors.Of(rules), ((IAsyncRuleInfo)this).HasAsyncRules(new HashSet<object>()));
    }

    bool IAsyncRuleInfo.HasAsyncRules(HashSet<object> visited) => visited.Add(this) && AsyncRules.Of(Volatile.Read(ref _rules), visited);

    ValidationReport IValidator.Validate(object instance, ValidationOptions? options) => Validate(Cast(instance), options);

    Task<ValidationReport> IValidator.ValidateAsync(object instance, ValidationOptions? options, CancellationToken cancellationToken) =>
        ValidateAsync(Cast(instance), options, cancellationToken);

    void IChildValidator<T>.RunAsChild(T instance, in ChildScope scope) => RunRules(new ValidationContext<T>(instance, scope));

    Task IChildValidator<T>.RunAsChildAsync(T instance, ChildScope scope) => RunRulesAsync(new ValidationContext<T>(instance, scope));

    /// <summary>
    /// Runs before the rules, also for child validators. Return <see langword="false"/> to skip
    /// the rules; failures added through <paramref name="context"/> stay in the report. It also
    /// runs for a <see langword="null"/> object, so it can report one instead of the default
    /// <see cref="ArgumentNullException"/>.
    /// </summary>
    /// <param name="context">The context of the run.</param>
    /// <returns><see langword="true"/> to run the rules.</returns>
    /// <example>
    /// <code>
    /// protected override bool PreValidate(ValidationContext&lt;Order&gt; context)
    /// {
    ///     if (context.Instance is null)
    ///     {
    ///         context.AddFailure("Please send an order.");
    ///         return false;
    ///     }
    ///
    ///     return true;
    /// }
    /// </code>
    /// </example>
    protected virtual bool PreValidate(ValidationContext<T> context) => true;

    /// <summary>
    /// Creates the exception thrown by <c>ValidateAndThrow</c> and by runs with
    /// <see cref="ValidationOptions.ThrowOnFailures"/> (FluentValidation's <c>RaiseValidationException</c>).
    /// </summary>
    /// <param name="report">The report with the failures.</param>
    /// <returns>The exception to throw. The default is a <see cref="ValidationFailedException"/>.</returns>
    protected virtual Exception CreateValidationException(ValidationReport report) => new ValidationFailedException(report);

    private static T Cast(object? instance) => instance switch
    {
        // A value type cannot be null; only Nullable<T> validators accept null here.
        null when default(T) is not null => throw new ArgumentNullException(nameof(instance)),
        null => default!,
        T typed => typed,
        _ => throw new ArgumentException(
            $"This validator validates {TypeNames.Display(typeof(T))}, not {TypeNames.Display(instance.GetType())}.",
            nameof(instance)),
    };

    private static IReadOnlyList<string>? AsList(IEnumerable<string>? groups) => groups switch
    {
        null => null,
        IReadOnlyList<string> list => list,
        _ => [.. groups],
    };

    private ValidationReport Run(T value, ValidationOptions? options, IReadOnlyList<string>? groups, bool throwOnFailures)
    {
        var report = new ValidationReport();
        var context = ValidationContext<T>.Rent(value, report, options, groups, isAsync: false, default);
        RunRules(context);
        context.Return();
        if (throwOnFailures && !report.IsValid)
        {
            throw CreateValidationException(report);
        }

        return report;
    }

    private Task<ValidationReport> RunAsync(T value, ValidationOptions? options, IReadOnlyList<string>? groups, bool throwOnFailures, CancellationToken cancellationToken)
    {
        ValidationContext<T> context;
        Task task;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            context = ValidationContext<T>.Rent(value, new ValidationReport(), options, groups, isAsync: true, cancellationToken);
            task = RunRulesAsync(context);
        }
#pragma warning disable CA1031 // Do not catch general exception types: the exception moves into the returned task, like an async method.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return TaskHelpers.FromException<ValidationReport>(exception);
        }

        return task.IsDone() ? Complete(context, throwOnFailures) : CompleteAsync(task, context, throwOnFailures);
    }

    private Task<ValidationReport> Complete(ValidationContext<T> context, bool throwOnFailures)
    {
        var report = context.Report;
        context.Return();
        if (throwOnFailures && !report.IsValid)
        {
#pragma warning disable CA1031 // Do not catch general exception types: the exception moves into the returned task.
            try
            {
                return TaskHelpers.FromException<ValidationReport>(CreateValidationException(report));
            }
            catch (Exception exception)
            {
                return TaskHelpers.FromException<ValidationReport>(exception);
            }
#pragma warning restore CA1031
        }

        return Task.FromResult(report);
    }

    private async Task<ValidationReport> CompleteAsync(Task task, ValidationContext<T> context, bool throwOnFailures)
    {
        await task.ConfigureAwait(false);
        var report = context.Report;
        context.Return();
        if (throwOnFailures && !report.IsValid)
        {
            throw CreateValidationException(report);
        }

        return report;
    }

    private void RunRules(ValidationContext<T> context)
    {
        if (!RunPreValidate(context))
        {
            return;
        }

        Argument.NotNull(context.Instance, "value");
        RuleList<T>.Run(Volatile.Read(ref _rules), context, StopsOnFailure(context));
    }

    private Task RunRulesAsync(ValidationContext<T> context)
    {
        if (!RunPreValidate(context))
        {
            return Task.CompletedTask;
        }

        Argument.NotNull(context.Instance, "value");
        return RuleList<T>.RunAsync(Volatile.Read(ref _rules), 0, context, StopsOnFailure(context));
    }

    // Failures added through the context in PreValidate get the context path. Rules run one
    // after another (child validators run inside a rule that does not use the frame), so a
    // frame is set when a rule starts and cleared when it ends, without saving the old one.
    private bool RunPreValidate(ValidationContext<T> context)
    {
        if (!_callsPreValidate)
        {
            return true;
        }

        var run = context.Run;
        run.Frame = new RuleFrame(ContextSink.Instance, context, -1, null);
        var proceed = PreValidate(context);
        run.Frame = default;
        run.ClearArguments();
        return proceed;
    }

    private bool StopsOnFailure(ValidationContext<T> context) =>
        (context.ClassCascadeOverride ?? ClassCascade) == CascadeMode.Stop;
}
