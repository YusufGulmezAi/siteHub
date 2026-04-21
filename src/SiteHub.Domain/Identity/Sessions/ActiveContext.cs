using SiteHub.Domain.Identity.Authorization;

namespace SiteHub.Domain.Identity.Sessions;

/// <summary>
/// Session içinde "şu an hangi bağlamdayım" bilgisi (ADR-0011 §7.7).
///
/// URL şemasına göre set edilir:
/// - /c/system/... → ContextType=System, ContextId=null
/// - /c/org/{orgCode}/... → ContextType=Organization, ContextCode=orgCode, ContextId=resolved Guid
/// - /c/site/{siteCode}/... → ContextType=Site, ContextCode=siteCode, ContextId=resolved Guid
///
/// Aynı tab'da URL değiştikçe ActiveContext yenilenir — session hep aynı.
///
/// PermissionSnapshot: O an'ın seçili context'ine göre hesaplanmış izin set'i.
/// Memberships'ten compute edilir (ADR-0011 §8.1), cache edilir, context değişince tazelenir.
/// </summary>
public sealed record ActiveContext(
    MembershipContextType ContextType,
    Guid? ContextId,
    string? ContextCode,
    string? ContextDisplayName,
    IReadOnlySet<string> PermissionSnapshot);
