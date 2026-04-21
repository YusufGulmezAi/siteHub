using System.Globalization;
using MudBlazor.Services;
using Serilog;
using SiteHub.Application;
using SiteHub.Infrastructure;
using SiteHub.ManagementPortal.Components;

// ═══════════════════════════════════════════════════════════════════════════════
// Yönetici Portalı — Program.cs
//
// Bu dosya, uygulamanın giriş noktasıdır. Üç aşama:
//   1. Bootstrap Logger (henüz DI container yok — ilk loglar için)
//   2. Builder configuration (servisler kaydedilir)
//   3. App configuration (middleware pipeline kurulur)
//
// Dikkat: Hatayı erken yakalamak için tüm builder/app kısmı try/catch içinde.
// ═══════════════════════════════════════════════════════════════════════════════

// ─── 0. Türkçe culture'ı bütün thread'lerde varsayılan yap (ADR-0009) ────────
// Bu satır NEDEN ÖNEMLİ:
//   - string.Compare, ToUpper, ToLower, DateTime.Parse gibi API'ler bu culture'a göre davranır
//   - 'I/ı/İ/i' Türkçe kurallarına göre dönüştürülür
//   - Tarih/para formatı Türkçe gösterilir
//   - MudBlazor bileşenleri Türkçe ay isimleri kullanır
//
// NOT: Uzun vadede kullanıcı tercih ettiği kültürü seçebilmeli (i18n). Şimdi
// tek kültür (tr-TR) destekliyoruz çünkü hedef pazar Türkiye.
var turkishCulture = CultureInfo.GetCultureInfo("tr-TR");
CultureInfo.DefaultThreadCurrentCulture = turkishCulture;
CultureInfo.DefaultThreadCurrentUICulture = turkishCulture;
CultureInfo.CurrentCulture = turkishCulture;
CultureInfo.CurrentUICulture = turkishCulture;

// ─── 1. Bootstrap Logger ─────────────────────────────────────────────────────
// DI container hazır olmadan önceki hatalar buraya düşer
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    // Loglar culture-invariant formatla (farklı ortamlarda aynı görünüm, log
    // parsing tool'ları için tutarlılık — kullanıcıya gösterilmiyor)
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    Log.Information("SiteHub Management Portal başlatılıyor...");

    var builder = WebApplication.CreateBuilder(args);

    // ─── 2. Serilog (gerçek logger) ──────────────────────────────────────────
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "SiteHub.ManagementPortal")
        .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName));

    // ─── 3. Blazor + Render Modes ────────────────────────────────────────────
    // Interactive Server aktif — SignalR circuit kullanır (anlık UI için)
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    // ─── 4. MudBlazor ────────────────────────────────────────────────────────
    builder.Services.AddMudServices();

    // ─── Infrastructure (EF Core + PostgreSQL) ───────────────────────────────
    builder.Services.AddInfrastructure(builder.Configuration);

    // ─── Application (MediatR + FluentValidation) ────────────────────────────
    builder.Services.AddApplication();

    // ─── Demo Services (ileride backend'e bağlanacak) ────────────────────────
    builder.Services.AddSingleton<SiteHub.ManagementPortal.Services.Contexts.DemoContextService>();

    // ─── HTTP / Health ────────────────────────────────────────────────────
    builder.Services.AddHealthChecks();

    // TODO (sonraki adımlar):
    //   - builder.Services.AddSingleton(TimeProvider.System);
    //   - builder.Services.AddSingleton<ITurkeyClock, TurkeyClock>();
    //   - builder.Services.AddApplication();        (MediatR + pipeline behaviors:
    //                                               Validation, Logging, ContextAuthorization)
    //   - Authentication: ASP.NET Identity + Cookie (scheme: "MgmtAuth")
    //     - PasswordHasher, 2FA (TOTP/SMS/Email), Lockout
    //     - Login sonrası context SEÇİLMEZ — default context atanır (ADR-0005)
    //   - HTTP request logging: Serilog + PII masking (ADR-0006)
    //   - OpenTelemetry: distributed tracing + correlation ID
    //   - Rate limiting (per IP + per user)
    //   - Anti-CSRF token (AddAntiforgery zaten var)
    //   - Health checks: DB, Redis bağlantı kontrolü
    //
    //   Şu an AddInfrastructure() ile gelen:
    //     ✓ DbContext + Audit interceptor + Soft-delete filter
    //     ✓ Redis ICacheStore (generic cache — ADR-0011 §8, ADR-0016)
    //     ✓ Feistel ICodeGenerator (ADR-0012 §11)
    //     ✓ TimeProvider.System (singleton)
    //
    //   - Hangfire (arka plan işleri — aidat tahakkuku, SMS kuyruğu, vs.):
    //       builder.Services.AddHangfire(cfg => cfg
    //           .UsePostgreSqlStorage(opts => opts.UseNpgsqlConnection(connStr),
    //               new PostgreSqlStorageOptions { SchemaName = "hangfire" }));
    //       builder.Services.AddHangfireServer();
    //       app.UseHangfireDashboard("/hangfire", new DashboardOptions {
    //           Authorization = [ /* AdminOnlyAuthFilter */ ]
    //       });

    var app = builder.Build();

    // ─── 5. Database migrate + seed (startup) ────────────────────────────────
    // Migration'ları uygular ve referans verileri (Geography) seed eder.
    // İdempotent — zaten seed edilmiş verileri atlar. Her startup'ta güvenli.
    // Production'da bu adım ya startup'ta (burada), ya da CI/CD pipeline'da
    // ayrı `dotnet ef database update` komutuyla çalıştırılır.
    await app.Services.InitializeDatabaseAsync();

    // ─── 6. Middleware pipeline ──────────────────────────────────────────────
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseHttpsRedirection();

    // Serilog request logging — her HTTP request için structured log
    app.UseSerilogRequestLogging();

    app.UseStaticFiles();
    app.UseAntiforgery();

    app.MapHealthChecks("/health");

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    Log.Information("SiteHub Management Portal hazır. URL: {Urls}",
        string.Join(", ", app.Urls));

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "SiteHub Management Portal başlatılamadı.");
}
finally
{
    Log.CloseAndFlush();
}
