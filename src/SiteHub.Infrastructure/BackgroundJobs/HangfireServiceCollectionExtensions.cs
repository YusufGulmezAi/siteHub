using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SiteHub.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire kurulumu — periyodik ve zamanlanmış arka plan işleri.
///
/// <para>Storage: PostgreSQL (<c>hangfire</c> schema'sı — mevcut DB, ayrı namespace).</para>
///
/// <para>Dashboard: <c>/hangfire</c> — sadece localhost'tan erişilir
/// (<see cref="LocalhostOnlyDashboardAuthFilter"/>).</para>
///
/// <para>İlk çalıştırmada Hangfire kendi tablolarını oluşturur
/// (<c>PrepareSchemaIfNecessary = true</c>). Schema ismi: "hangfire".</para>
/// </summary>
public static class HangfireServiceCollectionExtensions
{
    public const string SchemaName = "hangfire";

    public static IServiceCollection AddSiteHubHangfire(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Postgres boş. env.ps1 yüklü mü?");

        services.AddHangfire(cfg =>
        {
            cfg.SetDataCompatibilityLevel(CompatibilityLevel.Version_180);
            cfg.UseSimpleAssemblyNameTypeSerializer();
            cfg.UseRecommendedSerializerSettings();

            cfg.UsePostgreSqlStorage(options =>
            {
                options.UseNpgsqlConnection(connectionString);
            },
            new PostgreSqlStorageOptions
            {
                SchemaName = SchemaName,
                PrepareSchemaIfNecessary = true,
                QueuePollInterval = TimeSpan.FromSeconds(15),
                InvisibilityTimeout = TimeSpan.FromMinutes(5),
                DistributedLockTimeout = TimeSpan.FromMinutes(1),
            });
        });

        services.AddHangfireServer(options =>
        {
            options.ServerName = $"sitehub-{Environment.MachineName}";
            options.WorkerCount = Math.Min(Environment.ProcessorCount * 2, 20);
            options.Queues = new[] { "default", "cleanup", "notifications" };
        });

        return services;
    }

    /// <summary>
    /// <c>/hangfire</c> dashboard'u mount eder. Localhost dışı erişim engellenir.
    ///
    /// <para>Bu metot Infrastructure tarafında tutulur ki ManagementPortal
    /// Hangfire.AspNetCore'u direkt reference etmek zorunda kalmasın.</para>
    /// </summary>
    public static WebApplication UseSiteHubHangfireDashboard(this WebApplication app)
    {
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = new[] { new LocalhostOnlyDashboardAuthFilter() },
            DashboardTitle = "SiteHub — Background Jobs",
            DisplayStorageConnectionString = false,
        });

        return app;
    }
}
