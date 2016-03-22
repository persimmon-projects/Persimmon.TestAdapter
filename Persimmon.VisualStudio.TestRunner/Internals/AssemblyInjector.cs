using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Persimmon.VisualStudio.TestRunner.Internals
{
    public sealed class AssemblyInjector : MarshalByRefObject
    {
        private readonly Dictionary<string, AssemblyName> loadedAssemblies_;

        public AssemblyInjector(AssemblyName[] names)
        {
            Debug.Assert(names != null);

            loadedAssemblies_ = names.ToDictionary(name => name.FullName);

            AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve;

            Debug.WriteLine(string.Format(
                "AssemblyInjector: Hook: Current={0}", AppDomain.CurrentDomain));
        }

        private Assembly AssemblyResolve(object sender, ResolveEventArgs e)
        {
            AssemblyName name;
            lock (loadedAssemblies_)
            {
                if (loadedAssemblies_.TryGetValue(e.Name, out name) == false)
                {
                    Debug.WriteLine(string.Format(
                        "AssemblyInjector: Not found: RequireName={0}, Requesting={1}, Current={2}",
                        e.Name,
                        e.RequestingAssembly,
                        AppDomain.CurrentDomain));

                    return null;
                }
            }

            Debug.WriteLine(string.Format(
                "AssemblyInjector: Try to load: RequireName={0}, Name={1}, Requesting={2}, Current={3}",
                e.Name,
                name,
                e.RequestingAssembly,
                AppDomain.CurrentDomain));

            var assembly = Assembly.Load(name);

            Debug.WriteLine(string.Format(
                "AssemblyInjector: Loaded: RequireName={0}, Name={1}, Loaded={2}, Requesting={3}, Current={4}",
                e.Name,
                name,
                assembly.FullName,
                e.RequestingAssembly,
                AppDomain.CurrentDomain));

            return assembly;
        }
    }
}
