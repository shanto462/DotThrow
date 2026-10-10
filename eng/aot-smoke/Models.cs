using System.Text.Json.Serialization;

namespace DotThrow.AotSmoke;

public sealed record SignUp(string? Name, string? Email);

public sealed record Order(string? Sku, int Quantity);

public sealed record Accepted(string Id);

public sealed class SmokeOptions
{
    public int MaxQuantity { get; set; }
}

public sealed class SignUpValidator : Validator<SignUp>
{
    public SignUpValidator()
    {
        For(x => x.Name, name => name.IfNullOrWhiteSpace());
        For(x => x.Email, email => email.IfNullOrWhiteSpace().IfNotEmailAddress());
    }
}

[JsonSerializable(typeof(SignUp))]
[JsonSerializable(typeof(Order))]
[JsonSerializable(typeof(Accepted))]
internal sealed partial class SmokeJsonContext : JsonSerializerContext;
