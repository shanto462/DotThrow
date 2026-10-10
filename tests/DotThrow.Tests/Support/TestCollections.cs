using Xunit;

namespace DotThrow.Tests.Support;

/// <summary>
/// Tests that change <see cref="MessageTemplates"/> (global state) run alone, after the
/// parallel tests, so every other test sees the default messages.
/// </summary>
[CollectionDefinition(nameof(MessageTemplates), DisableParallelization = true)]
public sealed class MessageTemplatesGroup
{
}
