using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace DotThrow.DependencyInjection.Tests;

public sealed class OptionsValidationTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly SmtpOptions Valid = new() { Host = "smtp.example.com", Port = 587 };

    [Fact]
    public void Registered_validator_fails_the_options_with_property_paths()
    {
        using var provider = Provider(services =>
        {
            services.AddValidatorsFromAssemblyContaining<SmtpOptionsValidator>();
            services.AddOptions<SmtpOptions>()
                .Configure(o =>
                {
                    o.Host = " ";
                    o.Port = 0;
                    o.Credentials = new SmtpCredentials();
                })
                .ValidateWithDotThrow();
        });

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<SmtpOptions>>().Value);

        Assert.Equal(typeof(SmtpOptions), exception.OptionsType);
        Assert.Equal(Options.DefaultName, exception.OptionsName);
        var expected = Messages(new SmtpOptionsValidator().Validate(new SmtpOptions { Host = " ", Port = 0, Credentials = new SmtpCredentials() }, null));
        Assert.Equal(expected, exception.Failures);
        Assert.Equal(3, expected.Count);
        Assert.StartsWith("SmtpOptions.Host: ", expected[0], StringComparison.Ordinal);
        Assert.StartsWith("SmtpOptions.Port: ", expected[1], StringComparison.Ordinal);
        Assert.StartsWith("SmtpOptions.Credentials.UserName: ", expected[2], StringComparison.Ordinal);
    }

    [Fact]
    public void Whole_object_failures_are_named_after_the_options_class()
    {
        using var provider = Provider(services => services.AddOptions<SmtpOptions>()
            .Configure(o =>
            {
                o.Host = "localhost";
                o.Port = 25;
            })
            .ValidateWithDotThrow(new SmtpOptionsValidator()));

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<SmtpOptions>>().Value);

        Assert.Equal(["SmtpOptions: Port 25 on localhost is not allowed."], exception.Failures);
    }

    [Fact]
    public void Valid_options_pass()
    {
        using var provider = Provider(services =>
        {
            services.AddValidator<SmtpOptions, SmtpOptionsValidator>(ServiceLifetime.Transient);
            services.AddOptions<SmtpOptions>().Configure(o => Copy(Valid, o)).ValidateWithDotThrow();
        });

        Assert.Equal(587, provider.GetRequiredService<IOptions<SmtpOptions>>().Value.Port);
    }

    [Fact]
    public void Inline_rules_validate_the_options()
    {
        using var provider = Provider(services => services.AddOptions<SmtpOptions>()
            .Configure(o => o.Port = 70000)
            .ValidateWithDotThrow(v => v.For(x => x.Port, port => port.IfGreaterThan(65535))));

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<SmtpOptions>>().Value);

        Assert.Single(exception.Failures);
        Assert.StartsWith("SmtpOptions.Port: ", exception.Failures.First(), StringComparison.Ordinal);
    }

    [Fact]
    public void Named_options_are_validated_by_name()
    {
        using var provider = Provider(services =>
        {
            services.AddValidator<SmtpOptions, SmtpOptionsValidator>();
            services.AddOptions<SmtpOptions>("backup").Configure(o => o.Port = 0).ValidateWithDotThrow();
            services.AddOptions<SmtpOptions>("main").Configure(o => Copy(Valid, o)).ValidateWithDotThrow();
            services.Configure<SmtpOptions>(o => o.Port = -1);
        });

        var monitor = provider.GetRequiredService<IOptionsMonitor<SmtpOptions>>();

        Assert.Equal(587, monitor.Get("main").Port);
        // The default name has no DotThrow validation, so its bad port passes.
        Assert.Equal(-1, monitor.CurrentValue.Port);
        var exception = Assert.Throws<OptionsValidationException>(() => monitor.Get("backup"));
        Assert.Equal("backup", exception.OptionsName);
    }

    [Fact]
    public void Missing_validator_registration_is_a_clear_error()
    {
        using var provider = Provider(services => services.AddOptions<SmtpOptions>().ValidateWithDotThrow());

        var exception = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IOptions<SmtpOptions>>().Value);

        Assert.Contains("No IValidator<SmtpOptions> is registered", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Async_rules_cannot_run_in_options_validation()
    {
        var validator = new Validator<SmtpOptions>().MustAsync((_, _) => Task.FromResult(true));
        using var provider = Provider(services => services.AddOptions<SmtpOptions>().ValidateWithDotThrow(validator));

        Assert.Throws<AsyncRuleInvokedSynchronouslyException>(() => provider.GetRequiredService<IOptions<SmtpOptions>>().Value);
    }

    [Fact]
    public async Task ValidateOnStart_stops_the_host_with_bad_configuration()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true, EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Smtp:Host"] = "smtp.example.com", ["Smtp:Port"] = "0" });
        builder.Services.AddValidatorsFromAssemblyContaining<SmtpOptionsValidator>();
        builder.Services.AddOptions<SmtpOptions>().BindConfiguration("Smtp").ValidateWithDotThrow().ValidateOnStart();

        using var host = builder.Build();
        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync(Token));

        Assert.Single(exception.Failures);
        Assert.StartsWith("SmtpOptions.Port: ", exception.Failures.First(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateOnStart_lets_good_configuration_start()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Smtp:Host"] = "smtp.example.com", ["Smtp:Port"] = "587" });
        builder.Services.AddValidator<SmtpOptions, SmtpOptionsValidator>();
        builder.Services.AddOptions<SmtpOptions>().BindConfiguration("Smtp").ValidateWithDotThrow().ValidateOnStart();

        using var host = builder.Build();
        await host.StartAsync(Token);
        await host.StopAsync(Token);
    }

    [Fact]
    public void Reloaded_configuration_is_validated_again()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Smtp:Host"] = "smtp.example.com", ["Smtp:Port"] = "587" })
            .Build();
        using var provider = Provider(services =>
        {
            services.AddSingleton<IConfiguration>(configuration);
            services.AddValidator<SmtpOptions, SmtpOptionsValidator>(ServiceLifetime.Singleton);
            services.AddOptions<SmtpOptions>().BindConfiguration("Smtp").ValidateWithDotThrow();
        });
        var monitor = provider.GetRequiredService<IOptionsMonitor<SmtpOptions>>();
        Assert.Equal(587, monitor.CurrentValue.Port);

        configuration["Smtp:Port"] = "99999";
        // The options monitor reads the new value inside the reload callback, so the reload itself
        // may report the failure too.
        _ = Record.Exception(configuration.Reload);

        var exception = Assert.Throws<OptionsValidationException>(() => monitor.CurrentValue);
        Assert.StartsWith("SmtpOptions.Port: ", exception.Failures.First(), StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateOptions_skips_other_names_and_checks_arguments()
    {
        var validate = new DotThrowValidateOptions<SmtpOptions>("main", new SmtpOptionsValidator());
        var everyName = new DotThrowValidateOptions<SmtpOptions>(null, new SmtpOptionsValidator());

        Assert.Equal("main", validate.Name);
        Assert.Null(everyName.Name);
        Assert.True(validate.Validate("other", new SmtpOptions()).Skipped);
        Assert.True(validate.Validate("main", Valid).Succeeded);
        Assert.True(everyName.Validate("any", new SmtpOptions()).Failed);
        Assert.Throws<ArgumentNullException>("options", () => validate.Validate("main", null!));
        Assert.Throws<ArgumentNullException>("services", () => new DotThrowValidateOptions<SmtpOptions>(null, (IServiceProvider)null!));
        Assert.Throws<ArgumentNullException>("validator", () => new DotThrowValidateOptions<SmtpOptions>(null, (IValidator<SmtpOptions>)null!));
    }

    [Fact]
    public void Builder_extensions_check_arguments()
    {
        OptionsBuilder<SmtpOptions> missing = null!;
        var builder = new ServiceCollection().AddOptions<SmtpOptions>();

        Assert.Throws<ArgumentNullException>("builder", () => missing.ValidateWithDotThrow());
        Assert.Throws<ArgumentNullException>("builder", () => missing.ValidateWithDotThrow(new SmtpOptionsValidator()));
        Assert.Throws<ArgumentNullException>("builder", () => missing.ValidateWithDotThrow(_ => { }));
        Assert.Throws<ArgumentNullException>("validator", () => builder.ValidateWithDotThrow((IValidator<SmtpOptions>)null!));
        Assert.Throws<ArgumentNullException>("configure", () => builder.ValidateWithDotThrow((Action<Validator<SmtpOptions>>)null!));
    }

    private static ServiceProvider Provider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private static List<string> Messages(ValidationReport report)
    {
        var messages = new List<string>();
        foreach (var failure in report.Failures)
        {
            messages.Add("SmtpOptions." + failure.Name + ": " + failure.Message);
        }

        return messages;
    }

    private static void Copy(SmtpOptions from, SmtpOptions to)
    {
        to.Host = from.Host;
        to.Port = from.Port;
    }
}
