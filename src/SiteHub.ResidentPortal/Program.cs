using System.Globalization;
using MudBlazor.Services;
using Serilog;
using SiteHub.Application;
using SiteHub.Infrastructure;
using SiteHub.ResidentPortal.Components;

// ─── Türkçe culture'ı bütün thread'lerde varsayılan yap (ADR-0009) ──────────
var turkishCulture = CultureInfo.GetCultureInfo("tr-TR");
CultureInfo.DefaultThreadCurrentCulture = turkishCulture;
CultureInfo.DefaultThreadCurrentUICulture = turkishCulture;
CultureInfo.CurrentCulture = turkishCulture;
CultureInfo.CurrentUICulture = turkishCulture;

// ═══════════════════════════════════════════════════════════════════════════════
// Malik/Sakin Portalı — Program.cs
//
// Management Portal'dan ayrı bir Blazor Server uygulaması.
// Farklı auth scheme, farklı layout, sadece aidat/ödeme/talep gibi
// sakin-odaklı özellikler içerecek.
// ═══════════════════════════════════════════════════════════════════════════════

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    Log.Information("SiteHub Resident Portal başlatılıyor...");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "SiteHub.ResidentPortal")
        .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName));

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddMudServices();

    // ─── Infrastructure (EF Core + PostgreSQL) ───────────────────────────────
    builder.Services.AddInfrastructure(builder.Configuration);

    // ─── Application (MediatR + FluentValidation) ────────────────────────────
    builder.Services.AddApplication();

    builder.Services.AddHealthChecks();

    // TODO (sonraki adımlar):
    //   - builder.Services.AddSingleton(TimeProvider.System);
    //   - builder.Services.AddSingleton<ITurkeyClock, TurkeyClock>();
    //   - AddApplication(); AddInfrastructure(cfg);
    //   - Ayrı auth scheme: "ResidentAuth" (cookie adı ".SiteHub.Resident",
    //     Management Portal cookie'siyle çakışmaz)
    //   - Sadece Unit seviyesinde Membership olanlar giriş yapabilir
    //   - Route kısıtlamaları (sadece sakin ekranları: aidat, ödeme, talep, duyuru)
    //   - PII masking + structured logging (ADR-0006)

    var app = builder.Build();

    // ─── Database migrate/seed burada YAPILMAZ ──────────────────────────────
    // ManagementPortal startup'ta InitializeDatabaseAsync() çağırır. İki portal
    // aynı anda seed çalıştırsa idempotent ama yarış durumunda duplicate key
    // exception riski var. Tek portal sorumlu olsun.
    // Production'da her iki portal startup'ta migrate check yapabilir (sadece
    // `Database.MigrateAsync()` — idempotent ve thread-safe), ama seed
    // sadece ManagementPortal'da.

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseSerilogRequestLogging();
    app.UseStaticFiles();
    app.UseAntiforgery();

    app.MapHealthChecks("/health");

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    Log.Information("SiteHub Resident Portal hazır. URL: {Urls}",
        string.Join(", ", app.Urls));

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "SiteHub Resident Portal başlatılamadı.");
}
finally
{
    Log.CloseAndFlush();
}
