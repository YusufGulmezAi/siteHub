using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SiteHub.Infrastructure.Persistence;

/// <summary>
/// EF Core Tools (dotnet ef) design-time'da DbContext'i oluşturmak için kullanır.
///
/// <para>Normal çalışma zamanında DbContext, ASP.NET Core'un DI container'ı tarafından
/// oluşturulur. Ama "dotnet ef migrations add" gibi komutlar DI container olmadan
/// çalışır — bu factory devreye girer.</para>
///
/// <para>Connection string <c>ConnectionStrings__Postgres</c> environment variable'dan
/// okunur (<c>.env</c>'den <c>env.ps1</c> ile yüklenir). Env yoksa exception fırlatılır
/// — böylece sessizce yanlış DB'ye bağlanma riski yok.</para>
/// </summary>
public sealed class SiteHubDbContextFactory : IDesignTimeDbContextFactory<SiteHubDbContext>
{
    public SiteHubDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? throw new InvalidOperationException(
                "ConnectionStrings__Postgres env var'ı yok. " +
                "Kurulum: `.\\env.ps1` komutuyla .env'i yükleyin. " +
                ".env dosyası yoksa .env.example'dan kopyalayın.");

        var options = new DbContextOptionsBuilder<SiteHubDbContext>()
            .UseNpgsql(connectionString, npg => npg
                .MigrationsHistoryTable("__ef_migrations_history", "public")
                .MigrationsAssembly(typeof(SiteHubDbContextFactory).Assembly.GetName().Name))
            .Options;

        return new SiteHubDbContext(options);
    }
}
