using System;
using DotThrow.Internal;

namespace DotThrow;

// Checks for Uri values. A null Uri fails with the "Null" code. For checks on strings that
// should be URIs, see IfNotUri and IfNotHttpUrl.
public static partial class GuardExtensions
{
    /// <summary>Fails when the URI is relative (<see cref="Uri.IsAbsoluteUri"/> is <see langword="false"/>).</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<Uri> IfRelative(this Guard<Uri> guard, string? message = null)
    {
        var uri = guard.Value;
        if (guard.IsActive && (uri is null || !uri.IsAbsoluteUri))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.Relative, message);
        }

        return guard;
    }

    /// <summary>Fails when the URI is not an absolute <c>https</c> URI. A relative URI fails.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<Uri> IfNotHttps(this Guard<Uri> guard, string? message = null)
    {
        var uri = guard.Value;
        if (guard.IsActive && (uri is null || !uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotHttps, message);
        }

        return guard;
    }

    /// <summary>Fails when the URI is not an absolute <c>http</c> or <c>https</c> URI. A relative URI fails.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<Uri> IfNotHttpOrHttps(this Guard<Uri> guard, string? message = null)
    {
        var uri = guard.Value;
        if (guard.IsActive && (uri is null || !uri.IsAbsoluteUri || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotHttpOrHttps, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the URI points to the local machine: the host is <c>localhost</c>, a name
    /// ending in <c>.localhost</c>, or a loopback IP address (<c>127.0.0.0/8</c>, <c>::1</c>).
    /// A relative URI has no host, so it passes.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    /// <remarks>
    /// The check reads the URI only; it does not resolve host names, so a name that resolves
    /// to a loopback address passes. It is one layer of protection against server-side request
    /// forgery, not all of it.
    /// </remarks>
    public static Guard<Uri> IfLoopback(this Guard<Uri> guard, string? message = null)
    {
        var uri = guard.Value;
        if (guard.IsActive && (uri is null || IsLoopback(uri)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.Loopback, message);
        }

        return guard;
    }

    /// <summary>Fails when the URI is relative or its scheme is not one of <paramref name="schemes"/> (compared without case).</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="schemes">The allowed schemes, for example <c>"https"</c> and <c>"wss"</c>.</param>
    /// <returns>The guard.</returns>
    public static Guard<Uri> IfNotScheme(this Guard<Uri> guard, params string[] schemes) =>
        guard.IfNotScheme(schemes, message: null);

    /// <summary>Fails when the URI is relative or its scheme is not one of <paramref name="schemes"/> (compared without case).</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="schemes">The allowed schemes, for example <c>"https"</c> and <c>"wss"</c>.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Schemes}</c> (the allowed schemes, joined by commas).</param>
    /// <returns>The guard.</returns>
    public static Guard<Uri> IfNotScheme(this Guard<Uri> guard, string[] schemes, string? message)
    {
        Argument.NotNull(schemes);
        var uri = guard.Value;
        if (guard.IsActive && (uri is null || !uri.IsAbsoluteUri || !HasScheme(schemes, uri.Scheme)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotScheme, message, Placeholder.Schemes, string.Join(", ", schemes));
        }

        return guard;
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Fails when the URI is relative or its scheme is not one of <paramref name="schemes"/>
    /// (compared without case). With C# 13 or later the compiler picks this overload for a
    /// list of schemes and passes them on the stack. Available on .NET 8 and later.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="schemes">The allowed schemes.</param>
    /// <returns>The guard.</returns>
    public static Guard<Uri> IfNotScheme(this Guard<Uri> guard, params ReadOnlySpan<string> schemes) =>
        guard.IfNotScheme(schemes, message: null);

    /// <summary>Fails when the URI is relative or its scheme is not one of <paramref name="schemes"/> (compared without case). Available on .NET 8 and later.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="schemes">The allowed schemes.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Schemes}</c>.</param>
    /// <returns>The guard.</returns>
    public static Guard<Uri> IfNotScheme(this Guard<Uri> guard, scoped ReadOnlySpan<string> schemes, string? message)
    {
        var uri = guard.Value;
        if (guard.IsActive && (uri is null || !uri.IsAbsoluteUri || !HasScheme(schemes, uri.Scheme)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotScheme, message, Placeholder.Schemes, string.Join(", ", schemes.ToArray()));
        }

        return guard;
    }

    private static bool HasScheme(scoped ReadOnlySpan<string> schemes, string scheme)
    {
        foreach (var candidate in schemes)
        {
            if (string.Equals(candidate, scheme, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
#else
    private static bool HasScheme(string[] schemes, string scheme)
    {
        foreach (var candidate in schemes)
        {
            if (string.Equals(candidate, scheme, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
#endif

    private static bool IsLoopback(Uri uri)
    {
        if (!uri.IsAbsoluteUri)
        {
            return false;
        }

        if (uri.IsLoopback)
        {
            return true;
        }

        // Uri.IsLoopback is false for "LOCALHOST" or "LocalHost" on .NET 8 and earlier, so the
        // host name is checked here too, the same way on every runtime.
        return uri.HostNameType == UriHostNameType.Dns
            && (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase));
    }
}
