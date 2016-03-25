using System;

namespace Persimmon.VisualStudio.TestRunner.Internals
{
    [Serializable]
    public sealed class SymbolInformation
    {
        public readonly string SymbolName;
        public readonly string FileName;
        public readonly int MaxLineNumber;
        public readonly int MinLineNumber;

        public SymbolInformation(
            string symbolName,
            string fileName,
            int minLineNumber,
            int maxLineNumber)
        {
            this.SymbolName = symbolName;
            this.FileName = fileName;
            this.MaxLineNumber = maxLineNumber;
            this.MinLineNumber = minLineNumber;
        }
    }
}
