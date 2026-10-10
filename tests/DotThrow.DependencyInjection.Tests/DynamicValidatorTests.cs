using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotThrow.Dynamic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace DotThrow.DependencyInjection.Tests;

public sealed class DynamicValidatorTests
{
    private static readonly string NameRules = NameRulesWith(10);

    private static readonly Contact Ada = new() { Name = "Ada Lovelace", Email = "ada@example.com", Age = 36, Country = "NL", Tags = ["math"], Address = new Address { City = "London" } };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static PropertyMap<Contact> Map => new PropertyMap<Contact>()
        .Add(x => x.Name)
        .Add(x => x.Email)
        .Add(x => x.Age)
        .Add(x => x.Country)
        .AddCollection(x => x.Tags)
        .Add(x => x.Address)
        .Add(x => x.Address?.City);

    [Fact]
    public void Appsettings_rules_validate_like_FromJson_rules()
    {
        var configuration = RuleSetConfigurationTests.Json(RuleSetConfigurationTests.AppSettings(RuleSetConfigurationTests.ContactRules));
        using var provider = Provider(services => services.AddDynamicValidator<Contact>(configuration.GetSection("Validation:Contact")));
        var validator = provider.GetRequiredService<IValidator<Contact>>();
        var expected = new DynamicValidator<Contact>(RuleSetDefinition.FromJson(RuleSetConfigurationTests.ContactRules));

        Contact[] samples =
        [
            Ada,
            new() { Name = " ", Email = "x", Age = 12, Tags = ["TEST", "ok"], Country = "NL", Address = new Address { City = "" } },
            new() { Name = "A very long name indeed", Email = null, Age = 200, Country = "DE", Address = new Address { City = "1st" } },
        ];
        foreach (var sample in samples)
        {
            Assert.Equal(Failures(expected.Validate(sample, ValidationGroups.All)), Failures(validator.Validate(sample, ValidationGroups.All)));
        }

        Assert.True(validator.Validate(Ada).IsValid);
        Assert.Same(validator, provider.GetRequiredService<DynamicValidator<Contact>>());
        Assert.Same(validator, provider.GetRequiredService<IValidator<Contact>>());
    }

    [Fact]
    public void Configuration_reload_swaps_the_rules()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(Flat(NameRules)).Build();
        using var provider = Provider(services => services.AddDynamicValidator<Contact>(configuration));
        var validator = provider.GetRequiredService<IValidator<Contact>>();
        var name = new Contact { Name = "Ada Lovelace" };

        Assert.False(validator.Validate(name).IsValid);

        configuration["rules:0:checks:1:value"] = "20";
        configuration.Reload();

        Assert.True(validator.Validate(name).IsValid);
        Assert.Equal("20", provider.GetRequiredService<DynamicValidator<Contact>>().Definition.Rules[0].Checks[1].Value);
    }

    [Fact]
    public void Invalid_reload_keeps_the_old_rules_and_reports_every_error()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(Flat(NameRules)).Build();
        var reported = new List<RuleDefinitionException>();
        IServiceProvider? seen = null;
        using var provider = Provider(services => services.AddDynamicValidator<Contact>(configuration, o => o.OnReloadError = (error, services) =>
        {
            reported.Add(error);
            seen = services;
        }));
        var validator = provider.GetRequiredService<IValidator<Contact>>();
        var longName = new Contact { Name = "Ada Lovelace" };

        configuration["rules:0:checks:1:check"] = "LongerThen";
        configuration.Reload();
        configuration["rules:0:checks:1:check"] = "LongerThan";
        configuration["rules:0:chekcs:0"] = "WhiteSpace";
        configuration.Reload();

        Assert.Equal(2, reported.Count);
        Assert.Contains(reported[0].Errors, e => Has(e, "LongerThen"));
        Assert.Contains(reported[1].Errors, e => Has(e, "did you mean 'checks'"));
        Assert.Same(provider.GetRequiredService<DynamicValidator<Contact>>(), seen!.GetRequiredService<DynamicValidator<Contact>>());
        Assert.False(validator.Validate(longName).IsValid);
        Assert.Equal("10", provider.GetRequiredService<DynamicValidator<Contact>>().Definition.Rules[0].Checks[1].Value);
    }

    [Fact]
    public void Unexpected_reload_exceptions_are_reported_as_definition_errors()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["rules:0:property"] = "Name",
            ["rules:0:checks:0:check"] = "Boom",
            ["rules:0:checks:0:value"] = "ok",
        }).Build();
        RuleDefinitionException? reported = null;
        using var provider = Provider(services => services.AddDynamicValidator<Contact>(configuration, o =>
        {
            o.Checks = new CheckRegistry().Add<string>("Boom", args => args.Value == "boom" ? throw new InvalidOperationException("boom") : (guard, _) => guard);
            o.OnReloadError = (error, _) => reported = error;
        }));
        _ = provider.GetRequiredService<IValidator<Contact>>();

        configuration["rules:0:checks:0:value"] = "boom";
        configuration.Reload();

        Assert.IsType<InvalidOperationException>(reported?.InnerException);
        Assert.StartsWith("Reloading the rules failed: boom", reported!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reload_errors_go_to_trace_without_a_handler_and_a_throwing_handler_is_caught()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(Flat(NameRules)).Build();
        var listener = new CollectingListener();
        Trace.Listeners.Add(listener);
        try
        {
            using (var provider = Provider(services => services.AddDynamicValidator<Contact>(configuration)))
            {
                _ = provider.GetRequiredService<IValidator<Contact>>();
                configuration["rules:0:checks:1:check"] = "NoSuchCheck";
                configuration.Reload();
            }

            configuration["rules:0:checks:1:check"] = "LongerThan";
            configuration.Reload();
            using (var provider = Provider(services => services.AddDynamicValidator<Contact>(configuration, o => o.OnReloadError = (_, _) => throw new InvalidOperationException("handler failed"))))
            {
                _ = provider.GetRequiredService<IValidator<Contact>>();
                configuration["rules:0:checks:1:check"] = "NoSuchCheck";
                configuration.Reload();
            }
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }

        Assert.Contains(listener.Messages, m => Has(m, "DynamicValidator<Contact> were not reloaded") && Has(m, "NoSuchCheck"));
        Assert.Contains(listener.Messages, m => Has(m, "OnReloadError of DynamicValidator<Contact> threw") && Has(m, "handler failed"));
    }

    [Fact]
    public async Task Json_file_with_reloadOnChange_updates_the_rules()
    {
        var folder = Path.Combine(Path.GetTempPath(), "dotthrow-di-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "appsettings.json");
        try
        {
            File.WriteAllText(file, RuleSetConfigurationTests.AppSettings(NameRules));
            var configuration = new ConfigurationBuilder().AddJsonFile(file, optional: false, reloadOnChange: true).Build();
            using var provider = Provider(services =>
            {
                services.AddSingleton<IConfiguration>(configuration);
                services.AddDynamicValidator<Contact>("Validation:Contact");
            });
            var validator = provider.GetRequiredService<IValidator<Contact>>();
            var name = new Contact { Name = "Ada Lovelace" };
            Assert.False(validator.Validate(name).IsValid);

            File.WriteAllText(file, RuleSetConfigurationTests.AppSettings(NameRulesWith(30)));

            // The file watcher reloads after a short delay; wait up to 15 seconds.
            var watch = Stopwatch.StartNew();
            while (!validator.Validate(name).IsValid && watch.Elapsed < TimeSpan.FromSeconds(15))
            {
                await Task.Delay(100, Token);
            }

            Assert.True(validator.Validate(name).IsValid);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void PropertyMap_overloads_check_only_mapped_properties()
    {
        var configuration = RuleSetConfigurationTests.Json(RuleSetConfigurationTests.AppSettings(RuleSetConfigurationTests.ContactRules));
        using var provider = Provider(services => services.AddDynamicValidator(configuration.GetSection("Validation:Contact"), Map));
        var validator = provider.GetRequiredService<IValidator<Contact>>();
        var expected = new DynamicValidator<Contact>(RuleSetDefinition.FromJson(RuleSetConfigurationTests.ContactRules), Map);
        var bad = new Contact { Name = " ", Email = "x", Age = 12 };

        Assert.Equal(Failures(expected.Validate(bad)), Failures(validator.Validate(bad)));

        var withoutEmail = new PropertyMap<Contact>().Add(x => x.Name);
        using var limited = Provider(services => services.AddDynamicValidator(configuration.GetSection("Validation:Contact"), withoutEmail));
        var exception = Assert.Throws<RuleDefinitionException>(() => limited.GetRequiredService<IValidator<Contact>>());
        Assert.Contains(exception.Errors, e => Has(e, "Email"));
    }

    [Fact]
    public void Section_path_overloads_read_the_app_configuration()
    {
        var configuration = RuleSetConfigurationTests.Json(RuleSetConfigurationTests.AppSettings(NameRules));
        using var reflection = Provider(services => services.AddSingleton<IConfiguration>(configuration).AddDynamicValidator<Contact>("Validation:Contact"));
        using var mapped = Provider(services => services.AddSingleton<IConfiguration>(configuration).AddDynamicValidator("Validation:Contact", Map));
        using var missing = Provider(services => services.AddDynamicValidator<Contact>("Validation:Contact", o => o.ValidateOnStart = false));

        Assert.False(reflection.GetRequiredService<IValidator<Contact>>().Validate(new Contact { Name = "Ada Lovelace" }).IsValid);
        Assert.False(mapped.GetRequiredService<IValidator<Contact>>().Validate(new Contact { Name = "Ada Lovelace" }).IsValid);
        var exception = Assert.Throws<InvalidOperationException>(() => missing.GetRequiredService<IValidator<Contact>>());
        Assert.Contains("needs an IConfiguration service", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Definition_overloads_have_no_reload_and_Update_changes_the_rules()
    {
        var definition = RuleSetDefinition.FromJson(NameRules);
        using var reflection = Provider(services => services.AddDynamicValidator<Contact>(definition));
        using var mapped = Provider(services => services.AddDynamicValidator(definition, Map));

        foreach (var provider in new[] { reflection, mapped })
        {
            var validator = provider.GetRequiredService<IValidator<Contact>>();
            Assert.False(validator.Validate(new Contact { Name = "Ada Lovelace" }).IsValid);
            provider.GetRequiredService<DynamicValidator<Contact>>().Update(RuleSetDefinition.FromJson("""{ "rules": [ { "property": "Name", "checks": [ "WhiteSpace" ] } ] }"""));
            Assert.True(validator.Validate(new Contact { Name = "Ada Lovelace" }).IsValid);
        }
    }

    [Fact]
    public void Custom_checks_and_code_rules_run_with_the_dynamic_rules()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["rules:0:property"] = "Name",
            ["rules:0:checks:0"] = "NotReserved",
        }).Build();
        using var provider = Provider(services => services.AddDynamicValidator<Contact>(configuration, o =>
        {
            o.Checks = new CheckRegistry().Add<string>("NotReserved", (name, _) => name.IfOneOf("admin", "root"));
            o.CodeRules = v => v.For(x => x.Email, email => email.IfNullOrWhiteSpace());
        }));
        var validator = provider.GetRequiredService<IValidator<Contact>>();

        Assert.IsType<Validator<Contact>>(validator);
        Assert.Equal(["Email", "Name"], validator.Validate(new Contact { Name = "admin" }).Failures.Select(f => f.Name));
        Assert.True(validator.Validate(new Contact { Name = "Ada", Email = "ada@example.com" }).IsValid);

        configuration["rules:0:checks:0"] = "WhiteSpace";
        configuration.Reload();

        Assert.True(validator.Validate(new Contact { Name = "admin", Email = "ada@example.com" }).IsValid);
    }

    [Fact]
    public void Scanning_and_dynamic_validators_work_together()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Validation:Order:rules:0:property"] = "Quantity",
            ["Validation:Order:rules:0:checks:0:check"] = "NotMultipleOf",
            ["Validation:Order:rules:0:checks:0:value"] = "5",
        }).Build();
        using var provider = Provider(services =>
        {
            services.AddSingleton<IConfiguration>(configuration);
            services.AddValidatorsFromAssemblyContaining<SignUpValidator>();
            services.AddDynamicValidator<Order>("Validation:Order", o => o.CodeRules = v => v.Include(new OrderValidator()));
            services.AddDynamicValidator(RuleSetDefinition.FromJson(NameRules), Map);
        });

        // Scanned validators are scoped; the dynamic ones are singletons.
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        Assert.IsType<SignUpValidator>(services.GetRequiredService<IValidator<SignUp>>());
        var orders = services.GetServices<IValidator<Order>>().ToList();
        Assert.Equal(3, orders.Count);
        Assert.Contains(orders, v => v is OrderValidator);
        Assert.Contains(orders, v => v is OrderLimitValidator);
        var combined = services.GetRequiredService<IValidator<Order>>();
        Assert.Equal(["Quantity", "Quantity"], combined.Validate(new Order { Quantity = -3 }).Failures.Select(f => f.Name));
        Assert.Single(combined.Validate(new Order { Quantity = 7 }).Failures);
        Assert.True(combined.Validate(new Order { Quantity = 10 }).IsValid);
        Assert.False(services.GetRequiredService<IValidator<Contact>>().Validate(new Contact { Name = "Ada Lovelace" }).IsValid);
    }

    [Fact]
    public void A_second_registration_for_the_same_type_replaces_the_first()
    {
        var services = new ServiceCollection()
            .AddDynamicValidator<Contact>(RuleSetDefinition.FromJson(NameRules))
            .AddDynamicValidator<Contact>(RuleSetDefinition.FromJson("""{ "rules": [ { "property": "Email", "checks": [ "WhiteSpace" ] } ] }"""));

        Assert.Single(services, d => d.ServiceType == typeof(IValidator<Contact>));
        Assert.Single(services, d => d.ServiceType == typeof(DynamicValidator<Contact>));
        using var provider = services.BuildServiceProvider();
        Assert.Equal(["Email"], provider.GetRequiredService<IValidator<Contact>>().Validate(new Contact { Name = "Ada Lovelace" }).Failures.Select(f => f.Name));
    }

    [Fact]
    public void Disposed_providers_stop_reloading()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(Flat(NameRules)).Build();
        DynamicValidator<Contact> validator;
        using (var provider = Provider(services => services.AddDynamicValidator<Contact>(configuration)))
        {
            validator = provider.GetRequiredService<DynamicValidator<Contact>>();
        }

        configuration["rules:0:checks:1:value"] = "20";
        configuration.Reload();

        Assert.Equal("10", validator.Definition.Rules[0].Checks[1].Value);
    }

    [Fact]
    public void Invalid_rules_fail_when_the_validator_is_first_resolved()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["rules:0:property"] = "Nmae",
            ["rules:0:checks:0"] = "WhiteSpace",
        }).Build();
        using var provider = Provider(services => services.AddDynamicValidator<Contact>(configuration));

        var exception = Assert.Throws<RuleDefinitionException>(() => provider.GetRequiredService<IValidator<Contact>>());
        Assert.Contains(exception.Errors, e => Has(e, "Nmae"));
    }

    [Fact]
    public async Task Host_start_fails_with_invalid_rules_unless_turned_off()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Validation:Contact:rules:0:property"] = "Name",
            ["Validation:Contact:rules:0:checks:0"] = "NoSuchCheck",
        });
        builder.Services.AddDynamicValidator<Contact>("Validation:Contact");

        using (var host = builder.Build())
        {
            var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync(Token));
            Assert.All(exception.Failures, f => Assert.StartsWith("DynamicValidator<Contact>: ", f, StringComparison.Ordinal));
            Assert.Contains(exception.Failures, f => Has(f, "NoSuchCheck"));
        }

        var relaxed = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        relaxed.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Validation:Contact:rules:0:property"] = "Name", ["Validation:Contact:rules:0:checks:0"] = "NoSuchCheck" });
        relaxed.Services.AddDynamicValidator<Contact>("Validation:Contact", o => o.ValidateOnStart = false);
        using var started = relaxed.Build();
        await started.StartAsync(Token);
        await started.StopAsync(Token);
    }

    [Fact]
    public async Task Host_start_builds_valid_rules()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(RuleSetConfigurationTests.AppSettings(NameRules))));
        builder.Services.AddDynamicValidator<Contact>(builder.Configuration.GetSection("Validation:Contact"), Map);

        using var host = builder.Build();
        await host.StartAsync(Token);
        Assert.False(host.Services.GetRequiredService<IValidator<Contact>>().Validate(new Contact { Name = "Ada Lovelace" }).IsValid);
        await host.StopAsync(Token);
    }

    [Fact]
    public void Null_arguments_throw()
    {
        IServiceCollection services = new ServiceCollection();
        IServiceCollection missing = null!;
        var section = new ConfigurationBuilder().Build();
        var definition = new RuleSetDefinition();

        Assert.Throws<ArgumentNullException>("services", () => missing.AddDynamicValidator<Contact>(section));
        Assert.Throws<ArgumentNullException>("section", () => services.AddDynamicValidator<Contact>((IConfiguration)null!));
        Assert.Throws<ArgumentNullException>("section", () => services.AddDynamicValidator((IConfiguration)null!, Map));
        Assert.Throws<ArgumentNullException>("properties", () => services.AddDynamicValidator(section, (PropertyMap<Contact>)null!));
        Assert.Throws<ArgumentNullException>("sectionPath", () => services.AddDynamicValidator<Contact>((string)null!));
        Assert.Throws<ArgumentNullException>("sectionPath", () => services.AddDynamicValidator((string)null!, Map));
        Assert.Throws<ArgumentNullException>("properties", () => services.AddDynamicValidator("x", (PropertyMap<Contact>)null!));
        Assert.Throws<ArgumentNullException>("definition", () => services.AddDynamicValidator<Contact>((RuleSetDefinition)null!));
        Assert.Throws<ArgumentNullException>("definition", () => services.AddDynamicValidator((RuleSetDefinition)null!, Map));
        Assert.Throws<ArgumentNullException>("properties", () => services.AddDynamicValidator(definition, (PropertyMap<Contact>)null!));
    }

    private static string NameRulesWith(int maxLength) =>
        "{ \"rules\": [ { \"property\": \"Name\", \"checks\": [ \"WhiteSpace\", { \"check\": \"LongerThan\", \"value\": " +
        maxLength.ToString(System.Globalization.CultureInfo.InvariantCulture) + " } ] } ] }";

    private static bool Has(string text, string part) =>
#if NETFRAMEWORK
        text.IndexOf(part, StringComparison.Ordinal) >= 0;
#else
        text.Contains(part, StringComparison.Ordinal);
#endif

    private static ServiceProvider Provider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private static Dictionary<string, string?> Flat(string json)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in RuleSetConfigurationTests.Json(json).AsEnumerable())
        {
            values[pair.Key] = pair.Value;
        }

        return values;
    }

    private static List<string> Failures(ValidationReport report) =>
        [.. report.Failures.Select(f => $"{f.Name}|{f.ErrorCode}|{f.Severity}|{f.Message}")];

    private sealed class CollectingListener : TraceListener
    {
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (_messages)
                {
                    return [.. _messages];
                }
            }
        }

        public override void Write(string? message) => Add(message);

        public override void WriteLine(string? message) => Add(message);

        private void Add(string? message)
        {
            lock (_messages)
            {
                _messages.Add(message ?? string.Empty);
            }
        }
    }
}
