#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
using System;
#endif
using System.Net;
using System.Net.Sockets;

namespace DotThrow.Internal;

// IP address ranges, read from the address bytes. IPv4-mapped IPv6 addresses
// (::ffff:10.0.0.1) are judged by their IPv4 part. The zone (scope id) is ignored.
internal static class Network
{
    public static bool IsLoopback(IPAddress address)
    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
        Span<byte> bytes = stackalloc byte[16];
        address.TryWriteBytes(bytes, out var length);
#else
        var bytes = address.GetAddressBytes();
        var length = bytes.Length;
#endif
        if (length == 4)
        {
            return bytes[0] == 127;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            return bytes[12] == 127;
        }

        // ::1
        for (var i = 0; i < 15; i++)
        {
            if (bytes[i] != 0)
            {
                return false;
            }
        }

        return bytes[15] == 1;
    }

    // IPv4 10/8, 172.16/12, 192.168/16 (RFC 1918), 100.64/10 (RFC 6598 shared address
    // space, carrier-grade NAT) and 169.254/16 (link-local); IPv6 fc00::/7 (unique local)
    // and fe80::/10 (link-local).
    public static bool IsPrivate(IPAddress address)
    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
        Span<byte> bytes = stackalloc byte[16];
        address.TryWriteBytes(bytes, out var length);
#else
        var bytes = address.GetAddressBytes();
        var length = bytes.Length;
#endif
        if (length == 4)
        {
            return IsPrivateIPv4(bytes[0], bytes[1]);
        }

        if (address.IsIPv4MappedToIPv6)
        {
            return IsPrivateIPv4(bytes[12], bytes[13]);
        }

        return (bytes[0] & 0xFE) == 0xFC || (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80);
    }

    public static bool IsIPv4(IPAddress address) => address.AddressFamily == AddressFamily.InterNetwork;

    public static bool IsIPv6(IPAddress address) => address.AddressFamily == AddressFamily.InterNetworkV6;

    private static bool IsPrivateIPv4(byte first, byte second) =>
        first == 10
        || (first == 172 && (second & 0xF0) == 16)
        || (first == 192 && second == 168)
        || (first == 100 && (second & 0xC0) == 64)
        || (first == 169 && second == 254);
}
