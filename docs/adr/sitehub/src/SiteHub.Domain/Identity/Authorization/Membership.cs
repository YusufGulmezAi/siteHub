using SiteHub.Domain.Common;

namespace SiteHub.Domain.Identity.Authorization;

public readonly record struct MembershipId(Guid Value) : ITypedId<MembershipId>
{
    public static MembershipId New() => new(Guid.CreateVersion7());
    public static MembershipId FromGuid(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Üyelik kaydı (ADR-0011 §9): bir LoginAccount'un hangi bağlamda (Organization/
/// Site/ServiceOrganization) hangi rolde olduğu.
///
/// ÖNEMLİ: Malik/Hissedar/Kiracı membership olarak TUTULMAZ (ADR-0011 §12.3
/// reddedildi) — UnitPeriod tablosundan türer. Böylece dönem kapandığında
/// malik yetkisini otomatik kaybeder.
///
/// ÇOKLU ROL:
/// Aynı kullanıcının birden fazla aktif Membership'i olabilir (örn. Ahmet hem
/// ABC organizasyonunda yönetici hem DEF sitesinde bekçi). Her URL erişiminde
/// o URL'in context'ine uyan Membership seçilir.
///
/// VALIDITY:
/// ValidFrom/ValidTo sözleşmeli personel için. Dönem dışına çıkınca login
/// mümkün olur ama context erişimi kesilir (Membership inactive).
/// </summary>
public sealed class Membership : AuditableAggregateRoot<MembershipId>
{
    public LoginAccountId LoginAccountId { get; private set; }
    public MembershipContextType ContextType { get; private set; }

    /// <summary>
    /// Context ID — System scope'ta null, diğerlerinde zorunlu.
    /// Organization → OrganizationId.Value
    /// Site → SiteId.Value
    /// vs.
    /// </summary>
    public Guid? ContextId { get; private set; }

    public RoleId RoleId { get; private set; }

    public DateTimeOffset? ValidFrom { get; private set; }
    public DateTimeOffset? ValidTo { get; private set; }

    public bool IsActive { get; private set; }

    private Membership() : base() { }

    private Membership(
        MembershipId id,
        LoginAccountId loginAccountId,
        MembershipContextType contextType,
        Guid? contextId,
        RoleId roleId,
        DateTimeOffset? validFrom,
        DateTimeOffset? validTo)
        : base(id)
    {
        LoginAccountId = loginAccountId;
        ContextType = contextType;
        ContextId = contextId;
        RoleId = roleId;
        ValidFrom = validFrom;
        ValidTo = validTo;
        IsActive = true;
    }

    public static Membership Create(
        LoginAccountId loginAccountId,
        MembershipContextType contextType,
        Guid? contextId,
        RoleId roleId,
        DateTimeOffset? validFrom = null,
        DateTimeOffset? validTo = null)
    {
        if (contextType == MembershipContextType.System)
        {
            if (contextId.HasValue)
                throw new BusinessRuleViolationException(
                    "System scope'ta ContextId null olmalıdır.");
        }
        else
        {
            if (!contextId.HasValue || contextId.Value == Guid.Empty)
                throw new BusinessRuleViolationException(
                    $"{contextType} scope'ta ContextId zorunludur.");
        }

        if (validFrom.HasValue && validTo.HasValue && validFrom >= validTo)
            throw new BusinessRuleViolationException(
                "ValidFrom, ValidTo'dan önce olmalı.");

        return new Membership(
            MembershipId.New(),
            loginAccountId, contextType, contextId, roleId,
            validFrom, validTo);
    }

    /// <summary>Validity tarihlerini günceller.</summary>
    public void UpdateValidity(DateTimeOffset? validFrom, DateTimeOffset? validTo)
    {
        if (validFrom.HasValue && validTo.HasValue && validFrom >= validTo)
            throw new BusinessRuleViolationException("ValidFrom, ValidTo'dan önce olmalı.");

        ValidFrom = validFrom;
        ValidTo = validTo;
    }

    /// <summary>Rolü değiştirir (örn. terfi).</summary>
    public void ChangeRole(RoleId newRoleId)
    {
        if (RoleId == newRoleId) return;
        RoleId = newRoleId;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    /// <summary>
    /// Bu üyelik şu anda geçerli mi?
    /// - IsActive = true
    /// - Validity aralığı içinde
    /// </summary>
    public bool IsEffectiveAt(DateTimeOffset at)
    {
        if (!IsActive) return false;
        if (ValidFrom.HasValue && at < ValidFrom.Value) return false;
        if (ValidTo.HasValue && at > ValidTo.Value) return false;
        return true;
    }
}
