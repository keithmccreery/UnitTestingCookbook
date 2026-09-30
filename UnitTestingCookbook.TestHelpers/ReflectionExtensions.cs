using System.Reflection;

namespace UnitTestingCookbook.TestHelpers;

/// <summary>
/// ReflectionExtensions
/// </summary>
public static class ReflectionExtensions
{
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

            return (T?) (@this
                .GetType()
                .GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
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

            return (T?) (@this
                .GetType()
                .GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
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

            return (T?) (@this
                .GetType()
                .GetMethod(
                    methodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy,
                    args.Select(p => p.GetType()).ToArray())
                ?? throw new MissingMethodException(@this.GetType().Name, methodName))
                .Invoke(@this, args);
        }
    }
}
