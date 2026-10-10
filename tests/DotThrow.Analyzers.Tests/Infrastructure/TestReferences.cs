using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace DotThrow.Analyzers.Tests.Infrastructure;

/// <summary>
/// Metadata references for the test code, all on disk already, so nothing is downloaded:
/// the assemblies of the running .NET with the net10.0 build of DotThrow, or the
/// NETStandard.Library reference assemblies with the netstandard2.0 build of DotThrow.
/// </summary>
internal static class TestReferences
{
    private static readonly Lazy<ImmutableArray<MetadataReference>> LazyFramework = new(CreateFramework);
    private static readonly Lazy<ImmutableArray<MetadataReference>> LazyNet = new(() =>
        LazyFramework.Value.Add(MetadataReference.CreateFromFile(typeof(global::DotThrow.ValidationReport).Assembly.Location)));
    private static readonly Lazy<ImmutableArray<MetadataReference>> LazyNetStandard20 = new(CreateNetStandard20);

    /// <summary>Gets the references of a .NET 10 project without DotThrow.</summary>
    public static ImmutableArray<MetadataReference> Framework => LazyFramework.Value;

    /// <summary>Gets the references of a .NET 10 project that uses DotThrow.</summary>
    public static ImmutableArray<MetadataReference> Net => LazyNet.Value;

    /// <summary>Gets the references of a netstandard2.0 project that uses DotThrow.</summary>
    public static ImmutableArray<MetadataReference> NetStandard20 => LazyNetStandard20.Value;

    /// <summary>Gets the root of the repository, set at build time.</summary>
    public static string RepositoryRoot => Metadata("RepositoryRoot");

    private static ImmutableArray<MetadataReference> CreateFramework()
    {
        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var trusted = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        var framework = trusted
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetDirectoryName(path), runtimeDirectory, StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal);

        var references = new List<MetadataReference>();
        foreach (var path in framework)
        {
            references.Add(MetadataReference.CreateFromFile(path));
        }

        return [.. references];
    }

    private static ImmutableArray<MetadataReference> CreateNetStandard20()
    {
        var packages = Metadata("NuGetPackageRoot");
        var referenceDirectory = Path.Combine(packages, "netstandard.library", "2.0.3", "build", "netstandard2.0", "ref");
        var references = Directory.GetFiles(referenceDirectory, "*.dll")
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();

        var dotThrow = Path.Combine(AppContext.BaseDirectory, "netstandard2.0", "DotThrow.dll");
        references.Add(MetadataReference.CreateFromFile(dotThrow));
        return [.. references];
    }

    private static string Metadata(string key) =>
        typeof(TestReferences).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == key).Value
        ?? throw new InvalidOperationException("The build did not set " + key + ".");
}
