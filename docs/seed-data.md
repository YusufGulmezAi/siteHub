# Seed Data Kataloğu

Bu doküman sistem başlatıldığında veritabanına yüklenecek referans verileri listeler.

## Kaynak Stratejisi

| Strateji | Açıklama | Örnek |
|---|---|---|
| **Kod içinde enum** | Hiç değişmez, az sayıda | Yön (8 yön + Bilinmiyor), ContextType |
| **Kod içinde seed** | Nadiren değişir, sistem geneli | Bölge, Site Tipi, BB Tipi, Belge Kategorisi |
| **CSV dosyasından seed** | Sabit ama hacimli | İller, İlçeler, Mahalleler, Posta Kodları, Bankalar |
| **Runtime eklenebilir** | Sistem Admin genişletir | Site Tipi, BB Tipi, Isınma Tipi, Sayaç Tipi vs. |

Eklenebilir olan veriler tablo olarak tutulur, ilk seed kod içinden gelir, sonra UI'dan eklenir.

---

## 1. Country (Ülke) - seed

| Field | Değer |
|---|---|
| Kapsam | 2 kayıt — "Türkiye" (varsayılan), "Türkiye Dışı" |
| Değişkenlik | Sabit (v2'de genişleyebilir — uluslararası müşteriler) |
| Kaynak | Kod içinde |

## 2. Region (Bölge) - seed

| Field | Değer |
|---|---|
| Kapsam | 8 kayıt — 7 coğrafi bölge + "Türkiye Dışı" |
| Kayıtlar | Marmara, Ege, Akdeniz, İç Anadolu, Karadeniz, Doğu Anadolu, Güneydoğu Anadolu, Türkiye Dışı |
| Değişkenlik | Sabit |
| Kaynak | Kod içinde |

## 3. Province (İl) - seed from CSV

| Field | Değer |
|---|---|
| Kapsam | 82 il (81 resmi + id=99 BİLİNMİYOR) |
| Kaynak | `/src/SiteHub.Infrastructure/Persistence/Seed/Data/turkey-addresses.csv` |
| Kolon eşleme | IL_ID, İL ADI |
| Bölge eşlemesi | Kod içinde (81 il için region mapping — DB'de ayrıca `RegionId` FK) |
| Değişkenlik | Nadir — yeni il kurulmuyor, BİLİNMİYOR sistem-only |

### İl-Bölge Eşleme Tablosu

**Marmara:** 10-Balıkesir, 11-Bilecik, 16-Bursa, 17-Çanakkale, 22-Edirne, 34-İstanbul, 39-Kırklareli, 41-Kocaeli, 54-Sakarya, 59-Tekirdağ, 77-Yalova

**Ege:** 03-Afyonkarahisar, 09-Aydın, 20-Denizli, 35-İzmir, 43-Kütahya, 45-Manisa, 48-Muğla, 64-Uşak

**Akdeniz:** 01-Adana, 07-Antalya, 15-Burdur, 31-Hatay, 32-Isparta, 33-Mersin (İçel), 46-Kahramanmaraş, 80-Osmaniye

**İç Anadolu:** 06-Ankara, 18-Çankırı, 26-Eskişehir, 38-Kayseri, 40-Kırşehir, 42-Konya, 50-Nevşehir, 51-Niğde, 58-Sivas, 66-Yozgat, 68-Aksaray, 70-Karaman, 71-Kırıkkale

**Karadeniz:** 05-Amasya, 08-Artvin, 14-Bolu, 19-Çorum, 28-Giresun, 29-Gümüşhane, 37-Kastamonu, 52-Ordu, 53-Rize, 55-Samsun, 57-Sinop, 60-Tokat, 61-Trabzon, 67-Zonguldak, 69-Bayburt, 74-Bartın, 78-Karabük, 81-Düzce

**Doğu Anadolu:** 04-Ağrı, 12-Bingöl, 13-Bitlis, 23-Elazığ, 24-Erzincan, 25-Erzurum, 30-Hakkari, 36-Kars, 44-Malatya, 49-Muş, 62-Tunceli, 65-Van, 75-Ardahan, 76-Iğdır

**Güneydoğu Anadolu:** 02-Adıyaman, 21-Diyarbakır, 27-Gaziantep, 47-Mardin, 56-Siirt, 63-Şanlıurfa, 72-Batman, 73-Şırnak, 79-Kilis

**Türkiye Dışı:** 99-BİLİNMİYOR

## 4. District (İlçe) - seed from CSV

| Field | Değer |
|---|---|
| Kapsam | 958 ilçe (957 + id=999 BİLİNMİYOR) |
| Kaynak | `turkey-addresses.csv` |
| Kolon eşleme | IL_ID, ILCE_ID, İLÇE ADI |
| Değişkenlik | Nadir — yeni ilçe kurulması nadirdir |

## 5. Neighborhood (Mahalle/Semt) - seed from CSV

| Field | Değer |
|---|---|
| Kapsam | 4125 mahalle/semt |
| Kaynak | `turkey-addresses.csv` |
| Kolon eşleme | ILCE_ID, SEMT_ID, SEMT_ADI_BUYUK, POSTA_KODU |
| Değişkenlik | Yılda bir güncelleme (kullanıcı isterse yönetici panelinden yeniden seed) |
| Özellik | Posta kodu 100 satırda null olabilir (köyler ve BİLİNMİYOR) |

## 6. Bank (Banka) - seed from CSV

| Field | Değer |
|---|---|
| Kapsam | ~50 banka (mevduat + katılım) |
| Kaynak | Kullanıcıdan gelecek CSV (Banka Tam Adı, Kısa Adı, EFT Kodu, SWIFT, Aktif/Pasif) |
| Format | `"Türkiye Cumhuriyeti Ziraat Bankası A.Ş.","Ziraat Bankası","0010","TCZBTR2A",1` |
| Referans | BDDK Kuruluş Listesi (bddk.org.tr/Kurulus/Liste/77) + TBB + SWIFT |
| Değişkenlik | Banka birleşmesi/kapanmasında güncelleme |
| Alanlar | FullName, ShortName, EftCode (4 hane), SwiftCode (8 veya 11 hane), IsActive |

## 7. SiteType (Site Tipi) - seed, runtime extensible

| Field | Değer |
|---|---|
| Kapsam | 9+ tip |
| Kayıtlar | Apartman, Konut Sitesi, Villa Sitesi, Karma (Konut+İşyeri), Karma (Konut+Ofis), AVM, Plaza, İş Merkezi, Ofis Binası |
| Yönetim | Sistem Admin ekler (sistem geneli, tüm organizasyonlarda kullanılır) |
| Kaynak | İlk seed kod içinden, sonra `site_types` tablosunda |

## 8. BuildingType (Yapı Tipi) - seed, runtime extensible

| Field | Değer |
|---|---|
| Kapsam | 7+ tip |
| Kayıtlar | Konut, Konut+Dükkan, Konut+İşyeri, Konut+Ofis, İş Yeri, Depo Binası, Karma |
| Yönetim | Sistem Admin |
| Kaynak | Kod seed, DB tablo |

## 9. UnitType (Bağımsız Bölüm Tipi) - seed, runtime extensible

| Field | Değer |
|---|---|
| Kapsam | 9+ tip |
| Kayıtlar | Daire, Dükkan, Ofis, İş Yeri, Depo, Görevli Dairesi, Otopark, Villa, Ortak Alan |
| Yönetim | Sistem Admin |
| Kaynak | Kod seed, DB tablo |

## 10. HeatingType (Isınma Tipi) - seed, runtime extensible

| Field | Değer |
|---|---|
| Kapsam | 9+ tip |
| Kayıtlar | Bireysel Isınma, Doğalgaz Kazanlı, Fosil Yakıtlı Kazanlı, Doğalgazlı Kombili, Elektrikli, Kömürlü, Jeotermal, Güneş Enerjisi, Yok |
| Yönetim | Sistem Admin |
| Kaynak | Kod seed, DB tablo |

## 11. Direction (Yön) - enum

| Field | Değer |
|---|---|
| Kapsam | 9 değer |
| Kayıtlar | Kuzey, Kuzeydoğu, Doğu, Güneydoğu, Güney, Güneybatı, Batı, Kuzeybatı, Bilinmiyor |
| Değişkenlik | Sabit, enum |

## 12. MeterType (Sayaç Tipi) - seed, runtime extensible

| Field | Değer |
|---|---|
| Kapsam | 6+ tip |
| Kayıtlar | Elektrik, Su, Doğalgaz, Isı Pay Ölçer (Kalorimetre), Ortak Elektrik, Ortak Su |
| Yönetim | Sistem Admin |

## 13. PaymentMethod (Ödeme Yöntemi) - seed

| Field | Değer |
|---|---|
| Kapsam | 8 yöntem |
| Kayıtlar | Havale, EFT, Sanal POS (Kredi Kartı), Nakit, Çek, Senet, Kurumsal Tahsilat (banka dosya import), Otomatik Ödeme Talimatı |
| Değişkenlik | Sabit |

## 14. DocumentCategory (Belge Kategorisi) - seed, runtime extensible (Sistem Admin seviyesinde)

| Field | Değer |
|---|---|
| Kapsam | 18+ kategori |
| Yönetim | Sistem Admin (sistem geneli) |
| Scope | System-wide (organizasyonlar ekleyemez, sadece sistem personeli) |
| Ek kolonlar | IsPermanent (bool), IsSensitive (bool) |

**Seed kategorileri:**

| Kategori | IsPermanent | IsSensitive | Sahip tipler |
|---|---|---|---|
| Yönetim Kararları | ✗ | ✗ | Site |
| Bütçe Tebligatları | ✗ | ✗ | Site |
| Sözleşmeler | ✗ | ✓ | Site, Organization, ServiceContract |
| Vekaletname & İmza Sirküleri | ✗ | ✓ | Person |
| Kimlik Kopyaları | ✗ | ✓ | Person |
| Mahkeme Kararları | ✗ | ✓ | Site, Unit, Person |
| Dilekçeler | ✗ | ✗ | Unit, Person |
| Tapu Kopyaları (güncel) | ✓ | ✓ | Unit |
| Rölöve / Plan | ✓ | ✗ | Unit |
| İskan / Ruhsat | ✓ | ✗ | Unit, Building |
| Kira Kontratları | ✗ | ✓ | UnitPeriod (Tenant) |
| Faturalar | ✗ | ✗ | UnitPeriod, Site |
| Hesap Ekstreleri | ✗ | ✓ | UnitPeriod, BankCustomerProfile |
| Tebligatlar | ✗ | ✗ | Unit, UnitPeriod |
| Fotoğraflar | ✗ | ✗ | Site, Unit |
| Paylaşım Pusulaları | ✗ | ✗ | Unit, UnitPeriod |
| Servis Firması Teslim Paketi (ZIP) | ✗ | ✗ | Site |
| Diğer | ✗ | ✗ | * |

**IsPermanent:** Belge döneme bağlı değil — rölöve, ruhsat gibi kalıcı BB belgeleri. Tüm geçmiş/yeni malik + kiracılar görebilir.

**IsSensitive:** Hassas veri. MVP'de özel audit kaydı (SensitiveDocumentAccessed), v2'de application-level encryption + justification.

## 15. AccountType (Banka Hesap Tipi) - seed, runtime extensible

| Field | Değer |
|---|---|
| Kapsam | 11 kayıt (başlangıç) + SystemAdmin eklenebilir |
| Kaynak | Kod seed, DB tablo |
| Yönetim | SystemAdmin (banka dünyasında sürekli yeni ürün çıkar) |

**İlk seed:**

| Key | DisplayName |
|---|---|
| `demand_deposit` | Vadesiz Mevduat |
| `time_deposit` | Vadeli Mevduat |
| `pos_normal` | POS (Normal Tahsilat) |
| `pos_blocked` | POS Bloke |
| `corporate_collection_normal` | Kurumsal Tahsilat (Normal) |
| `corporate_collection_blocked` | Kurumsal Tahsilat Bloke |
| `credit_guarantee_blocked` | Kredi Teminat Bloke |
| `foreign_exchange_deposit` | Döviz Tevdiat (DTH) |
| `investment_fund` | Yatırım Fonu Hesabı |
| `precious_metal` | Altın/Değerli Maden |
| `automatic_debit` | Otomatik Borçlandırma Hesabı (OBH) |
| `other` | Diğer |

## 15. Currency (Para Birimi) - seed

| Field | Değer |
|---|---|
| Kapsam | TRY (varsayılan) + dünya para birimleri |
| Kayıtlar | TRY, USD, EUR, GBP, CHF, JPY, SAR, AED |
| ISO standartı | ISO 4217 (3 harfli kod) |
| Yönetim | Sistem Admin |
| Default | TRY — organizasyonlar için varsayılan |

## 16. RevenueCategory (Gelir Kategorisi) - seed, runtime extensible (Organizasyon seviyesinde)

| Field | Değer |
|---|---|
| Kapsam | Sistem seed + organizasyon özel |
| Sistem seed | Aidat, Isınma, Kömür, Kuruluş Avans, Yatırım Avans, Gecikme Cezası, Faiz, Kira, Kapı Geç |
| Organizasyon özel | Her organizasyon kendi özel kategorilerini ekler (global görmez başkalarının) |

## 17. Role (Rol) - enum

ADR-0011'de tanımlı (ContextType × Role enum).

## 18. Permission (İzin) - kod içinde

ADR-0011'de tanımlı. Kod içinde sabit liste, DB'ye yazılmaz.

---

## Seed Implementation Notları

### Yükleme sırası (dependency order):

1. Country
2. Region (Country FK)
3. Province (Region FK) — CSV'den
4. District (Province FK) — CSV'den
5. Neighborhood (District FK) — CSV'den
6. Currency
7. Bank — CSV'den (kullanıcıdan)
8. SiteType, BuildingType, UnitType, HeatingType, MeterType, PaymentMethod, DocumentCategory, RevenueCategory
9. Permission (hiçbir tabloya yazılmaz, kod içinde enum)

### Çalıştırma:

- `dotnet ef database update` çağrısından sonra **otomatik** — migration runner'da seed stage çalışır
- İdempotent — aynı seed çalışsa bile duplicate yaratmaz (HasData ile EF Core native seed API'si)
- Bazıları "runtime extensible": seed tablolar boş değilse eklenmez, varsa güncellenmez (kullanıcının özelleştirmesine saygı)

### Güncelleme:

- Yılda bir kez (idari birim değişiklikleri için): admin panelden **"Adres verisi yeniden yükle"** butonu
- Yeni CSV yüklenir, sistem değişiklikleri uygular (yeni mahalleler eklenir, kaldırılanlar "DeprecatedAt" set edilir — silinmez çünkü tarihçe adreslerde referans var)

### CSV konumu:

```
src/SiteHub.Infrastructure/Persistence/Seed/Data/
  ├── turkey-addresses.csv   (İl-İlçe-Mahalle-Posta Kodu — YÜKLENDİ)
  └── banks.csv              (senden gelecek)
```
