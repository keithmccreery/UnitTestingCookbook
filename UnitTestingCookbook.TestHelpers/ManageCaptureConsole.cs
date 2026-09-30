namespace UnitTestingCookbook.TestHelpers;

/// <summary>
/// Redirects <see cref="Console.Out"/> to an in-memory buffer for the lifetime of this instance, so a test can
/// assert on what a piece of code wrote to the console. Restores the original <see cref="Console.Out"/> on
/// <see cref="Dispose()"/>.
/// </summary>
public class ManageCaptureConsole : IDisposable
{
    private StringWriter? _capturedConsole;
    private readonly TextWriter _originalOutput;

    public ManageCaptureConsole()
    {
        _originalOutput = System.Console.Out;

        _capturedConsole = new StringWriter();
        System.Console.SetOut(_capturedConsole);
    }

    #region IDisposable

    private bool isDisposed;

    protected virtual void Dispose(bool disposing)
    {
        if (!isDisposed)
        {
            System.Console.SetOut(_originalOutput);

            if (disposing)
            {
                _capturedConsole?.Dispose();
                _capturedConsole = null;
            }

            isDisposed = true;
        }
    }

    // Not strictly necessary - this class wraps only managed resources - but deliberately kept as a defensive
    // safety net: if a test forgets to Dispose() this instance, the finalizer still restores the real
    // Console.Out on the next GC, rather than leaving Console output silently redirected into later,
    // unrelated tests.
    ~ManageCaptureConsole()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(false);
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion

    /// <summary>
    /// Returns everything written to the console while this instance has been active.
    /// </summary>
    /// <returns>The captured console output.</returns>
    public override string? ToString()
    {
        return _capturedConsole?.ToString();
    }

}
