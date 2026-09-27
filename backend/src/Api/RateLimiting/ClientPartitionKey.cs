using System.Net;
using System.Net.Sockets;

namespace Api.RateLimiting;

/// <summary>
/// Builds the rate-limiter partition key for a client IP address: IPv4 addresses are
/// used as-is, IPv4-mapped IPv6 addresses are converted to their IPv4 form first (so
/// dual-mode Kestrel sockets don't lump every IPv4 client into a single IPv6 bucket),
/// and other IPv6 addresses are masked to their /64 network prefix (a single host
/// typically controls an entire /64, so keying on the full address would let it bypass
/// the per-client limit).
/// </summary>
public static class ClientPartitionKey
{
    public static string From(IPAddress? address)
    {
        if (address is null)
        {
            return "unknown";
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        for (var i = 8; i < 16; i++)
        {
            bytes[i] = 0;
        }

        return $"{new IPAddress(bytes)}/64";
    }
}
