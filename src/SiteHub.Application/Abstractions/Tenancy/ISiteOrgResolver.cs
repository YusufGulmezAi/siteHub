namespace SiteHub.Application.Abstractions.Tenancy;

/// <summary>
/// SiteId → OrganizationId resolver (ADR-0014 A.4.b, Faz F.4).
///
/// <para><b>Amaç:</b> Site context'te çalışan kullanıcının parent Organization'ını
/// çözmek. <see cref="ITenantContext.OrganizationId"/> bu resolver'ı kullanır.</para>
///
/// <para><b>Cache:</b> IMemoryCache global + 5 dk TTL. Site→Organization mapping
/// nadir değişir (site başka firmaya devri gibi yıllık olay); cache güvenlidir.
/// Site mutation handler'ları (Update/Delete) explicit <see cref="InvalidateCacheFor"/>
/// çağırarak stale veri önler.</para>
///
/// <para><b>Not:</b> Silinmiş Site'lar da OrganizationId döner — RLS fail-closed
/// mantığı DeletedAt filter'ını EF Core'un global query filter'ı ile sağlar,
/// resolver bu kontrolü yapmaz (cache invalidation karmaşıklaşmasın).</para>
/// </summary>
public interface ISiteOrgResolver
{
    /// <summary>
    /// <paramref name="siteId"/> için parent OrganizationId'yi döner.
    /// Cache'te varsa oradan, yoksa DB'den çözer ve cache'e yazar.
    /// </summary>
    /// <returns>Site varsa OrganizationId; Site yoksa (geçersiz id) null.</returns>
    Task<Guid?> GetOrganizationIdAsync(Guid siteId, CancellationToken ct = default);

    /// <summary>
    /// Site güncelleme/silme handler'ları cache invalidation için çağırır.
    /// Sonraki <see cref="GetOrganizationIdAsync"/> çağrısı DB'den taze okur.
    /// </summary>
    void InvalidateCacheFor(Guid siteId);
}
