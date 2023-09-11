namespace UnitTestingCookbook.TestHelpers;

/// <summary>
/// ManageCaptureConsole
/// </summary>
public class ManageCaptureConsole : IDisposable
{
    private StringWriter? _capturedConsole;
    private readonly TextWriter _originalOutput;

    public ManageCaptureConsole()
    {
        _originalOutput = System.Console.Out;

        _capturedConsole = new StringWriter();
        System.Console.SetOut( _capturedConsole );
    }

    #region IDisposable

    private bool isDisposed;

    protected virtual void Dispose( bool disposing )
    {
        if ( !isDisposed )
        {
            System.Console.SetOut( _originalOutput );

            if ( disposing )
            {
                _capturedConsole?.Dispose();
                _capturedConsole = null;
            }

            isDisposed = true;
        }
    }

    ~ManageCaptureConsole()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose( false );
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose( true );
        GC.SuppressFinalize( this );
    }

    #endregion

    /// <summary>
    /// ToString
    /// </summary>
    /// <returns></returns>
    public override string? ToString()
    {
        return _capturedConsole?.ToString();
    }

}
