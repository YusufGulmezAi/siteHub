namespace SiteHub.Domain.Common;

/// <summary>
/// Domain Event: Domain'de gerçekleşen önemli bir olay.
///
/// Örnekler:
/// - AidatTahakkukEdildi (tahakkuk kesildiğinde)
/// - OdemeAlindi (tahsilat olduğunda)
/// - KullaniciOlusturuldu
///
/// Aggregate'lar bu event'leri `RaiseDomainEvent()` ile yayınlar.
/// Infrastructure katmanı DbContext.SaveChanges sırasında MediatR üzerinden
/// bunları publish eder.
///
/// Bu sayede modüller birbirine doğrudan bağlı olmadan olaylara tepki verebilir.
/// (Örn: Billing modülü "OdemeAlindi" event'i yayar → Accounting modülü dinler
/// ve yevmiye kaydı oluşturur.)
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredOn { get; }
}

/// <summary>
/// Domain Event için temel kayıt (record).
/// Alt sınıflar kendi ihtiyaçlarına göre ek property ekler.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.CreateVersion7();
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
