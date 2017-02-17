using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
#if NETCORE
using System.Runtime.Loader;
#endif

namespace Persimmon.TestRunner.Internals
{
    /// <summary>
    /// Test assembly load/execute via remote AppDomain implementation.
    /// </summary>
    public sealed class RemotableTestExecutor
#if !NETCORE
        : MarshalByRefObject
#endif
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public RemotableTestExecutor()
        {
#if !NETCORE
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
#endif
        }

        /// <summary>
        /// Load target assembly and do action.
        /// </summary>
        /// <param name="targetAssemblyPath">Target assembly path</param>
        /// <param name="persimmonPartialAssemblyName">Target type name</param>
        /// <param name="persimmonTypeName">Target type name</param>
        /// <param name="sinkTrampoline">Execution logger interface</param>
        /// <param name="rawAction">Action delegate(TestCollector, TestAssembly)</param>
        private void InternalExecute(
            string targetAssemblyPath,
            string persimmonPartialAssemblyName,
            string persimmonTypeName,
            ISinkTrampoline sinkTrampoline,
            Action<dynamic, Assembly> rawAction)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(targetAssemblyPath));
            Debug.Assert(!string.IsNullOrWhiteSpace(persimmonPartialAssemblyName));
            Debug.Assert(!string.IsNullOrWhiteSpace(persimmonTypeName));
            Debug.Assert(sinkTrampoline != null);
            Debug.Assert(rawAction != null);

#if !NETCORE
            Debug.Assert(
                Path.GetDirectoryName(targetAssemblyPath) == AppDomain.CurrentDomain.BaseDirectory);
#endif

            sinkTrampoline.Begin(targetAssemblyPath);

            try
            {
#if NETCORE
                var testAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(targetAssemblyPath);
#else
                // 1. pre-load target assembly and analyze fully-qualified assembly name.
                //   --> Assebly.ReflectionOnlyLoadFrom() is load assembly into reflection-only context.
                var preLoadAssembly = Assembly.ReflectionOnlyLoadFrom(targetAssemblyPath);
                var assemblyFullName = preLoadAssembly.FullName;

                // 2. load assembly by fully-qualified assembly name.
                //   --> Assembly.Load() is load assembly into "default context."
                //   --> Failed if current AppDomain.ApplicationBase folder is not target assembly path.
                var testAssembly = Assembly.Load(assemblyFullName);
#endif

                // 3. extract Persimmon assembly name via test assembly,
                foreach (var persimmonFullAssemblyName in
                    testAssembly.GetReferencedAssemblies().
                        Where(assembly =>
                            (assembly.Name == persimmonPartialAssemblyName) &&
                            (assembly.GetPublicKeyToken() != null)))
                {
                    //   and load persimmon assembly.
                    var persimmonAssembly = Assembly.Load(persimmonFullAssemblyName);

                    // 4. Instantiate TestCollector/TestRunner class (by dynamic), and do action.
                    //   --> Because TestCollector/TestRunner class containing assembly version is unknown,
                    //       so this TestRunner assembly can't statically refering The Persimmon assembly...
                    var persimmonType = persimmonAssembly.GetType(persimmonTypeName);
                    if (persimmonType != null)
                    {
                        dynamic persimmonInstance = Activator.CreateInstance(persimmonType);
                        rawAction(persimmonInstance, testAssembly);

                        break;
                    }

                    var message = string.Format(
                        "Persimmon.VisualStudio.TestRunner: Cannot found required type (May be mismatched persimmon version) : TargetPath=\"{0}\", PersimmonPath=\"{1}\"",
                        targetAssemblyPath,
                        new Uri(persimmonAssembly.CodeBase).LocalPath);

                    Trace.WriteLine(message);
                    sinkTrampoline.Message(true, message);
                }
            }
            catch (Exception ex)
            {
                var message = string.Format(
                    "Persimmon.TestRunner: {0}: Type={1} TargetPath=\"{2}\", StackTrace={3}",
                    ex.Message,
                    ex.GetType().FullName,
                    targetAssemblyPath,
                    ex.StackTrace);

                Trace.WriteLine(message);
                sinkTrampoline.Message(true, message);
            }

            sinkTrampoline.Finished(targetAssemblyPath);
        }

        /// <summary>
        /// Discover tests target assembly.
        /// </summary>
        /// <param name="targetAssemblyPath">Target assembly path</param>
        /// <param name="sinkTrampoline">Execution logger interface</param>
        public void Discover(
            string targetAssemblyPath,
            ISinkTrampoline sinkTrampoline)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(targetAssemblyPath));
            Debug.Assert(sinkTrampoline != null);

            Debug.WriteLine(string.Format(
               "{0}: Discover: TargetAssembly={1}",
               this.GetType().FullName,
               targetAssemblyPath));

            // Callback delegate: testCase is ITestCase.
            var callback = new Action<dynamic>(testCase =>
            {
                string fullyQualifiedTestName = testCase.UniqueName;
                string symbolName = testCase.SymbolName;
                string displayName = testCase.DisplayName;

                // Re-construct results by safe serializable type. (object array)
                sinkTrampoline.Progress(new dynamic[]
                {
                    fullyQualifiedTestName,
                    symbolName,
                    displayName
                });
            });

            this.InternalExecute(
                targetAssemblyPath,
                "Persimmon",
                "Persimmon.Internals.TestCollector",
                sinkTrampoline,
                (testCollector, testAssembly) => testCollector.CollectAndCallback(
                    testAssembly,
                    callback));
        }

        /// <summary>
        /// Run tests target assembly.
        /// </summary>
        /// <param name="targetAssemblyPath">Target assembly path</param>
        /// <param name="fullyQualifiedTestNames">Target test names. Run all tests if empty.</param>
        /// <param name="sinkTrampoline">Execution logger interface</param>
        /// <param name="token">CancellationToken</param>
        private void InternalRun(
            string targetAssemblyPath,
            string[] fullyQualifiedTestNames,
            ISinkTrampoline sinkTrampoline,
            CancellationToken token)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(targetAssemblyPath));
            Debug.Assert(fullyQualifiedTestNames != null);
            Debug.Assert(sinkTrampoline != null);
            Debug.Assert(token != null);

            // Callback delegate: testResult is ITestResult.
            var callback = new Action<dynamic>(testResult =>
            {
                token.ThrowIfCancellationRequested();

                string fullyQualifiedTestName = testResult.TestCase.UniqueName;
                string symbolName = testResult.TestCase.SymbolName;
                string displayName = testResult.TestCase.DisplayName;

                // Re-construct results by safe serializable type. (object array)
                sinkTrampoline.Progress(new[]
                {
                    fullyQualifiedTestName,
                    symbolName,
                    displayName,
                    testResult.Exceptions, // TODO: exn may failed serialize. try convert safe types...
                    testResult.SkipMessages,
                    testResult.FailureMessages,
                    testResult.Duration
                });
            });

            this.InternalExecute(
                targetAssemblyPath,
                "Persimmon",
                "Persimmon.Internals.TestRunner",
                sinkTrampoline,
                (testRunner, testAssembly) => testRunner.RunTestsAndCallback(
                    testAssembly,
                    fullyQualifiedTestNames,
                    callback));
        }

        /// <summary>
        /// Run tests target assembly.
        /// </summary>
        /// <param name="targetAssemblyPath">Target assembly path</param>
        /// <param name="fullyQualifiedTestNames">Target test names. Run all tests if empty.</param>
        /// <param name="sinkTrampoline">Execution logger interface</param>
        /// <param name="token">CancellationToken</param>
        public void Run(
            string targetAssemblyPath,
            string[] fullyQualifiedTestNames,
            ISinkTrampoline sinkTrampoline,
            RemoteCancellationToken token)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(targetAssemblyPath));
            Debug.Assert(fullyQualifiedTestNames != null);
            Debug.Assert(sinkTrampoline != null);
            Debug.Assert(token != null);

            Debug.WriteLine(string.Format(
                "{0}: Run: TargetAssembly={1}",
                this.GetType().FullName,
                targetAssemblyPath));

            this.InternalRun(targetAssemblyPath, fullyQualifiedTestNames, sinkTrampoline, token);
        }
    }
}
