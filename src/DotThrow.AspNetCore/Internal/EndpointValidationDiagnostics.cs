using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DotThrow.AspNetCore.Internal;

// Collects WithValidation<T>() mistakes found while endpoints are built. Registered by
// ValidateEndpointsOnStart(), which builds every endpoint at startup and throws when any is found.
internal sealed class EndpointValidationDiagnostics
{
    private readonly List<string> _problems = [];

    public void Add(string problem)
    {
        lock (_problems)
        {
            _problems.Add(problem);
        }
    }

    public void ThrowIfAny()
    {
        lock (_problems)
        {
            if (_problems.Count == 0)
            {
                return;
            }

            var message = new StringBuilder("Endpoint validation is not set up correctly:");
            foreach (var problem in _problems)
            {
                message.AppendLine().Append(" - ").Append(problem);
            }

            throw new InvalidOperationException(message.ToString());
        }
    }
}

// Runs while the host starts, after the app's middleware (and so every Map call) is in place,
// before the server accepts requests.
internal sealed class EndpointValidationStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        var services = app.ApplicationServices;
        _ = services.GetRequiredService<EndpointDataSource>().Endpoints;
        services.GetRequiredService<EndpointValidationDiagnostics>().ThrowIfAny();
    };
}
