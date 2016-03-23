﻿using System;
using System.Diagnostics;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Persimmon.VisualStudio.TestRunner.Internals
{
    public sealed class DiscoverSinkTrampoline : MarshalByRefObject, ISinkTrampoline
    {
        private readonly string targetAssemblyPath_;
        private readonly ITestDiscoverSink parentSink_;

        internal DiscoverSinkTrampoline(string targetAssemblyPath, ITestDiscoverSink parentSink)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(targetAssemblyPath));
            Debug.Assert(parentSink != null);

            targetAssemblyPath_ = targetAssemblyPath;
            parentSink_ = parentSink;
        }

        public void Begin(string message)
        {
            parentSink_.Begin(message);
        }

        public void Progress(dynamic[] args)
        {
            var testCase = new TestCase(
                args[0],
                parentSink_.ExtensionUri,
                targetAssemblyPath_);

            SymbolInformation symbol = args[3];

            testCase.DisplayName = args[1];
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
