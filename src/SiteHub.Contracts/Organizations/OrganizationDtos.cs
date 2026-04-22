namespace SiteHub.Contracts.Organizations;

/// <summary>
/// Organizasyon dropdown için minimum alan seti.
/// Sistem admin "Hangi organizasyonu göreyim?" dropdown'unda kullanılır.
/// </summary>
public sealed record OrganizationSummaryDto(
    Guid Id,
    long Code,
    string Name,
    bool IsActive);

/// <summary>
/// Organizasyon liste DTO'su. <c>GET /api/organizations</c> response item.
/// <para>Not: Application.Features.Organizations.OrganizationListItemDto'nun kopyası.</para>
/// </summary>
public sealed record OrganizationListItemDto(
    Guid Id,
    long Code,
    string Name,
    string CommercialTitle,
    string TaxId,
    string? Phone,
    string? Email,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>
/// Organizasyon detay DTO'su. Form'u doldurmak için tam alan seti + audit.
/// <para>Not: Application.Features.Organizations.OrganizationDetailDto'nun kopyası.</para>
/// </summary>
public sealed record OrganizationDetailDto(
    Guid Id,
    long Code,
    string Name,
    string CommercialTitle,
    string TaxId,
    string? Address,
    string? Phone,
    string? Email,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? CreatedByName,
    string? UpdatedByName);

