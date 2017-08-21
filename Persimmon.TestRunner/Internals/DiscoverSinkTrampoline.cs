using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Persimmon.TestRunner.Internals
{
    public sealed class DiscoverSinkTrampoline :
        MarshalByRefObject,
        ISinkTrampoline
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

        public void Message(bool isError, string message)
        {
            parentSink_.Message(isError, message);
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

            Debug.WriteLine(string.Format(
                "DiscoverSinkTrampoline: FQTN={0}, DisplayName={1}, File={2}, Position={3}",
                fullyQualifiedTestName,
                displayName,
                (testCase.CodeFilePath != null) ? Path.GetFileName(testCase.CodeFilePath) : "(null)",
                testCase.LineNumber));

            parentSink_.Progress(testCase);
        }

        public void Finished(string message)
        {
            parentSink_.Finished(message);
        }
    }
}
