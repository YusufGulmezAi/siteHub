namespace SiteHub.Domain.Common;

/// <summary>
/// Domain seviyesinde iş kuralı ihlalleri için base exception.
/// Application/Infrastructure bu exception'ları yakalar, ApiError'a çevirir.
///
/// Türetilen sınıflar:
/// - InvalidStateException: entity yanlış durumda (örn. aktif sözleşme silinmeye çalışıldı)
/// - BusinessRuleViolationException: iş kuralı ihlali (örn. hisse toplamı 100% değil)
/// - NotFoundException: aranılan entity yok
/// - ValidationException: input validasyon hatası
/// - ForbiddenException: yetki yok (iş kuralı seviyesinde)
/// </summary>
public abstract class DomainException : Exception
{
    /// <summary>
    /// Makine-okunabilir hata kodu. ApiError.Code'a bu değer aktarılır.
    /// </summary>
    public string Code { get; }

    protected DomainException(string code, string message) : base(message)
    {
        Code = code;
    }

    protected DomainException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }
}

/// <summary>
/// Entity, işlem için uygun durumda değil.
/// Örn: Draft durumundaki sözleşmeyi aktif etmeye çalışmak.
/// </summary>
public sealed class InvalidStateException : DomainException
{
    public InvalidStateException(string message) : base("INVALID_STATE", message) { }
}

/// <summary>
/// Domain invariant veya iş kuralı ihlal edildi.
/// Örn: Hissedarların toplam hissesi 100%'den farklı, IBAN formatı geçersiz.
/// </summary>
public sealed class BusinessRuleViolationException : DomainException
{
    public BusinessRuleViolationException(string message)
        : base("BUSINESS_RULE_VIOLATION", message) { }

    public BusinessRuleViolationException(string code, string message)
        : base(code, message) { }
}

/// <summary>
/// Aranılan entity bulunamadı.
/// </summary>
public sealed class NotFoundException : DomainException
{
    public NotFoundException(string entityName, object key)
        : base("NOT_FOUND", $"{entityName} '{key}' bulunamadı.") { }

    public NotFoundException(string message) : base("NOT_FOUND", message) { }
}

/// <summary>
/// Field-level validation hatası. Birden fazla alan için hata listesi.
/// ApiError.Errors dictionary'ye aktarılır.
/// </summary>
public sealed class ValidationException : DomainException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("VALIDATION_FAILED", "Validasyon hatası — girdileri kontrol edin.")
    {
        Errors = errors;
    }

    public ValidationException(string field, string message)
        : base("VALIDATION_FAILED", "Validasyon hatası — girdileri kontrol edin.")
    {
        Errors = new Dictionary<string, string[]>
        {
            [field] = new[] { message }
        };
    }
}

/// <summary>
/// Kullanıcının bu işlemi yapma yetkisi yok.
/// Yetki kontrolü genelde middleware'de yapılır; bu exception domain'de
/// ekstra iş kuralı ihlali için (örn. yaratıcı sadece kendi izinlerini atayabilir).
/// </summary>
public sealed class ForbiddenException : DomainException
{
    public ForbiddenException(string message) : base("FORBIDDEN", message) { }
}
