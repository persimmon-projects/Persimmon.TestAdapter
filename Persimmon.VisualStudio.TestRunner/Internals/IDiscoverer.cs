namespace Persimmon.VisualStudio.TestRunner.Internals
{
    public interface IDiscoverer
    {
        SymbolInformation[] Discover(string targetAssemblyPath);
    }
}
