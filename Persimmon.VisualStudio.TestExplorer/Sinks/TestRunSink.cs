using System;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Persimmon.VisualStudio.TestRunner;

namespace Persimmon.VisualStudio.TestExplorer.Sinks
{
    internal sealed class TestRunSink : ITestRunSink
    {
        private readonly IRunContext runContext_;
        private readonly IFrameworkHandle frameworkHandle_;

        public TestRunSink(
            IRunContext runContext,
            IFrameworkHandle frameworkHandle)
        {
            runContext_ = runContext;
            frameworkHandle_ = frameworkHandle;
        }

        public Uri ExtensionUri
        {
            get { return Constant.ExtensionUri; }
        }

        public void Begin(string message)
        {
            frameworkHandle_.SendMessage(
                TestMessageLevel.Informational,
                string.Format("Begin tests: Path={0}", message));
        }

        public void Message(bool isError, string message)
        {
            frameworkHandle_.SendMessage(
                isError ? TestMessageLevel.Error : TestMessageLevel.Informational,
                message);
        }

        public void Progress(TestResult testResult)
        {
            frameworkHandle_.RecordResult(testResult);
        }

        public void Finished(string message)
        {
            frameworkHandle_.SendMessage(
                TestMessageLevel.Informational,
                string.Format("Finished tests: Path={0}", message));
        }
    }
}
