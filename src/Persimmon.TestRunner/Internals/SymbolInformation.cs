using System;
using System.IO;

namespace Persimmon.TestRunner.Internals
{
    /// <summary>
    /// Source code symbol informations from TestDiscoverer.
    /// </summary>
    [Serializable]
    public sealed class SymbolInformation : IEquatable<SymbolInformation>
    {
        public readonly string SymbolName;
        public readonly string FileName;
        public readonly int MinLineNumber;
        public readonly int MaxLineNumber;
        public readonly int MinColumnNumber;
        public readonly int MaxColumnNumber;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="symbolName">Symbol name</param>
        /// <param name="fileName">Source code file name</param>
        /// <param name="minLineNumber">Symbol name minimum line number</param>
        /// <param name="maxLineNumber">Symbol name maximum line number</param>
        /// <param name="minColumnNumber">Symbol name minimum column number</param>
        /// <param name="maxColumnNumber">Symbol name maximum column number</param>
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

        /// <summary>
        /// Is instance equals.
        /// </summary>
        /// <param name="other">Other instance</param>
        /// <returns>True if equal.</returns>
        public bool Equals(SymbolInformation other)
        {
            if (other == null)
            {
                return false;
            }

            return
                this.SymbolName.Equals(other.SymbolName) &&
                this.FileName.ToLowerInvariant().Equals(other.FileName.ToLowerInvariant()) &&
                this.MinLineNumber.Equals(other.MinLineNumber) &&
                this.MinColumnNumber.Equals(other.MinColumnNumber) &&
                this.MaxLineNumber.Equals(other.MaxLineNumber) &&
                this.MaxColumnNumber.Equals(other.MaxColumnNumber);
        }

        /// <summary>
        /// Get hash code.
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            return
                this.SymbolName.GetHashCode() ^
                this.FileName.ToLowerInvariant().GetHashCode() ^
                this.MinLineNumber.GetHashCode();
        }

        /// <summary>
        /// Get instance string.
        /// </summary>
        /// <returns>String</returns>
        public override string ToString()
        {
            return string.Format(
                "{0}({1},{2}): {3}",
                Path.GetFileName(this.FileName),
                this.MinLineNumber,
                this.MinColumnNumber,
                this.SymbolName);
        }
    }
}
