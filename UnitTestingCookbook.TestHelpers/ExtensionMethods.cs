using System.Reflection;

using Microsoft.Extensions.Logging;

using Moq;

namespace UnitTestingCookbook.TestHelpers;

public static class ExtensionMethods
{
    /// <summary>
    /// VerifyLogging
    /// </summary>
    /// <remarks>
    /// https://adamstorr.azurewebsites.net/blog/mocking-ilogger-with-moq
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    /// <param name="logger"></param>
    /// <param name="expectedMessage"></param>
    /// <param name="expectedLogLevel"></param>
    /// <param name="times"></param>
    /// <returns></returns>
    public static Mock<ILogger<T>> VerifyLogging<T>( this Mock<ILogger<T>> logger, string expectedMessage, LogLevel expectedLogLevel = LogLevel.Debug, Times? times = null )
    {
        times ??= Times.Once();

        Func<object, Type, bool> state = ( v, t ) => v.ToString()?.CompareTo( expectedMessage ) == 0;

        logger.Verify(
            x => x.Log(
                It.Is<LogLevel>( l => l == expectedLogLevel ),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>( ( v, t ) => state( v, t ) ),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>( ( v, t ) => true ) )
            , ( Times ) times );

        return logger;
    }

    /// <summary>
    /// Get Property Value
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="this"></param>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    /// <exception cref="MissingMemberException"></exception>
    public static T? GetPropertyValue<T>( this object @this, string propertyName )
    {
        ArgumentNullException.ThrowIfNull( @this );

        return ( T? ) ( @this
            .GetType()
            .GetProperty( propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy )
            ?? throw new MissingMemberException( @this.GetType().Name, propertyName ) )
            .GetValue( @this, null );
    }

    /// <summary>
    /// Get Field Value
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="this"></param>
    /// <param name="fieldName"></param>
    /// <returns></returns>
    /// <exception cref="MissingFieldException"></exception>
    public static T? GetFieldValue<T>( this object @this, string fieldName )
    {
        ArgumentNullException.ThrowIfNull( @this );

        return ( T? ) ( @this
            .GetType()
            .GetField( fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy )
            ?? throw new MissingFieldException( @this.GetType().Name, fieldName ) )
            .GetValue( @this );
    }

    /// <summary>
    /// Execute Method
    /// </summary>
    /// <remarks>
    /// DOES NOT HANDLE argument values as null - unable to .GetType() on null
    /// TO DO: Implement .GetMethodExt() from https://stackoverflow.com/questions/4035719/getmethod-for-generic-method
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    /// <param name="this"></param>
    /// <param name="methodName"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    /// <exception cref="MissingMethodException"></exception>
    public static T? ExecuteMethod<T>( this object @this, string methodName, params object[] args )
    {
        ArgumentNullException.ThrowIfNull( @this );

        return ( T? ) ( @this
            .GetType()
            .GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy,
                args.Select( p => p.GetType() ).ToArray() )
            ?? throw new MissingMethodException( @this.GetType().Name, methodName ) )
            .Invoke( @this, args );
    }
}
