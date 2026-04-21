using System.Net;
using System.Net.Sockets;

namespace SiteHub.Application.Abstractions.Authentication;

/// <summary>
/// CIDR notasyonuyla IP eşleştirme yardımcıları.
/// "10.0.0.0/8", "192.168.1.0/24" formatlarını destekler. IPv4 ve IPv6.
///
/// <para>Kullanım (ADR-0011 §3.2):</para>
/// <code>
/// if (!CidrMatcher.IsIpInAnyRange(clientIp, loginAccount.IpWhitelist))
///     return LoginFailureCode.IpNotAllowed;
/// </code>
/// </summary>
public static class CidrMatcher
{
    /// <summary>
    /// <paramref name="ip"/> adresi <paramref name="cidrListCommaSeparated"/> içindeki
    /// CIDR aralıklarından herhangi birinde mi?
    /// </summary>
    /// <remarks>
    /// whitelist boş/null ise → her IP geçerli (kısıt yok, true döner).
    /// Bozuk CIDR'ler sessizce atlanır — whitelist tamamen bozuksa hiçbir IP eşleşmez.
    /// </remarks>
    public static bool IsIpInAnyRange(string ip, string? cidrListCommaSeparated)
    {
        // Whitelist yok → kısıt yok
        if (string.IsNullOrWhiteSpace(cidrListCommaSeparated))
            return true;

        if (!IPAddress.TryParse(ip, out var target))
            return false;

        var cidrs = cidrListCommaSeparated.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var cidr in cidrs)
        {
            if (MatchesCidr(target, cidr))
                return true;
        }
        return false;
    }

    private static bool MatchesCidr(IPAddress target, string cidr)
    {
        // "192.168.1.0/24" → (192.168.1.0, 24)
        var parts = cidr.Split('/', 2);
        if (parts.Length != 2) return false;

        if (!IPAddress.TryParse(parts[0], out var range))
            return false;
        if (!int.TryParse(parts[1], out var prefixLength) || prefixLength < 0)
            return false;

        // Farklı address family (IPv4 vs IPv6) → eşleşmez
        if (target.AddressFamily != range.AddressFamily)
            return false;

        var maxPrefix = target.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
        if (prefixLength > maxPrefix) return false;

        var targetBytes = target.GetAddressBytes();
        var rangeBytes = range.GetAddressBytes();

        int fullBytes = prefixLength / 8;
        int remainingBits = prefixLength % 8;

        // Tam byte'ları karşılaştır
        for (int i = 0; i < fullBytes; i++)
        {
            if (targetBytes[i] != rangeBytes[i]) return false;
        }

        // Kalan bitleri maskeyle karşılaştır
        if (remainingBits > 0 && fullBytes < targetBytes.Length)
        {
            byte mask = (byte)(0xFF << (8 - remainingBits));
            if ((targetBytes[fullBytes] & mask) != (rangeBytes[fullBytes] & mask))
                return false;
        }

        return true;
    }
}
