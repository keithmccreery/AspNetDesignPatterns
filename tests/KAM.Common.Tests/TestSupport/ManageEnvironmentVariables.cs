namespace KAM.Common.Tests.TestSupport;

/// <summary>
/// Sets process environment variables for the duration of a test and restores their original
/// values on <see cref="Dispose"/>. Use with a <c>using</c> block around code that reads
/// <see cref="Environment.GetEnvironmentVariable(string)"/> directly.
/// </summary>
public sealed class ManageEnvironmentVariables : IDisposable
{
    private readonly Dictionary<string, string?> _original = [];

    public ManageEnvironmentVariables(IDictionary<string, string?> variables)
    {
        foreach ((string key, string? value) in variables)
        {
            _original[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    public void Dispose()
    {
        foreach ((string key, string? value) in _original)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
