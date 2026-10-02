using System.Reflection;

namespace UnitTestingCookbook.TestHelpers;

/// <summary>
/// Helpers for instantiating a type via a constructor plain reflection can't normally reach.
/// </summary>
public static class TestHelper
{
    /// <summary>
    /// Creates an instance of <typeparamref name="T"/> via a non-public (private or internal) constructor
    /// matching <paramref name="args"/> by their runtime types - for testing a type that deliberately hides its
    /// constructor from normal callers.
    /// </summary>
    /// <remarks>
    /// DOES NOT HANDLE argument values as null - unable to .GetType() on null
    /// TO DO: Implement .GetConstructorExt() by using the approach for .GetMethodExt() from https://stackoverflow.com/questions/4035719/getmethod-for-generic-method
    /// </remarks>
    /// <typeparam name="T">The type to instantiate.</typeparam>
    /// <param name="args">
    /// The constructor arguments. Matched to an overload by each argument's runtime type - see the
    /// DOES NOT HANDLE note above for the one real limitation this creates.
    /// </param>
    /// <returns>The newly constructed instance.</returns>
    /// <exception cref="NotImplementedException">No non-public constructor matching <paramref name="args"/>'s types was found on <typeparamref name="T"/>.</exception>
    // begin-snippet: TestHelper_InstantiateInternalConstructor
    public static T InstantiateInternalConstructor<T>(params object[] args)
    {
        return (T) (typeof(T)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, args.Select(p => p.GetType()).ToArray(), null)
            ?? throw new NotImplementedException("No internal constructor matches the parameters."))
            .Invoke(args);
    }
    // end-snippet
}
