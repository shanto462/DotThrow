using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DotThrow.AspNetCore.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace DotThrow.AspNetCore.Tests;

public sealed class MvcTests
{
    private static readonly SignUp Valid = new() { Name = "Ada Lovelace", Email = "ada@example.com", Age = 36 };
    private static readonly SignUp Invalid = new() { Name = " ", Email = "ada.example.com", Age = 12 };

    [Fact]
    public async Task Global_filter_returns_a_validation_problem_for_api_controllers()
    {
        await using var app = await StartAsync(mvc => mvc.AddDotThrowValidation());

        var response = await app.PostAsync("/mvc/api/sign-up", Invalid);
        var json = await TestApp.ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(new SignUpValidator().Validate(Invalid).ToDictionary(), TestApp.ReadMap(json, "errors"));
        Assert.True(json.TryGetProperty("traceId", out _));
        Assert.False(json.TryGetProperty("errorCodes", out _));
        Assert.DoesNotContain("ada.example.com", json.GetRawText(), StringComparison.Ordinal);
        Assert.Equal("valid", await Text(await app.PostAsync("/mvc/api/sign-up", Valid)));
    }

    [Fact]
    public async Task Problem_options_add_error_codes_and_severities()
    {
        await using var app = await StartAsync(
            mvc => mvc.AddDotThrowValidation(o => o.Groups = [ValidationGroups.All]),
            services => services.Configure<ValidationProblemOptions>(o =>
            {
                o.IncludeErrorCodes = true;
                o.IncludeSeverities = true;
            }));

        var json = await TestApp.ReadJsonAsync(await app.PostAsync("/mvc/api/sign-up", Invalid));

        Assert.Equal([ErrorCodes.NotEmailAddress], TestApp.ReadMap(json, "errorCodes")["Email"]);
        Assert.Equal(["TooYoung"], TestApp.ReadMap(json, "errorCodes")["Age"]);
        Assert.Equal(["Warning"], TestApp.ReadMap(json, "severities")["Age"]);
        Assert.Equal(TestApp.ReadMap(json, "errors").Keys.OrderBy(k => k, StringComparer.Ordinal), TestApp.ReadMap(json, "errorCodes").Keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Extension_keys_follow_the_mvc_dictionary_key_policy()
    {
        await using var app = await StartAsync(
            mvc => mvc.AddDotThrowValidation().AddJsonOptions(o => o.JsonSerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase),
            services => services.Configure<ValidationProblemOptions>(o =>
            {
                o.IncludeErrorCodes = true;
                o.IncludeSeverities = true;
            }));

        var json = await TestApp.ReadJsonAsync(await app.PostAsync("/mvc/api/sign-up", Invalid));
        var errors = TestApp.ReadMap(json, "errors").Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();

        Assert.Equal(["email", "name"], errors);
        Assert.Equal(errors, TestApp.ReadMap(json, "errorCodes").Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal(errors, TestApp.ReadMap(json, "severities").Keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Attribute_groups_and_skip_replace_the_global_settings()
    {
        await using var app = await StartAsync(mvc => mvc.AddDotThrowValidation());
        var young = new SignUp { Name = "Ada Lovelace", Email = "ada@example.com", Age = 12 };

        Assert.Equal("valid", await Text(await app.PostAsync("/mvc/api/sign-up", young)));
        var adult = await TestApp.ReadJsonAsync(await app.PostAsync("/mvc/api/adult", young));
        Assert.Equal(["Age"], TestApp.ReadMap(adult, "errors").Keys);
        Assert.Equal("valid", await Text(await app.PostAsync("/mvc/api/skip", Invalid)));
    }

    [Fact]
    public async Task Attribute_alone_validates_its_controller_and_runs_once_with_the_global_filter()
    {
        await using (var attributeOnly = await StartAsync(mvc => { }))
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await attributeOnly.PostAsync("/mvc/attribute", Invalid)).StatusCode);
            Assert.Equal("valid", await Text(await attributeOnly.PostAsync("/mvc/api/sign-up", Invalid)));
        }

        await using var both = await StartAsync(mvc => mvc.AddDotThrowValidation().AddDotThrowValidation());
        var json = await TestApp.ReadJsonAsync(await both.PostAsync("/mvc/attribute", Invalid));
        Assert.Single(TestApp.ReadMap(json, "errors")["Name"]);
    }

    [Fact]
    public async Task Other_controllers_run_the_action_with_model_state_errors()
    {
        await using var app = await StartAsync(mvc => mvc.AddDotThrowValidation());

        Assert.Equal("Name=1,Email=1", await Text(await app.PostAsync("/mvc/form", Invalid)));
        Assert.Equal("valid", await Text(await app.PostAsync("/mvc/form", Valid)));
    }

    [Fact]
    public async Task Suppressed_model_state_filter_lets_the_action_decide()
    {
        await using var app = await StartAsync(
            mvc => mvc.AddDotThrowValidation(),
            services => services.Configure<ApiBehaviorOptions>(o => o.SuppressModelStateInvalidFilter = true));

        Assert.Equal("invalid", await Text(await app.PostAsync("/mvc/api/sign-up", Invalid)));
    }

    [Fact]
    public async Task Custom_invalid_model_state_response_is_used()
    {
        await using var app = await StartAsync(
            mvc => mvc.AddDotThrowValidation(),
            services => services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = context =>
                new ContentResult { StatusCode = 422, Content = FormController.ErrorKeys(context.ModelState) }));

        var response = await app.PostAsync("/mvc/api/sign-up", Invalid);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("Name=1,Email=1", await Text(response));
    }

    [Fact]
    public async Task Binding_prefix_names_the_model_state_keys()
    {
        await using var app = await StartAsync(mvc => mvc.AddDotThrowValidation());

        var json = await TestApp.ReadJsonAsync(await app.GetAsync("/mvc/api/filter?filter.Max=500"));

        Assert.Equal(["filter.Max"], TestApp.ReadMap(json, "errors").Keys);
        Assert.Equal("valid", await Text(await app.GetAsync("/mvc/api/filter?filter.Max=5")));
    }

    [Fact]
    public async Task Arguments_without_a_validator_from_services_and_null_are_skipped()
    {
        await using var app = await StartAsync(mvc => mvc.AddDotThrowValidation());

        Assert.Equal(HttpStatusCode.BadRequest, (await app.PostAsync("/mvc/api/services", Invalid)).StatusCode);
        Assert.Equal("valid", await Text(await app.PostAsync("/mvc/api/item", new Item())));
        var empty = await app.Client.PostAsync(new Uri("/mvc/api/optional", UriKind.Relative), new System.Net.Http.StringContent(string.Empty, null, "application/json"), TestContext.Current.CancellationToken);
        Assert.Equal("none", await Text(empty));
    }

    [Fact]
    public async Task Options_pass_request_data_to_the_rules()
    {
        await using var app = await StartAsync(
            mvc => mvc.AddDotThrowValidation(o => o.ConfigureValidation = (options, context) =>
                options.Items["tenant"] = context.HttpContext.Request.Headers["X-Tenant"].ToString()),
            services => services.AddSingleton<IValidator<SignUp>>(new TenantSignUpValidator()));

        Assert.Equal(HttpStatusCode.BadRequest, (await app.PostAsync("/mvc/api/sign-up", Valid)).StatusCode);
        app.Client.DefaultRequestHeaders.Add("X-Tenant", "contoso");
        Assert.Equal("valid", await Text(await app.PostAsync("/mvc/api/sign-up", Valid)));
        var taken = await TestApp.ReadJsonAsync(await app.PostAsync("/mvc/api/sign-up", new SignUp { Name = "taken" }));
        Assert.Equal(["The name is taken."], TestApp.ReadMap(taken, "errors")[""]);
    }

    [Fact]
    public void Mvc_core_builder_adds_the_global_filter()
    {
        var services = new ServiceCollection();
        services.AddMvcCore().AddDotThrowValidation(o => o.Groups = ["adult"]);
        using var provider = services.BuildServiceProvider();

        Assert.Single(provider.GetRequiredService<IOptions<MvcOptions>>().Value.Filters.OfType<DotThrowValidationFilter>());
        Assert.Equal(["adult"], provider.GetRequiredService<IOptions<MvcValidationOptions>>().Value.Groups!);
    }

    [Fact]
    public void Builder_extensions_check_arguments()
    {
        IMvcBuilder mvc = null!;
        IMvcCoreBuilder core = null!;

        Assert.Throws<ArgumentNullException>("builder", () => mvc.AddDotThrowValidation());
        Assert.Throws<ArgumentNullException>("builder", () => core.AddDotThrowValidation());
    }

    [Fact]
    public void Attribute_exposes_its_settings()
    {
        var attribute = new ValidateWithDotThrowAttribute("a", "b") { Skip = true, Order = 5 };
        var none = new ValidateWithDotThrowAttribute(null!);

        Assert.Equal(["a", "b"], attribute.Groups);
        Assert.Empty(none.Groups);
        Assert.True(attribute.IsReusable);
        var filter = Assert.IsType<DotThrowValidationFilter>(attribute.CreateInstance(new ServiceCollection().BuildServiceProvider()));
        Assert.Equal(5, filter.Order);
    }

    [Fact]
    public void Validator_types_are_made_once_and_impossible_ones_are_null()
    {
        Assert.Equal(typeof(IValidator<SignUp>), DotThrowValidationFilter.ValidatorType(typeof(SignUp)));
        Assert.Same(DotThrowValidationFilter.ValidatorType(typeof(SignUp)), DotThrowValidationFilter.ValidatorType(typeof(SignUp)));
        Assert.Null(DotThrowValidationFilter.ValidatorType(typeof(int).MakePointerType()));
    }

    private static Task<TestApp> StartAsync(Action<IMvcBuilder> mvc, Action<IServiceCollection>? services = null) =>
        TestApp.StartAsync(
            builder =>
            {
                builder.Services.AddValidator<SignUp, SignUpValidator>();
                builder.Services.AddValidator<BaseItem, BaseItemValidator>();
                builder.Services.AddValidator<Filter, FilterValidator>();
                mvc(builder.Services.AddControllers());
                services?.Invoke(builder.Services);
            },
            app => app.MapControllers());

    private static Task<string> Text(System.Net.Http.HttpResponseMessage response) =>
        response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
