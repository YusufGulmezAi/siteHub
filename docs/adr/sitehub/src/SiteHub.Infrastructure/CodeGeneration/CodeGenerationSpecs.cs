namespace SiteHub.Infrastructure.CodeGeneration;

/// <summary>
/// Bir entity tipi için kod üretim spesifikasyonu (ADR-0012 §11.6).
/// Sequence adı, hedef aralık ve Feistel parametreleri.
/// </summary>
internal sealed record CodeGenerationSpec(
    string SequenceName,
    long MinValue,
    long MaxValue,
    int FeistelBits)
{
    public long SlotCount => MaxValue - MinValue + 1;
}

/// <summary>
/// Tüm entity tipleri için kod üretim spesifikasyonları.
/// Type → Spec eşlemesi.
/// </summary>
internal static class CodeGenerationSpecs
{
    // Entity tip adları — kod içinde typeof().Name ile eşleşir
    public const string Organization = "Organization";
    public const string Site = "Site";
    public const string Unit = "Unit";
    public const string UnitPeriod = "UnitPeriod";

    private static readonly Dictionary<string, CodeGenerationSpec> _specs = new()
    {
        // 6 hane: 100001-999999 (900K slot), 20-bit Feistel (2^20 = 1,048,576)
        [Organization] = new CodeGenerationSpec(
            SequenceName: "seq_organization_code",
            MinValue: 100_001,
            MaxValue: 999_999,
            FeistelBits: 20),

        // 6 hane: 100001-999999 (900K slot), 20-bit Feistel
        [Site] = new CodeGenerationSpec(
            SequenceName: "seq_site_code",
            MinValue: 100_001,
            MaxValue: 999_999,
            FeistelBits: 20),

        // 7 hane: 1000001-9999999 (9M slot), 24-bit Feistel (2^24 = 16,777,216)
        [Unit] = new CodeGenerationSpec(
            SequenceName: "seq_unit_code",
            MinValue: 1_000_001,
            MaxValue: 9_999_999,
            FeistelBits: 24),

        // 9 hane: 111111111-999999999 (~889M slot), 30-bit Feistel (2^30 = 1,073,741,824)
        [UnitPeriod] = new CodeGenerationSpec(
            SequenceName: "seq_unit_period_code",
            MinValue: 111_111_111,
            MaxValue: 999_999_999,
            FeistelBits: 30)
    };

    public static CodeGenerationSpec GetFor(string entityTypeName)
    {
        if (!_specs.TryGetValue(entityTypeName, out var spec))
        {
            throw new ArgumentException(
                $"'{entityTypeName}' tipi için kod üretim spesifikasyonu tanımlanmamış. " +
                $"Desteklenen tipler: {string.Join(", ", _specs.Keys)}",
                nameof(entityTypeName));
        }
        return spec;
    }

    public static IReadOnlyCollection<string> SupportedTypes => _specs.Keys;
}
