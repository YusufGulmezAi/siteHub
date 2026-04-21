namespace SiteHub.Infrastructure.Notifications;

/// <summary>
/// <c>appsettings.json</c> "Email" bölümünden okunur.
///
/// <para>Development:</para>
/// <code>
/// "Email": {
///   "Host": "localhost",
///   "Port": 1025,
///   "UseSsl": false,
///   "Username": null,
///   "Password": null,
///   "FromAddress": "noreply@sitehub.local",
///   "FromName": "SiteHub"
/// }
/// </code>
///
/// <para>Production'da Host + credentials gerçek SMTP'ye işaret eder.</para>
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public bool UseSsl { get; set; } = false;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "noreply@sitehub.local";
    public string FromName { get; set; } = "SiteHub";
}
