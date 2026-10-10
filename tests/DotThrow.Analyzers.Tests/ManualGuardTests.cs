using System.Threading.Tasks;
using DotThrow.Analyzers.CodeFixes;
using DotThrow.Analyzers.Tests.Infrastructure;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace DotThrow.Analyzers.Tests;

public sealed class ManualGuardTests
{
    private const string Types = """

        enum Color { Red, Green }

        [System.Flags]
        enum Access { Read = 1, Write = 2 }
        """;

    [Fact]
    public Task DT0002_reports_guards_that_a_check_replaces() =>
        Verify.AnalyzerAsync<ManualGuardAnalyzer>(
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using DotThrow;

            class C
            {
                private readonly string _name = "";

                int Count { get; }

                void M(string name, string? text, int count, double ratio, long size, decimal price, List<int> items,
                    int[] values, Guid id, Color color, int? maybe, object o, int min, int max)
                {
                    {|DT0002:if (name is null)|} throw new ArgumentNullException(nameof(name));
                    {|DT0002:if (name == null)|} { throw new ArgumentNullException(nameof(name)); }
                    {|DT0002:if (null == o)|} throw new ArgumentNullException("o");
                    {|DT0002:if (ReferenceEquals(o, null))|} throw new ArgumentException("Required.", nameof(o));
                    {|DT0002:if (maybe is null)|} throw new ArgumentNullException(nameof(maybe));
                    {|DT0002:if (_name is null)|} throw new ArgumentNullException();
                    {|DT0002:if (string.IsNullOrEmpty(text))|} throw new ArgumentException("Text is required.", nameof(text));
                    {|DT0002:if (string.IsNullOrWhiteSpace(name))|} throw new ArgumentException(null, nameof(name));
                    {|DT0002:if (items.Count == 0)|} throw new ArgumentException("No items.", nameof(items));
                    {|DT0002:if (values.Length < 1)|} throw new ArgumentException("No values.", nameof(values));
                    {|DT0002:if (!items.Any())|} throw new ArgumentException("No items.", nameof(items));
                    {|DT0002:if (id == Guid.Empty)|} throw new ArgumentException("Empty.", nameof(id));
                    {|DT0002:if (!Enum.IsDefined(typeof(Color), color))|} throw new ArgumentOutOfRangeException(nameof(color));
                    {|DT0002:if (count < 0)|} throw new ArgumentOutOfRangeException(nameof(count));
                    {|DT0002:if (0 >= count)|} throw new ArgumentOutOfRangeException(nameof(count));
                    {|DT0002:if (count > 100)|} throw new ArgumentOutOfRangeException(nameof(count), count, "Too many.");
                    {|DT0002:if (size == 0)|} throw new ArgumentOutOfRangeException(nameof(size));
                    {|DT0002:if (price <= 0m)|} throw new ArgumentException("Free?", nameof(price));
                    {|DT0002:if (count < 1 || count > 10)|} throw new ArgumentOutOfRangeException(nameof(count));
                    {|DT0002:if (count <= min || count >= max)|} throw new ArgumentOutOfRangeException(nameof(count));
                    {|DT0002:if (max < count)|} throw new ArgumentOutOfRangeException(nameof(count));
                    {|DT0002:if (Count > max)|} throw new ArgumentOutOfRangeException(nameof(Count));
                }
            }
            """ + Types);

    [Fact]
    public Task DT0002_ignores_guards_that_would_change_behavior() =>
        Verify.AnalyzerAsync<ManualGuardAnalyzer>(
            """
            using System;
            using DotThrow;

            class C
            {
                void M(string name, int count, int other, Access access, int? maybe, Func<int> limit, Color color)
                {
                    // Not an argument exception, or more than a throw.
                    if (name is null) throw new InvalidOperationException();
                    if (name is null) { Console.WriteLine(); throw new ArgumentNullException(nameof(name)); }
                    if (name is null) throw new ArgumentNullException(nameof(name)); else Console.WriteLine();
                    if (name is null) throw new ArgumentNullException("Missing.", new Exception());
                    if (name is null) throw new ArgumentNullException(nameof(name)) { Source = "x" };
                    if (count < 0) return;

                    // DotThrow would throw an exception that callers do not catch.
                    if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
                    if (count < 0) throw new ArgumentNullException(nameof(count));

                    // Different meaning: [Flags] enums, lifted comparisons, reversed ranges, two values.
                    if (!Enum.IsDefined(typeof(Access), access)) throw new ArgumentOutOfRangeException(nameof(access));
                    if (maybe < 0) throw new ArgumentOutOfRangeException(nameof(maybe));
                    if (count < 10 || count > 5) throw new ArgumentOutOfRangeException(nameof(count));
                    if (count < 0 || other > 10) throw new ArgumentOutOfRangeException(nameof(count));
                    if (count < 1 || count >= 10) throw new ArgumentOutOfRangeException(nameof(count));
                    if (!Enum.IsDefined(typeof(Access), color)) throw new ArgumentOutOfRangeException(nameof(color));

                    // The value or the limit is computed.
                    if (Compute() < 0) throw new ArgumentOutOfRangeException();
                    if (count < limit()) throw new ArgumentOutOfRangeException(nameof(count));
                    if (count < 1 || count > limit()) throw new ArgumentOutOfRangeException(nameof(count));
                }

                static int Compute() => 1;
            }
            """ + Types);

    [Fact]
    public Task DT0002_reports_the_replacement() =>
        Verify.AnalyzerAsync<ManualGuardAnalyzer>(
            """
            using System;

            class C
            {
                void M(int count)
                {
                    if (count < 1 || count > 10) throw new ArgumentOutOfRangeException(nameof(count));
                }
            }
            """,
            Verify.Diagnostic<ManualGuardAnalyzer>("DT0002").WithSpan(7, 9, 7, 37).WithArguments("count.Throw().IfOutOfRange(1, 10)"));

    [Fact]
    public Task DT0002_fix_rewrites_guards_and_adds_the_using() =>
        Verify.CodeFixAsync<ManualGuardAnalyzer, ManualGuardCodeFix>(
            """
            // File header.
            using System;

            namespace App;

            class C
            {
                C(string name, int count, string? input, decimal price)
                {
                    // Required.
                    {|DT0002:if (name is null)|}
                    {
                        throw new ArgumentNullException(nameof(name));
                    }

                    {|DT0002:if (count < 1 || count > 10)|} throw new ArgumentOutOfRangeException(nameof(count), "Count must be from 1 to 10.");
                    {|DT0002:if (string.IsNullOrWhiteSpace(input))|} throw new ArgumentException("Input is required.", "data");
                    {|DT0002:if (price < 0)|} throw new ArgumentOutOfRangeException(nameof(price), price, null);
                }
            }
            """,
            """
            // File header.
            using System;
            using DotThrow;

            namespace App;

            class C
            {
                C(string name, int count, string? input, decimal price)
                {
                    // Required.
                    name.Throw().IfNull();

                    count.Throw().IfOutOfRange(1, 10, "Count must be from 1 to 10.");
                    input = input.Throw("data").IfNullOrWhiteSpace("Input is required.");
                    price.Throw().IfNegative();
                }
            }
            """);

    [Fact]
    public Task DT0002_fix_adds_the_first_using_after_the_header() =>
        Verify.CodeFixAsync<ManualGuardAnalyzer, ManualGuardCodeFix>(
            """
            // File header.

            class C
            {
                void M(object value)
                {
                    {|DT0002:if (value == null)|} throw new System.ArgumentNullException(nameof(value));
                }
            }
            """,
            """
            // File header.

            using DotThrow;

            class C
            {
                void M(object value)
                {
                    value.Throw().IfNull();
                }
            }
            """);

    [Fact]
    public Task DT0002_fix_needs_no_using_inside_the_DotThrow_namespace() =>
        Verify.CodeFixAsync<ManualGuardAnalyzer, ManualGuardCodeFix>(
            """
            namespace DotThrow.App
            {
                using System;

                class C
                {
                    void M(Guid id)
                    {
                        {|DT0002:if (id == Guid.Empty)|} throw new ArgumentException("The id is empty.", nameof(id));
                    }
                }
            }
            """,
            """
            namespace DotThrow.App
            {
                using System;

                class C
                {
                    void M(Guid id)
                    {
                        id.Throw().IfEmpty("The id is empty.");
                    }
                }
            }
            """);

    [Fact]
    public Task DT0002_fix_adds_the_using_inside_the_namespace_when_usings_are_there() =>
        Verify.CodeFixAsync<ManualGuardAnalyzer, ManualGuardCodeFix>(
            """
            namespace App
            {
                using System;

                class C
                {
                    void M(Color color)
                    {
                        {|DT0002:if (!Enum.IsDefined(typeof(Color), color))|} throw new ArgumentOutOfRangeException(nameof(color));
                    }
                }

                enum Color { Red }
            }
            """,
            """
            namespace App
            {
                using System;
                using DotThrow;

                class C
                {
                    void M(Color color)
                    {
                        color.Throw().IfNotDefined();
                    }
                }

                enum Color { Red }
            }
            """);

    [Fact]
    public Task DT0002_fix_passes_the_name_before_CSharp_10() =>
        Verify.CodeFixAsync(() => new DotThrowCodeFixTest<ManualGuardAnalyzer, ManualGuardCodeFix>
        {
            LanguageVersion = LanguageVersion.CSharp9,
            TestCode = """
                using System;
                using DotThrow;

                class C
                {
                    void M(string name, object value)
                    {
                        {|DT0002:if (name is null)|} throw new ArgumentNullException(nameof(name));
                        {|DT0002:if (value is null)|} throw new ArgumentNullException();
                    }
                }
                """,
            FixedCode = """
                using System;
                using DotThrow;

                class C
                {
                    void M(string name, object value)
                    {
                        name.Throw(nameof(name)).IfNull();
                        value.Throw("value").IfNull();
                    }
                }
                """,
        });

    [Fact]
    public Task DT0002_fix_handles_collections_and_else_if() =>
        Verify.CodeFixAsync<ManualGuardAnalyzer, ManualGuardCodeFix>(
            """
            using System;
            using System.Collections.Generic;
            using DotThrow;

            class C
            {
                void M(List<int> items, bool strict, int count)
                {
                    {|DT0002:if (items.Count == 0)|} throw new ArgumentException("No items.", nameof(items));

                    if (strict)
                    {
                        Console.WriteLine();
                    }
                    else {|DT0002:if (count > 5)|} throw new ArgumentOutOfRangeException(nameof(count));
                }
            }
            """,
            """
            using System;
            using System.Collections.Generic;
            using DotThrow;

            class C
            {
                void M(List<int> items, bool strict, int count)
                {
                    items.Throw().IfNullOrEmpty("No items.");

                    if (strict)
                    {
                        Console.WriteLine();
                    }
                    else count.Throw().IfGreaterThan(5);
                }
            }
            """);

    [Fact]
    public Task DT0002_ignores_guards_whose_rewrite_would_not_compile_or_differ() =>
        Verify.AnalyzerAsync<ManualGuardAnalyzer>(
            """
            using System;
            using DotThrow;

            class C
            {
                private string? _optional;

                void M(byte b, uint u, short s, double d, float f, int x)
                {
                    // T would not be inferred from Guard<byte> and an int limit.
                    if (b > 200) throw new ArgumentOutOfRangeException(nameof(b));
                    if (u < 5) throw new ArgumentOutOfRangeException(nameof(u));
                    if (s < 1 || s > 10) throw new ArgumentOutOfRangeException(nameof(s));
                    {|DT0002:if (b == 0)|} throw new ArgumentOutOfRangeException(nameof(b));

                    // DotThrow's ordering checks reject NaN; x == 0 and IfZero both let it through.
                    if (d < 0) throw new ArgumentOutOfRangeException(nameof(d));
                    if (f < 0 || f > 1) throw new ArgumentOutOfRangeException(nameof(f));
                    {|DT0002:if (d == 0)|} throw new ArgumentOutOfRangeException(nameof(d));

                    // A computed message would run on every call.
                    if (x < 0) throw new ArgumentOutOfRangeException(nameof(x), $"was {x}");
                    if (x == 0) throw new ArgumentOutOfRangeException(nameof(x), Describe(x));

                    // A nullable field cannot be assigned back, so nullable analysis would warn later.
                    if (_optional is null) throw new ArgumentNullException(nameof(_optional));
                }

                static string Describe(int value) => value.ToString();
            }
            """);

    [Fact]
    public Task DT0002_fix_keeps_nullable_analysis_happy() =>
        Verify.CodeFixAsync(() => new DotThrowCodeFixTest<ManualGuardAnalyzer, ManualGuardCodeFix>
        {
            // Warnings count too: the fixed code must not add CS8602 or CS8629.
            CompilerDiagnostics = CompilerDiagnostics.Warnings,
            TestCode = """
                using System;
                using DotThrow;

                class C
                {
                    int M(string? text, int? count)
                    {
                        {|DT0002:if (text is null)|} throw new ArgumentNullException(nameof(text));
                        {|DT0002:if (count == null)|} throw new ArgumentNullException(nameof(count));
                        var other = Read();
                        {|DT0002:if (string.IsNullOrEmpty(other))|} throw new ArgumentException("Empty.", nameof(other));
                        return text.Length + count.Value + other.Length;
                    }

                    static string? Read() => null;
                }
                """,
            FixedCode = """
                using System;
                using DotThrow;

                class C
                {
                    int M(string? text, int? count)
                    {
                        text = text.Throw().IfNull();
                        count = count.Throw().IfNull();
                        var other = Read();
                        other = other.Throw().IfNullOrEmpty("Empty.");
                        return text.Length + count.Value + other.Length;
                    }

                    static string? Read() => null;
                }
                """,
        });

    [Fact]
    public Task DT0002_reports_and_fixes_coalesce_throw() =>
        Verify.CodeFixAsync<ManualGuardAnalyzer, ManualGuardCodeFix>(
            """
            using System;

            class C
            {
                private readonly string _name;

                C(string name, string? title, object? tag)
                {
                    _name = {|DT0002:name ?? throw new ArgumentNullException(nameof(name))|};
                    Title = {|DT0002:title ?? throw new ArgumentNullException(nameof(title), "Title is required.")|};
                    string local = {|DT0002:name ?? throw new ArgumentNullException(nameof(name))|};
                    Use({|DT0002:tag ?? throw new ArgumentNullException(nameof(tag))|});

                    // The guard would not convert back to the value here.
                    var inferred = name ?? throw new ArgumentNullException(nameof(name));
                    Generic(name ?? throw new ArgumentNullException(nameof(name)));
                    _ = name ?? throw new ArgumentNullException(nameof(name));
                    var length = (name ?? throw new ArgumentNullException(nameof(name))).Length;
                    object other = tag ?? throw new InvalidOperationException();
                }

                string Title { get; }

                static string Pick(string? value) => {|DT0002:value ?? throw new ArgumentNullException(nameof(value))|};

                static void Use(object value) { }

                static void Generic<T>(T value) { }
            }
            """,
            """
            using System;
            using DotThrow;

            class C
            {
                private readonly string _name;

                C(string name, string? title, object? tag)
                {
                    _name = name.Throw().IfNull();
                    Title = title.Throw().IfNull("Title is required.");
                    string local = name.Throw().IfNull();
                    Use(tag.Throw().IfNull());

                    // The guard would not convert back to the value here.
                    var inferred = name ?? throw new ArgumentNullException(nameof(name));
                    Generic(name ?? throw new ArgumentNullException(nameof(name)));
                    _ = name ?? throw new ArgumentNullException(nameof(name));
                    var length = (name ?? throw new ArgumentNullException(nameof(name))).Length;
                    object other = tag ?? throw new InvalidOperationException();
                }

                string Title { get; }

                static string Pick(string? value) => value.Throw().IfNull();

                static void Use(object value) { }

                static void Generic<T>(T value) { }
            }
            """);
}
