using SiteHub.Domain.Text;

namespace SiteHub.Domain.Common;

/// <summary>
/// Arama desteği olan aggregate'ler için temel sınıf.
///
/// AuditableAggregateRoot'a ek olarak: <see cref="SearchText"/> alanı tutar.
/// Bu alan normalize edilmiş (lowercase TR, trim, whitespace-collapsed) formdur.
///
/// DB'de ayrı bir kolon (search_text) olarak saklanır, deterministic collation
/// kullanır — böylece ILIKE ve pattern arama SORUNSUZ çalışır.
///
/// KULLANIM:
///   public sealed class Organization : SearchableAggregateRoot&lt;OrganizationId&gt;
///   {
///       public string Name { get; private set; }
///       public string CommercialTitle { get; private set; }
///       ...
///
///       public static Organization Create(string name, string title, ...)
///       {
///           var org = new Organization(...);
///           org.UpdateSearchText(name, title, ...);   // ← bunu unutma
///           return org;
///       }
///
///       public void Rename(string newName, string newTitle)
///       {
///           Name = newName;
///           CommercialTitle = newTitle;
///           UpdateSearchText(Name, CommercialTitle, ...);   // ← bunu da
///       }
///   }
///
/// ARAMA:
///   var q = TurkishNormalizer.Normalize(userInput);
///   var results = db.Organizations.Where(o => EF.Functions.ILike(o.SearchText, $"%{q}%"));
/// </summary>
public abstract class SearchableAggregateRoot<TId> : AuditableAggregateRoot<TId>
    where TId : struct
{
    /// <summary>Arama için normalize edilmiş metin. DB kolonu: search_text.</summary>
    public string SearchText { get; private set; } = string.Empty;

    protected SearchableAggregateRoot() : base() { }
    protected SearchableAggregateRoot(TId id) : base(id) { }

    /// <summary>
    /// Verilen alanları birleştirip SearchText'i yeniler.
    /// Entity'nin Create ve davranış (Rename, UpdateContact, vb.) metotlarında
    /// state değiştikten SONRA çağrılmalı.
    /// </summary>
    protected void UpdateSearchText(params string?[] fields)
    {
        SearchText = TurkishNormalizer.Combine(fields);
    }
}
