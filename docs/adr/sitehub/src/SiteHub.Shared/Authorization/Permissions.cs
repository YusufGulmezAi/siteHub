namespace SiteHub.Shared.Authorization;

/// <summary>
/// Sistem geneli izin sabitleri (ADR-0011 §6.1).
///
/// HER İZİN KOD İÇİNDE SABİT — veritabanı ile senkronize tutulur
/// (PermissionSynchronizer deploy sırasında çalışır).
///
/// Adlandırma kuralı: {resource}.{action} (snake_case resource, küçük harf)
///
/// Neden kod-tabanlı?
/// - Yeni özellik geldikçe geliştirici kod ekler, deploy sırasında DB güncellenir
/// - Kod review sürecinden geçer (kimin ne izin eklediği görünür)
/// - Runtime'da "yeni izin" oluşturulamaz → güvenlik riski azalır
///
/// Bu sınıf SiteHub.Shared'da — tüm katmanlar referans verebilir (Domain, Application, UI).
/// </summary>
public static class Permissions
{
    /// <summary>Sistem seviyesi izinler (SystemAdmin, SystemSupport).</summary>
    public static class System
    {
        public const string Read        = "system.read";
        public const string Manage      = "system.manage";
        public const string Impersonate = "system.impersonate";
    }

    /// <summary>Organization (Yönetim/Servis Firması) izinleri.</summary>
    public static class Organization
    {
        public const string Read          = "organization.read";
        public const string Create        = "organization.create";
        public const string Update        = "organization.update";
        public const string Delete        = "organization.delete";
        public const string Analytics     = "organization.analytics";
        public const string BankManage    = "organization.bank.manage";
        public const string BranchManage  = "organization.branch.manage";
        public const string ContractSign  = "organization.contract.sign";
    }

    /// <summary>Site izinleri.</summary>
    public static class Site
    {
        public const string Read           = "site.read";
        public const string Create         = "site.create";
        public const string Update         = "site.update";
        public const string Delete         = "site.delete";
        public const string Analytics      = "site.analytics";
        public const string StructureEdit  = "site.structure.edit";
        public const string DocumentUpload = "site.document.upload";
        public const string BankManage     = "site.bank.manage";
    }

    /// <summary>Period (BB Dönem) izinleri.</summary>
    public static class Period
    {
        public const string Read   = "period.read";
        public const string Create = "period.create";
        public const string Update = "period.update";
        public const string Close  = "period.close";
    }

    /// <summary>Person izinleri. NOT: Delete yok — Person silinmez (ADR-0011 §1.3).</summary>
    public static class Person
    {
        public const string Read   = "person.read";
        public const string Create = "person.create";
        public const string Update = "person.update";
    }

    /// <summary>ServiceContract izinleri.</summary>
    public static class ServiceContract
    {
        public const string Read      = "service_contract.read";
        public const string Create    = "service_contract.create";
        public const string Update    = "service_contract.update";
        public const string Terminate = "service_contract.terminate";
    }

    /// <summary>Onay zinciri izinleri (ADR-0013).</summary>
    public static class Approval
    {
        public const string Approve      = "approval.approve";
        public const string PolicyManage = "approval.policy.manage";
    }
}
