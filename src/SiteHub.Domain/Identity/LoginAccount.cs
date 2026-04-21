using SiteHub.Domain.Common;

namespace SiteHub.Domain.Identity;

/// <summary>LoginAccount için strongly-typed ID.</summary>
public readonly record struct LoginAccountId(Guid Value) : ITypedId<LoginAccountId>
{
    public static LoginAccountId New() => new(Guid.CreateVersion7());
    public static LoginAccountId FromGuid(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Person'a bağlı giriş hesabı (ADR-0011 §2).
///
/// Person 1:0..1 LoginAccount — yani bir Person'ın EN FAZLA bir LoginAccount'u olur.
/// LoginAccount olmadan Person sistemde pasif bir kayıttır (audit/hissedar geçmişi için).
///
/// GÜVENLİK KATMANLARI (ADR-0011 §3.2):
/// - ValidFrom / ValidTo tarih aralığı (sözleşmeli çalışan, proje bazlı geçici hesap)
/// - IpWhitelist (CIDR) — sadece belirli IP'lerden giriş
/// - LoginSchedule (JSON) — gün+saat kısıtı (Pzt-Cum 08:00-18:00 vb.)
/// - FailedLoginCount + LockoutUntil — brute-force koruma
/// - IsActive — hızlı devre dışı bırakma
///
/// LOGIN EMAIL:
/// Person.Email'den AYRI — login için ayrı e-posta kullanılabilir (örn. iş
/// email'i iletişim için, başka email login için).
///
/// ŞİFRE YÖNETİMİ:
/// PasswordHash alanı ASP.NET Core Identity'nin PasswordHasher'ı ile doldurulur.
/// Bu entity direkt hash tutmaz — sadece hash'i saklar.
/// </summary>
public sealed class LoginAccount : AuditableAggregateRoot<LoginAccountId>
{
    public PersonId PersonId { get; private set; }
    public string LoginEmail { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public bool IsActive { get; private set; }

    // Geçerlilik aralığı
    public DateTimeOffset? ValidFrom { get; private set; }
    public DateTimeOffset? ValidTo { get; private set; }

    // IP kısıtı — virgülle ayrılmış CIDR'ler. Boş/null = kısıt yok.
    // Örn: "10.0.0.0/8,192.168.1.0/24"
    public string? IpWhitelist { get; private set; }

    // Saat kısıtı — JSON (LoginSchedule). Boş/null = kısıt yok (7/24).
    public string? LoginScheduleJson { get; private set; }

    // Login istatistikleri
    public DateTimeOffset? LastLoginAt { get; private set; }
    public string? LastLoginIp { get; private set; }
    public int FailedLoginCount { get; private set; }
    public DateTimeOffset? LockoutUntil { get; private set; }

    private LoginAccount() : base() { }

    private LoginAccount(LoginAccountId id, PersonId personId, string loginEmail, string passwordHash)
        : base(id)
    {
        PersonId = personId;
        LoginEmail = loginEmail;
        PasswordHash = passwordHash;
        IsActive = true;
        FailedLoginCount = 0;
    }

    // ─── Factory ─────────────────────────────────────────────────────────

    /// <summary>
    /// Person'a bağlı yeni login account yaratır. PasswordHash önceden
    /// hash'lenmiş olarak gelir (application katmanında IPasswordHasher ile).
    /// </summary>
    public static LoginAccount Create(PersonId personId, string loginEmail, string passwordHash)
    {
        ValidateLoginEmail(loginEmail);
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new BusinessRuleViolationException("Şifre hash'i boş olamaz.");

        return new LoginAccount(LoginAccountId.New(), personId, loginEmail.Trim().ToLowerInvariant(), passwordHash);
    }

    // ─── E-posta güncelleme ──────────────────────────────────────────────

    public void ChangeLoginEmail(string newEmail)
    {
        ValidateLoginEmail(newEmail);
        LoginEmail = newEmail.Trim().ToLowerInvariant();
    }

    // ─── Şifre değişimi ──────────────────────────────────────────────────
    // NOT: Yeni şifre hash'i zaten application katmanında hesaplanmış olarak gelir.
    // Bu metod ayrıca "tüm oturumları kapat" event'i yaymalı (ADR-0011 §7.5) —
    // bu v1'de Application handler'da yapılır.

    public void ChangePasswordHash(string newHash)
    {
        if (string.IsNullOrWhiteSpace(newHash))
            throw new BusinessRuleViolationException("Şifre hash'i boş olamaz.");
        PasswordHash = newHash;
        // FailedLoginCount'u sıfırla — başarılı şifre değişimi
        FailedLoginCount = 0;
        LockoutUntil = null;
    }

    // ─── Aktivasyon durumu ──────────────────────────────────────────────

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    // ─── Geçerlilik aralığı ──────────────────────────────────────────────

    public void SetValidity(DateTimeOffset? validFrom, DateTimeOffset? validTo)
    {
        if (validFrom.HasValue && validTo.HasValue && validFrom >= validTo)
            throw new BusinessRuleViolationException(
                "ValidFrom, ValidTo'dan önce olmalı.");

        ValidFrom = validFrom;
        ValidTo = validTo;
    }

    // ─── IP whitelist ────────────────────────────────────────────────────

    /// <summary>
    /// IP whitelist'i günceller. CIDR validasyonu application katmanında
    /// FluentValidation ile yapılır — domain sadece format uzunluğunu kontrol eder.
    /// </summary>
    public void SetIpWhitelist(string? cidrsCommaSeparated)
    {
        if (cidrsCommaSeparated is not null && cidrsCommaSeparated.Length > 2000)
            throw new BusinessRuleViolationException(
                "IP whitelist en fazla 2000 karakter olabilir.");

        IpWhitelist = string.IsNullOrWhiteSpace(cidrsCommaSeparated)
            ? null
            : cidrsCommaSeparated.Trim();
    }

    // ─── Login schedule ──────────────────────────────────────────────────

    /// <summary>
    /// Saat bazlı giriş kısıtını günceller.
    /// JSON parse + validasyon application katmanında. Burada sadece uzunluk kontrolü.
    /// </summary>
    public void SetLoginSchedule(string? json)
    {
        if (json is not null && json.Length > 5000)
            throw new BusinessRuleViolationException(
                "LoginSchedule JSON'ı en fazla 5000 karakter olabilir.");

        LoginScheduleJson = string.IsNullOrWhiteSpace(json) ? null : json.Trim();
    }

    // ─── Login olayları ──────────────────────────────────────────────────

    /// <summary>Başarılı login sonrası çağrılır.</summary>
    public void RecordSuccessfulLogin(DateTimeOffset at, string? ip)
    {
        LastLoginAt = at;
        LastLoginIp = ip;
        FailedLoginCount = 0;
        LockoutUntil = null;
    }

    /// <summary>
    /// Başarısız login sonrası çağrılır. Eşik aşılırsa otomatik lockout.
    /// </summary>
    public void RecordFailedLogin(
        DateTimeOffset at,
        int lockoutThreshold,
        TimeSpan lockoutDuration)
    {
        FailedLoginCount++;
        if (FailedLoginCount >= lockoutThreshold)
        {
            LockoutUntil = at.Add(lockoutDuration);
        }
    }

    /// <summary>Manuel lockout (admin tarafından).</summary>
    public void LockUntil(DateTimeOffset until)
    {
        LockoutUntil = until;
    }

    /// <summary>Manuel unlock.</summary>
    public void Unlock()
    {
        FailedLoginCount = 0;
        LockoutUntil = null;
    }

    // ─── Durum sorguları (hesaplamalar) ─────────────────────────────────

    /// <summary>
    /// Hesap şu anda giriş yapabilir mi? (Cascade kontrol)
    /// - IsActive = true
    /// - Lockout süresi dolmuş olmalı
    /// - Geçerlilik aralığı içinde olmalı (ValidFrom/ValidTo)
    ///
    /// IP ve saat kontrolü BU METODDA YOK — onlar login handler'da ayrı.
    /// </summary>
    public bool IsLoginAllowedAt(DateTimeOffset at)
    {
        if (!IsActive) return false;
        if (LockoutUntil.HasValue && at < LockoutUntil.Value) return false;
        if (ValidFrom.HasValue && at < ValidFrom.Value) return false;
        if (ValidTo.HasValue && at > ValidTo.Value) return false;
        return true;
    }

    // ─── Validation helpers ──────────────────────────────────────────────

    private static void ValidateLoginEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new BusinessRuleViolationException("Login e-posta zorunludur.");
        if (email.Length > 320)
            throw new BusinessRuleViolationException("Login e-posta en fazla 320 karakter olabilir.");
        if (!email.Contains('@'))
            throw new BusinessRuleViolationException("Geçerli e-posta formatı gerekli.");
    }
}
