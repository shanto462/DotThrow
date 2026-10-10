using System;
using DotThrow.Internal;

namespace DotThrow;

// Checks for Type values, for example a plugin type read from configuration. A null Type
// fails with the "Null" code. They read only the type's own flags, so they are trimming and
// AOT safe.
public static partial class GuardExtensions
{
    /// <summary>
    /// Fails when a value of the type cannot be assigned to <paramref name="targetType"/>
    /// (<see cref="Type.IsAssignableFrom(Type)"/>), for example a type that does not implement
    /// a required interface. An open generic type such as <c>typeof(List&lt;&gt;)</c> is not
    /// assignable to a closed one.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="targetType">The type that the value must be assignable to.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Type}</c> (the target type name).</param>
    /// <returns>The guard.</returns>
    public static Guard<Type> IfNotAssignableTo(this Guard<Type> guard, Type targetType, string? message = null)
    {
        Argument.NotNull(targetType);
        var type = guard.Value;
        if (guard.IsActive && (type is null || !targetType.IsAssignableFrom(type)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotAssignableTo, message, Placeholder.Type, targetType.Name);
        }

        return guard;
    }

    /// <summary>Fails when the type is abstract. Interfaces and static classes are abstract too.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<Type> IfAbstract(this Guard<Type> guard, string? message = null)
    {
        var type = guard.Value;
        if (guard.IsActive && (type is null || type.IsAbstract))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.Abstract, message);
        }

        return guard;
    }

    /// <summary>Fails when the type is an interface.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<Type> IfInterface(this Guard<Type> guard, string? message = null)
    {
        var type = guard.Value;
        if (guard.IsActive && (type is null || type.IsInterface))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.Interface, message);
        }

        return guard;
    }
}
