//MIT License

//Copyright(c) 2016 interactsw

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.

// port from https://github.com/adamchester/expecto-adapter/blob/9.0.0/src/Expecto.VisualStudio.TestAdapter/SourceLocation.fs

#if NETCORE

using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil.Cil;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace Persimmon.TestRunner.Internals
{
    public sealed class SymbolInformationDiscoverer
    {
        const int lineNumberIndicatingHiddenLine = 0xfeefee;
        private readonly Dictionary<string, TypeDefinition> types;

        public SymbolInformationDiscoverer(string assemblyPath)
        {
            var readerParams = new ReaderParameters
            {
                ReadSymbols = true,
                InMemory = true
            };
            var moduleDefinition = ModuleDefinition.ReadModule(assemblyPath, readerParams);
            types = moduleDefinition.GetTypes().ToDictionary(t => t.FullName);
        }

        public SymbolInformation[] Discover()
        {
            return types.SelectMany(kv => Collect(kv.Key, kv.Value))
                .Where(v => v != null)
                .ToArray();
        }

        private IEnumerable<SymbolInformation> Collect(string className, TypeDefinition definition)
        {
            return definition.GetMethods()
                .Select(method =>
                {
                    var v = GetFirstOrDefaultSequencePoint(method);
                    if (v != null)
                    {
                        var symbolName = $"{className}.{method.Name}";
                        return new SymbolInformation(symbolName, v.Document.Url, v.StartLine, v.EndLine, v.StartColumn, v.EndColumn);
                    }
                    else
                    {
                        return null;
                    }
                })
                .Where(v => v != null);
        }

        private SequencePoint GetFirstOrDefaultSequencePoint(MethodDefinition m)
        {
            var mapping = m.DebugInformation.GetSequencePointMapping();

            var v = m.Body.Instructions
                .FirstOrDefault(i => IsStartDifferentThanHidden(GetSequencePoint(mapping, i)));
            if (v != null)
            {
                return GetSequencePoint(mapping, v);
            }
            else
            {
                return null;
            }
        }

        private SequencePoint GetSequencePoint(IDictionary<Instruction, SequencePoint> mapping, Instruction instruction)
        {
            mapping.TryGetValue(instruction, out var point);
            return point;
        }
        
        private bool IsStartDifferentThanHidden(SequencePoint seqPoint)
        {
            if (seqPoint == null)
            {
                return false;
            }
            else if (seqPoint.StartLine != lineNumberIndicatingHiddenLine)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}

#endif
