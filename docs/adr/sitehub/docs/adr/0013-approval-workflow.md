# ADR-0013: Onay Zinciri (Approval Workflow) Altyapısı

**Durum:** Taslak (onay bekliyor)
**Tarih:** 2026-04-20
**İlgili:** ADR-0011 (Kimlik/Yetki), ADR-0012 (Organizasyonel Yapı), ADR-0006 (Audit)

## Bağlam

SiteHub'da birçok iş operasyonu **onaydan geçmeden** kalıcı hale gelmemeli:

- Servis sözleşmesi imzalama (ADR-0012 §9.13)
- Hissedar/kiracı para iadesi IBAN tanımlama (ADR-0012 §7.2, §8.1)
- Hard delete işlemleri (ADR-0012 §10.11)
- (v2) Bütçe onayı, ödeme talimatı, yüksek tutarlı fatura, BB yapısı değişikliği, tahakkuk oluşturma

Bu ADR, **generic** bir onay zinciri altyapısı tanımlar. Her iş tipi için ayrı onay kodu yazılmasın; tek bir altyapıyı farklı politika şablonları ile kullanırız.

## Karar Özeti

| Konu | Karar |
|---|---|
| Desen | Generic `IApprovable` + `ApprovalPolicy` + `ApprovalInstance` |
| Çok aşamalı | Birden fazla adım (sequential) |
| Çoklu onaylayıcı | Her adımda 1..n kişi (quorum ile) |
| Adım tipi | Sıralı (step 2, step 1'den sonra); paralel adımlar v2 |
| Koşullu adımlar | ActivationCondition (expression) ile step atlama — MVP altyapı var, kullanım v2 modülleriyle |
| Quorum | Yüzde bazlı (örn. %50, %60), minimum kişi sayısı, "hepsi" seçeneği |
| SLA | Her adım için gün bazında süre |
| Vekalet | Kullanıcı kendi onayını başkasına delege edebilir (izin dahilinde) |
| Eskalasyon | SLA ihlalinde bir üst amire + süreç sahibine bildirim |
| Reddetme | Herhangi bir adımda red → süreç `Rejected`, red nedeni zorunlu |
| Değişiklik olursa | Aktif süreçte kaynak entity değişirse → süreç resetlenir veya iptal (policy kararına göre) |
| Audit | Her aksiyon `audit.approval_events` (özel tablo) |
| Analiz | Dashboard: ortalama bekleme, SLA ihlal oranı, en çok reddeden, vs. |

---

## 1. Temel Kavramlar

### 1.1. IApprovable (Onaylanabilir Entity)

Bir domain entity'si "bu değişikliğim onaydan geçmeli" dediğinde `IApprovable` olur:

```csharp
public interface IApprovable
{
    Guid Id { get; }
    string ApprovableType { get; }      // "ServiceContract", "BankRefundIban"
    ApprovalStatus ApprovalStatus { get; }
    Guid? CurrentApprovalInstanceId { get; }

    void OnApprovalCompleted(ApprovalInstanceId instance);
    void OnApprovalRejected(ApprovalInstanceId instance, string reason);
    void OnApprovalCancelled(ApprovalInstanceId instance, string reason);
}
```

**ApprovalStatus enum:**

| Değer | Anlam |
|---|---|
| NotRequired | Bu entity'de onay istenmez |
| Draft | Entity hazırlanıyor, henüz gönderilmedi |
| Pending | Onay sürecinde |
| Approved | Tüm adımlar geçti, aktif |
| Rejected | Reddedildi |
| Cancelled | Süreç iptal edildi (kaynağın silinmesi, değişiklik vb.) |

### 1.2. ApprovalPolicy (Onay Politikası)

Bir **iş tipi** için onay zincirinin **şablon** tanımı. Örn. "ServiceContract için onay politikası" — kaç adım, kimler onaylar, SLA ne.

```csharp
public sealed class ApprovalPolicy : AuditableAggregateRoot<ApprovalPolicyId>
{
    public string ApprovableType { get; }       // "ServiceContract"
    public string Name { get; }                  // "Site Hizmet Sözleşmesi Onayı"
    public string? Description { get; }
    public Guid? ScopeOrganizationId { get; }    // null = sistem varsayılanı, değilse org-specific
    public Guid? ScopeSiteId { get; }            // null = org veya sistem varsayılanı, değilse site-specific
    public bool IsActive { get; }
    public List<ApprovalPolicyStep> Steps { get; }
    public ApprovalPolicyConditions? Conditions { get; }  // hangi durumlarda uygulanır (v2)
}
```

### 1.3. ApprovalPolicyStep (Politika Adımı)

Her adım:

```csharp
public sealed class ApprovalPolicyStep : Entity<ApprovalPolicyStepId>
{
    public int SequenceNumber { get; }           // 1, 2, 3 ...
    public string Name { get; }                   // "Org Yöneticisi Onayı", "Yönetim Kurulu Onayı"
    public ApproverType ApproverType { get; }
    public string? ApproverRoleKey { get; }       // ApproverType=Role ise rol adı
    public List<Guid>? ApproverUserIds { get; }   // ApproverType=SpecificUsers ise
    public QuorumRule Quorum { get; }
    public int SlaDays { get; }                   // 3, 7, 14 vb.
    public bool CanDelegate { get; }              // Bu adımda vekalet verilebilir mi?
    public EscalationPolicy? OnSlaBreach { get; }
    public string? ActivationCondition { get; }   // Opsiyonel — bkz. §4 Koşullu Adımlar
}
```

**ActivationCondition (opsiyonel):** Boş ise adım her zaman çalışır. Dolu ise expression değerlendirilir; `false` dönerse adım `Skipped` olarak işaretlenir ve bir sonrakine geçilir. Detay: §4.

### 1.4. ApproverType (Onaylayıcı Tipi)

```csharp
public enum ApproverType
{
    Role,              // Belirli bir rolde olan TÜM kullanıcılar (örn. tüm OrgManager'lar)
    SpecificUsers,     // Belirli LoginAccount ID'leri
    UserFromRelation,  // Dinamik: entity ile ilişkili kişi (örn. BB'nin maliki)
    SiteBoard,         // Site yönetim kurulu üyeleri (özel)
    SystemAdmin        // Sistem yöneticileri
}
```

### 1.5. QuorumRule (Çoğunluk Kuralı)

```csharp
public sealed record QuorumRule
{
    public QuorumType Type { get; }         // AnyOne, AllRequired, Percentage, MinimumCount
    public int? MinimumCount { get; }        // "en az 3 kişi onay"
    public decimal? PercentageThreshold { get; }  // 0.50, 0.60, vb.
    public bool UnanimousRejection { get; }  // "bir kişi red → süreç red" mi, "oylama" mı
}
```

**QuorumType örnekleri:**

| Tip | Açıklama | Örnek |
|---|---|---|
| AnyOne | Herhangi birinin onayı yeter | OrgManager rolünden biri onaylarsa geçer |
| AllRequired | Hepsi onaylamalı | Çift imza: 2 müdür de onaylamalı |
| Percentage | Belirli orana ulaşmalı | Site Yönetim Kurulu'nun %51'i |
| MinimumCount | Minimum sayıya ulaşmalı | En az 3 yönetim kurulu üyesi |

### 1.6. ApprovalInstance (Süreç Kaydı)

Bir IApprovable entity'nin onaya alındığında oluşan **somut süreç kaydı**. Her gönderim yeni bir instance yaratır.

```csharp
public sealed class ApprovalInstance : AuditableAggregateRoot<ApprovalInstanceId>
{
    public ApprovalPolicyId PolicyId { get; }
    public string ApprovableType { get; }
    public Guid ApprovableId { get; }
    public ApprovalStatus Status { get; }
    public DateTimeOffset SubmittedAt { get; }
    public LoginAccountId SubmittedBy { get; }
    public DateTimeOffset? CompletedAt { get; }
    public List<ApprovalStepInstance> StepInstances { get; }
    public int CurrentStepIndex { get; private set; }
    public string? FinalDecisionReason { get; }      // Red veya iptal sebebi
    public string? SubmissionNote { get; }            // Başlatan kişi not yazar
    public JsonDocument? ContextSnapshot { get; }     // entity'nin o anki durumu (audit için)
}
```

### 1.7. ApprovalStepInstance (Adım Kaydı)

Her policy step'in somut uygulaması:

```csharp
public sealed class ApprovalStepInstance : Entity<ApprovalStepInstanceId>
{
    public int SequenceNumber { get; }
    public ApprovalStepStatus Status { get; }   // Pending, Approved, Rejected, Skipped, Escalated
    public DateTimeOffset? StartedAt { get; }
    public DateTimeOffset? CompletedAt { get; }
    public DateTimeOffset SlaDeadline { get; }
    public List<ApproverDecision> Decisions { get; }
    public List<Guid> EligibleApproverIds { get; } // başlangıçta hesaplanır (UserFromRelation için)
}

public sealed record ApproverDecision
{
    public LoginAccountId ApproverId { get; }
    public LoginAccountId? DelegatedFromId { get; }  // vekaleten onaylıyorsa
    public DecisionType Decision { get; }  // Approved, Rejected, Abstained
    public DateTimeOffset DecidedAt { get; }
    public string? Comment { get; }
    public string IpAddress { get; }
    public string UserAgent { get; }
}
```

---

## 2. Yaşam Döngüsü

### 2.1. Bir Süreç Nasıl Başlar

1. IApprovable entity hazırlanır (örn. ServiceContract Draft)
2. Kullanıcı "Onaya Gönder" der
3. Sistem uygun **ApprovalPolicy**'yi bulur (§5)
4. `ApprovalInstance` yaratılır:
   - Policy'den adımlar kopyalanır → `ApprovalStepInstance` listesi
   - İlk adım için `EligibleApproverIds` hesaplanır (dinamik)
   - İlk adım `Status = Pending`, `StartedAt = now`, `SlaDeadline = now + SlaDays`
5. Entity'nin `ApprovalStatus = Pending` + `CurrentApprovalInstanceId` set
6. İlk adımdaki onaylayıcılara bildirim gider (e-posta + (opsiyonel) SMS + sistem bildirimi)

### 2.2. Onaylayıcı Aksiyonu

Onaylayıcı "Onay Bekleyenler" ekranında süreci görür:

1. Detaya tıklar → entity'nin özeti + süreç geçmişi + onay notu
2. Aksiyon seçer: **Onayla / Reddet / Vekalet Ver / Çekimser (opsiyonel)**
3. Not yazar (red için zorunlu, onay için opsiyonel)
4. Kaydet → `ApproverDecision` kaydı oluşur

### 2.3. Adım Tamamlanma Kontrolü

Her karar sonrası sistem Quorum kontrolü yapar:

```
CheckStepCompletion(step):
  decisions = step.Decisions
  approvedCount = decisions.Count(d => d.Decision == Approved)
  rejectedCount = decisions.Count(d => d.Decision == Rejected)
  totalEligible = step.EligibleApproverIds.Count

  if step.Quorum.UnanimousRejection && rejectedCount > 0:
    step.Status = Rejected
    MarkInstanceAsRejected()
    return

  switch step.Quorum.Type:
    case AnyOne:
      if approvedCount >= 1:
        step.Status = Approved
        AdvanceToNextStep()
    case AllRequired:
      if approvedCount == totalEligible:
        step.Status = Approved
        AdvanceToNextStep()
    case MinimumCount:
      if approvedCount >= step.Quorum.MinimumCount:
        step.Status = Approved
        AdvanceToNextStep()
    case Percentage:
      if (approvedCount / totalEligible) >= step.Quorum.PercentageThreshold:
        step.Status = Approved
        AdvanceToNextStep()
```

### 2.4. Sonraki Adım

```
AdvanceToNextStep():
  currentStep.Status = Approved
  currentStep.CompletedAt = now

  while true:
    if CurrentStepIndex == LastStep:
      // Süreç başarıyla tamamlandı
      Instance.Status = Approved
      Entity.OnApprovalCompleted(Instance.Id)
      Publish ApprovalCompletedEvent
      return

    CurrentStepIndex++
    nextStep = StepInstances[CurrentStepIndex]

    // Koşullu adım kontrolü (§4)
    if nextStep.ActivationCondition is not null:
      result = EvaluateCondition(nextStep.ActivationCondition, context)
      if result == false:
        nextStep.Status = Skipped
        nextStep.CompletedAt = now
        Publish StepSkippedEvent(step, "Koşul false")
        continue  // sonraki adıma geç (aynı while'da)

    // Adım çalışacak
    nextStep.StartedAt = now
    nextStep.SlaDeadline = now + nextStep.SlaDays
    nextStep.EligibleApproverIds = ComputeApprovers(policy, nextStep, context)
    NotifyApprovers(nextStep)
    return
```

**Zincirleme atlama:** Birden fazla koşullu adım arka arkaya false dönerse `while` döngüsü atlamaya devam eder. En sonunda ya bir aktive olan step bulunur ya da son step'e gelinir → süreç tamamlanır.

**Policy invariant:** En az bir step'in `ActivationCondition = null` olmalı (aksi halde tüm step'ler atlanabilir, süreç anlamsız olur). Policy yaratılırken doğrulanır.

### 2.5. Reddetme

Herhangi bir adımda red koşulu oluşursa:

```
Instance.Status = Rejected
Instance.CompletedAt = now
Instance.FinalDecisionReason = rejectionNote
Entity.OnApprovalRejected(Instance.Id, reason)
Publish ApprovalRejectedEvent

// Gönderene bildirim
NotifySubmitter("Süreciniz reddedildi: {reason}")
```

Süreç reddedildikten sonra entity tekrar **Draft**'a çekilebilir ve yeniden gönderilebilir — bu durumda **yeni bir ApprovalInstance** yaratılır, eskisi tarihçe olarak kalır.

### 2.6. İptal

Süreç iptal koşulları:
- Kaynağın silinmesi (ServiceContract soft-delete)
- Kaynağın kritik alanlarının değişmesi (IBAN değişti vb. — policy'e göre)
- Başlatan kişi veya yetkili kullanıcı "İptal" demesi

```
Instance.Status = Cancelled
Entity.OnApprovalCancelled(Instance.Id, reason)
```

### 2.7. Vekalet (Delegation)

Onaylayıcı "Ben 15 gün tatildeyim, benim onaylarıma Ayşe baksın" der:

1. Kullanıcı Profili → "Vekalet Yönetimi" ekranı
2. "Yeni Vekalet" → Delegat, başlangıç, bitiş tarihi, kapsam (tüm süreçler mi belirli bir policy mi)
3. Sistem bu tarih aralığında vekilin yerine kullanıcıyı onaylayıcı olarak kabul eder
4. Vekil onay verirken `ApproverDecision.DelegatedFromId` doldurulur
5. Audit log'da "Ayşe, Ahmet adına onayladı" şeklinde kaydedilir

**Vekalet kısıtları:**
- Vekil, asıl onaylayıcının sahip olduğu izinlere sahip olmalı (izin düşürme yok)
- Vekalet başlangıç/bitiş tarihleri arasında aktif
- Policy "CanDelegate = false" ise o adımda vekalet yasak (hassas kararlar için)
- Bir kullanıcı aynı anda birden fazla vekalet tanımlayabilir (farklı kişiler için)
- Zincir vekalet: Ahmet → Ayşe → Mehmet yasak; Ayşe vekaletini Mehmet'e aktarmaya izin verilmez (karışıklık olur)

### 2.8. Eskalasyon (SLA İhlali)

SLA süresi dolmuşsa:

1. Background job `ApprovalSlaCheckerJob` her saat çalışır
2. `StartedAt + SlaDays < now && Status == Pending` olan step'leri bulur
3. `EscalationPolicy`'ye göre aksiyon alır:

**Eskalasyon seçenekleri:**

| Aksiyon | Açıklama |
|---|---|
| `ReminderNotification` | Onaylayıcıya + amirine hatırlatma bildirimi, süreç devam eder |
| `EscalateToManager` | Amirine yeni görev düşer, o da onaylayabilir |
| `AutoApprove` | Süreç otomatik onaylanır (sakıncalı, çok az durumda) |
| `AutoReject` | Süreç otomatik reddedilir |
| `SkipStep` | Adım atlanır, sonraki adıma geçilir (yalnızca belirli durumlar) |

MVP için **`ReminderNotification` + `EscalateToManager`** yeterli. Diğerleri v2.

---

## 3. Onaylayıcı Hesaplama (ComputeApprovers)

Policy step'i `ApproverType`'a göre gerçek kişileri belirler:

```
ComputeApprovers(policy, step, context):
  switch step.ApproverType:
    case Role:
      // Scope içindeki o rolde olan tüm kullanıcılar
      scope = DetermineScope(context)  // org/site/sistem
      users = SELECT login_account_id FROM memberships
              WHERE role_key = step.ApproverRoleKey
                AND context_type = scope.Type
                AND context_id = scope.Id
                AND is_active

    case SpecificUsers:
      users = step.ApproverUserIds

    case UserFromRelation:
      // Entity ile ilişkili kişi (örn. BB'nin aktif maliki)
      users = ResolveRelation(context, step.RelationKey)

    case SiteBoard:
      users = SELECT login_account_id FROM memberships
              WHERE role_key = "SiteBoard"
                AND context_type = Site
                AND context_id = site.Id
                AND is_active

    case SystemAdmin:
      users = SELECT login_account_id FROM memberships
              WHERE role_key = "SystemAdmin"
                AND context_type = System

  // Vekalet kontrolü — aktif vekalet varsa delegate'leri de ekle
  users += ResolveActiveDelegations(users, now)

  return users
```

### 3.1. Dinamik (UserFromRelation) Örneği

BB'de malik değişikliği onayı:
- ApproverType = UserFromRelation
- RelationKey = "UnitPeriod.Shareholders"
- Süreç başlarken context'ten BB ve aktif UnitPeriod alınır
- Mevcut Shareholder'lar onaylayıcı olur

Bu dinamik hesaplama sayesinde politika şablonu **hangi özel kişi olduğunu bilmeden** genel tanımlanır.

---

## 4. Koşullu Adımlar (Conditional Steps)

Gerçek iş süreçlerinde bazı onay adımları **belirli koşullarda çalışmalı, diğerlerinde atlanmalıdır.** Örnek senaryolar (v2+ modülleriyle devreye girecek):

- **Finansal Ödeme:** Tutar > 25K → Yönetim Kurulu onayı gerekir; daha azı Site Yöneticisi onayı yeter
- **Satın Alma Talebi:** Tutar > 50K → Denetim Kurulu; cinsi "Yatırım Malzemesi" → teknik onay
- **İş Avansı Talebi:** Tutar > aylık maaş × 2 → Org Owner onayı
- **BB Hard Delete:** IsSensitive=true → Yasal Danışman ek onayı

### 4.1. Nasıl Çalışır (MVP Altyapısı)

`ApprovalPolicyStep.ActivationCondition` alanına **koşul ifadesi** yazılır (C# expression syntax). Adım sırası geldiğinde:

```
Adım sırası geldi (CurrentStepIndex = k)
  ↓
step = StepInstances[k]
  ↓
Koşul var mı? (step.ActivationCondition != null)
  │
  ├── YOK   → Adım çalışır: Status=Pending, bildirim gider
  └── VAR   → Koşul değerlendir:
               │
               ├── TRUE  → Adım çalışır: Status=Pending, bildirim gider
               └── FALSE → Adım ATLANIR: Status=Skipped, CurrentStepIndex++
                           Sonraki adıma geç (recursive kontrol)
```

### 4.2. Expression Dili

**Kütüphane:** DynamicExpresso (NuGet) veya Flee — .NET'te C# benzeri expression parser.

**Kullanılabilir değişkenler (context):**

| Değişken | Tip | Kaynak |
|---|---|---|
| `Entity` | dynamic | IApprovable entity'nin kendisi (tüm public property'leri) |
| `Submitter` | LoginAccount | Süreci başlatan kişi |
| `SubmittedAt` | DateTimeOffset | Süreç başlangıç zamanı |
| `StepDecisions[n]` | List\<ApproverDecision\> | n numaralı step'in kararları (önceki adımlara bakmak için) |

**Örnek koşullar:**

```csharp
// Finansal ödeme
"Entity.Amount > 25000"

// Satın alma türe göre
"Entity.PurchaseType == \"Investment\" || Entity.Amount > 50000"

// Sözleşme aylık ücretine göre
"Entity.MonthlyFee > 50000 && Entity.Currency == \"TRY\""

// Önceki adımda OrgManager'ın yorumu "ek inceleme" ise ek adım
"StepDecisions[0].Any(d => d.Comment != null && d.Comment.Contains(\"ek inceleme\"))"

// Hassas belge silme
"Entity.Category.IsSensitive == true"

// Geriye dönük (önceki step reddettiyse ek adım) — genelde gereksiz ama mümkün
"StepDecisions[0].Any(d => d.Decision == DecisionType.Approved) && Entity.Amount > 100000"
```

### 4.3. Güvenlik

Expression kullanıcı input'u ile değerlendirilir → güvenlik riski. Önlemler:

- **Sandbox mode:** DynamicExpresso'da sadece whitelisted metodlar, System.IO/Reflection yok
- **Whitelisted tipler:** Entity, Submitter, StepDecisions, temel primitives (int, string, decimal, bool, DateTime)
- **Timeout:** Expression evaluation 100ms'yi geçerse iptal (DoS koruması)
- **Syntax validation:** Policy yaratılırken koşul sözdizimi denenir, geçersizse kayıt reddedilir
- **Audit log:** Her koşul değerlendirmesi kaydedilir (hangi koşul + sonuç)

### 4.4. Tüm Koşullar False Olursa (Edge Case)

Policy'de 3 step var, üçü de koşullu, hiçbiri aktive olmadı:
- **Süreç otomatik onaylanır mı?** Hayır — tehlikeli
- **En az 1 step çalışmalı** invariant'ı policy yaratılırken kontrol edilir
- Policy: `At least one step MUST be unconditional (ActivationCondition = null)` kuralı
- İlk step genelde koşulsuz (en az bir onaylayıcı daima dahil)

### 4.5. Koşul Ne Zaman Değerlendirilir?

**Her adımın sırası geldiğinde** (lazy evaluation) — süreç başlangıcında değil. Çünkü:

- Entity değişebilir mi? Genelde hayır (süreç başladıktan sonra entity immutable olmalı — §7.6)
- Önceki step kararları context'e eklenmeli (StepDecisions array dolarak gider)
- Lazy evaluation daha net + hata ayıklama kolaylaşır

### 4.6. MVP'de Kullanım

MVP seed policy'leri (§6) hiçbir adımda `ActivationCondition` kullanmaz. **Altyapı hazır, kullanım v2 modülleriyle başlar:**

| Modül | Versiyon | Koşul örnekleri |
|---|---|---|
| ServiceContract onayı | MVP | Yok (koşulsuz 2 adım) |
| Ödeme Onayı | v2 (Cari Hesap) | Tutar-bazlı katmanlı onay |
| Satın Alma Talebi | v3 (Satın Alma modülü) | Tutar + cins bazlı |
| İş Avansı | v5 (İK modülü) | Aylık maaş oranı bazlı |
| Hard Delete (Belge) | v2 | IsSensitive bazlı |

### 4.7. Politika Hiyerarşisi

Bir entity için hangi ApprovalPolicy'nin uygulanacağı **hiyerarşik arama** ile bulunur:

```
FindApplicablePolicy(approvableType, context):
  // En spesifikten en geneline doğru
  1. Site-specific policy (ScopeSiteId = context.SiteId)
  2. Org-specific policy (ScopeOrganizationId = context.OrgId)
  3. System-default policy (Scope = null)

  İlk eşleşen aktif (IsActive=true) policy döner.
```

**Örnek:**
- Sistem genelinde "ServiceContract için varsayılan onay: OrgManager + SiteBoard"
- ABC Yönetim Organizasyonu kendi özel politikasını tanımladı: "ServiceContract için 3 adım (OrgManager + SiteBoard + OrgOwner)"
- Çamlıca Sitesi daha da özel politika: "4 adım (Muhasebe + OrgManager + SiteBoard + OrgOwner)"

Çamlıca Sitesi'nde ServiceContract imzalanırken 4 adımlı Çamlıca politikası uygulanır. Başka sitede (ABC'nin başka sitesi) 3 adımlı ABC politikası. Başka organizasyonda sistem varsayılanı.

**Avantajı:** Merkezi kural + özel yerelleştirme. Kurumsal esneklik.

---

## 5. Varsayılan Politikalar (Seed)

Aşağıdaki sistem varsayılan politikaları seed olarak yüklenir:

### 5.1. ServiceContract (Sistem Varsayılanı)

| Adım | Ad | Onaylayıcı | Quorum | SLA |
|---|---|---|---|---|
| 1 | Org Yöneticisi Onayı | Role: OrganizationManager | AnyOne | 3 gün |
| 2 | Site Yönetim Kurulu Onayı | SiteBoard | Percentage 50%+1 | 7 gün |

### 5.2. BankRefundIban (Hissedar/Kiracı Para İade IBAN'ı — v2)

| Adım | Ad | Onaylayıcı | Quorum | SLA |
|---|---|---|---|---|
| 1 | Muhasebe Onayı | Role: SiteAccounting | AnyOne | 2 gün |
| 2 | Site Yöneticisi Onayı | Role: SiteManager | AnyOne | 2 gün |

### 5.3. HardDelete (Kalıcı Belge Silme — v2)

| Adım | Ad | Onaylayıcı | Quorum | SLA |
|---|---|---|---|---|
| 1 | System Admin Onayı | SystemAdmin | AnyOne | 3 gün |
| 2 | Yasal Danışman Onayı | Role: LegalAdvisor | AnyOne | 7 gün |

### 5.4. MVP Kapsamında Aktif

MVP'de **sadece §5.1 ServiceContract** aktive edilir. Diğerleri v2 ile birlikte devreye girer.

---

## 6. UI Ekranları

### 6.1. Onay Bekleyenler (Inbox)

Her kullanıcının kendi dashboard'unda:
- Onay bekleyen süreçler listesi
- Filtre: ApprovableType, SLA durumu (günü dolmak üzere, geçmiş), önem
- Satır: entity özeti, başlayan tarih, kalan süre, aksiyon butonları

### 6.2. Süreç Detayı

- Entity'nin detaylı görünümü
- Tüm adımlar ve durumları (görsel flow)
- Yapılmış kararlar + kim + ne zaman + yorum
- Kendi aksiyon seçenekleri (onayla/reddet/vekalet)

### 6.3. Gönderilmiş Süreçlerim

- Kendi başlattığım süreçler
- Durum takibi (hangi adımda, ne bekliyor)
- İptal opsiyonu

### 6.4. Vekalet Yönetimi

- Aktif vekaletlerim
- Yeni vekalet ekleme
- Vekalet geçmişi

### 6.5. ApprovalPolicy Yönetimi (Admin)

- Org/Site yöneticileri kendi politikalarını yaratır/düzenler
- Şablon seçim (sistem varsayılanından kopyala + düzenle)
- Politika aktif/pasif toggle
- Test modu (v2 — politikanın sonucunu önizleme)

### 6.6. Analytics Dashboard (v2)

- Ortalama onay süresi
- SLA ihlal oranı
- En çok reddeden onaylayıcılar
- Bekleyen işlerin yaş dağılımı
- Eskalasyon heat map

---

## 7. Audit ve Tarihçe

Tüm onay aksiyonları `audit.approval_events` tablosuna yazılır:

| Alan | Tip |
|---|---|
| Id | Guid |
| Timestamp | datetimeoffset |
| EventType | enum |
| InstanceId | FK |
| StepInstanceId | FK? |
| ApproverId | FK? |
| DelegatedFromId | FK? |
| Details | JSONB |
| IpAddress | string |
| UserAgent | string |

**EventType değerleri:**
- InstanceSubmitted
- InstanceApproved
- InstanceRejected
- InstanceCancelled
- StepStarted
- StepApproved
- StepRejected
- StepSkipped (koşul false — §4)
- DecisionRecorded
- DelegationCreated
- DelegationRevoked
- SlaBreached
- Escalated
- PolicyChanged (policy mid-süreçte değiştirildi — v2'de kısıtlı)
- ConditionEvaluated (hangi koşul + sonuç + kullanılan değişkenler — §4.3)

**Retention:** 10 yıl (ADR-0006).

---

## 8. Domain Entegrasyonu — IApprovable Örneği

```csharp
public sealed class ServiceContract : AuditableAggregateRoot<ServiceContractId>, IApprovable
{
    public ApprovalStatus ApprovalStatus { get; private set; }
    public Guid? CurrentApprovalInstanceId { get; private set; }
    public string ApprovableType => nameof(ServiceContract);

    public void SubmitForApproval(IApprovalWorkflowService workflow, LoginAccountId submittedBy, string? note)
    {
        if (Status != ServiceContractStatus.Draft)
            throw new InvalidStateException("Yalnızca Draft durumundaki sözleşme onaya gönderilebilir");

        var instance = workflow.Submit(this, submittedBy, note);
        Status = ServiceContractStatus.PendingApproval;
        ApprovalStatus = ApprovalStatus.Pending;
        CurrentApprovalInstanceId = instance.Id;
        AddDomainEvent(new ServiceContractSubmittedForApprovalEvent(Id, instance.Id));
    }

    public void OnApprovalCompleted(ApprovalInstanceId instance)
    {
        Status = ServiceContractStatus.Active;
        ApprovalStatus = ApprovalStatus.Approved;
        AddDomainEvent(new ServiceContractActivatedEvent(Id, DateTimeOffset.UtcNow));
    }

    public void OnApprovalRejected(ApprovalInstanceId instance, string reason)
    {
        Status = ServiceContractStatus.Rejected;
        ApprovalStatus = ApprovalStatus.Rejected;
        AddDomainEvent(new ServiceContractRejectedEvent(Id, reason));
    }

    public void OnApprovalCancelled(ApprovalInstanceId instance, string reason)
    {
        // İptal edilirse Draft'a dönüyor
        Status = ServiceContractStatus.Draft;
        ApprovalStatus = ApprovalStatus.NotRequired;
        CurrentApprovalInstanceId = null;
    }
}
```

---

## 9. IApprovalWorkflowService Interface

Application katmanından kullanılacak ana servis:

```csharp
public interface IApprovalWorkflowService
{
    Task<ApprovalInstance> Submit(IApprovable entity, LoginAccountId submitter, string? note);
    Task<ApprovalInstance> Approve(ApprovalInstanceId instanceId, LoginAccountId approver, string? comment);
    Task<ApprovalInstance> Reject(ApprovalInstanceId instanceId, LoginAccountId approver, string reason);
    Task<ApprovalInstance> Cancel(ApprovalInstanceId instanceId, LoginAccountId canceller, string reason);

    Task<IReadOnlyList<ApprovalInstance>> GetMyPending(LoginAccountId userId);
    Task<IReadOnlyList<ApprovalInstance>> GetMySubmitted(LoginAccountId userId);
    Task<ApprovalInstance?> GetInstance(ApprovalInstanceId id);

    Task<DelegationAssignment> CreateDelegation(LoginAccountId delegator, LoginAccountId delegate_, DateTimeOffset from, DateTimeOffset to, DelegationScope scope);
    Task RevokeDelegation(DelegationAssignmentId id, LoginAccountId by);
}
```

---

## 10. Implementation Sırası

**Faz A — Altyapı:**
1. `IApprovable` interface + `ApprovalStatus` enum
2. `ApprovalPolicy` + `ApprovalPolicyStep` + `QuorumRule` aggregate'i
3. `ApprovalInstance` + `ApprovalStepInstance` + `ApproverDecision` aggregate'i
4. `audit.approval_events` tablosu + interceptor

**Faz B — Servis ve iş kuralları:**
5. `IApprovalWorkflowService` + implementation
6. `ComputeApprovers` servisi (5 ApproverType)
7. `CheckStepCompletion` servisi (4 QuorumType)
8. `AdvanceToNextStep` koordinasyonu (**koşullu adım atlama dahil — §4**)
9. `IConditionEvaluator` servisi (DynamicExpresso wrapper, sandbox, timeout)

**Faz C — Entegrasyon:**
9. ServiceContract'ı `IApprovable` yapma + SubmitForApproval metodu
10. Policy seed (sistem varsayılan ServiceContract politikası)

**Faz D — UI:**
11. Onay Bekleyenler ekranı
12. Süreç Detayı ekranı
13. Gönderilmiş Süreçlerim ekranı
14. Policy Yönetimi ekranı (site/org)

**Faz E — SLA + Vekalet:**
15. `ApprovalSlaCheckerJob` background job
16. Eskalasyon bildirimleri
17. `DelegationAssignment` aggregate + UI

**v2:**
18. BankRefundIban için politika + entegrasyon
19. HardDelete için politika + entegrasyon
20. Analytics dashboard
21. Paralel adımlar, karmaşık koşullar

---

## 11. Alternatifler (Reddedilen)

### 11.1. Her Entity İçin Ayrı Onay Kodu
`ServiceContractApprovalService`, `BankAccountApprovalService` vb. ayrı ayrı. Reddedildi — kod duplikasyonu + tutarsızlık.

### 11.2. External Workflow Engine (Camunda, Elsa)
Camunda/Elsa gibi BPMN tabanlı engine'ler. Reddedildi çünkü:
- Operational complexity (ayrı DB, ayrı servis)
- Bizim ihtiyacımız çok standart (sıralı adım + quorum) — tam BPMN'e gerek yok
- Turkçe lokalizasyon ek yük
- Lisans/maliyet
- MVP'de overkill

### 11.3. Tek Adım — Çoklu Onaylayıcı (adımsız)
Tüm onayları tek adımda topla. Reddedildi — gerçek hayatta sıralama var (önce muhasebe, sonra yönetim).

### 11.4. Onay Zinciri Yerine "Dört Göz Prensibi" (Dual Control)
Sadece iki kişi onaylar. Reddedildi — bazı süreçler 3-4 adım gerektirir (yönetim kurulu).

---

## 12. Açık Konular (v2+)

- **Help/Learn Sistemi (UI entegrasyonu):** Onay politikası tanımlama ekranlarında kullanıcıya yardım edecek rehber sistemi:
  - Kullanıcıya neyin ne işe yaradığını açıklayan inline ipuçları (tooltip'ler)
  - "Bu nasıl çalışır?" gösteren illüstratif görseller / flowchart'lar
  - Örnek senaryolar ("tutar bazlı onay nasıl tanımlanır?" → step-by-step walkthrough)
  - Ekran üstü yardım paneli — step yaratırken ilgili dokümantasyon yan panelde açılır
  - Video/GIF örneği (opsiyonel — v2.5)
  - "İlk kez" kullanıcılar için onboarding akışı (interaktif tur)
  - Teknik terimler (Quorum, Eskalasyon, ApproverType vb.) için hover tooltip + detay popup
  - Koşul (expression) yazma yardımı: popüler pattern'ler, syntax örnekleri, canlı validator
  - Bu sistem genel olarak **tüm karmaşık modül tanımlama ekranları** için ortak bir "Learn Panel" altyapısı olarak düşünülmeli (sadece onay zinciri değil)
- **Paralel adımlar:** Aynı anda iki adımın birlikte çalışması (örn. muhasebe + hukuk paralel). MVP'de sıralı.
- **Gerçek dallanma (Branching):** Step'ler arasında DAG yapısı — "karar A ise X'e git, karar B ise Y'ye git" (MVP'deki Koşullu Adımlar sadece lineer atlama destekler; full DAG v2).
- **Dinamik SLA:** Bayram/tatil dönemlerinde SLA uzaması
- **Onay şablonu Git-style versiyonlama:** Policy değişikliği tarihçe + aktif sürecin sabit snapshot'ı
- **Mobil push bildirimi:** Onaylayıcıya anlık uyarı
- **E-imza entegrasyonu:** Kritik sözleşmelerde KEP/e-imza ile onay
- **Multi-language:** Onay bildirimlerinin kullanıcı diline göre
- **Analytics dashboard:** §6.6
- **Onay iş akışı webhook:** Dış sisteme haber (v3)

---

## 13. Karar Kaydı

Bu ADR'ı onaylayan: _________________
Tarih: _________________
Notlar: _________________
