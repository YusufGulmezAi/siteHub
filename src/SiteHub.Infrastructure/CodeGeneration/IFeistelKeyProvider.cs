namespace SiteHub.Infrastructure.CodeGeneration;

/// <summary>
/// Entity tipine göre Feistel key sağlayıcı.
///
/// Dev: appsettings.json veya .env dosyasından okur.
/// Prod: Azure Key Vault / AWS Secrets Manager (ADR-0008).
///
/// Kritik kısıt: Key'ler uygulamanın ömrü boyunca DEĞİŞMEZ. Değişirse eski
/// kodlar "başka bir hikaye" anlatır (matematik olarak geçerli ama
/// uygulamaya karışıklık yaratır — §11.12).
/// </summary>
internal interface IFeistelKeyProvider
{
    /// <summary>
    /// Belirli bir entity tipi için Feistel key (16+ byte).
    /// </summary>
    byte[] GetKeyFor(string entityTypeName);
}
