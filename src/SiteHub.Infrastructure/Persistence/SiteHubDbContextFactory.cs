using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SiteHub.Infrastructure.Persistence;

/// <summary>
/// EF Core Tools (dotnet ef) design-time'da DbContext'i oluşturmak için kullanır.
///
/// Normal çalışma zamanında DbContext, ASP.NET Core'un DI container'ı tarafından
/// oluşturulur. Ama "dotnet ef migrations add" gibi komutlar DI container olmadan
/// çalışır — bu factory devreye girer.
///
/// Connection string şu sırayla aranır:
///   1. ConnectionStrings__Postgres environment variable (.env'den gelir)
///   2. Fallback: localhost dev varsayılanı
/// </summary>
public sealed class SiteHubDbContextFactory : IDesignTimeDbContextFactory<SiteHubDbContext>
{
    public SiteHubDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=sitehub;Username=sitehub;Password=sitehub_dev_pw_change_me;Include Error Detail=true;";

        var options = new DbContextOptionsBuilder<SiteHubDbContext>()
            .UseNpgsql(connectionString, npg => npg
                .MigrationsHistoryTable("__ef_migrations_history", "public")
                .MigrationsAssembly(typeof(SiteHubDbContextFactory).Assembly.GetName().Name))
            .Options;

        return new SiteHubDbContext(options);
    }
}
