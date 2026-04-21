using SiteHub.Domain.Common;

namespace SiteHub.Domain.Identity;

/// <summary>Şifre sıfırlama token'ı için strongly-typed ID.</summary>
public readonly record struct PasswordResetTokenId(Guid Value) : ITypedId<PasswordResetTokenId>
{
    public static PasswordResetTokenId New() => new(Guid.CreateVersion7());
    public static PasswordResetTokenId FromGuid(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Kanal seçimi — kullanıcı hangi yolla reset token'ı aldı.
/// ADR-0011 §5.1.
/// </summary>
public enum PasswordResetChannel
{
    Email = 1,
    Sms = 2
}

/// <summary>
/// Şifre sıfırlama token kaydı (ADR-0011 §5.2).
///
/// <para>Güvenlik ilkeleri:</para>
/// <list type="bullet">
///   <item>Token DB'de HASH olarak saklanır (plaintext sızıntı riskine karşı) — SHA-256</item>
///   <item>Tek kullanımlık — UsedAt set edildikten sonra kullanılamaz</item>
///   <item>15 dk TTL — ExpiresAt &lt; now ise geçersiz</item>
///   <item>Her reset talebinde bu LoginAccount için ESKİ açık token'lar invalidate edilir</item>
/// </list>
///
/// <para>Email için: Token plaintext email linkinde gider (<c>/reset-password?token=xxx</c>),
/// hash'lenerek DB'de karşılaştırılır.</para>
///
/// <para>SMS için: 6 haneli numeric kod kullanıcıya gider, yine hash'lenerek saklanır.</para>
/// </summary>
public sealed class PasswordResetToken : Entity<PasswordResetTokenId>
{
    public LoginAccountId LoginAccountId { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public PasswordResetChannel Channel { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }
    public string? UsedFromIp { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? RequestedFromIp { get; private set; }

    private PasswordResetToken() : base() { }

    private PasswordResetToken(
        PasswordResetTokenId id,
        LoginAccountId loginAccountId,
        string tokenHash,
        PasswordResetChannel channel,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt,
        string? requestedFromIp)
        : base(id)
    {
        LoginAccountId = loginAccountId;
        TokenHash = tokenHash;
        Channel = channel;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
        RequestedFromIp = requestedFromIp;
    }

    /// <summary>
    /// Yeni token kaydı yaratır. Plaintext token KAYDEDİLMEZ — hash'lenmiş hali verilir.
    /// </summary>
    public static PasswordResetToken Create(
        LoginAccountId loginAccountId,
        string tokenHash,
        PasswordResetChannel channel,
        TimeSpan ttl,
        DateTimeOffset now,
        string? requestedFromIp)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new BusinessRuleViolationException("Token hash'i boş olamaz.");
        if (ttl <= TimeSpan.Zero)
            throw new BusinessRuleViolationException("TTL pozitif olmalı.");

        return new PasswordResetToken(
            id: PasswordResetTokenId.New(),
            loginAccountId: loginAccountId,
            tokenHash: tokenHash,
            channel: channel,
            expiresAt: now.Add(ttl),
            createdAt: now,
            requestedFromIp: requestedFromIp);
    }

    /// <summary>
    /// Token kullanıldı olarak işaretler. Tekrar kullanılamaz.
    /// </summary>
    public void MarkAsUsed(DateTimeOffset usedAt, string? fromIp)
    {
        if (UsedAt.HasValue)
            throw new BusinessRuleViolationException("Token zaten kullanılmış.");
        UsedAt = usedAt;
        UsedFromIp = fromIp;
    }

    /// <summary>
    /// Token hâlâ kullanılabilir mi?
    /// - Kullanılmamış + expire olmamış.
    /// </summary>
    public bool IsUsable(DateTimeOffset now)
    {
        if (UsedAt.HasValue) return false;
        if (now > ExpiresAt) return false;
        return true;
    }
}
