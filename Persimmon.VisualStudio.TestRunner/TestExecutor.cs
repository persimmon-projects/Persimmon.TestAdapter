using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Persimmon.VisualStudio.TestRunner.Internals;

namespace Persimmon.VisualStudio.TestRunner
{
    /// <summary>
    /// Test assembly load/execute facade.
    /// </summary>
    public sealed class TestExecutor
    {
        private static readonly Type assemblyInjectorType_ = typeof(AssemblyInjector);
        private static readonly Type remotableExecutorType_ = typeof(RemotableTestExecutor);
        private static readonly string testRunnerAssemblyPath_ = remotableExecutorType_.Assembly.Location;

        /// <summary>
        /// Constructor.
        /// </summary>
        public TestExecutor()
        {
            Debug.Assert(this.GetType().Assembly.GlobalAssemblyCache);
        }

        /// <summary>
        /// Test execute target assembly.
        /// </summary>
        /// <param name="targetAssemblyPath">Target assembly path</param>
        /// <param name="action">Action</param>
        private Task InternalExecuteAsync(
            string targetAssemblyPath,
            Action<RemotableTestExecutor> action)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(targetAssemblyPath));
            Debug.Assert(action != null);

            return Task.Run(() =>
            {
                // Strategy: Shadow copy information:
                //   https://msdn.microsoft.com/en-us/library/ms404279%28v=vs.110%29.aspx

                // Execution context id (for diagnose).
                var contextId = Guid.NewGuid();

                // ApplicationBase path.
                // Important: Change from current AppDomain.ApplicationBase,
                //   may be stable execution test assemblies.
                var applicationBasePath = Path.GetDirectoryName(targetAssemblyPath);

                // Shadow copy target paths.
                var shadowCopyTargets = string.Join(
                    ";",
                    new[]
                {
                    applicationBasePath,
                    Path.GetDirectoryName(testRunnerAssemblyPath_)
                });

                // AppDomain name.
                var separatedAppDomainName = string.Format(
                    "{0}-{1}",
                    this.GetType().FullName,
                    contextId);

                // AppDomainSetup informations.
                var separatedAppDomainSetup = new AppDomainSetup
                {
                    ApplicationName = separatedAppDomainName,
                    ApplicationBase = applicationBasePath,
                    ShadowCopyFiles = "true",
                    ShadowCopyDirectories = shadowCopyTargets
                };

                // If test assembly has configuration file, try to set.
                var configurationFilePath = targetAssemblyPath + ".config";
                if (File.Exists(configurationFilePath))
                {
                    Debug.WriteLine(string.Format(
                        "Persimmon test runner: Try to set configuration file: Path={0}", configurationFilePath));

                    separatedAppDomainSetup.ConfigurationFile = configurationFilePath;
                }

                // Derived current evidence.
                // (vstest agent may be full trust...)
                var separatedAppDomainEvidence = new Evidence(AppDomain.CurrentDomain.Evidence);

                // Create AppDomain.
                var separatedAppDomain = AppDomain.CreateDomain(
                    separatedAppDomainName,
                    separatedAppDomainEvidence,
                    separatedAppDomainSetup);

                try
                {
#if false
                    // Create AssemblyInjector instance into new AppDomain.
                    var assemblyInjector = (AssemblyInjector)separatedAppDomain.CreateInstanceFromAndUnwrap(
                        testRunnerAssemblyPath_,
                        assemblyInjectorType_.FullName,
                        false,
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                        null,
                        new object[]
                        {
                            AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetName()).ToArray()
                        },
                        null,
                        null);
#endif
                    // Create RemotableTestExecutor instance into new AppDomain,
                    //   and get remote reference.
                    var remoteExecutor = (RemotableTestExecutor)separatedAppDomain.CreateInstanceFromAndUnwrap(
                        testRunnerAssemblyPath_,
                        remotableExecutorType_.FullName);

                    ///////////////////////////////////////////////////////////////////////////////////////////
                    // Execute via remote AppDomain

                    action(remoteExecutor);
                }
                finally
                {
                    // Discard AppDomain.
                    AppDomain.Unload(separatedAppDomain);
                }
            });
        }

        /// <summary>
        /// Test execute target assembly.
        /// </summary>
        /// <param name="targetAssemblyPath">Target assembly path</param>
        /// <param name="sink">Execution logger interface</param>
        public Task DiscoverAsync(
            string targetAssemblyPath,
            ITestDiscoverSink sink)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(targetAssemblyPath));
            Debug.Assert(sink != null);

            return this.InternalExecuteAsync(
                targetAssemblyPath,
                executor => executor.Discover(
                    targetAssemblyPath,
                    new DiscoverSinkTrampoline(targetAssemblyPath, sink)));
        }

        /// <summary>
        /// Test execute target assembly.
        /// </summary>
        /// <param name="targetAssemblyPath">Target assembly path</param>
        /// <param name="testCases">Target test cases.</param>
        /// <param name="sink">Execution logger interface</param>
        /// <param name="token">CancellationToken</param>
        public Task RunAsync(
            string targetAssemblyPath,
            ICollection<TestCase> testCases,
            ITestRunSink sink,
            CancellationToken token)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(targetAssemblyPath));
            Debug.Assert(testCases != null);
            Debug.Assert(sink != null);
            Debug.Assert(token != null);

            var fullyQualifiedTestNames = testCases.Select(testCase => testCase.FullyQualifiedName).ToArray();
            var testCaseDicts = testCases.ToDictionary(testCase => testCase.FullyQualifiedName);

            return this.InternalExecuteAsync(
                targetAssemblyPath,
                executor => executor.Run(
                    targetAssemblyPath,
                    fullyQualifiedTestNames,
                    new RunSinkTrampoline(targetAssemblyPath, sink, testCaseDicts),
                    token));
        }
    }
}
