using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Persimmon.TestRunner
{
    public interface ITestDiscoverSink : ITestSink
    {
        void Progress(TestCase testCase);
    }
}
