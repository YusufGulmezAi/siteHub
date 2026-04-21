using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SiteHub.Application.Abstractions.Audit;
using SiteHub.Application.Abstractions.Authentication;
using SiteHub.Application.Abstractions.CodeGeneration;
using SiteHub.Application.Abstractions.Context;
using SiteHub.Application.Abstractions.Notifications;
using SiteHub.Application.Abstractions.Persistence;
using SiteHub.Application.Abstractions.Sessions;
using SiteHub.Infrastructure.Authentication;
using SiteHub.Infrastructure.BackgroundJobs;
using SiteHub.Infrastructure.Caching;
using SiteHub.Infrastructure.CodeGeneration;
using SiteHub.Infrastructure.Connection;
using SiteHub.Infrastructure.Context;
using SiteHub.Infrastructure.Identity;
using SiteHub.Infrastructure.Notifications;
using SiteHub.Infrastructure.Persistence;
using SiteHub.Infrastructure.Persistence.Interceptors;
using SiteHub.Infrastructure.Persistence.Seed;
using SiteHub.Infrastructure.Sessions;
using SiteHub.Shared.Caching;
using StackExchange.Redis;

namespace SiteHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "'ConnectionStrings:Postgres' yapılandırması bulunamadı. " +
                ".env dosyasını kontrol edin.");

        // ─── Temel servisler ────────────────────────────────────────────
        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();

        // ─── Configuration: LoginSecurity (dev=1 dk, prod=15 dk) ────────
        services.Configure<LoginSecurityOptions>(
            configuration.GetSection(LoginSecurityOptions.SectionName));

        // ─── Audit altyapısı (ADR-0006) ─────────────────────────────────
        services.AddScoped<ICurrentUserService, NullCurrentUserService>();
        services.AddScoped<ICurrentConnectionInfo, HttpCurrentConnectionInfo>();
        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<SiteHubDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npg => npg
                .MigrationsHistoryTable("__ef_migrations_history", "public")
                .MigrationsAssembly(typeof(SiteHubDbContextFactory).Assembly.GetName().Name));

            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());

            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
            }
        });

        // Application layer soyutlaması — aynı scoped instance, interface alias
        services.AddScoped<ISiteHubDbContext>(sp => sp.GetRequiredService<SiteHubDbContext>());

        // ─── Redis Cache (ADR-0011 §7-8 + ADR-0016) ─────────────────────
        services.AddRedisCache(configuration);

        // ─── Kod üretimi (ADR-0012 §11 — Feistel + Sequence) ────────────
        services.AddCodeGeneration();

        // ─── Authentication (ADR-0011) ──────────────────────────────────
        services.AddAuthentication();

        // ─── Notifications (Email + SMS) ────────────────────────────────
        services.AddNotifications(configuration);

        // ─── Background Jobs (Hangfire) ─────────────────────────────────
        services.AddSiteHubHangfire(configuration);
        services.AddScoped<PasswordResetTokenCleanupJob>();

        // ─── Seed servisleri ────────────────────────────────────────────
        services.AddScoped<TurkeyGeographySeeder>();
        services.AddScoped<PermissionSynchronizer>();
        services.AddScoped<SystemRolesSeeder>();
        services.AddScoped<DevelopmentUsersSeeder>();

        return services;
    }

    private static IServiceCollection AddAuthentication(this IServiceCollection services)
    {
        services.AddSingleton<ISessionStore, RedisSessionStore>();
        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        services.AddSingleton<ITotpService, OtpNetTotpService>();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<I2FARateLimiter, Redis2FARateLimiter>();
        return services;
    }

    private static IServiceCollection AddNotifications(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<ISmsSender, ConsoleSmsSender>();
        return services;
    }

    private static IServiceCollection AddRedisCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisConnection = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException(
                "'ConnectionStrings:Redis' yapılandırması bulunamadı. " +
                ".env dosyasını kontrol edin (REDIS_CONNECTION_STRING).");

        // Multiplexer: uygulama ömrü boyunca TEK instance (singleton).
        // StackExchange.Redis docs: "Reuse ConnectionMultiplexer — do not create per request."
        services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(redisConnection));

        services.AddSingleton<ICacheStore, RedisCacheStore>();

        return services;
    }

    private static IServiceCollection AddCodeGeneration(this IServiceCollection services)
    {
        // Key provider singleton — key'ler uygulama ömrü boyunca cache'lenir
        services.AddSingleton<IFeistelKeyProvider, ConfigurationFeistelKeyProvider>();

        // Code generator scoped — DbContext scoped olduğu için
        services.AddScoped<ICodeGenerator, FeistelCodeGenerator>();

        return services;
    }
}

/// <summary>
/// Startup sırasında çalıştırılacak başlatma/seed metotları.
/// Program.cs'ten: <c>await app.InitializeDatabaseAsync();</c>
/// </summary>
public static class InfrastructureInitializationExtensions
{
    /// <summary>
    /// Migration'ları uygular ve seed verileri çalıştırır (idempotent).
    /// Development'ta sürekli, production'da ilk deployment sonrası tek sefer çağrılır.
    ///
    /// Sıralı:
    /// 1. Database.MigrateAsync — schema oluşturma
    /// 2. PermissionSynchronizer — kod sabitleriyle permissions tablosunu eşitle
    /// 3. SystemRolesSeeder — varsayılan sistem rolleri (permission'lara bağımlı)
    /// 4. TurkeyGeographySeeder — 81 il + 958 ilçe + 4125 mahalle
    /// 5. DevelopmentUsersSeeder — SADECE Development'ta test admin kullanıcısı
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILogger<SiteHubDbContext>>();

        try
        {
            var db = sp.GetRequiredService<SiteHubDbContext>();

            logger.LogInformation("Database migrate başlıyor...");
            await db.Database.MigrateAsync(ct);
            logger.LogInformation("Database migrate tamamlandı.");

            // 1. Permission sync (reflection'dan gelir, seed değil "sync")
            var permSync = sp.GetRequiredService<PermissionSynchronizer>();
            await permSync.SynchronizeAsync(ct);

            // 2. System roles (permissions'a bağımlı)
            var rolesSeeder = sp.GetRequiredService<SystemRolesSeeder>();
            await rolesSeeder.SeedAsync(ct);

            // 3. Geography (bağımsız — sona alabiliriz, büyük veri seti)
            var geoSeeder = sp.GetRequiredService<TurkeyGeographySeeder>();
            await geoSeeder.SeedAsync(ct);

            // 4. Development ortamında test kullanıcısı
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase))
            {
                var devSeeder = sp.GetRequiredService<DevelopmentUsersSeeder>();
                await devSeeder.SeedAsync(ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database başlatma başarısız.");
            throw;
        }
    }
}


