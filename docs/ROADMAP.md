# SiteHub — Ürün Yol Haritası

Bu dosya SiteHub yönetim portalının **planlanan** ekran ve modüllerini tutar.
Her modülün gerçekleştiği faz yanında belirtilir. Sol menüye **sadece çalışan**
modüller eklenir — bu dosya bu yüzden ayrı tutulur.

> Not: Menüde neyin göründüğünü görmek için
> [`Components/Navigation/MenuTree.cs`](../src/SiteHub.ManagementPortal/Components/Navigation/MenuTree.cs) dosyasına bakın.

---

## Durum Gösterimi

- ✅ **Tamamlandı** — Menüde, çalışıyor
- 🚧 **Geliştiriliyor** — Aktif faz
- 📋 **Planlandı** — Sıradaki fazlardan birinde
- ⏳ **v2** — MVP sonrası

---

## Kimlik / Erişim Modülü

| Ekran | Yol | Durum | Faz |
|---|---|---|---|
| Giriş | `/login` | ✅ | C |
| Şifremi Unuttum | `/forgot-password` | ✅ | D1 |
| Şifre Sıfırla | `/reset-password` | ✅ | D1 |
| 2FA Doğrulama | `/verify-2fa` | ✅ | D2 |
| 2FA Ayarları | `/account/security/2fa` | ✅ | D2 |
| Kullanıcı profili | `/account/profile` | 📋 | F |

## Organizasyon (Kiracı) Modülü

| Ekran | Yol | Durum | Faz |
|---|---|---|---|
| Firma listesi | `/firms` | 📋 | E |
| Firma detay/düzenle | `/firms/{id}` | 📋 | E |
| Firma oluştur | `/firms/new` | 📋 | E |
| Siteler | `/sites` | 📋 | E |
| Blok ve Bağımsız Bölümler | `/units` | 📋 | E |

## Kullanıcılar & Roller

| Ekran | Yol | Durum | Faz |
|---|---|---|---|
| Kullanıcı listesi | `/system/users` | 📋 | F |
| Kullanıcı detay | `/system/users/{id}` | 📋 | F |
| Rol listesi | `/system/roles` | 📋 | F |
| İzinler | `/system/permissions` | 📋 | F |
| Admin emergency 2FA reset | (eylem) | 📋 | F |
| Denetim logları | `/system/audit` | 📋 | G |

## Malik / Sakin Modülü

| Ekran | Yol | Durum | Faz |
|---|---|---|---|
| Malikler | `/owners` | 📋 | - |
| Kiracılar (ev) | `/tenants` | 📋 | - |
| Yönetim Kurulu | `/board` | 📋 | - |

## Finansal Modül

| Ekran | Yol | Durum | Faz |
|---|---|---|---|
| Bütçeler | `/budgets` | 📋 | - |
| Tahakkuk | `/accruals` | 📋 | - |
| Tahsilat | `/collections` | 📋 | - |
| Hesap Ekstresi | `/statements` | 📋 | - |
| İcra Takibi | `/legal-collections` | 📋 | - |

## İletişim & Talepler Modülü

| Ekran | Yol | Durum | Faz |
|---|---|---|---|
| Talep/Şikayet/Öneri | `/requests` | 📋 | - |
| Duyurular | `/announcements` | 📋 | - |
| Karar Defteri | `/decisions` | 📋 | - |

## Raporlar

| Ekran | Yol | Durum | Faz |
|---|---|---|---|
| Finansal Özet | `/reports/financial` | 📋 | - |
| Tahsilat Raporu | `/reports/collections` | 📋 | - |
| Borçlu Listesi | `/reports/debtors` | 📋 | - |

## v2 — MVP Sonrası Modüller

| Modül | Durum |
|---|---|
| Muhasebe (hesap planı, yevmiye, mizan) | ⏳ v2 |
| İnsan Kaynakları (personel, puantaj, bordro, SGK) | ⏳ v2 |
| Satın Alma & Stok (talep, sipariş, stok) | ⏳ v2 |

---

## Proje Genelinde UI / Tablo Standardı — Gelecek İşler

Bu bölüm, **tüm liste sayfalarında** ortak olması gereken ama henüz yapılmamış
özellikleri listeler. Uygulandığı yer: `SiteHubDataGrid<T>` wrapper
(`Components/Shared/SiteHubDataGrid.razor`) — tek bir yerde yapılır, tüm liste
sayfaları (Organization, Site, Unit, Person, …) otomatik kazanır.

| # | Özellik | Not | Durum |
|---|---|---|---|
| 3 | **Kolon bazlı server-side filtre** | MudDataGrid'in `FilterDefinition`'ları backend handler'larına aktarılmalı. Her kolon için tip-güvenli filter builder gerek. İlk sürüm: text kolon `contains`. | 📋 |
| 6 | **Excel (xlsx) + PDF export** | **Filtrelenmiş veri** + mevcut sıra korunarak. ClosedXML + QuestPDF (ikisi de MIT). Büyük veri için stream + temp file. Ayrı ADR yazılmalı: *Export Stratejisi*. | 📋 |
| 7 | **Çok kelimeli AND arama** | Backend search: "güneş kadıköy" → `LIKE '%gunes%' AND LIKE '%kadikoy%'`. Her Query handler'ında `search` parametresi split edilip AND ile uygulanmalı (Organizations, Sites, Persons, Units, …). Proje genelinde tutarlı davranış. | 📋 |

> **Not:** Bu özellikler F.6 kapsamında **bilinçli olarak ertelendi.** Öncelik önce
> CRUD UI'nın tamamlanması. Yukarıdakiler F.6 bittikten sonra ayrı bir "UI Standart
> İyileştirme" iterasyonunda tek seferde yapılır ki tüm sayfalar aynı anda kazansın.

---

## Faz Tamamlama Özeti

| Faz | İçerik | Durum |
|---|---|---|
| **0** | İskelet, Docker, ADR'ler | ✅ |
| **1-2** | Infrastructure + Geography seed | ✅ |
| **3A** | Identity core (Person, LoginAccount, Role, Membership) | ✅ |
| **3B** | Permission seed + SystemRolesSeeder + DevelopmentUsersSeeder | ✅ |
| **C** | Login UI + session + Redis + avatar menu | ✅ |
| **D1** | Şifremi unuttum (email + SMS interface) | ✅ |
| **D2** | 2FA (TOTP) setup/verify/disable | ✅ |
| **D3** | Hangfire + cleanup + 2FA rate limit + login hak mesajı + config-based lockout | ✅ |
| **E** | Organization CRUD (firma/site/unit) | 🚧 |
| **F** | Kullanıcı & Rol yönetimi UI | 📋 |
| **G** | Security audit events + admin emergency 2FA reset | 📋 |
| **H** | IP-based rate limit + advanced security | 📋 |
