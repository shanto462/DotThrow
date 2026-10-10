using System.Net;
using DotThrow.Internal;

namespace DotThrow;

// Checks for IPAddress values. A null address fails with the "Null" code. An IPv4-mapped
// IPv6 address (::ffff:10.0.0.1) is judged by its IPv4 part.
public static partial class GuardExtensions
{
    /// <summary>Fails when the address is a loopback address: <c>127.0.0.0/8</c>, <c>::1</c>, or an IPv4-mapped loopback address.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    /// <remarks>
    /// <c>0.0.0.0</c> and <c>::</c> are the unspecified address, not loopback, so they pass.
    /// Together with <see cref="IfPrivate(Guard{IPAddress}, string?)"/> this helps against
    /// server-side request forgery only when you check the address you actually connect to
    /// (after DNS resolution); it is not complete SSRF protection on its own.
    /// </remarks>
    public static Guard<IPAddress> IfLoopback(this Guard<IPAddress> guard, string? message = null)
    {
        var address = guard.Value;
        if (guard.IsActive && (address is null || Network.IsLoopback(address)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.Loopback, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the address is in a private or link-local range: IPv4 <c>10.0.0.0/8</c>,
    /// <c>172.16.0.0/12</c>, <c>192.168.0.0/16</c>, <c>100.64.0.0/10</c> (shared address space
    /// of carrier-grade NAT, RFC 6598) and <c>169.254.0.0/16</c>; IPv6 <c>fc00::/7</c> (unique
    /// local) and <c>fe80::/10</c> (link-local). Loopback addresses are not in these ranges; use
    /// <see cref="IfLoopback(Guard{IPAddress}, string?)"/> for them.
    /// </summary>
    /// <remarks>
    /// This is not SSRF protection on its own: check the address you actually connect to, after
    /// DNS resolution, because a public host name can resolve to a private address.
    /// </remarks>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<IPAddress> IfPrivate(this Guard<IPAddress> guard, string? message = null)
    {
        var address = guard.Value;
        if (guard.IsActive && (address is null || Network.IsPrivate(address)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.Private, message);
        }

        return guard;
    }

    /// <summary>Fails when the address is not an IPv4 address. An IPv4-mapped IPv6 address is IPv6, so it fails.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<IPAddress> IfNotIPv4(this Guard<IPAddress> guard, string? message = null)
    {
        var address = guard.Value;
        if (guard.IsActive && (address is null || !Network.IsIPv4(address)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotIPv4, message);
        }

        return guard;
    }

    /// <summary>Fails when the address is not an IPv6 address.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<IPAddress> IfNotIPv6(this Guard<IPAddress> guard, string? message = null)
    {
        var address = guard.Value;
        if (guard.IsActive && (address is null || !Network.IsIPv6(address)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotIPv6, message);
        }

        return guard;
    }
}
