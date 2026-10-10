using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading.Tasks;

namespace DotThrow.AspNetCore.Tests;

public sealed class SignUp
{
    public string? Name { get; set; }

    public string? Email { get; set; }

    public int Age { get; set; }
}

public class BaseItem
{
    public string? Title { get; set; }
}

public sealed class Item : BaseItem
{
    public int Count { get; set; }
}

public sealed class Filter
{
    public int Max { get; set; }
}

// Binds from the query string through TryParse, so one handler can take two of them.
public sealed class Code
{
    private Code(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static bool TryParse(string? text, IFormatProvider? provider, [NotNullWhen(true)] out Code? code)
    {
        code = text is null ? null : new Code(text);
        return code is not null;
    }

    public override string ToString() => Value;
}

public sealed class SignUpValidator : Validator<SignUp>
{
    public SignUpValidator()
    {
        For(x => x.Name, name => name.IfNullOrWhiteSpace());
        For(x => x.Email, email => email.IfNullOrWhiteSpace().IfNotEmailAddress());
        For(x => x.Age, age => age.IfLessThan(18), rule => rule.InGroup("adult").WithSeverity(Severity.Warning).WithErrorCode("TooYoung"));
    }
}

public sealed class BaseItemValidator : Validator<BaseItem>
{
    public BaseItemValidator()
    {
        For(x => x.Title, title => title.IfNullOrEmpty());
    }
}

public sealed class FilterValidator : Validator<Filter>
{
    public FilterValidator()
    {
        For(x => x.Max, max => max.IfGreaterThan(100));
    }
}

public sealed class CodeValidator : Validator<Code>
{
    public CodeValidator()
    {
        For(x => x.Value, value => value.IfLongerThan(3));
    }
}

// Reads ValidationOptions.Items, filled from the request by ConfigureValidation.
public sealed class TenantSignUpValidator : Validator<SignUp>
{
    public TenantSignUpValidator()
    {
        Custom((x, context) =>
        {
            if (!context.Items.TryGetValue("tenant", out var tenant) || tenant is not "contoso")
            {
                context.AddFailure("Tenant", "Unknown tenant.");
            }
        });
        MustAsync(async (x, token) =>
        {
            await Task.Yield();
            return x.Name != "taken";
        }, "The name is taken.");
    }
}

internal static class Expected
{
    public static string Message(IValidator<SignUp> validator, SignUp value, string name, params string[] groups)
    {
        foreach (var failure in validator.Validate(value, groups).Failures)
        {
            if (failure.Name == name)
            {
                return failure.Message;
            }
        }

        throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "No failure named {0}.", name));
    }
}
