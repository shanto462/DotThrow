using System;
using System.Text;

namespace DotThrow.Internal;

internal static class TypeNames
{
    // "Validator`1" becomes "Validator<User>".
    public static string Display(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.Name;
        }

        var name = type.Name;
        var tick = name.IndexOf('`');
        var builder = new StringBuilder(tick < 0 ? name : name.Substring(0, tick)).Append('<');
        var arguments = type.GetGenericArguments();
        for (var i = 0; i < arguments.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(Display(arguments[i]));
        }

        return builder.Append('>').ToString();
    }
}
