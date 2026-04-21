using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace SiteHub.Application;

/// <summary>
/// Application katmanı DI kayıtları — MediatR, FluentValidation, use case handler'ları.
///
/// <para>MediatR: <c>AddMediatR(...)</c> assembly taraması ile tüm IRequestHandler'ları bulur.</para>
/// <para>FluentValidation: <c>AddValidatorsFromAssembly(...)</c> tüm AbstractValidator&lt;T&gt;'leri kayıt eder.</para>
/// <para>Pipeline behavior'ları (validation, logging) v2'de eklenecek.</para>
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<IApplicationMarker>());

        services.AddValidatorsFromAssemblyContaining<IApplicationMarker>(
            includeInternalTypes: true);

        return services;
    }
}
