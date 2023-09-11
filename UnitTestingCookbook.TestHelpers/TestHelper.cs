using System.Reflection;

namespace UnitTestingCookbook.TestHelpers;

/// <summary>
/// TestHelper
/// </summary>
public static class TestHelper
{
    /// <summary>
    /// Instantiate Internal Constructor
    /// </summary>
    /// <remarks>
    /// DOES NOT HANDLE argument values as null - unable to .GetType() on null
    /// TO DO: Implement .GetConstructorExt() by using the approach for .GetMethodExt() from https://stackoverflow.com/questions/4035719/getmethod-for-generic-method
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    /// <param name="args"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static T InstantiateInternalConstructor<T>( params object[] args )
    {
        return ( T ) ( typeof( T )
            .GetConstructor( BindingFlags.NonPublic | BindingFlags.Instance, null, args.Select( p => p.GetType() ).ToArray(), null )
            ?? throw new NotImplementedException( "No internal constructor matches the parameters." ) )
            .Invoke( args );
    }
}
