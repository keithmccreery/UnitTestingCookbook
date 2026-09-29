namespace UnitTestingCookbook.TestHelpers;

/// <summary>
/// ManageEnvironmentVariables
/// </summary>
public class ManageEnvironmentVariables : IDisposable
{
    private readonly Dictionary<string, string?> _originalEnvironmentVariables = new Dictionary<string, string?>(StringComparer.Ordinal);

    public ManageEnvironmentVariables() { }

    public ManageEnvironmentVariables(Dictionary<string, string?> environmentVariables)
    {
        SetEnvironmentVariables(environmentVariables);
    }

    public void SetEnvironmentVariables(Dictionary<string, string?> environmentVariables)
    {
        environmentVariables
            .ToList()
            .ForEach(kvp => SetEnvironmentVariable(kvp.Key, kvp.Value));
    }

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

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion

}
