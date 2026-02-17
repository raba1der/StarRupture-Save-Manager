namespace StarRuptureSaveManager.Core.Tests.TestSupport;

internal sealed class ScopedEnvVar : IDisposable
{
    private readonly string _name;
    private readonly string? _oldValue;
    private readonly EnvironmentVariableTarget _target;

    public ScopedEnvVar(string name, string? value, EnvironmentVariableTarget target = EnvironmentVariableTarget.Process)
    {
        _name = name;
        _target = target;
        _oldValue = Environment.GetEnvironmentVariable(name, target);
        Environment.SetEnvironmentVariable(name, value, target);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(_name, _oldValue, _target);
    }
}
