namespace Persimmon.TestRunner.Internals
{
    public interface IDiscoverer
    {
        SymbolInformation[] Discover(string targetAssemblyPath);
    }
}
