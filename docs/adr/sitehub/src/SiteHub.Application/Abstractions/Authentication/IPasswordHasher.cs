namespace SiteHub.Application.Abstractions.Authentication;

/// <summary>
/// Parola hash'leme ve doğrulama soyutlaması (ADR-0011 §1.2).
///
/// Implementation: ASP.NET Core Identity'nin <c>PasswordHasher&lt;T&gt;</c> (PBKDF2-HMAC-SHA512,
/// 100000 iteration default). Bu class Application katmanını ASP.NET Identity paketlerine
/// bağımlı yapmamak için soyutlar.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Plain text parolayı hash'ler. Salt otomatik üretilir, hash'in içinde saklanır.</summary>
    string Hash(string plainPassword);

    /// <summary>
    /// Verilen hash ile plain text parolayı karşılaştırır.
    /// </summary>
    /// <returns>
    /// Success: doğru, hash güncel format.
    /// SuccessRehashNeeded: doğru ama hash eski format — yeniden hash'lenmeli.
    /// Failed: yanlış parola.
    /// </returns>
    PasswordVerificationResult Verify(string hashedPassword, string plainPassword);
}

public enum PasswordVerificationResult
{
    Failed = 0,
    Success = 1,
    SuccessRehashNeeded = 2
}
