using System.Collections.Generic;
using DotThrow;
using DotThrow.AotSmoke;
using DotThrow.Dynamic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, SmokeJsonContext.Default));

// Gives the JSON options the metadata that ValidationProblem results need in Native AOT.
builder.Services.AddProblemDetails();

// A validator class, registered without assembly scanning.
builder.Services.AddValidator<SignUp, SignUpValidator>();

// Options validation, run at startup.
builder.Services.AddOptions<SmokeOptions>()
    .Configure(options => options.MaxQuantity = 100)
    .ValidateWithDotThrow(validator => validator.For(x => x.MaxQuantity, max => max.IfOutOfRange(1, 1000)))
    .ValidateOnStart();

// Rules from configuration. The PropertyMap reads the properties without reflection.
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Validation:Order:rules:0:property"] = "Sku",
    ["Validation:Order:rules:0:checks:0"] = "NullOrWhiteSpace",
    ["Validation:Order:rules:1:property"] = "Quantity",
    ["Validation:Order:rules:1:checks:0:check"] = "OutOfRange",
    ["Validation:Order:rules:1:checks:0:min"] = "1",
    ["Validation:Order:rules:1:checks:0:max"] = "100",
});
builder.Services.AddDynamicValidator(
    "Validation:Order",
    new PropertyMap<Order>().Add(x => x.Sku).Add(x => x.Quantity));

var app = builder.Build();

app.MapGet("/health", () => TypedResults.Ok(new Accepted("ok")));
app.MapPost("/sign-up", (SignUp request) => TypedResults.Ok(new Accepted(request.Email ?? string.Empty)))
    .WithValidation<SignUp>();
app.MapPost("/orders", (Order order) => TypedResults.Ok(new Accepted(order.Sku ?? string.Empty)))
    .WithValidation<Order>();

await app.RunAsync().ConfigureAwait(false);
