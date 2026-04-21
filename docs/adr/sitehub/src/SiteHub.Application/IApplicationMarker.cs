namespace SiteHub.Application;

/// <summary>
/// Application katmanı için assembly marker — bu sınıf MediatR ve FluentValidation
/// registration'da assembly'yi bulmak için kullanılır.
///
/// Örnek kullanım (Infrastructure DependencyInjection.cs'te):
///   services.AddMediatR(cfg =>
///       cfg.RegisterServicesFromAssembly(typeof(IApplicationMarker).Assembly));
/// </summary>
public interface IApplicationMarker { }
