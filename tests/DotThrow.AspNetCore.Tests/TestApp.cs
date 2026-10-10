using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DotThrow.AspNetCore.Tests;

// A web app on TestServer, started per test.
internal sealed class TestApp : IAsyncDisposable
{
    private TestApp(WebApplication app)
    {
        App = app;
        Client = app.GetTestClient();
    }

    public WebApplication App { get; }

    public HttpClient Client { get; }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static async Task<TestApp> StartAsync(Action<WebApplicationBuilder> configureServices, Action<WebApplication> configureApp)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            // The test assembly holds the MVC controllers.
            ApplicationName = typeof(TestApp).Assembly.GetName().Name,
            EnvironmentName = Environments.Development,
        });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        configureServices(builder);

        var app = builder.Build();
        try
        {
            configureApp(app);
            await app.StartAsync(Token);
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }

        return new TestApp(app);
    }

    // Builds the endpoints (and so runs the endpoint filter factories).
    public IReadOnlyList<Endpoint> Endpoints() => [.. ((IEndpointRouteBuilder)App).DataSources.SelectMany(source => source.Endpoints)];

    public Task<HttpResponseMessage> PostAsync<T>(string url, T value) => Client.PostAsJsonAsync(url, value, Token);

    public Task<HttpResponseMessage> GetAsync(string url) => Client.GetAsync(new Uri(url, UriKind.Relative), Token);

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync(Token);
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    public static Dictionary<string, string[]> ReadMap(JsonElement json, string property)
    {
        var map = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var entry in json.GetProperty(property).EnumerateObject())
        {
            map[entry.Name] = [.. entry.Value.EnumerateArray().Select(value => value.GetString()!)];
        }

        return map;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await App.DisposeAsync();
    }
}

// Keeps every log entry, for tests that check what was logged.
internal sealed class CollectingLoggerProvider : ILoggerProvider
{
    private readonly List<(string Category, LogLevel Level, string Message)> _entries = [];

    public IReadOnlyList<(string Category, LogLevel Level, string Message)> Entries
    {
        get
        {
            lock (_entries)
            {
                return [.. _entries];
            }
        }
    }

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class Logger(CollectingLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (provider._entries)
            {
                provider._entries.Add((category, logLevel, formatter(state, exception)));
            }
        }
    }
}
