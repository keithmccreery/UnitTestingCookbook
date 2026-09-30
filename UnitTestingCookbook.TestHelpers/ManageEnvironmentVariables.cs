namespace UnitTestingCookbook.TestHelpers;

/// <summary>
/// Sets one or more environment variables for the lifetime of this instance, capturing each one's current
/// value first, and restores those captured values on <see cref="Dispose()"/>.
/// </summary>
/// <remarks>
/// Not safe for overlapping/concurrent use against the same variable name - see
/// <see href="../README/README_ParallelProcessing.md">Parallel Processing</see> for a worked example of exactly
/// how this breaks when two scopes touching the same variable are disposed out of nested (LIFO) order.
/// </remarks>
public class ManageEnvironmentVariables : IDisposable
{
    private readonly Dictionary<string, string?> _originalEnvironmentVariables = new Dictionary<string, string?>(StringComparer.Ordinal);

    /// <summary>
    /// Creates an instance that doesn't set anything yet - call <see cref="SetEnvironmentVariable"/> or
    /// <see cref="SetEnvironmentVariables"/> afterward.
    /// </summary>
    public ManageEnvironmentVariables() { }

    /// <summary>
    /// Creates an instance and immediately sets each of the given environment variables, capturing their
    /// current values first.
    /// </summary>
    /// <param name="environmentVariables">The environment variables to set, keyed by name.</param>
    public ManageEnvironmentVariables(Dictionary<string, string?> environmentVariables)
    {
        SetEnvironmentVariables(environmentVariables);
    }

    /// <summary>
    /// Sets each of the given environment variables, capturing their current values first so they can be
    /// restored on <see cref="Dispose()"/>.
    /// </summary>
    /// <param name="environmentVariables">The environment variables to set, keyed by name.</param>
    public void SetEnvironmentVariables(Dictionary<string, string?> environmentVariables)
    {
        environmentVariables
            .ToList()
            .ForEach(kvp => SetEnvironmentVariable(kvp.Key, kvp.Value));
    }

    /// <summary>
    /// Sets a single environment variable, capturing its current value first so it can be restored on
    /// <see cref="Dispose()"/>.
    /// </summary>
    /// <param name="key">The environment variable's name.</param>
    /// <param name="value">The value to set. <see langword="null"/> unsets the variable.</param>
    public void SetEnvironmentVariable(string key, string? value)
    {
        // overwrite if already present
        _originalEnvironmentVariables[key] = Environment.GetEnvironmentVariable(key);

        // set
        Environment.SetEnvironmentVariable(key, value);
    }

    private void RestoreEnvironmentVariables()
    {
        // restore
        _originalEnvironmentVariables
            ?.ToList()
            .ForEach(kvp => SetEnvironmentVariable(kvp.Key, kvp.Value));
    }

    #region IDisposable

    private bool isDisposed;

    protected virtual void Dispose(bool disposing)
    {
        if (!isDisposed)
        {
            RestoreEnvironmentVariables();

            isDisposed = true;
        }
    }

    // Not strictly necessary - this class wraps only managed state - but deliberately added as a defensive
    // safety net: if a test forgets to Dispose() this instance, the finalizer still restores the original
    // environment variables on the next GC, rather than leaving the modified values to silently leak into
    // later, unrelated tests.
    ~ManageEnvironmentVariables()
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

}
