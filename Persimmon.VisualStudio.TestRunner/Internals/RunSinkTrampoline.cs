using System;
using System.Collections.Generic;
using System.Diagnostics;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Persimmon.VisualStudio.TestRunner.Internals
{
    public sealed class RunSinkTrampoline : MarshalByRefObject, ISinkTrampoline
    {
        private readonly string targetAssemblyPath_;
        private readonly ITestRunSink parentSink_;
        private readonly Dictionary<string, TestCase> testCases_;

        internal RunSinkTrampoline(
            string targetAssemblyPath,
            ITestRunSink parentSink,
            Dictionary<string, TestCase> testCases)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(targetAssemblyPath));
            Debug.Assert(parentSink != null);
            Debug.Assert(testCases != null);

            targetAssemblyPath_ = targetAssemblyPath;
            parentSink_ = parentSink;
            testCases_ = testCases;
        }

        public void Begin(string message)
        {
            parentSink_.Begin(message);
        }

        public void Progress(dynamic[] args)
        {
            string fullyQualifiedTestName = args[0];
            string symbolName = args[1];
            string displayName = args[2];
            Exception[] exceptions = args[3];
            TimeSpan duration = args[4];

            TestCase testCase;
            if (testCases_.TryGetValue(fullyQualifiedTestName, out testCase) == false)
            {
                Debug.WriteLine(string.Format(
                    "TestCase lookup failed: FQTN=\"{0}\", SymbolName=\"{1}\", DisplayName=\"{2}\"",
                    fullyQualifiedTestName,
                    symbolName,
                    displayName));

                // Invalid fqtn, try create only basic informational TestCase...
                //Debug.Fail("Not valid fqtn: " + fqtn);
                testCase = new TestCase(
                    fullyQualifiedTestName,
                    parentSink_.ExtensionUri,
                    targetAssemblyPath_);
                testCase.DisplayName = displayName;
            }

            var testResult = new TestResult(testCase);

            // TODO: Other outcome require handled.
            //   Strategy: testCases_ included target test cases,
            //     so match and filter into Finished(), filtered test cases marking TestOutcome.Notfound.
            testResult.Outcome = (exceptions.Length >= 1) ? TestOutcome.Failed : TestOutcome.Passed;
            testResult.Duration = duration;

            parentSink_.Progress(testResult);
        }

        public void Finished(string message)
        {
            parentSink_.Finished(message);
        }
    }
}
