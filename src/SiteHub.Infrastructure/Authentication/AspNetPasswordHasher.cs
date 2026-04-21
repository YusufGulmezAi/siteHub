using Microsoft.AspNetCore.Identity;
using SiteHub.Application.Abstractions.Authentication;
using SiteHub.Domain.Identity;
using AspNetResult = Microsoft.AspNetCore.Identity.PasswordVerificationResult;
using AppResult = SiteHub.Application.Abstractions.Authentication.PasswordVerificationResult;

namespace SiteHub.Infrastructure.Authentication;

/// <summary>
/// ASP.NET Core Identity'nin <c>PasswordHasher&lt;T&gt;</c> wrapper'ı.
///
/// <para>Default format (v3): PBKDF2 + HMAC-SHA512, 100000 iteration, 128-bit salt, 256-bit subkey.</para>
///
/// <para>Hash string yapısı (base64):</para>
/// <code>{format}{prf}{iteration}{saltSize}{salt}{subkey}</code>
///
/// <para>v2 (v1 ve v1'deki daha eski algoritma) hash'leri de verify edebilir — eski sistemden
/// import durumunda migration mümkün. Verify sonucu SuccessRehashNeeded dönerse handler
/// otomatik rehash yapar.</para>
/// </summary>
public sealed class AspNetPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<Person> _inner = new();

    public string Hash(string plainPassword)
    {
        if (string.IsNullOrEmpty(plainPassword))
            throw new ArgumentException("Parola boş olamaz.", nameof(plainPassword));

        // Person parametresi PasswordHasher<T> için placeholder — PBKDF2 user-agnostic
        return _inner.HashPassword(null!, plainPassword);
    }

    public AppResult Verify(string hashedPassword, string plainPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword) || string.IsNullOrEmpty(plainPassword))
            return AppResult.Failed;

        var result = _inner.VerifyHashedPassword(null!, hashedPassword, plainPassword);
        return result switch
        {
            AspNetResult.Success => AppResult.Success,
            AspNetResult.SuccessRehashNeeded => AppResult.SuccessRehashNeeded,
            _ => AppResult.Failed
        };
    }
}
