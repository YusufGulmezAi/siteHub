namespace SiteHub.Contracts.Common;

/// <summary>
/// Tüm API response'ları için standart zarf (Pattern A — ADR-0018 adayı).
/// Success senaryosunda Data dolu, Error null.
/// Failure senaryosunda Data null, Error dolu.
/// </summary>
public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public ApiError? Error { get; init; }

    /// <summary>Başarılı response oluşturur.</summary>
    public static ApiResponse<T> Ok(T data) => new()
    {
        Success = true,
        Data = data,
        Error = null
    };

    /// <summary>Başarısız response oluşturur.</summary>
    public static ApiResponse<T> Fail(ApiError error) => new()
    {
        Success = false,
        Data = default,
        Error = error
    };

    /// <summary>Kısayol: mesaj + kod ile başarısız response.</summary>
    public static ApiResponse<T> Fail(string code, string message) =>
        Fail(new ApiError { Code = code, Message = message });
}

/// <summary>
/// Data içermeyen operasyonlar için (void endpoint'lerde).
/// Örn: Delete, Update sonucunda sadece başarı bilgisi döner.
/// </summary>
public sealed class ApiResponse
{
    public bool Success { get; init; }
    public ApiError? Error { get; init; }

    public static ApiResponse Ok() => new() { Success = true };

    public static ApiResponse Fail(ApiError error) => new()
    {
        Success = false,
        Error = error
    };

    public static ApiResponse Fail(string code, string message) =>
        Fail(new ApiError { Code = code, Message = message });
}
