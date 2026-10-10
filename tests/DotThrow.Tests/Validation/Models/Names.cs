using System.Collections.Generic;
using System.Linq;

namespace DotThrow.Tests.Validation.Models;

public static class Names
{
    public static string[] Of(ValidationReport report) => [.. report.Failures.Select(f => f.Name)];

    public static string[] Codes(ValidationReport report) => [.. report.Failures.Select(f => f.ErrorCode)];

    public static string[] Messages(ValidationReport report) => [.. report.Failures.Select(f => f.Message)];

    public static string[] Of(IEnumerable<ValidationFailure> failures) => [.. failures.Select(f => f.Name)];
}
