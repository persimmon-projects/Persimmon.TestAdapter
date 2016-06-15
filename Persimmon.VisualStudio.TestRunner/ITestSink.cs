using System;

namespace Persimmon.VisualStudio.TestRunner
{
    public interface ITestSink
    {
        Uri ExtensionUri { get; }

        void Begin(string message);

        void Message(bool isError, string message);

        void Finished(string message);
    }
}
