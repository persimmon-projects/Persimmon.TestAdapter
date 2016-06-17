using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

namespace Persimmon.VisualStudio.TestRunner.Internals
{
    public sealed class StrongNameCollector : MarshalByRefObject
    {
        public StrongNameCollector()
        {
            Debug.Assert(this.GetType().Assembly.GlobalAssemblyCache);

            Debug.WriteLine(string.Format(
                "{0}: constructed: Process={1}, Thread=[{2},{3}], AppDomain=[{4},{5},{6}]",
                this.GetType().FullName,
                Process.GetCurrentProcess().Id,
                Thread.CurrentThread.ManagedThreadId,
                Thread.CurrentThread.Name,
                AppDomain.CurrentDomain.Id,
                AppDomain.CurrentDomain.FriendlyName,
                AppDomain.CurrentDomain.BaseDirectory));
        }

        public string CollectFrom(string targetAssemblyPath)
        {
            Debug.Assert(string.IsNullOrWhiteSpace(targetAssemblyPath) == false);

            // pre-load target assembly and analyze fully-qualified assembly name.
            //   --> Assebly.ReflectionOnlyLoadFrom() is load assembly into reflection-only context.
            var preLoadAssembly = Assembly.ReflectionOnlyLoadFrom(targetAssemblyPath);
            return preLoadAssembly.FullName;
        }
    }
}
