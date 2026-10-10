using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DotThrow.AspNetCore.Tests;

public sealed partial class MinimalApiTests
{
    private static readonly SignUp Valid = new() { Name = "Ada Lovelace", Email = "ada@example.com", Age = 36 };
    private static readonly SignUp Invalid = new() { Name = " ", Email = "ada.example.com", Age = 12 };

    [Fact]
    public async Task Valid_argument_runs_the_handler()
    {
        await using var app = await TestApp.StartAsync(
            builder => builder.Services.AddValidator<SignUp, SignUpValidator>(),
            app => app.MapPost("/sign-up", (SignUp request) => TypedResults.Ok(request.Name)).WithValidation<SignUp>());

        var response = await app.PostAsync("/sign-up", Valid);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_argument_returns_a_validation_problem_without_running_the_handler()
    {
        var ran = false;
        await using var app = await TestApp.StartAsync(
            builder => builder.Services.AddValidatorsFromAssemblyContaining<SignUpValidator>(filter: r => r.ValidatorType == typeof(SignUpValidator)),
            app => app.MapPost("/sign-up", (SignUp request) =>
            {
                ran = true;
                return TypedResults.Ok();
            }).WithValidation<SignUp>());

        var response = await app.PostAsync("/sign-up", Invalid);
        var json = await TestApp.ReadJsonAsync(response);

        Assert.False(ran);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(400, json.GetProperty("status").GetInt32());
        Assert.Equal(new SignUpValidator().Validate(Invalid).ToDictionary(), TestApp.ReadMap(json, "errors"));
        Assert.False(json.TryGetProperty("errorCodes", out _));
        Assert.False(json.TryGetProperty("severities", out _));
        Assert.DoesNotContain("ada.example.com", json.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task App_options_add_error_codes_and_severities()
    {
        await using var app = await TestApp.StartAsync(
            builder =>
            {
                builder.Services.AddValidator<SignUp, SignUpValidator>();
                builder.Services.Configure<ValidationProblemOptions>(o =>
                {
                    o.IncludeErrorCodes = true;
                    o.IncludeSeverities = true;
                });
            },
            app => app.MapPost("/sign-up", (SignUp request) => TypedResults.Ok()).WithValidation<SignUp>(ValidationGroups.All));

        var json = await TestApp.ReadJsonAsync(await app.PostAsync("/sign-up", Invalid));

        Assert.Equal(["Name", "Email", "Age"], TestApp.ReadMap(json, "errors").Keys);
        Assert.Equal([ErrorCodes.WhiteSpace], TestApp.ReadMap(json, "errorCodes")["Name"]);
        Assert.Equal([ErrorCodes.NotEmailAddress], TestApp.ReadMap(json, "errorCodes")["Email"]);
        Assert.Equal(["TooYoung"], TestApp.ReadMap(json, "errorCodes")["Age"]);
        Assert.Equal(["Error"], TestApp.ReadMap(json, "severities")["Name"]);
        Assert.Equal(["Warning"], TestApp.ReadMap(json, "severities")["Age"]);
    }

    [Fact]
    public async Task Groups_select_the_rules()
    {
        await using var app = await TestApp.StartAsync(
            builder => builder.Services.AddValidator<SignUp, SignUpValidator>(ServiceLifetime.Singleton),
            app =>
            {
                app.MapPost("/default", (SignUp request) => TypedResults.Ok()).WithValidation<SignUp>();
                app.MapPost("/adult", (SignUp request) => TypedResults.Ok()).WithValidation<SignUp>("adult");
            });

        var young = new SignUp { Name = "Ada Lovelace", Email = "ada@example.com", Age = 12 };

        Assert.Equal(HttpStatusCode.OK, (await app.PostAsync("/default", young)).StatusCode);
        var json = await TestApp.ReadJsonAsync(await app.PostAsync("/adult", young));
        Assert.Equal(["Age"], TestApp.ReadMap(json, "errors").Keys);
        Assert.Equal(Expected.Message(new SignUpValidator(), young, "Age", "adult"), TestApp.ReadMap(json, "errors")["Age"][0]);
    }

    [Fact]
    public async Task A_given_validator_needs_no_registration()
    {
        var validator = new Validator<SignUp>().For(x => x.Name, name => name.IfLongerThan(3));
        await using var app = await TestApp.StartAsync(
            builder => { },
            app => app.MapPost("/sign-up", (SignUp request) => TypedResults.Ok()).WithValidation(validator));

        Assert.Equal(HttpStatusCode.OK, (await app.PostAsync("/sign-up", new SignUp { Name = "Ada" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await app.PostAsync("/sign-up", Valid)).StatusCode);
    }

    [Fact]
    public async Task Options_pass_request_data_and_change_the_response()
    {
        await using var app = await TestApp.StartAsync(
            builder => { },
            app =>
            {
                app.MapPost("/tenant", (SignUp request) => TypedResults.Ok()).WithValidation<SignUp>(o =>
                {
                    o.Validator = new TenantSignUpValidator();
                    o.ConfigureValidation = (options, http) => options.Items["tenant"] = http.Request.Headers["X-Tenant"].ToString();
                });
                app.MapPost("/custom", (SignUp request) => TypedResults.Ok()).WithValidation<SignUp>(o =>
                {
                    o.Validator = new SignUpValidator();
                    o.Groups = ["adult"];
                    o.ResultFactory = (report, http) => TypedResults.UnprocessableEntity(report.Failures.Count);
                });
            });

        app.Client.DefaultRequestHeaders.Add("X-Tenant", "contoso");
        Assert.Equal(HttpStatusCode.OK, (await app.PostAsync("/tenant", Valid)).StatusCode);

        var taken = await TestApp.ReadJsonAsync(await app.PostAsync("/tenant", new SignUp { Name = "taken" }));
        Assert.Equal(["The name is taken."], TestApp.ReadMap(taken, "errors")[""]);

        app.Client.DefaultRequestHeaders.Remove("X-Tenant");
        var tenant = await TestApp.ReadJsonAsync(await app.PostAsync("/tenant", Valid));
        Assert.Equal(["Unknown tenant."], TestApp.ReadMap(tenant, "errors")["Tenant"]);

        var custom = await app.PostAsync("/custom", Invalid);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, custom.StatusCode);
        Assert.Equal("1", await custom.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Route_groups_validate_only_endpoints_with_the_argument()
    {
        await using var app = await TestApp.StartAsync(
            builder => builder.Services.AddValidator<SignUp, SignUpValidator>(),
            app =>
            {
                var group = app.MapGroup("/api").WithValidation<SignUp>();
                group.MapPost("/sign-up", (SignUp request) => TypedResults.Ok());
                group.MapGet("/ping", () => "pong");
                group.Map("/raw", (RequestDelegate)(context => context.Response.WriteAsync("raw", context.RequestAborted)));
            });

        Assert.Equal(HttpStatusCode.BadRequest, (await app.PostAsync("/api/sign-up", Invalid)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.PostAsync("/api/sign-up", Valid)).StatusCode);
        Assert.Equal("pong", await (await app.GetAsync("/api/ping")).Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("raw", await (await app.GetAsync("/api/raw")).Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var endpoints = app.Endpoints();
        Assert.Contains(ValidationProblems(endpoints, "/api/sign-up"), m => m.Type == typeof(HttpValidationProblemDetails));
        Assert.Empty(ValidationProblems(endpoints, "/api/ping"));
    }

    [Fact]
    public async Task Route_groups_take_a_validator_or_settings()
    {
        await using var app = await TestApp.StartAsync(
            builder => { },
            app =>
            {
                app.MapGroup("/a").WithValidation(new SignUpValidator()).MapPost("/one", (SignUp request) => TypedResults.Ok());
                app.MapGroup("/b").WithValidation<SignUp>(o => o.Validator = new SignUpValidator()).MapPost("/two", (SignUp request) => TypedResults.Ok());
            });

        Assert.Equal(HttpStatusCode.BadRequest, (await app.PostAsync("/a/one", Invalid)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await app.PostAsync("/b/two", Invalid)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.PostAsync("/b/two", Valid)).StatusCode);
    }

    [Fact]
    public async Task Parameters_of_a_derived_type_and_several_parameters_are_validated()
    {
        await using var app = await TestApp.StartAsync(
            builder =>
            {
                builder.Services.AddValidator<BaseItem, BaseItemValidator>();
                builder.Services.AddValidator<Code, CodeValidator>();
            },
            app =>
            {
                app.MapPost("/items", (Item item) => TypedResults.Ok()).WithValidation<BaseItem>();
                app.MapGet("/codes", (Code first, Code second) => first.Value + second.Value).WithValidation<Code>();
            });

        Assert.Equal(HttpStatusCode.BadRequest, (await app.PostAsync("/items", new Item())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.PostAsync("/items", new Item { Title = "Pen" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/codes?first=ab&second=cd")).StatusCode);
        Assert.Equal(["second.Value"], TestApp.ReadMap(await TestApp.ReadJsonAsync(await app.GetAsync("/codes?first=ab&second=long")), "errors").Keys);
        Assert.Equal(["first.Value"], TestApp.ReadMap(await TestApp.ReadJsonAsync(await app.GetAsync("/codes?first=long&second=ab")), "errors").Keys);

        // Every argument is validated and the failures come in one answer.
        Assert.Equal(["first.Value", "second.Value"], TestApp.ReadMap(await TestApp.ReadJsonAsync(await app.GetAsync("/codes?first=long&second=long")), "errors").Keys);
    }

    [Fact]
    public async Task Extension_keys_follow_the_dictionary_key_policy_like_errors()
    {
        await using var app = await TestApp.StartAsync(
            builder =>
            {
                builder.Services.AddValidator<SignUp, SignUpValidator>();
                builder.Services.Configure<ValidationProblemOptions>(o =>
                {
                    o.IncludeErrorCodes = true;
                    o.IncludeSeverities = true;
                });
                builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase);
            },
            app => app.MapPost("/sign-up", (SignUp request) => TypedResults.Ok()).WithValidation<SignUp>());

        var json = await TestApp.ReadJsonAsync(await app.PostAsync("/sign-up", Invalid));

        Assert.Equal(["name", "email"], TestApp.ReadMap(json, "errors").Keys);
        Assert.Equal(["name", "email"], TestApp.ReadMap(json, "errorCodes").Keys);
        Assert.Equal(["name", "email"], TestApp.ReadMap(json, "severities").Keys);
    }

    [Fact]
    public async Task A_missing_optional_argument_is_not_validated()
    {
        await using var app = await TestApp.StartAsync(
            builder => builder.Services.AddValidator<Code, CodeValidator>(),
            app => app.MapGet("/code", (Code? code) => code is null ? "none" : code.Value).WithValidation<Code>());

        Assert.Equal("none", await (await app.GetAsync("/code")).Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.BadRequest, (await app.GetAsync("/code?code=long")).StatusCode);
    }

    [Fact]
    public async Task A_setup_mistake_breaks_only_its_endpoint_and_is_logged()
    {
        var logs = new CollectingLoggerProvider();
        await using var app = await TestApp.StartAsync(
            builder =>
            {
                builder.Logging.AddProvider(logs);
                builder.Services.AddValidator<SignUp, SignUpValidator>();
            },
            app =>
            {
                app.MapGet("/ok", () => "ok");
                app.MapGet("/bad", () => "x").WithValidation<SignUp>();
            });

        Assert.Equal("ok", await (await app.GetAsync("/ok")).Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var bad = await app.GetAsync("/bad");
        Assert.Equal(HttpStatusCode.InternalServerError, bad.StatusCode);
        Assert.Contains("has no parameter of type SignUp", await bad.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal("ok", await (await app.GetAsync("/ok")).Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Error && e.Category == "DotThrow.AspNetCore.EndpointValidation" && e.Message.Contains("has no parameter of type SignUp", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateEndpointsOnStart_stops_the_host_with_every_mistake()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => TestApp.StartAsync(
            builder =>
            {
                builder.Services.ValidateEndpointsOnStart();
                builder.Services.AddValidator<Code, CodeValidator>();
            },
            app =>
            {
                app.MapGet("/ok", () => "ok");
                app.MapGet("/ping", () => "pong").WithValidation<Code>();
                app.MapPost("/sign-up", (SignUp request) => TypedResults.Ok()).WithValidation<SignUp>();
            }));

        Assert.StartsWith("Endpoint validation is not set up correctly:", exception.Message, StringComparison.Ordinal);
        Assert.Contains("has no parameter of type Code", exception.Message, StringComparison.Ordinal);
        Assert.Contains("needs an IValidator<SignUp> service", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateEndpointsOnStart_lets_a_correct_app_start()
    {
        await using var app = await TestApp.StartAsync(
            builder => builder.Services.ValidateEndpointsOnStart().ValidateEndpointsOnStart().AddValidator<SignUp, SignUpValidator>(),
            app => app.MapPost("/sign-up", (SignUp request) => TypedResults.Ok()).WithValidation<SignUp>());

        Assert.Equal(HttpStatusCode.BadRequest, (await app.PostAsync("/sign-up", Invalid)).StatusCode);
        Assert.Throws<ArgumentNullException>("services", () => ((IServiceCollection)null!).ValidateEndpointsOnStart());
    }

    [Fact]
    public async Task Source_generated_json_without_problem_details_is_a_setup_mistake()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => TestApp.StartAsync(
            builder =>
            {
                builder.Services.ValidateEndpointsOnStart();
                builder.Services.AddValidator<Code, CodeValidator>();
                builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.TypeInfoResolver = JsonTypeInfoResolver.Combine());
            },
            app => app.MapGet("/code", (Code code) => code.Value).WithValidation<Code>()));

        Assert.Contains("AddProblemDetails()", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Arguments_from_services_are_never_validated()
    {
        await using var app = await TestApp.StartAsync(
            builder =>
            {
                var invalid = new SignUp { Name = " ", Email = "x" };
                builder.Services.AddSingleton(invalid);
                builder.Services.AddKeyedSingleton("defaults", invalid);
                builder.Services.AddSingleton<INamed>(new Named(" "));
                builder.Services.AddValidator<SignUp, SignUpValidator>();
                builder.Services.AddValidator<INamed, NamedValidator>();
            },
            app =>
            {
                app.MapPost("/sign-up", ([FromBody] SignUp request, [FromServices] SignUp shared, [FromKeyedServices("defaults")] SignUp keyed) => TypedResults.Ok())
                    .WithValidation<SignUp>();
                app.MapGroup("/named").WithValidation<INamed>().MapGet("/", (INamed named, string? q) => named.Name);
            });

        Assert.Equal(HttpStatusCode.OK, (await app.PostAsync("/sign-up", Valid)).StatusCode);
        var json = await TestApp.ReadJsonAsync(await app.PostAsync("/sign-up", Invalid));
        Assert.Equal(new SignUpValidator().Validate(Invalid).ToDictionary(), TestApp.ReadMap(json, "errors"));
        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/named?q=1")).StatusCode);
    }

    [Fact]
    public async Task Source_generated_json_works_with_problem_details_metadata_or_a_result_factory()
    {
        // In Native AOT apps AddProblemDetails() adds this metadata (checked with a published app);
        // with reflection on, as in tests, a source-generated context stands in for it.
        await using var app = await TestApp.StartAsync(
            builder =>
            {
                builder.Services.AddValidator<Code, CodeValidator>();
                builder.Services.Configure<ValidationProblemOptions>(o => o.IncludeErrorCodes = true);
                builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.TypeInfoResolver = ProblemJson.Default);
            },
            app =>
            {
                app.MapGet("/code", (Code code) => code.Value).WithValidation<Code>();
                app.MapGet("/custom", (Code code) => code.Value).WithValidation<Code>(o => o.ResultFactory = (report, http) => TypedResults.BadRequest());
            });

        var response = await app.GetAsync("/code?code=long");
        var json = await TestApp.ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([ErrorCodes.LongerThan], TestApp.ReadMap(json, "errorCodes")["Value"]);
        Assert.Equal(HttpStatusCode.BadRequest, (await app.GetAsync("/custom?code=long")).StatusCode);
        Assert.True(Internal.ValidationEndpointFilter<Code>.CanWriteValidationProblems(new ServiceCollection().BuildServiceProvider()));
    }

    [Fact]
    public async Task Validated_endpoints_describe_the_400_response()
    {
        await using var app = await TestApp.StartAsync(
            builder => builder.Services.AddValidator<SignUp, SignUpValidator>(),
            app => app.MapPost("/sign-up", (SignUp request) => TypedResults.Ok()).WithValidation<SignUp>());

        var metadata = Assert.Single(ValidationProblems(app.Endpoints(), "/sign-up"));
        Assert.Equal(typeof(HttpValidationProblemDetails), metadata.Type);
        Assert.Equal(["application/problem+json"], metadata.ContentTypes);
    }

    [Fact]
    public void Extension_methods_check_arguments()
    {
        RouteHandlerBuilder handler = null!;
        RouteGroupBuilder group = null!;

        Assert.Throws<ArgumentNullException>("builder", () => handler.WithValidation<SignUp>());
        Assert.Throws<ArgumentNullException>("builder", () => group.WithValidation<SignUp>());
        Assert.Throws<ArgumentNullException>("validator", () => handler.WithValidation((IValidator<SignUp>)null!));
        Assert.Throws<ArgumentNullException>("validator", () => group.WithValidation((IValidator<SignUp>)null!));
        Assert.Throws<ArgumentNullException>("configure", () => handler.WithValidation((Action<EndpointValidationOptions<SignUp>>)null!));
        Assert.Throws<ArgumentNullException>("configure", () => group.WithValidation((Action<EndpointValidationOptions<SignUp>>)null!));
        Assert.Throws<ArgumentNullException>("groups", () => handler.WithValidation<SignUp>((string[])null!));
    }

    [JsonSerializable(typeof(HttpValidationProblemDetails))]
    [JsonSerializable(typeof(JsonElement))]
    internal sealed partial class ProblemJson : JsonSerializerContext;

    private static List<IProducesResponseTypeMetadata> ValidationProblems(IReadOnlyList<Endpoint> endpoints, string route)
    {
        var endpoint = endpoints.OfType<RouteEndpoint>().Single(e => e.RoutePattern.RawText == route);
        return [.. endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>().Where(m => m.StatusCode == StatusCodes.Status400BadRequest)];
    }
}
