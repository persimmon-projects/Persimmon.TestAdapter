using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Persimmon.TestRunner.Internals
{
    public sealed class DiscoverSinkTrampoline :
        MarshalByRefObject,
        ISinkTrampoline,
        IConvertible
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

        public TypeCode GetTypeCode()
        {
            return TypeCode.Object;
        }

        public bool ToBoolean(IFormatProvider provider)
        {
            return ThrowInvalidCast<bool>();
        }

        public char ToChar(IFormatProvider provider)
        {
            return ThrowInvalidCast<char>();
        }

        public sbyte ToSByte(IFormatProvider provider)
        {
            return ThrowInvalidCast<sbyte>();
        }

        public byte ToByte(IFormatProvider provider)
        {
            return ThrowInvalidCast<byte>();
        }

        public short ToInt16(IFormatProvider provider)
        {
            return ThrowInvalidCast<short>();
        }

        public ushort ToUInt16(IFormatProvider provider)
        {
            return ThrowInvalidCast<ushort>();
        }

        public int ToInt32(IFormatProvider provider)
        {
            return ThrowInvalidCast<int>();
        }

        public uint ToUInt32(IFormatProvider provider)
        {
            return ThrowInvalidCast<uint>();
        }

        public long ToInt64(IFormatProvider provider)
        {
            return ThrowInvalidCast<long>();
        }

        public ulong ToUInt64(IFormatProvider provider)
        {
            return ThrowInvalidCast<ulong>();
        }

        public float ToSingle(IFormatProvider provider)
        {
            return ThrowInvalidCast<float>();
        }

        public double ToDouble(IFormatProvider provider)
        {
            return ThrowInvalidCast<double>();
        }

        public decimal ToDecimal(IFormatProvider provider)
        {
            return ThrowInvalidCast<decimal>();
        }

        public DateTime ToDateTime(IFormatProvider provider)
        {
            return ThrowInvalidCast<DateTime>();
        }

        public string ToString(IFormatProvider provider)
        {
            return ThrowInvalidCast<string>();
        }

        public object ToType(Type conversionType, IFormatProvider provider)
        {
            if (conversionType == typeof(ISinkTrampoline) || conversionType == typeof(DiscoverSinkTrampoline))
            {
                return this;
            }
            return ThrowInvalidCast(conversionType);
        }

        private T ThrowInvalidCast<T>()
        {
            return (T)ThrowInvalidCast(typeof(T));
        }

        private object ThrowInvalidCast(Type type)
        {
            throw new InvalidCastException($"DiscoverSinkTrampoline does not cast {type.FullName}.");
        }
    }
}
