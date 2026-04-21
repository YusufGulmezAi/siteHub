using Microsoft.EntityFrameworkCore;
using SiteHub.Application.Abstractions.CodeGeneration;
using SiteHub.Infrastructure.Persistence;

namespace SiteHub.Infrastructure.CodeGeneration;

/// <summary>
/// ICodeGenerator implementasyonu — PostgreSQL sequence + Feistel cipher
/// (ADR-0012 §11).
///
/// Akış:
/// 1. Entity tipine göre CodeGenerationSpec bulunur (sequence adı, aralık, bits)
/// 2. PostgreSQL'den NEXTVAL(sequence_name) alınır (atomic, concurrent-safe)
/// 3. Feistel key alınır (IFeistelKeyProvider)
/// 4. FeistelCipher.EncryptToRange çağrılır (cycle walking dahil)
/// 5. Sonuç: normalized code [min, max] aralığında, all-time unique
///
/// Garantiler:
/// - Çakışma YOK (sequence monoton + Feistel bijection)
/// - Retry gerekmez
/// - DB check gerekmez
/// - O(1) — tek SQL round-trip, sabit CPU maliyeti
/// </summary>
internal sealed class FeistelCodeGenerator : ICodeGenerator
{
    private readonly SiteHubDbContext _db;
    private readonly IFeistelKeyProvider _keyProvider;

    public FeistelCodeGenerator(SiteHubDbContext db, IFeistelKeyProvider keyProvider)
    {
        _db = db;
        _keyProvider = keyProvider;
    }

    public async Task<long> GenerateAsync<T>(CancellationToken ct = default) where T : class
    {
        var entityTypeName = typeof(T).Name;
        var spec = CodeGenerationSpecs.GetFor(entityTypeName);

        // Sequence'dan next alınır — PostgreSQL atomic
        var sequenceValue = await GetNextSequenceValueAsync(spec.SequenceName, ct);

        // 0-based slot index'e çevrilir ([1..N] → [0..N-1])
        var slotIndex = sequenceValue - 1;

        // Feistel obfuscation + cycle walking + aralığa map
        var key = _keyProvider.GetKeyFor(entityTypeName);
        var code = FeistelCipher.EncryptToRange(
            input: slotIndex,
            slotCount: spec.SlotCount,
            bits: spec.FeistelBits,
            key: key,
            minValue: spec.MinValue);

        return code;
    }

    /// <summary>
    /// PostgreSQL sequence'ından bir sonraki değeri alır.
    /// NEXTVAL atomic — concurrent çağrılar çakışmaz.
    /// </summary>
    private async Task<long> GetNextSequenceValueAsync(string sequenceName, CancellationToken ct)
    {
        // Sequence adı CodeGenerationSpecs'de kod-sabit (hardcoded), kullanıcı
        // girdisi değil — SQL injection mümkün değil.
        var sql = $"SELECT nextval('{sequenceName}')";
        var result = await _db.Database
            .SqlQueryRaw<long>(sql)
            .ToListAsync(ct);

        if (result.Count == 0)
            throw new InvalidOperationException(
                $"Sequence '{sequenceName}' nextval döndürmedi — sequence tanımsız olabilir.");

        return result[0];
    }
}
