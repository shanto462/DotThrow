using System.Collections.Generic;
using System.Text;

namespace DotThrow.Testing;

internal static class TestMessages
{
    public static string Describe(string banner, string heading, IReadOnlyList<ValidationFailure> failures, bool useNames)
    {
        if (failures.Count == 0)
        {
            return banner;
        }

        var text = new StringBuilder(banner).Append('\n').Append("----").Append('\n').Append(heading).Append('\n');
        for (var i = 0; i < failures.Count; i++)
        {
            text.Append('[').Append(i).Append("]: ").Append(useNames ? failures[i].Name : failures[i].ToString()).Append('\n');
        }

        return text.ToString();
    }
}
