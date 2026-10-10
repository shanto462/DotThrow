using DotThrow;

// Uses each package once, so the build proves they restore and compile together.
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddValidator<SignUp, SignUpValidator>();
builder.Services.AddOptions<ConsumerOptions>()
    .Configure(options => options.Limit = 10)
    .ValidateWithDotThrow(v => v.For(x => x.Limit, limit => limit.IfOutOfRange(1, 100)));

var app = builder.Build();
app.MapPost("/sign-up", (SignUp request) => Results.Ok(request.Name))
    .WithValidation<SignUp>();

if (args.Contains("--run"))
{
    app.Run();
}

public sealed record SignUp(string? Name, string? Email);

public sealed class SignUpValidator : Validator<SignUp>
{
    public SignUpValidator()
    {
        For(x => x.Name, name => name.IfNullOrWhiteSpace());
        For(x => x.Email, email => email.IfNullOrWhiteSpace().IfNotEmailAddress());
    }
}

public sealed class ConsumerOptions
{
    public int Limit { get; set; }
}
