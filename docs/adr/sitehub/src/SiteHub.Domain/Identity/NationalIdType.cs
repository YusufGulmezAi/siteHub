namespace SiteHub.Domain.Identity;

/// <summary>
/// Ulusal kimlik numarası türleri.
///
/// - TCKN: Türkiye Cumhuriyeti Kimlik Numarası (11 hane, gerçek kişi)
/// - VKN:  Vergi Kimlik Numarası (10 hane, tüzel kişi)
/// - YKN:  Yabancı Kimlik Numarası (11 hane, 99 ile başlar)
/// </summary>
public enum NationalIdType
{
    TCKN = 1,
    VKN = 2,
    YKN = 3
}
