namespace SiteHub.Domain.Common;

/// <summary>
/// Strongly-typed ID'ler için temel interface.
///
/// NEDEN STRONGLY-TYPED ID?
/// ────────────────────────
/// C#'ta Guid kullanmak tehlikelidir çünkü derleyici şunu ayırt etmez:
///
///   void TransferMoney(Guid fromUserId, Guid toUserId, Guid accountId)
///   // → İki parametreyi yanlışlıkla değiştirirseniz derleme hatası YOK
///
/// Strongly-typed ID'lerle:
///
///   void TransferMoney(UserId from, UserId to, AccountId account)
///   // → Yanlış tip geçerseniz DERLEME HATASI — bug prod'a gitmez
///
/// TYPED ID'NİN YAPMASI GEREKENLER:
/// - readonly record struct olmalı (value-type, immutable, equality built-in)
/// - New() factory metodu (Guid.CreateVersion7 — sıralı UUID, indexing için iyi)
/// - EF Core value converter ile otomatik Guid ↔ TypedId dönüşümü
/// </summary>
public interface ITypedId<TSelf>
{
    Guid Value { get; }
    static abstract TSelf FromGuid(Guid value);
}
