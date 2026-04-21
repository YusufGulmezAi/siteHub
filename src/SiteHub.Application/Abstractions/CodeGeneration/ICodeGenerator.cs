namespace SiteHub.Application.Abstractions.CodeGeneration;

/// <summary>
/// Entity kodlarını üreten servis (Organization, Site, Unit, UnitPeriod).
///
/// Implementation: Infrastructure'daki FeistelCodeGenerator.
/// Strateji: PostgreSQL sequence + Feistel cipher obfuscation + cycle walking
/// aralık normalizasyonu (ADR-0012 §11).
///
/// Garantiler:
/// - All-time unique (sequence monoton artar, Feistel bijection)
/// - Tahmin edilemez (Feistel key uygulamaya özel)
/// - Retry/DB check gerekmez (matematiksel garanti)
/// - Deterministik (aynı sequence → aynı kod, test edilebilir)
/// </summary>
public interface ICodeGenerator
{
    /// <summary>
    /// Belirtilen entity için bir sonraki kodu üretir.
    /// Type parameter, hangi sequence ve Feistel key'in kullanılacağını belirler.
    /// Desteklenen entity tipleri: Organization, Site, Unit, UnitPeriod.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Desteklenmeyen entity tipi için atılır.
    /// </exception>
    Task<long> GenerateAsync<T>(CancellationToken ct = default) where T : class;
}
