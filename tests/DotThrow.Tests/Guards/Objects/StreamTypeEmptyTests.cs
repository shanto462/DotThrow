using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using DotThrow.Tests.Support;
using Xunit;

namespace DotThrow.Tests.Guards;

public sealed class StreamTypeEmptyTests
{
    [Fact]
    public void Stream_checks_work_on_derived_types()
    {
        using var memory = new MemoryStream([1, 2]);
        Check.Passes(memory, g => g.IfNotReadable().IfNotWritable().IfNotSeekable().IfEmpty());
        Stream stream = memory;
        Check.Passes(stream, g => g.IfNotReadable().IfEmpty());

        using var readOnly = new MemoryStream([1], writable: false);
        Check.Fails<ArgumentException, MemoryStream>(readOnly, g => g.IfNotWritable(), ErrorCodes.NotWritable, "'input' must be writable.");

        var closed = new MemoryStream();
        closed.Dispose();
        Check.Fails<ArgumentException, MemoryStream>(closed, g => g.IfNotReadable(), ErrorCodes.NotReadable, "'input' must be readable.");
        Check.Fails<ArgumentException, MemoryStream>(closed, g => g.IfNotSeekable(), ErrorCodes.NotSeekable, "'input' must be seekable.");
        Check.Fails<ArgumentNullException, Stream>(null!, g => g.IfNotReadable(), ErrorCodes.Null);
    }

    [Fact]
    public void IfEmpty_on_streams_needs_a_seekable_stream_of_length_zero()
    {
        using var empty = new MemoryStream();
        Check.Fails<ArgumentException, MemoryStream>(empty, g => g.IfEmpty(), ErrorCodes.Empty, "'input' must not be empty.");
        Check.Fails<ArgumentException, Stream>(empty, g => g.IfEmpty(), ErrorCodes.Empty);
        Check.Passes<Stream>(new NonSeekable(), g => g.IfEmpty());
    }

    [Fact]
    public void Type_checks()
    {
        Check.Passes(typeof(List<int>), g => g.IfAbstract().IfInterface().IfNotAssignableTo(typeof(IEnumerable<int>)));
        Check.Fails<ArgumentException, Type>(typeof(Stream), g => g.IfAbstract(), ErrorCodes.Abstract, "'input' must not be an abstract type.");
        Check.Fails<ArgumentException, Type>(typeof(IDisposable), g => g.IfAbstract(), ErrorCodes.Abstract);
        Check.Fails<ArgumentException, Type>(typeof(Math), g => g.IfAbstract(), ErrorCodes.Abstract);
        Check.Fails<ArgumentException, Type>(typeof(IDisposable), g => g.IfInterface(), ErrorCodes.Interface, "'input' must not be an interface.");
        Check.Fails<ArgumentException, Type>(typeof(string), g => g.IfNotAssignableTo(typeof(IDisposable)), ErrorCodes.NotAssignableTo, "'input' must be assignable to IDisposable.");
        Check.Fails<ArgumentException, Type>(typeof(List<>), g => g.IfNotAssignableTo(typeof(IList<int>)), ErrorCodes.NotAssignableTo);
        Check.Fails<ArgumentNullException, Type>(null!, g => g.IfInterface(), ErrorCodes.Null);
        Assert.Throws<ArgumentNullException>(() => typeof(int).Throw().IfNotAssignableTo(null!));
    }

    [Fact]
    public void IfEmpty_follows_FluentValidation_NotEmpty()
    {
        Check.Fails<ArgumentException, string>("", g => g.IfEmpty(), ErrorCodes.Empty, "'input' must not be empty.");
        Check.Fails<ArgumentException, string>(" \t", g => g.IfEmpty(), ErrorCodes.Empty);
        Check.Fails<ArgumentNullException, string>(null!, g => g.IfEmpty(), ErrorCodes.Null);
        Check.Fails<ArgumentException, int>(0, g => g.IfEmpty(), ErrorCodes.Empty);
        Check.Fails<ArgumentException, DateTime>(default, g => g.IfEmpty(), ErrorCodes.Empty);
        Check.Fails<ArgumentException, List<int>>([], g => g.IfEmpty(), ErrorCodes.Empty);
        Check.Fails<ArgumentException, int[]>([], g => g.IfEmpty(), ErrorCodes.Empty);
        Check.Fails<ArgumentException, IEnumerable<int>>(Enumerable.Empty<int>().Select(i => i), g => g.IfEmpty(), ErrorCodes.Empty);
        Check.Fails<ArgumentException, ImmutableArray<int>>(ImmutableArray<int>.Empty, g => g.IfEmpty(), ErrorCodes.Empty);
        Check.Fails<ArgumentException, ImmutableArray<int>>(default, g => g.IfEmpty(), ErrorCodes.Empty);
        Check.Fails<ArgumentNullException, int?>(null, g => g.IfEmpty(), ErrorCodes.Null);

        Check.Passes("a", g => g.IfEmpty());
        Check.Passes(1, g => g.IfEmpty());
        Check.Passes<int?>(0, g => g.IfEmpty());
        Check.Passes(new List<int> { 0 }, g => g.IfEmpty());
        Check.Passes<object>(new object(), g => g.IfEmpty());
        Check.Passes(ImmutableArray.Create(1), g => g.IfEmpty());
        Check.Passes<IEnumerable>(new ArrayList { 1 }, g => g.IfEmpty());
    }

    [Fact]
    public void IfNotEmpty_follows_FluentValidation_Empty()
    {
        Check.Passes<string>(null!, g => g.IfNotEmpty());
        Check.Passes(" ", g => g.IfNotEmpty());
        Check.Passes(0, g => g.IfNotEmpty());
        Check.Passes(new List<int>(), g => g.IfNotEmpty());
        Check.Fails<ArgumentException, string>("a", g => g.IfNotEmpty(), ErrorCodes.NotEmpty, "'input' must be empty.");
        Check.Fails<ArgumentException, int>(3, g => g.IfNotEmpty(), ErrorCodes.NotEmpty);
        Check.Fails<ArgumentException, int[]>([1], g => g.IfNotEmpty(), ErrorCodes.NotEmpty);
    }

    [Fact]
    public void IfEmpty_on_Guid_keeps_its_own_message() =>
        Check.Fails<ArgumentException, Guid>(Guid.Empty, g => g.IfEmpty(), ErrorCodes.Empty, "'input' must not be an empty GUID.");

    private sealed class NonSeekable : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => 0;

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
