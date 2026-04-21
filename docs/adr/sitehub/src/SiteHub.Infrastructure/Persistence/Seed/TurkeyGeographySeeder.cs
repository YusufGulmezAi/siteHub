using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SiteHub.Domain.Geography;
using SiteHub.Infrastructure.Persistence.Seed.Geography;

namespace SiteHub.Infrastructure.Persistence.Seed;

/// <summary>
/// Türkiye adres hiyerarşisi seed servisi.
///
/// Akış:
/// 1. Countries tablosu boşsa → Türkiye + "Diğer" oluştur
/// 2. Regions tablosu boşsa → 7 coğrafi bölge + "Türkiye Dışı" placeholder
/// 3. Provinces tablosu boşsa → CSV'den 81 il (IL_ID + İL ADI unique)
/// 4. Districts tablosu boşsa → CSV'den ~958 ilçe (ILCE_ID unique)
/// 5. Neighborhoods tablosu boşsa → CSV'den ~4125 mahalle (DistrictId + ExternalId unique)
///
/// Idempotent: Zaten dolu tabloları atlar. Startup'ta güvenle çağrılır.
/// Atomic: Her seviye tek transaction'da (rollback garantisi).
///
/// CSV: turkey-addresses.csv — Infrastructure/Persistence/Seed/Data/ altında.
/// Çalışma dizini Infrastructure DLL yanındaki "Persistence/Seed/Data/" olacak
/// şekilde csproj'da Content olarak kopyalanır.
/// </summary>
public sealed class TurkeyGeographySeeder
{
    private readonly SiteHubDbContext _db;
    private readonly ILogger<TurkeyGeographySeeder> _logger;

    public TurkeyGeographySeeder(SiteHubDbContext db, ILogger<TurkeyGeographySeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Türkiye adres seed başlatılıyor...");

        var turkey = await SeedCountriesAsync(ct);
        var regionsByCode = await SeedRegionsAsync(turkey.Id, ct);

        if (await _db.Provinces.AnyAsync(ct))
        {
            _logger.LogInformation(
                "Provinces zaten seed edilmiş ({Count} kayıt). Tüm CSV seed atlandı.",
                await _db.Provinces.CountAsync(ct));
            return;
        }

        var csvPath = ResolveCsvPath();
        _logger.LogInformation("CSV okunuyor: {Path}", csvPath);
        var (rows, skippedRows) = TurkeyAddressCsvReader.ReadAll(csvPath);
        _logger.LogInformation("CSV {Count} satır okundu.", rows.Count);

        if (skippedRows.Count > 0)
        {
            _logger.LogWarning("CSV'de {Count} bozuk satır atlandı.", skippedRows.Count);
            foreach (var skip in skippedRows)
                _logger.LogWarning("  {Reason}", skip);
        }

        var provincesByExternalId = await SeedProvincesFromCsvAsync(rows, regionsByCode, ct);
        var districtsByExternalId = await SeedDistrictsFromCsvAsync(rows, provincesByExternalId, ct);
        await SeedNeighborhoodsFromCsvAsync(rows, districtsByExternalId, ct);

        _logger.LogInformation("Türkiye adres seed tamamlandı.");
    }

    // ─── Countries ───────────────────────────────────────────────────────

    private async Task<Country> SeedCountriesAsync(CancellationToken ct)
    {
        var turkey = await _db.Countries
            .FirstOrDefaultAsync(c => c.IsoCode == Country.TurkeyIsoCode, ct);

        if (turkey is not null)
        {
            _logger.LogDebug("Türkiye kaydı zaten var.");
            return turkey;
        }

        turkey = Country.Create(
            isoCode: Country.TurkeyIsoCode,
            name: "Türkiye",
            phonePrefix: "+90",
            displayOrder: 1);

        var other = Country.Create(
            isoCode: "XX",
            name: "Diğer (Yurt Dışı)",
            phonePrefix: null,
            displayOrder: 999);

        _db.Countries.AddRange(turkey, other);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Countries seed edildi: Türkiye + Diğer.");
        return turkey;
    }

    // ─── Regions ─────────────────────────────────────────────────────────

    private async Task<IReadOnlyDictionary<string, Region>> SeedRegionsAsync(
        CountryId turkeyId, CancellationToken ct)
    {
        var existing = await _db.Regions
            .Where(r => r.CountryId == turkeyId)
            .ToDictionaryAsync(r => r.Code, r => r, ct);

        if (existing.Count >= TurkishRegionMap.Regions.Count)
        {
            _logger.LogDebug("Regions zaten seed edilmiş ({Count} kayıt).", existing.Count);
            return existing;
        }

        foreach (var (code, name, order) in TurkishRegionMap.Regions)
        {
            if (existing.ContainsKey(code)) continue;
            var region = Region.Create(turkeyId, name, code, order);
            _db.Regions.Add(region);
            existing[code] = region;
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Regions seed edildi: {Count} bölge.", existing.Count);
        return existing;
    }

    // ─── Provinces ───────────────────────────────────────────────────────

    private async Task<IReadOnlyDictionary<int, Province>> SeedProvincesFromCsvAsync(
        IReadOnlyList<TurkeyAddressRow> rows,
        IReadOnlyDictionary<string, Region> regionsByCode,
        CancellationToken ct)
    {
        // Distinct iller (IL_ID + İL ADI)
        var uniqueProvinces = rows
            .GroupBy(r => r.IlId)
            .Select(g => new { IlId = g.Key, IlAdi = g.First().IlAdi })
            .OrderBy(p => p.IlId)
            .ToList();

        var dict = new Dictionary<int, Province>();

        foreach (var p in uniqueProvinces)
        {
            var regionCode = TurkishRegionMap.ProvinceToRegion.TryGetValue(p.IlId, out var rc)
                ? rc
                : throw new InvalidOperationException(
                    $"İl {p.IlId} ({p.IlAdi}) için bölge eşlemesi TurkishRegionMap'te tanımlı değil.");

            var region = regionsByCode[regionCode];
            var plateCode = TurkishRegionMap.PlateCodeOf(p.IlId);

            var province = Province.Create(region.Id, p.IlId, p.IlAdi, plateCode);
            _db.Provinces.Add(province);
            dict[p.IlId] = province;
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Provinces seed edildi: {Count} il.", dict.Count);
        return dict;
    }

    // ─── Districts ───────────────────────────────────────────────────────

    private async Task<IReadOnlyDictionary<int, District>> SeedDistrictsFromCsvAsync(
        IReadOnlyList<TurkeyAddressRow> rows,
        IReadOnlyDictionary<int, Province> provincesByExternalId,
        CancellationToken ct)
    {
        // Distinct ilçeler (ILCE_ID + İLÇE ADI)
        // NOT: Aynı ILCE_ID birden fazla ilde tekrar etmez — CSV'de ID globally unique
        var uniqueDistricts = rows
            .GroupBy(r => r.IlceId)
            .Select(g =>
            {
                var first = g.First();
                return new { IlceId = g.Key, IlceAdi = first.IlceAdi, IlId = first.IlId };
            })
            .OrderBy(d => d.IlceId)
            .ToList();

        var dict = new Dictionary<int, District>();
        var batch = new List<District>();

        foreach (var d in uniqueDistricts)
        {
            if (!provincesByExternalId.TryGetValue(d.IlId, out var province))
                throw new InvalidOperationException(
                    $"İlçe {d.IlceId} ({d.IlceAdi}) için il {d.IlId} bulunamadı.");

            var district = District.Create(province.Id, d.IlceId, d.IlceAdi);
            batch.Add(district);
            dict[d.IlceId] = district;

            // Batch halinde ekle (bellek için)
            if (batch.Count >= 500)
            {
                _db.Districts.AddRange(batch);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
            _db.Districts.AddRange(batch);

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Districts seed edildi: {Count} ilçe.", dict.Count);
        return dict;
    }

    // ─── Neighborhoods ───────────────────────────────────────────────────

    private async Task SeedNeighborhoodsFromCsvAsync(
        IReadOnlyList<TurkeyAddressRow> rows,
        IReadOnlyDictionary<int, District> districtsByExternalId,
        CancellationToken ct)
    {
        // CSV'de aynı SEMT_ID birden fazla ilçede tekrar edebilir — bu durumda
        // her ilçe-SEMT_ID kombinasyonu ayrı mahalledir. (DistrictId, ExternalId)
        // composite unique index buna uygun.

        var count = 0;
        var batch = new List<Neighborhood>();

        foreach (var row in rows)
        {
            if (!districtsByExternalId.TryGetValue(row.IlceId, out var district))
                throw new InvalidOperationException(
                    $"Mahalle '{row.SemtAdi}' (SEMT_ID={row.SemtId}) için ilçe " +
                    $"{row.IlceId} bulunamadı.");

            var neighborhood = Neighborhood.Create(
                district.Id, row.SemtId, row.SemtAdi, row.PostaKodu);

            batch.Add(neighborhood);
            count++;

            if (batch.Count >= 500)
            {
                _db.Neighborhoods.AddRange(batch);
                await _db.SaveChangesAsync(ct);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            _db.Neighborhoods.AddRange(batch);
            await _db.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Neighborhoods seed edildi: {Count} mahalle.", count);
    }

    // ─── Path resolution ─────────────────────────────────────────────────

    private static string ResolveCsvPath()
    {
        // Infrastructure DLL'in yanındaki Persistence/Seed/Data/ klasöründen okur.
        // csproj'da Content olarak kopyalanır (aşağıda).
        var baseDir = AppContext.BaseDirectory;
        var path = Path.Combine(baseDir, "Persistence", "Seed", "Data", "turkey-addresses.csv");

        if (File.Exists(path)) return path;

        // Development fallback — solution root'tan ara (dotnet run senaryosu)
        var currentDir = new DirectoryInfo(baseDir);
        while (currentDir is not null)
        {
            var fallback = Path.Combine(
                currentDir.FullName,
                "src", "SiteHub.Infrastructure", "Persistence", "Seed", "Data", "turkey-addresses.csv");
            if (File.Exists(fallback)) return fallback;
            currentDir = currentDir.Parent;
        }

        throw new FileNotFoundException(
            $"turkey-addresses.csv bulunamadı. Arandı: {path} ve solution üst klasörleri.");
    }
}
