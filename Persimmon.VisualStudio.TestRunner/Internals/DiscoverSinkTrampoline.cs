using System;
using System.Collections.Generic;
using System.Diagnostics;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Persimmon.VisualStudio.TestRunner.Internals
{
    public sealed class DiscoverSinkTrampoline : MarshalByRefObject, ISinkTrampoline
    {
        private readonly string targetAssemblyPath_;
        private readonly ITestDiscoverSink parentSink_;
        private readonly Dictionary<string, SymbolInformation> symbolInformations_;

        internal DiscoverSinkTrampoline(
            string targetAssemblyPath,ITestDiscoverSink parentSink,
            Dictionary<string, SymbolInformation> symbolInformations)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(targetAssemblyPath));
            Debug.Assert(parentSink != null);

            targetAssemblyPath_ = targetAssemblyPath;
            parentSink_ = parentSink;
            symbolInformations_ = symbolInformations;
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

            SymbolInformation symbol;
            if (symbolInformations_.TryGetValue(symbolName, out symbol) == false)
            {
                Debug.WriteLine(string.Format(
                    "SymbolInformation lookup failed: FQTN=\"{0}\", SymbolName=\"{1}\", DisplayName=\"{2}\"",
                    fullyQualifiedTestName,
                    symbolName,
                    displayName));
            }

            var testCase = new TestCase(
                fullyQualifiedTestName,
                parentSink_.ExtensionUri,
                targetAssemblyPath_);
            testCase.DisplayName = displayName;
            testCase.CodeFilePath = (symbol != null) ? symbol.FileName : null;
            testCase.LineNumber = (symbol != null) ? symbol.MinLineNumber : 0;

            parentSink_.Progress(testCase);
        }

        public void Finished(string message)
        {
            parentSink_.Finished(message);
        }
    }
}
