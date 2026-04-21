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
