using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace SiteHub.Infrastructure.CodeGeneration;

/// <summary>
/// Feistel key'lerini IConfiguration'dan okur.
///
/// Konfigürasyon yolu: "CodeGeneration:FeistelKeys:{EntityName}" (Base64 string).
/// Örnek appsettings.Development.json:
/// <code>
/// "CodeGeneration": {
///   "FeistelKeys": {
///     "Organization": "base64-encoded-16-byte-key",
///     "Site": "...",
///     "Unit": "...",
///     "UnitPeriod": "..."
///   }
/// }
/// </code>
///
/// Development'ta otomatik key üretir (config'te yoksa) — her uygulama
/// restart'ında aynı key kalması için sabit seed kullanır. **PROD'DA
/// KULLANILAMAZ** — prod'da secret manager'dan okur.
/// </summary>
internal sealed class ConfigurationFeistelKeyProvider : IFeistelKeyProvider
{
    private readonly Dictionary<string, byte[]> _keyCache = new();
    private readonly IConfiguration _configuration;
    private readonly bool _isDevelopment;

    public ConfigurationFeistelKeyProvider(IConfiguration configuration)
    {
        _configuration = configuration;
        _isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development"
            || Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development";
    }

    public byte[] GetKeyFor(string entityTypeName)
    {
        if (_keyCache.TryGetValue(entityTypeName, out var cached))
            return cached;

        var configPath = $"CodeGeneration:FeistelKeys:{entityTypeName}";
        var base64Key = _configuration[configPath];

        byte[] key;
        if (!string.IsNullOrWhiteSpace(base64Key))
        {
            key = Convert.FromBase64String(base64Key);
            if (key.Length < 16)
                throw new InvalidOperationException(
                    $"Feistel key '{configPath}' için minimum 16 byte gerekli. Mevcut: {key.Length} byte.");
        }
        else if (_isDevelopment)
        {
            // Dev fallback: entity adından deterministik key türet.
            // PROD'DA ASLA BU DALA GİRMEMELİ — mutlaka config gelmeli.
            key = DeriveDevKey(entityTypeName);
        }
        else
        {
            throw new InvalidOperationException(
                $"Feistel key '{configPath}' konfigürasyonda bulunamadı. " +
                "Production ortamında secret manager'dan key sağlanmalı (ADR-0008).");
        }

        _keyCache[entityTypeName] = key;
        return key;
    }

    /// <summary>
    /// Development fallback: entity adından SHA-256 ile sabit 32-byte key türet.
    /// Her uygulama restart'ında aynı key — testler tutarlı.
    /// </summary>
    private static byte[] DeriveDevKey(string entityTypeName)
    {
        // Sabit salt — uygulama özelindeki dev-key'leri diğer sistemlerden ayırır
        const string salt = "sitehub-dev-feistel-key-v1";
        var input = $"{salt}:{entityTypeName}";
        return SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
    }
}
