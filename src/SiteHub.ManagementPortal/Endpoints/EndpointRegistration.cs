using System.Reflection;
using Microsoft.AspNetCore.Routing;

namespace SiteHub.ManagementPortal.Endpoints;

/// <summary>
/// Tüm <see cref="IEndpointModule"/> implementasyonlarını assembly'den bulur ve map eder.
///
/// <para>Program.cs'den: <c>app.MapSiteHubEndpoints();</c></para>
///
/// <para>Modüller parametresiz public constructor'a sahip olmalı. (Gelecekte DI gerekirse
/// <c>IServiceProvider</c>'dan resolve edilir.)</para>
/// </summary>
public static class EndpointRegistration
{
    public static IEndpointRouteBuilder MapSiteHubEndpoints(this IEndpointRouteBuilder app)
    {
        var moduleType = typeof(IEndpointModule);

        var modules = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && moduleType.IsAssignableFrom(t))
            .Select(t => (IEndpointModule)Activator.CreateInstance(t)!)
            .ToList();

        foreach (var module in modules)
            module.MapEndpoints(app);

        return app;
    }
}
