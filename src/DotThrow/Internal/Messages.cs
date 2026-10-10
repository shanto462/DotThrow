namespace DotThrow.Internal;

// Builds the message of a failed check. Called only after a check failed, so the happy
// path never touches templates or formats a string.
internal static class Messages
{
    // custom: the message the caller passed, or null for the template of the error code.
    // key: the template key ("Code" or "Code.Variant"), or null for the error code itself.
    public static string Render<TValue, TArgs>(string? custom, string code, string? key, string name, TValue value, TArgs args)
        where TArgs : struct, IMessageArgs
    {
        if (custom is not null)
        {
            // A message without braces cannot have placeholders, so it is used as is.
            return custom.IndexOf('{') < 0 ? custom : MessageTemplate.ForCustomMessage(custom).Render(name, value, args);
        }

        return MessageTemplates.Resolve(code, key ?? code).Render(name, value, args);
    }
}
