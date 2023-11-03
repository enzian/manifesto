namespace Manifesto.AspNet.Manifests;

public interface IKeyspaceSource
{
    string GetKeySpace (string type, string version, string group);
}

internal class DelegateKeyspaceSource : IKeyspaceSource
{
    private readonly Func<string, string, string, string> _keyspaces;

    public DelegateKeyspaceSource(Func<string, string, string, string> keyspaces)
    {
        _keyspaces = keyspaces;
    }

    public string GetKeySpace(string kind, string group, string version)
    {
        return _keyspaces(kind, group, version);
    }
}