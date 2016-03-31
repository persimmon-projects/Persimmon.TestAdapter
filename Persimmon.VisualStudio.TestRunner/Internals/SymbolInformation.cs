using System;

namespace Persimmon.VisualStudio.TestRunner.Internals
{
    [Serializable]
    public sealed class SymbolInformation
    {
        public readonly string SymbolName;
        public readonly string FileName;
        public readonly int MinLineNumber;
        public readonly int MaxLineNumber;
        public readonly int MinColumnNumber;
        public readonly int MaxColumnNumber;

        public SymbolInformation(
            string symbolName,
            string fileName,
            int minLineNumber,
            int maxLineNumber,
            int minColumnNumber,
            int maxColumnNumber)
        {
            this.SymbolName = symbolName;
            this.FileName = fileName;
            this.MinLineNumber = minLineNumber;
            this.MaxLineNumber = maxLineNumber;
            this.MinColumnNumber = minColumnNumber;
            this.MaxColumnNumber = maxColumnNumber;
        }
    }
}
