using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dia;

namespace Persimmon.VisualStudio.TestRunner.Internals
{
    internal sealed class PdbReader
    {
        private readonly Dictionary<string, List<SymbolInformation>> symbols_ =
            new Dictionary<string, List<SymbolInformation>>();

        public PdbReader()
        {
        }

        private static string Intern(Dictionary<string, string> internes, string value)
        {
            string interned;
            if (internes.TryGetValue(value, out interned) == true)
            {
                return interned;
            }

            internes.Add(value, value);
            return value;
        }

        private SymbolInformation ToSymbolInformation(
            ComDisposer<IDiaSymbol> symbol,
            SymbolInformation parentSymbolInformation,
            SymTagEnum tag,
            ComDisposer<IDiaSession> diaSession,
            Dictionary<string, string> internes)
        {
            IDiaEnumLineNumbers deln;
            diaSession.Instance.findLinesByAddr(
                symbol.Instance.addressSection,
                symbol.Instance.addressOffset,
                (uint)symbol.Instance.length,
                out deln);
            using (var lineNumbers = Hold(deln))
            {
                string fileName = null;
                var minLineNumber = int.MaxValue;
                var maxLineNumber = int.MinValue;

                IDiaLineNumber dln;
                uint fetched;
                lineNumbers.Instance.Next(1, out dln, out fetched);
                while ((fetched == 1) && (dln != null))
                {
                    using (var lineNumber = Hold(dln))
                    {
                        if ((lineNumber.Instance.lineNumber < 0xfeefee) &&
                            (lineNumber.Instance.lineNumberEnd < 0xfeefee))
                        {
                            using (var sourceFile = Hold(lineNumber.Instance.sourceFile))
                            {
                                fileName = sourceFile.Instance.fileName;
                                minLineNumber = Math.Min(
                                    minLineNumber,
                                    (int)lineNumber.Instance.lineNumber);
                                maxLineNumber = Math.Max(
                                    maxLineNumber,
                                    (int)lineNumber.Instance.lineNumberEnd);
                            }
                        }
                    }

                    lineNumbers.Instance.Next(1, out dln, out fetched);
                }

                var demangledSymbolName = symbol.Instance.name.Replace("::", ".");
                demangledSymbolName = (parentSymbolInformation != null) ?
                    (parentSymbolInformation.SymbolName + "." + demangledSymbolName) :
                    demangledSymbolName;

                demangledSymbolName = Intern(internes, demangledSymbolName);
                if (fileName != null)
                {
                    fileName = Intern(internes, fileName);
                }

                return new SymbolInformation(
                    tag,
                    demangledSymbolName,
                    fileName,
                    minLineNumber,
                    maxLineNumber);
            }
        }

        private static IEnumerable<ComDisposer<IDiaSymbol>> EnumerateSymbolsByTag(
            ComDisposer<IDiaSymbol> parent,
            SymTagEnum tag)
        {
            IDiaEnumSymbols dess;
            parent.Instance.findChildren(tag, null, 0, out dess);
            using (var symbols = Hold(dess))
            {
                IDiaSymbol s;
                uint fetched;
                symbols.Instance.Next(1, out s, out fetched);
                while ((fetched == 1) && (s != null))
                {
                    using (var symbol = Hold(s))
                    {
                        yield return symbol;
                    }

                    symbols.Instance.Next(1, out s, out fetched);
                }
            }
        }

        private void ReadSymbols(
            ComDisposer<IDiaSession> diaSession,
            ComDisposer<IDiaSymbol> parentSymbol,
            SymbolInformation parentSymbolInformation,
            Dictionary<string, string> internes)
        {
#if true
            IDiaEnumTables det;
            diaSession.Instance.getEnumTables(out det);
            using (var tables = Hold(det))
            {
                IDiaTable dt = null;
                uint fetched = 0;
                tables.Instance.Next(1, ref dt, ref fetched);
                while ((fetched == 1) && (dt != null))
                {
                    using (var table = Hold(dt))
                    {
                        IDiaEnumSymbols dess = table.Instance as IDiaEnumSymbols;
                        if (dess != null)
                        {
                            IDiaSymbol s;
                            uint fetched2;
                            dess.Next(1, out s, out fetched2);
                            while ((fetched2 == 1) && (s != null))
                            {
                                using (var symbol = Hold(s))
                                {
                                    if (string.IsNullOrWhiteSpace(symbol.Instance.name) == false)
                                    {
                                        var symbolInformation = ToSymbolInformation(
                                            symbol,
                                            parentSymbolInformation,
                                            (SymTagEnum)symbol.Instance.symTag,
                                            diaSession,
                                            internes);

                                        List<SymbolInformation> list;
                                        if (symbols_.TryGetValue(symbolInformation.SymbolName, out list) == false)
                                        {
                                            list = new List<SymbolInformation>();
                                            symbols_.Add(symbolInformation.SymbolName, list);
                                        }

                                        list.Add(symbolInformation);
                                    }
                                }

                                dess.Next(1, out s, out fetched2);
                            }
                        }
                    }

                    dt = null;
                    tables.Instance.Next(1, ref dt, ref fetched);
                }
            }
#else
    // Traverse any symbols.
            for (var tag = (SymTagEnum)1; tag < SymTagEnum.SymTagMax; tag++)
            {
                foreach (var symbol in EnumerateSymbolsByTag(parentSymbol, tag))
                {
                    if (string.IsNullOrWhiteSpace(symbol.Instance.name) == false)
                    {
                        var symbolInformation = ToSymbolInformation(
                            symbol,
                            parentSymbolInformation,
                            tag,
                            diaSession,
                            internes);
                        if (symbols_.ContainsKey(symbolInformation.SymbolName) == false)
                        {
                            symbols_.Add(symbolInformation.SymbolName, symbolInformation);

                            // Traverse nested recursivity.
                            this.ReadSymbols(diaSession, symbol, symbolInformation, internes);
                        }
                    }
                }
            }
#endif
        }

        private void InternalRead(ComDisposer<IDiaSession> diaSession)
        {
            // Cache equal strings.
            var internes = new Dictionary<string, string>();

            using (var globalSymbol = Hold(diaSession.Instance.globalScope))
            {
                this.ReadSymbols(diaSession, globalSymbol, null, internes);
            }
        }

        public bool TryRead(string executablePath)
        {
            try
            {
                using (var diaSource = Hold(new EmbDiaSourceClass()))
                {
                    diaSource.Instance.loadDataForExe(executablePath, null, null);

                    IDiaSession ds;
                    diaSource.Instance.openSession(out ds);
                    using (var diaSession = Hold(ds))
                    {
                        this.InternalRead(diaSession);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                return false;
            }

            return true;
        }

        public SymbolInformation GetSymbolInformation(string symbolName)
        {
            List<SymbolInformation> list;
            if (symbols_.TryGetValue(symbolName, out list) == false)
            {
                return null;
            }

            return list.FirstOrDefault(si => si.FileName != null) ?? list[0];
        }

        #region ComDisposer
        private sealed class ComDisposer<T> : IDisposable
            where T : class
        {
            private T comInterface_;

            public ComDisposer(T comInterface)
            {
                comInterface_ = comInterface;
            }

            public void Dispose()
            {
                if (comInterface_ != null)
                {
                    Marshal.ReleaseComObject(comInterface_);
                    comInterface_ = null;
                }
            }

            public T Instance
            {
                get
                {
                    return comInterface_;
                }
            }
        }

        private static ComDisposer<T> Hold<T>(T comInterface)
            where T : class
        {
            return new ComDisposer<T>(comInterface);
        }
        #endregion

        #region Interops
        [CoClass(typeof(EmbDiaSourceClass))]
        [Guid("79F1BB5F-B66E-48E5-B6A9-1545C323CA3D")]
        [ComImport]
        public interface EmbDiaSource : IDiaDataSource
        {
        }

        [ClassInterface(ClassInterfaceType.None)]
        [Guid("3BFCEA48-620F-4B6B-81F7-B9AF75454C7D")]
        [TypeLibType(TypeLibTypeFlags.FCanCreate)]
        [ComImport]
        public class EmbDiaSourceClass : IDiaDataSource, EmbDiaSource
        {
            [DispId(1)]
            public virtual extern string lastError
            {
                [MethodImpl(MethodImplOptions.InternalCall)]
                [return: MarshalAs(UnmanagedType.BStr)]
                get;
            }

            [MethodImpl(MethodImplOptions.InternalCall)]
            public virtual extern void loadDataFromPdb([MarshalAs(UnmanagedType.LPWStr)] [In] string pdbPath);

            [MethodImpl(MethodImplOptions.InternalCall)]
            public virtual extern void loadAndValidateDataFromPdb([MarshalAs(UnmanagedType.LPWStr)] [In] string pdbPath, [In] ref Guid pcsig70, [In] uint sig, [In] uint age);

            [MethodImpl(MethodImplOptions.InternalCall)]
            public virtual extern void loadDataForExe([MarshalAs(UnmanagedType.LPWStr)] [In] string executable, [MarshalAs(UnmanagedType.LPWStr)] [In] string searchPath, [MarshalAs(UnmanagedType.IUnknown)] [In] object pCallback);

            [MethodImpl(MethodImplOptions.InternalCall)]
            public virtual extern void loadDataFromIStream([MarshalAs(UnmanagedType.Interface)] [In] IStream pIStream);

            [MethodImpl(MethodImplOptions.InternalCall)]
            public virtual extern void openSession([MarshalAs(UnmanagedType.Interface)] out IDiaSession ppSession);

            [MethodImpl(MethodImplOptions.InternalCall)]
            public virtual extern void loadDataFromCodeViewInfo([MarshalAs(UnmanagedType.LPWStr)] [In] string executable, [MarshalAs(UnmanagedType.LPWStr)] [In] string searchPath, [In] uint cbCvInfo, [In] ref byte pbCvInfo, [MarshalAs(UnmanagedType.IUnknown)] [In] object pCallback);

            [MethodImpl(MethodImplOptions.InternalCall)]
            public virtual extern void loadDataFromMiscInfo([MarshalAs(UnmanagedType.LPWStr)] [In] string executable, [MarshalAs(UnmanagedType.LPWStr)] [In] string searchPath, [In] uint timeStampExe, [In] uint timeStampDbg, [In] uint sizeOfExe, [In] uint cbMiscInfo, [In] ref byte pbMiscInfo, [MarshalAs(UnmanagedType.IUnknown)] [In] object pCallback);
        }
        #endregion
    }
}
