using System;
using DotThrow.Tests.Support;
using Xunit;

namespace DotThrow.Tests.Guards;

/// <summary>Review fix: IfNotXml can cap the document size for untrusted input.</summary>
public sealed class XmlLimitTests
{
    [Fact]
    public void A_document_over_the_limit_fails()
    {
        const string Xml = "<a><b>hello</b></a>";
        Check.Passes(Xml, g => g.IfNotXml(100));
        Check.Passes(Xml, g => g.IfNotXml(0));
        Check.Passes(Xml, g => g.IfNotXml(Xml.Length));
        Check.Fails<ArgumentException, string>(Xml, g => g.IfNotXml(Xml.Length - 1), ErrorCodes.NotXml, "'input' must be well-formed XML.");
        Check.Fails<ArgumentException, string>("<a>", g => g.IfNotXml(100), ErrorCodes.NotXml);
        Check.Fails<ArgumentException, string>("<!DOCTYPE a><a/>", g => g.IfNotXml(100), ErrorCodes.NotXml);
        Check.Fails<ArgumentNullException, string>(null!, g => g.IfNotXml(100), ErrorCodes.Null);
        Assert.Throws<ArgumentOutOfRangeException>(() => Xml.Throw().IfNotXml(-1));
    }

    [Fact]
    public void A_large_document_is_rejected_without_parsing_it()
    {
        var large = "<a>" + new string('x', 1_000_000) + "</a>";
#if NET8_0_OR_GREATER
        var before = GC.GetAllocatedBytesForCurrentThread();
        Check.Fails<ArgumentException, string>(large, g => g.IfNotXml(1000), ErrorCodes.NotXml);
        Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 100_000);
#else
        // .NET Framework cannot count the bytes a thread allocates; check the result only.
        Check.Fails<ArgumentException, string>(large, g => g.IfNotXml(1000), ErrorCodes.NotXml);
#endif
    }
}
