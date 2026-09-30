using System.Reflection;

namespace UnitTestingCookbook.TestHelpers;

/// <summary>
/// Reflection-based helpers for reaching non-public members - reads/writes private and internal properties and
/// fields, and invokes private and internal methods - typically as a last resort for testing legacy code whose
/// seams can't easily be changed. Unlike naive reflection via <see cref="BindingFlags.FlattenHierarchy"/> alone,
/// these correctly reach private instance members declared on a base class (see <see cref="FindMember{TMember}"/>).
/// </summary>
public static class ReflectionExtensions
{
    private const BindingFlags MEMBER_BINDING_FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    extension(object @this)
    {
        /// <summary>
        /// Gets the value of a property (of any declared accessibility, instance or static) by name, searching
        /// the object's type and its base types.
        /// </summary>
        /// <typeparam name="T">The property's expected value type.</typeparam>
        /// <param name="propertyName">The property's name, as declared on the type (case-sensitive).</param>
        /// <returns>The property's current value, cast to <typeparamref name="T"/>.</returns>
        /// <exception cref="MissingMemberException">No property named <paramref name="propertyName"/> was found on the type or any of its base types.</exception>
        public T? GetPropertyValue<T>(string propertyName)
        {
            ArgumentNullException.ThrowIfNull(@this);

            return (T?) (FindMember(@this.GetType(), t => t.GetProperty(propertyName, MEMBER_BINDING_FLAGS))
                ?? throw new MissingMemberException(@this.GetType().Name, propertyName))
                .GetValue(@this, null);
        }

        /// <summary>
        /// Gets the value of a field (of any declared accessibility, instance or static) by name, searching the
        /// object's type and its base types.
        /// </summary>
        /// <typeparam name="T">The field's expected value type.</typeparam>
        /// <param name="fieldName">The field's name, as declared on the type (case-sensitive).</param>
        /// <returns>The field's current value, cast to <typeparamref name="T"/>.</returns>
        /// <exception cref="MissingFieldException">No field named <paramref name="fieldName"/> was found on the type or any of its base types.</exception>
        public T? GetFieldValue<T>(string fieldName)
        {
            ArgumentNullException.ThrowIfNull(@this);

            return (T?) (FindMember(@this.GetType(), t => t.GetField(fieldName, MEMBER_BINDING_FLAGS))
                ?? throw new MissingFieldException(@this.GetType().Name, fieldName))
                .GetValue(@this);
        }

        /// <summary>
        /// Invokes a method (of any declared accessibility, instance or static) by name, searching the object's
        /// type and its base types, matching overloads by the runtime types of <paramref name="args"/>.
        /// </summary>
        /// <remarks>
        /// DOES NOT HANDLE argument values as null - unable to .GetType() on null
        /// TO DO: Implement .GetMethodExt() from https://stackoverflow.com/questions/4035719/getmethod-for-generic-method
        /// </remarks>
        /// <typeparam name="T">The method's expected return type.</typeparam>
        /// <param name="methodName">The method's name, as declared on the type (case-sensitive).</param>
        /// <param name="args">
        /// The arguments to invoke the method with. Also used to resolve which overload to call by argument
        /// type - see the DOES NOT HANDLE note above for the one real limitation this creates.
        /// </param>
        /// <returns>The method's return value, cast to <typeparamref name="T"/>.</returns>
        /// <exception cref="MissingMethodException">No method named <paramref name="methodName"/> matching <paramref name="args"/>'s types was found on the type or any of its base types.</exception>
        public T? ExecuteMethod<T>(string methodName, params object[] args)
        {
            ArgumentNullException.ThrowIfNull(@this);

            return (T?) (FindMember(@this.GetType(), t => t.GetMethod(methodName, MEMBER_BINDING_FLAGS, args.Select(p => p.GetType()).ToArray()))
                ?? throw new MissingMethodException(@this.GetType().Name, methodName))
                .Invoke(@this, args);
        }
    }

    /// <summary>
    /// Walks <paramref name="type"/>'s <see cref="Type.BaseType"/> chain, returning the first non-null result
    /// <paramref name="lookup"/> produces. <see cref="BindingFlags.FlattenHierarchy"/> only reaches
    /// public/protected *static* members up the hierarchy - it does not find private instance members declared
    /// on a base class (e.g. <see cref="HttpClient"/>'s private <c>_handler</c> field, declared on its base type
    /// <c>HttpMessageInvoker</c>) - confirmed empirically. This walks the hierarchy explicitly instead.
    /// </summary>
    /// <typeparam name="TMember">The kind of member being looked up (property, field, or method).</typeparam>
    /// <param name="type">The type to start searching from.</param>
    /// <param name="lookup">A single-type lookup (e.g. <c>t => t.GetField(name, flags)</c>) to try at each level of the hierarchy.</param>
    /// <returns>The first match found, searching from <paramref name="type"/> up through its base types; <see langword="null"/> if none matched.</returns>
    private static TMember? FindMember<TMember>(Type? type, Func<Type, TMember?> lookup) where TMember : MemberInfo
    {
        for (; type is not null; type = type.BaseType)
        {
            TMember? member = lookup(type);
            if (member is not null)
                return member;
        }

        return null;
    }
}
