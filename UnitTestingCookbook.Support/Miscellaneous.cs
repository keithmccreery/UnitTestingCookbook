namespace UnitTestingCookbook.Support;

/// <summary>
/// Miscellaneous
/// </summary>
public class Miscellaneous
{
    public Miscellaneous() { }

    internal Miscellaneous( string @default )
    {
        PrivateProperty = @default;
    }

    private string PrivateProperty { get; set; } = "private_property";
    private string privateField = "private_field";

    private bool PrivateMethod( string message )
    {
        System.Console.WriteLine( message + privateField );

        return true;
    }

    /// <summary>
    /// ThrowsAnExceptionWithInnerException
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public void ThrowsAnExceptionWithInnerException()
    {
        throw new InvalidOperationException( "original exception", new NullReferenceException( "inner exception" ) );
    }

    /// <summary>
    /// ThrowsAnExceptionWithInnerExceptionAsync
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task ThrowsAnExceptionWithInnerExceptionAsync( CancellationToken cancellationToken = default )
    {
        await Task.Delay( 1, cancellationToken );

        throw new InvalidOperationException( "original exception", new NullReferenceException( "inner exception" ) );
    }

    public event EventHandler? SomethingHappenedEvent;

    protected virtual void OnSomethingHappened( EventArgs e )
    {
        SomethingHappenedEvent?.Invoke( this, e );
    }

    public void RaiseDoSomethingHappened()
    {
        OnSomethingHappened( EventArgs.Empty );
    }

    public void SlowRunningMethod()
    {
        Thread.Sleep( 100 );
    }
}
