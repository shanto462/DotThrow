using System;

namespace DotThrow.Dynamic.Internal;

// A FormatException whose message DotThrow wrote. Messages of the runtime's own parsers differ
// between .NET Framework and .NET, so definition errors only repeat messages of this type.
internal sealed class TextFormatException : FormatException
{
    public TextFormatException(string message)
        : base(message)
    {
    }

    // Our text when DotThrow wrote it, nothing when the runtime did.
    public static string DetailOf(Exception exception) =>
        exception is TextFormatException ? $" ({exception.Message})" : string.Empty;
}
