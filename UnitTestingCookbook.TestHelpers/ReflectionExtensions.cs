using System.Reflection;

namespace UnitTestingCookbook.TestHelpers;

/// <summary>
/// ReflectionExtensions
/// </summary>
public static class ReflectionExtensions
{
    private const BindingFlags MEMBER_BINDING_FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    extension(object @this)
    {
        /// <summary>
        /// Get Property Value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        /// <exception cref="MissingMemberException"></exception>
        public T? GetPropertyValue<T>(string propertyName)
        {
            ArgumentNullException.ThrowIfNull(@this);

            return (T?) (FindMember(@this.GetType(), t => t.GetProperty(propertyName, MEMBER_BINDING_FLAGS))
                ?? throw new MissingMemberException(@this.GetType().Name, propertyName))
                .GetValue(@this, null);
        }

        /// <summary>
        /// Get Field Value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        /// <exception cref="MissingFieldException"></exception>
        public T? GetFieldValue<T>(string fieldName)
        {
            ArgumentNullException.ThrowIfNull(@this);

            return (T?) (FindMember(@this.GetType(), t => t.GetField(fieldName, MEMBER_BINDING_FLAGS))
                ?? throw new MissingFieldException(@this.GetType().Name, fieldName))
                .GetValue(@this);
        }

        /// <summary>
        /// Execute Method
        /// </summary>
        /// <remarks>
        /// DOES NOT HANDLE argument values as null - unable to .GetType() on null
        /// TO DO: Implement .GetMethodExt() from https://stackoverflow.com/questions/4035719/getmethod-for-generic-method
        /// </remarks>
        /// <typeparam name="T"></typeparam>
        /// <param name="methodName"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        /// <exception cref="MissingMethodException"></exception>
        public T? ExecuteMethod<T>(string methodName, params object[] args)
        {
            ArgumentNullException.ThrowIfNull(@this);

            return (T?) (FindMember(@this.GetType(), t => t.GetMethod(methodName, MEMBER_BINDING_FLAGS, args.Select(p => p.GetType()).ToArray()))
                ?? throw new MissingMethodException(@this.GetType().Name, methodName))
                .Invoke(@this, args);
        }
    }

    // BindingFlags.FlattenHierarchy only reaches public/protected *static* members up the hierarchy - it does
    // not find private instance members declared on a base class (e.g. HttpClient's private _handler field,
    // declared on its base type HttpMessageInvoker) - confirmed empirically. Walk BaseType ourselves instead.
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
