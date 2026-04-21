namespace SiteHub.Contracts.Common;

/// <summary>
/// API hata yapısı. Error codes sistem geneli standartlaştırılır.
/// Field-bazlı validation hataları için Errors dictionary kullanılır.
/// </summary>
public sealed class ApiError
{
    /// <summary>
    /// Makine-okunabilir hata kodu. Örn: "VALIDATION_FAILED", "NOT_FOUND", "UNAUTHORIZED".
    /// UI tarafında bu koda göre özel işlemler yapılabilir.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Kullanıcıya gösterilecek Türkçe açıklama.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Field-bazlı validation hataları. Key: field adı, Value: hata listesi.
    /// Örn: { "email": ["Geçerli e-posta değil"], "name": ["Zorunlu alan"] }
    /// </summary>
    public Dictionary<string, string[]>? Errors { get; init; }

    /// <summary>
    /// Debug için izleme ID'si. Log'larda aynı ID ile arama yapılabilir.
    /// </summary>
    public string? TraceId { get; init; }
}

/// <summary>
/// Standart hata kodları — sistem genelinde tutarlı kullanım için.
/// UI bu kodlara göre özel davranış sergileyebilir (redirect, dialog vb.).
/// </summary>
public static class ApiErrorCodes
{
    // Genel
    public const string InternalError = "INTERNAL_ERROR";
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";

    // Kimlik/Yetki
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string SessionExpired = "SESSION_EXPIRED";
    public const string AccountLocked = "ACCOUNT_LOCKED";
    public const string AccountInactive = "ACCOUNT_INACTIVE";
    public const string IpNotAllowed = "IP_NOT_ALLOWED";
    public const string ScheduleBlocked = "SCHEDULE_BLOCKED";

    // Domain
    public const string InvalidState = "INVALID_STATE";
    public const string BusinessRuleViolation = "BUSINESS_RULE_VIOLATION";
    public const string DuplicateCode = "DUPLICATE_CODE";

    // Onay zinciri
    public const string ApprovalRequired = "APPROVAL_REQUIRED";
    public const string ApprovalAlreadySubmitted = "APPROVAL_ALREADY_SUBMITTED";
    public const string NotAuthorizedToApprove = "NOT_AUTHORIZED_TO_APPROVE";
}
