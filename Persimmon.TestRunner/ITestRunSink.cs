using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Persimmon.TestRunner
{
    public interface ITestRunSink : ITestSink
    {
        void Progress(TestResult testResult);
    }
}
